using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.IptvChannels
{
    public class GenerateLinkConfiguration
    {
        [Category("DRM")]
        [DisplayName("License Server URL")]
        [Description("Licence server URL passed to DRM client application.")]
        public string DrmLicenceServer
        { get; set; }

        [Category("DRM")]
        [DisplayName("Http Arguments")]
        [Description("Http arguments passed to DRM client application.")]
        public HttpUserWebRequestArgumentsWrapper DrmHttpArguments
        { get; set; } = new HttpUserWebRequestArgumentsWrapper(new Pbk.Net.Http.HttpUserWebRequestArguments());

        [Category("Stream")]
        [DisplayName("Type")]
        [Description("Leave Unknown to determine automatically.")]
        [DefaultValue(Proxy.StreamTypeEnum.Unknown)]
        public Proxy.StreamTypeEnum StreamType
        { get; set; }

        [Category("Source")]
        [Description("URL of the source stream.")]
        public string Url
        { get; set; }

        [Category("Stream")]
        [Description("Additional arguments (for streaming engine).")]
        public string Arguments
        { get; set; }

        [Category("Stream")]
        [Description("Strreaming engine for conversion to MPEG-TS format.")]
        [DisplayName("Streaming Engine")]
        [DefaultValue(Proxy.StreamingEngineEnum.Default)]
        public Proxy.StreamingEngineEnum StreamingEngine
        { get; set; } = Proxy.StreamingEngineEnum.Default;

        [Category("Stream")]
        [Editor(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [DisplayName("Use Media Server")]
        [Description("Http media caching server for HLS and MPEG-DASH. Required by DRM.")]
        [DefaultValue(false)]
        public bool UseMediaServer
        { get; set; }

        [Category("Stream")]
        [Editor(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [DisplayName("Use MP Url Source Splitter")]
        [Description("Use MpUrlSourceSplitter url format.")]
        [DefaultValue(true)]
        public bool UseMPUrlSourceSplitter
        { get; set; } = true;

        [Browsable(false)]
        public bool UseMPUrlSourceSplitterArguents
        { get; set; } = true;

        [Category("Source")]
        [DisplayName("Http Arguments")]
        [Description("Http arguments passed to the media server and connection handler.")]
        public HttpUserWebRequestArgumentsWrapper HttpArguments
        { get; set; } = new HttpUserWebRequestArgumentsWrapper(new Pbk.Net.Http.HttpUserWebRequestArguments());

    }
}
