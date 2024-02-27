using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.CompilerServices;
using NLog;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserServer
    {
        private class Buffering
        {
            private const int BUFFER_SIZE = 8 * 1024;
            private List<byte[]> _Buffers = new List<byte[]>();
            private int _CurrentBufferBytesWrite = 0;
            private byte[] _CurrentBuffer = new byte[BUFFER_SIZE];
            private bool _Done = false;

            public int TotalBytesWrite
            {
                get
                {
                    return this._TotalBytesWrite;
                }
            }private int _TotalBytesWrite = 0;

            public bool SaveData(byte[] buffer, int iOffset, int iLength)
            {
                if (this._Done) throw new Exception("Buffering is closed.");

                if (iLength < 1) return false;

                int iCount = iLength;

                while (iCount > 0)
                {
                    int iSizeToCopy = Math.Min(BUFFER_SIZE - this._CurrentBufferBytesWrite, iCount);

                    Buffer.BlockCopy(buffer, iOffset, this._CurrentBuffer, this._CurrentBufferBytesWrite, iSizeToCopy);
                    this._CurrentBufferBytesWrite += iSizeToCopy;
                    iCount -= iSizeToCopy;
                    iOffset += iSizeToCopy;

                    if (this._CurrentBufferBytesWrite == BUFFER_SIZE)
                    {
                        //Buffer full; put the buffer into the list and prepare new one
                        this._Buffers.Add(this._CurrentBuffer);
                        this._CurrentBufferBytesWrite = 0;
                        this._CurrentBuffer = new byte[BUFFER_SIZE];
                    }
                }

                this._TotalBytesWrite += iLength;

                return true;
            }

            public byte[] BuildData()
            {
                if (this._Done) throw new Exception("Buffering is closed.");

                byte[] bufferResult = null;

                if (this._TotalBytesWrite > 0)
                {
                    //Create output data buffer
                    bufferResult = new byte[this._TotalBytesWrite];
                    int iPos = 0;
                    if (this._CurrentBufferBytesWrite > 0) this._Buffers.Add(this._CurrentBuffer); //add last buffer
                    foreach (byte[] buff in this._Buffers)
                    {
                        int iLength = this._TotalBytesWrite - iPos;
                        if (iLength == 0) break;
                        else if (iLength > buff.Length) iLength = buff.Length;
                        Buffer.BlockCopy(buff, 0, bufferResult, iPos, iLength);
                        iPos += iLength;
                    }
                }
                this._Buffers.Clear();
                this._Buffers = null;
                this._CurrentBuffer = null;
                this._Done = true;

                return bufferResult;
            }
        }

        private class Connection
        {
            public Socket Socket;
            public Thread Thread;
        }

        public const string HTTP_TEMPLATE_REDIRECT_302 = 
                            "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\r\n" +
                            "<html><head>\r\n" +
                            "<title>302 Found</title>\r\n" +
                            "</head><body>\r\n" +
                            "<h1>Found</h1>\r\n" +
                            "<p>The document has moved <a href=\"{0}\">here</a>.</p>\r\n" +
                            "</body></html>";

        #region Variables
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        public int Port
        {
            get
            {
                return this._Port;
            }
        }private int _Port = 80;
        public int ReceiveTimeout
        {
            get
            {
                return this._ReceiveTimeout;
            }
            set
            {
                if (value < 1) this._ReceiveTimeout = 1;
                else this._ReceiveTimeout = value;
            }
        }private int _ReceiveTimeout = 10000;
        public int SendTimeout
        {
            get
            {
                return this._SendTimeout;
            }
            set
            {
                if (value < 1) this._SendTimeout = 1;
                else this._SendTimeout = value;
            }
        }private int _SendTimeout = 10000;

        public int KeepAliveMax
        {
            get
            {
                return this._KeepAliveMax;
            }
            set
            {
                if (value < 1)
                    this._KeepAliveMax = -1;
                else
                    this._KeepAliveMax = value;
            }
        }private int _KeepAliveMax = -1;
        public int KeepAliveTimeout
        {
            get
            {
                return this._KeepAliveTimeout;
            }
            set
            {
                if (value < 1000)
                    this._KeepAliveTimeout = 1000;
                else
                    this._KeepAliveTimeout = value;
            }
        }private int _KeepAliveTimeout = 30000;


        public bool IsRunning
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return this._Server != null;
            }
        }

        public event HttpUserServerEventHandler RequestReceived;

        private TcpListener _Server;
        private AsyncCallback _AcceptSocketCallback = null;

        private object _RequestReceivedPadlock = new object();

        private List<Connection> _Connections = new List<Connection>();

        [ThreadStatic]
        private volatile static HttpUserServerEventHandler _RequestReceived = null;

        #endregion

        #region ctor
        static HttpUserServer()
        {
            Logging.Log.Init();
        }
        #endregion

        #region Public methods
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            this.Start(this._Port);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(int port)
        {
            if (this._Server != null)
                return;

            if (this._AcceptSocketCallback == null)
                this._AcceptSocketCallback = new AsyncCallback(acceptSocket);

            try
            {

                this._Server = new TcpListener(IPAddress.Any, port);
                this._Server.Start();
                this._Port = ((IPEndPoint)this._Server.LocalEndpoint).Port;

                this._Server.BeginAcceptSocket(this._AcceptSocketCallback, null);
                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[Run] Server started on port: {0}", this._Port);
            }
            catch (Exception ex)
            {
                _Logger.Error("[Run] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartBlocking()
        {
            this.StartBlocking();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartBlocking(int port)
        {
            if (this._Server != null) return;

            if (this._AcceptSocketCallback == null) this._AcceptSocketCallback = new AsyncCallback(acceptSocket);

            try
            {
                this._Server = new TcpListener(IPAddress.Any, port);
                this._Server.Start();
                this._Port = ((IPEndPoint)this._Server.LocalEndpoint).Port;

                while (true)
                {
                    Socket socket = this._Server.AcceptSocket();
                    if (socket != null)
                    {
                        if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[AcceptSocket] New socket connection accepted: '{0}'", socket.RemoteEndPoint.ToString());
                        this.handler(socket);
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[Run] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            this._Server.Stop();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (this._Server == null)
                return;

            try
            {
                TcpListener srv = this._Server;
                this._Server = null;
                srv.Stop();

                lock (this._Connections)
                {
                    foreach (Connection con in this._Connections)
                    {
                        try
                        {
                            con.Socket.Close();
                        }
                        catch { };
                    }
                }

                //Wait until all threads all closed
                while (true)
                {
                    lock (this._Connections)
                    {
                        if (this._Connections.Count == 0)
                            break;
                    }
                    Thread.Sleep(100);
                }

                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[Stop] Server stopped.");
            }
            catch (Exception ex)
            {
                _Logger.Error("[Stop] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }

        public bool SendToSocket(Socket socket, byte[] buffer)
        {
            return this.SendToSocket(socket, buffer, 0, buffer.Length);
        }
        public bool SendToSocket(Socket socket, byte[] buffer, int iOffset, int iSize)
        {
            while (iSize > 0)
            {
                if (socket == null || !socket.Connected)
                {
                    _Logger.Error("[SendToSocket] Invalid socket.");
                    return false;
                }

                try
                {
                    IAsyncResult ia = socket.BeginSend(buffer, iOffset, iSize, SocketFlags.None, null, null);
                    if (!ia.AsyncWaitHandle.WaitOne(this._SendTimeout))
                    {
                        _Logger.Error("[SendToSocket][{0}] Send timout.", socket.RemoteEndPoint);
                        return false;
                    }
                    int iSent = socket.EndSend(ia);
                    if (iSent == 0)
                    {
                        _Logger.Error("[SendToSocket][{0}] Failed to send data.", socket.RemoteEndPoint);
                        return false;
                    }

                    iOffset += iSent;
                    iSize -= iSent;
                }
                catch (SocketException ex)
                {
                    if (ex.ErrorCode == 0x00002746)
                        _Logger.Debug("[SendToSocket][{0}] Socket terminated.", socket.RemoteEndPoint);

                    else
                        _Logger.Error("[SendToSocket] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);

                    return false;
                }
                catch (Exception ex)
                {
                    _Logger.Error("[SendToSocket] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Private methods
        private void handler(Socket socket)
        {
            string strRemoteClient = null;
            int iKeepAliveMaxCnt = 0;
            bool bCloseSocket = true;

            try
            {
                //Get the request

                strRemoteClient = socket.RemoteEndPoint.ToString();

                byte[] buffer = new byte[8192];

                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[handler][{0}] Raw Request Received... ", strRemoteClient);

                bool bKeepAlive = false;

            start:

                //Read http request from socket
                int iRecLng = 0;
            read:
                int iCnt;

                IAsyncResult ia = socket.BeginReceive(buffer, iRecLng, buffer.Length - iRecLng, SocketFlags.None, null, null);

                bool bTimoutKeepAlive = bKeepAlive && iRecLng == 0;
                if (!ia.AsyncWaitHandle.WaitOne(bTimoutKeepAlive ? this._KeepAliveTimeout : this._ReceiveTimeout))
                {
                    if (!bTimoutKeepAlive)
                        _Logger.Error("[handler][{0}] Receive timout.", strRemoteClient);
                    else
                        if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[handler][{0}] Keep-Alive receive timeout. Closing connection ", strRemoteClient);

                    return;
                }

                if (!socket.Connected || (iCnt = socket.EndReceive(ia)) < 1)
                {
                    if (iRecLng > 0)
                        _Logger.Error("[handler][{0}] Socket closed while reading http request.", strRemoteClient);
                    else
                        if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[handler][{0}] Remote socket terminated.", strRemoteClient);
                    return;
                }
                iRecLng += iCnt;

                //Analyze http request
                HttpMethodEnum httpMethod;
                string strHttpPath = "";
                CookieContainer cookies = null;
                Dictionary<string, string> headersRequest = new Dictionary<string, string>();
                int iHttpReqLength = HttpUserWebRequest.GetHttpRequest(buffer, 0, iRecLng, ref headersRequest, ref cookies, out httpMethod, out strHttpPath);
                if (iHttpReqLength < 1)
                {
                    if (iRecLng == buffer.Length)
                    {
                        _Logger.Error("[handler][{0}] Invalid http header. Buffer full.", strRemoteClient);
                        return;
                    }

                    if (iHttpReqLength == 0)
                        goto read; //not received yet
                    else
                    {
                        _Logger.Error("[handler][{0}] Invalid http header.", strRemoteClient);
                        return;
                    }
                }

                if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[handler][{0}] Request:\r\n{1}", strRemoteClient, Encoding.ASCII.GetString(buffer, 0, iHttpReqLength));

                byte[] postData = null;

                string strVal;

                bKeepAlive = true;
                iKeepAliveMaxCnt++;
                if (headersRequest.TryGetValue(HttpHeaderField.HTTP_FIELD_CONNECTION, out strVal))
                {
                    if (strVal.Equals(HttpHeaderField.HTTP_FIELD_CLOSE, StringComparison.CurrentCultureIgnoreCase))
                        bKeepAlive = false;
                }

                if (httpMethod == HttpMethodEnum.POST)
                {
                    #region POST data
                    int iContentLength;
                    if (headersRequest.TryGetValue(HttpHeaderField.HTTP_FIELD_CONTENT_LENGTH, out strVal) && int.TryParse(strVal, out iContentLength))
                    {
                        int iRem = iRecLng - iHttpReqLength;

                        if (iRem >= iContentLength)
                        {
                            //All received
                            postData = new byte[iContentLength];
                            Buffer.BlockCopy(buffer, iHttpReqLength, postData, 0, iContentLength);
                        }
                        else
                        {
                            //Receive rest of the post data
                            Buffering buff = new Buffering();

                            //Save remaining data
                            if (iRem > 0)
                                buff.SaveData(buffer, iHttpReqLength, iRem);

                            //Receive remaining data
                            if (socket.Connected)
                            {
                                while (true)
                                {
                                    ia = socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, null, null);

                                    if (!ia.AsyncWaitHandle.WaitOne(this._ReceiveTimeout))
                                    {
                                        _Logger.Error("[handler][{0}] Receive timout.", strRemoteClient);
                                        buff = null;
                                        return;
                                    }

                                    if (socket.Connected && (iCnt = socket.EndReceive(ia)) > 0)
                                    {
                                        iRecLng += iCnt;
                                        buff.SaveData(buffer, 0, iCnt);
                                        if (buff.TotalBytesWrite >= iContentLength)
                                            break;
                                    }
                                    else
                                        break;
                                }
                            }

                            if (buff.TotalBytesWrite >= iContentLength)
                            {
                                postData = buff.BuildData();
                                buff = null;
                            }
                            else
                            {
                                _Logger.Error("[handler][{0}] Failed to receive content data.", strRemoteClient);
                                buff = null;
                                return;
                            }

                        }
                    }
                    else
                    {
                        _Logger.Error("[handler][{0}] Invalid Content-Length field.", strRemoteClient);
                        return;
                    }
                    #endregion
                }

                //Monitor.Enter(this._RequestReceivedPadlock);

                _RequestReceived = this.RequestReceived;

                if (_RequestReceived != null)
                {
                    HttpUserServerEventArgs args = new HttpUserServerEventArgs();
                    args.Method = httpMethod;
                    args.Path = strHttpPath;
                    args.HeaderFields = headersRequest;
                    args.Cookies = cookies;
                    args.PostData = postData;
                    args.RemoteSocket = socket;
                    args.KeepAlive = bKeepAlive;

                    //Fire event
                    try
                    {
                        _RequestReceived(this, args);
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[handler] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    }
                    finally
                    {
                        //Monitor.Exit(this._RequestReceivedPadlock);
                    }

                    //Check if the request was handled
                    if (args.Handled)
                    {
                        if (!args.ResponseSent)
                        {
                            byte[] response;

                            StringBuilder sbResponse = new StringBuilder(512);
                            sbResponse.Append("HTTP/1.1 ");

                            long lContentLength = 0;
                            long lFrom = -1, lTo = -1;
                            if (args.ResponseCode == HttpStatusCode.OK && args.ResponseStream != null)
                            {
                                if (HttpUserWebRequest.TryGetHttpRequestFieldRange(args.HeaderFields, out lFrom, out lTo))
                                {
                                    if (lFrom >= args.ResponseStream.Length || lTo >= args.ResponseStream.Length || (lTo >= 0 && lTo < lFrom))
                                    {
                                        _Logger.Error("[handler][{0}] Invalid Range Request. From:{1} To:{2}", strRemoteClient, lFrom, lTo);
                                        return;
                                    }

                                    args.ResponseCode = HttpStatusCode.PartialContent;
                                    args.ResponseStream.Position = lFrom;
                                }
                                else
                                    args.ResponseStream.Position = 0;

                            }

                            //Response code
                            switch (args.ResponseCode)
                            {
                                case HttpStatusCode.OK:
                                    sbResponse.Append("200 OK\r\n");
                                    break;

                                case HttpStatusCode.PartialContent:
                                    sbResponse.Append("206 Partial Content\r\n");
                                    break;

                                case HttpStatusCode.Moved:
                                    sbResponse.Append("301 Moved\r\n");
                                    break;

                                case HttpStatusCode.Found:
                                    sbResponse.Append("302 Found\r\n");
                                    break;

                                case HttpStatusCode.Unauthorized:
                                    sbResponse.Append("401 Unauthorized\r\n");
                                    break;

                                case HttpStatusCode.Forbidden:
                                    sbResponse.Append("403 Forbidden\r\n");
                                    break;

                                default:
                                case HttpStatusCode.NotFound:
                                    sbResponse.Append("404 Not Found\r\n");
                                    break;
                            }

                            string strContentType = !string.IsNullOrWhiteSpace(args.ResponseContentType) ? args.ResponseContentType : HttpHeaderField.HTTP_CONTENT_TYPE_TEXT_HTML;

                            if (args.ResponseHeaderFields != null)
                            {
                                foreach (KeyValuePair<string, string> pair in args.ResponseHeaderFields)
                                {
                                    if (pair.Key.Equals(HttpHeaderField.HTTP_FIELD_CONTENT_LENGTH, StringComparison.CurrentCultureIgnoreCase) ||
                                        pair.Key.Equals(HttpHeaderField.HTTP_FIELD_CONNECTION, StringComparison.CurrentCultureIgnoreCase) ||
                                        pair.Key.Equals(HttpHeaderField.HTTP_FIELD_KEEP_ALIVE, StringComparison.CurrentCultureIgnoreCase))
                                        continue;
                                    else if (pair.Key.Equals(HttpHeaderField.HTTP_FIELD_CONTENT_TYPE, StringComparison.CurrentCultureIgnoreCase))
                                        strContentType = pair.Value;
                                    else if (pair.Key.Equals(HttpHeaderField.HTTP_FIELD_CONTENT_RANGE, StringComparison.CurrentCultureIgnoreCase))
                                        continue;
                                    else
                                    {
                                        sbResponse.Append(pair.Key);
                                        sbResponse.Append(HttpHeaderField.HTTP_FIELD_COLON);
                                        sbResponse.Append(pair.Value);
                                        sbResponse.Append(HttpHeaderField.EOL);
                                    }
                                }
                            }

                            //Content-Type
                            sbResponse.Append(HttpHeaderField.HTTP_FIELD_CONTENT_TYPE);
                            sbResponse.Append(HttpHeaderField.HTTP_FIELD_COLON);
                            sbResponse.Append(strContentType);
                            sbResponse.Append(HttpHeaderField.EOL);

                            //ContentRange
                            if (lFrom > 0)
                            {
                                _Logger.Debug("[handler][{0}] RangeRequest: {1}-{2}", strRemoteClient, lFrom, lTo >= 0 ? lTo.ToString() : null);

                                sbResponse.Append(HttpHeaderField.HTTP_FIELD_CONTENT_RANGE);
                                sbResponse.Append(HttpHeaderField.HTTP_FIELD_COLON);
                                sbResponse.Append("bytes ");
                                sbResponse.Append(lFrom);
                                sbResponse.Append('-');
                                sbResponse.Append(lTo > 0 ? lTo : args.ResponseStream.Length - 1);
                                sbResponse.Append('/');
                                sbResponse.Append(args.ResponseStream.Length);
                                sbResponse.Append(HttpHeaderField.EOL);
                            }

                            //Content-Length
                            sbResponse.Append(HttpHeaderField.HTTP_FIELD_CONTENT_LENGTH);
                            sbResponse.Append(HttpHeaderField.HTTP_FIELD_COLON);
                            if (args.ResponseStream != null)
                                lContentLength = (lTo >= 0 ? lTo + 1 : args.ResponseStream.Length) - args.ResponseStream.Position;
                            else if (args.ResponseData != null)
                                lContentLength = args.ResponseData.Length;

                            sbResponse.Append(lContentLength);
                            sbResponse.Append(HttpHeaderField.EOL);

                            //Connection
                            sbResponse.Append(HttpHeaderField.HTTP_FIELD_CONNECTION);
                            sbResponse.Append(HttpHeaderField.HTTP_FIELD_COLON);
                            if (bKeepAlive)
                            {
                                sbResponse.Append(HttpHeaderField.HTTP_FIELD_KEEP_ALIVE);
                                sbResponse.Append(HttpHeaderField.EOL);

                                sbResponse.Append(HttpHeaderField.HTTP_FIELD_KEEP_ALIVE);
                                sbResponse.Append(HttpHeaderField.HTTP_FIELD_COLON);

                                sbResponse.Append("timeout=");
                                sbResponse.Append(this._KeepAliveTimeout / 1000);
                                if (this._KeepAliveMax > 0)
                                {
                                    sbResponse.Append(", max=");
                                    sbResponse.Append(this._KeepAliveMax);
                                }
                                sbResponse.Append(HttpHeaderField.EOL);
                            }
                            else
                            {
                                sbResponse.Append(HttpHeaderField.HTTP_FIELD_CLOSE);
                                sbResponse.Append(HttpHeaderField.EOL);
                            }

                            sbResponse.Append(HttpHeaderField.EOL);

                            response = Encoding.ASCII.GetBytes(sbResponse.ToString());


                            //Send HTTP Response Header
                            if (!this.SendToSocket(socket, response))
                                return;

                            //Content data
                            if (args.Method != HttpMethodEnum.HEAD)
                            {
                                if (args.ResponseStream != null)
                                {
                                    using (args.ResponseStream)
                                    {
                                        while (lContentLength > 0)
                                        {
                                            int iSize = args.ResponseStream.Read(buffer, 0, (int)Math.Min(lContentLength, buffer.Length));
                                            if (!this.SendToSocket(socket, buffer, 0, iSize))
                                                return;

                                            lContentLength -= iSize;
                                        }
                                    }
                                }
                                else if (args.ResponseData != null)
                                {
                                    if (!this.SendToSocket(socket, args.ResponseData))
                                        return;
                                }
                            }

                            if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[handler][{0}] Response:\r\n{1}", strRemoteClient, Encoding.ASCII.GetString(response));
                        }

                        //KeepAlive
                        if (args.KeepAlive && bKeepAlive && this._KeepAliveMax < 1 || (iKeepAliveMaxCnt < this._KeepAliveMax))
                            goto start;
                        else
                        {
                            bCloseSocket = args.CloseSocket;
                            return;
                        }
                    }
                }
                //else Monitor.Exit(this._RequestReceivedPadlock);

                socket.Send(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Type: text/html\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));

            }
            catch (SocketException ex)
            {
                if (((SocketException)ex).ErrorCode == 0x00002745 || ((SocketException)ex).ErrorCode == 0x00002746 || ((SocketException)ex).ErrorCode == 0x00002749)
                {
                    //WSAECONNABORTED(10053) Software caused connection abort. 
                    //WSAECONNRESET  (10054) Connection reset by peer.
                    //WSAENOTCONN    (10057) Socket is not connected.

                    if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[handler][{0}] Socket closed.", strRemoteClient);
                }
                else
                    _Logger.Error("[handler][{0}] Socket exception: {1}", strRemoteClient, ((SocketException)ex).ErrorCode);
            }
            catch (Exception ex)
            {
                _Logger.Error("[handler][{3}] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, strRemoteClient);
            }
            finally
            {
                try
                {
                    if (bCloseSocket)
                    {
                        if (socket.Connected)
                        {
                            socket.Shutdown(SocketShutdown.Both);
                            socket.Disconnect(true);
                        }
                        socket.Close();
                    }
                }
                catch { }

                lock (this._Connections)
                {
                    this._Connections.Remove(this._Connections.Find(p => p.Thread == Thread.CurrentThread));
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void acceptSocket(IAsyncResult result)
        {
            try
            {
                if (this._Server != null)
                {
                    Socket socket = this._Server.EndAcceptSocket(result);
                    if (socket != null)
                    {
                        if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[acceptSocket] New socket connection accepted: '{0}'", socket.RemoteEndPoint.ToString());

                        Thread thread = new Thread(new ParameterizedThreadStart((object o) => this.handler((Socket)o)));
                        lock (this._Connections)
                        {
                            this._Connections.Add(new Connection() { Thread = thread, Socket = socket });
                        }
                        thread.Start(socket);
                    }

                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[acceptSocket] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            //Start Accept again
            if (this._Server != null)
                this._Server.BeginAcceptSocket(this._AcceptSocketCallback, null);

        }
        #endregion
    }
}
