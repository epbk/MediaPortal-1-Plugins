using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.SSDP
{
    public abstract class UpnpService
    {
        public string ServiceType { get; protected set; }
        public string ServiceID { get; protected set; }
        public string ServiceDescriptionURL { get; protected set; }
        public string ServiceControlURL { get; protected set; }
        public string ServiceEventURL { get; protected set; }
    }
}
