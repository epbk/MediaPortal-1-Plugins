using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MediaPortal.IptvChannels.Controls.UIEditor
{
    public partial class StringDictionaryEditorForm : Form
    {
        public string[] ProhibitedNames
        { get; set; }

        public NameValueCollection Value
        {
            get
            {
                this._Value.Clear();

                for (int i = 0; i < this.dataGridView.Rows.Count; i++)
                {
                    DataGridViewRow r = this.dataGridView.Rows[i];
                    if (r.Tag == null)
                        break;

                    this._Value.Add((string)r.Tag, (string)r.Cells[1].Value);
                }

                return this._Value;
            }

            set
            {
                if (value != null)
                {
                    this.dataGridView.Rows.Clear();

                    foreach (string strKey in value.Keys)
                    {
                        if (this.validateName(strKey))
                            this.dataGridView.Rows.Add(strKey, value[strKey]);
                    }
                }
            }
        }private NameValueCollection _Value = new NameValueCollection();

        public StringDictionaryEditorForm()
        {
            InitializeComponent();
        }

        private void dataGridView_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow r = this.dataGridView.Rows[e.RowIndex];
                string strValue = (string)e.FormattedValue;
                if (e.ColumnIndex == 0)
                {
                    if (!this.validateName(strValue))
                    {
                        e.Cancel = true;
                        return;
                    }

                    //Check for existing value
                    for (int i = 0; i < this.dataGridView.Rows.Count; i++)
                    {
                        if (i == e.RowIndex)
                            continue;

                        if ((string)this.dataGridView.Rows[i].Tag == strValue)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(strValue))
                    {
                        if (r.Tag == null)
                            r.Cells[1].Value = "Value";

                        r.Tag = strValue;
                    }
                    else if (e.RowIndex != this.dataGridView.Rows.Count - 1)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                else if (e.ColumnIndex == 1)
                {
                    if ((r.Tag != null && string.IsNullOrWhiteSpace(strValue)) || (r.Tag == null && !string.IsNullOrWhiteSpace(strValue)))
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
        }

        private bool validateName(string strName)
        {
            if (strName.Contains(' '))
                return false;

            if (this.ProhibitedNames != null && this.ProhibitedNames.Any(n => n == strName))
                return false;
            
            return true;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }
    }
}
