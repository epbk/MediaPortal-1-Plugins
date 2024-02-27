using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy
{
    public class ConnectionEventArgs : EventArgs
    {
        public ConnectionEventTypeEnum EventType;
        public object Tag;
    }
}
