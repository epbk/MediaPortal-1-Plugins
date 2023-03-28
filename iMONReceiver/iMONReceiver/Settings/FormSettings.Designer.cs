using System;
using System.Globalization;

namespace MediaPortal.Plugins.iMONReceiver.Settings
{
    partial class FormSettings
    {
        
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.button_Mapping = new System.Windows.Forms.Button();
            this.comboBoxFiles = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button_Mapping
            // 
            this.button_Mapping.Location = new System.Drawing.Point(287, 45);
            this.button_Mapping.Name = "button_Mapping";
            this.button_Mapping.Size = new System.Drawing.Size(75, 23);
            this.button_Mapping.TabIndex = 8;
            this.button_Mapping.Text = "Mapping";
            this.button_Mapping.UseVisualStyleBackColor = true;
            this.button_Mapping.Click += new System.EventHandler(this.button_Mapping_Click);
            // 
            // comboBoxFiles
            // 
            this.comboBoxFiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFiles.FormattingEnabled = true;
            this.comboBoxFiles.Location = new System.Drawing.Point(23, 46);
            this.comboBoxFiles.Name = "comboBoxFiles";
            this.comboBoxFiles.Size = new System.Drawing.Size(242, 21);
            this.comboBoxFiles.TabIndex = 9;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(20, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(116, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "Selected configuration:";
            // 
            // FormSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(390, 91);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboBoxFiles);
            this.Controls.Add(this.button_Mapping);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormSettings";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "iMON Receiver Settings";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.cbFormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button_Mapping;
        private System.Windows.Forms.ComboBox comboBoxFiles;
        private System.Windows.Forms.Label label1;


    }
}