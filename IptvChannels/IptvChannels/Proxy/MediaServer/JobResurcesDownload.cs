using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class JobResurcesDownload : JobResources
    {
        public Pbk.Net.Http.HttpUserWebRequest WebRequest;
        public byte[] Buffer;
    }
}
