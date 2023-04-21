namespace MediaPortal.Plugins.WorldWeatherLite.UserControls
{
    partial class HolidayTextBox
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
            this.textBoxDescription = new System.Windows.Forms.TextBox();
            this.dayMonthTextBox = new MediaPortal.Plugins.WorldWeatherLite.UserControls.DayMonthTextBox();
            this.SuspendLayout();
            // 
            // textBoxDescription
            // 
            this.textBoxDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxDescription.Location = new System.Drawing.Point(0, 0);
            this.textBoxDescription.Name = "textBoxDescription";
            this.textBoxDescription.Size = new System.Drawing.Size(323, 20);
            this.textBoxDescription.TabIndex = 0;
            // 
            // dayMonthTextBox1
            // 
            this.dayMonthTextBox.BackColor = System.Drawing.SystemColors.Window;
            this.dayMonthTextBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dayMonthTextBox.Location = new System.Drawing.Point(325, 0);
            this.dayMonthTextBox.Name = "dayMonthTextBox1";
            this.dayMonthTextBox.Size = new System.Drawing.Size(43, 21);
            this.dayMonthTextBox.TabIndex = 1;
            // 
            // HolidayTextBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.dayMonthTextBox);
            this.Controls.Add(this.textBoxDescription);
            this.Name = "HolidayTextBox";
            this.Size = new System.Drawing.Size(371, 20);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBoxDescription;
        private DayMonthTextBox dayMonthTextBox;
    }
}
