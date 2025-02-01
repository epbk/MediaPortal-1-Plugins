using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.IO;
using System.Web;
using SetupTv;
using TvControl;
using TvDatabase;
using TvLibrary.Channels;
using TvLibrary.Interfaces;
using TvEngine;
using TvEngine.PowerScheduler;
using TvEngine.PowerScheduler.Interfaces;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using MediaPortal.Pbk.Extensions;
using NLog;

namespace MediaPortal.IptvChannels
{
    public class Plugin : ITvServerPlugin, ITvServerPluginStartedAll, IWakeupHandler
    {
        #region Constants

        private const string _LOG_PATTERN = "MediaPortal.IptvChannels.*";

        private const string _PROVIDER_PREFIX = "IPTV channels: ";

        private const string _WAKEUP_HANDLER_NAME = "IptvChannelsHandler";

        private const int _NETWORK_ID_DEFAULT = 1;
        private const int _PROGRAM_ID_DEFAULT = 1;
        private const int _TS_ID_DEFAULT = 1;
        private const int _PMT_PID_DEFAULT = 8188;
        public const string URL_FILTER_BASE = "http://127.0.0.1####Url=";
        public static readonly string URL_FILTER_PARAM = "&Mpeg2TsTransportStreamID=" + _TS_ID_DEFAULT +
            "&Mpeg2TsProgramNumber=" + _PROGRAM_ID_DEFAULT +
            "&Mpeg2TsProgramMapPID=" + _PMT_PID_DEFAULT + "&HttpOpenConnectionTimeout=30000";

        public const string HTTP_PATH_CHANNEL_LINK = "/GetChannelUrl";
        public const string HTTP_PATH_STREAM = "/GetStream";
        public const string HTTP_PATH_MEDIA_HANDLER = "/GetMediaHandler";
        public const string HTTP_PATH_STREAM_CONNECTIONS = "/GetStreamConnections";
        public const string HTTP_PATH_STREAM_CLIENTS = "/GetConnectionClients";
        public const string HTTP_PATH_CDN_TASKS = "/GetCDNTasks";
        public const string HTTP_PATH_CDN_SEGMENTS = "/GetCDNSegments";
        public const string HTTP_PATH_EVENTS = "/GetEvents";
        public const string HTTP_PATH_APPLY_SETTINGS = "/ApplySettings";
        public const string HTTP_PATH_DESCRIPTION = "/description.xml";

        public const string URL_PARAMETER_NAME_URL = "url";
        public const string URL_PARAMETER_NAME_SITE = "site";
        public const string URL_PARAMETER_NAME_CHANNEL = "channel";
        public const string URL_PARAMETER_NAME_MEDIA_SERVER = "cdn";
        public const string URL_PARAMETER_NAME_ARGUMENTS = "args";
        public const string URL_PARAMETER_NAME_STREAM_TYPE = "streamType";
        public const string URL_PARAMETER_NAME_DRM_LICENCE_SERVER = "drmLicenceServer";
        public const string URL_PARAMETER_NAME_DRM_HTTP_ARGUMENTS = "drmHttpArguments";
        public const string URL_PARAMETER_NAME_DRM_KEY = "drmKey";
        public const string URL_PARAMETER_NAME_HTTP_ARGUMENTS = "httpArguments";
        public const string URL_PARAMETER_NAME_STREAMING_ENGINE = "streamingEngine";
        public const string URL_PARAMETER_NAME_SEGMENT_LIST_BUILD = "segmenListBuild";

        public const string UPNP_DEVICE_TYPE = "urn:team-mediaportal.com:device:IptvChannels:1";
        
        #endregion

        #region Types
        public delegate SiteUtils.LinkResult GetSiteLinkHandler(string strSite, string strChannel);

        #endregion

        #region Events
        #endregion

        #region Variables

        public Settings.Setting PluginSettings;
        public Database.dbSettings Settings = Database.dbSettings.Instance;
        public DateTime LastRefresh = DateTime.Now;
        public DateTime NextRefresh = DateTime.Now;

        private readonly List<SiteUtils.SiteUtilBase> _Sites;

        private readonly PluginLoader _PluginLoader;

        private bool _WorkerThreadRunning = false;
        private System.Timers.Timer _ScheduleTimer;

        private readonly TvBusinessLayer _TvLayer = new TvBusinessLayer();
        private List<Card> _IptvCards = null;

        private Pbk.Net.Http.HttpUserServer _HttpServer;

        private readonly List<Proxy.MediaServer.TaskCDN> _CDNTasks = new List<Proxy.MediaServer.TaskCDN>();

        private readonly StringBuilder _EventDataSb = new StringBuilder(1024);
        private byte[] _EventData = new byte[2048];
        private int _EventDataSize = 0;
        private static readonly byte[] _EventDataPing = Encoding.ASCII.GetBytes("14\r\n{\"eventType\":\"Ping\"}\r\n");
        private uint _EventId = 0;
        private ManualResetEvent[] _EventClientFlags = null;
        private readonly List<ManualResetEvent> _EventClientFlagsList = new List<ManualResetEvent>();

        private SSDP.SsdpServer _SsdpServer;
        private SSDP.UpnpDevice _RootDevice;
        private IPEndPoint _HttpServerEndpoint;

        private static NLog.Logger _Logger;

        #endregion

        #region ctor

        static Plugin()
        {
            Log.AddRule(_LOG_PATTERN);
        }

        /// <summary>
        /// Create a new instance of a generic standby handler
        /// </summary>
        public Plugin()
        {
            _Logger = LogManager.GetCurrentClassLogger();

            string strVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            _Logger.Info(this.Name +  " (" + strVersion + ")");

            this._PluginLoader = new PluginLoader();
            this._Sites = new List<SiteUtils.SiteUtilBase>();

            //SSDP
            this._RootDevice = new SSDP.UpnpDevice()
            {
                DeviceType = UPNP_DEVICE_TYPE,
                Udn = Guid.NewGuid(),
                FriendlyName = this.Name,
                ModelName = this.Name,
                Manufacturer = this.Author,
                ModelNumber = strVersion,
                ServerPort = Database.dbSettings.Instance.HttpServerPort
            };
            this._RootDevice.InitDescription();
            this._SsdpServer = new SSDP.SsdpServer(new SSDP.UpnpDevice[] { this._RootDevice });
            this._HttpServerEndpoint = new IPEndPoint(Dns.GetHostAddresses(Dns.GetHostName()).First(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork),
                Database.dbSettings.Instance.HttpServerPort);

            //Load external plugins
            this.createSiteList();

            //Create settings
            this.PluginSettings = new Settings.Setting(this);

            //Load Settings
            this.PluginSettings.Load();

            //Initialize all sites
            this.initalizeSites();
        }

        #endregion

        #region Properties
        public IList<SiteUtils.SiteUtilBase> Sites
        {
            get
            {
                return this._Sites;
            }
        }
        #endregion

        #region Public methods
        public static string GenerateLink(GenerateLinkConfiguration args)
        {
            if (!string.IsNullOrWhiteSpace(args.Url))
            {
                string strHttpArguments = args.HttpArguments?.Serialize();
                string strDrmhttpArguments = args.DrmHttpArguments?.Serialize();

                StringBuilder sb = new StringBuilder(256);
                sb.Append("http://127.0.0.1:");
                sb.Append(Database.dbSettings.Instance.HttpServerPort);
                sb.Append(HTTP_PATH_STREAM);
                sb.Append('?');
                sb.Append(URL_PARAMETER_NAME_URL);
                sb.Append('=');
                sb.Append(HttpUtility.UrlEncode(args.Url));
                if (args.StreamType != Proxy.StreamTypeEnum.Unknown)
                {
                    sb.Append('&');
                    sb.Append(URL_PARAMETER_NAME_STREAM_TYPE);
                    sb.Append('=');
                    sb.Append(args.StreamType);
                }

                sb.Append('&');
                sb.Append(URL_PARAMETER_NAME_MEDIA_SERVER);
                sb.Append('=');
                sb.Append(args.UseMediaServer ? '1' : '0');
                if (!string.IsNullOrWhiteSpace(args.Arguments))
                {
                    sb.Append('&');
                    sb.Append(URL_PARAMETER_NAME_ARGUMENTS);
                    sb.Append('=');
                    sb.Append(HttpUtility.UrlEncode(args.Arguments));
                }
                if (!string.IsNullOrWhiteSpace(strHttpArguments))
                {
                    sb.Append('&');
                    sb.Append(URL_PARAMETER_NAME_HTTP_ARGUMENTS);
                    sb.Append('=');
                    sb.Append(HttpUtility.UrlEncode(strHttpArguments));
                }
                if (!string.IsNullOrWhiteSpace(args.DrmLicenceServer))
                {
                    sb.Append('&');
                    sb.Append(URL_PARAMETER_NAME_DRM_LICENCE_SERVER);
                    sb.Append('=');
                    sb.Append(HttpUtility.UrlEncode(args.DrmLicenceServer));
                }
                if (!string.IsNullOrWhiteSpace(strDrmhttpArguments))
                {
                    sb.Append('&');
                    sb.Append(URL_PARAMETER_NAME_DRM_HTTP_ARGUMENTS);
                    sb.Append('=');
                    sb.Append(HttpUtility.UrlEncode(strDrmhttpArguments));
                }

                if (args.UseMPUrlSourceSplitter)
                {
                    string strUrl = HttpUtility.UrlEncode(sb.ToString());
                    sb.Clear();
                    sb.Append(URL_FILTER_BASE);
                    sb.Append(strUrl);
                    if (args.UseMPUrlSourceSplitterArguents)
                        sb.Append(URL_FILTER_PARAM);
                }

                if (args.StreamingEngine != Proxy.StreamingEngineEnum.Default)
                {
                    sb.Append('&');
                    sb.Append(URL_PARAMETER_NAME_STREAMING_ENGINE);
                    sb.Append(args.StreamingEngine);
                }

                return sb.ToString();
            }
            else
                return null;

        }

        public bool CreateChannel(string strChannelName, string strChannelUrl)
        {
            this.checkIptvCards();

            if (this._IptvCards != null)
            {
                //Create TV channel
                DVBIPChannel channel = new DVBIPChannel
                {
                    IsTv = true,
                    IsRadio = false,

                    Url = strChannelUrl,
                    PmtPid = _PMT_PID_DEFAULT,
                    ServiceId = _PROGRAM_ID_DEFAULT,
                    NetworkId = _NETWORK_ID_DEFAULT,
                    TransportId = _TS_ID_DEFAULT,

                    Provider = _PROVIDER_PREFIX,
                    FreeToAir = true,
                    Name = strChannelName
                };

                Channel dbChannel = this._TvLayer.AddNewChannel(strChannelName, channel.LogicalChannelNumber);
                dbChannel.SortOrder = 10000;

                if (channel.LogicalChannelNumber >= 1)
                    dbChannel.SortOrder = channel.LogicalChannelNumber;

                dbChannel.IsTv = true;
                dbChannel.IsRadio = false;
                dbChannel.GrabEpg = false;
                dbChannel.VisibleInGuide = true;
                dbChannel.Persist();

                this._TvLayer.AddChannelToGroup(dbChannel, TvConstants.TvGroupNames.AllChannels);
                this._TvLayer.AddTuningDetails(dbChannel, channel);

                this._IptvCards.ForEach(card => this._TvLayer.MapChannelToCard(card, dbChannel, false));

                _Logger.Debug("[CreateChannel] Channel created: " + dbChannel.DisplayName);

                return true;
            }

            return false;
        }

        public void CheckSiteChannels(SiteUtils.SiteUtilBase site)
        {
            List<Channel> delList = new List<Channel>();

            this.checkIptvCards();

            this.checkSiteChannels(site, delList);

            if (delList.Count > 0)
                this.deleteChannels(delList);
        }
        #endregion

        #region Private members

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void startImport()
        {
            if (this._WorkerThreadRunning)
                return;

            this._WorkerThreadRunning = true;
            Thread thread = new Thread(new ThreadStart(this.refreshEpg))
            {
                Name = "WebEPGImporter",
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            thread.Start();
        }

        private void refreshEpg()
        {
            _Logger.Debug("[RefreshEpg] RefreshEpg started...");

            this.setStandbyAllowed(false);

            bool bRefreshNeeded = false;

            int iMinRefreshPeriod = 60000 * 60;

            try
            {
                try
                {
                    foreach (SiteUtils.SiteUtilBase util in this._Sites)
                    {
                        if (!util.Enabled || !util.EpgRefreshEnabled)
                            continue;

                        //Check last refresh time stamp
                        if ((DateTime.Now - util.EpgLastRefresh.AddMilliseconds(util.EpgRefreshPeriod)).TotalSeconds < -30)
                            continue;

                        //Refresh EPG in SiteUtil
                        _Logger.Debug("[RefreshEpg] Site:" + util.Name);

                        try
                        {
                            bool bResult = false;
                            Thread thread = new Thread(new ThreadStart(() =>
                            {
                                bResult = util.RefreshEpg();
                            }));

                            thread.Start();

                            //Wait for thread
                            if (!thread.Join(60000))
                            {
                                thread.Abort();
                                _Logger.Error("[RefreshEpg] Function timeout:RefreshEpg()");
                                continue;
                            }
                            else if (!bResult)
                            {
                                _Logger.Error("[RefreshEpg] Site util has failed refresh EPG:" + util.Name);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _Logger.Error("[RefreshEpg] Plugin error:RefreshEpg() {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                            continue;
                        }

                        //Insert ProgrammList into TV channel
                        foreach (SiteUtils.IptvChannel channel in util.Channels)
                        {
                            if (!channel.Enabled)
                                continue;

                            if (channel.Channel != null && channel.EpgProgramList != null)
                            {
                                if (channel.EpgProgramList.Count > 0)
                                {
                                    bRefreshNeeded = true;

                                    // Sort programs
                                    channel.EpgProgramList.SortIfNeeded();
                                    channel.EpgProgramList.AlreadySorted = true;

                                    // Fix end times
                                    channel.EpgProgramList.FixEndTimes();

                                    // Remove overlapping programs
                                    channel.EpgProgramList.RemoveOverlappingPrograms();

                                    //Finally insert programs into channel
                                    this._TvLayer.InsertPrograms(channel.EpgProgramList, DeleteBeforeImportOption.OverlappingPrograms, ThreadPriority.BelowNormal);
                                }

                                _Logger.Debug("[RefreshEpg]  Channel:" + channel.Channel.DisplayName + "  Programs added:" + channel.EpgProgramList.Count);
                            }
                            else
                                _Logger.Error("[RefreshEpg] Invalid channel. Site:" + util.Name);
                        }

                        util.EpgLastRefresh = DateTime.Now;
                        if (util.EpgRefreshPeriod < iMinRefreshPeriod)
                            iMinRefreshPeriod = util.EpgRefreshPeriod;
                    }

                    //Wait for database update
                    if (bRefreshNeeded)
                        this._TvLayer.WaitForInsertPrograms();

                    if (iMinRefreshPeriod < 60000)
                        iMinRefreshPeriod = 60000;

                    this.LastRefresh = DateTime.Now;
                    this.NextRefresh = DateTime.Now.AddMilliseconds(iMinRefreshPeriod);

                    _Logger.Debug("[RefreshEpg] RefreshEpg finished. Next refresh:" + this.NextRefresh);
                }
                catch (Exception ex)
                {
                    _Logger.Error("[RefreshEpg] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                }
            }
            finally
            {
                _Logger.Debug("[RefreshEpg] Refresh done.");
                this._WorkerThreadRunning = false;

                this.setStandbyAllowed(true);
            }
        }

        private void createSiteList()
        {
            //Load External plugins
            this._PluginLoader.Load();
            foreach (SiteUtils.SiteUtilBase plugin in this._PluginLoader.Plugins)
            {
                this._Sites.Add(plugin);
                _Logger.Debug("[CreateSiteList] Site Added:" + plugin.Name);
            }

            _Logger.Debug("[CreateSiteList] Total sites:" + this._Sites.Count);
        }

        private void initalizeSites()
        {
            foreach (SiteUtils.SiteUtilBase util in this._Sites)
            {
                try
                {
                    util.Initialize(this);
                }
                catch (Exception ex)
                {
                    _Logger.Error("[CheckChannels] Plugin error:Initialize() {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                }

                _Logger.Debug("[InitalizeSites] Site initialized: " + util.Name + " (" + util.Version + ") '" + util.Description + "'");
            }
        }

        private void checkIptvCards()
        {
            //Get list IPTV cards
            if (this._IptvCards == null)
            {
                _Logger.Debug("[CheckChannels] Checking for IPTV cards...");

                this._IptvCards = new List<Card>();

                foreach (Card card in this._TvLayer.Cards)
                {
                    if (RemoteControl.Instance.Type(card.IdCard) == CardType.DvbIP)
                    {
                        this._IptvCards.Add(card);
                        _Logger.Debug("[CheckChannels] IPTV Card found:" + card.Name);
                    }
                }

                if (this._IptvCards.Count < 1)
                {
                    _Logger.Error("[CheckChannels] Error: IPTV card not found.");
                    return;
                }
            }
        }

        private void checkSiteChannels()
        {
            List<Channel> delList = new List<Channel>();

            this.checkIptvCards();

            this._Sites.ForEach(site => this.checkSiteChannels(site, delList));

            if (delList.Count > 0)
                this.deleteChannels(delList);
        }
        private void checkSiteChannels(SiteUtils.SiteUtilBase site, List<Channel> delList)
        {
            _Logger.Debug("[CheckChannels] Checking SiteUtil:" + site.Name);

            try
            {
                if (this._IptvCards.Count > 0)
                {
                    foreach (SiteUtils.IptvChannel iptvChannel in site.Channels)
                    {
                        Channel dbChannel;
                        string strChannelUrl = iptvChannel.TvServerLink;
                        string strChannelProvider = _PROVIDER_PREFIX + site.Name + '|' + iptvChannel.Id;

                        if ((dbChannel = getChannel(strChannelProvider)) == null)
                        {
                            if (!iptvChannel.Enabled)
                                continue;

                            if (!site.Enabled)
                            {
                                //Site not enabled; do not create TV channel
                                _Logger.Debug("[CheckChannels] Site not enabled. Skip creating channel:" + iptvChannel.Name);
                                continue;
                            }

                            //Create TV channel
                            DVBIPChannel channel = new DVBIPChannel
                            {
                                IsTv = true,
                                IsRadio = false,
                                Url = strChannelUrl
                            };

                            if (iptvChannel.PmtID >= 32 && iptvChannel.PmtID <= 8191)
                            {
                                channel.PmtPid = iptvChannel.PmtID;
                                channel.ServiceId = iptvChannel.ServiceID;
                                channel.NetworkId = iptvChannel.NetworkID;
                                channel.TransportId = iptvChannel.TransportStreamID;
                            }
                            else
                            {
                                channel.PmtPid = _PMT_PID_DEFAULT;
                                channel.ServiceId = _PROGRAM_ID_DEFAULT;
                                channel.NetworkId = _NETWORK_ID_DEFAULT;
                                channel.TransportId = _TS_ID_DEFAULT;
                            }

                            channel.Provider = strChannelProvider;
                            channel.FreeToAir = true;
                            channel.Name = iptvChannel.Name;

                            dbChannel = this._TvLayer.AddNewChannel(iptvChannel.Name, channel.LogicalChannelNumber);
                            dbChannel.SortOrder = 10000;

                            if (channel.LogicalChannelNumber >= 1)
                                dbChannel.SortOrder = channel.LogicalChannelNumber;

                            dbChannel.IsTv = true;
                            dbChannel.IsRadio = false;
                            dbChannel.GrabEpg = false;
                            dbChannel.VisibleInGuide = true;
                            dbChannel.Persist();

                            this._TvLayer.AddChannelToGroup(dbChannel, TvConstants.TvGroupNames.AllChannels);
                            this._TvLayer.AddTuningDetails(dbChannel, channel);

                            this._IptvCards.ForEach(card => this._TvLayer.MapChannelToCard(card, dbChannel, false));

                            _Logger.Debug("[CheckChannels] Channel created: " + dbChannel.DisplayName);
                        }
                        else
                        {
                            _Logger.Debug("[CheckChannels] Channel found:" + dbChannel.DisplayName);

                            if (Database.dbSettings.Instance.DeleteUnreferencedChannels && (!site.Enabled || !iptvChannel.Enabled))
                            {
                                //Site/channel not enabled; delete channel
                                _Logger.Debug("[CheckChannels] Site/channel not enabled. Delete channel:" + dbChannel.DisplayName);
                                delList.Add(dbChannel);
                                continue;
                            }
                            else if (site.UpdateTvServerChannelLink && site.Enabled && dbChannel.ReferringTuningDetail()[0].Url != strChannelUrl)
                            {
                                _Logger.Debug("[CheckChannels] Updating channel '{0}' url: {1}", dbChannel.DisplayName, strChannelUrl);
                                dbChannel.ReferringTuningDetail()[0].Url = strChannelUrl;
                                dbChannel.ReferringTuningDetail()[0].Persist();
                            }
                        }

                        //Assign Channel
                        iptvChannel.Channel = dbChannel;
                    }
                }

                //Delete unreferenced channels
                if (Database.dbSettings.Instance.DeleteUnreferencedChannels)
                {
                    //Remove all unknown channels
                    this.removeUnknownChannels();
                }

            }
            catch (Exception ex)
            {
                _Logger.Error("[CheckChannels] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }

        private void removeUnknownChannels()
        {
            //Remove all unknown channels
            this.removeUnknownChannels(null);
        }
        private void removeUnknownChannels(SiteUtils.SiteUtilBase site)
        {
            //Remove all unknown channels
            Regex regexLink = new Regex(_PROVIDER_PREFIX + "(?<site>.+?)\\|(?<channel>.+)");

            List<Channel> delList = new List<Channel>();
            foreach (Channel ch in this._TvLayer.Channels)
            {
                IList<TuningDetail> tun = ch.ReferringTuningDetail();
                if (tun[0].Provider.StartsWith(_PROVIDER_PREFIX))
                {
                    Match m = regexLink.Match(tun[0].Provider);
                    if (m.Success)
                    {
                        string strSiteName = m.Groups["site"].Value;
                        SiteUtils.SiteUtilBase siteCheck = null;

                        if (site == null)
                        {
                            //Check all sites
                            siteCheck = this._Sites.Find(p => p.Name == strSiteName);

                            if (siteCheck == null)
                                goto del; //Unknown site; add to delete list
                        }
                        else
                        {
                            //Check selected site only
                            if (strSiteName == siteCheck.Name)
                                siteCheck = site;
                            else
                                continue; //different site
                        }

                        //If site is not enabled then delete channel else check for existing channel in siteutil
                        if (siteCheck.Enabled)
                        {
                            //Try find channel in site
                            string strChannel = HttpUtility.UrlDecode(m.Groups["channel"].Value);
                            foreach (SiteUtils.IptvChannel iptvCh in siteCheck.Channels)
                            {
                                if (iptvCh.Id == strChannel)
                                    goto nxt; //exist
                            }
                        }

                    del:
                        //Not found; add to delete list
                        delList.Add(ch);
                    }

                }
            nxt:
                continue;
            }

            if (delList.Count > 0)
                this.deleteChannels(delList);
        }

        private bool getChannel(string strUrl, bool bStarts, out Channel channel)
        {
            _Logger.Debug("[GetChannel] Looking for url:" + strUrl + "   Starts-With:" + bStarts);

            channel = null;

            if (string.IsNullOrEmpty(strUrl) || this._TvLayer.Channels != null)
            {
                foreach (Channel ch in this._TvLayer.Channels)
                {
                    IList<TuningDetail> tun = ch.ReferringTuningDetail();
                    if ((!bStarts && tun[0].Url == strUrl) || tun[0].Url.StartsWith(strUrl))
                    {
                        channel = ch;
                        return true;
                    }
                }
            }

            _Logger.Debug("[GetChannel] Channel not found: " + strUrl);
            return false;
        }

        private Channel getChannel(string strChnnelProvider)
        {
            _Logger.Debug("[GetChannel] Looking for channel: " + strChnnelProvider);

            if (this._TvLayer.Channels != null)
            {
                foreach (Channel ch in this._TvLayer.Channels)
                {
                    IList<TuningDetail> tun = ch.ReferringTuningDetail();
                    if (tun[0].Provider == strChnnelProvider)
                        return ch;
                }
            }

            _Logger.Debug("[GetChannel] Channel not found: " + strChnnelProvider);
            return null;
        }

        private void deleteChannels(List<Channel> channels)
        {
            try
            {
                IList<Schedule> schedules = Schedule.ListAll();
                TvServer server = new TvServer();
                channels.ForEach(channel =>
                {
                    if (schedules != null)
                    {
                        for (int i = schedules.Count - 1; i > -1; i--)
                        {
                            Schedule schedule = schedules[i];
                            if (schedule.IdChannel == channel.IdChannel)
                            {
                                server.StopRecordingSchedule(schedule.IdSchedule);
                                schedule.Delete();
                                schedules.RemoveAt(i);
                            }
                        }
                    }
                    channel.Delete();

                    _Logger.Debug("[deleteChannels] Channel deleted:" + channel.DisplayName);
                });
            }
            catch (Exception ex)
            {
                _Logger.Error("[deleteChannels] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

        }

        private void cbScheduleTimer(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (DateTime.Now >= this.NextRefresh)
                this.startImport();
        }

        private void epgScheduleDue()
        {
            this.startImport();
        }

        private void registerForEPGSchedule()
        {
            if (GlobalServiceProvider.Instance.IsRegistered<IEpgHandler>())
            {
                IEpgHandler handler = GlobalServiceProvider.Instance.Get<IEpgHandler>();
                if (handler != null)
                {
                    handler.EPGScheduleDue += new EPGScheduleHandler(this.epgScheduleDue);
                    _Logger.Debug("[RegisterForEPGSchedule] Registered with PowerScheduler EPG handler");
                    return;
                }
            }
            _Logger.Debug("[RegisterForEPGSchedule] NOT registered with PowerScheduler EPG handler");
        }

        private void registerWakeupHandler()
        {
            if (GlobalServiceProvider.Instance.IsRegistered<IPowerScheduler>())
            {
                GlobalServiceProvider.Instance.Get<IPowerScheduler>().Register(this as IWakeupHandler);
                _Logger.Debug("[RegisterWakeupHandler] Registered WakeupHandler with PowerScheduler");
                return;
            }
            _Logger.Debug("[RegisterWakeupHandler] NOT registered WakeupHandler with PowerScheduler");
        }

        private void unregisterWakeupHandler()
        {
            if (GlobalServiceProvider.Instance.IsRegistered<IPowerScheduler>())
            {
                GlobalServiceProvider.Instance.Get<IPowerScheduler>().Unregister(this as IWakeupHandler);
                _Logger.Debug("[UnregisterWakeupHandler] Unregistered WakeupHandler with PowerScheduler ");
            }
        }

        private void setStandbyAllowed(bool allowed)
        {
            if (GlobalServiceProvider.Instance.IsRegistered<IEpgHandler>())
            {
                const int TIMEOUT = 3600;
                _Logger.Debug("[SetStandbyAllowed] Telling PowerScheduler standby is allowed: {0}, timeout is:{1}", allowed, TIMEOUT);
                GlobalServiceProvider.Instance.Get<IEpgHandler>().SetStandbyAllowed(this, allowed, TIMEOUT);
            }
        }

        private static void deleteFolder(string strDir)
        {
            if (!Directory.Exists(strDir))
                return;

            string[] dirs = Directory.GetDirectories(strDir);
            foreach (string strD in dirs)
            {
                deleteFolder(strD);
            }

            string[] files = Directory.GetFiles(strDir);

            foreach (string strF in files)
            {
                try { File.Delete(strF); }
                catch { };
            }

            _Logger.Debug("[deleteFolder] Deleting folder: " + strDir);

            try { Directory.Delete(strDir); }
            catch { }
        }

        private SiteUtils.IptvChannel getIptvChannelByUrlParam(string strSite, string strChannel)
        {
            if (strSite != null && strChannel != null)
            {
                SiteUtils.SiteUtilBase site = this._Sites.Find(p => p.Name == strSite && p.Enabled);
                if (site == null)
                {
                    _Logger.Error("[GetIptvChannelByUrlParam] Invalid site.");
                    return null;
                }

                foreach (SiteUtils.IptvChannel iptvChannel in site.Channels)
                {
                    if (iptvChannel.Id == strChannel)
                        return iptvChannel;
                }

                _Logger.Debug("[GetIptvChannelByUrlParam] Channel not found");

                return new SiteUtils.IptvChannel(site, strChannel, null, strChannel);
            }
            else
                _Logger.Error("[GetIptvChannelByUrlParam] Invalid query");

            return null;
        }

        #endregion

        #region ITvServerPlugin
        /// <summary>
        /// returns the name of the plugin
        /// </summary>
        public string Name
        {
            get { return "IptvChannels"; }
        }

        /// <summary>
        /// returns the version of the plugin
        /// </summary>
        public string Version
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        /// <summary>
        /// returns the author of the plugin
        /// </summary>
        public string Author
        {
            get { return "Pbk"; }
        }

        /// <summary>
        /// returns if the plugin should only run on the master server
        /// or also on slave servers
        /// </summary>
        public bool MasterOnly
        {
            get { return true; }
        }

        /// <summary>
        /// Starts the plugin
        /// </summary>
        public void Start(IController controller)
        {
            _Logger.Debug("[Start] Started...");

            //Check TV site channels
            this.checkSiteChannels();

            //Start http server
            if (this._HttpServer == null)
            {
                this._HttpServer = new Pbk.Net.Http.HttpUserServer();
                this._HttpServer.RequestReceived += this.cbHttpServer;
            }

            this._HttpServer.Start(Database.dbSettings.Instance.HttpServerPort);

            //Refresh EPG now
            this.startImport();

            //CheckNewTVGuide();
            this._ScheduleTimer = new System.Timers.Timer
            {
                Interval = 60000,
                Enabled = true
            };
            this._ScheduleTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.cbScheduleTimer);

            //SSDP
            this._SsdpServer.Start();
        }

        /// <summary>
        /// Stops the plugin
        /// </summary>
        public void Stop()
        {
            _Logger.Debug("[Stop] Stopping...");

            this.unregisterWakeupHandler();

            //SSDP
            this._SsdpServer.Stop();

            //Terminate timer
            if (this._ScheduleTimer != null)
            {
                this._ScheduleTimer.Enabled = false;
                this._ScheduleTimer.Dispose();
                this._ScheduleTimer = null;
            }

            //Terminate all event listeners
            this.httpSendEvent(SendEventTypeEnum.None, null);

            //Stop http server
            if (this._HttpServer != null)
            {
                this._HttpServer.Stop();
                _Logger.Debug("[Stop] Http server closed.");
            }

            //Close connection handler
            Proxy.ConnectionHandler.CloseAll();
            _Logger.Debug("[Stop] Connectionhandler closed.");

            //Stop all CDN tasks
            lock (this._CDNTasks)
            {
                this._CDNTasks.ForEach(task => task.Stop());
            }

            _Logger.Debug("[Stop] CDN tasks closed.");


            Proxy.VlcControlManager.Terminate();

            _Logger.Debug("[Stop] Stopped.");
        }

        /// <summary>
        /// returns the setup sections for display in SetupTv
        /// </summary>
        public SetupTv.SectionSettings Setup
        {
            get { return new SetupTv.Sections.Setup(this); }
        }

        #endregion

        #region ITvServerPluginStartedAll

        public void StartedAll()
        {
            this.registerForEPGSchedule();
            this.registerWakeupHandler();
        }

        #endregion

        #region IWakeupHandler

        [MethodImpl(MethodImplOptions.Synchronized)]
        public DateTime GetNextWakeupTime(DateTime earliestWakeupTime)
        {
            return Database.dbSettings.Instance.WakeupForEpgGrabbing ? this.NextRefresh : DateTime.MaxValue;
        }

        public string HandlerName
        {
            get { return _WAKEUP_HANDLER_NAME; }
        }

        #endregion

        #region HTTP
        /// <summary>
        /// Callback from http server to handle client request
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbHttpServer(object sender, Pbk.Net.Http.HttpUserServerEventArgs e)
        {
            Uri uri = new Uri(e.Path.StartsWith("http") ? e.Path : ("http://127.0.0.1:80" + e.Path));

            Dictionary<string, string> prm = null;
            if (!string.IsNullOrEmpty(uri.Query))
                prm = Pbk.Utils.Tools.GetUrlParams(uri.Query, true);

            _Logger.Debug("[cbHtppServer] Access: '{0}' Client: {1}", uri.PathAndQuery, e.RemoteSocket.RemoteEndPoint);


            Proxy.MediaServer.TaskCDN cdnTask = null;
            string strIdent = null;
            bool bLocked = false;
            string strUrl = null;
            string strValue;
            StringBuilder sb;
            int iId;

            try
            {
                // /cdn/stream/{nr}/
                if (uri.Segments.Length >= 4 && uri.Segments[0] == "/" && uri.Segments[1] == "cdn/" && uri.Segments[3].EndsWith("/"))
                {
                    strIdent = uri.Segments[3].Substring(0, uri.Segments[3].Length - 1);

                    //Enter lock
                    Monitor.Enter(this._HttpServer, ref bLocked);

                    //Try find existing task
                    cdnTask = this.cdnTaskGet(strIdent);
                }
                else
                {
                    switch (uri.LocalPath)
                    {
                        #region /GetStream
                        case HTTP_PATH_STREAM:
                            if (!prm.TryGetValue(URL_PARAMETER_NAME_URL, out strUrl))
                                strUrl = null;

                            Proxy.ConnectionHandler con = Proxy.ConnectionHandler.Get(prm, this.GetLinkFromSite);
                            if (con != null)
                            {
                                con.AddClient(e.RemoteSocket);
                                e.KeepAlive = false;
                                e.Handled = true;
                                e.ResponseSent = true;
                                e.CloseSocket = false;
                            }
                            break;
                        #endregion

                        #region /GetMediaHandler
                        case HTTP_PATH_MEDIA_HANDLER:
                            if (prm.TryGetValue(URL_PARAMETER_NAME_URL, out strUrl))
                            {
                                //Enter lock
                                Monitor.Enter(this._HttpServer, ref bLocked);

                                //Try find existing task
                                cdnTask = this.cdnTaskGet(strUrl);

                                if (cdnTask == null)
                                {
                                    //Check the string URL
                                    if (!Uri.IsWellFormedUriString(strUrl, UriKind.Absolute))
                                        return;

                                    //New task
                                    cdnTask = new Proxy.MediaServer.TaskCDN()
                                    {
                                        Url = strUrl,
                                        Title = strUrl,
                                        Autoterminate = true,
                                        DRMLicenceServer = prm.TryGetValue(URL_PARAMETER_NAME_DRM_LICENCE_SERVER, out strValue) ? strValue : null,
                                        DRMKey = prm.TryGetValue(URL_PARAMETER_NAME_DRM_KEY, out strValue) ? strValue : null,
                                        DRMHttpArguments = prm.TryGetValue(URL_PARAMETER_NAME_DRM_HTTP_ARGUMENTS, out strValue)
                                            ? Pbk.Net.Http.HttpUserWebRequestArguments.Deserialize(strValue) : null,
                                        HttpArguments = prm.TryGetValue(URL_PARAMETER_NAME_HTTP_ARGUMENTS, out strValue)
                                            ? Pbk.Net.Http.HttpUserWebRequestArguments.Deserialize(strValue) : null,
                                        SegmentListBuild = !prm.TryGetValue(URL_PARAMETER_NAME_SEGMENT_LIST_BUILD, out strValue) || strValue == "1"
                                    };

                                    this.cdnTaskAdd(cdnTask);
                                    cdnTask.Event += this.cbTaskEvent;
                                    cdnTask.Start();
                                }

                                //Redirect to handler
                                uri = new Uri(cdnTask.PlaylistUrl);
                            }
                            break;
                        #endregion

                        #region /GetChannelUrl
                        case HTTP_PATH_CHANNEL_LINK:
                            this.httpHandleChannelRequest(prm, e);
                            break;
                        #endregion

                        #region /GetStreamConnections
                        case HTTP_PATH_STREAM_CONNECTIONS:
                            sb = new StringBuilder(1024);
                            sb.Append('{');
                            sb.Append("\"result\":[");
                            foreach (Proxy.ConnectionHandler c in Proxy.ConnectionHandler.GetHandlers())
                            {
                                if (sb[sb.Length - 1] == '}')
                                    sb.Append(',');

                                c.SerializeJson(sb);
                            }
                            sb.Append("]}");

                            e.ResponseContentType = Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_JSON;
                            e.ResponseData = Encoding.UTF8.GetBytes(sb.ToString());
                            e.ResponseCode = HttpStatusCode.OK;
                            e.Handled = true;

                            break;
                        #endregion

                        #region /GetConnectionClients
                        case HTTP_PATH_STREAM_CLIENTS:
                            if (prm.TryGetValue("id", out strValue) && !string.IsNullOrWhiteSpace(strValue))
                            {
                                foreach (Proxy.ConnectionHandler c in Proxy.ConnectionHandler.GetHandlers())
                                {
                                    if (c.HandlerId == strValue)
                                    {
                                        sb = new StringBuilder(1024);
                                        sb.Append('{');
                                        sb.Append("\"result\":[");

                                        List<Proxy.RemoteClient> list = c.ClientList;

                                        list.ForEach(cl =>
                                            {
                                                if (sb[sb.Length - 1] == '}')
                                                    sb.Append(',');

                                                cl.SerializeJson(sb);
                                            });
                                        sb.Append("]}");

                                        e.ResponseContentType = Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_JSON;
                                        e.ResponseData = Encoding.UTF8.GetBytes(sb.ToString());
                                        e.ResponseCode = HttpStatusCode.OK;
                                        e.Handled = true;
                                        break;
                                    }
                                }
                            }
                            break;
                        #endregion

                        #region /GetCDNTasks
                        case HTTP_PATH_CDN_TASKS:
                            sb = new StringBuilder(1024);
                            sb.Append('{');
                            sb.Append("\"result\":[");
                            lock (this._CDNTasks)
                            {
                                this._CDNTasks.ForEach(cdn =>
                                {
                                    if (sb[sb.Length - 1] == '}')
                                        sb.Append(',');

                                    cdn.SerializeJson(sb);
                                });
                            }
                            sb.Append("]}");

                            e.ResponseContentType = Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_JSON;
                            e.ResponseData = Encoding.UTF8.GetBytes(sb.ToString());
                            e.ResponseCode = HttpStatusCode.OK;
                            e.Handled = true;

                            break;
                        #endregion

                        #region /GetCDNSegments
                        case HTTP_PATH_CDN_SEGMENTS:
                            if (prm.TryGetValue("id", out strValue) && int.TryParse(strValue, out iId))
                            {
                                lock (this._CDNTasks)
                                {
                                    Proxy.MediaServer.TaskCDN task = this._CDNTasks.Find(cdn => cdn.Identifier == iId);
                                    if (task != null)
                                    {
                                        sb = new StringBuilder(1024);
                                        sb.Append('{');
                                        sb.Append("\"result\":[");

                                        Proxy.MediaServer.TaskSegment[] segments = task.Segments;

                                        for (int i = 0; i < segments.Length; i++)
                                        {
                                            Proxy.MediaServer.TaskSegment sg = segments[i];
                                            if (sb[sb.Length - 1] == '}')
                                                sb.Append(',');

                                            ((Proxy.MediaServer.TaskSegmentCDN)sg).SerializeJson(sb);
                                        };
                                        sb.Append("]}");

                                        e.ResponseContentType = Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_JSON;
                                        e.ResponseData = Encoding.UTF8.GetBytes(sb.ToString());
                                        e.ResponseCode = HttpStatusCode.OK;
                                        e.Handled = true;
                                        break;
                                    }
                                }

                            }
                            break;
                        #endregion

                        #region /GetEvents
                        case HTTP_PATH_EVENTS:

                            //ChunkedTransfer
                            e.RemoteSocket.SendTimeout = 2000;
                            e.RemoteSocket.Send(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=UTF-8\r\nTransfer-Encoding: chunked\r\n\r\n"));
                            e.ResponseSent = true;

                            lock (this._EventDataSb)
                            {
                                ManualResetEvent flag = new ManualResetEvent(true);
                                uint iLastId = this._EventId;
                                if (this._EventClientFlagsList.Count == 0)
                                    Proxy.ConnectionHandler.Event += this.cbConnectionHandler;

                                this._EventClientFlagsList.Add(flag);
                                this._EventClientFlags = this._EventClientFlagsList.ToArray();

                                try
                                {
                                    while (e.RemoteSocket.Connected)
                                    {
                                        if (iLastId == this._EventId)
                                        {
                                            if (!Monitor.Wait(this._EventDataSb, 5000))
                                            {
                                                //No event within last 5s; send ping
                                                try { e.RemoteSocket.Send(_EventDataPing); }
                                                catch { break; }
                                            }
                                        }
                                        else
                                        {
                                            //New event
                                            iLastId = this._EventId;
                                            try { e.RemoteSocket.Send(this._EventData, this._EventDataSize, System.Net.Sockets.SocketFlags.None); }
                                            catch { break; }
                                            finally
                                            {
                                                //We are done; decrement the counter; if zero raise the flag that informs all clients are done with the send
                                                flag.Set();
                                            }

                                            //null termination chunk
                                            if (this._EventDataSize <= 5)
                                                break;
                                        }
                                    }
                                }
                                finally
                                {
                                    flag.Set();
                                    this._EventClientFlagsList.Remove(flag);
                                    this._EventClientFlags = this._EventClientFlagsList.ToArray();
                                    if (this._EventClientFlagsList.Count == 0)
                                        Proxy.ConnectionHandler.Event -= this.cbConnectionHandler;
                                }
                            }
                            break;
                        #endregion

                        #region /ApplySettings
                        case HTTP_PATH_APPLY_SETTINGS:
                            if (e.PostData != null)
                            {
                                this.Settings.DeserializeFromJson(JsonConvert.DeserializeObject<JToken>(Encoding.UTF8.GetString(e.PostData)));
                                this.Settings.CommitNeeded = true;
                                this.Settings.Commit();

                                e.ResponseCode = HttpStatusCode.OK;
                                e.Handled = true;
                            }

                            break;
                            #endregion

                        #region /description.xml
                        case HTTP_PATH_DESCRIPTION:
                            e.ResponseContentType = "text/xml; charset=\"utf-8\"";
                            e.ResponseHeaderFields = new Dictionary<string, string>
                            {
                                { "X-IPTV_CHANNELS-HTTP-Port", Database.dbSettings.Instance.HttpServerPort.ToString() }
                            };
                            e.ResponseData = this._RootDevice.Description;
                            e.ResponseCode = HttpStatusCode.OK;
                            e.Handled = true;
                            break;
                        #endregion
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[cbHttpServer] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
            finally
            {
                if (bLocked)
                    Monitor.Exit(this._HttpServer);
            }

            //Handle the request by task
            if (cdnTask != null)
                cdnTask.HandleHttpRequest(e, uri);
        }

        private void httpHandleChannelRequest(Dictionary<string, string> prm, Pbk.Net.Http.HttpUserServerEventArgs e)
        {
            //Get final url from site
            SiteUtils.LinkResult link = null;
            if (prm.TryGetValue("site", out string strSite) && prm.TryGetValue("channel", out string strChannel))
                link = GetLinkFromSite(strSite, strChannel);

            #region Response
            if (link?.Url != null)
            {
                e.ResponseCode = HttpStatusCode.Moved;
                e.ResponseHeaderFields = new Dictionary<string, string>();
                string strLink;
                if (!prm.TryGetValue("toMediaHandler", out string str) || str == "1")
                {
                    StringBuilder sb = new StringBuilder(256);
                    sb.Append("http://").Append(this._HttpServerEndpoint).Append(HTTP_PATH_MEDIA_HANDLER).Append('?');
                    strLink = link.Serialize(sb).ToString();
                }
                else
                    strLink = link.Url;

                e.ResponseHeaderFields.Add("Location", strLink);
            }
            else
            {
                e.ResponseCode = HttpStatusCode.NotFound;
                e.ResponseContentType = "text/html";
            }

            e.Handled = true;

            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[httpHandleChannelRequest][{0}] Response:\r\n{1}",
                e.RemoteSocket.RemoteEndPoint.ToString(), link?.Url);
            #endregion
        }

        public SiteUtils.LinkResult GetLinkFromSite(string strSite, string strChannel)
        {
            //Get final url from site
            SiteUtils.LinkResult response = null;
            SiteUtils.IptvChannel channel = this.getIptvChannelByUrlParam(strSite, strChannel);
            if (channel != null)
            {
                //Channel found; call site to get the final url
                try
                {
                    Thread thread = new Thread(new ThreadStart(() =>
                    {
                        response = channel.SiteUtil.GetStreamUrl(channel);
                    }));

                    thread.Start();

                    //Wait for thread
                    if (!thread.Join(60000))
                    {
                        thread.Abort();
                        response = null;
                        _Logger.Error("[GetLinkFromSite] Function timeout:GetStreamUrl(IptvChannel channel)");
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Error("[GetLinkFromSite] Plugin error:GetStreamUrl(IptvChannel channel) {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    response = null;
                }
            }

            return response;
        }

        /// <summary>
        /// Send event to the http listeners
        /// </summary>
        /// <param name="type"></param>
        /// <param name="o"></param>
        private void httpSendEvent(SendEventTypeEnum type, object o)
        {
            lock (this._EventClientFlagsList) //make sure we are processing one event at time
            {
                bool bLocked = false;
                try
                {
                    Monitor.Enter(this._EventDataSb, ref bLocked);

                    if (this._EventClientFlagsList.Count > 0)
                    {
                        //Get current client list
                        ManualResetEvent[] flags = this._EventClientFlags;

                        //Release lock
                        Monitor.Exit(this._EventDataSb);
                        bLocked = false;

                        //Wait for all clients to be ready
                        ManualResetEvent.WaitAll(flags);

                        //Reacquire the lock
                        Monitor.Enter(this._EventDataSb, ref bLocked);
                        
                        if (type == SendEventTypeEnum.None)
                        {
                            //null termination chunk; we are closing
                            this._EventData[0] = (byte)'0';
                            this._EventData[1] = (byte)'\r';
                            this._EventData[2] = (byte)'\n';
                            this._EventData[3] = (byte)'\r';
                            this._EventData[4] = (byte)'\n';
                            this._EventDataSize = 5;
                        }
                        else
                        {
                            //New event
                            this._EventDataSb.Clear();

                            this._EventDataSb.Append("\r\n{");

                            //Type
                            this._EventDataSb.Append("\"eventType\":\"");
                            this._EventDataSb.Append(type);

                            //Id counter
                            this._EventDataSb.Append("\",\"eventId\":\"");
                            this._EventDataSb.Append(++this._EventId);

                            //Object
                            this._EventDataSb.Append("\",\"object\":");

                            switch (type)
                            {
                                case SendEventTypeEnum.ConnectionHandlerAdded:
                                case SendEventTypeEnum.ConnectionHandlerRemoved:
                                case SendEventTypeEnum.ConnectionHandlerChanged:
                                    ((Proxy.ConnectionHandler)o).SerializeJson(this._EventDataSb);
                                    break;

                                case SendEventTypeEnum.ConnectionClientAdded:
                                case SendEventTypeEnum.ConnectionClientRemoved:
                                case SendEventTypeEnum.ConnectionClientChanged:
                                    ((Proxy.RemoteClient)o).SerializeJson(this._EventDataSb);
                                    break;

                                case SendEventTypeEnum.CDNTaskAdded:
                                case SendEventTypeEnum.CDNTaskRemoved:
                                case SendEventTypeEnum.CDNTaskChanged:
                                    ((Proxy.MediaServer.TaskCDN)o).SerializeJson(this._EventDataSb);
                                    break;

                                case SendEventTypeEnum.CDNSegmentAdded:
                                case SendEventTypeEnum.CDNSegmentRemoved:
                                case SendEventTypeEnum.CDNSegmentChanged:
                                    ((Proxy.MediaServer.TaskSegmentCDN)o).SerializeJson(this._EventDataSb);
                                    break;

                                default:
                                    return;
                            }

                            this._EventDataSb.Append("}\r\n");
                                                        
                        write:
                            //Insert chunk length in hex format first
                            this._EventDataSize = 0;
                            int iSize = this._EventDataSb.Length - 4;
                            int iShift = iSize >= 0x10000 ? 28 : 12;
                            while (iShift >= 0)
                            {
                                int iValue = (iSize >> iShift) & 0xF;
                                if (this._EventDataSize > 0 || iShift == 0 || iValue != 0)
                                    this._EventData[this._EventDataSize++] = (byte)(iValue + (iValue > 9 ? 'W' : '0'));

                                iShift -= 4;
                            }
                                                        
                            try
                            {
                                //Append StrinBuilder to the data as UTF8
                                this._EventDataSb.GetUTF8Bytes(this._EventData, ref this._EventDataSize);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                //Resize the buffer
                                this._EventData = new byte[this._EventData.Length * 2];
                                goto write;
                            }
                        }

                        //Reset all client flags
                        for (int i = 0; i < this._EventClientFlags.Length; i++)
                            this._EventClientFlags[i].Reset();

                        //Fire notification flag
                        Monitor.PulseAll(this._EventDataSb);
                    }
                }
                finally
                {
                    if (bLocked)
                        Monitor.Exit(this._EventDataSb);
                }
            }
        }
        #endregion

        #region Callbacks

        private void cbConnectionHandler(object sender, Proxy.ConnectionEventArgs e)
        {
            switch (e.EventType)
            {
                case Proxy.ConnectionEventTypeEnum.HandlerAdded:
                    this.httpSendEvent(SendEventTypeEnum.ConnectionHandlerAdded, sender);
                    break;

                case Proxy.ConnectionEventTypeEnum.HandlerRemoved:
                    this.httpSendEvent(SendEventTypeEnum.ConnectionHandlerRemoved, sender);
                    break;

                case Proxy.ConnectionEventTypeEnum.HandlerUpdated:
                    this.httpSendEvent(SendEventTypeEnum.ConnectionHandlerChanged, sender);
                    break;

                case Proxy.ConnectionEventTypeEnum.ClientAdded:
                    this.httpSendEvent(SendEventTypeEnum.ConnectionClientAdded, e.Tag);
                    break;

                case Proxy.ConnectionEventTypeEnum.ClientRemoved:
                    this.httpSendEvent(SendEventTypeEnum.ConnectionClientRemoved, e.Tag);
                    break;

                case Proxy.ConnectionEventTypeEnum.ClientUpdated:
                    this.httpSendEvent(SendEventTypeEnum.ConnectionHandlerChanged, e.Tag);
                    break;

            }
        }

        private void cbTaskEvent(object sender, EventArgs e)
        {
            Proxy.MediaServer.Task task = (Proxy.MediaServer.Task)sender;
            Proxy.MediaServer.TaskEventArgs args = (Proxy.MediaServer.TaskEventArgs)e;

            switch (args.Type)
            {
                case Proxy.MediaServer.TaskEventTypeEnum.SegmentNew:
                    this.httpSendEvent(SendEventTypeEnum.CDNSegmentAdded, args.Tag);
                    break;

                case Proxy.MediaServer.TaskEventTypeEnum.SegmentUpdate:
                case Proxy.MediaServer.TaskEventTypeEnum.SegmentStateChanged:
                    this.httpSendEvent(SendEventTypeEnum.CDNSegmentChanged, args.Tag);
                    break;

                case Proxy.MediaServer.TaskEventTypeEnum.SegmentDeleted:
                    this.httpSendEvent(SendEventTypeEnum.CDNSegmentRemoved, args.Tag);
                    break;

                case Proxy.MediaServer.TaskEventTypeEnum.TaskStateChanged:
                    this.httpSendEvent(SendEventTypeEnum.CDNTaskChanged, task);

                    if (sender is Proxy.MediaServer.TaskCDN)
                    {
                        if (task.Status == Proxy.MediaServer.TaskStatusEnum.Iddle
                            && ((Proxy.MediaServer.TaskCDN)task).Autoterminate)// && !((Proxy.MediaServer.PlayerTask)task).Persistent)
                        {
                            this.cdnTaskDelete((Proxy.MediaServer.TaskCDN)task);
                            deleteFolder(task.WorkFolder);
                        }
                    }

                    break;
            }
        }
        #endregion

        #region CDN

        private Proxy.MediaServer.TaskCDN cdnTaskGet(string strIdentifier)
        {
            lock (this._CDNTasks)
            {
                int iId;
                if (!int.TryParse(strIdentifier, out iId))
                    iId = -1;

                for (int i = 0; i < this._CDNTasks.Count; i++)
                {
                    Proxy.MediaServer.TaskCDN task = this._CDNTasks[i];

                    if (iId >= 0 && task.Identifier == iId)
                        return task;

                    if (task.Url == strIdentifier && task.IsRequestAvailable)
                        return task;
                }
            }

            return null;

        }

        private void cdnTaskAdd(Proxy.MediaServer.TaskCDN task)
        {
            lock (this._CDNTasks)
            {
                this._CDNTasks.Add(task);
                this.httpSendEvent(SendEventTypeEnum.CDNTaskAdded, task);
            }
        }

        private bool cdnTaskDelete(Proxy.MediaServer.TaskCDN task)
        {
            lock (this._CDNTasks)
            {
                this.httpSendEvent(SendEventTypeEnum.CDNTaskRemoved, task);
                return this._CDNTasks.Remove(task);
            }
        }

        #endregion
    }
}
