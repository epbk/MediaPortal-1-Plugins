using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public enum StreamQualityEnum
    {
        Default = 0,
        SD = 768,
        HD720 = 1280,
        HD1080 = 1920,
        UHD2K = 2048,
        UHD4K = 3840,
        UHD8K = 7680,
    }
}
