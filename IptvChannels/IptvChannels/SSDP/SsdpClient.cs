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
        }
        #endregion

        #region Constants
        private const string SSDP_BROADCAST_ADDRESS = "239.255.255.250";
        private const int SSDP_BROADCAST_ADDRESS_PORT = 1900;
        #endregion

        #region Private fields
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        private string _SearchTarget;
        private byte[] _SearchMessage;
        private List<SsdpSocket> _Sockets;
        private SsdpServerInfo _ServerInfo = null;
        #endregion

        #region Properties
        public SsdpServerInfo ServerInfo
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                if (this._Sockets != null && (this._ServerInfo == null || this._ServerInfo.ExpiresIn.TotalSeconds < 10))
                {
                    this._ServerInfo = null;
                    this._Sockets.ForEach(s => search(s.ListenerSocket, this._SearchMessage));
                    if (!Monitor.Wait(this, 5000))
                        _Logger.Error("[ServerInfo] No response received in 5s.");
                }

                return this._ServerInfo;
            }

            private set
            {
                this._ServerInfo = value;
            }
        }
        #endregion

        #region ctor
        public SsdpClient(string strSearchTarget, string strUserAgent)
        {
            this._SearchTarget = strSearchTarget;
            this._SearchMessage = buildSearchMessage(strSearchTarget, strUserAgent);
        }
        #endregion

        #region Public methods

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            if (this._Sockets == null)
            {
                _Logger.Debug("[Start]");
                this._Sockets = new List<SsdpSocket>();
                foreach (IPAddress ip in Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork))
                {
                    SsdpSocket ssdp = new SsdpSocket();
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, ip.GetAddressBytes());
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
                    ssdp.ListenerSocket = socket;
                    search(socket, this._SearchMessage);
                    socket.BeginReceiveFrom(ssdp.Buffer, 0, ssdp.Buffer.Length, SocketFlags.None, ref ssdp.RemoteEp, this.cbReceive, ssdp);

                    this._Sockets.Add(ssdp);
                }
            }
        }

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
                this._Sockets = null;
            }
        }

        public static List<SsdpServerInfo> Discover(string strSearchTarget, string strUserAgent)
        {
            try
            {
                //We need to send M-SEARCH message from all interfaces to make sure local apps receive the message.

                List<SsdpServerInfo> result = new List<SsdpServerInfo>();
                IEnumerable<IPAddress> addresses = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork);
                ManualResetEvent flagDone = new ManualResetEvent(false);
                int iThreadsCounter = addresses.Count();

                byte[] searchMessage = buildSearchMessage(strSearchTarget, strUserAgent);

                foreach (IPAddress ip in addresses)
                {
                    ThreadPool.QueueUserWorkItem(new WaitCallback((adr) =>
                    {
                        List<SsdpServerInfo> res = discover((IPAddress)adr, searchMessage, strSearchTarget);
                        lock (result)
                        {
                            if (res != null && res.Count > 0)
                            {
                                //Merge result
                                res.ForEach(inf =>
                                {
                                    if (!result.Exists(p => p.Location == inf.Location || p.UUID == inf.UUID))
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

                            sb.Append("Server: ").AppendLine(srv.Server);
                            sb.Append("Location: ").AppendLine(srv.Location);
                            sb.Append("MaxAge: ").Append(srv.MaxAge).AppendLine();
                            sb.Append("FriendlyName: ").AppendLine(srv.FriendlyName);
                            sb.Append("DeviceType: ").AppendLine(srv.DeviceType);
                            sb.Append("DeviceID: ").Append(srv.DeviceID).AppendLine();
                            sb.Append("UDN: ").AppendLine(srv.UDN);
                            sb.Append("USN: ").AppendLine(srv.USN);
                            sb.Append("UUID: ").AppendLine(srv.UUID);
                            sb.Append("Version: ").Append(srv.SpecVersionMajor).Append('.').Append(srv.SpecVersionMinor).AppendLine();
                            sb.Append("Manufacturer: ").AppendLine(srv.Manufacturer);
                            sb.Append("ManufacturerURL: ").AppendLine(srv.ManufacturerUrl);
                            sb.Append("ModelDescription: ").AppendLine(srv.ModelDescription);
                            sb.Append("ModelName: ").AppendLine(srv.ModelName);
                            sb.Append("ModelNumber: ").AppendLine(srv.ModelNumber);
                            sb.Append("ModelURL: ").AppendLine(srv.ModelUrl);
                            sb.Append("SerialNumber: ").AppendLine(srv.SerialNumber);
                            sb.Append("Capabilities: ").AppendLine(srv.Capabilities);
                            sb.Append("PresentationURL: ").AppendLine(srv.PresentationUrl);
                            sb.Append("ChannelListURL: ").AppendLine(srv.ChannelListUrl);
                            sb.Append("UPC: ").AppendLine(srv.UPC);
                            if (srv.RtspPort > 0)
                            {
                                sb.Append("RtspPort: ");
                                sb.Append(srv.RtspPort);
                                sb.AppendLine();
                            }
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
        #endregion

        #region Private methods
        private static List<SsdpServerInfo> discover(IPAddress ip, byte[] searchMessage, string strSearchTarget)
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
                //socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

                //socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                //    new MulticastOption(IPAddress.Parse("239.255.255.250"), ip3));

                search(socket, searchMessage);

                byte[] buffer = new byte[1024];
                int iReceived = 0;

                result = new List<SsdpServerInfo>();
                EndPoint epRemote = new IPEndPoint(IPAddress.Any, 0);

                while (true)
                {
                    int iLength = 0;
                    try
                    {
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
                            result.AddRange(parseResponse(fields, strSearchTarget));
                        }
                    }
                    else
                        break; //timeout

                    iReceived = 0;
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

        private static void search(Socket socket, byte[] searchMessage)
        {
            IPEndPoint epMulticast = new IPEndPoint(IPAddress.Parse(SSDP_BROADCAST_ADDRESS), SSDP_BROADCAST_ADDRESS_PORT);

            //Send three packets within interval 100ms
            socket.SendTo(searchMessage, epMulticast);
            Thread.Sleep(100);
            socket.SendTo(searchMessage, epMulticast);
            Thread.Sleep(100);
            socket.SendTo(searchMessage, epMulticast);
        }

        private static List<SsdpServerInfo> parseResponse(Dictionary<string, string> fields, string strSearchTarget)
        {
            List<SsdpServerInfo> result = new List<SsdpServerInfo>();
            if (fields.TryGetValue("ST", out string strTmp) && strTmp == strSearchTarget &&
                fields.TryGetValue("LOCATION", out string strLocation) &&
                fields.TryGetValue("USN", out string strUSN) &&
                fields.TryGetValue("SERVER", out string strServer) &&
                fields.TryGetValue("BOOTID.UPNP.ORG", out strTmp) &&
                fields.TryGetValue("CONFIGID.UPNP.ORG", out strTmp) && int.TryParse(strTmp, out int iCfgId) &&
                fields.TryGetValue("CACHE-CONTROL", out strTmp) && strTmp.StartsWith("max-age=") && int.TryParse(strTmp.Substring(8), out int iMaxAge)
                )
            {
                int iDevId;

                if (fields.TryGetValue("DEVICEID.SES.COM", out strTmp))
                    int.TryParse(strTmp, out iDevId);
                else
                    iDevId = -1;


                //Get UUID first
                int iIdxStart = strUSN.IndexOf("uuid:");
                if (iIdxStart >= 0)
                {
                    iIdxStart += 5;
                    int iIdxEnd = strUSN.IndexOf(':', iIdxStart);

                    if (iIdxEnd >= 0)
                        strTmp = strUSN.Substring(iIdxStart, iIdxEnd - iIdxStart);
                    else
                        strUSN.Substring(iIdxStart);

                    if (!result.Exists(p => p.Location == strLocation || p.UUID == strTmp))
                    {
                        //New device detected
                        result.Add(new SsdpServerInfo()
                        {
                            Location = strLocation,
                            USN = strUSN,
                            UUID = strTmp,
                            Server = strServer,
                            ConfigID = iCfgId,
                            DeviceID = iDevId,
                            MaxAge = iMaxAge,
                            RefreshTimeStamp = DateTime.Now
                        }
                        );
                    }
                }
            }

            //Get description for each server
            result.ForEach(srv =>
            {
                try
                {
                    using (HttpUserWebRequest rq = new HttpUserWebRequest(srv.Location))
                    {
                        XmlDocument xml = new XmlDocument();
                        xml.LoadXml(rq.Download<string>());
                        srv.ParseDescription(xml);
                        if (!rq.HttpResponseFields.TryGetValue("X-SATIP-RTSP-Port", out string str) || !int.TryParse(str, out srv.RtspPort))
                            srv.RtspPort = -1;
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Error("[parseResponse] Error getting description: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                }
            });

            return result;
        }
        #endregion

        #region Callbacks
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void cbReceive(IAsyncResult ar)
        {
            SsdpSocket ssdp = (SsdpSocket)ar.AsyncState;
            int iReceived = -1;
            try
            {
                if (ssdp.IsClosing)
                    return;

                int iLength = ssdp.ListenerSocket.EndReceiveFrom(ar, ref ssdp.RemoteEp);

                if (iLength <= 0)
                    return;

                iReceived = 0;

                while (!ssdp.IsClosing && iLength > 0)
                {
                    iReceived += iLength;

                    if (iReceived == ssdp.Buffer.Length)
                    {
                        _Logger.Debug("[cbReceive] Buffer full.");
                        return;
                    }

                    Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    CookieContainer cookies = null;
                    int iResponseLength = HttpUserWebRequest.GetHttpResponse(null, ssdp.Buffer, 0, iReceived, ref fields, ref cookies, out HttpStatusCode httpResponseCode, out _);
                    if (iResponseLength > 0 && httpResponseCode == HttpStatusCode.OK)
                    {
                        _Logger.Debug("[cbReceive] Response received from: {0}\r\n{1}", ssdp.RemoteEp, Encoding.ASCII.GetString(ssdp.Buffer, 0, iResponseLength));
                        List<SsdpServerInfo> result = parseResponse(fields, this._SearchTarget);
                        if (result.Count > 0)
                        {
                            this.ServerInfo = result[0];
                            Monitor.PulseAll(this);
                        }

                        if (iResponseLength == iReceived)
                            return;

                        iLength = iReceived - iResponseLength;
                        iReceived = iResponseLength;
                        continue;
                    }

                    //Next receive
                    IAsyncResult ia = ssdp.ListenerSocket.BeginReceiveFrom(ssdp.Buffer, iReceived, ssdp.Buffer.Length - iReceived, SocketFlags.None, ref ssdp.RemoteEp, null, null);
                    if (ia.AsyncWaitHandle.WaitOne(2000))
                        iLength = ssdp.ListenerSocket.EndReceiveFrom(ia, ref ssdp.RemoteEp);
                    else
                        break; //timeout
                }
            }
            catch { }
            finally
            {
                if (!ssdp.IsClosing && iReceived >= 0)
                    ssdp.ListenerSocket.BeginReceiveFrom(ssdp.Buffer, 0, ssdp.Buffer.Length, SocketFlags.None, ref ssdp.RemoteEp, this.cbReceive, ssdp);
                else
                    _Logger.Debug("[cbReceive] Socket closed: {0}", ssdp.RemoteEp);
            }

        }
        #endregion
    }
}

