using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace MediaPortal.Pbk.Controls
{
    public class ToolStripSpringTextBox : ToolStripTextBox
    {
        public ToolStripSpringTextBox()
            : base()
        {
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ToolStripSpringTextBox(Control c)
            : base(c)
        {
        }
        public ToolStripSpringTextBox(string name)
            : base(name)
        {
        }

        public override Size GetPreferredSize(Size constrainingSize)
        {
            // Use the default size if the text box is on the overflow menu
            // or is on a vertical ToolStrip.
            if (this.IsOnOverflow || this.Owner.Orientation == Orientation.Vertical)
            {
                return this.DefaultSize;
            }

            // Declare a variable to store the total available width as
            // it is calculated, starting with the display width of the
            // owning ToolStrip.
            Int32 iWidth = this.Owner.DisplayRectangle.Width;

            // Subtract the width of the overflow button if it is displayed.
            if (this.Owner.OverflowButton.Visible)
            {
                iWidth = iWidth - this.Owner.OverflowButton.Width - this.Owner.OverflowButton.Margin.Horizontal;
            }

            // Declare a variable to maintain a count of ToolStripSpringTextBox
            // items currently displayed in the owning ToolStrip.
            Int32 iSpringBoxCount = 0;

            foreach (ToolStripItem item in this.Owner.Items)
            {
                // Ignore items on the overflow menu.
                if (item.IsOnOverflow)
                    continue;

                if (item is ToolStripSpringTextBox)
                {
                    // For ToolStripSpringTextBox items, increment the count and
                    // subtract the margin width from the total available width.
                    iSpringBoxCount++;
                    iWidth -= item.Margin.Horizontal;
                }
                else
                {
                    // For all other items, subtract the full width from the total
                    // available width.
                    iWidth = iWidth - item.Width - item.Margin.Horizontal;
                }
            }

            // If there are multiple ToolStripSpringTextBox items in the owning
            // ToolStrip, divide the total available width between them.
            if (iSpringBoxCount > 1)
                iWidth /= iSpringBoxCount;

            // If the available width is less than the default width, use the
            // default width, forcing one or more items onto the overflow menu.
            if (iWidth < DefaultSize.Width)
                iWidth = DefaultSize.Width;

            // Retrieve the preferred size from the base class, but change the
            // width to the calculated width.
            Size size = base.GetPreferredSize(constrainingSize);
            size.Width = iWidth;
            return size;
        }
    }
}
