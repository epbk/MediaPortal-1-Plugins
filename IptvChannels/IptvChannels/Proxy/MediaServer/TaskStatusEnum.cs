using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public enum TaskStatusEnum
    {
        Iddle = 0,
        Done,
        Available,
        Ready,
        Removed,
        Failed,
        Aborted,

        Starting = 100,
        Stopping,
        Restarting,
        Running,
        Uploading,
        Downloading,
        InQueue,
        Error
        
    }
}
