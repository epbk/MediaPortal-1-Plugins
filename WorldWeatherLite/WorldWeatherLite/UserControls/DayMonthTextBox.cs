using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MediaPortal.Plugins.WorldWeatherLite.UserControls
{
    public partial class DayMonthTextBox : UserControl
    {
        public int Day
        {
            get { return this._Day; }
        }private int _Day = 1;

        public int Month
        {
            get { return this._Month; }
        }private int _Month = 1;


        public DayMonthTextBox()
        {
            InitializeComponent();
        }

        public void Init(int iDay, int iMonth)
        {
            this._Day = iDay;
            this._Month = iMonth;
            this.textBoxDay.Text = iDay.ToString("00");
            this.textBoxMonth.Text = iMonth.ToString("00");

        }

        private static bool isValueValid(string strValue, int iValueMax)
        {
            if (strValue.Length > 2)
                return false;

            int i;
            return int.TryParse(strValue, out i) && i <= iValueMax;
        }


        private void textBoxDay_Validating(object sender, CancelEventArgs e)
        {
            if (!isValueValid(this.textBoxDay.Text, 31))
                e.Cancel = true;
        }

        private void textBoxDay_Validated(object sender, EventArgs e)
        {
            this._Day = int.Parse(this.textBoxDay.Text);

            this.textBoxDay.Text = this._Day.ToString("00");
            this.textBoxMonth.Focus();
        }
                

        private void textBoxMonth_Validating(object sender, CancelEventArgs e)
        {
            if (!isValueValid(this.textBoxMonth.Text, 12))
                e.Cancel = true;
        }

        private void textBoxMonth_Validated(object sender, EventArgs e)
        {
            int iMonth = int.Parse(this.textBoxMonth.Text);

            int iDayMax = 31;
            switch (iMonth)
            {
                case 2:
                    iDayMax = 28;
                    break;

                case 4:
                case 6:
                case 9:
                case 11:
                    iDayMax = 30;
                    break;
            }

            if (this._Day > iDayMax)
            {
                this._Day = iDayMax;
                this.textBoxDay.Text = this._Day.ToString("00");
            }
                        
            this.textBoxMonth.Text = iMonth.ToString("00");
            
            this._Month = iMonth;
        }


        private void textBoxDay_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\b')
                return;

            if (e.KeyChar == (char)27)
            {
                this.textBoxDay.Text = this._Day.ToString("00");
                return;
            }

            if (e.KeyChar == '\r')
            {
                if (!isValueValid(this.textBoxDay.Text, 31))
                    e.Handled = true;
                else
                {
                    this.textBoxMonth.Focus();
                    return;
                }
            }

            if ((this.textBoxDay.Text.Length >= 2 && this.textBoxDay.SelectionLength != this.textBoxDay.Text.Length) || e.KeyChar < '0' || e.KeyChar > '9')
                e.Handled = true;
        }

        private void textBoxMonth_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\b')
                return;

            if (e.KeyChar == (char)27)
            {
                this.textBoxMonth.Text = this._Month.ToString("00");
                return;
            }

            if (e.KeyChar == '\r')
            {
                if (!isValueValid(this.textBoxMonth.Text, 12))
                    e.Handled = true;
                else
                {
                    return;
                }
            }

            if ((this.textBoxMonth.Text.Length >= 2 && this.textBoxMonth.SelectionLength != this.textBoxMonth.Text.Length) || e.KeyChar < '0' || e.KeyChar > '9')
                e.Handled = true;
        }

        private void textBoxDay_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Right 
                && this.textBoxDay.SelectionStart == this.textBoxDay.Text.Length
                && isValueValid(this.textBoxDay.Text, 31))
            {
                this.textBoxMonth.Focus();
                this.textBoxMonth.SelectionStart = 0;
                this.textBoxMonth.SelectionLength = 0;
            }
        }

        private void textBoxMonth_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Left && this.textBoxMonth.SelectionStart == 0 && isValueValid(this.textBoxMonth.Text, 12))
            {
                this.textBoxDay.Focus();
                this.textBoxDay.SelectionStart = this.textBoxDay.Text.Length;
                this.textBoxDay.SelectionLength = 0;
            }
        }


    }
}
