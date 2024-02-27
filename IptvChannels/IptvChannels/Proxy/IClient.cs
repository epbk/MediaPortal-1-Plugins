using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy
{
    public interface IClient
    {
        ulong DataSent { get; }
        ulong PacketErrors { get; }
        DateTime DataSentFirstTS { get; }
    }
}
