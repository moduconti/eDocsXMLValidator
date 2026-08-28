namespace eDocument_Validator.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.titleLabel = new System.Windows.Forms.Label();
            this.formatLabel = new System.Windows.Forms.Label();
            this.formatBox = new System.Windows.Forms.ComboBox();
            this.documentLabel = new System.Windows.Forms.Label();
            this.pathBox = new System.Windows.Forms.TextBox();
            this.browseButton = new System.Windows.Forms.Button();
            this.validateButton = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.statusLabel = new System.Windows.Forms.Label();
            this.resultLabel = new System.Windows.Forms.Label();
            this.showWarningsBox = new System.Windows.Forms.CheckBox();
            this.showAllChecksBox = new System.Windows.Forms.CheckBox();
            this.darkThemeBox = new System.Windows.Forms.CheckBox();
            this.openResultsButton = new System.Windows.Forms.Button();
            this.resultBox = new System.Windows.Forms.RichTextBox();
            this.inputPanel = new System.Windows.Forms.Panel();
            this.filterPanel = new System.Windows.Forms.Panel();
            this.inputPanel.SuspendLayout();
            this.filterPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // titleLabel
            //
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold);
            this.titleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.titleLabel.Location = new System.Drawing.Point(18, 14);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(316, 35);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "eDocument Validator";
            //
            // formatLabel
            //
            this.formatLabel.AutoSize = true;
            this.formatLabel.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.formatLabel.Location = new System.Drawing.Point(21, 64);
            this.formatLabel.Name = "formatLabel";
            this.formatLabel.Size = new System.Drawing.Size(58, 23);
            this.formatLabel.TabIndex = 1;
            this.formatLabel.Text = "Format";
            //
            // formatBox
            //
            this.formatBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.formatBox.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.formatBox.FormattingEnabled = true;
            this.formatBox.Location = new System.Drawing.Point(125, 60);
            this.formatBox.Name = "formatBox";
            this.formatBox.Size = new System.Drawing.Size(360, 31);
            this.formatBox.TabIndex = 2;
            this.formatBox.SelectedIndexChanged += new System.EventHandler(this.formatBox_SelectedIndexChanged);
            //
            // documentLabel
            //
            this.documentLabel.AutoSize = true;
            this.documentLabel.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.documentLabel.Location = new System.Drawing.Point(21, 108);
            this.documentLabel.Name = "documentLabel";
            this.documentLabel.Size = new System.Drawing.Size(87, 23);
            this.documentLabel.TabIndex = 3;
            this.documentLabel.Text = "Document";
            //
            // pathBox
            //
            this.pathBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pathBox.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.pathBox.Location = new System.Drawing.Point(125, 104);
            this.pathBox.Name = "pathBox";
            this.pathBox.Size = new System.Drawing.Size(880, 30);
            this.pathBox.TabIndex = 4;
            //
            // browseButton
            //
            this.browseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.browseButton.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.browseButton.Location = new System.Drawing.Point(1015, 102);
            this.browseButton.Name = "browseButton";
            this.browseButton.Size = new System.Drawing.Size(110, 34);
            this.browseButton.TabIndex = 5;
            this.browseButton.Text = "Choose file";
            this.browseButton.UseVisualStyleBackColor = true;
            this.browseButton.Click += new System.EventHandler(this.browseButton_Click);
            //
            // validateButton
            //
            this.validateButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.validateButton.Location = new System.Drawing.Point(125, 150);
            this.validateButton.Name = "validateButton";
            this.validateButton.Size = new System.Drawing.Size(160, 40);
            this.validateButton.TabIndex = 6;
            this.validateButton.Text = "Validate";
            this.validateButton.UseVisualStyleBackColor = true;
            this.validateButton.Click += new System.EventHandler(this.validateButton_Click);
            //
            // progressBar
            //
            this.progressBar.Location = new System.Drawing.Point(300, 158);
            this.progressBar.MarqueeAnimationSpeed = 30;
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(185, 24);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.TabIndex = 7;
            this.progressBar.Visible = false;
            //
            // statusLabel
            //
            this.statusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.statusLabel.AutoEllipsis = true;
            this.statusLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.statusLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(95)))), ((int)(((byte)(95)))));
            this.statusLabel.Location = new System.Drawing.Point(500, 162);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(625, 22);
            this.statusLabel.TabIndex = 8;
            this.statusLabel.Text = "Choose a format and a document, then press Validate.";
            //
            // inputPanel
            //
            this.inputPanel.BackColor = System.Drawing.Color.White;
            this.inputPanel.Controls.Add(this.titleLabel);
            this.inputPanel.Controls.Add(this.formatLabel);
            this.inputPanel.Controls.Add(this.formatBox);
            this.inputPanel.Controls.Add(this.documentLabel);
            this.inputPanel.Controls.Add(this.pathBox);
            this.inputPanel.Controls.Add(this.browseButton);
            this.inputPanel.Controls.Add(this.validateButton);
            this.inputPanel.Controls.Add(this.progressBar);
            this.inputPanel.Controls.Add(this.statusLabel);
            this.inputPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.inputPanel.Location = new System.Drawing.Point(0, 0);
            this.inputPanel.Name = "inputPanel";
            this.inputPanel.Size = new System.Drawing.Size(1144, 205);
            this.inputPanel.TabIndex = 0;
            //
            // resultLabel
            //
            this.resultLabel.AutoSize = true;
            this.resultLabel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.resultLabel.Location = new System.Drawing.Point(21, 12);
            this.resultLabel.Name = "resultLabel";
            this.resultLabel.Size = new System.Drawing.Size(56, 23);
            this.resultLabel.TabIndex = 0;
            this.resultLabel.Text = "Result";
            //
            // showWarningsBox
            //
            this.showWarningsBox.AutoSize = true;
            this.showWarningsBox.Checked = true;
            this.showWarningsBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showWarningsBox.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.showWarningsBox.Location = new System.Drawing.Point(125, 12);
            this.showWarningsBox.Name = "showWarningsBox";
            this.showWarningsBox.Size = new System.Drawing.Size(146, 27);
            this.showWarningsBox.TabIndex = 1;
            this.showWarningsBox.Text = "Show warnings";
            this.showWarningsBox.UseVisualStyleBackColor = true;
            this.showWarningsBox.CheckedChanged += new System.EventHandler(this.filter_CheckedChanged);
            //
            // showAllChecksBox
            //
            this.showAllChecksBox.AutoSize = true;
            this.showAllChecksBox.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.showAllChecksBox.Location = new System.Drawing.Point(295, 12);
            this.showAllChecksBox.Name = "showAllChecksBox";
            this.showAllChecksBox.Size = new System.Drawing.Size(151, 27);
            this.showAllChecksBox.TabIndex = 2;
            this.showAllChecksBox.Text = "Show all checks";
            this.showAllChecksBox.UseVisualStyleBackColor = true;
            this.showAllChecksBox.CheckedChanged += new System.EventHandler(this.filter_CheckedChanged);
            //
            // darkThemeBox
            //
            this.darkThemeBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.darkThemeBox.AutoSize = true;
            this.darkThemeBox.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.darkThemeBox.Location = new System.Drawing.Point(820, 12);
            this.darkThemeBox.Name = "darkThemeBox";
            this.darkThemeBox.Size = new System.Drawing.Size(118, 27);
            this.darkThemeBox.TabIndex = 3;
            this.darkThemeBox.Text = "Dark colours";
            this.darkThemeBox.UseVisualStyleBackColor = true;
            this.darkThemeBox.CheckedChanged += new System.EventHandler(this.darkThemeBox_CheckedChanged);
            //
            // openResultsButton
            //
            this.openResultsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.openResultsButton.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.openResultsButton.Location = new System.Drawing.Point(965, 10);
            this.openResultsButton.Name = "openResultsButton";
            this.openResultsButton.Size = new System.Drawing.Size(160, 30);
            this.openResultsButton.TabIndex = 4;
            this.openResultsButton.Text = "Open results folder";
            this.openResultsButton.UseVisualStyleBackColor = true;
            this.openResultsButton.Click += new System.EventHandler(this.openResultsButton_Click);
            //
            // filterPanel
            //
            this.filterPanel.BackColor = System.Drawing.Color.White;
            this.filterPanel.Controls.Add(this.resultLabel);
            this.filterPanel.Controls.Add(this.showWarningsBox);
            this.filterPanel.Controls.Add(this.showAllChecksBox);
            this.filterPanel.Controls.Add(this.darkThemeBox);
            this.filterPanel.Controls.Add(this.openResultsButton);
            this.filterPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.filterPanel.Location = new System.Drawing.Point(0, 205);
            this.filterPanel.Name = "filterPanel";
            this.filterPanel.Size = new System.Drawing.Size(1144, 50);
            this.filterPanel.TabIndex = 1;
            //
            // resultBox
            //
            this.resultBox.BackColor = System.Drawing.Color.White;
            this.resultBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.resultBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultBox.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.resultBox.HideSelection = false;
            this.resultBox.Location = new System.Drawing.Point(0, 255);
            this.resultBox.Name = "resultBox";
            this.resultBox.ReadOnly = true;
            this.resultBox.Size = new System.Drawing.Size(1144, 545);
            this.resultBox.TabIndex = 2;
            this.resultBox.Text = "";
            this.resultBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.resultBox.WordWrap = true;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1144, 800);
            this.Controls.Add(this.resultBox);
            this.Controls.Add(this.filterPanel);
            this.Controls.Add(this.inputPanel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "eDocument Validator";
            this.inputPanel.ResumeLayout(false);
            this.inputPanel.PerformLayout();
            this.filterPanel.ResumeLayout(false);
            this.filterPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label formatLabel;
        private System.Windows.Forms.ComboBox formatBox;
        private System.Windows.Forms.Label documentLabel;
        private System.Windows.Forms.TextBox pathBox;
        private System.Windows.Forms.Button browseButton;
        private System.Windows.Forms.Button validateButton;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Label resultLabel;
        private System.Windows.Forms.CheckBox showWarningsBox;
        private System.Windows.Forms.CheckBox showAllChecksBox;
        private System.Windows.Forms.CheckBox darkThemeBox;
        private System.Windows.Forms.Button openResultsButton;
        private System.Windows.Forms.RichTextBox resultBox;
        private System.Windows.Forms.Panel inputPanel;
        private System.Windows.Forms.Panel filterPanel;
    }
}
