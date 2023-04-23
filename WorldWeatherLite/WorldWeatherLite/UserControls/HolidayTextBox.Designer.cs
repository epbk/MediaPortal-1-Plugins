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
            this.comboBoxType = new System.Windows.Forms.ComboBox();
            this.dayMonthTextBox = new MediaPortal.Plugins.WorldWeatherLite.UserControls.DayMonthTextBox();
            this.SuspendLayout();
            // 
            // textBoxDescription
            // 
            this.textBoxDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxDescription.Location = new System.Drawing.Point(0, 0);
            this.textBoxDescription.Name = "textBoxDescription";
            this.textBoxDescription.Size = new System.Drawing.Size(246, 20);
            this.textBoxDescription.TabIndex = 0;
            // 
            // comboBoxType
            // 
            this.comboBoxType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxType.FormattingEnabled = true;
            this.comboBoxType.Location = new System.Drawing.Point(249, 0);
            this.comboBoxType.Name = "comboBoxType";
            this.comboBoxType.Size = new System.Drawing.Size(116, 21);
            this.comboBoxType.TabIndex = 2;
            this.comboBoxType.SelectedValueChanged += new System.EventHandler(this.comboBoxType_SelectedValueChanged);
            // 
            // dayMonthTextBox
            // 
            this.dayMonthTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.dayMonthTextBox.BackColor = System.Drawing.SystemColors.Window;
            this.dayMonthTextBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dayMonthTextBox.Location = new System.Drawing.Point(367, 0);
            this.dayMonthTextBox.Name = "dayMonthTextBox";
            this.dayMonthTextBox.Size = new System.Drawing.Size(43, 21);
            this.dayMonthTextBox.TabIndex = 1;
            // 
            // HolidayTextBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.comboBoxType);
            this.Controls.Add(this.dayMonthTextBox);
            this.Controls.Add(this.textBoxDescription);
            this.Name = "HolidayTextBox";
            this.Size = new System.Drawing.Size(410, 20);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBoxDescription;
        private DayMonthTextBox dayMonthTextBox;
        private System.Windows.Forms.ComboBox comboBoxType;
    }
}
