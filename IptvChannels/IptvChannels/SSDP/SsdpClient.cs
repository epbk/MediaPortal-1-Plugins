using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.CompilerServices;
using NLog;
using MediaPortal.Pbk.Net.Http;

namespace MediaPortal.IptvChannels.SSDP
{
    public class SsdpClient
    {
        #region Types
        private class SsdpSocket
        {
            public Socket ListenerSocket;
            public byte[] Buffer = new byte[1024];
            public EndPoint RemoteEp = new IPEndPoint(IPAddress.Any, 0);
            public volatile bool IsClosing;
            public string LocalEndpoint;
        }

        private class SsdpJob
        {
            public string SearchTarget;
            public byte[] SearchMessage;
            public List<SsdpServerInfo> ServerInfo = new List<SsdpServerInfo>();
            public DateTime LastSearch = DateTime.MinValue;
            private List<SsdpEventHandler> _Callbacks = new List<SsdpEventHandler>();

            public int RegisteredCallbacks => this._Callbacks.Count;

            public TimeSpan NearestExpiration
            {
                get
                {
                    if (this.ServerInfo.Count > 0)
                        return this.ServerInfo.Min(si => si.ExpiresIn);

                    return TimeSpan.MaxValue;
                }
            }

            public SsdpJob(string strSearchTarget, string strUserAgent)
            {
                this.SearchTarget = strSearchTarget;
                this.SearchMessage = buildSearchMessage(strSearchTarget, strUserAgent);
            }

            
            public bool CallbackRegister(SsdpEventHandler callback)
            {
                lock(this._Callbacks)
                {
                    if (!this._Callbacks.Exists(cb => cb == callback))
                    {
                        this._Callbacks.Add(callback);
                        return true;
                    }
                    return false;
                }
            }

            public bool CallbackUnregister(SsdpEventHandler callback)
            {
                lock (this._Callbacks)
                {
                    return this._Callbacks.Remove(callback);
                }
            }

            public void CallbackInvoke(SsdpEventArgsAttribute e)
            {
                lock (this._Callbacks)
                {
                    for (int i = 0; i < this._Callbacks.Count; i++)
                    {
                        try
                        {
                            this._Callbacks[i].Invoke(this, e);
                        }
                        catch { }
                    }
                }
            }
        }

        private class InternalAsyncResult : IAsyncResult
        {
            private AsyncCallback _AsyncCallback;
            private object _AsyncState;
            private ManualResetEvent _WaitHandle = new ManualResetEvent(false);
            private int _Completed = 0;
            private bool _CompletedSync = false;
            private int _Result = -1;
            private Exception _Ex = null;

            public int Result
            {
                get { return this._Result; }
            }

            public Exception Exception
            {
                get { return this._Ex; }
            }

            #region IAsyncResult
            public InternalAsyncResult(AsyncCallback asyncCallback, Object asyncState)
            {
                this._AsyncCallback = asyncCallback;
                this._AsyncState = asyncState;
            }

            public object AsyncState
            {
                get { return this._AsyncState; }
            }

            public WaitHandle AsyncWaitHandle
            {
                get { return this._WaitHandle; }
            }

            public bool CompletedSynchronously
            {
                get { return this._CompletedSync; }
            }

            public bool IsCompleted
            {
                get { return this._Completed > 0; }
            }
            #endregion

            public void SetComplete(int iResult, Exception ex)
            {
                if (Interlocked.CompareExchange(ref this._Completed, 1, 0) == 0)
                {
                    this._Result = iResult;
                    this._Ex = ex;
                    this._WaitHandle.Set();
                    this._AsyncCallback?.Invoke(this);
                }
            }
        }


        #endregion

        #region Constants
        private const string SSDP_BROADCAST_ADDRESS = "239.255.255.250";
        private const int SSDP_BROADCAST_ADDRESS_PORT = 1900;
        #endregion

        #region Private fields
        private static Logger _Logger = LogManager.GetCurrentClassLogger();
        private List<SsdpSocket> _Sockets;
        private Dictionary<string, SsdpJob> _SsdpJobs = new Dictionary<string, SsdpJob>();
        private string _UserAgent;
        private System.Timers.Timer _TimerRefresh;

        #endregion

        #region Properties
        #endregion

        #region Events
        public event SsdpEventHandler Event;
        #endregion

        #region ctor
        public SsdpClient(string strUserAgent = null)
        {
            if (string.IsNullOrWhiteSpace(strUserAgent))
                this._UserAgent = HttpHeaderField.HTTP_USER_AGENT_MOZILLA;
            else
                this._UserAgent = strUserAgent;
        }
        #endregion

        #region Public methods

        /// <summary>
        /// Starts searching for specific targets. Client constantly receives broadcast SSDP messages and updates found ServerInfos.
        /// </summary>
        /// <param name="strSearchTarget">Device name to search for.</param>
        /// <param name="registerCallback">Callback for target events.</param>
        /// <returns>The number of found devices.</returns>
        public int Start(string strSearchTarget, SsdpEventHandler registerCallback)
        {
            if (string.IsNullOrWhiteSpace(strSearchTarget))
                throw new NullReferenceException("Start] strSearchTarget is null");

            if (registerCallback == null)
                throw new NullReferenceException("[Start] registerCallback is null");

            _Logger.Debug("[Start] SearchTarget: '{0}'", strSearchTarget);

            bool bLocked = false;
            try
            {
                Monitor.Enter(this, ref bLocked);

                this.init();

                if (!this._SsdpJobs.TryGetValue(strSearchTarget, out SsdpJob job))
                {
                    job = new SsdpJob(strSearchTarget, this._UserAgent);
                    this._SsdpJobs.Add(strSearchTarget, job);

                    try { this.Event?.Invoke(this, new SsdpEventArgsAttribute(SsdpEventTypeEnum.TargetAdded, strSearchTarget)); }
                    catch { }
                }
                                
                job.CallbackRegister(registerCallback);

                Monitor.Exit(this);
                bLocked = false;

                this.search(job);

                _Logger.Debug("[Start] Complete. SearchTarget: '{0}' Result:{1}", strSearchTarget, job.ServerInfo.Count);
                return job.ServerInfo.Count;
            }
            finally
            {
                if (bLocked)
                    Monitor.Exit(this);
            }
        }

        /// <summary>
        /// Begins to asynchronously search for specific target.
        /// </summary>
        /// <param name="strSearchTarget">Device name to search for.</param>
        /// <param name="registerCallback">Callback for target events.</param>
        /// <param name="callback">The System.AsyncCallback delegate.</param>
        /// <param name="state">An object that contains state information for this request.</param>
        /// <returns>An System.IAsyncResult that references the asynchronous search.</returns>
        public IAsyncResult BeginStart(string strSearchTarget, SsdpEventHandler registerCallback, AsyncCallback callback, object state)
        {
            InternalAsyncResult ar = new InternalAsyncResult(callback, state);

            Thread t = new Thread(new ParameterizedThreadStart((o) =>
            {
                InternalAsyncResult iar = (InternalAsyncResult)((object[])o)[0];
                int iResult = -1;
                try
                {
                    iResult = this.Start((string)((object[])o)[1], (SsdpEventHandler)((object[])o)[2]);
                }
                catch (Exception ex)
                {
                    _Logger.Error("[BeginStart] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    iar.SetComplete(-1, ex);
                }
                finally
                {
                    iar.SetComplete(iResult, null);
                }
            }));

            t.Start(new object[] { ar, strSearchTarget, registerCallback });

            return ar;

        }

        /// <summary>
        /// Ends a pending asynchronous Start operation on soecific search target.
        /// </summary>
        /// <param name="ar">An System.IAsyncResult that stores state information and any user defined data for this asynchronous operation.</param>
        /// <returns>If successful, the number of found devices.</returns>
        public static int EndStart(IAsyncResult ar)
        {
            if (ar == null)
                throw new ArgumentException("IAsyncResult is null");

            return ar.IsCompleted && ar is InternalAsyncResult iar ? iar.Result : -1;
        }

        /// <summary>
        /// Stop monitoring of all search targets started by Start method.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (this._Sockets != null)
            {
                _Logger.Debug("[Stop]");
                this._Sockets.ForEach(socket =>
                {
                    socket.IsClosing = true;
                    socket.ListenerSocket.Close();
                });

                this._TimerRefresh.Dispose();
                this._TimerRefresh = null;
                this._Sockets = null;
                this._SsdpJobs.Clear();
                try { this.Event?.Invoke(this, new SsdpEventArgsAttribute(SsdpEventTypeEnum.ClientStopped, null)); }
                catch { }
            }
        }

        /// <summary>
        /// Stop searching for specific target.
        /// </summary>
        /// <param name="strSearchTarget">Device name to stop searching.</param>
        /// <param name="registerCallback">Callback for target events.</param>
        /// <returns>True if sucessfully removed.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Stop(string strSearchTarget, SsdpEventHandler registerCallback)
        {
            if (string.IsNullOrWhiteSpace(strSearchTarget))
                throw new NullReferenceException("Stop] strSearchTarget is null");

            if (registerCallback == null)
                throw new NullReferenceException("[Stop] registerCallback is null");

            if (this._SsdpJobs.TryGetValue(strSearchTarget, out SsdpJob job))
            {
                if (job.CallbackUnregister(registerCallback))
                {
                    if (job.RegisteredCallbacks == 0)
                    {
                        this._SsdpJobs.Remove(strSearchTarget);
                        try { this.Event?.Invoke(this, new SsdpEventArgsAttribute(SsdpEventTypeEnum.TargetRemoved, strSearchTarget)); }
                        catch { }
                    }

                    return true;
                }
            }
            else
                _Logger.Error("[Stop] SearchTarget not found: {0}", strSearchTarget);

            return false;
        }

        /// <summary>
        /// Retuns all ServerInfos found for given SearchTarget started by Start method.
        /// </summary>
        /// <param name="strSearchTarget">>Device name for ServerInfos to return.</param>
        /// <returns>All ServerInfos found for given SearchTarget.</returns>
        public IEnumerable<SsdpServerInfo> GetServerInfos(string strSearchTarget)
        {
            bool bLocked = false;
            try
            {
                Monitor.Enter(this, ref bLocked);
                if (this._Sockets == null)
                    throw new NullReferenceException("GetServerInfo] Server is stopped.");

                if (string.IsNullOrWhiteSpace(strSearchTarget))
                    throw new NullReferenceException("GetServerInfo] strSearchTarget is null.");

                _Logger.Debug("[GetServerInfo] '{0}'", strSearchTarget);

                if (!this._SsdpJobs.TryGetValue(strSearchTarget, out SsdpJob job))
                    _Logger.Error("[GetServerInfo] SearchTarget not found: '{0}'", strSearchTarget);
                else
                {
                    bLocked = false;
                    Monitor.Exit(this);
                    lock (job)
                    {
                        if (job.ServerInfo.Count == 0)
                            this.search(job);

                        int i = 0;
                        while (i < job.ServerInfo.Count)
                        {
                            SsdpServerInfo si = job.ServerInfo[i];
                            if (si.IsValid)
                            {
                                yield return si;
                                i++;
                            }
                            else
                            {
                                //Remove no longer valid info
                                job.ServerInfo.RemoveAt(i);
                                _Logger.Debug("[GetServerInfo] ServerInfo removed: '{0}'", si.USN);
                                SsdpEventArgsAttribute e = new SsdpEventArgsAttribute(SsdpEventTypeEnum.ServerInfoRemoved, si);
                                job.CallbackInvoke(e);
                                try { this.Event?.Invoke(this, e); }
                                catch { }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (bLocked)
                    Monitor.Exit(bLocked);
            }
        }

        /// <summary>
        /// Discover all devices for spicific SearchTarget.
        /// </summary>
        /// <param name="strSearchTarget">Device name to search for.</param>
        /// <param name="strUserAgent">User agent used for search.</param>
        /// <returns>List of found devices.</returns>
        public static List<SsdpServerInfo> Discover(string strSearchTarget, string strUserAgent)
        {
            return discover(strSearchTarget, buildSearchMessage(strSearchTarget, strUserAgent), true);
        }

        #endregion

        #region Private methods

        private void init()
        {
            if (this._Sockets == null)
            {
                _Logger.Debug("[init] Init sockets...");
                this._Sockets = new List<SsdpSocket>();
                foreach (IPAddress ip in Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork))
                {
                    SsdpSocket ssdp = new SsdpSocket();
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    ssdp.ListenerSocket = socket;
                    socket.Bind(new IPEndPoint(ip, SSDP_BROADCAST_ADDRESS_PORT));
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(SSDP_BROADCAST_ADDRESS), ip));
                    ssdp.LocalEndpoint = socket.LocalEndPoint.ToString();
                    socket.BeginReceiveFrom(ssdp.Buffer, 0, ssdp.Buffer.Length, SocketFlags.None, ref ssdp.RemoteEp, this.cbSocketReceive, ssdp);
                    _Logger.Debug("[init] Socket initialized: {0}", ssdp.LocalEndpoint);
                    this._Sockets.Add(ssdp);
                }
                try { this.Event?.Invoke(this, new SsdpEventArgsAttribute(SsdpEventTypeEnum.ClientStarted, null)); }
                catch { }

                this._TimerRefresh = new System.Timers.Timer();
                this._TimerRefresh.Elapsed += this.cbTimerElapsed;
                this._TimerRefresh.AutoReset = false;
            }
        }

        private int search(SsdpJob job, bool bInitRefresh = true)
        {
            try
            {
                lock (job)
                {
                    if ((DateTime.Now - job.LastSearch).TotalSeconds < 10)
                        return -1;

                    job.LastSearch = DateTime.Now;
                    List<SsdpServerInfo> list = discover(job.SearchTarget, job.SearchMessage, false);
                    if (list?.Count > 0)
                    {
                        list.ForEach(si => this.serverInfoAddOrUpdate(job, si));
                        
                        return list.Count(si => si.IsValid);
                    }

                    _Logger.Debug("[search] None device discovered: '{0}'", job.SearchTarget);
                    return 0;
                }
            }
            finally
            {
                if (bInitRefresh)
                    this.initRefreshTimer();
            }
        }
        private static List<SsdpServerInfo> discover(string strSearchTarget, byte[] searchMessage, bool bLoadDescription)
        {
            try
            {
                //We need to send M-SEARCH message from all interfaces to make sure local apps receive the message.

                List<SsdpServerInfo> result = new List<SsdpServerInfo>();
                IEnumerable<IPAddress> addresses = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork);
                ManualResetEvent flagDone = new ManualResetEvent(false);
                int iThreadsCounter = addresses.Count();

                foreach (IPAddress ip in addresses)
                {
                    ThreadPool.QueueUserWorkItem(new WaitCallback((adr) =>
                    {
                        List<SsdpServerInfo> res = discover((IPAddress)adr, searchMessage, strSearchTarget, bLoadDescription);
                        lock (result)
                        {
                            if (res != null && res.Count > 0)
                            {
                                //Merge result
                                res.ForEach(inf =>
                                {
                                    if (!result.Exists(p => p == inf))
                                    {
                                        //New device
                                        result.Add(inf);
                                    }
                                });
                            }

                            if (--iThreadsCounter == 0)
                                flagDone.Set(); //last job
                        }
                    }
                    ), ip);
                }

                //Wait for all jobs to be done
                flagDone.WaitOne();

                //Log description for each server
                StringBuilder sb = new StringBuilder(1024);
                result.ForEach(srv =>
                {
                    try
                    {
                        if (srv.Parsed)
                        {
                            sb.Clear();
                            sb.AppendLine("[Discover] Device discovered:");
                            srv.PrintReport(sb);
                            sb.AppendLine();
                            _Logger.Debug(sb.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[Discover] Error getting description: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    }
                });

                return result;

            }
            catch (Exception ex)
            {
                _Logger.Error("[Discover] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return null;
            }
            finally
            {

            }
        }
        private static List<SsdpServerInfo> discover(IPAddress ip, byte[] searchMessage, string strSearchTarget, bool bLoadDescription)
        {
            Socket socket = null;
            List<SsdpServerInfo> result;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, ip.GetAddressBytes());
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
                socket.ReceiveTimeout = 2000;

                EndPoint epRemote = new IPEndPoint(IPAddress.Any, 0);
                IPEndPoint epTarget = new IPEndPoint(IPAddress.Parse(SSDP_BROADCAST_ADDRESS), SSDP_BROADCAST_ADDRESS_PORT);
                byte[] buffer = new byte[1024];
                result = new List<SsdpServerInfo>();
                int iAttempts = 3;
                while (iAttempts-- > 0)
                {
                    //Send broadcast message
                    socket.SendTo(searchMessage, epTarget);

                    int iReceived = 0;
                    while (true)
                    {
                        int iLength = 0;
                        try
                        {
                            //Receive response
                            iLength = socket.ReceiveFrom(buffer, iReceived, buffer.Length - iReceived, SocketFlags.None, ref epRemote);
                        }
                        catch (SocketException ex)
                        {
                            if (ex.SocketErrorCode == SocketError.TimedOut)
                                break;
                            else
                                throw ex;
                        }

                        if (iLength > 0)
                        {
                            iReceived += iLength;

                            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            CookieContainer cookies = null;
                            int iResponseLength = HttpUserWebRequest.GetHttpResponse(null, buffer, 0, iReceived, ref fields, ref cookies, out HttpStatusCode httpResponseCode, out _);
                            if (iResponseLength > 0 && httpResponseCode == HttpStatusCode.OK)
                            {
                                _Logger.Debug("[discover] Response received from: {0}\r\n{1}", epRemote, Encoding.ASCII.GetString(buffer, 0, iResponseLength));
                                SsdpServerInfo res = parseResponse("HTTP/1.1 200 OK", fields);
                                if (res != null && (!bLoadDescription || res.LoadDescription()) && res.DeviceType == strSearchTarget && !result.Exists(si => si.IsMatch(res)))
                                    result.Add(res);

                                iReceived = 0;
                            }
                        }
                        else
                            break; //timeout
                    }

                    if (result.Count > 0)
                        break;
                }

                return result;
            }
            catch (Exception ex)
            {
                _Logger.Error("[discover] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return null;
            }
            finally
            {
                if (socket != null)
                    socket.Close();
            }
        }

        private static byte[] buildSearchMessage(string strSearchTarget, string strUserAgent)
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.AppendLine("M-SEARCH * HTTP/1.1");
            sb.Append("HOST: ");
            sb.Append(SSDP_BROADCAST_ADDRESS);
            sb.Append(':');
            sb.Append(SSDP_BROADCAST_ADDRESS_PORT).AppendLine();
            sb.AppendLine("MAN: \"ssdp:discover\"");
            sb.AppendLine("MX: 2");
            sb.Append("ST: ");
            sb.AppendLine(strSearchTarget);
            sb.Append("USER-AGENT: ");
            sb.AppendLine(strUserAgent);
            sb.AppendLine();

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        private static SsdpServerInfo parseResponse(string strHeader, Dictionary<string, string> fields)
        {
            SsdpServerInfo result = null;
            SsdpServerInfoStatusEnum status;
            if (strHeader.StartsWith("HTTP/1") && strHeader.EndsWith(" 200 OK")) //HTTP/1.1 200 OK
            {
                if (!fields.TryGetValue("ST", out _))
                    return null;

                status = SsdpServerInfoStatusEnum.Alive;
            }
            else if (strHeader.StartsWith("NOTIFY * HTTP/1.")) //NOTIFY * HTTP/1.1
            {
                if (fields.TryGetValue("NT", out _) && fields.TryGetValue("NTS", out string strNts))
                {
                    switch (strNts)
                    {
                        case "ssdp:alive":
                            status = SsdpServerInfoStatusEnum.Alive;
                            break;

                        case "ssdp:update":
                            status = SsdpServerInfoStatusEnum.Updated;
                            break;

                        case "ssdp:byebye":
                            status = SsdpServerInfoStatusEnum.Dead;
                            break;

                        default:
                            return null;
                    }
                }
                else
                    return null;
            }
            else
                return null;

            if (fields.TryGetValue("LOCATION", out string strLocation) &&
                fields.TryGetValue("USN", out string strUSN) &&
                fields.TryGetValue("SERVER", out string strServer) &&
                fields.TryGetValue("CACHE-CONTROL", out string strTmp) && strTmp.StartsWith("max-age=") && int.TryParse(strTmp.Substring(8), out int iMaxAge))
            {
                int iCfgId = -1;
                int iBootId = -1;

                if (strServer.Contains("UPnP/1.1"))
                {
                    if (!fields.TryGetValue("BOOTID.UPNP.ORG", out strTmp) || !int.TryParse(strTmp, out iBootId) ||
                        !fields.TryGetValue("CONFIGID.UPNP.ORG", out strTmp) || !int.TryParse(strTmp, out iCfgId))
                        return null;
                }


                //Get UUID first
                //uuid:<device-UUID>::urn:<domain-name>:device:<deviceType>:<ver>
                //uuid:<device-UUID>::urn:<domain-name>:service:<serviceType>:<ver>
                int iIdxStart = strUSN.IndexOf("uuid:");
                if (iIdxStart >= 0)
                {
                    iIdxStart += 5;
                    int iIdxEnd = strUSN.IndexOf(':', iIdxStart);
                    if (iIdxEnd > 0)
                    {
                        string strUUID = strUSN.Substring(iIdxStart, iIdxEnd - iIdxStart);
                        iIdxStart = strUSN.IndexOf("::urn:", iIdxEnd);
                        if (iIdxStart > 0)
                        {
                            string strDevType = strUSN.Substring(iIdxStart + 2);
                            if (!string.IsNullOrWhiteSpace(strDevType))
                            {
                                //New device detected
                                result = new SsdpServerInfo(strDevType, strLocation, strUSN, strUUID,
                                    strServer, iCfgId, iBootId, iMaxAge, fields);
                            }
                        }
                    }
                }
            }

            if (result != null)
                result.Status = status;

            return result;
        }

        private SsdpServerInfo serverInfoAddOrUpdate(SsdpJob job, SsdpServerInfo serverInfo)
        {
            SsdpEventArgsAttribute e;
            for (int iIdx = 0; iIdx < job.ServerInfo.Count; iIdx++)
            {
                SsdpServerInfo si = job.ServerInfo[iIdx];
                if (si.IsMatch(serverInfo))
                {
                    //Update existing
                    int iRes = job.ServerInfo[iIdx].UpdateFrom(serverInfo);

                    if (iRes < 0)
                        return null;

                    _Logger.Debug("[serverInfoAddOrUpdate] ServerInfo {0}: USN: '{1}'  Expires in: {2}s",
                        iRes > 0 ? "update" : "refresh", serverInfo.USN, (long)serverInfo.ExpiresIn.TotalSeconds);

                    e = new SsdpEventArgsAttribute(iRes > 0 ? SsdpEventTypeEnum.ServerInfoUpdated : SsdpEventTypeEnum.ServerInfoRefreshed, si);
                    job.CallbackInvoke(e);
                    try { this.Event?.Invoke(this, e); }
                    catch { }

                    return si;
                }
            }

            //New
            if (serverInfo.Parsed || serverInfo.LoadDescription())
            {
                job.ServerInfo.Add(serverInfo);

                _Logger.Debug("[servereInfoAddOrUpdate] ServerInfo created: USN: '{0}'  Expires in: {1}s",
                    serverInfo.USN, (long)serverInfo.ExpiresIn.TotalSeconds);
            }
            else
                return null;

            e = new SsdpEventArgsAttribute(SsdpEventTypeEnum.ServerInfoCreated, serverInfo);
            job.CallbackInvoke(e);
            try { this.Event?.Invoke(this, e); }
            catch { }

            return serverInfo;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void initRefreshTimer()
        {
            TimeSpan tsExp = this._SsdpJobs.Values.Min(j => j.NearestExpiration);
            if (tsExp != TimeSpan.MaxValue)
            {
                this._TimerRefresh.Interval = Math.Max(2000, tsExp.TotalMilliseconds - 10000);
                this._TimerRefresh.Start();

                _Logger.Debug("[initRefreshTimer] Next search in: {0}s", (int)(this._TimerRefresh.Interval / 1000));
            }
        }
        #endregion

        #region Callbacks
        private void cbSocketReceive(IAsyncResult ar)
        {
            SsdpSocket ssdp = (SsdpSocket)ar.AsyncState;
            bool bLocked = false;
            try
            {
                try
                {
                    Monitor.Enter(this, ref bLocked);

                    if (this._Sockets == null || ssdp.IsClosing)
                        return;

                    int iLength = ssdp.ListenerSocket.EndReceiveFrom(ar, ref ssdp.RemoteEp);
                    if (iLength <= 0)
                        return;

                    if (iLength == ssdp.Buffer.Length)
                    {
                        _Logger.Debug("[cbSocketReceive] Buffer full.");
                        return;
                    }

                    //Accept NOTIFY only
                    if (iLength > 6 && ssdp.Buffer[0] == 'N' && ssdp.Buffer[1] == 'O' && ssdp.Buffer[2] == 'T')
                    {
                        string strContent = Encoding.ASCII.GetString(ssdp.Buffer, 0, iLength);
                        _Logger.Debug("[cbSocketReceive] Response received from: {0}  Length:{1}\r\n{2}", ssdp.RemoteEp, iLength, strContent);
                        string[] lines = strContent.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                        if (lines.Length > 0)
                        {
                            Dictionary<string, string> httpFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            char[] split = new char[] { ':' };
                            for (int i = 1; i < lines.Length; i++)
                            {
                                string[] keyValues = lines[i].Split(split, 2);
                                if (keyValues.Length == 2)
                                    httpFields[keyValues[0].Trim()] = keyValues[1].Trim();
                            }

                            SsdpServerInfo result = parseResponse(lines[0], httpFields);
                            if (result != null && this._SsdpJobs.TryGetValue(httpFields["NT"], out SsdpJob job))
                            {
                                bLocked = false;
                                Monitor.Exit(this);
                                lock (job)
                                {
                                    if (result.Status < SsdpServerInfoStatusEnum.Alive)
                                    {
                                        //Remove no longer valid info
                                        SsdpServerInfo si = job.ServerInfo.Find(s => s.IsMatch(result));
                                        if (si != null)
                                        {
                                            si.Status = SsdpServerInfoStatusEnum.Dead;
                                            job.ServerInfo.Remove(si);
                                            _Logger.Debug("[cbSocketReceive] ServerInfo removed: '{0}'", si.USN);
                                            SsdpEventArgsAttribute e = new SsdpEventArgsAttribute(SsdpEventTypeEnum.ServerInfoRemoved, si);
                                            job.CallbackInvoke(e);
                                            try { this.Event?.Invoke(this, e); }
                                            catch { }
                                        }
                                        return;
                                    }

                                    this.serverInfoAddOrUpdate(job, result);
                                }
                                this.initRefreshTimer();
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Error("[cbSocketReceive] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                }
                finally
                {
                    if (!bLocked)
                        Monitor.Enter(this, ref bLocked);

                    if (!ssdp.IsClosing)
                        ssdp.ListenerSocket.BeginReceiveFrom(ssdp.Buffer, 0, ssdp.Buffer.Length, SocketFlags.None, ref ssdp.RemoteEp, this.cbSocketReceive, ssdp);
                    else
                        _Logger.Debug("[cbSocketReceive] Socket closed: {0}", ssdp.LocalEndpoint);
                }
            }
            finally
            {
                if (bLocked)
                    Monitor.Exit(this);
            }

        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void cbTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _Logger.Debug("[cbTimerElapsed]");
            foreach (SsdpJob job in this._SsdpJobs.Values.Where(j => j.NearestExpiration.TotalSeconds < 30))
                this.search(job, false);

            this.initRefreshTimer();
        }
        #endregion
    }
}

