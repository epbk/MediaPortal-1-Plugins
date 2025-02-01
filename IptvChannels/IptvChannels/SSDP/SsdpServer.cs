using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.IptvChannels.SSDP
{
    public class SsdpServer
    {
        #region Types
        private class SsdpSocket
        {
            public Socket ListenerSocket;
            public Socket NotifySocket;
            public IPAddress Address;
            public byte[] Buffer = new byte[1024];
            public EndPoint RemoteEp = new IPEndPoint(IPAddress.Any, 0);
        }
        #endregion

        #region Private fields
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        private readonly UpnpDevice[] _UpnpDevices;
        private Timer[] _NnotifyTimers;
        private SsdpSocket[] _Sockets;
        private Timer _DelayedNetworkChangeTimer;

        private IPAddress[] _HostAddresses = new IPAddress[0];

        private readonly Random _Rnd = new Random();
        private readonly int _MaxAge = 1800;
        #endregion

        #region ctor
        public SsdpServer(UpnpDevice[] upnpDevices)
        {
            this._UpnpDevices = upnpDevices;

            NetworkChange.NetworkAddressChanged += new NetworkAddressChangedEventHandler(this.cbNetworkAddressChanged);
        }
        #endregion

        #region Public methods
        public void Start()
        {
            this.Start(new IPAddress[0]);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(IPAddress[] hostAddresses)
        {
            if (this._Sockets == null)
            {
                this._HostAddresses = hostAddresses;

                this._DelayedNetworkChangeTimer = new Timer(new TimerCallback(this.cbNetworkChangTimeout), null, Timeout.Infinite, Timeout.Infinite);

                this._Sockets = this.getSockets();

                this._NnotifyTimers = new Timer[this._Sockets.Length];

                for (int i = 0; i < this._Sockets.Length; i++)
                {
                    SsdpSocket socket = this._Sockets[i];
                    socket.ListenerSocket.BeginReceiveFrom(socket.Buffer, 0, socket.Buffer.Length, SocketFlags.None, ref socket.RemoteEp, this.cbReceive, socket);
                    this._NnotifyTimers[i] = new Timer(new TimerCallback(this.cbNotifyTimeout), socket, 1000, this._MaxAge * 450);
                    if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[Start] SSDP server started on {0}", socket.Address);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (this._Sockets != null)
            {
                this._DelayedNetworkChangeTimer.Dispose();
                foreach (Timer timer in this._NnotifyTimers)
                    timer.Dispose();

                foreach (SsdpSocket socket in this._Sockets)
                    socket.ListenerSocket.Close();

                this._Sockets = null;
                this._NnotifyTimers = null;
                this._DelayedNetworkChangeTimer = null;
            }
        }
        #endregion

        #region Callbacks
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void cbNetworkAddressChanged(object sender, EventArgs e)
        {
            if (this._Sockets == null)
                return;

            this._DelayedNetworkChangeTimer.Change(5000, Timeout.Infinite);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void cbNetworkChangTimeout(object socketObj)
        {
            if (this._Sockets == null)
                return;

            if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[cbNetworkChangTimeout] SSDP server will be restarted due to change of IP address");

            try
            {
                this.Stop();
                this.Start();
            }
            catch (Exception ex)
            {
                _Logger.Error("[cbNetworkChangTimeout] SSDP server could not be restarted: " + ex.Message);
            }
        }

        private void cbNotifyTimeout(object socketObj)
        {
            SsdpSocket socket = (SsdpSocket)socketObj;
            this.sendNotify(socket.NotifySocket, socket.Address.ToString(), true);
        }

        private void cbReceive(IAsyncResult ar)
        {
            SsdpSocket socket = (SsdpSocket)ar.AsyncState;
            int iLength = -1;
            try
            {
                iLength = socket.ListenerSocket.EndReceiveFrom(ar, ref socket.RemoteEp);

                string[] lines = Encoding.ASCII.GetString(socket.Buffer, 0, iLength).Split(new string[] { "\r\n" },
                    StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length > 0 && lines[0] == "M-SEARCH * HTTP/1.1")
                {
                    if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[cbReceive] Received:\r\n{0}", lines[0]);

                    Dictionary<string, string> httpFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 1; i < lines.Length; i++)
                    {
                        string[] keyValues = lines[i].Split(new char[] { ':' }, 2);
                        if (keyValues.Length == 2)
                            httpFields[keyValues[0].Trim()] = keyValues[1].Trim();
                    }

                    //it defines the scope (namespace) of the extension. MUST be "ssdp:discover".
                    if (!httpFields.TryGetValue("MAN", out string str) || !str.Equals("\"ssdp:discover\"", StringComparison.OrdinalIgnoreCase))
                        return;

                    //seconds to delay response
                    //Field value contains maximum wait time in seconds. MUST be greater than or equal to 1 and SHOULD be less than 5 inclusive.
                    if (!httpFields.TryGetValue("MX", out str) || !int.TryParse(str, out int iMx) || iMx < 0)
                        return;

                    //Search Target
                    if (!httpFields.TryGetValue("ST", out string strSt))
                        return;

                    for (int i = 0; i < this._UpnpDevices.Length; i++)
                    {
                        UpnpDevice dev = this._UpnpDevices[i];
                        if (StringComparer.OrdinalIgnoreCase.Compare(strSt, dev.DeviceType) == 0)
                        {
                            Thread.Sleep(this._Rnd.Next(iMx * 500));
                            this.sendResponseMessage(dev, socket.ListenerSocket, socket.RemoteEp, socket.Address.ToString(), strSt, dev.Udn.ToString());
                        }
                    }
                }
            }
            catch
            {

            }
            finally
            {
                if (iLength > 0)
                    socket.ListenerSocket.BeginReceiveFrom(socket.Buffer, 0, socket.Buffer.Length, SocketFlags.None, ref socket.RemoteEp, this.cbReceive, socket);
                else
                {
                    this.sendNotify(socket.NotifySocket, socket.Address.ToString(), false);
                    socket.NotifySocket.Close();
                    if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[cbReceive] SSDP server stopped on {0}", socket.Address);
                }
            }
        }
        #endregion

        #region Private methods
        private SsdpSocket[] getSockets()
        {
            IEnumerable<IPAddress> addresses;

            if (this._HostAddresses.Length <= 0)
                addresses = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork);
            else
                addresses = this._HostAddresses;

            return addresses.Select(delegate (IPAddress address)
            {
                Socket listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                listenerSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
                listenerSocket.Bind(new IPEndPoint(address, 1900));
                listenerSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse("239.255.255.250"), address));
                Socket notifySocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                notifySocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                notifySocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                notifySocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
                notifySocket.Bind(new IPEndPoint(address, 1900));
                return new SsdpSocket() { ListenerSocket = listenerSocket, NotifySocket = notifySocket, Address = address };

            }).ToArray();
        }

        private void sendNotify(Socket socket, string strHost, bool bIsAlive)
        {
            for (int i = 0; i < this._UpnpDevices.Length; i++)
            {
                UpnpDevice dev = this._UpnpDevices[i];
                this.sendNotifyMessage(dev, socket, strHost, dev.DeviceType, dev.Udn.ToString(), bIsAlive);
            }
        }

        private void sendNotifyMessage(UpnpDevice dev, Socket socket, string strHost, string strNt, string strUsn, bool bIsAlive)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.AppendLine("NOTIFY * HTTP/1.1");

            //REQUIRED. Field value contains multicast address and port reserved for SSDP by Internet Assigned Numbers Authority (IANA). MUST be 239.255.255.250:1900.
            sb.AppendLine("HOST: 239.255.255.250:1900");

            //REQUIRED. Field value MUST have the max-age directive (“max-age=”) followed by an integer that specifies the number of seconds the advertisement is valid.
            sb.Append("CACHE-CONTROL: max-age=");
            sb.Append(this._MaxAge);
            sb.AppendLine();

            //REQUIRED. Field value contains a URL to the UPnP description of the root device.
            sb.Append("LOCATION: http://");
            sb.Append(strHost);
            sb.Append(':');
            sb.Append(dev.ServerPort);
            sb.AppendLine("/description.xml");

            //REQUIRED. Field value contains Notification Type.
            sb.Append("NT: ");
            if (strNt == strUsn)
                sb.Append("uuid: ");
            sb.AppendLine(strNt);

            //REQUIRED.Field value contains Notification Sub Type.
            sb.Append("NTS: ssdp:");
            sb.AppendLine(bIsAlive ? "alive" : "byebye");

            //REQUIRED. Specified by UPnP vendor.
            sb.Append("SERVER: WindowsNT/");
            sb.Append(Environment.OSVersion.Version.Major);
            sb.Append('.');
            sb.Append(Environment.OSVersion.Version.Minor);
            sb.Append(" UPnP/1.1 ");
            sb.Append(dev.FriendlyName);
            sb.Append('/');
            sb.AppendLine(dev.ModelNumber);

            //REQUIRED. Field value contains Unique Service Name.
            sb.Append("USN: uuid:");
            sb.Append(strUsn);
            if (strNt != strUsn)
            {
                sb.Append("::");
                sb.Append(strNt);
            }
            sb.AppendLine();

            //REQUIRED.The BOOTID.UPNP.ORG header field represents the boot instance of the device expressed according to a monotonically increasing value.
            sb.Append("BOOTID.UPNP.ORG: ");
            sb.Append(dev.BootID);
            sb.AppendLine();

            //REQUIRED. The CONFIGID.UPNP.ORG field value MUST be a non-negative, 31-bit integer, ASCII encoded, decimal, without leading zeros (leading zeroes,
            //if present, MUST be ignored by the recipient) that MUST represent the configuration number of a root device.
            sb.Append("CONFIGID.UPNP.ORG: ");
            sb.Append(dev.ConfigID);
            sb.AppendLine();

            if (dev.AdditionalSsdpNotify != null)
                sb.AppendLine(dev.AdditionalSsdpNotify);

            sb.AppendLine();

            try { socket.SendTo(Encoding.ASCII.GetBytes(sb.ToString()), new IPEndPoint(IPAddress.Broadcast, 1900)); }
            catch { }

            Thread.Sleep(100);
        }

        private void sendResponseMessage(UpnpDevice dev, Socket socket, EndPoint receivePoint, string strHost, string strSt, string strUsn)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.AppendLine("HTTP/1.1 200 OK");

            //REQUIRED. Field value MUST have the max-age directive (“max-age=”) followed by an integer that specifies the number of seconds the advertisement is valid.
            sb.Append("CACHE-CONTROL: max-age=");
            sb.Append(this._MaxAge);
            sb.AppendLine();

            //RECOMMENDED. Field value contains date when response was generated.
            sb.Append("DATE: ");
            sb.AppendLine(DateTime.UtcNow.ToString("r"));

            //REQUIRED for backwards compatibility with UPnP 1.0. (Header field name only; no field value.)
            sb.AppendLine("EXT:");

            //REQUIRED. Field value contains a URL to the UPnP description of the root device.
            sb.Append("LOCATION: http://");
            sb.Append(strHost);
            sb.Append(':');
            sb.Append(dev.ServerPort);
            sb.AppendLine("/description.xml");

            //REQUIRED. Specified by UPnP vendor.
            sb.Append("SERVER: WindowsNT/");
            sb.Append(Environment.OSVersion.Version.Major);
            sb.Append('.');
            sb.Append(Environment.OSVersion.Version.Minor);
            sb.Append(" UPnP/1.1 ");
            sb.Append(dev.FriendlyName);
            sb.Append('/');
            sb.AppendLine(dev.ModelNumber);

            //REQUIRED. Field value contains Search Target.
            sb.Append("ST: ");
            sb.AppendLine(strSt);

            //REQUIRED. Field value contains Unique Service Name.
            sb.Append("USN: uuid:");
            sb.Append(strUsn);
            if (strSt != strUsn)
            {
                sb.Append("::");
                sb.Append(strSt);
            }
            sb.AppendLine();

            //REQUIRED.
            sb.Append("BOOTID.UPNP.ORG: ");
            sb.Append(dev.BootID);
            sb.AppendLine();

            //OPTIONAL. The CONFIGID.UPNP.ORG field value MUST be a non-negative, 31-bit integer, ASCII encoded, decimal, without leading zeros (leading zeroes,
            //if present, MUST be ignored by the recipient) that MUST represent the configuration number of a root device.
            sb.Append("CONFIGID.UPNP.ORG: ");
            sb.Append(dev.ConfigID);
            sb.AppendLine();

            if (dev.AdditionalSsdpNotify != null)
                sb.AppendLine(dev.AdditionalSsdpNotify);

            sb.AppendLine();

            try { socket.SendTo(Encoding.ASCII.GetBytes(sb.ToString()), receivePoint); }
            catch { }
        }
        #endregion
    }
}
