using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels
{
    public enum SendEventTypeEnum
    {
        None = 0,
        Ping = 1,

        ConnectionHandlerAdded,
        ConnectionHandlerRemoved,
        ConnectionHandlerChanged,

        ConnectionClientAdded,
        ConnectionClientRemoved,
        ConnectionClientChanged,

        CDNTaskAdded,
        CDNTaskRemoved,
        CDNTaskChanged,

        CDNSegmentAdded,
        CDNSegmentRemoved,
        CDNSegmentChanged,
    }
}
