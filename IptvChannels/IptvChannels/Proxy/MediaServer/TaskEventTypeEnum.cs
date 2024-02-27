using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public enum TaskEventTypeEnum
    {
        None = 0,
        Report,
        TaskNew,
        TaskStateChanged,
        SegmentNew,
        SegmentUpdate,
        SegmentStateChanged,
        SegmentDeleted,
    }
}
