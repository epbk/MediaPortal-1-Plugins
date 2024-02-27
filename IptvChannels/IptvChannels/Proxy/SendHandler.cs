using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy
{
    public delegate int SendHandler(byte[] buffer, int iOffset, int iLength);
}
