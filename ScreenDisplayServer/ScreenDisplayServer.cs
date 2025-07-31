using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using FFmpeg.AutoGen;

namespace ScreenDisplayServer
{
    public partial class ScreenDisplayServer : Form
    {
        private TcpListener tcpListener;
        private bool isListening = false;

        // FFmpeg 上下文
        private unsafe AVCodecContext* codecContext;
        private unsafe AVFrame* frame;
        private unsafe AVPacket* packet;
        private unsafe SwsContext* swsContext;

        // FPS 计算相关
        private Queue<DateTime> frameTimestamps = new Queue<DateTime>();
        private int fpsCalculationWindowSeconds = 1; // 计算1秒内的FPS
        private Timer fpsUpdateTimer;
        private double currentFPS = 0;
        private string baseTitle = "屏幕显示服务器";
        private int currentFrameSize = 0; // 当前帧大小(KB)

        public ScreenDisplayServer()
        {
            InitializeComponent();
            InitializeFFmpeg();
            InitializeFPSTimer();
        }

        private void InitializeFPSTimer()
        {
            // 创建定时器，每500毫秒更新一次标题
            fpsUpdateTimer = new Timer();
            fpsUpdateTimer.Interval = 500; // 0.5秒更新一次
            fpsUpdateTimer.Tick += UpdateFPSDisplay;
            fpsUpdateTimer.Start();
        }

        private void UpdateFPSDisplay(object sender, EventArgs e)
        {
            // 更新窗口标题显示FPS和帧大小
            if (currentFrameSize > 0)
            {
                this.Text = $"{baseTitle} - FPS: {currentFPS:F1} | 帧大小: {currentFrameSize} KB";
            }
            else
            {
                this.Text = $"{baseTitle} - FPS: {currentFPS:F1}";
            }
        }

        private void UpdateFPS()
        {
            DateTime now = DateTime.Now;
            frameTimestamps.Enqueue(now);

            // 移除超过时间窗口的时间戳
            while (frameTimestamps.Count > 0 &&
                   (now - frameTimestamps.Peek()).TotalSeconds > fpsCalculationWindowSeconds)
            {
                frameTimestamps.Dequeue();
            }

            // 计算FPS
            if (frameTimestamps.Count > 1)
            {
                currentFPS = frameTimestamps.Count / fpsCalculationWindowSeconds;
            }
        }

        private unsafe void InitializeFFmpeg()
        {
            // 设置 FFmpeg 库路径（如果需要）
            ffmpeg.RootPath = @".\x64";

            // 查找 VP9 解码器
            AVCodec* codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_VP9);
            if (codec == null)
                throw new InvalidOperationException("VP9 解码器未找到");

            // 分配解码器上下文
            codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (codecContext == null)
                throw new InvalidOperationException("无法分配解码器上下文");

            // 打开解码器
            int ret = ffmpeg.avcodec_open2(codecContext, codec, null);
            if (ret < 0)
                throw new InvalidOperationException($"无法打开解码器: {ret}");

            // 分配帧和包
            frame = ffmpeg.av_frame_alloc();
            packet = ffmpeg.av_packet_alloc();

            if (frame == null || packet == null)
                throw new InvalidOperationException("无法分配帧或包");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = baseTitle; // 设置初始标题
            startServerButton_Click(null, null);
        }

        private void startServerButton_Click(object sender, EventArgs e)
        {
            try
            {
                int port = int.Parse(portTextBox.Text);
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();

                isListening = true;
                startServerButton.Enabled = false;
                stopServerButton.Enabled = true;
                statusLabel.Text = $"服务器已启动，监听端口 {port}";
                statusLabel.ForeColor = Color.Green;

                // 重置FPS计算
                frameTimestamps.Clear();
                currentFPS = 0;
                currentFrameSize = 0;

                // 启动监听任务
                _ = Task.Run(ListenForClientsAsync);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动服务器失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = $"启动失败: {ex.Message}";
                statusLabel.ForeColor = Color.Red;
            }
        }

        private async Task ListenForClientsAsync()
        {
            while (isListening)
            {
                try
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();

                    this.Invoke(new Action(() =>
                    {
                        clientInfoLabel.Text = $"客户端已连接: {client.Client.RemoteEndPoint}";
                        clientInfoLabel.ForeColor = Color.Green;
                        // 重置FPS计算
                        frameTimestamps.Clear();
                        currentFPS = 0;
                        currentFrameSize = 0;
                    }));

                    // 处理客户端连接
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException)
                {
                    // 服务器已停止
                    break;
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        statusLabel.Text = $"监听错误: {ex.Message}";
                        statusLabel.ForeColor = Color.Red;
                    }));
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = null;
            try
            {
                stream = client.GetStream();

                // 读取视频流头信息
                byte[] headerBuffer = new byte[32];
                int headerBytesRead = await stream.ReadAsync(headerBuffer, 0, headerBuffer.Length);
                string headerInfo = Encoding.UTF8.GetString(headerBuffer, 0, headerBytesRead);

                this.Invoke(new Action(() =>
                {
                    clientInfoLabel.Text += $" | 分辨率: {headerInfo.Trim()}";
                }));

                while (client.Connected && isListening)
                {
                    // 读取帧大小
                    byte[] sizeBuffer = new byte[4];
                    int sizeBytesRead = 0;
                    while (sizeBytesRead < 4)
                    {
                        int bytesRead = await stream.ReadAsync(sizeBuffer, sizeBytesRead, 4 - sizeBytesRead);
                        if (bytesRead == 0) break;
                        sizeBytesRead += bytesRead;
                    }

                    if (sizeBytesRead < 4) break;

                    int frameSize = BitConverter.ToInt32(sizeBuffer, 0);
                    if (frameSize <= 0 || frameSize > 10 * 1024 * 1024) // 最大10MB
                        continue;

                    // 读取帧数据
                    byte[] frameBuffer = new byte[frameSize];
                    int frameBytesRead = 0;
                    while (frameBytesRead < frameSize)
                    {
                        int bytesRead = await stream.ReadAsync(frameBuffer, frameBytesRead, frameSize - frameBytesRead);
                        if (bytesRead == 0) break;
                        frameBytesRead += bytesRead;
                    }

                    if (frameBytesRead < frameSize) break;

                    // 解码并显示帧
                    await DecodeAndDisplayFrame(frameBuffer, frameSize);
                }
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    statusLabel.Text = $"处理客户端错误: {ex.Message}";
                    statusLabel.ForeColor = Color.Red;
                    clientInfoLabel.Text = "客户端已断开连接";
                    clientInfoLabel.ForeColor = Color.Gray;
                    // 客户端断开时重置FPS
                    frameTimestamps.Clear();
                    currentFPS = 0;
                    currentFrameSize = 0;
                }));
            }
            finally
            {
                stream?.Close();
                client?.Close();
            }
        }

        private unsafe Task DecodeAndDisplayFrame(byte[] encodedData, int frameSize)
        {
            return Task.Run(() =>
            {
                try
                {
                    // 创建 AVPacket 并填充数据
                    fixed (byte* dataPtr = encodedData)
                    {
                        packet->data = dataPtr;
                        packet->size = encodedData.Length;

                        // 发送包到解码器
                        int ret = ffmpeg.avcodec_send_packet(codecContext, packet);
                        if (ret < 0)
                        {
                            Console.WriteLine($"发送包到解码器失败: {ret}");
                            return;
                        }

                        // 接收解码后的帧
                        ret = ffmpeg.avcodec_receive_frame(codecContext, frame);
                        if (ret < 0)
                        {
                            if (ret != ffmpeg.AVERROR(ffmpeg.EAGAIN) && ret != ffmpeg.AVERROR_EOF)
                                Console.WriteLine($"接收解码帧失败: {ret}");
                            return;
                        }

                        // 转换 YUV420P 到 BGR24
                        Bitmap bitmap = ConvertFrameToBitmap(frame);
                        if (bitmap != null)
                        {
                            // 在UI线程上更新显示和FPS
                            this.Invoke(new Action(() =>
                            {
                                Image oldImage = displayPictureBox.Image;
                                displayPictureBox.Image = bitmap;
                                oldImage?.Dispose();

                                // 更新FPS计算和帧大小
                                UpdateFPS();
                                currentFrameSize = frameSize / 1024; // 转换为KB

                                // 在状态栏显示详细信息
                                statusLabel.Text = $"正在接收 | 帧大小: {currentFrameSize} KB | FPS: {currentFPS:F1}";
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解码错误: {ex.Message}");
                }
            });
        }

        private unsafe Bitmap ConvertFrameToBitmap(AVFrame* frame)
        {
            try
            {
                int width = frame->width;
                int height = frame->height;

                // 初始化图像转换上下文（如果还没有）
                if (swsContext == null)
                {
                    swsContext = ffmpeg.sws_getContext(
                        width, height, (AVPixelFormat)frame->format,
                        width, height, AVPixelFormat.AV_PIX_FMT_BGR24,
                        ffmpeg.SWS_BILINEAR, null, null, null);

                    if (swsContext == null)
                    {
                        Console.WriteLine("无法创建图像转换上下文");
                        return null;
                    }
                }

                // 创建目标位图
                Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                BitmapData bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);

                try
                {
                    // 转换像素格式
                    byte*[] dstData = { (byte*)bitmapData.Scan0.ToPointer() };
                    int[] dstLinesize = { bitmapData.Stride };

                    ffmpeg.sws_scale(swsContext, frame->data, frame->linesize, 0, height,
                                   dstData, dstLinesize);

                    return bitmap;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"转换帧到位图错误: {ex.Message}");
                return null;
            }
        }

        protected override unsafe void OnFormClosed(FormClosedEventArgs e)
        {
            StopServer();

            // 停止FPS更新定时器
            fpsUpdateTimer?.Stop();
            fpsUpdateTimer?.Dispose();

            // 清理 FFmpeg 资源
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

            base.OnFormClosed(e);
        }

        private void StopServer()
        {
            isListening = false;
            tcpListener?.Stop();

            startServerButton.Enabled = true;
            stopServerButton.Enabled = false;
            statusLabel.Text = "服务器已停止";
            statusLabel.ForeColor = Color.Blue;
            clientInfoLabel.Text = "等待客户端连接...";
            clientInfoLabel.ForeColor = Color.Gray;

            // 清空显示区域
            displayPictureBox.Image = null;

            // 重置FPS
            frameTimestamps.Clear();
            currentFPS = 0;
            currentFrameSize = 0;
            this.Text = baseTitle;
        }

        private void stopServerButton_Click(object sender, EventArgs e)
        {
            StopServer();
        }
    }
}