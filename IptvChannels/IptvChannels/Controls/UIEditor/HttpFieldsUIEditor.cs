using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace MediaPortal.IptvChannels.Controls.UIEditor
{
    public class HttpFieldsUIEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(System.ComponentModel.ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(System.ComponentModel.ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            IWindowsFormsEditorService editorService = null;

            if (provider != null)
                editorService = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;

            if (editorService != null && context != null)
            {
                StringDictionaryEditorForm f = new StringDictionaryEditorForm()
                {
                    Text = "Http Fields",
                    Value = (NameValueCollection)value,
                    StartPosition = FormStartPosition.CenterParent,
                    ProhibitedNames = new string[]
                    {
                        "User-Agent", 
                        "Host",
                        "Content-Type",
                        "Content-Length", 
                        "Referer",
                        "Accept",
                        "Accept-Language", 
                        "Accept-Encoding",
                        "Cookie", 
                        "Connection",
                        "Range",
                        "If-Modified-Since"
                    }
                };

                if (f.ShowDialog() == DialogResult.OK)
                    return f.Value;
            }

            return value;
        }

    }
}
