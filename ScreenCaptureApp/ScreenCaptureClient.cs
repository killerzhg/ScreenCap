using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Concurrent;
using FFmpeg.AutoGen;

namespace ScreenCaptureApp
{
    public partial class ScreenCaptureClient : Form
    {
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private bool isCapturing = false;
        private CancellationTokenSource cancellationTokenSource;

        // FFmpeg 上下文
        private unsafe AVCodecContext* codecContext;
        private unsafe AVFrame* frame;
        private unsafe AVPacket* packet;
        private unsafe SwsContext* swsContext;

        // 同步对象保护FFmpeg上下文
        private readonly object ffmpegLock = new object();

        // 异步处理队列
        private ConcurrentQueue<Bitmap> frameQueue = new ConcurrentQueue<Bitmap>();
        private SemaphoreSlim encodingSemaphore = new SemaphoreSlim(1); // 改为1，确保编码串行化

        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(ProcessDpiAwareness value);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint SRCCOPY = 0x00CC0020;

        public ScreenCaptureClient()
        {
            SetProcessDpiAwareness(ProcessDpiAwareness.ProcessPerMonitorDpiAware);
            InitializeComponent();
            InitializeFFmpeg();
        }

        public enum ProcessDpiAwareness
        {
            ProcessDpiUnaware = 0,
            ProcessSystemDpiAware = 1,
            ProcessPerMonitorDpiAware = 2
        }

        private unsafe void InitializeFFmpeg()
        {
            ffmpeg.RootPath = @".\x64";

            // 查找 VP9 编码器
            AVCodec* codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_VP9);
            if (codec == null)
                throw new InvalidOperationException("VP9 编码器未找到");

            codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (codecContext == null)
                throw new InvalidOperationException("无法分配编码器上下文");

            // VP9 优化编码参数
            codecContext->width = Screen.PrimaryScreen.Bounds.Width;
            codecContext->height = Screen.PrimaryScreen.Bounds.Height;
            codecContext->time_base = new AVRational { num = 1, den = 30 };
            codecContext->framerate = new AVRational { num = 30, den = 1 };
            codecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            codecContext->bit_rate = 3000000; // 3Mbps - VP9压缩率高，可以用较低码率
            codecContext->gop_size = 60; // 较大的GOP以利用VP9的压缩优势
            codecContext->max_b_frames = 0; // 禁用B帧减少延迟和复杂度

            // VP9 快速编码设置
            AVDictionary* opts = null;
            ffmpeg.av_dict_set(&opts, "speed", "8", 0);  // 最快速度 (0-8, 8最快)
            ffmpeg.av_dict_set(&opts, "tile-columns", "2", 0); // 启用并行编码
            ffmpeg.av_dict_set(&opts, "threads", "4", 0); // 使用4线程
            ffmpeg.av_dict_set(&opts, "deadline", "realtime", 0); // 实时编码模式
            ffmpeg.av_dict_set(&opts, "cpu-used", "8", 0); // CPU使用级别，8最快
            ffmpeg.av_dict_set(&opts, "row-mt", "1", 0); // 行级多线程

            // VP9 高质量设置
            //ffmpeg.av_dict_set(&opts, "speed", "4", 0);  // 平衡速度和质量 (0-8)
            //ffmpeg.av_dict_set(&opts, "tile-columns", "2", 0);
            //ffmpeg.av_dict_set(&opts, "threads", "4", 0);
            //ffmpeg.av_dict_set(&opts, "quality", "good", 0); // 使用good质量模式
            //ffmpeg.av_dict_set(&opts, "crf", "20", 0); // 恒定质量因子，数值越小质量越高
            //ffmpeg.av_dict_set(&opts, "b:v", "8M", 0); // 显式设置码率

            int ret = ffmpeg.avcodec_open2(codecContext, codec, &opts);
            ffmpeg.av_dict_free(&opts);

            if (ret < 0)
                throw new InvalidOperationException($"无法打开编码器: {ret}");

            frame = ffmpeg.av_frame_alloc();
            packet = ffmpeg.av_packet_alloc();

            if (frame == null || packet == null)
                throw new InvalidOperationException("无法分配帧或包");

            frame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
            frame->width = codecContext->width;
            frame->height = codecContext->height;

            int ret2 = ffmpeg.av_frame_get_buffer(frame, 32);
            if (ret2 < 0)
                throw new InvalidOperationException($"无法分配帧缓冲区: {ret2}");

            swsContext = ffmpeg.sws_getContext(
                codecContext->width, codecContext->height, AVPixelFormat.AV_PIX_FMT_BGR24,
                codecContext->width, codecContext->height, AVPixelFormat.AV_PIX_FMT_YUV420P,
                ffmpeg.SWS_FAST_BILINEAR, null, null, null); // 使用更快的插值算法

            if (swsContext == null)
                throw new InvalidOperationException("无法创建图像转换上下文");
        }

        // 更快的屏幕捕获方法
        private Bitmap CaptureScreenFast(int width, int height)
        {
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memoryDC = CreateCompatibleDC(screenDC);
            IntPtr bitmap = CreateCompatibleBitmap(screenDC, width, height);
            IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

            BitBlt(memoryDC, 0, 0, width, height, screenDC, 0, 0, SRCCOPY);

            SelectObject(memoryDC, oldBitmap);
            DeleteDC(memoryDC);
            ReleaseDC(IntPtr.Zero, screenDC);

            Bitmap result = Bitmap.FromHbitmap(bitmap);
            DeleteObject(bitmap);

            return result;
        }

        private void StopCapture()
        {
            isCapturing = false;
            cancellationTokenSource?.Cancel();

            // 清空队列
            while (frameQueue.TryDequeue(out Bitmap frame))
            {
                frame?.Dispose();
            }

            networkStream?.Close();
            tcpClient?.Close();

            startButton.Enabled = true;
            stopButton.Enabled = false;
            statusLabel.Text = "已停止";
            statusLabel.ForeColor = Color.Blue;
        }

        private async Task CaptureScreenAsync(CancellationToken cancellationToken)
        {
            try
            {
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
                int width = screenBounds.Width;
                int height = screenBounds.Height;

                byte[] header = System.Text.Encoding.UTF8.GetBytes($"{width}x{height}\n");
                await networkStream.WriteAsync(header, 0, header.Length, cancellationToken);

                int frameNumber = 0;

                // 启动编码器工作线程
                var encodingTask = Task.Run(() => ProcessEncodingQueue(cancellationToken), cancellationToken);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                while (isCapturing && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // 快速屏幕捕获
                        Bitmap screenshot = CaptureScreenFast(width, height);

                        // 如果队列太长，丢弃旧帧以保持实时性
                        if (frameQueue.Count > 5)
                        {
                            while (frameQueue.Count > 2 && frameQueue.TryDequeue(out Bitmap oldFrame))
                            {
                                oldFrame?.Dispose();
                            }
                        }

                        frameQueue.Enqueue(screenshot);
                        frameNumber++;

                        // 动态帧率控制
                        long elapsed = stopwatch.ElapsedMilliseconds;
                        int targetFrameTime = 33; // 30 FPS = 33ms per frame

                        if (elapsed < targetFrameTime)
                        {
                            await Task.Delay(targetFrameTime - (int)elapsed, cancellationToken);
                        }

                        stopwatch.Restart();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"捕获帧错误: {ex.Message}");
                    }
                }

                await encodingTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消操作
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    statusLabel.Text = $"捕获错误: {ex.Message}";
                    statusLabel.ForeColor = Color.Red;
                }));
            }
        }

        private async Task ProcessEncodingQueue(CancellationToken cancellationToken)
        {
            int frameNumber = 0;

            while (!cancellationToken.IsCancellationRequested || !frameQueue.IsEmpty)
            {
                if (frameQueue.TryDequeue(out Bitmap screenshot))
                {
                    try
                    {
                        await encodingSemaphore.WaitAsync(cancellationToken);

                        // 同步编码，避免多线程访问FFmpeg上下文
                        try
                        {
                            byte[] encodedData = await EncodeFrameFast(screenshot, frameNumber++);

                            if (encodedData != null && encodedData.Length > 0 && networkStream != null)
                            {
                                byte[] sizeBytes = BitConverter.GetBytes(encodedData.Length);
                                await networkStream.WriteAsync(sizeBytes, 0, sizeBytes.Length, cancellationToken);
                                await networkStream.WriteAsync(encodedData, 0, encodedData.Length, cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"编码错误: {ex.Message}");
                        }
                        finally
                        {
                            screenshot?.Dispose();
                            encodingSemaphore.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        screenshot?.Dispose();
                        break;
                    }
                }
                else
                {
                    await Task.Delay(5, cancellationToken); // 短暂等待新帧
                }
            }
        }

        private unsafe Task<byte[]> EncodeFrameFast(Bitmap bitmap, int frameNumber)
        {
            return Task.Run(() =>
            {
                if (bitmap == null) return null;

                // 使用锁保护FFmpeg上下文
                lock (ffmpegLock)
                {
                    try
                    {
                        BitmapData bitmapData = bitmap.LockBits(
                            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format24bppRgb);

                        try
                        {
                            byte*[] srcData = { (byte*)bitmapData.Scan0.ToPointer() };
                            int[] srcLinesize = { bitmapData.Stride };

                            // 检查上下文是否有效
                            if (swsContext == null || codecContext == null || frame == null || packet == null)
                            {
                                Console.WriteLine("FFmpeg 上下文无效");
                                return null;
                            }

                            ffmpeg.sws_scale(swsContext, srcData, srcLinesize, 0, codecContext->height,
                                           frame->data, frame->linesize);

                            frame->pts = frameNumber;

                            // 确保帧数据有效
                            if (frame->data[0] == null)
                            {
                                Console.WriteLine("帧数据无效");
                                return null;
                            }

                            int ret = ffmpeg.avcodec_send_frame(codecContext, frame);
                            if (ret < 0)
                            {
                                Console.WriteLine($"发送帧失败: {ret}");
                                return null;
                            }

                            using (MemoryStream outputStream = new MemoryStream())
                            {
                                while (ret >= 0)
                                {
                                    ret = ffmpeg.avcodec_receive_packet(codecContext, packet);
                                    if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                                        break;
                                    else if (ret < 0)
                                    {
                                        Console.WriteLine($"接收包失败: {ret}");
                                        return null;
                                    }

                                    if (packet->data == null || packet->size <= 0)
                                    {
                                        ffmpeg.av_packet_unref(packet);
                                        continue;
                                    }

                                    byte[] packetData = new byte[packet->size];
                                    Marshal.Copy(new IntPtr(packet->data), packetData, 0, packet->size);
                                    outputStream.Write(packetData, 0, packetData.Length);

                                    ffmpeg.av_packet_unref(packet);
                                }

                                return outputStream.ToArray();
                            }
                        }
                        finally
                        {
                            bitmap.UnlockBits(bitmapData);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"编码错误: {ex.Message}");
                        Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                        return null;
                    }
                }
            });
        }

        protected override unsafe void OnFormClosed(FormClosedEventArgs e)
        {
            StopCapture();

            // 等待所有编码任务完成
            encodingSemaphore?.Wait(5000); // 最多等待5秒

            // 在锁内清理FFmpeg资源
            lock (ffmpegLock)
            {
                encodingSemaphore?.Dispose();

                if (swsContext != null)
                {
                    ffmpeg.sws_freeContext(swsContext);
                    swsContext = null;
                }

                if (frame != null)
                {
                    AVFrame* framePtr = frame;
                    ffmpeg.av_frame_free(&framePtr);
                    frame = null;
                }

                if (packet != null)
                {
                    AVPacket* packetPtr = packet;
                    ffmpeg.av_packet_free(&packetPtr);
                    packet = null;
                }

                if (codecContext != null)
                {
                    AVCodecContext* contextPtr = codecContext;
                    ffmpeg.avcodec_free_context(&contextPtr);
                    codecContext = null;
                }
            }

            base.OnFormClosed(e);
        }

        private async void startButton_Click_1(object sender, EventArgs e)
        {
            try
            {
                string serverAddress = serverAddressTextBox.Text;
                int port = int.Parse(portTextBox.Text);

                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverAddress, port);
                networkStream = tcpClient.GetStream();

                isCapturing = true;
                cancellationTokenSource = new CancellationTokenSource();

                startButton.Enabled = false;
                stopButton.Enabled = true;
                statusLabel.Text = "正在捕获屏幕...";
                statusLabel.ForeColor = Color.Green;

                _ = Task.Run(() => CaptureScreenAsync(cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = $"连接失败: {ex.Message}";
                statusLabel.ForeColor = Color.Red;
            }
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            StopCapture();
        }

        private void ScreenCaptureClient_Load(object sender, EventArgs e)
        {

        }
    }
}