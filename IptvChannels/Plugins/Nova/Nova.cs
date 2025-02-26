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
    public class Nova : SiteUtilBase
    {
        #region Constants
        #endregion

        #region Variables
        private DateTime _TsEpgLast = DateTime.MinValue;
        #endregion

        #region Properties
        [Browsable(false)]
        public override VideoQualityTypes VideoQuality
        { get; set; }
        #endregion

        #region ctor
        public Nova()
        {
            //Basics
            this._Version = "1.0.1";
            this._Author = "Pbk";
            this._Description = "Nova";
            this._EpgRefreshPeriod = 15 * 60000;
        }
        #endregion

        #region Overrides
        public override void Initialize(Plugin plugin)
        {
            this._ChannelList.Add(new IptvChannel(this, "NOVA", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova/live_fullhd", "Nova") { Tag = "nova", Identifier = 1 });
            this._ChannelList.Add(new IptvChannel(this, "CINEMA", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-cinema/live_fullhd", "Nova Cinema") { Tag = "nova-cinema", Identifier = 2 });
            this._ChannelList.Add(new IptvChannel(this, "ACTION", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-action/live_fullhd", "Nova Action") { Tag = "nova-action", Identifier = 3});
            this._ChannelList.Add(new IptvChannel(this, "FUN", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova2/live_fullhd", "Nova Fun") { Tag = "nova-fun", Identifier = 4});
            this._ChannelList.Add(new IptvChannel(this, "GOLD", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-gold/live_fullhd", "Nova Gold") { Tag = "nova-gold", Identifier = 5});
            this._ChannelList.Add(new IptvChannel(this, "LADY", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-lady/live_fullhd", "Nova Lady") { Tag = "nova-lady", Identifier = 29});
            this._ChannelList.Add(new IptvChannel(this, "SPORT1", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-sport1/live_fullhd", "Nova Sport 1") { Tag = "nova-sport-1", Identifier = 7});
            this._ChannelList.Add(new IptvChannel(this, "SPORT2", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-sport2/live_fullhd", "Nova Sport 2") { Tag = "nova-sport-2", Identifier = 8});
            this._ChannelList.Add(new IptvChannel(this, "TNLIVE", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-news/live_fullhd", "TN Live") { Tag = "tnlive-4", Identifier = 0x0400 });
            this._ChannelList.Add(new IptvChannel(this, "TNLIVE2", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-5/live_fullhd", "TN Live 2") { Tag = "tnlive-3", Identifier = 0x0300 });
            this._ChannelList.Add(new IptvChannel(this, "TNLIVE3", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-4/live_fullhd", "TN Live 3") { Tag = "tnlive-2", Identifier = 0x0200 });
            this._ChannelList.Add(new IptvChannel(this, "TNLIVE4", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-gc1/live_fullhd", "TN Live 4") { Tag = "tnlive-7", Identifier = 0x0700 });
            this._ChannelList.Add(new IptvChannel(this, "TNSVET", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-aptn/live_fullhd", "TN Live Svět") { Tag = "tnlive-1", Identifier = 0x0100 });
            this._ChannelList.Add(new IptvChannel(this, "TNLIVESNEMOVNA", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-snemovna/live_fullhd", "TN Live Sněmovna") { Tag = "tnlive-6", Identifier = 0x0600 });
            this._ChannelList.Add(new IptvChannel(this, "TNLIVEUKRAINE", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-gc2/live_fullhd", "TN Live Ukraine") { Tag = "tnlive-8", Identifier = 0x0800});

            //Initialize base
            base.Initialize(plugin);
        }

        public override LinkResult GetStreamUrl(IptvChannel channel)
        {
            lock (channel)
            {
                this._Logger.Debug("[GetStreamUrl] Query:{0}  ID:{1}", channel.Name, channel.Id);

                return new LinkResult() { Url = channel.Url };
            }
        }

        public override bool RefreshEpg()
        {
            this._Logger.Debug("[RefreshEpg] RefreshEpg started...");
            try
            {
                DateTime timeNow = DateTime.Now;

                JToken jTn = Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("http://dispatcher.hybridtv.cra.cz/nova_tn_apis/api/v5/block-display?api_key=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImp0aSI6ImNiNThkMmNjMjc5MDhmNDNkNjc3OWJjMjg5MTdjODhmMTM2YzA4MmM3Y2M1NGJlMDQxYWMwZjcxYzE1ZjRhMWJiZTM5NGYzOWYxMTVlMTk1In0.eyJhdWQiOiJjbXNfdG4iLCJqdGkiOiJjYjU4ZDJjYzI3OTA4ZjQzZDY3NzliYzI4OTE3Yzg4ZjEzNmMwODJjN2NjNTRiZTA0MWFjMGY3MWMxNWY0YTFiYmUzOTRmMzlmMTE1ZTE5NSIsImlhdCI6MTcyOTA4MTUwMywibmJmIjoxNzI5MDgxNTAzLCJleHAiOjQxMDI0NDExOTksInN1YiI6IjcyNiIsInNjb3BlcyI6W119.Yb1ElIndq_SCvz5zcAU5zrIqF25rKYHgUXUuLWqU8ryuobm5qOcL5WEeaLjTOxOK-pL8-KRL3lT28ad10WISCyJCWa1tLxs4r43P-OT-MhbN3HLUXTs8UshYbUv0HZ9h9hxFIQiKt_yvtE3V4rk0WVE9dYvjRZMPprocWvhXWpRMmQi74n5wJ6enla1L_XRGtgkblh2CGvkjuN5q5RzKoRvLUrQtzhWs9xWNiJusN-qFZ22puH3xvq2QjdjMcYWPBXZZeJ724BgtKXjV8ck7kZHD9kg3qEYpBk8R2rPJcE307N4oVNHcoQ6x9yIFIphGLFkD9GDHBtUlUmY5oEQ37A",
                    post: Encoding.ASCII.GetBytes("{\"blockId\":\"8018\"}"), strContentType: Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_JSON);

                JToken jEpg = null;
                if ((DateTime.Now - this._TsEpgLast).TotalHours >= 2)
                {
                    DateTime dt = DateTime.Today;
                    jEpg = Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("http://dispatcher.hybridtv.cra.cz/nova_apis_new/api/v3/epg.display?api_key=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImp0aSI6ImFlZjk3NDM5NjRhMGRmMDYyNzIwYjkyZDg4NzIwYjM2NWI2YzNjMDc5MzA2ODRlOTRhYzI3ODg2NTcxNGEwODc3YmUwNzJiNjcxZGVmMjRkIn0.eyJhdWQiOiJjbXNfaGJidHYiLCJqdGkiOiJhZWY5NzQzOTY0YTBkZjA2MjcyMGI5MmQ4ODcyMGIzNjViNmMzYzA3OTMwNjg0ZTk0YWMyNzg4NjU3MTRhMDg3N2JlMDcyYjY3MWRlZjI0ZCIsImlhdCI6MTY5OTU2NzcyMCwibmJmIjoxNjk5NTY3NzIwLCJleHAiOjQxMDI0NDExOTksInN1YiI6IjcyNiIsInNjb3BlcyI6W119.ePiDBYb4SLSCTB_m8rIUXFCU20kBxjPk5aFfbq69TiTHB-u21FHBRANP0sHom0Kuq_D84hrSZBVqBeuVCGLhSkA-aYxysgyQ0LbnSoV0G7xk6G5TttbWAiP4xpSgBooFl17YA-pHpnmhOwMPxnjKuzQVuIXz9LeNdak_06P-yZkNNslAN4ECqScME15J7wWAXCk64vV_3nqJdEnPbSUlrsqVAWKFzTmnNSN0QUKrqub07kqK2DsdybOBzx0kFhSMng0rOnYxNMGUdDG5ESNQYUtHEDE7_4ebzhDbl2R5qyA2wLh473_b2HINu9-aFvXUXql0kdNK6iHZ_FACfTS2SA",
                        post: Encoding.ASCII.GetBytes("{\"criteria\":{\"time\":{\"from\":\"" + dt.ToString("yyyy-MM-dd") + "\",\"to\":\"" + dt.AddDays(2).ToString("yyyy-MM-dd") + "\"},\"channel\":{\"from\":\"1\",\"to\":\"100\"}},\"output\":{\"channelFormat\":\"all\"}}"), strContentType: Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_JSON);

                    this._TsEpgLast = DateTime.Now;
                }

                this._ChannelList.ForEach(channel =>
                {
                    if (!channel.Enabled || !channel.GrabEPG)
                        return;

                    if (channel.Identifier >= 0x0100)
                    {
                        if (channel.EpgProgramList == null)
                            channel.EpgProgramList = new ProgramList();
                        else
                            channel.EpgProgramList.Clear();

                        int iID = channel.Identifier >> 8;
                        JToken jTitle;
                        JToken j = jTn.SelectTokens("$..action.params.payload.contentId").FirstOrDefault(p => (string)p == (string)channel.Tag);
                        if (j == null)
                            j = jTn.SelectTokens("$..action.params.payload.channelId").FirstOrDefault(p => (int)p == iID);

                        if (j != null)
                        {
                            do
                            {
                                j = j.Parent;
                                jTitle = j is JObject ? j["title"] : null;
                            }
                            while (jTitle == null && j.Parent != null);

                            if (jTitle != null)
                            {
                                channel.EpgProgramList.Add(new Program(
                                    channel.Channel.IdChannel,
                                    (DateTime)j["progressBar"]["startAt"],
                                    (DateTime)j["progressBar"]["endAt"],
                                    (string)jTitle, string.Empty, "Special",
                                    Program.ProgramState.None, System.Data.SqlTypes.SqlDateTime.MinValue.Value, string.Empty, string.Empty,
                                    string.Empty, string.Empty, 0, string.Empty, 0
                                    ));
                            }
                            return;
                        }
                    }
                    else if (jEpg != null)
                    {
                        if (channel.EpgProgramList == null)
                            channel.EpgProgramList = new ProgramList();
                        else
                            channel.EpgProgramList.Clear();

                        //JToken jID = jEpg.SelectToken("$.availableChannels[?(@.name=='" + channel.Name + "')].id");
                        //if (jID != null)
                        {
                            foreach (JToken j in jEpg.SelectTokens("$.program..schedule[?(@.id==" + channel.Identifier + ")].segments[*].items[*]"))
                            {
                                channel.EpgProgramList.Add(new Program(
                                   channel.Channel.IdChannel,
                                   (DateTime)j["startAt"],
                                   (DateTime)j["endAt"],
                                   (string)j["title"], (string)j["description"], string.Empty,
                                   Program.ProgramState.None, System.Data.SqlTypes.SqlDateTime.MinValue.Value, string.Empty, string.Empty,
                                   string.Empty, string.Empty, 0, string.Empty, 0
                                   ));
                            }
                        }

                    }
                    else
                        channel.EpgProgramList = null;
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

        #endregion
    }
}
