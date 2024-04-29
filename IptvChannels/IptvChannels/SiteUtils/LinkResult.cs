using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.SiteUtils
{
    public class LinkResult
    {
        /// <summary>
        /// Final video url link
        /// </summary>
        public string Url;

        /// <summary>
        /// Optional DRM Licence Server url
        /// </summary>
        public string DRMLicenceServer;

        /// <summary>
        /// Optional Stream type. Leave as Unknown to determine automatically later by connection handler.
        /// </summary>
        public Proxy.StreamTypeEnum StreamType = Proxy.StreamTypeEnum.Unknown;

        /// <summary>
        /// Optional http fields like UserAgent, Cookies, etc.
        /// </summary>
        public Pbk.Net.Http.HttpUserWebRequestArguments HttpArguments;
    }
}
