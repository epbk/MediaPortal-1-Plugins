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

        /// <summary>
        /// Serialize all properties to url argument format
        /// </summary>
        /// <returns></returns>
        public string Serialize()
        {
            return this.Serialize(new StringBuilder(256)).ToString();
        }

        /// <summary>
        /// Serialize all properties to url argument format to StringBuilder
        /// </summary>
        /// <param name="sb">StringBuilder for seialization.</param>
        /// <returns></returns>
        public StringBuilder Serialize(StringBuilder sb)
        {
            sb.Append(Plugin.URL_PARAMETER_NAME_URL).Append('=').Append(System.Web.HttpUtility.UrlEncode(this.Url));

            if (!string.IsNullOrWhiteSpace(this.DRMLicenceServer))
                sb.Append('&').Append(Plugin.URL_PARAMETER_NAME_DRM_LICENCE_SERVER).Append('=').Append(System.Web.HttpUtility.UrlEncode(this.DRMLicenceServer));

            if (!string.IsNullOrWhiteSpace(this.DRMKey))
                sb.Append('&').Append(Plugin.URL_PARAMETER_NAME_DRM_KEY).Append('=').Append(System.Web.HttpUtility.UrlEncode(this.DRMKey));

            if (this.DRMHttpArguments != null)
                sb.Append('&').Append(Plugin.URL_PARAMETER_NAME_DRM_HTTP_ARGUMENTS).Append('=').Append(System.Web.HttpUtility.UrlEncode(this.DRMHttpArguments.Serialize()));

            if (this.StreamType != Proxy.StreamTypeEnum.Unknown)
                sb.Append('&').Append(Plugin.URL_PARAMETER_NAME_STREAM_TYPE).Append('=').Append(this.StreamType);

            if (this.HttpArguments != null)
                sb.Append('&').Append(Plugin.URL_PARAMETER_NAME_HTTP_ARGUMENTS).Append('=').Append(System.Web.HttpUtility.UrlEncode(this.HttpArguments.Serialize()));

            if (this.StreamingEngine != Proxy.StreamingEngineEnum.Default)
                sb.Append('&').Append(Plugin.URL_PARAMETER_NAME_STREAMING_ENGINE).Append('=').Append(this.StreamingEngine);

            sb.Append('&').Append(Plugin.URL_PARAMETER_NAME_SEGMENT_LIST_BUILD).Append('=').Append(this.SegmentListBuild ? '1' : '0');

            return sb;
        }
    }
}
