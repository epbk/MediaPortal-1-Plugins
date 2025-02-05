using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.SSDP
{
    public class SsdpEventArgsAttribute : EventArgs
    {
        public SsdpEventTypeEnum EventType { get; private set; }
        public object Tag { get; private set; }

        public SsdpEventArgsAttribute(SsdpEventTypeEnum type, object tag)
        {
            this.EventType = type;
            this.Tag = tag;
        }
    }
}
