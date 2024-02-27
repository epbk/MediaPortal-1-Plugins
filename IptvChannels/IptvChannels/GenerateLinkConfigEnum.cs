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
        FFMPEG = 1,
        CDN = 2,
        MPURL_SOURCE_SPLITTER = 4,
        MPURL_SOURCE_SPLITTER_ARGS = 8
    }
}
