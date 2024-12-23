﻿using System;
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
        /// Optional DRM Http headers; e.g. "X-AxDRM-Message"
        /// </summary>
        public Pbk.Net.Http.HttpUserWebRequestArguments DRMHttpArguments;

        /// <summary>
        /// Explicit DEM key in format: <kid>:<key>
        /// </summary>
        public string DRMKey;

        /// <summary>
        /// Optional Stream type. Leave as Unknown to determine automatically later by connection handler.
        /// </summary>
        public Proxy.StreamTypeEnum StreamType = Proxy.StreamTypeEnum.Unknown;

        /// <summary>
        /// Optional http fields like UserAgent, Cookies, etc.
        /// </summary>
        public Pbk.Net.Http.HttpUserWebRequestArguments HttpArguments;

        /// <summary>
        /// Streaming engine for conversion of HLS/DASH to MPEG-TS stream
        /// </summary>
        public Proxy.StreamingEngineEnum StreamingEngine = Proxy.StreamingEngineEnum.Default;

        /// <summary>
        /// True to keep/update internal HLS/DASH segment list. It has abilty to predownload subsequent segment ahead.
        /// </summary>
        public bool SegmentListBuild = true;
    }
}
