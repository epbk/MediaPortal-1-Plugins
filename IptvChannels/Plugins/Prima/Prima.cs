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
            this._Version = "1.1.0";
            this._Author = "Pbk";
            this._Description = "Prima";
        }
        #endregion

        #region Overrides
        public override void Initialize(Plugin plugin)
        {
            this._ChannelList.Add(new IptvChannel(this, "PRIMA", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_family/live_hd", "Prima") { Tag = "p111013" });
            this._ChannelList.Add(new IptvChannel(this, "ZOOM" , "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_zoom/live_hd", "Prima ZOOM") { Tag = "p111015" });
            this._ChannelList.Add(new IptvChannel(this, "MAX", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_max/live_hd", "Prima MAX") { Tag = "p111017" });
            this._ChannelList.Add(new IptvChannel(this, "COOL", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_cool/live_hd", "Prima COOL") { Tag = "p111014" });
            this._ChannelList.Add(new IptvChannel(this, "LOVE", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_love/live_hd", "Prima LOVE") { Tag = "p111016" });
            this._ChannelList.Add(new IptvChannel(this, "KRIMI", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_krimi/live_hd", "Prima KRIMI") { Tag = "p432829" });
            this._ChannelList.Add(new IptvChannel(this, "CNN", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_cnn/live_hd", "CNN Prima News") { Tag = "p650443" });
            this._ChannelList.Add(new IptvChannel(this, "SHOW", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_show/live_hd", "Prima SHOW") { Tag = "p899572" });
            this._ChannelList.Add(new IptvChannel(this, "STAR", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/prima_star/live_hd", "Prima STAR") { Tag = "p846043" });

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

        public override bool RefreshEpg()
        {
            this._Logger.Debug("[RefreshEpg] RefreshEpg started...");
            try
            {
                DateTime dt = DateTime.Today;
                for (int i = 0; i < 3; i++)
                {
                    JToken jEpg = Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("http://dispatcher.hybridtv.cra.cz/prima-json-rpc/",
                        post: Encoding.ASCII.GetBytes("{\"jsonrpc\":\"2.0\",\"method\":\"epg.program.bulk.list\",\"params\":{\"channelIds\":[\"prima\",\"cnn_prima_news\",\"prima_cool\",\"prima_max\",\"prima_krimi\",\"prima_love\",\"prima_zoom\",\"prima_show\",\"prima_star\"],\"from\":null,\"to\":null,\"date\":{\"date\":\"" + dt.ToString("yyyy-MM-dd") + "\"}},\"id\":\"1\"}"),
                        strContentType: Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_JSON);

                    dt = dt.AddDays(1);

                    this._ChannelList.ForEach(channel =>
                    {
                        if (!channel.Enabled || !channel.GrabEPG)
                            return;

                        if (channel.EpgProgramList == null)
                            channel.EpgProgramList = new ProgramList();
                        else
                            channel.EpgProgramList.Clear();

                        foreach (JToken j in jEpg.SelectTokens("$.result.data[?(@.playId=='" + (string)channel.Tag + "')].items[*]"))
                        {
                            string strGenre = j["type"]?.Value<string>();
                            if (strGenre != null && strGenre.Equals("movie", StringComparison.OrdinalIgnoreCase))
                                strGenre = "Film";
                            else
                            {
                                JToken jGenre = j["genres"]?.First;
                                if (jGenre != null)
                                    strGenre = (string)jGenre;
                                else
                                    strGenre = string.Empty;
                            }

                            channel.EpgProgramList.Add(new Program(
                                channel.Channel.IdChannel,
                                (DateTime)j["programStartTime"],
                                (DateTime)j["programEndTime"],
                                (string)j["title"],(string)j["description"], strGenre,
                                Program.ProgramState.None, System.Data.SqlTypes.SqlDateTime.MinValue.Value, string.Empty, string.Empty,
                                string.Empty, string.Empty, 0, string.Empty, 0
                                ));
                        }
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                this._Logger.Error("[RefreshEpg] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return false;
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
