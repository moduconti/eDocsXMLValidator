namespace eDocument_Validator
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pathBox = new System.Windows.Forms.TextBox();
            this.welcomeLabel = new System.Windows.Forms.Label();
            this.lookupButton = new System.Windows.Forms.Button();
            this.validateButton = new System.Windows.Forms.Button();
            this.selectLabel = new System.Windows.Forms.Label();
            this.validationResultLabel = new System.Windows.Forms.Label();
            this.formatBox = new System.Windows.Forms.ComboBox();
            this.resultBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // pathBox
            // 
            this.pathBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pathBox.Location = new System.Drawing.Point(71, 217);
            this.pathBox.Name = "pathBox";
            this.pathBox.Size = new System.Drawing.Size(1058, 30);
            this.pathBox.TabIndex = 0;
            // 
            // welcomeLabel
            // 
            this.welcomeLabel.AutoSize = true;
            this.welcomeLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.welcomeLabel.Location = new System.Drawing.Point(348, 40);
            this.welcomeLabel.Name = "welcomeLabel";
            this.welcomeLabel.Size = new System.Drawing.Size(613, 37);
            this.welcomeLabel.TabIndex = 1;
            this.welcomeLabel.Text = "Welcome to eDocuments XML validator";
            // 
            // lookupButton
            // 
            this.lookupButton.Location = new System.Drawing.Point(1126, 211);
            this.lookupButton.Name = "lookupButton";
            this.lookupButton.Size = new System.Drawing.Size(48, 36);
            this.lookupButton.TabIndex = 2;
            this.lookupButton.Text = "...";
            this.lookupButton.UseVisualStyleBackColor = true;
            this.lookupButton.Click += new System.EventHandler(this.lookupButton_Click);
            // 
            // validateButton
            // 
            this.validateButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.validateButton.Location = new System.Drawing.Point(71, 276);
            this.validateButton.Name = "validateButton";
            this.validateButton.Size = new System.Drawing.Size(186, 42);
            this.validateButton.TabIndex = 3;
            this.validateButton.Text = "Validate";
            this.validateButton.UseVisualStyleBackColor = true;
            this.validateButton.Click += new System.EventHandler(this.validateButton_Click);
            // 
            // selectLabel
            // 
            this.selectLabel.AutoSize = true;
            this.selectLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.selectLabel.Location = new System.Drawing.Point(67, 155);
            this.selectLabel.Name = "selectLabel";
            this.selectLabel.Size = new System.Drawing.Size(296, 29);
            this.selectLabel.TabIndex = 4;
            this.selectLabel.Text = "Select XML file to validate:";
            // 
            // validationResultLabel
            // 
            this.validationResultLabel.AutoSize = true;
            this.validationResultLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.validationResultLabel.Location = new System.Drawing.Point(66, 364);
            this.validationResultLabel.Name = "validationResultLabel";
            this.validationResultLabel.Size = new System.Drawing.Size(199, 29);
            this.validationResultLabel.TabIndex = 6;
            this.validationResultLabel.Text = "Validation Result:";
            // 
            // formatBox
            // 
            this.formatBox.FormattingEnabled = true;
            this.formatBox.Location = new System.Drawing.Point(71, 104);
            this.formatBox.Name = "formatBox";
            this.formatBox.Size = new System.Drawing.Size(245, 28);
            this.formatBox.TabIndex = 7;
            // 
            // resultBox
            // 
            this.resultBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.resultBox.Location = new System.Drawing.Point(35, 396);
            this.resultBox.Multiline = true;
            this.resultBox.Name = "resultBox";
            this.resultBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.resultBox.Size = new System.Drawing.Size(1708, 714);
            this.resultBox.TabIndex = 8;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.ClientSize = new System.Drawing.Size(1812, 1149);
            this.Controls.Add(this.resultBox);
            this.Controls.Add(this.formatBox);
            this.Controls.Add(this.validationResultLabel);
            this.Controls.Add(this.selectLabel);
            this.Controls.Add(this.validateButton);
            this.Controls.Add(this.lookupButton);
            this.Controls.Add(this.welcomeLabel);
            this.Controls.Add(this.pathBox);
            this.Name = "Form1";
            this.Text = "eDocument Validator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox pathBox;
        private System.Windows.Forms.Label welcomeLabel;
        private System.Windows.Forms.Button lookupButton;
        private System.Windows.Forms.Button validateButton;
        private System.Windows.Forms.Label selectLabel;
        private System.Windows.Forms.Label validationResultLabel;
        private System.Windows.Forms.ComboBox formatBox;
        private System.Windows.Forms.TextBox resultBox;
    }
}

