using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.SSDP
{
    public enum SsdpEventTypeEnum
    {
        None = 0,
        ClientStopped,
        ClientStarted,
        TargetAdded,
        TargetRemoved,
        ServerInfoCreated,
        ServerInfoUpdated,
        ServerInfoRefreshed,
        ServerInfoRemoved
    }
}
