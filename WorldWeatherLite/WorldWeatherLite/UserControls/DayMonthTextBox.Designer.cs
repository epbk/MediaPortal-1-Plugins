namespace MediaPortal.Plugins.WorldWeatherLite.UserControls
{
    partial class DayMonthTextBox
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textBoxDay = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxMonth = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // textBoxDay
            // 
            this.textBoxDay.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxDay.Location = new System.Drawing.Point(3, 2);
            this.textBoxDay.Name = "textBoxDay";
            this.textBoxDay.Size = new System.Drawing.Size(12, 13);
            this.textBoxDay.TabIndex = 0;
            this.textBoxDay.Text = "01";
            this.textBoxDay.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBoxDay_KeyPress);
            this.textBoxDay.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.textBoxDay_PreviewKeyDown);
            this.textBoxDay.Validating += new System.ComponentModel.CancelEventHandler(this.textBoxDay_Validating);
            this.textBoxDay.Validated += new System.EventHandler(this.textBoxDay_Validated);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(16, 2);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(11, 15);
            this.label1.TabIndex = 1;
            this.label1.Text = ".";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxMonth
            // 
            this.textBoxMonth.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxMonth.Location = new System.Drawing.Point(24, 2);
            this.textBoxMonth.Name = "textBoxMonth";
            this.textBoxMonth.Size = new System.Drawing.Size(12, 13);
            this.textBoxMonth.TabIndex = 2;
            this.textBoxMonth.Text = "01";
            this.textBoxMonth.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBoxMonth_KeyPress);
            this.textBoxMonth.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.textBoxMonth_PreviewKeyDown);
            this.textBoxMonth.Validating += new System.ComponentModel.CancelEventHandler(this.textBoxMonth_Validating);
            this.textBoxMonth.Validated += new System.EventHandler(this.textBoxMonth_Validated);
            // 
            // DayMonthTextBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Controls.Add(this.textBoxMonth);
            this.Controls.Add(this.textBoxDay);
            this.Controls.Add(this.label1);
            this.Name = "DayMonthTextBox";
            this.Size = new System.Drawing.Size(43, 20);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBoxDay;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxMonth;
    }
}
