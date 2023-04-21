using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace MediaPortal.Pbk.Controls
{
    public class ToolStripMenuItemCustom : ToolStripMenuItem
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (this.Checked)
            {
                Pen pen = Renderer.Renderer.GetCachedPen2(this.Selected ? SystemColors.HighlightText : this.ForeColor);

                //e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                e.Graphics.FillRectangle(Renderer.Renderer.GetCachedBrush(this.Selected ? SystemColors.Highlight : this.BackColor), 0, 0, 23, 23);

                e.Graphics.DrawLine(pen, 8, 11, 13, 16);

                e.Graphics.DrawLine(pen, 13, 16, 21, 8);

            }
        }
    }
}
