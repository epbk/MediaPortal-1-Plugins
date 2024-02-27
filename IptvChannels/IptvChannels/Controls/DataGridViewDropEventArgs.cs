using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MediaPortal.IptvChannels.Controls
{
    public class DataGridViewDropEventArgs: EventArgs
    {
        public IEnumerable<DataGridViewRow> MovedRows;
    }
}
