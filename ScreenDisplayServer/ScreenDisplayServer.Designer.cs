namespace ScreenDisplayServer
{
    partial class ScreenDisplayServer
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.portTextBox = new System.Windows.Forms.TextBox();
            this.startServerButton = new System.Windows.Forms.Button();
            this.stopServerButton = new System.Windows.Forms.Button();
            this.statusLabel = new System.Windows.Forms.Label();
            this.displayPictureBox = new System.Windows.Forms.PictureBox();
            this.clientInfoLabel = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.displayPictureBox)).BeginInit();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(2, 0);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "监听端口:";
            // 
            // portTextBox
            // 
            this.portTextBox.Location = new System.Drawing.Point(95, 2);
            this.portTextBox.Margin = new System.Windows.Forms.Padding(2);
            this.portTextBox.Name = "portTextBox";
            this.portTextBox.Size = new System.Drawing.Size(76, 28);
            this.portTextBox.TabIndex = 1;
            this.portTextBox.Text = "8888";
            // 
            // startServerButton
            // 
            this.startServerButton.Location = new System.Drawing.Point(175, 2);
            this.startServerButton.Margin = new System.Windows.Forms.Padding(2);
            this.startServerButton.Name = "startServerButton";
            this.startServerButton.Size = new System.Drawing.Size(138, 37);
            this.startServerButton.TabIndex = 2;
            this.startServerButton.Text = "启动服务器";
            this.startServerButton.UseVisualStyleBackColor = true;
            this.startServerButton.Click += new System.EventHandler(this.startServerButton_Click);
            // 
            // stopServerButton
            // 
            this.stopServerButton.Location = new System.Drawing.Point(317, 2);
            this.stopServerButton.Margin = new System.Windows.Forms.Padding(2);
            this.stopServerButton.Name = "stopServerButton";
            this.stopServerButton.Size = new System.Drawing.Size(129, 37);
            this.stopServerButton.TabIndex = 3;
            this.stopServerButton.Text = "停止服务器";
            this.stopServerButton.UseVisualStyleBackColor = true;
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new System.Drawing.Point(450, 0);
            this.statusLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(62, 18);
            this.statusLabel.TabIndex = 4;
            this.statusLabel.Text = "label2";
            // 
            // displayPictureBox
            // 
            this.displayPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.displayPictureBox.Location = new System.Drawing.Point(0, 0);
            this.displayPictureBox.Margin = new System.Windows.Forms.Padding(2);
            this.displayPictureBox.Name = "displayPictureBox";
            this.displayPictureBox.Size = new System.Drawing.Size(1319, 836);
            this.displayPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.displayPictureBox.TabIndex = 5;
            this.displayPictureBox.TabStop = false;
            // 
            // clientInfoLabel
            // 
            this.clientInfoLabel.AutoSize = true;
            this.clientInfoLabel.Location = new System.Drawing.Point(516, 0);
            this.clientInfoLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.clientInfoLabel.Name = "clientInfoLabel";
            this.clientInfoLabel.Size = new System.Drawing.Size(62, 18);
            this.clientInfoLabel.TabIndex = 6;
            this.clientInfoLabel.Text = "label2";
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.label1);
            this.flowLayoutPanel1.Controls.Add(this.portTextBox);
            this.flowLayoutPanel1.Controls.Add(this.startServerButton);
            this.flowLayoutPanel1.Controls.Add(this.stopServerButton);
            this.flowLayoutPanel1.Controls.Add(this.statusLabel);
            this.flowLayoutPanel1.Controls.Add(this.clientInfoLabel);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(1319, 56);
            this.flowLayoutPanel1.TabIndex = 7;
            this.flowLayoutPanel1.Visible = false;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1319, 836);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.displayPictureBox);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.displayPictureBox)).EndInit();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox portTextBox;
        private System.Windows.Forms.Button startServerButton;
        private System.Windows.Forms.Button stopServerButton;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.PictureBox displayPictureBox;
        private System.Windows.Forms.Label clientInfoLabel;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
    }
}

