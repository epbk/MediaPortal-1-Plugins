using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NLog;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.IptvChannels.Proxy
{
    public class Client : IClient
    {
        public const int TS_BLOCK_SIZE = 188;

        #region Private Fields


        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        private Socket _Socket = null;
        private Stream _RemoteStream = null;
        //private IPEndPoint _RemoteEndpoint;

        private SendHandler _ClientDataCallback;

        private AsyncCallback _ReceiveCallback;

        private byte[] _Buffer = new byte[TS_BLOCK_SIZE * 40];

        #endregion

        public ulong DataSent
        {
            get
            {

                return this._DataSent;
            }
        }private ulong _DataSent = 0;

        public DateTime DataSentFirstTS
        {
            get
            {

                return this._DataSentFirstTS;
            }
        }private DateTime _DataSentFirstTS = DateTime.MinValue;

        public ulong PacketErrors
        {
            get
            {

                return 0;
            }
        }

        public int Port
        {
            get
            {
                Socket s = this._Socket;
                return s != null ? ((IPEndPoint)s.LocalEndPoint).Port : 0;
            }
        }

        #region ctor
        public Client(string strUrl, SendHandler clientDataCallback, int iPort)
        {
            this._ClientDataCallback = clientDataCallback;
            this._ReceiveCallback = new AsyncCallback(this.receive);

            try
            {
                int iUrlPort = 0;

                if (!string.IsNullOrWhiteSpace(strUrl))
                {
                    Regex regex = new Regex("udp://(?<ip>[0-9,\\.]+):(?<port>[0-9]+)");

                    Match m = regex.Match(strUrl);

                    if (m.Success)
                        iUrlPort = int.Parse(m.Groups["port"].Value);
                }

                this._Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                this._Socket.ReceiveBufferSize = 1024 * 256;
                this._Socket.Bind(new IPEndPoint(IPAddress.Any, iPort >= 0 ? iPort : iUrlPort));
            }
            catch (Exception ex)
            {
                _Logger.Error("[ctor] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }

        public Client(Stream remoteStream, SendHandler clientDataCallback)
        {
            if (remoteStream != null)
                this._RemoteStream = remoteStream;
        }

        public Client(string strUrl, SendHandler clientDataCallback)
        {
            this._ClientDataCallback = clientDataCallback;
            this._ReceiveCallback = new AsyncCallback(this.receive);

            try
            {
                Pbk.Net.Http.HttpUserWebRequest rq = new Pbk.Net.Http.HttpUserWebRequest(strUrl);
                this._RemoteStream = rq.GetResponseStream();
                if (rq.HttpResponseCode != HttpStatusCode.OK)
                    this._RemoteStream = null;
            }
            catch (Exception ex)
            {
                _Logger.Error("[ctor] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                this._RemoteStream = null;
            }
        }

        #endregion

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Connect()
        {
            if (this._Socket != null)
            {
                this._Socket.BeginReceive(this._Buffer, 0, this._Buffer.Length, SocketFlags.None, this._ReceiveCallback, null);

                _Logger.Debug("[Connect] Socket: begin receive...");

                return true;
            }
            else if (this._RemoteStream != null)
            {
                _Logger.Debug("[Connect] Stream: begin receive...");

                this._RemoteStream.BeginRead(this._Buffer, 0, this._Buffer.Length, this._ReceiveCallback, null);

                return true;
            }
            else
                return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            if (this._Socket != null)
            {
                this._Socket.Close();
                this._Socket = null;
            }

            if (this._RemoteStream != null)
            {
                this._RemoteStream.Close();
                this._RemoteStream = null;
            }
        }

        private void receive(IAsyncResult ia)
        {
            try
            {
                int iLength = this._Socket != null ? this._Socket.EndReceive(ia) : this._RemoteStream.EndRead(ia);

                if (iLength > 0)
                {
                    if (this._DataSent == 0)
                        this._DataSentFirstTS = DateTime.Now;

                    this._DataSent += (uint)iLength;

                    this._ClientDataCallback(this._Buffer, 0, iLength);

                    if (this._Socket != null)
                        this._Socket.BeginReceive(this._Buffer, 0, this._Buffer.Length, SocketFlags.None, this._ReceiveCallback, null);
                    else
                        this._RemoteStream.BeginRead(this._Buffer, 0, this._Buffer.Length, this._ReceiveCallback, null);

                    return;
                }
            }
            catch { }

            _Logger.Debug("[receive] Socket closed.");
        }
    }
}
