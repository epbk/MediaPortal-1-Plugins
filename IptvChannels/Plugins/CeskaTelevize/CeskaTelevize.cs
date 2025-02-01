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
    public class CeskaTelevize : SiteUtilBase
    {
        #region Constants
        private const string _URL_HBBTV_BASE = "https://hbbtv.ceskatelevize.cz";
        private const string _URL_HBBTV_LIVE = _URL_HBBTV_BASE + "/ivysilani/services/data/live.json";
        //private const string _URL_HBBTV_ONLINE = _URL_HBBTV_BASE + "/ivysilani/services/data/online.json";
        private const string _URL_HBBTV_ONLINE = _URL_HBBTV_BASE + "/online/services/data/online.json";
        private const string _URL_HBBTV_VIDEOPLAYER = _URL_HBBTV_BASE + "/modules/videoplayer-v3/services/videodetail.php?id={0}&getQualities=1&qualities=%7B%22VOD%22%3A%22max1080p%22%2C%22LIVE%22%3A%22max1080p%22%7D&newodpl=1&{1}";

        private const int _CACHE_LIVETIME_ONLINE = 120; //[s]

        private const string _URL_API = _URL_HBBTV_BASE + "/ct-api-graphql/";

        //[\"ct1\",\"ct2\",\"sport\",\"ct24\",\"art\",\"decko\",\"ct3\",\"ct24plus\",\"ctSportExtra\"]
        private const string _QUERY_POST_PROGRAM_DAILY = "{{\"query\":\"query TVProgramDailyChannelsPlanV2($channels: [String!]!, $date: Date!){{TVProgramDailyChannelsPlanV2(channels: $channels, date: $date) {{channel, channelSettings {{channelColor, channelLogo, channelName}}, currentBroadcast {{legacyEncoder, item {{aboveTitle, alternative, description, genre, idec, imageUrl, ivysilani, length, liveOnly, originalTitle, part, programSource, properties {{code, description}}, regional, show, sidp, startTime, title, vps, playable, isPlayableNow, start, end}}}}, program {{aboveTitle, alternative, description, genre, idec, imageUrl, ivysilani, length, liveOnly, originalTitle, part, programSource, properties {{code, description}}, regional, show, sidp, startTime, title, vps, playable, isPlayableNow, start, end}}}}}}\",\"variables\":{{\"channels\":[\"ctSportExtra\"],\"date\":\"{0}\"}}}}";

        #endregion

        #region Variables
        private DateTime _CacheOnlineTs = DateTime.MinValue;
        private JToken _CacheOnline = null;
        private DateTime _CacheOnline2Ts = DateTime.MinValue;
        private JToken _CacheOnline2 = null;
        private IptvChannel[] _BasicChannels = null;
        #endregion

        #region Properties
        [Category("Channels"), Description("Include channels ČT 1, ČT 2, ČT 24, ČT Sport and ČT :D/Art"), DisplayName("Include Basic Channels"), Browsable(true)]
        [DBField()]
        [Editor(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public bool IncludeBasicChannels
        { 
            get
            {
                return this._IncludeBasicChannels;
            }
            set
            {
                if (value != this._IncludeBasicChannels)
                {
                    this._IncludeBasicChannels = value;

                    this.updateChannelList();

                    if (this._Initialized)
                        this._ParentPlugin.CheckSiteChannels(this);

                }
            }
        }private bool _IncludeBasicChannels = false;

        [Browsable(false)]
        public override VideoQualityTypes VideoQuality
        { get; set; }
        #endregion

        #region ctor
        public CeskaTelevize()
        {
            //Basics
            this._Version = "1.1.7";
            this._Author = "Pbk";
            this._Description = "Česká Televize";
            this._EpgRefreshPeriod = 15 * 60000;
        }
        #endregion

        #region Overrides
        public override void Initialize(Plugin plugin)
        {
            this._BasicChannels = new IptvChannel[]
            {
              new IptvChannel(this, "CT1", "CH_1", "ČT 1"),
              new IptvChannel(this, "CT2", "CH_2", "ČT 2"),
              new IptvChannel(this, "CT3", "CH_3", "ČT 24"),
              new IptvChannel(this, "CT4", "CH_4", "ČT sport"),
              new IptvChannel(this, "CT5", "CH_5", "ČT :D"),
              new IptvChannel(this, "CT6", "CH_6", "ČT art")
            };

            this._ChannelList.AddRange(this._BasicChannels);

            this._ChannelList.Add(new IptvChannel(this, "CT9", "CT9", "CT9") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CT25", "CH_25", "CT25") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CT26", "CH_26", "CT26") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CT27", "CH_27", "CT27") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CT28", "CH_28", "CT28") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CT29", "CH_29", "CT29") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CT30", "CH_30", "CT30") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CT31", "CH_31", "CT31") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CT32", "CH_32", "CT32") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CTMOBILE", "CH_MP_01", "CTMOBILE") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CTMOBILE2", "CH_MP_02", "CTMOBILE2") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CTMOBILE03", "CH_MP_03", "CTMOBILE03") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CTMOBILE04", "CH_MP_04", "CTMOBILE04") { Tag = "ČT sport Plus" });
            this._ChannelList.Add(new IptvChannel(this, "CTMOBILE05", "CH_MP_05", "CTMOBILE05") { Tag = "ČT sport Plus" });

            //Test MEPG-DASH with Widevine
            this._ChannelList.Add(new IptvChannel(this, "CT2_DASH", "CH_2", "CT2 - DASH") { Tag = "DASH" });

            this.updateChannelList();

            //Initialize base
            base.Initialize(plugin);

        }

        public override LinkResult GetStreamUrl(IptvChannel channel)
        {
            lock (channel)
            {
                LinkResult result = new LinkResult();
                this._Logger.Debug("[GetStreamUrl] Query:" + channel.Name + "  ID:" + channel.Id);

                if (channel.Tag is string && (string)channel.Tag == "DASH")
                    return getStreamUrlDash(channel.Url);
                else
                    return getStreamUrl(channel.Url);
            }
        }

        public override bool RefreshEpg()
        {
            this._Logger.Debug("[RefreshEpg] RefreshEpg started...");
            try
            {
                DateTime timeNow = DateTime.Now;

                this._ChannelList.ForEach(channel =>
                    {
                        if (!channel.Enabled)
                            return;

                        if (channel.EpgProgramList == null)
                            channel.EpgProgramList = new ProgramList();
                        else
                            channel.EpgProgramList.Clear();

                        if ((string)channel.Tag == "ČT sport Plus")
                        {
                            channel.EpgProgramList.AddRange(this.getEventListOnline2(channel, timeNow));

                                //this.getEventListOnline(channel, timeNow).ForEach(p =>
                                //    {
                                //        if (!string.IsNullOrWhiteSpace(p.Description))
                                //        {
                                //            Program pr = channel.EpgProgramList.Find(item => string.IsNullOrWhiteSpace(item.Description) 
                                //                && item.StartTime == p.StartTime && item.EndTime == p.StartTime);

                                //            if (pr != null)
                                //                pr.Description = p.Description;
                                //        }

                                //    });
                            }
                    });

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
        private List<Program> getEventListOnline(IptvChannel iptvChannel, DateTime dt)
        {
            if (this._CacheOnline == null || (DateTime.Now - this._CacheOnlineTs).TotalSeconds > _CACHE_LIVETIME_ONLINE)
            {
                byte[] post = Encoding.UTF8.GetBytes(string.Format(_QUERY_POST_PROGRAM_DAILY, DateTime.Today.ToString("yyyy-MM-dd")));

                JToken jData = Pbk.Net.Http.HttpUserWebRequest.Download<JToken>(_URL_API, strContentType: "application/json", post: post);

                if (jData == null)
                {
                    this._Logger.Error("[getEventListOnline] Unable to get web page.");
                    return null;
                }

                this._CacheOnlineTs = DateTime.Now;
                this._CacheOnline = jData;
            }

            DateTime dtStart, dtStop;
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);

            List<Program> result = new List<Program>();

            foreach (JToken j in this._CacheOnline["data"]["TVProgramDailyChannelsPlanV2"])
            {
                JToken jBroadcast = j["currentBroadcast"];

                if (jBroadcast is JToken && jBroadcast.Type == JTokenType.Object)
                {
                    JToken jItem = jBroadcast["item"];

                    if ((string)jBroadcast["legacyEncoder"] == iptvChannel.Id)// && ((bool)jItem["isPlayableNow"]))
                    {
                        dtStart = ((DateTime)jItem["start"]).AddHours(utcOffset.Hours);
                        dtStop = ((DateTime)jItem["end"]).AddHours(utcOffset.Hours);

                        if (dtStart.Date.Equals(dt.Date))
                        {
                            //string strGenre = string.Empty;
                            //switch (iptvChannel.Id)
                            //{
                            //    case "CT4":
                            //    case "CT9":
                            //    case "CT25":
                            //    case "CT26":
                            //    case "CT27":
                            //    case "CT28":
                            //    case "CT29":
                            //    case "CT30":
                            //    case "CT31":
                            //    case "CT32":
                            //    case "CTmobile":
                            //    case "CTmobile2":
                            //    case "CTmobile03":
                            //    case "CTmobile04":
                            //    case "CTmobile05":
                            //        strGenre = "Sport";
                            //        break;
                            //}

                            result.Add(new Program(iptvChannel.Channel.IdChannel, dtStart, dtStop, (string)jItem["title"], (string)jItem["description"], "Sport",
                                Program.ProgramState.None, System.Data.SqlTypes.SqlDateTime.MinValue.Value, String.Empty, String.Empty,
                                String.Empty, String.Empty, 0, String.Empty, 0));
                        }
                    }
                }
            }

            return result;
        }

        private List<Program> getEventListOnline2(IptvChannel iptvChannel, DateTime dt)
        {
            if (this._CacheOnline2 == null || (DateTime.Now - this._CacheOnline2Ts).TotalSeconds > _CACHE_LIVETIME_ONLINE)
            {
                JToken jData = Pbk.Net.Http.HttpUserWebRequest.Download<JToken>(_URL_HBBTV_ONLINE);

                if (jData == null)
                {
                    this._Logger.Error("[getEventListOnline] Unable to get web page.");
                    return null;
                }

                this._CacheOnline2Ts = DateTime.Now;
                this._CacheOnline2 = jData;
            }

            DateTime dtStart, dtStop;
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);

            List<Program> result = new List<Program>();

            Regex regexT = new Regex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z");

            foreach (JToken j in this._CacheOnline2)
            {
                if (((string)j["encoder"]).Equals(iptvChannel.Url, StringComparison.CurrentCultureIgnoreCase))
                {
                    TimeSpan tsStart = TimeSpan.ParseExact((string)j["time_str"], "hh\\:mm", null);
                    TimeSpan tsEnd = TimeSpan.ParseExact((string)j["time_end_str"], "hh\\:mm", null);
                    TimeSpan tsDur = tsEnd - tsStart;
                    if (tsDur.Ticks < 0)
                        tsDur = tsDur.Add(new TimeSpan(24, 0, 0));

                    JToken jT = j["time"];
                    if (jT != null)
                        dtStart = new DateTime(1970, 1, 1).AddSeconds((int)j["time"]).AddHours(utcOffset.Hours);
                    else
                    {
                        jT = j["id"];
                        if (jT != null)
                        {
                            Match m = regexT.Match((string)jT);
                            if (m.Success)
                            {
                                dtStart = DateTime.Parse(m.Value);
                                goto nxt;
                            }
                        }

                        dtStart = DateTime.Today + tsStart;
                    }

                nxt:
                    dtStop = dtStart.Add(tsDur);

                    if (dtStart.Date.Equals(dt.Date))
                    {
                        result.Add(new Program(iptvChannel.Channel.IdChannel, dtStart, dtStop, (string)j["programTitle"], string.Empty, "Sport",
                            Program.ProgramState.None, System.Data.SqlTypes.SqlDateTime.MinValue.Value, string.Empty, string.Empty,
                            string.Empty, string.Empty, 0, string.Empty, 0));
                    }
                }
            }

            return result;
        }

        private LinkResult getStreamUrl(string strId)
        {

            if (string.IsNullOrEmpty(strId))
            {
                this._Logger.Error("[getStreamUrl] Invalid video ID.");
                return null;
            }

            JToken j = Pbk.Net.Http.HttpUserWebRequest.Download<JToken>(
                string.Format(_URL_HBBTV_VIDEOPLAYER, strId, (DateTime.Now - new DateTime(1970, 1, 1)).Ticks / 10000));

            if (j == null)
            {
                this._Logger.Error("[getStreamUrl] Failed to get player data: " + strId);
                return null;
            }

            try
            {
                return new LinkResult() { Url = (string)j.SelectToken("detail.playlist[0].streamUrls.main") };
            }
            catch (Exception ex)
            {
                _Logger.Error("[getStreamUrl] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return null;
            }

            //this._Logger.Error("[getStreamUrl] Stream link not found: " + strLink);
            //return null;
        }

        private LinkResult getStreamUrlDash(string strId)
        {
            if (string.IsNullOrEmpty(strId))
            {
                this._Logger.Error("[getStreamUrlDash] Invalid video ID.");
                return null;
            }

            JToken j = Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("https://api.ceskatelevize.cz/video/v1/playlist-live/v1/stream-data/channel/" 
                + strId + "?canPlayDrm=true&streamType=dash&quality=web&maxQualityCount=5");

            if (j == null)
            {
                this._Logger.Error("[getStreamUrlDash] Failed to get player data: {0}", strId);
                return null;
            }

            try
            {
                return new LinkResult()
                {
                    Url = (string)j.SelectToken("streamUrls.main"),
                    DRMLicenceServer = "https://ivys-wvproxy.o2tv.cz/license?access_token=c3RlcGFuLWEtb25kcmEtanNvdS1wcm9zdGUtbmVqbGVwc2k="
                };
            }
            catch (Exception ex)
            {
                _Logger.Error("[getStreamUrlDash] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return null;
            }
        }

        private void updateChannelList()
        {
            for (int i = 0; i < this._BasicChannels.Length; i++)
            {
                this._BasicChannels[i].Enabled = this._IncludeBasicChannels;
            }
        }
        #endregion
    }
}
