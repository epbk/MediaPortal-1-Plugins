using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;
using NLog;

namespace MediaPortal.IptvChannels.Proxy
{
    public class ConnectionHandler
    {
        #region Constants
        public const int TS_BLOCK_SIZE = 188;
        public const int TS_BLOCK_MARKER = 0x47;
        private const int REFRESH_PERIOD = 1000; //1s
        #endregion

        private enum ProcessResultEnum
        {
            Unknown,
            NoData,
            NoClients,
            Terminate,
        }

        private enum SendDataTypeEnum
        {
            Data,
            Header,
            HeaderError
        }

        #region Private Fields

        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private static List<ConnectionHandler> _HandlerList = new List<ConnectionHandler>();

        private List<RemoteClient> _ClientList = new List<RemoteClient> { };

        private static long _IdCnt = 0;
        private long _Id = 0;
        private string _HandlerId;

        private ulong _DataCounter = 0;
        private ulong _DataSent = 0;
        private int _SendPeek = 0;
        private bool _Closed = false;

        private int _TimoutNoClients = Database.dbSettings.Instance.TimeoutNoClients;
        private int _TimoutNoData = Database.dbSettings.Instance.TimeoutNoData;

        private DateTime _OpenTimeStamp = new DateTime();
        private DateTime _StartProcesTimeStamp = new DateTime();

        private Thread _ThreadProcess = null;
        private bool _ForceTerminate = false;

        private string _Info = string.Empty;

        private static System.Globalization.CultureInfo _CiEn = new System.Globalization.CultureInfo("en-US");

        private static List<ConnectionEventHandler> _EventTargets = new List<ConnectionEventHandler>();

        private Plugin.GetSiteLinkHandler getLinkHandler;

        private Dictionary<string, string> _HttpArguments;

        private static Dictionary<string, DateTime> _BlackListUrl = new Dictionary<string, DateTime>();
        #endregion

        #region Public Fields

        public string Info
        {
            get
            {
                return this._Info;
            }
            set
            {
                this._Info = value;

                if (_EventTargets.Count > 0)
                    invokeEvent(this, new ConnectionEventArgs() { EventType = ConnectionEventTypeEnum.HandlerUpdated, Tag = this });
            }
        }

        public bool LogEvents = false;
        public string ServerUrl { get; private set; } = null;
        public string Args { get; private set; } = null;
        public string SiteUtil { get; private set; } = null;
        public string ChannelId { get; private set; } = null;
        public bool UseMediaServer { get; private set; } = Database.dbSettings.Instance.UseMediaServer;
        public string DRMLicenceServer { get; private set; } = null;
        public Pbk.Net.Http.HttpUserWebRequestArguments DRMHttpArguments { get; private set; } = null;
        public Pbk.Net.Http.HttpUserWebRequestArguments HttpArguments { get; private set; } = null;
        public string DRMKey { get; private set; } = null;
        public StreamingEngineEnum StreamingEngine { get; private set; } = StreamingEngineEnum.Default;
        public bool SegmentListBuild { get; set; } = true;

        public StreamTypeEnum StreamType { get; private set; } = StreamTypeEnum.Unknown;

        public string HandlerId
        { get { return this._HandlerId; } }

        public int ConnectedClients
        {
            get
            {
                //lock (this._ClientList)
                {
                    return this._ClientList.Count;
                }
            }
        }

        public List<RemoteClient> ClientList
        {
            get
            {
                List<RemoteClient> result = new List<RemoteClient>();
                lock (this._ClientList)
                {
                    result.AddRange(this._ClientList);
                }

                return result;
            }
        }
        #endregion

        public static event ConnectionEventHandler Event
        {
            add
            {
                lock (_EventTargets)
                {
                    if (!_EventTargets.Exists(h => h == value))
                        _EventTargets.Add(value);
                }
            }

            remove
            {
                lock (_EventTargets)
                {
                    _EventTargets.Remove(value);
                }
            }
        }

        #region ctor
        public ConnectionHandler(string strServerUrl)
        {
            this._Id = Interlocked.Increment(ref _IdCnt);
            this._HandlerId = this._Id.ToString("000");

            this._OpenTimeStamp = DateTime.Now;
            this.ServerUrl = strServerUrl;

            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][ctor] URL: {1}", this._HandlerId, strServerUrl);
        }
        #endregion

        public static ConnectionHandler Get(Dictionary<string, string> prm, Plugin.GetSiteLinkHandler getLinkHandler)
        {
            lock (_HandlerList)
            {
                string strSite = null;
                string strChannel = null;

                if (!prm.TryGetValue(Plugin.URL_PARAMETER_NAME_URL, out string strUrl) || !Uri.IsWellFormedUriString(strUrl, UriKind.Absolute))
                {
                    //Invalid url; try get site/channel prms
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_SITE, out strSite) && prm.TryGetValue(Plugin.URL_PARAMETER_NAME_CHANNEL, out strChannel)
                        && !string.IsNullOrWhiteSpace(strSite) && !string.IsNullOrWhiteSpace(strChannel))
                    {
                        strUrl = strSite + '+' + strChannel;
                    }
                    else
                        return null;
                }

                //Try find existing handler
                ConnectionHandler handler = _HandlerList.Find(h => h.ServerUrl == strUrl);
                if (handler == null)
                {
                    //New handler
                    handler = new ConnectionHandler(strUrl)
                    {
                        getLinkHandler = getLinkHandler,
                        SiteUtil = strSite,
                        ChannelId = strChannel,
                        Args = prm.TryGetValue(Plugin.URL_PARAMETER_NAME_ARGUMENTS, out string strValue) ? strValue : null,
                        _HttpArguments = prm
                    };

                    //MediaServer prm
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_MEDIA_SERVER, out strValue))
                        handler.UseMediaServer = strValue == "1";

                    //StreamType prm
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_STREAM_TYPE, out strValue) && Enum.TryParse(strValue, true, out StreamTypeEnum type))
                        handler.StreamType = type;

                    //DRM LicenceServer
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_DRM_LICENCE_SERVER, out strValue) && !string.IsNullOrWhiteSpace(strValue))
                        handler.DRMLicenceServer = strValue;

                    //DRM HttpHeaders
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_DRM_HTTP_ARGUMENTS, out strValue) && !string.IsNullOrWhiteSpace(strValue))
                        handler.DRMHttpArguments = Pbk.Net.Http.HttpUserWebRequestArguments.Deserialize(strValue);

                    //DRM Key
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_DRM_KEY, out strValue) && !string.IsNullOrWhiteSpace(strValue))
                        handler.DRMKey = strValue;

                    //Http Arguments
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_HTTP_ARGUMENTS, out strValue) && !string.IsNullOrWhiteSpace(strValue))
                        handler.HttpArguments = Pbk.Net.Http.HttpUserWebRequestArguments.Deserialize(strValue);

                    //StreamingEngine
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_STREAMING_ENGINE, out strValue) && Enum.TryParse(strValue, true, out StreamingEngineEnum eng))
                        handler.StreamingEngine = eng;

                    //SegmentListBuild
                    if (prm.TryGetValue(Plugin.URL_PARAMETER_NAME_SEGMENT_LIST_BUILD, out strValue))
                        handler.SegmentListBuild = strValue == "1";


                    _HandlerList.Add(handler);

                    if (_EventTargets.Count > 0)
                        invokeEvent(handler, new ConnectionEventArgs() { EventType = ConnectionEventTypeEnum.HandlerAdded, Tag = handler });

                    handler._ThreadProcess = new Thread(new ThreadStart(() => handler.doProcess()));
                    handler._ThreadProcess.Start();
                }

                return handler;
            }
        }

        public void Close()
        {
            if (!this._Closed)
            {
                this._ForceTerminate = true;
                this.close();
                this._ThreadProcess.Join();
            }
        }

        public static void CloseAll()
        {
            lock (_HandlerList)
            {
                _HandlerList.ForEach(h => h.Close());
            }
        }

        public void AddClient(Socket socketRemote)
        {
            lock (this._ClientList)
            {
                socketRemote.SendTimeout = 5000;
                RemoteClient c = new RemoteClient(this, socketRemote);
                c.BufferSize = Database.dbSettings.Instance.ClientMemoryBufferSize;

                this._ClientList.Add(c);

                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[" + this._HandlerId + "][AddClient(] Remote socket: " + c.RemotePoint);

                if (_EventTargets.Count > 0)
                    invokeEvent(this, new ConnectionEventArgs() { EventType = ConnectionEventTypeEnum.ClientAdded, Tag = c });
            }
        }

        public static IEnumerable<ConnectionHandler> GetHandlers()
        {
            lock (_HandlerList)
            {
                for (int i = 0; i < _HandlerList.Count; i++)
                    yield return _HandlerList[i];
            }
        }

        public StringBuilder SerializeJson(StringBuilder sb)
        {
            sb.Append('{');
            sb.Append("\"type\":\"ConnectionHandler\",");
            sb.Append("\"id\":\"");
            sb.Append(this.HandlerId);
            sb.Append("\",\"url\":\"");
            sb.Append(this.ServerUrl);
            sb.Append("\",\"info\":\"");
            Tools.Json.AppendAndValidate(this.Info, sb);
            sb.Append("\",\"clients\":\"");
            sb.Append(this._ClientList.Count);
            sb.Append("\"}");

            return sb;
        }

        #region Private Methods

        private void close()
        {
            lock (this._ClientList)
            {
                if (!this._Closed)
                {
                    if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[" + this._HandlerId + "][close] Closing...");

                    for (int i = this._ClientList.Count - 1; i >= 0; i--)
                    {
                        RemoteClient c = this._ClientList[i];
                        try
                        {
                            c.Close();
                        }
                        catch
                        {
                        }

                        this._ClientList.RemoveAt(i);

                        if (_EventTargets.Count > 0)
                            invokeEvent(this, new ConnectionEventArgs() { EventType = ConnectionEventTypeEnum.ClientRemoved, Tag = c });
                    };

                    this._Closed = true;

                    if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[" + this._HandlerId + "][close] Closed.");
                }
            }

            lock (_HandlerList)
            {
                _HandlerList.Remove(this);
            }

            if (_EventTargets.Count > 0)
                invokeEvent(this, new ConnectionEventArgs() { EventType = ConnectionEventTypeEnum.HandlerRemoved, Tag = this });
        }

        private void doProcess()
        {
            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][DoProcess] Start...", this._HandlerId);

            bool bHeaderSent = false;
            int iAttempts = 5;
            string strUrl = null;
            byte[] buffer = null;
            Stream remoteStream = null;
            int iDataSize;

            try
            {
                while (iAttempts-- > 0)
                {
                    if (this.SiteUtil != null)
                    {
                        //Get final url from plugin
                        this.Info = "Getting URL from plugin...";
                        SiteUtils.LinkResult result = this.getLinkHandler(this.SiteUtil, this.ChannelId);
                        if (result != null)
                        {
                            strUrl = result.Url;
                            this.DRMLicenceServer = result.DRMLicenceServer;
                            this.DRMHttpArguments = result.DRMHttpArguments;
                            this.DRMKey = result.DRMKey;
                            this.HttpArguments = result.HttpArguments;
                            this.StreamType = result.StreamType;
                            this.StreamingEngine = result.StreamingEngine;
                            this.SegmentListBuild = result.SegmentListBuild;
                        }
                        else
                            strUrl = null;
                    }
                    else
                        strUrl = this.ServerUrl;

                    if (Uri.IsWellFormedUriString(strUrl, UriKind.Absolute))
                    {
                        //If the scheme is not http then use ffmpeg process
                        if (!strUrl.StartsWith("http://") && !strUrl.StartsWith("https://"))
                        {
                            //Call FFMPEG
                            this._StartProcesTimeStamp = DateTime.Now;
                            this.doProcessFFMPEG(strUrl);
                            return;
                        }

                        //Do not process invalid url not older than 60s
                        if (_BlackListUrl.TryGetValue(strUrl, out DateTime dt) && (DateTime.Now - dt).TotalSeconds < 60)
                        {
                            _Logger.Error("[{0}][DoProcess] Blacklisted url: {1}", this._HandlerId, strUrl);
                            goto ext;
                        }

                        switch (this.StreamType)
                        {
                            case StreamTypeEnum.TransportStrem:

                                //Direct TransportStream

                                this.Info = "Connecting to URL...";

                                Client con = new Client(remoteStream, this.sendToAllClients);
                                if (con.Connect())
                                {
                                    if (!bHeaderSent)
                                    {
                                        // Send http OK to the clients
                                        this.sendToAllClients(null, 0, 0, -1, SendDataTypeEnum.Header, false);
                                        bHeaderSent = true;
                                    }

                                    //Start monitoring
                                    this._StartProcesTimeStamp = DateTime.Now;

                                    ProcessResultEnum result = this.processMonitor(con, null);

                                    if ((DateTime.Now - this._StartProcesTimeStamp).TotalSeconds >= 60)
                                        iAttempts = 0;

                                    con.Close();

                                    switch (result)
                                    {
                                        case ProcessResultEnum.NoClients:
                                        case ProcessResultEnum.Terminate:
                                            return; //exit

                                        case ProcessResultEnum.NoData:
                                            //no data from the source stream; try to connect again
                                            break;
                                    }
                                }

                                break;

                            case StreamTypeEnum.Dash:
                            case StreamTypeEnum.HLS:
                                //HLS & DASH: use FFMPEG to convert the source stream to TransportStream

                                if (this.UseMediaServer)
                                {
                                    //MediaServer http cache is used
                                    StringBuilder sb = new StringBuilder(256);

                                    //Server
                                    sb.Append("http://127.0.0.1:");
                                    sb.Append(Database.dbSettings.Instance.HttpServerPort);

                                    //Path
                                    sb.Append(Plugin.HTTP_PATH_MEDIA_HANDLER);

                                    //Url
                                    sb.Append('?');
                                    sb.Append(Plugin.URL_PARAMETER_NAME_URL);
                                    sb.Append('=');
                                    sb.Append(System.Web.HttpUtility.UrlEncode(strUrl));

                                    if (this._HttpArguments != null)
                                    {
                                        foreach (KeyValuePair<string, string> prm in this._HttpArguments)
                                        {
                                            sb.Append('&');
                                            sb.Append(prm.Key);
                                            sb.Append('=');
                                            sb.Append(System.Web.HttpUtility.UrlEncode(prm.Value));
                                        }
                                    }

                                    //DRM licence server
                                    if (!string.IsNullOrWhiteSpace(this.DRMLicenceServer))
                                    {
                                        sb.Append('&');
                                        sb.Append(Plugin.URL_PARAMETER_NAME_DRM_LICENCE_SERVER);
                                        sb.Append('=');
                                        sb.Append(System.Web.HttpUtility.UrlEncode(this.DRMLicenceServer));
                                    }

                                    //DRM HttpArguments
                                    if (this.DRMHttpArguments != null)
                                    {
                                        sb.Append('&');
                                        sb.Append(Plugin.URL_PARAMETER_NAME_DRM_HTTP_ARGUMENTS);
                                        sb.Append('=');
                                        sb.Append(System.Web.HttpUtility.UrlEncode(this.DRMHttpArguments.Serialize()));
                                    }

                                    //DRM Key
                                    if (!string.IsNullOrWhiteSpace(this.DRMKey))
                                    {
                                        sb.Append('&');
                                        sb.Append(Plugin.URL_PARAMETER_NAME_DRM_KEY);
                                        sb.Append('=');
                                        sb.Append(System.Web.HttpUtility.UrlEncode(this.DRMKey));
                                    }

                                    //SegmentListBuild
                                    sb.Append('&');
                                    sb.Append(Plugin.URL_PARAMETER_NAME_SEGMENT_LIST_BUILD);
                                    sb.Append('=');
                                    sb.Append(this.SegmentListBuild ? "1" : "0");


                                    //Http user arguments
                                    if (this.HttpArguments != null)
                                    {
                                        sb.Append('&');
                                        sb.Append(Plugin.URL_PARAMETER_NAME_HTTP_ARGUMENTS);
                                        sb.Append('=');
                                        sb.Append(System.Web.HttpUtility.UrlEncode(this.HttpArguments.Serialize()));
                                    }

                                    strUrl = sb.ToString();
                                }

                                //Call FFMPEG
                                this._StartProcesTimeStamp = DateTime.Now;
                                this.doProcessFFMPEG(strUrl);
                                return;

                            case StreamTypeEnum.Unknown:
                            default:
                                //Unknown type; try determine the type from the response

                                buffer = new byte[TS_BLOCK_SIZE];

                                try
                                {
                                    Pbk.Net.Http.HttpUserWebRequest rq = new Pbk.Net.Http.HttpUserWebRequest(strUrl, this.HttpArguments);
                                    remoteStream = rq.GetResponseStream();
                                    if (rq.HttpResponseCode == HttpStatusCode.OK)
                                    {
                                        iDataSize = remoteStream.Read(buffer, 0, buffer.Length);
                                        if (iDataSize >= 7)
                                        {
                                            if (buffer[0] == TS_BLOCK_MARKER)
                                            {
                                                //TransportStream
                                                _Logger.Debug("[{0}][DoProcess] StreamType: TS", this._HandlerId);
                                                this.StreamType = StreamTypeEnum.TransportStrem;
                                                goto case StreamTypeEnum.TransportStrem;
                                            }

                                            if (buffer[0] == '#' && buffer[1] == 'E' && buffer[2] == 'X' && buffer[3] == 'T' && buffer[4] == 'M' && buffer[5] == '3' && buffer[6] == 'U')
                                            {
                                                //HLS
                                                _Logger.Debug("[{0}][DoProcess] StreamType: HLS", this._HandlerId);
                                                this.StreamType = StreamTypeEnum.HLS;
                                                rq.Close();
                                                goto case StreamTypeEnum.HLS;
                                            }

                                            if (buffer[0] == '<' && (buffer[1] == '?' && buffer[2] == 'x' && buffer[3] == 'm' && buffer[4] == 'l')
                                                || (buffer[1] == 'M' && buffer[2] == 'P' && buffer[3] == 'D'))
                                            {
                                                //MPEG DASH
                                                _Logger.Debug("[{0}][DoProcess] StreamType: DASH", this._HandlerId);
                                                this.StreamType = StreamTypeEnum.Dash;
                                                rq.Close();
                                                goto case StreamTypeEnum.Dash;
                                            }

                                            _Logger.Error("[{0}][DoProcess] Unknown stream type.", this._HandlerId);
                                            rq.Close();
                                            return;
                                        }
                                    }
                                    rq.Close();
                                }
                                catch (Exception ex)
                                {
                                    _Logger.Error("[{3}][DoProcess] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._HandlerId);
                                }

                                break;
                        }
                    }

                    Thread.Sleep(500);
                }

                //Put the url into blacklist
                if (strUrl != null)
                    _BlackListUrl[strUrl] = DateTime.Now;

               ext:
                if (!bHeaderSent)
                    this.sendToAllClients(null, 0, 0, 0, SendDataTypeEnum.HeaderError, false);
            }

            catch (Exception ex)
            {
                _Logger.Error("[{3}][DoProcess] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._HandlerId);
            }
            finally
            {
                this.close();
                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][DoProcess] Closed.", this._HandlerId);
            }
        }

        private void doProcessFFMPEG(string strUrl)
        {
            if ((this.StreamingEngine == StreamingEngineEnum.VLC 
                || (this.StreamingEngine == StreamingEngineEnum.Default && Database.dbSettings.Instance.StreamingEngine == StreamingEngineEnum.VLC))
                && File.Exists(Database.dbSettings.Instance.VlcPath))
            {
                this.doProcessVLC(strUrl);
                return;
            }

            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][ffmpeg][DoProcess] Start...", this._HandlerId);

            this.Info = "Connecting to UDP...";

            //Start FFmpeg
            try
            {
                //Init UDP
                Client udp = new Client(null, this.sendToAllClients, 0); //port = 0 : pick free port
                if (!udp.Connect())
                    return;

                this.Info = "Connecting to FFmpeg ...";

                ProcessStartInfo startInfo = new ProcessStartInfo();

                //if (!this._ShowDosWindowBox)
                {
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.CreateNoWindow = true;
                }

                startInfo.FileName = "\"ffmpeg.exe\"";
                startInfo.UseShellExecute = false;
                startInfo.ErrorDialog = false;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                Process pr = new Process();
                pr.StartInfo = startInfo;
                //test.Exited += new EventHandler(restream_Exited);
                pr.OutputDataReceived += this.outputHandler;
                pr.ErrorDataReceived += this.errorHandler;

                //-map 0 From input index #0 (the 1st input) select all streams. 
                //- map 1:a From input index #1 (the 2nd input) select all audio streams. 
                //-map 3:s: 4 From input index #3 (the 4th input) select subtitle stream index #4 (the fifth subtitle stream). 
                //- map 0 - map - 0:s Will select all streams from input index #0 (the 1st input) except subtitles. The - indicates negative mapping.
                //If you do not use the -map option then the default stream selection behavior will automatically choose streams.

                startInfo.Arguments = " -re -i " + strUrl + ' ' + (this.Args ?? Database.dbSettings.Instance.FfmpegStreamArguments) + " -f mpegts udp://127.0.0.1:" + udp.Port;

                // Send http OK
                this.sendToAllClients(null, 0, 0, -1, SendDataTypeEnum.Header, false);

                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][ffmpeg][DoProcess] Starting FFmpeg: {1}", this._HandlerId, startInfo.Arguments);

                //Start ffmpeg
                pr.Start();
                pr.BeginOutputReadLine();
                pr.BeginErrorReadLine();

                //Proxy url
                //this.ServerUrl = "udp://127.0.0.1:" + udp.Port;

                //Start UDP process
                this.processMonitor(udp, null);

                pr.CancelOutputRead();
                pr.CancelErrorRead();

                //Stop FFmpeg
                if (!pr.HasExited)
                    pr.Kill();

                //Close UDP
                udp.Close();
                udp = null;
            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][ffmpeg][DoProcess] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._HandlerId);
            }
            finally
            {
                this.close();
            }

            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][ffmpeg][DoProcess] Closed.", this._HandlerId);
        }

        private void doProcessVLC(string strUrl)
        {
            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][vlc][DoProcess] Start...", this._HandlerId);

            this.Info = "Connecting to UDP...";

            //Start VLC
            try
            {
                //Init UDP
                Client udp = new Client(null, this.sendToAllClients, 0); //port = 0 : pick free port
                if (!udp.Connect())
                    return;

                this.Info = "Connecting to VLC ...";

                // Send http OK
                this.sendToAllClients(null, 0, 0, -1, SendDataTypeEnum.Header, false);

                //Start VLC streaming
                int iId = VlcControlManager.Instance.StreamingStart(strUrl, udp.Port);
                if (iId < 0)
                {
                    _Logger.Error("[{0}][vlc][DoProcess] Failed to start VLC sreaming.", this._HandlerId);
                    return;
                }

                //Start UDP process
                this.processMonitor(udp, null);

                //Stop VLC streaming
                VlcControlManager.Instance.StreamingDelete(iId);

                //Close UDP
                udp.Close();
                udp = null;
            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][vlc][DoProcess] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._HandlerId);
            }
            finally
            {
                this.close();
            }

            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][vlc][DoProcess] Closed.", this._HandlerId);
        }

        private void outputHandler(object sender, DataReceivedEventArgs e)
        {
            if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][ffmpeg][outputHandler] {1}", this._HandlerId, e.Data);
        }

        private void errorHandler(object sender, DataReceivedEventArgs e)
        {
            if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][ffmpeg][errorHandler] {1}", this._HandlerId, e.Data);
        }

        private static string getSizeStringShort(ulong val)
        {
            string strT, strVal;
            if (val < 1024)
            {
                strVal = val.ToString();
                strT = " B";
            }
            else if (val < 1048576)
            {
                strVal = (val / 1024).ToString();
                strT = " kB";
            }
            else if (val < 1073741824)
            {
                strVal = (val / 1048576).ToString();
                strT = " MB";
            }
            else
            {
                strVal = ((double)val / 1073741824).ToString("0.00");
                strT = " GB";
            }

            return strVal + strT;
        }


        private ProcessResultEnum processMonitor(IClient client, Pbk.Utils.Buffering.IBuffer buffer)
        {
            ProcessResultEnum result = ProcessResultEnum.Unknown;

            ulong lDownloadedBytes = 0;
            int iTimeOutAct = 0;

            int iSocketCnt;
            int iSocketCntTimeout = 0;

            StringBuilder sbInfo = new StringBuilder(256);

            while (true)
            {
                if (this._ForceTerminate)
                {
                    result = ProcessResultEnum.Terminate;
                    break;
                }

                Thread.Sleep(REFRESH_PERIOD);

                iSocketCnt = this._ClientList.Count;

                if (iSocketCnt < 1)
                    iSocketCntTimeout += REFRESH_PERIOD;
                else
                    iSocketCntTimeout = 0;

                if (iSocketCntTimeout >= this._TimoutNoClients)
                {
                    result = ProcessResultEnum.NoClients;
                    break;
                }

                //Data timeout test
                ulong lReadDiff = client.DataSent - lDownloadedBytes;
                if (lReadDiff == 0)
                    iTimeOutAct += REFRESH_PERIOD;
                else
                    iTimeOutAct = 0;

                if (iTimeOutAct >= this._TimoutNoData)
                {
                    //Timeout elapsed; no data has been received within the interval
                    this.Info = "No Data. Closing connection with the server. [" + this._HandlerId + "]";
                    _Logger.Error("[{0}][Process] No Data. Closing connection with the server.", this._HandlerId);

                    result = ProcessResultEnum.NoData;

                    break;
                }
                else if (_EventTargets.Count > 0)
                {
                    #region Info
                    uint wBitRate = (uint)((float)(lReadDiff * 8) / REFRESH_PERIOD); //in kbit
                    ulong dwTimeDiff = (ulong)(DateTime.Now - client.DataSentFirstTS).TotalSeconds;
                    ulong dwBitRateAvg = dwTimeDiff > 0 && client.DataSent > 0 ? (client.DataSent * 8 / 1000 / dwTimeDiff) : 0;

                    sbInfo.Clear();
                    sbInfo.Append("Data received:  ");
                    sbInfo.Append(getSizeStringShort(client.DataSent));
                    sbInfo.Append("   Bitrate: ");
                    if (wBitRate < 1000)
                    {
                        sbInfo.Append(wBitRate);
                        sbInfo.Append(" kbit");
                    }
                    else
                    {
                        sbInfo.Append(((float)wBitRate / 1000).ToString("0.00"));
                        sbInfo.Append(" mbit");
                    }
                    sbInfo.Append('/');
                    if (dwBitRateAvg > 0)
                    {
                        if (dwBitRateAvg < 1000)
                        {
                            sbInfo.Append(dwBitRateAvg);
                            sbInfo.Append(" kbit");
                        }
                        else
                        {
                            sbInfo.Append(((float)dwBitRateAvg / 1000).ToString("0.00"));
                            sbInfo.Append(" mbit");
                        }
                    }
                    else
                        sbInfo.Append("0 kbit");
                    sbInfo.Append("  Errors: ");
                    sbInfo.Append(client.PacketErrors);
                    if (buffer != null)
                    {
                        sbInfo.Append("  Buffer: [");
                        sbInfo.Append(buffer.BuffersInUse);
                        sbInfo.Append('/');
                        sbInfo.Append(buffer.Buffers);
                        sbInfo.Append('/');
                        sbInfo.Append(buffer.BuffersMax);
                        sbInfo.Append("][");
                        sbInfo.Append(Pbk.Utils.Tools.PrintFileSize(buffer.BufferSizeMax, "0", _CiEn));
                        sbInfo.Append("] ");
                        sbInfo.Append(buffer.CurrentLevel.ToString("0"));
                        sbInfo.Append('%');
                    }
                    sbInfo.Append("   Handler duration: ");
                    sbInfo.Append((DateTime.Now - this._OpenTimeStamp).ToString("hh\\:mm\\:ss"));
                    this.Info = sbInfo.ToString();
                    #endregion
                }

                lDownloadedBytes = client.DataSent;
            }

            this._DataSent = client.DataSent;

            return result;
        }


        private void removeClient(int iIdx)
        {
            lock (this._ClientList)
            {
                RemoteClient client = this._ClientList[iIdx];
                this._ClientList.Remove(client);
                //this._CheckClientsTs = DateTime.Now;

                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[" + this._HandlerId + "][removeClient] Remote socket: " + client.RemotePoint);

                if (_EventTargets.Count > 0)
                    invokeEvent(this, new ConnectionEventArgs() { EventType = ConnectionEventTypeEnum.ClientRemoved, Tag = client });
            }
        }

        private void sendHttpHeader(RemoteClient client)
        {
            this.sendHttpHeader(client, createHttpResponse());
        }
        private void sendHttpHeader(RemoteClient client, string strResponse)
        {
            if (!client.HttpResponseSent)
            {
                //First place into buffer default http response
                byte[] resp = Encoding.ASCII.GetBytes(strResponse);

                client.WriteData(resp, 0, resp.Length);

                client.HttpResponseSent = true;
            }
        }

        private static string createHttpResponse()
        {
            StringBuilder sb = new StringBuilder(128);

            sb.Append("HTTP/1.1 200 OK");
            sb.Append(Pbk.Net.Http.HttpHeaderField.EOL);

            sb.Append("Content-Type: ");
            sb.Append("video/mp2t");
            sb.Append(Pbk.Net.Http.HttpHeaderField.EOL);

            sb.Append("Connection: close");
            sb.Append(Pbk.Net.Http.HttpHeaderField.EOL);
            sb.Append(Pbk.Net.Http.HttpHeaderField.EOL);

            string strResp = sb.ToString();
            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[CreateHttpResponse] Response:\r\n" + strResp);
            return strResp;
        }

        private static string createHttpErrorResponse()
        {
            StringBuilder sb = new StringBuilder(128);

            sb.Append("HTTP/1.1 404 Not Found");
            sb.Append(Pbk.Net.Http.HttpHeaderField.EOL);

            sb.Append("Connection: close");
            sb.Append(Pbk.Net.Http.HttpHeaderField.EOL);
            sb.Append(Pbk.Net.Http.HttpHeaderField.EOL);

            string strResp = sb.ToString();
            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[CreateHttpErrorResponse] Response:\r\n" + strResp);
            return strResp;
        }

        private static int findTsMarker(byte[] buffer, int iOffset, int iLength)
        {
            int iIdxDataEnd = iOffset + iLength - 1;

            for (int iIdx = iOffset; (iIdx < buffer.Length && iIdx <= iIdxDataEnd); iIdx++)
            {
                if (buffer[iIdx] == TS_BLOCK_MARKER)
                {
                    int iIdxNext = iIdx + TS_BLOCK_SIZE;
                    if (iIdxNext < buffer.Length && iIdxNext <= iIdxDataEnd)
                    {
                        if (buffer[iIdxNext] == TS_BLOCK_MARKER)
                            return iIdx; //Next block has TS marker; OK
                        else
                            continue; //Invalid TS marker in next block; try next byte
                    }
                    else
                        return iIdx; //not enough data; assume correct TS marker
                }
            }

            return -1; //TS marker not found
        }

        private int sendToAllClients(byte[] buffer, int iOffset, int iLength)
        {
            this.sendToAllClients(buffer, iOffset, iLength, -1, SendDataTypeEnum.Data, true);

            return 0;
        }
        private void sendToAllClients(byte[] buffer, int iOffset, int iLength, int iMinSendLength, SendDataTypeEnum dataType, bool bCount)
        {
            if (iLength > this._SendPeek)
            {
                this._SendPeek = iLength;
                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[" + this._HandlerId + "][SendToAllClients] Send peek:" + iLength);
            }

            lock (this._ClientList)
            {
                for (int iIdx = 0; iIdx < this._ClientList.Count; iIdx++)
                {
                    RemoteClient client = this._ClientList[iIdx];

                    if (dataType != SendDataTypeEnum.Data && client.HttpResponseSent)
                        continue;

                    int iClientOffset = iOffset;
                    int iClientLength = iLength;

                    try
                    {
                        if (client.IsConnected)
                        {
                            lock (client)
                            {
                                if (dataType != SendDataTypeEnum.Data)
                                {
                                    if (dataType == SendDataTypeEnum.HeaderError)
                                        this.sendHttpHeader(client, createHttpErrorResponse());
                                    else
                                        this.sendHttpHeader(client);
                                    client.SendTs = DateTime.Now;
                                }
                                else
                                {
                                    if (!client.FirstDataPacketSent)
                                    {
                                        //First data to the client

                                        this.sendHttpHeader(client);

                                        //TS packet size boundaries
                                        int iTsMarkerIdx = findTsMarker(buffer, iClientOffset, iClientLength);
                                        if (iTsMarkerIdx >= 0)
                                        {
                                            int iSkip = iTsMarkerIdx - iClientOffset;
                                            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[" + this._HandlerId + "][SendToAllClients] Skipping:" + iSkip + "/" + iClientLength + "  [" + client.RemotePoint + "]");

                                            iClientOffset = iTsMarkerIdx;
                                            iClientLength -= iSkip;
                                        }
                                        else
                                            _Logger.Warn("[" + this._HandlerId + "][SendToAllClients] Invalid TS marker.  [" + client.RemotePoint + "]");

                                        client.FirstDataPacketSent = true;
                                    }

                                    //Send
                                    byte[] bufferSend = buffer;

                                    client.SendTs = DateTime.Now;
                                    int iRem = client.WriteData(bufferSend, iClientOffset, iClientLength);
                                    if (iRem > 0)
                                    {
                                        //Not all data has been written; buffer full

                                        //Buffer overflow
                                        if ((DateTime.Now - client.LastWarning).TotalMilliseconds > 200)
                                        {
                                            client.LastWarning = DateTime.Now;
                                            _Logger.Error("[" + this._HandlerId + "][SendToAllClients] Client buffer overflow. ["
                                                + iClientLength + ":" + client.BufferSize + "][" + client.RemotePoint + "]");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[" + this._HandlerId + "][SendToAllClients] Error occured: client socket closed. Removing from list. " + "[" + client.RemotePoint + "]");
                            client.Close();
                            this.removeClient(iIdx);
                            iIdx--;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Client socket closed ??
                        if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[" + this._HandlerId + "][SendToAllClients] Error occured: unable to send data to client socket. Removing from list. " + "[" + client.RemotePoint + "]");
                        client.Close();
                        this.removeClient(iIdx);
                        iIdx--;
                    }
                }
            }

            if (dataType == SendDataTypeEnum.Data && bCount)
                this._DataCounter += (ulong)iLength;
        }


        private static void invokeEvent(object sender, ConnectionEventArgs e)
        {
            lock (_EventTargets)
            {
                _EventTargets.ForEach(h =>
                    {
                        try { h.Invoke(sender, e); }
                        catch { }
                    });
            }
        }
        #endregion
    }
}
