using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy
{
    public enum ConnectionEventTypeEnum
    {
        None,
        HandlerAdded,
        HandlerRemoved,
        HandlerUpdated,
        ClientAdded,
        ClientRemoved,
        ClientUpdated,
    }
}
