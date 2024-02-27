using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public enum JobStatus
    { 
        Iddle = 0,
        Unknown,
        Done,
        Error,
        NotFound, 
        NotAvailable,
        Abort,
        Complete,
        Terminated,
        Failed,
        Started = 100,
        Running,
        Processing,
        Timeout
    }
}
