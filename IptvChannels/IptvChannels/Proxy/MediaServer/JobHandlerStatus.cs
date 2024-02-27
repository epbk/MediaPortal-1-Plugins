using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public enum JobHandlerStatus
    {
        Unknown,
        Terminated,
        JobAlreadyExist,
        JobCreated,
        NoSlotAvailable,
    }
}
