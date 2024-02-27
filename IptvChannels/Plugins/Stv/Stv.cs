using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Threading;
using TvLibrary;
using TvDatabase;
using System.ComponentModel;
using NLog;

namespace MediaPortal.IptvChannels.SiteUtils.Sites
{
    public class Stv : SiteUtilBase
    {
        #region Constants
        private const string URL_BASE = "http://rtvs.sk";
        const string URL_EPG = URL_BASE + "/rio2016/program/"; //http://rtvs.sk/rio2016/program/2016-08-12
        #endregion

        #region Variables
        private bool _IncludeBasicChannels = false;
        #endregion

        #region Properties
        [Category("IptvChannelsUserConfiguration"), Description("Include channels STV1 and STV2."), Browsable(true)]
        public bool IncludeBasicChannels
        {
            get
            {
                return this._IncludeBasicChannels;
            }
            set
            {
                this._IncludeBasicChannels = value;
            }
        }
        #endregion

        #region ctor
        public Stv()
        {
            //Basics
            this._Version = "1.0.0";
            this._Author = "Pablik";
            this._Description = "Slovenská Televízia - LOH Rio 2016";
            this._EpgRefreshPeriod = 60;
        }
        #endregion

        #region Overrides
        public override void Initialize()
        {
            //Create Channel list
            if (this.IncludeBasicChannels)
            {
                this._ChannelList.Add(new IptvChannel(this, "stv1", "/rio2016/live/1", "STV 1"));
                this._ChannelList.Add(new IptvChannel(this, "stv2", "/rio2016/live/2", "STV 2"));
            }
            this._ChannelList.Add(new IptvChannel(this, "stvrio1", "/rio2016/live/7", "RIO 1"));
            this._ChannelList.Add(new IptvChannel(this, "stvrio2", "/rio2016/live/8", "RIO 2"));
            this._ChannelList.Add(new IptvChannel(this, "stvrio3", "/rio2016/live/9", "RIO 3"));
            this._ChannelList.Add(new IptvChannel(this, "stvrio4", "/rio2016/live/10", "RIO 4"));
            this._ChannelList.Add(new IptvChannel(this, "stvrio5", "/rio2016/live/11", "RIO 5"));
            this._ChannelList.Add(new IptvChannel(this, "stvrio6", "/rio2016/live/12", "RIO 6"));
            this._ChannelList.Add(new IptvChannel(this, "stvrio7", "/rio2016/live/13", "RIO 7"));
            this._ChannelList.Add(new IptvChannel(this, "stvrio8", "/rio2016/live/14", "RIO 8"));

            //Initialize base
            base.Initialize();

        }

        public override string GetStreamUrl(IptvChannel channel)
        {
            lock (channel)
            {
                this.Logger.Debug("[GetStreamUrl] Query:" + channel.Name + "  ID:" + channel.Id);

                //Try use cached url first
                if (!string.IsNullOrEmpty(channel.LastFinalUrl) && (DateTime.Now - channel.LastFinalUrlTS).TotalSeconds < 60)
                {
                    this.Logger.Debug("[GetStreamUrl] Cached url:" + channel.LastFinalUrl);
                    return channel.LastFinalUrl;
                }

                XmlDocument xmldoc = WebTools.GetWebData<XmlDocument>(URL_BASE + channel.Url);
                if (xmldoc == null)
                {
                    this.Logger.Error("[GetStreamUrl] Failed to get web page.");
                    return null;
                }

                Regex regexPlaylistUrl = new Regex("\"playlist\"\\s*: \"(?<url>[^\"]+)\"");
                Regex regexFinalUrl = new Regex("\"file\"\\s*: \"(?<url>[^\"]+)\"");

                XmlNode nodePlaylist = xmldoc.SelectSingleNode("//script/text()[contains(.,'playlist')]");
                if (nodePlaylist != null)
                {
                    Match m = regexPlaylistUrl.Match(nodePlaylist.Value);
                    if (m.Success)
                    {
                        string strFileContent = WebTools.GetWebData<string>(m.Groups["url"].Value);
                        if (!string.IsNullOrEmpty(strFileContent))
                        {
                            m = regexFinalUrl.Match(strFileContent);
                            if (m.Success)
                            {
                                //Load m3u8 content
                                string strLinkUrl = m.Groups["url"].Value;
                                string strM3u8Content = WebTools.GetWebData<string>(strLinkUrl, Encoding.UTF8);
                                if (!string.IsNullOrEmpty(strM3u8Content))
                                {
                                    string strResult = strLinkUrl;
                                    string[] lines = strM3u8Content.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    List<VideoDescription> linkList = new List<VideoDescription>();
                                    Regex regexBandwith = new Regex("BANDWIDTH=(?<bandwith>[\\d]+)");
                                    for (int i = 0; (i + 1) < lines.Length; i++)
                                    {

                                        //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=1001472,RESOLUTION=720x404
                                        //chunklist_w1152493450_b1001472.m3u8?auth=b64%3AX2FueV98MTQ3MTE3MzQ1MnxkYmM5OTdkMWQ2NTE4N2U0NWQ3NDY5MTI1NGY3NDgxNWZkMGFmMDRl
                                        //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=655360,RESOLUTION=512x288
                                        //chunklist_w1152493450_b655360.m3u8?auth=b64%3AX2FueV98MTQ3MTE3MzQ1MnxkYmM5OTdkMWQ2NTE4N2U0NWQ3NDY5MTI1NGY3NDgxNWZkMGFmMDRl

                                        string line = lines[i].Trim();
                                        if (line.StartsWith("#EXT-", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            m = regexBandwith.Match(line);
                                            if (m.Success)
                                            {
                                                linkList.Add(new VideoDescription() { Bandwith = int.Parse(m.Groups["bandwith"].Value), Url = lines[i + 1].Trim() });
                                            }
                                        }

                                    }

                                    if (linkList.Count > 0)
                                    {
                                        //Sort by bitrate
                                        linkList.Sort((p1, p2) => p2.Bandwith.CompareTo(p1.Bandwith));
                                        strResult = strLinkUrl.Substring(0, strLinkUrl.LastIndexOf("/") + 1) + linkList[0].Url;
                                    }

                                    this.Logger.Debug("[GetStreamUrl] Link found:" + strResult);
                                    channel.LastFinalUrl = strResult;
                                    channel.LastFinalUrlTS = DateTime.Now;
                                    return strResult;
                                }
                                else
                                {
                                    this.Logger.Debug("[GetStreamUrl] Link not found.");
                                    return null;
                                }
                            }
                        }
                    }

                }

                this.Logger.Error("[GetStreamUrl] Link not found.");
                return null;
            }

        }

        public override bool RefreshEpg()
        {
            this.Logger.Debug("[RefreshEpg] RefreshEpg started...");
            try
            {
                foreach (IptvChannel channel in this._ChannelList)
                {
                    if (channel.EpgProgramList == null) channel.EpgProgramList = new ProgramList();
                    else channel.EpgProgramList.Clear();
                }

                //Get list for 2 days
                DateTime[] dayList = new DateTime[2] { DateTime.Today, DateTime.Today.AddDays(1) };

                Regex regexTime = new Regex("new Date\\((?<year>\\d+),(?<month>\\d+),(?<day>\\d+),(?<hr>\\d+),(?<min>\\d+)\\)");
                Regex regexData = new Regex("\\</strong\\>(?<title>[^\\<]+)\\</div\\>.*href\\=\"http://rtvs\\.sk/rio2016/live\\?c\\=(?<channel>\\d+)\"");

                foreach (DateTime dt in dayList)
                {
                    XmlDocument xmldoc = WebTools.GetWebData<XmlDocument>(string.Format("{0}{1}-{2}-{3}", URL_EPG, dt.Year, dt.Month.ToString("00"), dt.Day.ToString("00")), Encoding.UTF8);
                    if (xmldoc == null)
                    {
                        this.Logger.Error("[RefreshEpg] Failed to get web page.");
                        return false;
                    }

                    XmlNode nodePrg = xmldoc.SelectSingleNode("//script/text()[contains(.,'data.push')]");

                    if (nodePrg == null)
                    {
                        this.Logger.Error("[RefreshEpg] No EPG data.");
                        return false;
                    }

                    int iCnt = 0;
                    int iIdxStart = 0;
                    while (true)
                    {
                        iIdxStart = nodePrg.Value.IndexOf("data.push({", iIdxStart);

                        if (iIdxStart < 0) break;

                        int iIdxEnd = nodePrg.Value.IndexOf("});", iIdxStart);

                        if (iIdxEnd < 0) break;

                        string strJson = nodePrg.Value.Substring(iIdxStart + 10, iIdxEnd - iIdxStart - 9).Replace("\t", "").Replace("\r", "").Replace("\n", "");

                        iIdxStart = iIdxEnd;

                        MatchCollection mc = regexTime.Matches(strJson);
                        if (mc.Count == 2)
                        {
                            //Start time
                            DateTime dtStart = new DateTime(
                            int.Parse(mc[0].Groups["year"].Value),
                            int.Parse(mc[0].Groups["month"].Value),
                            int.Parse(mc[0].Groups["day"].Value),
                            int.Parse(mc[0].Groups["hr"].Value),
                            int.Parse(mc[0].Groups["min"].Value),
                            0
                            );

                            if (dtStart.Month == 7) dtStart = dtStart.AddMonths(1);

                            //End time
                            DateTime dtEnd = new DateTime(
                                int.Parse(mc[1].Groups["year"].Value),
                                int.Parse(mc[1].Groups["month"].Value),
                                int.Parse(mc[1].Groups["day"].Value),
                                int.Parse(mc[1].Groups["hr"].Value),
                                int.Parse(mc[1].Groups["min"].Value),
                                0
                                );

                            if (dtEnd.Month == 7) dtEnd = dtEnd.AddMonths(1); //fix old month

                            if (dtEnd > DateTime.Now)
                            {
                                //Current or expecting program

                                Match m = regexData.Match(strJson);
                                if (m.Success)
                                {
                                    int iChannelId = int.Parse(m.Groups["channel"].Value);
                                    
                                    IptvChannel channel = this._ChannelList.Find(p => p.Url.EndsWith("/" + iChannelId));
                                    if (channel != null)
                                    {
                                        if (!channel.EpgProgramList.Exists(p => p.StartTime == dtStart && p.EndTime == dtEnd))
                                        {
                                            //Not existing program

                                            string strTitle = m.Groups["title"].Value;

                                            this.Logger.Debug("[RefreshEpg] Program added:{0}  StartTime:{1}  EndTime:{2}  Channel:{3}", strTitle, dtStart, dtEnd, channel.Name);

                                            channel.EpgProgramList.Add(new Program(channel.Channel.IdChannel, dtStart, dtEnd, strTitle, strTitle, "Sport",
                                                       Program.ProgramState.None, System.Data.SqlTypes.SqlDateTime.MinValue.Value, String.Empty, String.Empty,
                                                       String.Empty, String.Empty, 0, String.Empty, 0));
                                            iCnt++;
                                        }
                                    }
                                    //else this.Logger.Error("[RefreshEpg] Invalid channel id:" + iChannelId);
                                }
                            }
                        }
                    }


                    this.Logger.Debug("[RefreshEpg] Programs found:" + iCnt);



                }

                //Complete
                this.Logger.Debug("[RefreshEpg] RefreshEpg finished.");

                return true;

            }
            catch (Exception ex)
            {
                this.Logger.Error("[RefreshEpg] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return false;
            }
        }

        protected override NLog.Logger Logger
        {
            get
            {
                if (this._Logger == null) this._Logger = LogManager.GetCurrentClassLogger();
                return this._Logger;
            }
        }
        #endregion

    }
}

