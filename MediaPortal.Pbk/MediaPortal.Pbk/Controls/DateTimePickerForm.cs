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
    public partial class DateTimePickerForm : Form
    {
        public DateTime Value
        {
            get { return this.dateTimePicker.Value; }
            set { this.dateTimePicker.Value = value; }
        }

        public DateTime MinDate
        {
            get { return this.dateTimePicker.MinDate; }
            set { this.dateTimePicker.MinDate = value; }
        }

        public DateTimePickerForm()
        {
            this.InitializeComponent();
        }

        private void button_Cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button_OK_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }
    }
}
