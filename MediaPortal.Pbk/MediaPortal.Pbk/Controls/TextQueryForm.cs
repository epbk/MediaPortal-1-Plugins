using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MediaPortal.Pbk.Controls
{
    public partial class TextQueryForm : Form
    {
        public string Query
        {
            get
            {
                return this.textBoxQuery.Text;
            }
        }

        public string InvalidQueryMessage
        {
            get
            {
                return this._InvalidQueryMessage;
            }

            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    this._InvalidQueryMessage = value;
            }

        }private string _InvalidQueryMessage = "Invalid query";

        public Predicate<string> Check = null;

        public char[] InvalidChars = null;

        public TextQueryForm()
        {
            this.InitializeComponent();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Close();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this.textBoxQuery.Text))
            {
                MessageBox.Show("Text is empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (this.InvalidChars != null)
            {
                foreach (char c in this.textBoxQuery.Text)
                {
                    if (this.InvalidChars.Contains(c))
                    {
                        MessageBox.Show("Invalid character: " + c, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            this.textBoxQuery.Text = this.textBoxQuery.Text.Trim();

            if (this.Check != null && !this.Check(this.textBoxQuery.Text))
            {
                MessageBox.Show(this._InvalidQueryMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }
    }
}
