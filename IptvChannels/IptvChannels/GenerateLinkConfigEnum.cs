using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels
{
    [Flags]
    public enum GenerateLinkConfigEnum
    {
        NONE = 0,
        CDN = 1,
        MPURL_SOURCE_SPLITTER = 2,
        MPURL_SOURCE_SPLITTER_ARGS = 4
    }
}
