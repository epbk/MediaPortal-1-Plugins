using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Controls
{
    public class DataGridViewCustomRow : System.Windows.Forms.DataGridViewRow
    {
        public DataGridViewRowTypeEnum ItemType = DataGridViewRowTypeEnum.Item;

        public bool InvalidateNeeded = false;
    }
}
