using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MediaPortal.Pbk.Controls.UIEditor
{
    public class SelectFileUIEditor : UITypeEditor
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
                OpenFileDialog fd = new OpenFileDialog();
                fd.Multiselect = false;
                if (value != null && System.IO.File.Exists((string)value))
                {
                    System.IO.FileInfo fi = new System.IO.FileInfo((string)value);
                    fd.FileName = fi.Name;
                    fd.InitialDirectory = fi.Directory.FullName;
                }
                
                if (fd.ShowDialog() == DialogResult.OK)
                    return fd.FileName;
            }

            return value;
        }
    }
}
