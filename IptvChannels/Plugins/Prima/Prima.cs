using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TvDatabase;
using System.ComponentModel;
using NLog;
using MediaPortal.Pbk.Cornerstone.Database;

namespace MediaPortal.IptvChannels.SiteUtils.Sites
{
    public class Prima : SiteUtilBase
    {
        #region Constants
        #endregion

        #region Variables
        #endregion

        #region Properties
        [Browsable(false)]
        public override VideoQualityTypes VideoQuality
        { get; set; }
        #endregion

        #region ctor
        public Prima()
        {
            //Basics
            this._Version = "1.0.0";
            this._Author = "Pbk";
            this._Description = "Prima";
        }
        #endregion

        #region Overrides
        public override void Initialize(Plugin plugin)
        {
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_family/live_hd",  "p111013" } };
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_zoom/live_hd",  "p111015" } };
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_max/live_hd",  "p111017" } };
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_cool/live_hd",  "p111014" } };
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_love/live_hd", "p111016" } };
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_krimi/live_hd",  "p432829" } };
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_cnn/live_hd",  "p650443" } };
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_star/live_hd",  "p846043" } };
            //"http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_show/live_hd",  "p899572" } };

            //this._ChannelList.Add(new IptvChannel(this, "PRIMA", "p111013", "Prima Web") { Tag = "iPrima" });
            this._ChannelList.Add(new IptvChannel(this, "ZOOM" , "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_zoom/live_hd", "Prima ZOOM Web") { Tag = "p111015" });
            //this._ChannelList.Add(new IptvChannel(this, "MAX", "p111017", "Prima MAX Web") { Tag = "iPrima" });
            //this._ChannelList.Add(new IptvChannel(this, "COOL", "p111014", "Prima COOL Web") { Tag = "iPrima" });
            //this._ChannelList.Add(new IptvChannel(this, "LOVE", "p111016", "Prima LOVE Web") { Tag = "iPrima" });
            //this._ChannelList.Add(new IptvChannel(this, "KRIMI", "p432829", "Prima KRIMI Web") { Tag = "iPrima" });
            //this._ChannelList.Add(new IptvChannel(this, "CNN", "p650443", "Prima CNN News Web") { Tag = "iPrima" });
            //this._ChannelList.Add(new IptvChannel(this, "SHOW", "p899572", "Prima SHOW Web") { Tag = "iPrima" });
            //this._ChannelList.Add(new IptvChannel(this, "STAR", "p846043", "Prima STAR Web") { Tag = "iPrima" });

            //Initialize base
            base.Initialize(plugin);
        }

        public override LinkResult GetStreamUrl(IptvChannel channel)
        {
            lock (channel)
            {
                this._Logger.Debug("[GetStreamUrl] Query:" + channel.Name + "  ID:" + channel.Id);

                return getStreamUrl(channel.Url, (string)channel.Tag);
            }
        }

        #endregion

        #region Private methods
        private LinkResult getStreamUrl(string strUrlPrimary, string strId)
        {
            if (!string.IsNullOrWhiteSpace(strUrlPrimary))
            {
                Pbk.Net.Http.HttpUserWebRequest.Download<byte[]>(strUrlPrimary, out string strRedirect, out System.Net.HttpStatusCode httpStatus,
                    method: Pbk.Net.Http.HttpMethodEnum.HEAD);

                if (httpStatus == System.Net.HttpStatusCode.OK)
                    return new LinkResult() { Url = strUrlPrimary };
            }

            if (string.IsNullOrEmpty(strId))
            {
                this._Logger.Error("[getStreamUrl] Invalid channel ID.");
                return null;
            }

            string strUrl = string.Format("https://api.play-backend.iprima.cz/prehravac/init-embed?_infuse=1&productId={0}&embed=true", strId);

            string strContent = Pbk.Net.Http.HttpUserWebRequest.Download<string>(strUrl,
                strReferer: "https://api.play-backend.iprima.cz/prehravac/embedded?id=" + strId);

            if (string.IsNullOrEmpty(strContent))
            {
                this._Logger.Error("[getStreamUrl] Failed to get web page: " + strUrl);
                return null;
            }

            int iIdxStart = strContent.IndexOf("https://prima-ott-live-sec.ssl.cdn.cra.cz");
            if (iIdxStart > 0)
            {
                return new LinkResult() { Url = strContent.Substring(iIdxStart, strContent.IndexOf('\'', iIdxStart) - iIdxStart) };
            }

            this._Logger.Error("[getStreamUrl] Stream link not found: " + strId);
            return null;
            
            
            //https://api.play-backend.iprima.cz/api/v1/products/id-p111015/play
        }
        #endregion
    }
}
