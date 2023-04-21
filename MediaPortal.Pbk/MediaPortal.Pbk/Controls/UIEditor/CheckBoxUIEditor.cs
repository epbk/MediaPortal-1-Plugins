using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace MediaPortal.Pbk.Controls.UIEditor
{
    public class CheckBoxUIEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override bool GetPaintValueSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override void PaintValue(PaintValueEventArgs e)
        {
            ButtonState state;

            bool bRes = Convert.ToBoolean(e.Value);

            if (bRes)
                state = ButtonState.Checked;
            else
                state = ButtonState.Normal;

            ControlPaint.DrawCheckBox(e.Graphics, e.Bounds, state);

            e.Graphics.ExcludeClip(e.Bounds);

        }
    }
}
