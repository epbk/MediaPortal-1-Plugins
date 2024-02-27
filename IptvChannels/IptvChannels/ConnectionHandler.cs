using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.IptvChannels
{
    class ConnectionHandler
    {
        #region Variables
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        public static int Port = Settings.Setting.PROXY_PORT_DEFAULT;

        private static TcpListener _Server;
        private static AsyncCallback _AcceptSocketCallback = null;

        private Socket _Client = null;
        #endregion

        #region ctor
        public ConnectionHandler(Socket client)
        {
            this._Client = client;
        }
        #endregion

        #region Public methods
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Start(int port)
        {
            if (_Server != null)
                return;

            if (_AcceptSocketCallback == null) 
                _AcceptSocketCallback = new AsyncCallback(acceptSocket);

            try
            {
                Port = port;
                _Server = new TcpListener(IPAddress.Any, port);
                _Server.Start();
                _Server.BeginAcceptSocket(_AcceptSocketCallback, null);
                _Logger.Debug("[Run] Server started on port: {0}", port);
            }
            catch (Exception ex)
            {
                _Logger.Error("[Run] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void StartBlocking(int port)
        {
            if (_Server != null) return;

            if (_AcceptSocketCallback == null) _AcceptSocketCallback = new AsyncCallback(acceptSocket);

            try
            {
                Port = port;
                _Server = new TcpListener(IPAddress.Any, port);
                _Server.Start();

                while (true)
                {
                    Socket socket = _Server.AcceptSocket();
                    if (socket != null)
                    {
                        _Logger.Debug("[AcceptSocket] New socket connection accepted: '{0}'", socket.RemoteEndPoint.ToString());
                        ConnectionHandler client = new ConnectionHandler(socket);
                        client.startHandling();
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[Run] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            _Server.Stop();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Stop()
        {
            if (_Server == null) return;

            try
            {
                TcpListener srv = _Server;
                _Server = null;
                srv.Stop();

                _Logger.Debug("[Stop] Server stopped.");


            }
            catch (Exception ex)
            {
                _Logger.Error("[Stop] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }
        #endregion

        #region Private methods
        private void startHandling()
        {
            Thread thread = new Thread(this.handler);
            //handler.Priority = ThreadPriority.AboveNormal;
            thread.Start();
        }

        private void handler()
        {
            try
            {
                //Get the request

                string strResponse = "";

                byte[] bufferResponse = new byte[8192];

                _Logger.Debug("[Handler][" + this._Client.RemoteEndPoint.ToString() + "] Raw Request Received... ");

                //Read http request from socket
                int iRecLng = 0;
            read:
                int iCnt;
                if ((iCnt = this._Client.Receive(bufferResponse, iRecLng, bufferResponse.Length - iRecLng, SocketFlags.None)) < 1)
                {
                    _Logger.Error("[Handler][" + this._Client.RemoteEndPoint.ToString() + "] Error occured: socket closed while reading http response.");
                    return;
                }
                iRecLng += iCnt;
                if (iRecLng < 4 || (!(bufferResponse[iRecLng - 1] == '\n' && bufferResponse[iRecLng - 2] == '\n') &&
                    !(bufferResponse[iRecLng - 1] == '\n' && bufferResponse[iRecLng - 2] == '\r' && bufferResponse[iRecLng - 3] == '\n' && bufferResponse[iRecLng - 4] == '\r'))) goto read;

                //Analyze http request
                int iRespLength;
                string strHttpHeader = null;
                Pbk.Net.Http.HttpMethodEnum httpMethod;
                string strHttpPath = "";
                Dictionary<string, string> headersRequest = new Dictionary<string, string>();
                CookieContainer cookies = null;
                if (Pbk.Net.Http.HttpUserWebRequest.GetHttpRequest(bufferResponse, 0, iRecLng, ref headersRequest, ref cookies, out httpMethod, out strHttpPath) < 1)
                {
                    _Logger.Error("[Handler][" + this._Client.RemoteEndPoint.ToString() + "] Invalid Http request");
                    return;
                }

                _Logger.Debug("[Handler][" + this._Client.RemoteEndPoint.ToString() + "] Request:\r\n" + strHttpHeader);

                //Get final url from site
                SiteUtils.IptvChannel channel = Plugin.Instance.getIptvChannelByUrlParam(strHttpPath);
                if (channel != null)
                {
                    //Channel found; call site to get the final url
                    try
                    {
                        Thread thread = new Thread(new ThreadStart(() =>
                            {
                                strResponse = channel.SiteUtil.GetStreamUrl(channel);
                            }));

                        thread.Start();

                        //Wait for thread
                        if (!thread.Join(60000))
                        {
                            thread.Abort();
                            strResponse = null;
                            _Logger.Error("[Handler] Function timeout:GetFinalUrl(IptvChannel channel)");
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[Handler] Plugin error:GetFinalUrl(IptvChannel channel) {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                        strResponse = null;
                    }
                }
               
                if (!string.IsNullOrEmpty(strResponse))
                    strResponse = "HTTP/1.1 301 Moved Permanently" + Pbk.Net.Http.HttpHeaderField.EOL + "Location: " + strResponse + Pbk.Net.Http.HttpHeaderField.EOL + Pbk.Net.Http.HttpHeaderField.EOL; //OK; we have the url; use redirect
                else
                    strResponse = "HTTP/1.1 404 Not Found" + Pbk.Net.Http.HttpHeaderField.EOL + Pbk.Net.Http.HttpHeaderField.EOL; //not found

                _Logger.Debug("[Handler][" + this._Client.RemoteEndPoint.ToString() + "] Response:\r\n" + strResponse);

                this._Client.Send(Encoding.UTF8.GetBytes(strResponse));

            }
            catch (Exception ex)
            {
                _Logger.Error("[Handler] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
            finally
            {
                this._Client.Shutdown(SocketShutdown.Both);
                this._Client.Disconnect(true);
                this._Client.Close();
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void acceptSocket(IAsyncResult result)
        {
            try
            {
                if (_Server != null)
                {
                    Socket socket = _Server.EndAcceptSocket(result);
                    if (socket != null)
                    {
                        _Logger.Debug("[acceptSocket] New socket connection accepted: '{0}'", socket.RemoteEndPoint.ToString());
                        ConnectionHandler client = new ConnectionHandler(socket);
                        client.startHandling();
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[acceptSocket] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            //Start Accept again
            if (_Server != null)
                _Server.BeginAcceptSocket(_AcceptSocketCallback, null);

        }
        #endregion
    }
}
