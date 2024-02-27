using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public interface IJob
    {
        string JobTitle
        { get; }

        JobStatus JobStatus
        { get; set; }

        ManualResetEvent JobFlagDone
        { get; }

        JobStatus DoJob(ref JobResources resources);

        void JobAbort();

        int JobSlotsMax
        { get;}

        int JobSlotsInUse
        { get; set; }

        EventHandler JobEvent
        { get; set; }

        object Parent
        { get; }
        
    }
}
