using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.NetworkInformation;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Sgml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using MediaPortal.Pbk.Logging;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using System.Reflection;
using MediaPortal.Profile;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebRequest : IDisposable
    {
        #region Types

        private class InterStream : Stream
        {
            private Stream _Stream;
            private byte[] _Buffer = null;
            private int _BufferOffset = 0;
            private int _InBuffer = 0;
            private HttpUserWebRequest _Request;

            public InterStream(Stream stream, byte[] data, int iOffset, int iSize, HttpUserWebRequest request)
            {
                if (stream == null)
                    throw new ArgumentNullException("Stream is empty");

                this._Stream = stream;
                this._Request = request;

                if (iSize > 0)
                {
                    if (data == null)
                        throw new ArgumentNullException("data");

                    if (iOffset >= 0 && iOffset < data.Length && iOffset + iSize <= data.Length)
                    {
                        this._Buffer = new byte[Math.Max(iSize, 1024)];
                        Buffer.BlockCopy(data, iOffset, this._Buffer, 0, iSize);
                        this._InBuffer = iSize;
                    }
                    else
                        throw new ArgumentOutOfRangeException();
                }
                //else
                //    this._Buffer = new byte[1024];
            }

            public override bool CanRead
            {
                get { return this._Stream.CanRead || this._InBuffer > 0; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override bool CanTimeout
            {
                get { return this._Stream.CanTimeout; }
            }

            public override void Flush()
            {
                this._Stream.Flush();
            }

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int ReadByte()
            {
                this._Request._StreamReadAttempts++;

                if (this._InBuffer <= 0)
                {
                    //Allow keepalive. If tcp stream returns 0 then cannot by used anymore
                    if (this._Request.IsResponseStreamEnded)
                        return -1;

                    if (this._Buffer == null)
                        this._Buffer = new byte[1024];

                    this._BufferOffset = 0;
                    this._InBuffer = this._Stream.Read(this._Buffer, 0, this._Buffer.Length);

                    if (this._InBuffer <= 0)
                    {
                        this._Request._ConnectionKeepAlive = false;
                        return -1;
                    }
                }

                //From buffer
                int i = this._Buffer[this._BufferOffset++];
                this._InBuffer--;
                this._Request._ResponseBytesReceived++;
                return i;
            }

            public override int Read(byte[] buffer, int iOffset, int iLength)
            {
                this._Request._StreamReadAttempts++;

                if (this._InBuffer > 0)
                {
                    //From buffer
                    int iToCopy = Math.Min(iLength, this._InBuffer);
                    Buffer.BlockCopy(this._Buffer, this._BufferOffset, buffer, iOffset, iToCopy);

                    this._BufferOffset += iToCopy;
                    this._InBuffer -= iToCopy;

                    this._Request._ResponseBytesReceived += iToCopy;

                    return iToCopy;
                }
                else
                {
                    //Allow keepalive. If tcp stream returns 0 then cannot by used anymore
                    if (this._Request.IsResponseStreamEnded)
                        return 0;

                    int iReceived = this._Stream.Read(buffer, iOffset, iLength);

                    if (iReceived > 0)
                        this._Request._ResponseBytesReceived += iReceived;
                    else
                        this._Request._ConnectionKeepAlive = false;

                    return iReceived;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int iOffset, int iLength)
            {
                throw new NotSupportedException();
            }

            public override void Close()
            {
                //If the source stream can't be reused then close it
                if (!this._Request._ConnectionKeepAlive)
                    this._Stream.Close();
            }

        }

        private class Connection : Stream
        {
            public enum StateEnum
            {
                Unknown = 0,
                Disconnected,
                Iddle,
                InUse,
                HandingOver,
                HandedOver
            }

            private byte[] _Buffer = new byte[1024];
            private int _BufferRead = 0;
            private int _BufferPosition = 0;
            private Stream _StreamSource;

            public ulong ID;
            public string Autority { get { return this._Autority; } }private string _Autority = null;

            public TcpClient Client;

            public StateEnum State
            {
                get
                {
                    return this._State;
                }

                set
                {
                    lock (this._StreamSource)
                    {
                        if (value == StateEnum.Iddle && (this._State == StateEnum.HandedOver || this._State == StateEnum.Unknown))
                        {
                            this._State = StateEnum.Iddle;
                            this._BufferRead = 0;
                            this._StreamSource.BeginRead(this._Buffer, 0, this._Buffer.Length, this.cbStream, null);
                        }
                        else if (value == StateEnum.InUse && this._State == StateEnum.Iddle)
                            this._State = StateEnum.InUse;
                    }
                }
            }private StateEnum _State = StateEnum.Unknown;

            public int CloseTimeout = -1; //[s]

            public int MaxRequests = 0;

            public DateTime TimestampIddle;

            public string Proxy;

            public string Scheme { get { return this._Scheme; } }private string _Scheme = null;

            public bool Connected
            {
                get { return this._State > StateEnum.Disconnected; }
            }

            public bool IsAvailable
            {
                get
                {
                    return this._State == StateEnum.Iddle &&
                        ((this.CloseTimeout - (DateTime.Now - this.TimestampIddle).TotalSeconds) >= 2);
                }
            }

            public bool CanBeUsed(HttpUserWebRequest rq, bool bConnectionKeepAliveAllowCrossDomain)
            {
                return
                    this.IsAvailable && this._Scheme.Equals(rq._ServerUri.Scheme) &&
                    ((this.Proxy == null && rq.Proxy == null && ((bConnectionKeepAliveAllowCrossDomain && !rq._ReuseConnectionFullDomainCheck)
                        || this.Autority.Equals(rq._ServerAutority, StringComparison.CurrentCultureIgnoreCase)))
                        || (this.Proxy != null && this.Proxy.Equals(rq.Proxy)));
            }


            public Connection(Stream stream, string strAuthority, string strScheme)
            {
                this._StreamSource = stream;
                this._Autority = strAuthority;
                this._Scheme = strScheme;
                this.State = StateEnum.Iddle;
            }


            public void CloseStream()
            {
                this._State = StateEnum.Disconnected;
                this._StreamSource.Close();
            }

            private void cbStream(IAsyncResult ar)
            {
                lock (this._StreamSource)
                {
                    int iRd = 0;
                    try
                    {
                        if (this._State > StateEnum.Disconnected && this._StreamSource.CanRead)
                            iRd = this._StreamSource.EndRead(ar);
                    }
                    catch { iRd = 0; }


                    if (iRd < 1)
                        this._State = StateEnum.Disconnected;
                    else if (this._State != StateEnum.InUse)
                        throw new Exception("Connection: data received while not using the connection.");
                    else
                    {
                        this._BufferRead = iRd;
                        this._BufferPosition = 0;
                        this._State = StateEnum.HandingOver;
                    }

                    //Inform about state change
                    Monitor.Pulse(this._StreamSource);
                }
            }

            #region Stream
            public override bool CanRead
            {
                get { return this.Connected; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return this.Connected && this._StreamSource.CanWrite; }
            }

            public override void Flush()
            {
                this._StreamSource.Flush();
            }

            public override long Length
            {
                get { throw new NotImplementedException(); }
            }

            public override long Position
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this._State == StateEnum.HandedOver)
                    return this._StreamSource.Read(buffer, offset, count); //directly from the stream

                lock (this._StreamSource)
                {
                    while (true)
                    {
                        switch (this._State)
                        {
                            case StateEnum.InUse:
                                //Reposnse data not received yet; wait
                                Monitor.Wait(this._StreamSource);
                                continue;

                            case StateEnum.HandingOver:
                                //Response can be read now; empty the buffer
                                int iCnt = Math.Min(count, this._BufferRead - this._BufferPosition);
                                Buffer.BlockCopy(this._Buffer, this._BufferPosition, buffer, offset, iCnt);
                                this._BufferPosition += iCnt;

                                if ((this._BufferRead -= iCnt) == 0)
                                    this._State = StateEnum.HandedOver;

                                return iCnt;

                            default:
                                return 0;
                        }
                    }
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (this.Connected)
                    this._StreamSource.Write(buffer, offset, count);
            }
            #endregion
        }

        private class ConnectionGroup
        {
            public ulong ID;
            public string Autority;
            public IPEndPoint Ep
            {
                get { return this._Ep; }
                set
                {
                    if (value != null)
                        this._IsIpLocal = WebTools.IsLocalIpAddress(value.Address.ToString());

                    this._Ep = value;
                }
            }private IPEndPoint _Ep;
            public List<Connection> Connections = new List<Connection>();
            public int ConnectionSlotsInUse = 0;
            public List<ulong> AssignedTokens = new List<ulong>();
            public bool IsIpLocal
            {
                get
                {
                    return this._IsIpLocal;
                }
            }private bool _IsIpLocal = false;


        }

        private class ConnectionHandler
        {
            private static Logger _Logger = LogManager.GetCurrentClassLogger();
            private List<ConnectionGroup> _Groups = new List<ConnectionGroup>();
            private System.Timers.Timer _Timer = null;

            private ulong _IdGroupCounter = 0;
            private ulong _IdConnectionCounter = 0;
            private ulong _TokenConnectionCounter = 0;

            private DateTime _TsConnectionsCheck = DateTime.MinValue;

            private DateTime _TargetCheckTime = DateTime.MinValue;

            public int MaxConnectionsPerGroup = 10; // < 1 : Unlimited

            public bool ConnectionKeepAliveAllowCrossDomain = false;

            ~ConnectionHandler()
            {
                this.ReleaseAllConnections();
            }


            public void ReleaseAllConnections()
            {
                _Logger.Debug("[ReleaseAllConnections] Closing all connections...");

                lock (this._Groups)
                {
                    foreach (ConnectionGroup cg in this._Groups)
                    {
                        lock (cg)
                        {
                            foreach (Connection sc in cg.Connections)
                            {
                                try
                                {
                                    _Logger.Debug("[ReleaseAllConnections] Closing Connection[{2}] Group[{3}]:{0} Connected:{1}",
                                        sc.Client.Client.RemoteEndPoint, sc.Client.Connected, sc.ID, cg.ID);

                                    sc.Client.Close();
                                    sc.CloseStream();
                                }
                                catch { }
                            }
                        }//Release group lock
                    }

                    if (this._Timer != null)
                    {
                        this._Timer.Enabled = false;
                        this._Timer.Dispose();
                        this._Timer = null;
                    }

                    this._Groups.Clear();
                }//Release groups lock

            }

            public Connection GetConnection(HttpUserWebRequest rq)
            {
                return this.getConnection(rq, false);
            }

            public void ReleaseConnection(HttpUserWebRequest rq)
            {
                if (rq._Token > 0)
                {
                    ConnectionGroup cg;

                    //First get the group
                    lock (this._Groups)
                    {
                        cg = this._Groups.Find(p =>
                           (p.Ep.Address.Equals(rq._ServerIpEndpoint.Address) && p.Ep.Port.Equals(rq._ServerIpEndpoint.Port)) ||
                           p.Autority == rq._ServerAutority);

                        //Remove assigned token
                        cg.AssignedTokens.Remove(rq._Token);
                        rq._Token = 0;
                    }//Release groups lock


                    bool bCheckNeeded = true;

                    //We need lock to the group
                    lock (cg)
                    {
                        cg.ConnectionSlotsInUse--;

                        if (rq._Stream != null && rq._TcpClient != null)
                        {
                            //Try find the connection in the group
                            Connection sc = cg.Connections.Find(p => p.Client == rq._TcpClient);
                            if (sc != null)
                            {
                                //Found
                                if (rq._TcpClient.Connected && rq._ConnectionKeepAlive && sc.MaxRequests != 0 && sc.State == Connection.StateEnum.HandedOver)
                                {
                                    //Maintain the connection
                                    sc.State = Connection.StateEnum.Iddle;

                                    if (rq._ConnectionKeepAliveTimeout > 0)
                                        sc.CloseTimeout = rq._ConnectionKeepAliveTimeout;
                                    else
                                        sc.CloseTimeout = _ConnectionKeepAliveTimeoutDefault;

                                    sc.TimestampIddle = DateTime.Now;

                                    _Logger.Debug("[ReleaseConnection] Maintaining Connection[{4}] Group[{5}] {0} Connected:{1} Max:{2} Timeout:{3} Proxy:{6}",
                                            sc.Client.Client.RemoteEndPoint, sc.Client.Connected, sc.MaxRequests, sc.CloseTimeout, sc.ID, cg.ID, sc.Proxy);
                                }
                                else
                                {
                                    //Can't be used anymore
                                    sc.State = Connection.StateEnum.Disconnected;
                                    terminate(rq); //Terminate connection
                                }
                            }
                            else
                            {
                                //Not found
                                if (rq._TcpClient.Connected && rq._ConnectionKeepAlive && rq._ConnectionKeepAliveMax != 0)
                                {
                                    //Maintain new connection
                                    sc = new Connection(rq._StreamSource, rq._ServerAutority, rq._ServerUri.Scheme);
                                    sc.ID = this._IdConnectionCounter++;
                                    sc.Client = rq._TcpClient;
                                    sc.Proxy = rq.Proxy;

                                    if (rq._ConnectionKeepAliveTimeout > 0)
                                        sc.CloseTimeout = rq._ConnectionKeepAliveTimeout;
                                    else
                                        sc.CloseTimeout = _ConnectionKeepAliveTimeoutDefault;

                                    sc.MaxRequests = rq._ConnectionKeepAliveMax;

                                    sc.TimestampIddle = DateTime.Now;

                                    cg.Connections.Add(sc);

                                    _Logger.Debug("[ReleaseConnection] New Connection to maintain[{3}] Group[{4}] {0} Max:{1} Timeout:{2} Proxy:{5}",
                                        sc.Client.Client.RemoteEndPoint, sc.MaxRequests, sc.CloseTimeout, sc.ID, cg.ID, sc.Proxy);
                                }
                                else
                                {
                                    bCheckNeeded = false;
                                    terminate(rq); //Terminate connection
                                }
                            }
                        }

                        //Notify about releasing connection slot
                        System.Threading.Monitor.Pulse(cg);
                    }//Release group lock

                    //If needed check all groups & connections
                    if (bCheckNeeded)
                        this.check();
                    else
                        this.timerSet(10); //check anyway later; the group can be empty
                }
                else
                    terminate(rq); //Terminate connection
            }

            public void RegroupConnection(HttpUserWebRequest rq)
            {
                if (rq._Token > 0 && rq._Stream == null)
                {
                    _Logger.Debug("[RegroupConnection] From: {0} to {1}", rq._ServerIpEndpoint.Address, ((IPEndPoint)rq._TcpClient.Client.RemoteEndPoint).Address);

                    this.ReleaseConnection(rq);

                    rq._ServerIpEndpoint = new IPEndPoint(((IPEndPoint)rq._TcpClient.Client.RemoteEndPoint).Address, rq._ServerUri.Port);

                    this.getConnection(rq, true);
                }
            }


            private Connection getConnection(HttpUserWebRequest rq, bool bGroupOnly)
            {
                bool bLocked = false;

                try
                {
                    //Lock
                    System.Threading.Monitor.Enter(this._Groups, ref bLocked);

                    //Assign request token to webrequest
                    rq._Token = ++this._TokenConnectionCounter;

                    //First try get the group based on ip & port or autority
                    ConnectionGroup cg = this._Groups.Find(p =>
                           (p.Ep.Address.Equals(rq._ServerIpEndpoint.Address) && p.Ep.Port.Equals(rq._ServerIpEndpoint.Port)) ||
                           p.Autority == rq._ServerAutority);

                    if (cg != null)
                    {
                        //Add token to the group to avoid deleting the group
                        cg.AssignedTokens.Add(rq._Token);

                        //Now we can release the lock
                        if (bLocked)
                        {
                            System.Threading.Monitor.Exit(this._Groups); //Release groups lock
                            bLocked = false;
                        }

                        //Lock to the group
                        lock (cg)
                        {
                            if (bGroupOnly)
                            {
                                //Increase number of running webrequests per group
                                cg.ConnectionSlotsInUse++;
                                return null;
                            }

                            while (true)
                            {
                                //Try find existing free working connection
                                for (int i = 0; i < cg.Connections.Count; i++)
                                {
                                    Connection con = cg.Connections[i];

                                    if (con.CanBeUsed(rq, this.ConnectionKeepAliveAllowCrossDomain))
                                    {
                                        //We have free connection to use
                                        _Logger.Debug("[GetConnection] Reusing Connection[{4}] Group[{6}] Token[{5}] {0} Connected:{1} Max:{2} Timeout:{3} Proxy:{7}",
                                                    con.Client.Client.RemoteEndPoint, con.Connected, con.MaxRequests, con.CloseTimeout, con.ID, rq._Token,
                                                    cg.ID, con.Proxy);

                                        con.State = Connection.StateEnum.InUse;

                                        if (con.MaxRequests > 0)
                                            con.MaxRequests--;

                                        //Increase number of running webrequests per group
                                        cg.ConnectionSlotsInUse++;

                                        return con;
                                    }
                                    //else
                                    //{
                                    //    _Logger.Debug("[GetConnection] Unavailable Connection[{4}] Group[{6}] Connected:{1} Max:{2} Timeout:{3} Proxy:{7} State:{8} Available:{9}",
                                    //                con.Client.Client.RemoteEndPoint, con.Connected, con.MaxRequests, con.CloseTimeout, con.ID, rq._Token,
                                    //                cg.ID, con.Proxy, con.State, con.IsAvailable);
                                    //}
                                }

                                if ((this.MaxConnectionsPerGroup > 0 && cg.ConnectionSlotsInUse < this.MaxConnectionsPerGroup)
                                    || cg.IsIpLocal)
                                {
                                    //No free connection slot but limit is not reached yet so we can create a new one
                                    cg.ConnectionSlotsInUse++;
                                    return null;
                                }
                                else
                                {
                                    //No free connection slot and we can't create new one 'cause limit is reached

                                    _Logger.Debug("[GetConnection] No free connection slot. Group[{2}] Token[{3}] {0} Limit:{1} Proxy:{4}",
                                            cg.Ep, this.MaxConnectionsPerGroup, cg.ID, rq._Token, rq.Proxy);

                                    //Release the group lock and wait for pulse
                                    System.Threading.Monitor.Wait(cg);

                                    //We are awaken and locked. Try again get free connection slot

                                    _Logger.Debug("[GetConnection] Awaken: Check free connection slot. Group[{2}] Token[{3}] {0} Limit:{1} Proxy:{4}",
                                            cg.Ep, this.MaxConnectionsPerGroup, cg.ID, rq._Token, rq.Proxy);
                                }
                            }
                        }//Release group lock

                    }
                    else
                    {
                        //Group does not exist yet

                        _Logger.Debug("[GetConnection] Creating new Group[{2}] Token[{3}] {0}:{1}",
                            rq._ServerIpEndpoint.Address, rq._ServerIpEndpoint.Port, this._IdGroupCounter, rq._Token);

                        cg = new ConnectionGroup()
                        {
                            ID = this._IdGroupCounter++,
                            Autority = rq._ServerAutority,
                            Ep = new IPEndPoint(rq._ServerIpEndpoint.Address, rq._ServerIpEndpoint.Port),
                            ConnectionSlotsInUse = 1
                        };

                        cg.AssignedTokens.Add(rq._Token);

                        this._Groups.Add(cg);

                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Error("[GetConnection] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                }
                finally
                {
                    if (bLocked)
                        System.Threading.Monitor.Exit(this._Groups); //Release groups lock
                }

                return null;
            }

            private static void terminate(HttpUserWebRequest rq)
            {
                rq._ConnectionKeepAlive = false;
                if (rq._Stream != null)
                {
                    rq._Stream.Close();
                    rq._Stream = null;
                    rq._StreamSource = null;
                    rq._StreamChunked = null;
                }

                if (rq._TcpClient != null)
                {
                    rq._TcpClient.Close();
                    rq._TcpClient = null;
                }
            }

            private void check()
            {
                //First lock to the groups
                lock (this._Groups)
                {
                    _Logger.Debug("[check] Checking...");

                    int iTimeout = int.MaxValue; //[s]
                    DateTime tsNow = DateTime.Now;

                    //Process every group
                    for (int iIdxGroup = this._Groups.Count - 1; iIdxGroup >= 0; iIdxGroup--)
                    {
                        ConnectionGroup cg = this._Groups[iIdxGroup];

                        //Lock to the group
                        lock (cg)
                        {
                            //Check every connection in the group
                            for (int iIdxCon = cg.Connections.Count - 1; iIdxCon >= 0; iIdxCon--)
                            {
                                Connection sc = cg.Connections[iIdxCon];

                                if (sc.State == Connection.StateEnum.Iddle || !sc.Connected)
                                {
                                    int iTdiff = sc.CloseTimeout - (int)((tsNow - sc.TimestampIddle).TotalSeconds);

                                    if (!sc.Connected || iTdiff < 2)
                                    {
                                        //Remove the connection
                                        try
                                        {
                                            _Logger.Debug("[check] Closing Connection[{3}] Group[{4}] {0} Connected:{1} RemainingTime:{2}",
                                                sc.Client.Connected ? sc.Client.Client.RemoteEndPoint : null,
                                                sc.Connected,
                                                iTdiff,
                                                sc.ID,
                                                cg.ID);

                                            sc.Client.Close();
                                            sc.CloseStream();
                                        }
                                        catch (Exception ex) { _Logger.Error("[check] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace); }

                                        cg.Connections.RemoveAt(iIdxCon);

                                        //Notify about releasing connection slot
                                        System.Threading.Monitor.Pulse(cg);
                                    }
                                    else if (iTdiff < iTimeout)
                                        iTimeout = iTdiff;
                                }
                            }

                            _Logger.Debug("[check] Connections:{3} SlotsInUse:{2} Group[{1}] {0}",
                                cg.Ep, cg.ID, cg.ConnectionSlotsInUse, cg.Connections.Count);

                            //If there is no connection or request(token) in the group then delete group
                            if (cg.Connections.Count == 0 && cg.ConnectionSlotsInUse <= 0 && cg.AssignedTokens.Count == 0)
                            {
                                _Logger.Debug("[check] Removing Group[{1}] {0}", cg.Ep, cg.ID);
                                this._Groups.RemoveAt(iIdxGroup);
                            }
                        }//Release group lock

                    }

                    _Logger.Debug("[check] Groups: {0}", this._Groups.Count);

                    //Setup the new time to check
                    if (iTimeout < int.MaxValue)
                        this.timerSet(iTimeout - 2);
                }//Release groups lock
            }

            private void timerSet(int iTimeout)
            {
                lock (this._Groups)
                {
                    //Setup the new time to check
                    if (iTimeout < int.MaxValue)
                    {
                        int iT = Math.Max(500, Math.Min(10000, iTimeout * 1000)); //min 0.5s; max 10s

                        DateTime dtCheck = DateTime.Now.AddMilliseconds(iT);

                        if (this._Timer == null)
                        {
                            this._Timer = new System.Timers.Timer();
                            this._Timer.AutoReset = false;
                            this._Timer.Elapsed += this.timerCallback;
                        }

                        if (!this._Timer.Enabled || this._TargetCheckTime == DateTime.MinValue || (this._TargetCheckTime - dtCheck).TotalMilliseconds > 500)
                        {
                            this._Timer.Interval = iT;
                            this._Timer.Enabled = true;

                            _Logger.Debug("[timerSet] Timer set to:" + this._Timer.Interval + " ms");
                            this._TargetCheckTime = dtCheck;
                        }
                    }
                }//Release groups lock
            }

            private void timerCallback(object sender, System.Timers.ElapsedEventArgs e)
            {
                _Logger.Debug("[timerCallback] Timer elapsed.");
                this.check();
            }

            private static bool isClientConnected(TcpClient client)
            {
                if (!client.Connected)
                    return false;

                TcpConnectionInformation[] tcpCons = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();

                foreach (TcpConnectionInformation c in tcpCons)
                {
                    try
                    {
                        if (c.LocalEndPoint.Equals(client.Client.LocalEndPoint) && c.RemoteEndPoint.Equals(client.Client.RemoteEndPoint))
                            return c.State == TcpState.Established;
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[isClientConnected] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                        return false;
                    }
                }

                return false;
            }

        }
        #endregion

        #region Constants
        public const string REGEX_CONTENT_RANGE = "bytes\\s(?<from>[\\d]+)-(?<to>[\\d]+)/(?<length>[\\d]+)";
        public const string REGEX_RANGE = "bytes=(?<from>[\\d]+)-(?<to>[\\d]+)*";

        private const int _CONNECTION_KEEP_ALIVE_TIMEOUT_DEFAULT = 60; //[s]
        private const int _CONNECTION_KEEP_ALIVE_MAX_DEFAULT = -1; //[requests]

        private const int _CONNECT_TIMEOUT_DEFAULT = 5000; //[ms]
        private const int _RESPONSE_TIMEOUT_DEFAULT = 10000; //[ms]

        private const int _RECEIVE_BUFFSIZE = 1024 * 256;

        private const int _BUFFER_SIZE = 8 * 1024;
        #endregion

        #region Private fields

        protected static Logger _Logger = LogManager.GetCurrentClassLogger();

        private int _Id = -1;
        private static int _IdCounter = -1;

        private static Regex _RegexHttpFieldCookie = new Regex("(?<key>[^;=]+)=(?<value>[^;]+)", RegexOptions.Compiled);
        private static Regex _RegexHttpFieldStatus = new Regex("HTTP/(?<version>[0-9\\.]+)\\s+(?<code>[0-9]+)((\\s+(?<result>.+))|)$", RegexOptions.Compiled);
        private static Regex _RegexHttpFieldMethod = new Regex("(?<type>GET|HEAD|POST|PUT|CONNECT|DELETE|PATCH|OPTIONS|TRACE) (?<path>[^\\s]+)", RegexOptions.Compiled);
        private static Regex _RegexHttpField = new Regex("(?<key>[^:\\s]+)\\s*:\\s*(?<value>.+)*", RegexOptions.Compiled);

        private static ConnectionHandler _Connections = new ConnectionHandler();

        private Uri _ServerUri;

        private int _TotalBytesWritten;
        private byte[] _BufferReceive;


        private TcpClient _TcpClient = null;
        private Stream _Stream = null;
        private Stream _StreamSource = null;

        private bool _ConnectionChunked;
        private bool _ConnectionKeepAlive;


        private int _ConnectionKeepAliveMax;

        private int _ReRequestAttempts;

        private bool _Redirect;

        private IPEndPoint _ServerIpEndpoint;
        private string _ServerAutority;

        private ulong _Token = 0;

        private long _ResumeFrom;
        private long _ResumeLength;



        private DateTime _DownloadProgressUpdateTs;

        private int _StreamReadAttempts;

        private bool _ReusingConnection;
        private bool _ReuseConnectionFullDomainCheck = false;

        private long _PostDataSize = -1;

        #endregion

        #region Event delegates
        /// <summary>
        /// Called to report current status of downloading
        /// </summary>
        public HttpUserWebRequestEventHandler Event;

        /// <summary>
        /// Called before sending http request to the server
        /// </summary>
        public HttpUserWebBeforeRequestEventHandler BeforeRequest;

        /// <summary>
        /// Called after receiving http response from the server
        /// </summary>
        public HttpUserWebBeforeDownloadEventHandler BeforeDownload;

        /// <summary>
        /// Caled after receiving http response and before creating file stream.
        /// </summary>
        public HttpUserWebBeforeSaveToFileEventHandler BeforeSaveToFile;
        #endregion

        /// <summary>
        /// Function to override internal http head creation
        /// </summary>
        public Func<string> CreateHttpHeader = null;

        public GetResponseResultEnum GetResponseResult
        {
            get
            {
                return this._GetResponseResult;
            }
        }private GetResponseResultEnum _GetResponseResult;

        public string Url
        {
            get
            {
                return this._ServerUrl;
            }

            set
            {
                if (!this._Closed)
                    throw new Exception("Can't reuse unclosed request.");

                this.init(value);
            }
        }private string _ServerUrl;

        public string ServerUrlRedirect
        {
            get { return this._ServerUrlRedirect; }
        }private string _ServerUrlRedirect;

        public object Tag;

        public string Referer;
        public string UserAgent = HttpHeaderField.HTTP_DEFAULT_USER_AGENT;
        public string Accept = HttpHeaderField.HTTP_DEFAULT_ACCEPT;
        public string AcceptLanguage = HttpHeaderField.HTTP_DEFAULT_ACCEPT_LANGUAGE;
        public CookieContainer Cookies;
        public bool DoNotTrack = true;
        public string ContentType;

        public double HttpResponseVersion
        {
            get
            {
                return this._HttpResponseVersion;
            }
        }private double _HttpResponseVersion;

        public long ContentLength
        {
            get
            {
                return this._ContentLength;
            }
        }private long _ContentLength;

        public string ContentFilename
        {
            get
            {
                return this._ContentFilename;
            }
        }private string _ContentFilename;

        public long ResponseBytesReceived
        {
            get
            {
                return this._ResponseBytesReceived;
            }
        }private long _ResponseBytesReceived;

        public long FilestreamPosition
        {
            get
            {
                return this._FileStreamPosition;
            }
        }private long _FileStreamPosition;

        public long FileOffset
        {
            get { return this._FileOffset; }
        } private long _FileOffset;

        public bool AllowKeepAlive = true;

        public int ConnectionKeepAliveTimeout
        {
            get
            {
                return this._ConnectionKeepAliveTimeout;
            }

            set
            {
                if (value < 2)
                    this._ConnectionKeepAliveTimeout = 2;
                else if (value > 300)
                    this._ConnectionKeepAliveTimeout = 300;
                else
                    this._ConnectionKeepAliveTimeout = value;

            }
        }private int _ConnectionKeepAliveTimeout;

        /// <summary>
        /// Enable or disable cookies when sending http request
        /// </summary>
        public bool AllowCookies = true;

        public bool AllowRedirect = true;

        public int RedirectMaxCount
        {

            get
            {
                return this._RedirectMaxCount;
            }
            set
            {
                if (value < 1)
                    this._RedirectMaxCount = 1;
                else if (value > 10)
                    this._RedirectMaxCount = 10;
                else
                    this._RedirectMaxCount = value;
            }

        }private int _RedirectMaxCount;

        public int ConnectTimeout
        {
            get
            {
                return this._ConnectTimeout;
            }
            set
            {
                if (value < 1000)
                    this._ConnectTimeout = 1000;
                else if (value > 60000)
                    this._ConnectTimeout = 60000;
                else
                    this._ConnectTimeout = value;
            }

        }private int _ConnectTimeout = _CONNECT_TIMEOUT_DEFAULT;
        public int ResponseTimeout
        {
            get
            {
                return this._ResponseTimeout;
            }
            set
            {
                if (value < 1000)
                    this._ResponseTimeout = 1000;
                else if (value > 60000)
                    this._ResponseTimeout = 60000;
                else
                    this._ResponseTimeout = value;
            }

        }private int _ResponseTimeout = _RESPONSE_TIMEOUT_DEFAULT;

        /// <summary>
        /// Data to be send as POST content.
        /// </summary>
        public byte[] Post;

        /// <summary>
        /// Delegate to get data to be send as POST content.
        /// Return zero if there is no more data to be send.
        /// </summary>
        public BufferHandler PostDataSendHandler = null;

        public string Proxy = null;

        public Utils.OptionEnum AllowSystemProxy = Utils.OptionEnum.Default;
        public static bool AllowSystemProxyDefault
        {
            get
            {
                return _AllowSystemProxyDefault;
            }

            set
            {
                _AllowSystemProxyDefault = value;
                _Logger.Debug("[AllowSystemProxyDefault] " + value);
            }
        }private static bool _AllowSystemProxyDefault = true;

        public NameValueCollection HttpRequestFields;
        public Dictionary<string, string> HttpResponseFields
        {
            get { return this._HttpResponseFields; }
        }private Dictionary<string, string> _HttpResponseFields;

        public HttpStatusCode HttpResponseCode
        {
            get { return this._HttpResponseCode; }
        }private HttpStatusCode _HttpResponseCode;

        public ChunkedStream StreamChunked
        {
            get { return this._StreamChunked; }
        }private ChunkedStream _StreamChunked = null;

        public Utils.OptionEnum UseOpenSSL = Utils.OptionEnum.Default;
        public static bool UseOpenSSLDefault
        {
            get
            {
                return _UseOpenSSLDefault;
            }

            set
            {
                _UseOpenSSLDefault = value;
                _Logger.Debug("[UseOpenSSLDefault] " + value);
            }
        }private static bool _UseOpenSSLDefault = System.Environment.OSVersion.Version.Major < 10;

        public OpenSSL.SSL.SslProtocols OpenSslProtocols = OpenSSL.SSL.SslProtocols.TLS1_2_VERSION;
        public OpenSSL.SSL.SslStrength OpenSslStrength = OpenSSL.SSL.SslStrength.Default;
        public string OpenSslCipherList = null; //custom ciphers: overrides Strength
        public OpenSSL.SSL.RemoteCertificateValidationHandler OpenSslRemoteCertificationCallback = null;
        public OpenSSL.SSL.LocalCertificateSelectionHandler OpenSslLocalCertificationCallback = null;

        public DateTime ModifiedSince
        {
            get { return this._ModifiedSince; }
        } private DateTime _ModifiedSince;

        public HttpMethodEnum Method;

        public bool Abort;

        /// <summary>
        /// Get or Set update interval for download progress report. Default 1000 ms.
        /// </summary>
        public int ProgressUpdateInterval
        {
            get
            {
                return this._ProgressUpdateInterval;
            }

            set
            {
                if (value < 100)
                    this._ProgressUpdateInterval = 100;
                else
                    this._ProgressUpdateInterval = value;
            }
        }private int _ProgressUpdateInterval = 1000;


        public long ResumingFrom
        {
            get
            {
                return this._ResumingFrom;
            }
        }private long _ResumingFrom;

        public long FileLength
        {
            get
            {
                return this._FileLength;
            }
        }private long _FileLength;


        /// <summary>
        /// Returns true if output stream length size is well known, i.e. content lenghth is > 0 and the response stream is not compressed.
        /// </summary>
        public bool IsOutputStreamSizeKnown
        {
            get
            {
                return this._ContentLength > 0 && !this._IsResponseStreamCompressed;
            }
        }

        /// <summary>
        /// Returns true if the response stream end is well known, i.e. content length is > 0 or the response stream is chunked type.
        /// </summary>
        public bool IsResponseStreamEndKnown
        {
            get
            {
                return this._ContentLength > 0 || this._StreamChunked != null;
            }
        }

        /// <summary>
        /// Returns true if the response stream is fully received.
        /// </summary>
        public bool IsResponseStreamEnded
        {
            get
            {
                ChunkedStream stream;

                if (this._IsResponseStreamEnded)
                    return true;
                else if (this._GetResponseResult != GetResponseResultEnum.OK)
                    return false;
                else if ((stream = this._StreamChunked) != null && stream.IsEnded)
                    this._IsResponseStreamEnded = true;
                else if (this._ContentLength > 0 && this._ResponseBytesReceived >= this._ContentLength)
                    this._IsResponseStreamEnded = true;

                return this._IsResponseStreamEnded;
            }
        }private bool _IsResponseStreamEnded;

        /// <summary>
        /// Returns true if the response stream is compressed.
        /// </summary>
        public bool IsResponseStreamCompressed
        {
            get
            {
                return this._IsResponseStreamCompressed;
            }
        }private bool _IsResponseStreamCompressed = false;

        public bool ConnectionKeepAlive
        {
            get
            {
                return this._ConnectionKeepAlive;
            }

            set
            {
                if (!value)
                    this._ConnectionKeepAlive = false;
            }
        }

        public bool Closed
        {
            get { return this._Closed; }
        }private bool _Closed = true;

        /// <summary>
        /// Maximum number of silmultaneous connection to one server(IP).
        /// Unlimited: -1
        /// </summary>
        public static int MaxConnectionsPerServer
        {
            get
            {
                return _Connections.MaxConnectionsPerGroup;
            }

            set
            {
                if (value <= 0)
                    _Connections.MaxConnectionsPerGroup = -1; //unlimited
                else
                    _Connections.MaxConnectionsPerGroup = value;
            }
        }

        /// <summary>
        /// Maximum number of requests per one keep-alive connection.
        /// Unlimited: -1
        /// </summary>
        public static int ConnectionKeepAliveMaxDefault
        {
            get
            {
                return _ConnectionKeepAliveMaxDefault;
            }

            set
            {
                if (value <= 0)
                    _ConnectionKeepAliveMaxDefault = -1; //unlimited
                else
                    _ConnectionKeepAliveMaxDefault = value;
            }
        }private static int _ConnectionKeepAliveMaxDefault = _CONNECTION_KEEP_ALIVE_MAX_DEFAULT;

        /// <summary>
        /// Keep-alive connection timout[s].
        /// </summary>
        public static int ConnectionKeepAliveTimeoutDefault
        {
            get
            {
                return _ConnectionKeepAliveTimeoutDefault;
            }

            set
            {
                if (value < 1)
                    _ConnectionKeepAliveTimeoutDefault = 1;
                else
                    _ConnectionKeepAliveTimeoutDefault = value;
            }
        }private static int _ConnectionKeepAliveTimeoutDefault = _CONNECTION_KEEP_ALIVE_TIMEOUT_DEFAULT;

        /// <summary>
        /// Keep-alive reused connection cross-domain access
        /// </summary>
        public static bool ConnectionKeepAliveAllowCrossDomain
        {
            get
            {
                return _Connections.ConnectionKeepAliveAllowCrossDomain;
            }

            set
            {
                _Connections.ConnectionKeepAliveAllowCrossDomain = value;
            }

        }

        #region ctor
        static HttpUserWebRequest()
        {
            Log.Init();

            using (Settings settings = new MPSettings())
            {
                Utils.OptionEnum option;
                try
                {
                    option = (Utils.OptionEnum)Enum.Parse(typeof(Utils.OptionEnum), settings.GetValueAsString("Pbk.Http", "UseOpenSSL", "Default"), true);
                    _Logger.Debug("[ctor][Config] UseOpenSSL: " + option);
                    switch (option)
                    {
                        case Utils.OptionEnum.Yes:
                            UseOpenSSLDefault = true;
                            break;

                        case Utils.OptionEnum.No:
                            UseOpenSSLDefault = false;
                            break;
                    }

                }
                catch (Exception ex)
                { _Logger.Error("[ctor][Config] Error: " + ex.Message); }

                try
                {
                    option = (Utils.OptionEnum)Enum.Parse(typeof(Utils.OptionEnum), settings.GetValueAsString("Pbk.Http", "AllowSystemProxy", "Default"), true);
                    _Logger.Debug("[ctor][Config] AllowSystemProxy: " + option);
                    switch (option)
                    {
                        case Utils.OptionEnum.Yes:
                            AllowSystemProxyDefault = true;
                            break;

                        case Utils.OptionEnum.No:
                            AllowSystemProxyDefault = false;
                            break;
                    }

                }
                catch (Exception ex)
                { _Logger.Error("[ctor][Config] Error: " + ex.Message); }
            }

        }
        public HttpUserWebRequest()
        {
            this._Id = System.Threading.Interlocked.Increment(ref _IdCounter);

            this.OpenSslRemoteCertificationCallback = this.openSslRemoteCertificationCallback;
            //this.OpenSslLocalCertificationCallback = this.openSslLocalCertificationCallback;
        }
        public HttpUserWebRequest(string strUrl)
            : this()
        {
            this.init(strUrl);
        }
        #endregion

        #region Public methods

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual void Close()
        {
            if (!this._Closed)
            {
                if ((this._ContentLength > 0 && this._ResponseBytesReceived < this._ContentLength) ||
                    (this._ContentLength <= 0 && this._StreamChunked != null && !this._StreamChunked.IsEnded))
                    this._ConnectionKeepAlive = false; //terminate all streams

                try
                {
                    _Connections.ReleaseConnection(this);

                    if (this._Stream != null && this._Stream != this._StreamSource)
                        this._Stream.Close(); //Terminate all unused streams
                }
                catch (Exception ex)
                {
                    if (this._StreamSource != null)
                        this._StreamSource.Close();

                    throw ex;
                }
                finally
                {
                    this._Stream = null;
                    this._StreamSource = null;
                    this._TcpClient = null;
                    this._StreamChunked = null;

                    this._Closed = true;
                }
            }
        }

        public static T Download<T>(string strUrl)
        {
            using (HttpUserWebRequest wr = new HttpUserWebRequest(strUrl))
            {
                return wr.Download<T>();
            }
        }
        public static T Download<T>(
            string strUrl,
            HttpMethodEnum method = HttpMethodEnum.GET,
            Encoding enc = null,
            NameValueCollection httpRequestFields = null,
            CookieContainer cookies = null,
            byte[] post = null,
            BufferHandler postDataSendHandler = null,
            string strContentType = null,
            string strUserAgent = null,
            string strReferer = null,
            string strAccept = null,
            string strAcceptLanguage = null,
            string strProxy = null,
            Utils.OptionEnum useOpenSSL = Utils.OptionEnum.Default,
            bool bAllowRedirect = true,
            Utils.OptionEnum allowSystemProxy = Utils.OptionEnum.Default,
            bool bAllowKeepAlive = true,
            bool bAllowCookies = true,
            int iConnectTimout = _CONNECT_TIMEOUT_DEFAULT,
            int iResponseTimout = _RESPONSE_TIMEOUT_DEFAULT
            )
        {
            using (HttpUserWebRequest wr = new HttpUserWebRequest(strUrl))
            {
                wr.UserAgent = !string.IsNullOrWhiteSpace(strUserAgent) ? strUserAgent : HttpHeaderField.HTTP_DEFAULT_USER_AGENT;
                wr.Accept = !string.IsNullOrWhiteSpace(strAccept) ? strAccept : HttpHeaderField.HTTP_DEFAULT_ACCEPT;
                wr.AcceptLanguage = !string.IsNullOrWhiteSpace(strAcceptLanguage) ? strAcceptLanguage : HttpHeaderField.HTTP_DEFAULT_ACCEPT_LANGUAGE;
                wr.ContentType = strContentType;
                wr.Referer = strReferer;
                wr.Post = post;
                wr.PostDataSendHandler = postDataSendHandler;
                wr.AllowSystemProxy = allowSystemProxy;
                wr.AllowRedirect = bAllowRedirect;
                wr.HttpRequestFields = httpRequestFields;
                wr.UseOpenSSL = useOpenSSL;
                wr.Cookies = cookies;
                wr.Proxy = strProxy;
                wr.ConnectTimeout = iConnectTimout;
                wr.ResponseTimeout = iResponseTimout;
                wr.Method = method;
                wr.AllowKeepAlive = bAllowKeepAlive;
                wr.AllowCookies = bAllowCookies;
                return wr.Download<T>(enc == null ? Encoding.UTF8 : enc);
            }
        }
        public static T Download<T>(
            string strUrl,
            out string strRedirect,
            out HttpStatusCode httpResult,
            HttpMethodEnum method = HttpMethodEnum.GET,
            Encoding enc = null,
            NameValueCollection httpRequestFields = null,
            CookieContainer cookies = null,
            byte[] post = null,
            BufferHandler postDataSendHandler = null,
            string strContentType = null,
            string strUserAgent = null,
            string strReferer = null,
            string strAccept = null,
            string strAcceptLanguage = null,
            string strProxy = null,
            Utils.OptionEnum useOpenSSL = Utils.OptionEnum.Default,
            bool bAllowRedirect = true,
            Utils.OptionEnum allowSystemProxy = Utils.OptionEnum.Default,
            bool bAllowKeepAlive = true,
            bool bAllowCookies = true,
            int iConnectTimout = _CONNECT_TIMEOUT_DEFAULT,
            int iResponseTimout = _RESPONSE_TIMEOUT_DEFAULT)
        {
            using (HttpUserWebRequest wr = new HttpUserWebRequest(strUrl))
            {
                wr.UserAgent = !string.IsNullOrWhiteSpace(strUserAgent) ? strUserAgent : HttpHeaderField.HTTP_DEFAULT_USER_AGENT;
                wr.Accept = !string.IsNullOrWhiteSpace(strAccept) ? strAccept : HttpHeaderField.HTTP_DEFAULT_ACCEPT;
                wr.AcceptLanguage = !string.IsNullOrWhiteSpace(strAcceptLanguage) ? strAcceptLanguage : HttpHeaderField.HTTP_DEFAULT_ACCEPT_LANGUAGE;
                wr.ContentType = strContentType;
                wr.Referer = strReferer;
                wr.Post = post;
                wr.PostDataSendHandler = postDataSendHandler;
                wr.AllowSystemProxy = allowSystemProxy;
                wr.AllowRedirect = bAllowRedirect;
                wr.HttpRequestFields = httpRequestFields;
                wr.UseOpenSSL = useOpenSSL;
                wr.Cookies = cookies;
                wr.Proxy = strProxy;
                wr.ConnectTimeout = iConnectTimout;
                wr.ResponseTimeout = iResponseTimout;
                wr.Method = method;
                wr.AllowKeepAlive = bAllowKeepAlive;
                wr.AllowCookies = bAllowCookies;
                object o = wr.Download<T>(enc == null ? Encoding.UTF8 : enc);
                httpResult = wr._HttpResponseCode;
                strRedirect = wr._ServerUrlRedirect;
                return (T)o;
            }
        }

        public virtual T Download<T>()
        {
            return this.Download<T>(Encoding.UTF8);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual T Download<T>(Encoding enc)
        {
            T result;
            while (true)
            {
                _Logger.Debug("[{0}][Download<T>] Url: {1}", this._Id, this._ServerUrl);

                result = this.download<T>(enc);

                if (this._GetResponseResult == GetResponseResultEnum.OK && this._Redirect && this.AllowRedirect
                    && !string.IsNullOrEmpty(this._ServerUrlRedirect))
                {
                    if (this._RedirectMaxCount-- > 0)
                    {
                        //Redirect
                        this._ServerUri = new Uri(this._ServerUrlRedirect);
                        this._ServerUrl = this._ServerUrlRedirect;
                        continue;
                    }
                    else
                        _Logger.Warn("[{0}][Download<T>] Max. redirect count reached. Url: {1}", this._Id, this._ServerUrl);
                }

                break;
            }

            return result;
        }

        public virtual bool DownloadFile(string strPath)
        {
            return this.DownloadFile(strPath, false, -1, -1, -1, true);
        }
        public virtual bool DownloadFile(string strPath, bool bDeleteFileOnError)
        {
            return this.DownloadFile(strPath, false, -1, -1, -1, bDeleteFileOnError);
        }
        public virtual bool DownloadFile(string strPath, long lResumeFrom, bool bDeleteFileOnError)
        {
            return this.DownloadFile(strPath, true, lResumeFrom, -1, -1, bDeleteFileOnError);
        }
        public virtual bool DownloadFile(string strPath, long lResumeFrom, long lResumeLength, bool bDeleteFileOnError)
        {
            return this.DownloadFile(strPath, true, lResumeFrom, lResumeLength, -1, bDeleteFileOnError);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual bool DownloadFile(string strPath, bool bForceDownload, long lResumeFrom, long lResumeLength, long lFileOffset, bool bDeleteFileOnError)
        {
            this._FileOffset = lFileOffset;
            bool bResult = false;
            try
            {
                if (!bForceDownload && File.Exists(strPath) && lResumeFrom < 0)
                {
                    FileInfo fi = new FileInfo(strPath);
                    this._ModifiedSince = fi.CreationTime;
                }

                if (lResumeFrom > 0)
                {
                    this._ResumeFrom = lResumeFrom;
                    if (lResumeLength > 0)
                        this._ResumeLength = lResumeLength;
                }

                if (this.getResponseStream() == null || (this._HttpResponseCode != HttpStatusCode.OK && this._HttpResponseCode != HttpStatusCode.PartialContent))
                {
                    if (this._Stream != null)
                    {
                        _Logger.Debug("[{0}][DownloadFile] Flushing stream... Url: {1}", this._Id, this._ServerUrl);

                        if (!this.flushStream())
                            return false;
                    }

                    if (this._HttpResponseCode == HttpStatusCode.NotModified)
                    {
                        _Logger.Debug("[{0}][DownloadFile] File Not Modified. Url: {1}", this._Id, this._ServerUrl);
                        return true;
                    }

                    _Logger.Error("[{0}][DownloadFile] Failed to get the stream. Url: {1}", this._Id, this._ServerUrl);
                    return false;
                }

                #region  Event: Before save to file (call the event to get filename)
                if (this.BeforeSaveToFile != null)
                {
                    try
                    {
                        HttpUserWebBeforeSaveToFileEventArgs args = new HttpUserWebBeforeSaveToFileEventArgs() { FileNameFullPath = strPath };
                        this.BeforeSaveToFile(this, args);
                        if (string.IsNullOrWhiteSpace(args.FileNameFullPath))
                        {
                            _Logger.Error("[{0}][getResponseStream] Before save to file: invalid filename '{1}'", this._Id, this.Url);
                            return false;
                        }
                        else
                            strPath = args.FileNameFullPath;
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[{3}][getResponseStream] Error onEvent BeforeSaveToFile: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                    }
                }
                #endregion

                using (FileStream fs = new FileStream(strPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    if (this._FileOffset > 0)
                    {
                        fs.Seek(this._FileOffset, SeekOrigin.Begin);
                        _Logger.Debug("[{0}][DownloadFile] File offset initialised: {1}  Filepath: '{2}'", this._Id, this._FileOffset, strPath);
                    }

                    bResult = this.readStream(fs);
                    fs.Flush(true);
                }

                if (bResult)
                {
                    _Logger.Debug("[{0}][downloadFile] Result: ContentLength:{1} Received:{2} Written:{3} ConnectionChunked:{4} Compressed:{5}",
                        this._Id,
                        this._ContentLength, this._ResponseBytesReceived, this._TotalBytesWritten, this._ConnectionChunked, this._IsResponseStreamCompressed
                        );
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][DownloadFile] Error: {0} {1} {2}\r\nState: ContentLength:{4} Received:{5} Written:{6} ConnectionChunked:{7}{9} Compressed:{8}",
                    ex.Message, ex.Source, ex.StackTrace, this._Id,
                    this._ContentLength, this._ResponseBytesReceived, this._TotalBytesWritten, this._ConnectionChunked, this._IsResponseStreamCompressed,
                    this._StreamChunked != null ? " ChunkedStreamEnded:" + this._StreamChunked.IsEnded : null);
                this._ConnectionKeepAlive = false;
            }
            finally
            {
                string str = this._StreamChunked != null ? " ChunkedStreamEnded:" + this._StreamChunked.IsEnded : null;

                try { this.Close(); }
                catch (Exception ex)
                {
                    _Logger.Error("[{3}][download] Error: {0} {1} {2}\r\nState: ContentLength:{4} Received:{5} Written:{6} ConnectionChunked:{7}{9} Compressed:{8}",
                        ex.Message, ex.Source, ex.StackTrace, this._Id,
                        this._ContentLength, this._ResponseBytesReceived, this._TotalBytesWritten, this._ConnectionChunked, this._IsResponseStreamCompressed,
                        str);

                    bResult = false;
                    this._ConnectionKeepAlive = false;
                }
            }

            if (!bResult && bDeleteFileOnError && File.Exists(strPath))
            {
                try { File.Delete(strPath); }
                catch { }
            }

            return bResult;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual Stream GetResponseStream()
        {
            if (this.getResponseStream() != null)
                return new HttpUserWebResponseStream(this._Stream, this);
            else
                return null;
        }

        //public static XmlDocument LoadXmlFromHtml(string strContent)
        //{
        //    SgmlReader sgmlReader = null;
        //    TextReader txreader = null;
        //    MemoryStream mem_stream = null;
        //    XmlTextReader xmltxtrd = null;
        //    try
        //    {
        //        //_logger.Debug(string.Format("[LoadHtml]"));
        //        // setup SgmlReader
        //        sgmlReader = new SgmlReader();
        //        sgmlReader.DocType = "HTML";
        //        sgmlReader.WhitespaceHandling = WhitespaceHandling.All;
        //        sgmlReader.CaseFolding = CaseFolding.ToLower;
        //        txreader = new StringReader(strContent);
        //        sgmlReader.InputStream = txreader;
        //        //sgmlReader.Read();

        //        // create document
        //        XmlDocument xmldoc = new XmlDocument();
        //        xmldoc.PreserveWhitespace = false;
        //        xmldoc.XmlResolver = null;
        //        xmldoc.Load(sgmlReader);

        //        txreader.Close();
        //        txreader.Dispose();
        //        txreader = null;

        //        // I need to "reload" xml via XmlTextReader to ignore namespace
        //        mem_stream = new MemoryStream();
        //        xmldoc.Save(new XmlTextWriter(mem_stream, null));
        //        mem_stream.Position = 0;
        //        xmltxtrd = new XmlTextReader(mem_stream);
        //        xmltxtrd.Namespaces = false;
        //        xmldoc.Load(xmltxtrd);

        //        return xmldoc;

        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error(string.Format("[loadHtml] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace));
        //        return null;
        //    }
        //    finally
        //    {
        //        if (sgmlReader != null) sgmlReader.Close();
        //        if (xmltxtrd != null) xmltxtrd.Close();
        //        if (mem_stream != null)
        //        {
        //            mem_stream.Close();
        //            mem_stream.Dispose();
        //            mem_stream = null;
        //        }

        //        if (txreader != null)
        //        {
        //            txreader.Close();
        //            txreader.Dispose();
        //            txreader = null;
        //        }
        //    }

        //}

        public static int GetHttpResponse(Uri uri, byte[] buffer, int iOffset, int iLength, ref Dictionary<string, string> httpResponseFields,
            ref CookieContainer cookies, out HttpStatusCode httpStatusCode, out double dVersion)
        {
            httpStatusCode = 0;
            dVersion = 0.0;

            if (buffer == null)
                return -1;

            if (httpResponseFields == null)
                httpResponseFields = new Dictionary<string, string>();
            else
                httpResponseFields.Clear();

            if (cookies == null)
                cookies = new CookieContainer();

            bool bHttpOK = false;
            bool bHttpStatusAcquired = false;

            int iPosIdx = iOffset;

            int iHttpLength = 0;
            int iLineStartIdx = iOffset;

            int iResult = 0;

            //Get the bytes from the buffer one by one
            while (iPosIdx < buffer.Length && iHttpLength < iLength)
            {
                char chr = (char)buffer[iPosIdx++];
                iHttpLength++;

                //Check HTTP start
                if (!bHttpOK)
                {
                    if (iHttpLength == 5)
                    {
                        if (Encoding.ASCII.GetString(buffer, iOffset, 5) != "HTTP/")
                        {
                            _Logger.Error("[GetHttpResponse] Invalid HTTP response:\r\n{0}", Encoding.ASCII.GetString(buffer, iOffset, iLength - iOffset));
                            return -1;
                        }
                        else
                            bHttpOK = true;
                    }
                    continue;
                }
                else if (chr == '\n') //End of line
                {
                    //HTTP header end
                    if (buffer[iPosIdx - 2] == '\n' || // \n\n
                        (buffer[iPosIdx - 4] == '\r' && buffer[iPosIdx - 3] == '\n' && buffer[iPosIdx - 2] == '\r'))
                    {
                        iResult = 1;
                        break; // \r\n\r\n
                    }

                    //Get the line
                    string strLine = Encoding.UTF8.GetString(buffer, iLineStartIdx, iPosIdx - iLineStartIdx).Trim();
                    iLineStartIdx = iPosIdx; //set new line

                    Match match;
                    if (!bHttpStatusAcquired)
                    {
                        //HTTP response code
                        match = _RegexHttpFieldStatus.Match(strLine);
                        if (match.Success)
                        {
                            httpStatusCode = (HttpStatusCode)int.Parse(match.Groups["code"].Value);
                            dVersion = double.Parse(match.Groups["version"].Value, System.Globalization.CultureInfo.CreateSpecificCulture("en-US"));
                            bHttpStatusAcquired = true;
                        }
                        else
                        {
                            _Logger.Error("[GetHttpResponse] Invalid response status line: '{0}'", strLine);
                            return -1;
                        }
                    }
                    else
                    {
                        //Get header field
                        match = _RegexHttpField.Match(strLine);
                        if (match.Success)
                        {
                            if (match.Groups["value"].Success)
                            {
                                string strKey = match.Groups["key"].Value.Trim().ToLower();

                                //Format
                                char[] chars = new char[strKey.Length];
                                bool bStart = true;
                                for (int iIdx = 0; iIdx < strKey.Length; iIdx++)
                                {
                                    if (bStart)
                                    {
                                        bStart = false;
                                        chars[iIdx] = Char.ToUpper(strKey[iIdx]);
                                        continue;
                                    }
                                    else if (strKey[iIdx] == '-')
                                        bStart = true;

                                    chars[iIdx] = strKey[iIdx];
                                }

                                strKey = new String(chars);

                                if (strKey == "Set-Cookie")
                                    cookies.Add(uri, parseCookie(match.Groups["value"].Value.Trim()));
                                else if (!httpResponseFields.ContainsKey(strKey))
                                    httpResponseFields.Add(strKey, match.Groups["value"].Value.Trim());
                            }
                        }
                        else
                            _Logger.Error("[GetHttpResponse] Invalid line: '{0}'", strLine);
                    }

                }

                //Check the max length
                if (iHttpLength > 8192)
                {
                    _Logger.Error("[GetHttpResponse] Bad HTTP response - header too long.");
                    return -1;
                }
            }

            if (iResult == 1)
                return iHttpLength;
            else
                return 0;
        }

        public static int GetHttpRequest(byte[] buffer, int iOffset, int iLength, ref Dictionary<string, string> httpRequestFields, ref CookieContainer cookies,
            out HttpMethodEnum httpMethod, out string strPath)
        {
            httpMethod = HttpMethodEnum.UNKNOWN;
            strPath = "";

            if (buffer == null || buffer.Length < 8)
                return 0;

            if (httpRequestFields == null)
                httpRequestFields = new Dictionary<string, string>();
            else
                httpRequestFields.Clear();

            bool bHttpOK = false;
            bool bHttpRqAcquired = false;

            int iPosIdx = iOffset;

            int iHttpLength = 0;
            int iLineStartIdx = iOffset;

            int iResult = 0;

            //Get the bytes from the buffer one by one
            while (iPosIdx < buffer.Length && iHttpLength < iLength)
            {
                char chr = (char)buffer[iPosIdx++];
                iHttpLength++;

                //Check HTTP start
                if (!bHttpOK)
                {
                    if (iHttpLength == 8)
                    {
                        string strStart = Encoding.ASCII.GetString(buffer, iOffset, 8);
                        if (!strStart.StartsWith("GET ")
                            && !strStart.StartsWith("POST ")
                            && !strStart.StartsWith("HEAD ")
                            && !strStart.StartsWith("OPTIONS ")
                            && !strStart.StartsWith("PUT ")
                            && !strStart.StartsWith("DELETE ")
                            && !strStart.StartsWith("CONNECT ")
                            && !strStart.StartsWith("PATCH ")
                            && !strStart.StartsWith("TRACE ")
                            )
                        {
                            _Logger.Error("[GetHttpRequest] Invalid HTTP request.");
                            return -1;
                        }
                        else
                            bHttpOK = true;
                    }
                    continue;
                }
                else if (chr == '\n') //End of line
                {
                    //HTTP header end
                    if (buffer[iPosIdx - 2] == '\n' || // \n\n
                        (buffer[iPosIdx - 4] == '\r' && buffer[iPosIdx - 3] == '\n' && buffer[iPosIdx - 2] == '\r'))
                    {
                        iResult = 1;
                        break; // \r\n\r\n
                    }

                    //Get the line
                    string strLine = Encoding.ASCII.GetString(buffer, iLineStartIdx, iPosIdx - iLineStartIdx).Trim();
                    iLineStartIdx = iPosIdx; //set new line

                    Match match;
                    if (!bHttpRqAcquired)
                    {
                        match = _RegexHttpFieldMethod.Match(strLine);
                        if (match.Success)
                        {
                            httpMethod = (HttpMethodEnum)Enum.Parse(typeof(HttpMethodEnum), match.Groups["type"].Value.Trim());
                            strPath = match.Groups["path"].Value.Trim();
                            bHttpRqAcquired = true;
                        }
                        else
                        {
                            _Logger.Error("[GetHttpRequest] Invalid Request status line:" + strLine);
                            return -1;
                        }
                    }
                    else
                    {
                        //Get header field
                        match = _RegexHttpField.Match(strLine);
                        if (match.Success)
                        {
                            if (match.Groups["value"].Success)
                            {
                                string strKey = match.Groups["key"].Value.Trim();

                                //Format
                                char[] chars = new char[strKey.Length];
                                bool bStart = true;
                                for (int iIdx = 0; iIdx < strKey.Length; iIdx++)
                                {
                                    if (bStart)
                                    {
                                        bStart = false;
                                        chars[iIdx] = Char.ToUpper(strKey[iIdx]);
                                        continue;
                                    }
                                    else if (strKey[iIdx] == '-')
                                        bStart = true;

                                    chars[iIdx] = strKey[iIdx];
                                }

                                strKey = new String(chars);

                                if (!httpRequestFields.ContainsKey(strKey))
                                    httpRequestFields.Add(strKey, match.Groups["value"].Value.Trim());
                            }
                        }
                        else
                            _Logger.Error("[GetHttpRequest] Invalid line: '{0}'", strLine);
                    }

                }

                //Check the max length
                if (iHttpLength > 8192)
                {
                    _Logger.Error("[GetHttpRequest] Bad HTTP response - header too long.");
                    return -1;
                }
            }

            if (iResult == 1)
            {
                string strHost;
                string strCookie;
                if (httpRequestFields.TryGetValue("Host", out strHost) && httpRequestFields.TryGetValue("Cookie", out strCookie))
                {
                    if (cookies == null)
                        cookies = new CookieContainer();

                    Uri uri = new Uri(strPath.StartsWith("/") ? "http://" + strHost + strPath : strPath);

                    MatchCollection mc = _RegexHttpFieldCookie.Matches(strCookie);
                    foreach (Match match in mc)
                    {
                        cookies.Add(uri, new Cookie(match.Groups["key"].Value.Trim(), match.Groups["value"].Value.Trim()));
                    }
                }

                return iHttpLength;
            }

            return 0;
        }

        public static string GetHttpFilename(string strField)
        {
            //Content-Disposition: attachment; filename*=UTF-8''test.mp3
            // title*=UTF-8''%c2%a3%20and%20%e2%82%ac%20rates
            // title*=iso-8859-1'en'%A3%20rates

            string strFilename = null;
            string[] vars = strField.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string strVar in vars)
            {
                try
                {
                    string s = strVar.TrimStart();
                    int i = s.IndexOf('=');
                    if (i > 0)
                    {
                        if (s.StartsWith("filename*", StringComparison.CurrentCultureIgnoreCase))
                        {
                            string strFnTmp = s.Substring(i + 1);
                            i = strFnTmp.IndexOf('\'');
                            if (i >= 0)
                            {
                                string strEnc = strFnTmp.Substring(0, i);
                                int i2 = strFnTmp.IndexOf('\'', i + 1);
                                if (i2 >= 0)
                                {
                                    string strLng = strFnTmp.Substring(i + 1, i2 - i - 1);
                                    strFnTmp = strFnTmp.Substring(i2 + 1);
                                    byte[] data = new byte[strFnTmp.Length];

                                    //Get bytes to decode
                                    i = 0;
                                    int iDataLength = 0;
                                    while (i < strFnTmp.Length)
                                    {
                                        if (strFnTmp[i] == '%')
                                        {
                                            data[iDataLength++] = byte.Parse(strFnTmp.Substring(i + 1, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
                                            i += 3;
                                        }
                                        else
                                            data[iDataLength++] = (byte)strFnTmp[i++];

                                    }

                                    if (iDataLength > 0)
                                    {
                                        if (!string.IsNullOrEmpty(strLng))
                                        {
                                            //?
                                            strFilename = Encoding.GetEncoding(strEnc).GetString(data, 0, iDataLength);
                                        }
                                        else
                                            strFilename = Encoding.GetEncoding(strEnc).GetString(data, 0, iDataLength);

                                        break;

                                    }
                                }
                            }

                        }
                        else if (s.StartsWith("filename", StringComparison.CurrentCultureIgnoreCase))
                        {
                            strFilename = s.Substring(i + 1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    //_Logger.Error("[GetHttpFilename] Error parsing filename: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    return null;
                }
            }

            return strFilename != null ? strFilename.Trim('\"') : null;
        }

        public void Dispose()
        {
            this.Close();
        }

        public void SetHttpRequestFieldRange(long lFrom)
        {
            this.SetHttpRequestFieldRange(lFrom, -1);
        }
        public void SetHttpRequestFieldRange(long lFrom, long lTo)
        {
            if (this.HttpRequestFields == null)
                this.HttpRequestFields = new System.Collections.Specialized.NameValueCollection();

            this.HttpRequestFields.Add(HttpHeaderField.HTTP_FIELD_RANGE, "bytes=" + lFrom + '-' + (lTo > lFrom ? lTo.ToString() : null));
        }


        public bool TryGetHttpResponseFieldContentRange(out long lFrom)
        {
            long lTo, lLength;
            return TryGetHttpResponseFieldContentRange(out lFrom, out lTo, out lLength);
        }
        public bool TryGetHttpResponseFieldContentRange(out long lFrom, out long lTo, out long lLength)
        {
            lFrom = -1;
            lTo = -1;
            lLength = -1;
            string strContentRange;
            if (this.HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_CONTENT_RANGE, out strContentRange)
                && !string.IsNullOrEmpty(strContentRange))
            {
                Regex regex = new Regex(REGEX_CONTENT_RANGE);
                Match m = regex.Match(strContentRange);
                if (m.Success)
                {
                    lFrom = long.Parse(m.Groups["from"].Value);
                    lTo = long.Parse(m.Groups["to"].Value);
                    lLength = long.Parse(m.Groups["length"].Value);
                    return true;
                }
                else
                {
                    this._ConnectionKeepAlive = false;
                    _Logger.Error("[{1}][TryGetHttpResponseFieldContentRange] Invalid Content-Range field: '0'", strContentRange, this._Id);
                }
            }

            return false;
        }

        public static bool TryGetHttpRequestFieldRange(Dictionary<string, string> httpFileds, out long lFrom, out long lTo)
        {
            lFrom = -1;
            lTo = -1;
            string strRange;
            if (httpFileds.TryGetValue(HttpHeaderField.HTTP_FIELD_RANGE, out strRange)
                && !string.IsNullOrEmpty(strRange))
            {
                Regex regex = new Regex(REGEX_RANGE);
                Match m = regex.Match(strRange);
                if (m.Success)
                {
                    lFrom = long.Parse(m.Groups["from"].Value);

                    if (m.Groups["to"].Success)
                        lTo = long.Parse(m.Groups["to"].Value);

                    return true;
                }
                else
                    _Logger.Error("[TryGetHttpRequestFieldRange] Invalid Range field: '0'", strRange);
            }

            return false;
        }

        public static bool TryGetHttpResponseFieldContentRange(Dictionary<string, string> httpFileds, out long lFrom, out long lTo, out long lLength)
        {
            lFrom = -1;
            lTo = -1;
            lLength = -1;
            string strContentRange;
            if (httpFileds.TryGetValue(HttpHeaderField.HTTP_FIELD_CONTENT_RANGE, out strContentRange)
                && !string.IsNullOrEmpty(strContentRange))
            {
                Regex regex = new Regex(REGEX_CONTENT_RANGE);
                Match m = regex.Match(strContentRange);
                if (m.Success)
                {
                    lFrom = long.Parse(m.Groups["from"].Value);
                    lTo = long.Parse(m.Groups["to"].Value);
                    lLength = long.Parse(m.Groups["length"].Value);
                    return true;
                }
                else
                    _Logger.Error("[TryGetHttpResponseFieldContentRange] Invalid Content-Range field: '0'", strContentRange);
            }

            return false;
        }

        #endregion

        #region Private methods
        private void init(string strUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(strUrl))
                {
                    _Logger.Error("[{0}][init] Invalid url", this._Id);
                    throw new ArgumentNullException("Invalid url");
                }

                if (strUrl.StartsWith("//"))
                    strUrl = "https:" + strUrl;
                else if (!strUrl.StartsWith("http://", StringComparison.CurrentCultureIgnoreCase)
                    && !strUrl.StartsWith("https://", StringComparison.CurrentCultureIgnoreCase))
                {
                    _Logger.Error("[{0}][init] Invalid url: {1}", this._Id, strUrl);
                    throw new ArgumentException("Invalid url: " + strUrl);
                }

                this._ServerUri = new Uri(strUrl);
            }
            catch
            {
                _Logger.Error("[{0}][init] Invalid url", this._Id);
                throw new ArgumentException("Invalid url");
            }

            this.Abort = false;
            this.Cookies = null;
            this._ConnectionChunked = false;
            this._ConnectionKeepAlive = false;
            this._ServerIpEndpoint = null;
            this._ServerAutority = null;
            this._ResumeFrom = -1;
            this._ResumeLength = -1;
            this._FileOffset = -1;
            this._DownloadProgressUpdateTs = DateTime.MinValue;
            this._StreamReadAttempts = 0;
            this._ConnectionKeepAliveTimeout = _ConnectionKeepAliveTimeoutDefault;
            this._ConnectionKeepAliveMax = -1;
            this._ReRequestAttempts = 3;
            this._ResumingFrom = -1;
            this._FileLength = -1;
            this._ContentLength = -1;
            this._ResponseBytesReceived = 0;
            this._TotalBytesWritten = 0;
            this._FileStreamPosition = 0;
            this._Redirect = false;
            this._RedirectMaxCount = 10;
            this._ContentFilename = null;
            this.Post = null;
            this.HttpRequestFields = null;
            this._HttpResponseFields = null;
            this._HttpResponseCode = 0;
            this._ServerUrlRedirect = null;
            this.Referer = null;
            this.ContentType = null;
            this._ModifiedSince = DateTime.MinValue;
            this.Method = HttpMethodEnum.GET;
            this._GetResponseResult = GetResponseResultEnum.None;
            this._ServerUrl = strUrl;
            this._IsResponseStreamEnded = false;
        }

        private T download<T>(Encoding enc)
        {
            this._HttpResponseCode = 0;
  
            try
            {
                //Get response stream
                while (true)
                {
                    if (this.getResponseStreamProcess() == null)
                    {
                        if ((this._GetResponseResult == GetResponseResultEnum.ErrorRemoteSocketClosed
                            || this._GetResponseResult == GetResponseResultEnum.ErrorOtherConnectionRequired
                            || this._GetResponseResult == GetResponseResultEnum.ErrorFailedConnect)
                            && --this._ReRequestAttempts > 0)
                            continue; //try again
                        else
                            return (T)(object)null;
                    }
                    break;
                }

                int iReceived = 0;
                int iCnt;
                int iBufferCurrentWritten = 0;
                this._TotalBytesWritten = 0;
                byte[] buffer;
                List<byte[]> buffers = null;

                const int _BUFFER_SEGMENT_SIZE = 16 * 1024;

                if (this._ContentLength == 0)
                {
                    //Empty response content length
                    this.Close();
                    return (T)(object)null;
                }
                else if (this.IsOutputStreamSizeKnown)
                {
                    //Exact size
                    buffer = new byte[this._ContentLength];
                }
                else
                {
                    //Unknown size
                    buffers = new List<byte[]>();
                    buffer = this._BufferReceive;
                }

                IAsyncResult ia;

                try
                {
                    //_logger.Debug("[Download] Begin read from stream. Url:{0}", this._ServerUrl);

                    HttpUserWebRequestEventArgs args = new HttpUserWebRequestEventArgs() { Type = HttpUserWebRequestEventType.DownloadProgressUpdate };

                    while (!this.Abort)
                    {
                        if (this._TcpClient == null || !this._TcpClient.Connected)
                        {
                            if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][download] Socket closed. Url: {1}", this._Id, this._ServerUrl);
                            break;
                        }

                        //Read stream
                        ia = this._Stream.BeginRead(buffer, iReceived, buffer.Length - iReceived, null, null);

                        if (!ia.AsyncWaitHandle.WaitOne(this._ResponseTimeout))
                        {
                            _Logger.Error("[{4}][download] Response timeout. Content-Length:{0} ContentBytesReceived:{1} ReadAttempts:{2} Url: {3}",
                                this._ContentLength, this._ResponseBytesReceived, this._StreamReadAttempts, this._ServerUrl, this._Id);
                            this._ConnectionKeepAlive = false;
                            return (T)(object)null;
                        }

                        if ((iCnt = this._Stream.EndRead(ia)) > 0)
                        {
                            //Total counter
                            this._TotalBytesWritten += iCnt;

                            //Current buffer size
                            iReceived += iCnt;

                            if (buffers != null)
                            {
                                //Buffer full check
                                if (iReceived == buffer.Length)
                                {
                                    buffers.Add(buffer);
                                    buffer = new byte[_BUFFER_SEGMENT_SIZE];
                                    iReceived = 0;
                                }
                                
                                //Remember current buffer size
                                iBufferCurrentWritten = iReceived;
                            }

                            //Report progress
                            if ((DateTime.Now - this._DownloadProgressUpdateTs).TotalMilliseconds > this._ProgressUpdateInterval)
                            {
                                if (this.Event != null)
                                {
                                    try { this.Event(this, args); }
                                    catch { }
                                }

                                this._DownloadProgressUpdateTs = DateTime.Now;
                            }
                        }
                        else if (this.IsResponseStreamEnded)
                            break;
                        else if (this.IsResponseStreamEndKnown)
                        {
                            _Logger.Error("[{3}][download] Socket closed. Content-Length:{0} ContentBytesReceived:{1} Url: {2}",
                                this._ContentLength, this._ResponseBytesReceived, this._ServerUrl, this._Id);
                            this._ConnectionKeepAlive = false;
                            return (T)(object)null;
                        }
                        else //Unknown length
                            break;

                    }

                    if (this.Abort)
                    {
                        _Logger.Warn("[{0}][download] Abort.", this._Id);
                        this._ConnectionKeepAlive = false;
                        return (T)(object)null;
                    }

                    _Logger.Debug("[{0}][download] Result: ContentLength:{1} Received:{2} Written:{3} ConnectionChunked:{4} Compressed:{5}",
                        this._Id,
                        this._ContentLength, this._ResponseBytesReceived, this._TotalBytesWritten, this._ConnectionChunked, this._IsResponseStreamCompressed
                        );

                }
                catch (SocketException ex)
                {
                    this._ConnectionKeepAlive = false;

                    if (((SocketException)ex).ErrorCode == 0x00002745 || ((SocketException)ex).ErrorCode == 0x00002749)
                    {
                        //WSAECONNABORTED(10053) Software caused connection abort. 
                        //WSAECONNRESET  (10054) Connection reset by peer.
                        //WSAENOTCONN    (10057) Socket is not connected.

                        if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][download] Socket closed. Url: {1}", this._Id, this._ServerUrl);
                    }
                    else
                    {
                        _Logger.Error("[{0}][download] Socket exception: {1}", this._Id, ((SocketException)ex).ErrorCode);
                        return (T)(object)null;
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Error("[{3}][download] Error: {0} {1} {2}\r\nState: ContentLength:{4} Received:{5} Written:{6} ConnectionChunked:{7}{9} Compressed:{8}",
                        ex.Message, ex.Source, ex.StackTrace, this._Id,
                        this._ContentLength, this._ResponseBytesReceived, this._TotalBytesWritten, this._ConnectionChunked, this._IsResponseStreamCompressed,
                        this._StreamChunked != null ? " ChunkedStreamEnded:" + this._StreamChunked.IsEnded : null);

                    this._ConnectionKeepAlive = false;
                    return (T)(object)null;
                }
                finally
                {
                    string str = this._StreamChunked != null ? " ChunkedStreamEnded:" + this._StreamChunked.IsEnded : null;

                    try { this.Close(); }
                    catch (Exception ex)
                    {
                        _Logger.Error("[{3}][download] Error: {0} {1} {2}\r\nState: ContentLength:{4} Received:{5} Written:{6} ConnectionChunked:{7}{9} Compressed:{8}",
                            ex.Message, ex.Source, ex.StackTrace, this._Id,
                            this._ContentLength, this._ResponseBytesReceived, this._TotalBytesWritten, this._ConnectionChunked, this._IsResponseStreamCompressed,
                            str);

                        this._TotalBytesWritten = 0;
                        this._ConnectionKeepAlive = false;
                        if (buffers != null)
                            buffers = null;
                    }
                }

                if (this._TotalBytesWritten == 0)
                    return (T)(object)null;

                #region Concat all buffers to single one
                if (buffers != null)
                {
                    if (this._TotalBytesWritten != iBufferCurrentWritten || typeof(T) == typeof(byte[]))
                    {
                        if (iBufferCurrentWritten > 0)
                            buffers.Add(buffer); //add last buffer

                        //Final size
                        buffer = new byte[this._TotalBytesWritten];

                        int iPos = 0;
                        byte[] buff;
                        int iLength;

                        for (int i = 0; i < buffers.Count; i++)
                        {
                            //Remaining size
                            iLength = this._TotalBytesWritten - iPos;

                            if (iLength == 0)
                                break; //last buffer is empty

                            buff = buffers[i];

                            if (iLength > buff.Length)
                                iLength = buff.Length;

                            //Copy buffer to the destination
                            Buffer.BlockCopy(buff, 0, buffer, iPos, iLength);

                            //Advance position
                            iPos += iLength;
                        }
                    }

                    buffers = null;
                }
                #endregion

                #region Return requested data type


                string strContentType;
                this.HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_CONTENT_TYPE, out strContentType);

                if (typeof(T) == typeof(string))
                    return (T)(object)enc.GetString(buffer, 0, this._TotalBytesWritten);
                else if (typeof(T) == typeof(byte[]))
                    return (T)(object)buffer;
                else if (typeof(T) == typeof(XmlDocument))
                {
                    XmlDocument xml = new XmlDocument();
                    xml.LoadXml(enc.GetString(buffer, 0, this._TotalBytesWritten));
                    return (T)(object)xml;
                }
                else if (typeof(T) == typeof(HtmlDocument))
                {
                    HtmlDocument html = new HtmlDocument();
                    html.LoadFromHtml(enc.GetString(buffer, 0, this._TotalBytesWritten));
                    return (T)(object)html;
                }
                else if (typeof(T) == typeof(HtmlAgilityPack.HtmlDocument))
                {
                    HtmlAgilityPack.HtmlDocument html = new HtmlAgilityPack.HtmlDocument();
                    html.LoadHtml(enc.GetString(buffer, 0, this._TotalBytesWritten));
                    return (T)(object)html;
                }
                else if (typeof(T) == typeof(JToken))
                    return (T)(object)JsonConvert.DeserializeObject<JToken>(enc.GetString(buffer, 0, this._TotalBytesWritten));
                else if (typeof(T) == typeof(JObject))
                    return (T)(object)JsonConvert.DeserializeObject<JObject>(enc.GetString(buffer, 0, this._TotalBytesWritten));
                else if (typeof(T) == typeof(JContainer))
                    return (T)(object)JsonConvert.DeserializeObject<JContainer>(enc.GetString(buffer, 0, this._TotalBytesWritten));

                if (strContentType != null && strContentType.StartsWith("text/html", StringComparison.CurrentCultureIgnoreCase))
                    return (T)(object)null;

                if (typeof(T) == typeof(System.Drawing.Image))
                {
                    System.Drawing.Image img;
                    using (MemoryStream ms = new MemoryStream(buffer, 0, this._TotalBytesWritten, false))
                    {
                        img = System.Drawing.Image.FromStream(ms);
                    }
                    return (T)(object)img;
                }
                else
                    return (T)(object)null;
                #endregion
            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][download] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                return (T)(object)null;
            }
            finally
            {
                this._BufferReceive = null;
            }
        }

        private bool flushStream()
        {
            return this.readStream(null);
        }
        private bool readStream(FileStream fsDest)
        {
            bool bResult = false;

            this._TotalBytesWritten = 0;
            int iCnt;

            long lToReceive = -1;

            HttpUserWebRequestEventArgs args = new HttpUserWebRequestEventArgs() { Type = HttpUserWebRequestEventType.DownloadProgressUpdate };

            if (fsDest != null)
            {
                #region Content range
                long lTo;
                long lLength;
                if (this._HttpResponseCode == HttpStatusCode.PartialContent && this.TryGetHttpResponseFieldContentRange(out this._ResumingFrom, out lTo, out lLength))
                {
                    if (this._ResumingFrom != this._ResumeFrom || (this._ResumeLength > 0 && this._ResumeLength != this._ContentLength))
                    {
                        _Logger.Error("[{2}][readStream] Resume mismatch. Expected:{0}-{1}", this._ResumeFrom, this._ResumeLength, this._Id);
                        this._GetResponseResult = GetResponseResultEnum.ErrorResumeMismatch;
                        this._ConnectionKeepAlive = false;
                        return false;
                    }

                    lToReceive = lTo + 1 - this._ResumingFrom;

                    this._FileLength = lLength;

                    this._FileStreamPosition = this._ResumingFrom;
                    fsDest.Seek(this._FileStreamPosition, SeekOrigin.Current);

                    _Logger.Debug("[{2}][readStream] Resume: {0}-{1}", this._ResumingFrom, lTo, this._Id);

                }
                else if (this._ResumeFrom > 0)
                {
                    _Logger.Error("[{0}][readStream] Resume not available.", this._Id);
                    this._GetResponseResult = GetResponseResultEnum.ErrorResumeNotAvailable;
                    this._ConnectionKeepAlive = false;
                    return false;
                }
                else
                {
                    this._FileLength = this._ContentLength;

                    if (this._FileOffset < 0)
                        fsDest.SetLength(0);
                }
                #endregion
            }

            while (!this.Abort)
            {
                //Read stream
                IAsyncResult ia = this._Stream.BeginRead(this._BufferReceive, 0, _BUFFER_SIZE, null, null);

                if (!ia.AsyncWaitHandle.WaitOne(this._ResponseTimeout))
                {
                    _Logger.Error("[{3}][readStream] Response timeout. Content-Length:{0} ContentBytesReceived:{1} Url: {2}",
                        this._ContentLength, this._ResponseBytesReceived, this._ServerUrl, this._Id);
                    this._ConnectionKeepAlive = false;
                    break;
                }

                if ((iCnt = this._Stream.EndRead(ia)) > 0)
                {
                    if (fsDest != null)
                    {
                        //Write to file
                        fsDest.Write(this._BufferReceive, 0, iCnt);
                        this._FileStreamPosition += iCnt;

                        //Report progress
                        if ((DateTime.Now - this._DownloadProgressUpdateTs).TotalMilliseconds >= this._ProgressUpdateInterval)
                        {
                            if (this.Event != null)
                            {
                                try { this.Event(this, args); }
                                catch { }
                            }

                            this._DownloadProgressUpdateTs = DateTime.Now;
                        }
                    }

                    this._TotalBytesWritten += iCnt;

                    //if (this.IsResponseStreamEndKnown && this._ContentLength == this._ResponseBytesReceived && !this.IsResponseStreamCompressed)
                    //{
                    //    //Response received
                    //    bResult = true;
                    //    break;
                    //}
                }
                else if (this.IsResponseStreamEnded)
                {
                    bResult = true;
                    break;
                }
                else if (this.IsResponseStreamEndKnown)
                {
                    _Logger.Error("[{3}][readStream] Socket closed. Content-Length:{0} ContentBytesReceived:{1} Url: {2}",
                    this._ContentLength, this._ResponseBytesReceived, this._ServerUrl, this._Id);

                    this._ConnectionKeepAlive = false;
                    break;
                }
                else //Unknown length
                {
                    bResult = true;
                    break;
                }
            }

            if (this.Abort)
            {
                _Logger.Warn("[{0}][readStream] Abort.", this._Id);
                this._ConnectionKeepAlive = false;
            }

            return bResult;
        }

        private Stream getResponseStream()
        {
            _Logger.Debug("[{0}][getResponseStream] Url: {1}", this._Id, this._ServerUrl);

            Stream stream;
            while (true)
            {
                stream = this.getResponseStreamProcess();

                if (this._GetResponseResult == GetResponseResultEnum.ErrorRemoteSocketClosed
                    || this._GetResponseResult == GetResponseResultEnum.ErrorOtherConnectionRequired
                    || this._GetResponseResult == GetResponseResultEnum.ErrorFailedConnect)
                {
                    if (--this._ReRequestAttempts > 0)
                        continue; //try again
                    else
                        return null;
                }

                //Redirect
                if (this._GetResponseResult == GetResponseResultEnum.OK && this._Redirect && this.AllowRedirect
                    && !string.IsNullOrEmpty(this._ServerUrlRedirect))
                {
                    if (this._RedirectMaxCount-- > 0)
                    {
                        //Flush stream
                        if (stream != null)
                        {
                            this.flushStream();
                            this.Close();
                        }

                        //Redirect
                        this._ServerUri = new Uri(this._ServerUrlRedirect);
                        this._ServerUrl = this._ServerUrlRedirect;
                        continue;
                    }
                    else
                        _Logger.Warn("[{0}][getResponseStream] Max. redirect count reached. Url: {1}", this._Id, this._ServerUrl);
                }

                break;
            }

            return stream;

        }
        private Stream getResponseStreamProcess()
        {
            if (!this._Closed)
                throw new Exception("[getResponseStream] Can't use unclosed request.");

            if (this._ServerUri == null)
                throw new Exception("[getResponseStream] Not initialized.");

            #region Proxy
            try
            {
                if ((this.AllowSystemProxy == Utils.OptionEnum.Yes || (this.AllowSystemProxy == Utils.OptionEnum.Default && _AllowSystemProxyDefault))
                    && string.IsNullOrEmpty(this.Proxy))
                {
                    IWebProxy p = WebRequest.GetSystemWebProxy();
                    if (p != null)
                    {
                        FieldInfo fi = p.GetType().GetField("webProxy", BindingFlags.NonPublic | BindingFlags.Instance);
                        System.Net.WebProxy wp = (System.Net.WebProxy)fi.GetValue(p);
                        Type t = wp.GetType();
                        fi = t.GetField("_ProxyHostAddresses", BindingFlags.NonPublic | BindingFlags.Instance);
                        Hashtable proxyHostAddresses = (Hashtable)fi.GetValue(wp);
                        Uri uri = ((proxyHostAddresses != null) ? (proxyHostAddresses[this._ServerUri.Scheme] as Uri) : null);
                        if (uri != null && !(bool)t.GetMethod("IsMatchInBypassList", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(wp, new object[] { this._ServerUri }))
                            this.Proxy = uri.Authority;
                        else
                            this.Proxy = null;

                        //this.Proxy = p.GetProxy(new Uri("http://proxy.test")).Authority;
                        //if (!string.IsNullOrEmpty(this.Proxy) && this.Proxy == "proxy.test")
                        //    this.Proxy = null;
                    }
                }
            }
            catch { }
            #endregion

            #region Init
            byte[] httpHeader = Encoding.ASCII.GetBytes(this.createHttpHeader());
            //this.ServerUrlRedirect = null;
            this._HttpResponseFields = null;
            this._HttpResponseCode = 0;
            this._ContentLength = -1;
            this._ResponseBytesReceived = 0;
            this._Stream = null;
            this._StreamReadAttempts = 0;
            this._GetResponseResult = GetResponseResultEnum.None;
            this._Redirect = false;
            this._Token = 0;
            this._Closed = false;
            this._ReusingConnection = false;
            this._IsResponseStreamEnded = false;
            this._IsResponseStreamCompressed = false;

            if (httpHeader == null || httpHeader.Length < 1)
            {
                _Logger.Error("[{0}][getResponseStream] Invalid header parameter.", this._Id);
                this._GetResponseResult = GetResponseResultEnum.ErrorInvalidHeader;
                return null;
            }

            bool bClose = true;
            #endregion

            try
            {
                int iReceived = 0;
                int iCnt;
                IAsyncResult ia;

                if (this._BufferReceive == null)
                    this._BufferReceive = new byte[_BUFFER_SIZE];

                //Get IPs address
                List<IPAddress> ips = getPrefferredDnsIp(this._ServerUri.Host);

                this._ServerIpEndpoint = new IPEndPoint(ips[0], this._ServerUri.Port);
                this._ServerAutority = this._ServerUri.Authority;

                #region Connect
                //Try get existing available connection
                Connection sc = _Connections.GetConnection(this);
                if (sc != null)
                {
                    this._TcpClient = sc.Client;
                    this._Stream = sc;
                    this._ReusingConnection = true;
                }
                else
                {
                    #region Connect to the server
                    if (!string.IsNullOrEmpty(this.Proxy))
                    {
                        IPEndPoint ep = null;
                        try
                        {
                            int iIdx = this.Proxy.IndexOf(":");
                            ep = new IPEndPoint(IPAddress.Parse(this.Proxy.Substring(0, iIdx).Trim()), int.Parse(this.Proxy.Substring(iIdx + 1).Trim()));
                        }
                        catch
                        {
                            _Logger.Error("[{0}][getResponseStream] Invalid Proxy: {1}", this._Id, this.Proxy);
                            this._GetResponseResult = GetResponseResultEnum.ErrorInvalidProxy;
                            return null;
                        }

                        this._TcpClient = createTcpClient(ep, this._ConnectTimeout);

                    }
                    else
                        this._TcpClient = createTcpClient(ips, this._ServerUri.Port, this._ConnectTimeout);

                    if (this._TcpClient == null)
                    {
                        _Logger.Error("[{0}][getResponseStream] Unable connect to remote server.", this._Id);
                        this._GetResponseResult = GetResponseResultEnum.ErrorFailedConnect;
                        return null;
                    }
                    else if (string.IsNullOrEmpty(this.Proxy) && !((IPEndPoint)this._TcpClient.Client.RemoteEndPoint).Address.Equals(this._ServerIpEndpoint.Address))
                    {
                        _Connections.RegroupConnection(this);
                    }

                    //SSL
                    if (this._ServerUri.Scheme == "https")
                    {
                        if (!string.IsNullOrEmpty(this.Proxy))
                        {
                            #region Proxy CONNECT

                            this._Stream = this._TcpClient.GetStream();

                            byte[] dataConnect = Encoding.UTF8.GetBytes(String.Format("CONNECT {0}:{1} HTTP/1.0\r\nHost: {0}:{1}\r\nProxy-Connection: Keep-Alive\r\n\r\n",
                                this._ServerUri.Host,
                                this._ServerUri.Port,
                                this._ServerUri.Port != 443 ? ":" + this._ServerUri.Port : null
                                ));
                            this._Stream.Write(dataConnect, 0, dataConnect.Length);

                            try
                            {
                                while (true)
                                {
                                    if (this._TcpClient == null || !this._TcpClient.Connected)
                                    {
                                        _Logger.Error("[{0}][getResponseStream] Proxy CONNECT: Remote socket terminated.", this._Id);
                                        goto ext;
                                    }

                                    //Start next receive
                                    ia = this._Stream.BeginRead(this._BufferReceive, iReceived, this._BufferReceive.Length - iReceived, null, null);

                                    if (!ia.AsyncWaitHandle.WaitOne(this._ResponseTimeout))
                                    {
                                        _Logger.Error("[{0}][getResponseStream] Proxy CONNECT: Response timeout.", this._Id);
                                        goto ext;
                                    }

                                    if ((iCnt = this._Stream.EndRead(ia)) > 0)
                                    {
                                        iReceived += iCnt;

                                        #region Http Response

                                        //Analyze HTTP response
                                        Dictionary<string, string> httpResponseFields = null;
                                        CookieContainer cookies = null;
                                        double dHttpVersion;
                                        int iHttpLength = GetHttpResponse(this._ServerUri, this._BufferReceive, 0, iReceived,
                                                               ref httpResponseFields, ref cookies, out this._HttpResponseCode, out dHttpVersion);

                                        if (iHttpLength == 0)
                                        {
                                            if (iReceived >= 8192)
                                            {
                                                _Logger.Error("[{0}][getResponseStream] Proxy CONNECT Error: Invalid response.", this._Id);
                                                goto ext;
                                            }
                                            else
                                                continue; // response not received yet
                                        }
                                        else if (iHttpLength < 0)
                                        {
                                            _Logger.Error("[{0}][getResponseStream] Proxy CONNECT: Invalid http response.", this._Id);
                                            goto ext;
                                        }
                                        else
                                        {
                                            switch (this._HttpResponseCode)
                                            {
                                                case HttpStatusCode.OK:
                                                    //"HTTP/1.1 200 Connection Established\r\nFiddlerGateway: Direct\r\nStartTime: 09:53:20.976\r\nConnection: close\r\n\r\n"
                                                    goto ssl;

                                                default:
                                                    _Logger.Error("[{0}][getResponseStream] Proxy CONNECT: HTTP response: {1}", this._Id, this.HttpResponseCode);
                                                    goto ext;
                                            }
                                        }

                                        #endregion

                                    }
                                    else
                                        goto ext;
                                }
                            }
                            catch (SocketException ex)
                            {
                                if (((SocketException)ex).ErrorCode == 0x00002745 || ((SocketException)ex).ErrorCode == 0x00002749)
                                {
                                    //WSAECONNABORTED(10053) Software caused connection abort. 
                                    //WSAECONNRESET  (10054) Connection reset by peer.
                                    //WSAENOTCONN    (10057) Socket is not connected.

                                    if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][getResponseStream] Proxy CONNECT: Socket closed.", this._Id);
                                }
                                else
                                {
                                    _Logger.Error("[{0}][getResponseStream] Proxy CONNECT: Socket exception: {1}", this._Id, ((SocketException)ex).ErrorCode);
                                }
                            }
                            catch (Exception ex)
                            {
                                _Logger.Error("[{3}][getResponseStream] Proxy CONNECT Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);

                            }
                            finally
                            {
                            }

                            #endregion
                        }

                    ssl:
                        if (this.UseOpenSSL == Utils.OptionEnum.Yes || (this.UseOpenSSL == Utils.OptionEnum.Default && _UseOpenSSLDefault))
                        {
                            _Logger.Debug("[{0}][getResponseStream] Using OpenSSL: {1}", this._Id, OpenSSL.Core.Version.Library);

                            if (this._Stream == null)
                                this._Stream = new OpenSSL.SSL.SslStream(this._TcpClient.GetStream(), false, this.OpenSslRemoteCertificationCallback, this.OpenSslLocalCertificationCallback);
                            else
                                this._Stream = new OpenSSL.SSL.SslStream(this._Stream, false, this.OpenSslRemoteCertificationCallback, this.OpenSslLocalCertificationCallback);

                            //Load default CA list
                            string[] paths = new string[] { "cacert.pem", "curl-ca-bundle.crt" };
                            FileInfo fiCaFile = null;
                            foreach (string strPath in paths)
                            {
                                string strCa = System.Windows.Forms.Application.StartupPath + "\\" + strPath;
                                if (File.Exists(strCa))
                                {
                                    if (fiCaFile == null || File.GetLastWriteTime(strCa) > fiCaFile.LastWriteTime)
                                        fiCaFile = new FileInfo(strCa);
                                }
                            }

                            if (fiCaFile != null)
                            {
                                ((OpenSSL.SSL.SslStream)this._Stream).CAListFileName = fiCaFile.FullName;
                                _Logger.Debug("[{0}][getResponseStream] OpenSSL: CA list loaded: '{1}'", this._Id, fiCaFile.FullName);
                            }

                            ia = ((OpenSSL.SSL.SslStream)this._Stream).BeginAuthenticateAsClient(
                                this._ServerUri.Host,
                                null,
                                null,
                                this.OpenSslProtocols,
                                this.OpenSslStrength,
                                this.OpenSslCipherList,
                                false,
                                null,
                                null);

                            if (!ia.AsyncWaitHandle.WaitOne(10000))
                            {
                                _Logger.Error("[{0}][getResponseStream] OpenSSL: authentication timeout.", this._Id);
                                goto err;
                            }

                            ((OpenSSL.SSL.SslStream)this._Stream).EndAuthenticateAsClient(ia);

                            if (!((OpenSSL.SSL.SslStream)this._Stream).IsAuthenticated)
                            {
                                _Logger.Error("[{0}][getResponseStream] OpenSSL: authentication failed.", this._Id);
                                goto err;
                            }
                            else
                                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[{0}][getResponseStream] OpenSSL: remote handshake complete. Cipher: {1}",
                                    this._Id, ((OpenSSL.SSL.SslStream)this._Stream).Ssl.CurrentCipher.Description.TrimEnd());
                        }
                        else
                        {
                            if (this._Stream == null)
                                this._Stream = new SslStream(this._TcpClient.GetStream());
                            else
                                this._Stream = new SslStream(this._Stream);

                            ia = ((SslStream)this._Stream).BeginAuthenticateAsClient(this._ServerUri.Host, null, null);

                            if (!ia.AsyncWaitHandle.WaitOne(10000))
                            {
                                _Logger.Error("[{0}][getResponseStream] SSL: authentication timeout.", this._Id);
                                goto err;
                            }

                            ((SslStream)this._Stream).EndAuthenticateAsClient(ia);
                        }
                    }
                    else
                        this._Stream = this._TcpClient.GetStream();

                    #endregion
                }

                this._StreamSource = this._Stream;
                #endregion

                #region Send Http header
                byte[] header = httpHeader;

                if (this.BeforeRequest != null)
                {
                    try
                    {
                        HttpUserWebBeforeRequestEventArgs args = new HttpUserWebBeforeRequestEventArgs() { Handled = false, HttpRequest = httpHeader.ToArray() };
                        this.BeforeRequest(this, args);
                        if (args.Handled && args.HttpRequest != null && args.HttpRequest.Length > 0)
                            header = args.HttpRequest;
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[{3}][getResponseStream] Error onEvent BeforeReqest: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                    }
                }

                if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{3}][getResponseStream] HTTP Request: Length:{0}\r\n{1}{2}",
                   header.Length,
                   Encoding.ASCII.GetString(header, 0, header.Length),
                   this.Method == HttpMethodEnum.POST && this.Post != null && this.Post.Length > 0
                    ? Encoding.UTF8.GetString(this.Post, 0, Math.Min(2048, this.Post.Length)) + "\r\n" : null,
                   this._Id);

                ia = this._Stream.BeginWrite(header, 0, header.Length, null, null);

                if (!ia.AsyncWaitHandle.WaitOne(this._ResponseTimeout))
                {
                    _Logger.Error("[{0}][getResponseStream] Send http header: timeout.", this._Id);
                    goto err;
                }

                this._Stream.EndWrite(ia);
                #endregion

                #region Post data
                if (this.Method == HttpMethodEnum.POST)
                {
                    if (this.Post != null && this.Post.Length > 0)
                    {
                        ia = this._Stream.BeginWrite(this.Post, 0, this.Post.Length, null, null);

                        if (!ia.AsyncWaitHandle.WaitOne(this._ResponseTimeout))
                        {
                            _Logger.Error("[{0}][getResponseStream] Send http post data: timeout. Url: {1}", this._Id, this._ServerUrl);
                            goto err;
                        }

                        this._Stream.EndWrite(ia);
                    }
                    else if (this.PostDataSendHandler != null)
                    {
                        bool bResult = false;
                        long dwSent = 0;
                        long dwLast = 0;
                        System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(() =>
                        {
                            int iLength;
                            try
                            {
                                while ((iLength = this.PostDataSendHandler(this._BufferReceive, 0, this._BufferReceive.Length, dwSent)) > 0)
                                {
                                    this._Stream.Write(this._BufferReceive, 0, iLength);
                                    dwSent += iLength;
                                }

                                if (iLength < 0)
                                {
                                    this._GetResponseResult = GetResponseResultEnum.AbortPost;
                                    return;
                                }


                                bResult = true;
                            }
                            catch (Exception ex)
                            {
                                _Logger.Error("[{3}][getResponseStream] POST Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                            }
                        }
                            ));

                        t.Start();
                        while (!t.Join(this._ResponseTimeout))
                        {
                            if (dwLast == dwSent)
                            {
                                t.Abort();
                                _Logger.Error("[{0}][getResponseStream] Send http post data: timeout. Url: {1}", this._Id, this._ServerUrl);
                                goto err;
                            }
                            else
                                dwLast = dwSent;
                        }

                        if (!bResult)
                        {
                            if (this._GetResponseResult == GetResponseResultEnum.AbortPost)
                                return null;

                            _Logger.Error("[{0}][getResponseStream] Send http POST data: failed. Url: {1}", this._Id, this._ServerUrl);
                            goto err;
                        }
                        else if (this.HttpRequestFields != null && long.TryParse(this.HttpRequestFields["Content-Length"], out this._PostDataSize)
                            && dwSent != this._PostDataSize)
                        {
                            _Logger.Error("[{0}][getResponseStream] Mismatch between 'Content-Length' and POST data size. Url: {1}", this._Id, this._ServerUrl);
                            goto err;
                        }
                        else
                            _Logger.Debug("[{0}][getResponseStream] POST done. Uploaded: {1}  Url: {2}", this._Id, dwSent, this._ServerUrl);
                    }
                }
                #endregion

                #region Response
                iReceived = 0;

                try
                {
                    //Read response
                    while (true)
                    {
                        if (this._TcpClient == null || !this._TcpClient.Connected)
                        {
                            _Logger.Error("[{0}][getResponseStream] Remote socket terminated. Url: {1}", this._Id, this._ServerUrl);
                            this._GetResponseResult = GetResponseResultEnum.ErrorRemoteSocketClosed;
                            return null;
                        }

                        //Start next receive
                        ia = this._Stream.BeginRead(this._BufferReceive, iReceived, this._BufferReceive.Length - iReceived, null, null);

                        if (!ia.AsyncWaitHandle.WaitOne(this._ResponseTimeout))
                        {
                            _Logger.Error("[{0}][getResponseStream] Response timeout. Url: {1}", this._Id, this._ServerUrl);
                            this._GetResponseResult = GetResponseResultEnum.ErrorTimeout;
                            return null;
                        }

                        if ((iCnt = this._Stream.EndRead(ia)) > 0)
                        {
                            iReceived += iCnt;

                            #region Http Response

                            //Analyze HTTP response
                            int iHttpLength = GetHttpResponse(this._ServerUri, this._BufferReceive, 0, iReceived,
                                                   ref this._HttpResponseFields, ref this.Cookies, out this._HttpResponseCode, out this._HttpResponseVersion);

                            if (iHttpLength == 0) //Incomplete
                            {
                                if (iReceived >= 8192)
                                {
                                    _Logger.Error("[{0}][getResponseStream] Error: Invalid response (length >= 8192). Url: {1}", this._Id, this._ServerUrl);
                                    this._GetResponseResult = GetResponseResultEnum.ErrorInvalidResponse;
                                    return null;
                                }
                                else
                                    continue; // response not received yet
                            }
                            else if (iHttpLength < 0) //Error
                            {
                                _Logger.Error("[{0}][getResponseStream] Invalid http response. Url: {1}", this._Id, this._ServerUrl);
                                this._GetResponseResult = GetResponseResultEnum.ErrorInvalidResponse;
                                return null;
                            }
                            else
                            {
                                #region Response received

                                if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{3}][getResponseStream] HTTP Response: Length:{0} Received:{1}\r\n{2}",
                                           iHttpLength,
                                           iReceived,
                                           Encoding.ASCII.GetString(this._BufferReceive, 0, iHttpLength),
                                           this._Id);

                                string strValue;

                                //ContentLength
                                long lContentLength;
                                if (this._HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_CONTENT_LENGTH, out strValue) && long.TryParse(strValue, out lContentLength))
                                    this._ContentLength = lContentLength;

                                //ContentDisposition
                                if (this._HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_CONTENT_DISPOSITION, out strValue))
                                    this._ContentFilename = GetHttpFilename(strValue);

                                #region Keep-Alive
                                bool bKeepAlive = this._HttpResponseVersion >= 1.1;
                                if (this._HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_CONNECTION, out strValue))
                                {
                                    if (strValue.Equals(HttpHeaderField.HTTP_FIELD_CLOSE, StringComparison.CurrentCultureIgnoreCase))
                                        bKeepAlive = false;
                                    else if (!bKeepAlive && strValue.Equals(HttpHeaderField.HTTP_FIELD_KEEP_ALIVE, StringComparison.CurrentCultureIgnoreCase))
                                        bKeepAlive = true;
                                }

                                if (this.AllowKeepAlive && bKeepAlive)
                                {
                                    this._ConnectionKeepAlive = true;
                                    this._ConnectionKeepAliveMax = _ConnectionKeepAliveMaxDefault;


                                    if (this._HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_KEEP_ALIVE, out strValue))
                                    {
                                        //Connection: Keep-Alive
                                        //Keep-Alive: timeout=3, max=99

                                        string[] prms = strValue.Split(new char[] { ',' });
                                        foreach (string strPrm in prms)
                                        {
                                            string[] parts = strPrm.Split(new char[] { '=' });
                                            if (parts.Length == 2)
                                            {
                                                int iValue;
                                                switch (parts[0].ToLower().Trim())
                                                {
                                                    case "timeout":
                                                        if (int.TryParse(parts[1], out iValue))
                                                            this._ConnectionKeepAliveTimeout = iValue;
                                                        break;

                                                    case "max":
                                                        if (int.TryParse(parts[1], out iValue))
                                                            this._ConnectionKeepAliveMax = iValue;
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                    this._ConnectionKeepAlive = false;
                                #endregion

                                //Chunked transfer
                                this._ConnectionChunked = this._HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_TRANSFER_ENCODING, out strValue) &&
                                    strValue.Equals("chunked", StringComparison.CurrentCultureIgnoreCase);


                                #region Analyze response code
                                switch (this._HttpResponseCode)
                                {
                                    case HttpStatusCode.NotModified:
                                        goto ext; //no content to receive

                                    case HttpStatusCode.OK:
                                    case HttpStatusCode.PartialContent:
                                    case HttpStatusCode.Created:
                                    ok:
                                        if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{3}][getResponseStream] HTTP Response: Length:{0} Received:{1}\r\n{2}",
                                            iHttpLength,
                                            iReceived,
                                            Encoding.ASCII.GetString(this._BufferReceive, 0, iHttpLength),
                                            this._Id);

                                        if (this.Method == HttpMethodEnum.HEAD || this._ContentLength == 0)
                                            goto ext; //no content to receive

                                        if (this._ContentLength < 0 && !this._ConnectionChunked)
                                            this._ConnectionKeepAlive = false;// unknown length

                                        #region  Event: Before download (call the event to check if we can proceed)
                                        if (this.BeforeDownload != null)
                                        {
                                            try
                                            {
                                                HttpUserWebBeforeDownloadEventArgs args = new HttpUserWebBeforeDownloadEventArgs() { Abort = false };
                                                this.BeforeDownload(this, args);
                                                if (args.Abort)
                                                {
                                                    _Logger.Error("[{0}][getResponseStream] Abort before download: '{1}'", this._Id, this.Url);

                                                    this._GetResponseResult = GetResponseResultEnum.AbortBeforeDownload;
                                                    return null;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _Logger.Error("[{3}][getResponseStream] Error onEvent BeforeDownload: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                                            }
                                        }
                                        #endregion

                                        //Stream chain
                                        // SocketStream <- SslStream <- HttpUserInterStream <- ChunkedStream <- CompressionStream <- HttpUserWebResponseStream

                                        #region Inter Stream
                                        int iRem = iReceived - iHttpLength;
                                        //_logger.Debug("[getResponseStream] Creating user stream: RemainingData:{0} Url:{1}", iRem, this._ServerUrl);
                                        this._Stream = new InterStream(this._Stream, this._BufferReceive, iHttpLength, iRem, this);
                                        #endregion

                                        #region Chunked Stream
                                        if (this._ConnectionChunked)
                                        {
                                            if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][getResponseStream] Chunked transfer detected.", this._Id);
                                            this._StreamChunked = new ChunkedStream(this._Stream, true) { CheckZeroChunk = false };
                                            this._Stream = this._StreamChunked;
                                        }
                                        #endregion

                                        #region Compression Stream
                                        if (this._HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_CONTENT_ENCODING, out strValue))
                                        {
                                            //If the previous stream is chunked then we need to set zero check flag
                                            if (this._StreamChunked != null)
                                                this._StreamChunked.CheckZeroChunk = true;

                                            string[] encs = strValue.ToLower().Split(',');

                                            for (int i = encs.Length - 1; i >= 0; i--) //proceed with each compression in reverse order
                                            {
                                                switch (encs[i].Trim())
                                                {
                                                    case "gzip":
                                                        //GZip
                                                        if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][getResponseStream] GZip encoding detected.", this._Id);
                                                        this._Stream = new Ionic.Zlib.GZipStream(this._Stream, Ionic.Zlib.CompressionMode.Decompress);
                                                        this._IsResponseStreamCompressed = true;
                                                        break;

                                                    case "deflate":
                                                        //Deflate
                                                        if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][getResponseStream] Deflate encoding detected.", this._Id);
                                                        this._Stream = new Ionic.Zlib.DeflateStream(this._Stream, Ionic.Zlib.CompressionMode.Decompress);
                                                        this._IsResponseStreamCompressed = true;
                                                        break;

                                                    case "br":
                                                        //Brotli
                                                        if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][getResponseStream] Brotli encoding detected.", this._Id);
                                                        this._Stream = new BrotliSharpLib.BrotliStream(this._Stream, CompressionMode.Decompress);
                                                        this._IsResponseStreamCompressed = true;
                                                        break;

                                                    default:
                                                        _Logger.Error("[{0}][getResponseStream] Unsupported encoding: '{1}'", this._Id, strValue);
                                                        goto err;
                                                }
                                            }
                                        }
                                        #endregion

                                        //We need to use different connection: flush current stream to allow keep alive
                                        if (this._GetResponseResult == GetResponseResultEnum.ErrorOtherConnectionRequired)
                                        {
                                            this.flushStream();
                                            goto ext;
                                        }

                                        //OK; return the stream
                                        this._GetResponseResult = GetResponseResultEnum.OK;
                                        bClose = false;
                                        return this._Stream;

                                    case HttpStatusCode.TemporaryRedirect:
                                    case HttpStatusCode.MovedPermanently:
                                    case HttpStatusCode.Redirect:
                                    case HttpStatusCode.SeeOther:
                                        //case HttpStatusCode.Found:
                                        if (this._HttpResponseFields.TryGetValue(HttpHeaderField.HTTP_FIELD_LOCATION, out strValue))
                                        {
                                            if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][getResponseStream] Redirect to: {1}", this._Id, strValue);

                                            if (strValue.StartsWith("/"))
                                                this._ServerUrlRedirect = this._ServerUri.Scheme + "://" + this._ServerUri.Authority + strValue;
                                            else
                                                this._ServerUrlRedirect = strValue;

                                            this._Redirect = true;
                                            goto ok;

                                        }
                                        else
                                            _Logger.Error("[{0}][getResponseStream] Invalid redirect. Url: {1}", this._Id, this._ServerUrl);

                                        goto err;

                                    case (HttpStatusCode)421:
                                        //421 Misdirected Request (RFC 7540)
                                        //The request was directed at a server that is not able to produce a response (for example because of connection reuse)
                                        _Logger.Warn("[{0}][getResponseStream] HTTP response: 421 Misdirected Request. Url: {1}", this._Id, this._ServerUrl);
                                        this._GetResponseResult = GetResponseResultEnum.ErrorOtherConnectionRequired;
                                        this._ReuseConnectionFullDomainCheck = true;
                                        goto ok;

                                    default:
                                        //case HttpStatusCode.BadRequest:
                                        //case HttpStatusCode.Unauthorized:
                                        //case HttpStatusCode.Forbidden:
                                        //case HttpStatusCode.NotFound:
                                        _Logger.Error("[{0}][getResponseStream] HTTP response: {1}. Url: {2}", this._Id, this._HttpResponseCode, this._ServerUrl);
                                        goto ok;

                                }
                                #endregion

                                #endregion
                            }

                            #endregion

                        }
                        else
                        {
                            _Logger.Error("[{5}][getResponseStream] Remote socket terminated. Failed to get http response. Url:{4} Received:{0} ReusedConnection:{1}{2} ReceivedData:\r\n{3}",
                                iReceived,
                                sc != null,
                                sc != null ? string.Format("[Connection[{0}] {1} Connected:{2}]",
                                                sc.ID, sc.Client.Client.RemoteEndPoint, sc.Client.Connected)
                                                : null,
                                iReceived > 0 ? Encoding.ASCII.GetString(this._BufferReceive, 0, iReceived) : null,
                                this._ServerUrl,
                                this._Id
                                );

                            this._GetResponseResult = GetResponseResultEnum.ErrorRemoteSocketClosed;
                            return null;
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (((SocketException)ex).ErrorCode == 0x00002745 || ((SocketException)ex).ErrorCode == 0x00002749)
                    {
                        //WSAECONNABORTED(10053) Software caused connection abort. 
                        //WSAECONNRESET  (10054) Connection reset by peer.
                        //WSAENOTCONN    (10057) Socket is not connected.

                        if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[{0}][getResponseStream] Socket closed.", this._Id);
                    }
                    else
                    {
                        _Logger.Error("[{0}][getResponseStream] Socket exception: {1}", this._Id, ((SocketException)ex).ErrorCode);
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Error("[{3}][getResponseStream] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);

                }
                finally
                {
                }
                #endregion

            err:
                this._GetResponseResult = GetResponseResultEnum.Error;
                return null;

            ext:
                if (this._GetResponseResult == GetResponseResultEnum.None)
                    this._GetResponseResult = GetResponseResultEnum.OK;

                this._IsResponseStreamEnded = true;
                return null;

            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][getResponseStream] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                return null;
            }
            finally
            {
                switch (this._GetResponseResult)
                {
                    case GetResponseResultEnum.OK:
                    case GetResponseResultEnum.ErrorOtherConnectionRequired:
                        break;

                    default: //terminate connection
                        this._ConnectionKeepAlive = false;
                        break;
                }

                if (bClose)
                    this.Close();
            }
        }

        private string createHttpHeader()
        {
            if (this.CreateHttpHeader != null)
                return this.CreateHttpHeader();

            //GET https://www.qr.cz/ HTTP/1.1
            //Host: www.qr.cz
            //User-Agent: Mozilla/5.0 (Windows NT 6.1; rv:80.0) Gecko/20100101 Firefox/80.0
            //Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8
            //Accept-Language: cs,sk;q=0.8,en-US;q=0.5,en;q=0.3
            //Accept-Encoding: gzip, deflate, br
            //DNT: 1
            //Connection: keep-alive
            //Upgrade-Insecure-Requests: 1

            //GET https://www.xbmc-kodi.cz/forum-video HTTP/1.1
            //User-Agent: Opera/9.80 (Windows NT 6.1) Presto/2.12.388 Version/12.18
            //Host: www.xbmc-kodi.cz
            //Accept: text/html, application/xml;q=0.9, application/xhtml+xml, image/png, image/webp, image/jpeg, image/gif, image/x-xbitmap, */*;q=0.1
            //Accept-Language: en,cs-CZ;q=0.9,cs;q=0.8
            //Accept-Encoding: gzip, deflate
            //Connection: Keep-Alive
            //DNT: 1



            List<string> fields = new List<string>();

            bool bPost = this.Post != null && this.Post.Length > 0;
            StringBuilder sb = new StringBuilder(1024);

            //Method
            if (bPost)
            {
                sb.Append("POST ");
                this.Method = HttpMethodEnum.POST;
            }
            else
            {
                sb.Append(this.Method);
                sb.Append(' ');
            }

            //Path
            sb.Append(!string.IsNullOrEmpty(this.Proxy) ? (this._ServerUri.Scheme == "https" ? this._ServerUri.PathAndQuery : this._ServerUri.AbsoluteUri) : this._ServerUri.PathAndQuery);
            sb.Append(" HTTP/1.1\r\n");

            //Host
            sb.Append(HttpHeaderField.HTTP_FIELD_HOST);
            sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
            sb.Append(this._ServerUri.Authority);
            if (this._ServerUri.Port != 80 && this._ServerUri.Port != 443)
            {
                sb.Append(':');
                sb.Append(this._ServerUri.Port);
            }
            sb.Append(HttpHeaderField.EOL);
            fields.Add(HttpHeaderField.HTTP_FIELD_HOST);

            //User-Agent
            if (!string.IsNullOrEmpty(this.UserAgent))
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_USER_AGENT);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append(this.UserAgent);
                sb.Append(HttpHeaderField.EOL);
                fields.Add(HttpHeaderField.HTTP_FIELD_USER_AGENT);
            }

            //Accept
            if (!string.IsNullOrEmpty(this.Accept))
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_ACCEPT);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append(this.Accept);
                sb.Append(HttpHeaderField.EOL);

                fields.Add(HttpHeaderField.HTTP_FIELD_ACCEPT);
            }

            //Accept-Language
            if (!string.IsNullOrEmpty(this.AcceptLanguage))
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_ACCEPT_LANGUAGE);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append(this.AcceptLanguage);
                sb.Append(HttpHeaderField.EOL);

                fields.Add(HttpHeaderField.HTTP_FIELD_ACCEPT_LANGUAGE);
            }

            //Gzip, Deflate
            sb.Append(HttpHeaderField.HTTP_FIELD_ACCEPT_ENCODING);
            sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
            sb.Append("gzip, deflate, br");
            sb.Append(HttpHeaderField.EOL);
            fields.Add(HttpHeaderField.HTTP_FIELD_ACCEPT_ENCODING);

            //Referer
            if (!string.IsNullOrEmpty(this.Referer))
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_REFERER);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append(this.Referer);
                sb.Append(HttpHeaderField.EOL);

                fields.Add(HttpHeaderField.HTTP_FIELD_REFERER);
            }

            //sb.Append("Pragma: no-cache\r\n");



            //DNT
            if (this.DoNotTrack)
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_DO_NOT_TRACK);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append('1');
                sb.Append(HttpHeaderField.EOL);
                fields.Add(HttpHeaderField.HTTP_FIELD_DO_NOT_TRACK);
            }

            //sb.Append("Cache-Control: no-cache\r\n");
            if (this.AllowKeepAlive)
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_CONNECTION);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append(HttpHeaderField.HTTP_FIELD_KEEP_ALIVE);
                sb.Append(HttpHeaderField.EOL);
                fields.Add(HttpHeaderField.HTTP_FIELD_CONNECTION);
            }

            //Cookies
            if (!this.AllowCookies)
                this.Cookies = null;
            else if (this.Cookies != null && this.Cookies.Count > 0)
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_COOKIE);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);

                foreach (Cookie c in WebTools.GetAllCookies(this.Cookies))
                {
                    sb.Append(c.Name);
                    sb.Append('=');
                    sb.Append(c.Value);
                    sb.Append("; ");
                }

                sb.Remove(sb.Length - 2, 2);
                sb.Append(HttpHeaderField.EOL);

                fields.Add(HttpHeaderField.HTTP_FIELD_COOKIE);
            }

            //Content-Length
            if (bPost)
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_CONTENT_LENGTH);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append(this.Post.Length);
                sb.Append(HttpHeaderField.EOL);

                fields.Add(HttpHeaderField.HTTP_FIELD_CONTENT_LENGTH);

                if (string.IsNullOrWhiteSpace(this.ContentType))
                    this.ContentType = HttpHeaderField.HTTP_DEFAULT_CONTENT_TYPE;
            }



            //If-Modified-Since: Tue, 10 Dec 2019 00:23:35 GMT
            if (this._ModifiedSince > DateTime.MinValue)
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_IF_MODIFIED_SINCE);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append(this._ModifiedSince.ToUniversalTime().ToString("R"));
                sb.Append(HttpHeaderField.EOL);

                fields.Add(HttpHeaderField.HTTP_FIELD_IF_MODIFIED_SINCE);
            }

            //Content-Type
            if (!string.IsNullOrEmpty(this.ContentType))
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_CONTENT_TYPE);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append(this.ContentType);
                sb.Append(HttpHeaderField.EOL);

                fields.Add(HttpHeaderField.HTTP_FIELD_CONTENT_TYPE);
            }

            //Range
            if (this._ResumeFrom >= 0)
            {
                sb.Append(HttpHeaderField.HTTP_FIELD_RANGE);
                sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                sb.Append("bytes=");
                sb.Append(this._ResumeFrom);
                sb.Append('-');
                if (this._ResumeLength > 0)
                    sb.Append(this._ResumeLength - 1);
                sb.Append(HttpHeaderField.EOL);

                fields.Add(HttpHeaderField.HTTP_FIELD_RANGE);
            }

            //Extra http fields
            if (this.HttpRequestFields != null)
            {
                foreach (string strKey in this.HttpRequestFields)
                {
                    if (!string.IsNullOrWhiteSpace(strKey))
                    {
                        string strKeyTrimmed = strKey.Trim();

                        if (fields.Exists(p => p.Equals(strKeyTrimmed, StringComparison.CurrentCultureIgnoreCase)))
                            throw new Exception("Http field already exists: " + strKey);

                        sb.Append(strKeyTrimmed);
                        sb.Append(HttpHeaderField.HTTP_FIELD_COLON);
                        sb.Append(this.HttpRequestFields[strKey].Trim());
                        sb.Append(HttpHeaderField.EOL);
                    }
                }
            }

            sb.Append(HttpHeaderField.EOL);


            if (this.Method == HttpMethodEnum.POST && !bPost && this.PostDataSendHandler == null)
                throw new Exception("Invalid POST request.");

            return sb.ToString();
        }

        private static Socket createConnection(Uri uri, int iConnectTiemout)
        {
            //Connect the stream server, uri.Port);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.ExclusiveAddressUse = true;
            socket.ReceiveBufferSize = _RECEIVE_BUFFSIZE;


            //Get the IP of the host
            IPAddress ip = System.Net.Dns.GetHostAddresses(uri.Host)[0];
            IPEndPoint remoteEP = new IPEndPoint(ip, uri.Port);


            // Connect to the remote endpoint.
            try
            {
                IAsyncResult ar = socket.BeginConnect(remoteEP, null, null);
                if (socket == null || !ar.AsyncWaitHandle.WaitOne(iConnectTiemout, true))
                {

                    if (socket != null)
                    {
                        socket.Close();
                        socket = null;
                    }
                    return null;
                }

                // Complete the connection.
                socket.EndConnect(ar);
            }
            catch (Exception ex)
            {
                _Logger.Error("[createConnection] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }
                return null;
            }

            return socket;
        }

        private static List<IPAddress> _IpsOutOfService = new List<IPAddress>();
        private static List<IPAddress> getPrefferredDnsIp(string strHost)
        {
            //Get the IPs of the host
            List<IPAddress> ips = new List<IPAddress>(System.Net.Dns.GetHostAddresses(strHost));

            if (ips.Count > 1)
            {
                //Sort ips: bad ip goes to the bottom
                lock (_IpsOutOfService)
                {
                    for (int i = ips.Count - 2; i >= 0; i--)
                    {
                        IPAddress ip = ips[i];

                        if (_IpsOutOfService.Exists(p => p.Equals(ip)))
                        {
                            //Exist; move to the bottom
                            ips.RemoveAt(i);
                            ips.Insert(ips.Count, ip);
                        }
                    }
                }
            }

            return ips;
        }
        private static TcpClient createTcpClient(List<IPAddress> ips, int iPort, int iConnectTimeout)
        {
            try
            {
                TcpClient tcp = null;

                for (int i = 0; i < ips.Count; i++)
                {
                    IPAddress ip = ips[i];
                    IPEndPoint remoteEP = new IPEndPoint(ip, iPort);
                    tcp = createTcpClient(remoteEP, iConnectTimeout);
                    if (tcp == null)
                    {
                        lock (_IpsOutOfService)
                        {
                            if (_IpsOutOfService.FirstOrDefault(p => p.Equals(ip)) == null)
                            {
                                _IpsOutOfService.Add(ip); //bad ip does not exist yet; add to the list
                                _Logger.Debug("[createTcpClient] Server out of service. Add to the black list: {0}", ip);
                            }
                        }
                    }
                    else
                    {
                        //OK
                        lock (_IpsOutOfService)
                        {
                            IPAddress ipOk = _IpsOutOfService.FirstOrDefault(p => p.Equals(ip));
                            if (ipOk != null)
                            {
                                _IpsOutOfService.Remove(ipOk); //ip is working now; remove from the list
                                _Logger.Debug("[createTcpClient] Server is running now. Remove from the black list: {0}", ip);
                            }
                        }

                        return tcp;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _Logger.Error("[createTcpClient] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return null;
            }
        }
        private static TcpClient createTcpClient(IPEndPoint remoteEP, int iConnectTiemout)
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient(AddressFamily.InterNetwork);
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, _RECEIVE_BUFFSIZE);

                // Connect to the remote endpoint.
                IAsyncResult ar = client.BeginConnect(remoteEP.Address, remoteEP.Port, null, null);
                if (client == null || !ar.AsyncWaitHandle.WaitOne(iConnectTiemout, true))
                {
                    _Logger.Error("[createTcpClient] Connection to the server: FAILED  EP:'{0}'", remoteEP);
                    if (client != null)
                    {
                        client.Close();
                        client = null;
                    }
                    return null;
                }

                // Complete the connection.
                client.EndConnect(ar);

                return client;

            }
            catch (Exception ex)
            {
                _Logger.Error("[createTcpClient] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                if (client != null)
                {
                    client.Close();
                    client = null;
                }
                return null;
            }
        }

        private static Cookie parseCookie(string strValue)
        {
            Cookie cookie = new Cookie();

            MatchCollection mc = _RegexHttpFieldCookie.Matches(strValue);
            if (mc.Count > 0)
            {
                for (int i = 0; i < mc.Count; i++)
                {
                    Match m = mc[i];
                    if (i == 0)
                    {
                        cookie.Name = m.Groups["key"].Value.Trim();
                        cookie.Value = m.Groups["value"].Value.Trim();
                    }
                    else
                    {
                        switch (m.Groups["key"].Value.Trim())
                        {
                            case "path":
                                cookie.Path = m.Groups["value"].Value.Trim();
                                break;

                            case "expires":
                                cookie.Expires = DateTime.Parse((m.Groups["value"].Value));
                                break;
                        }
                    }

                }
            }

            return cookie;
        }

        private bool openSslRemoteCertificationCallback(
             Object sender,
             OpenSSL.X509.X509Certificate cert,
             OpenSSL.X509.X509Chain chain,
             int depth,
             OpenSSL.X509.VerifyResult result)
        {

            string strSerial = Utils.Tools.PrintDataToHex(cert.SerialNumber, bSpace: false);
            string strIssuerCommon = cert.Issuer.Common;
            string strSubjectCommon = cert.Subject.Common;


            if (result == OpenSSL.X509.VerifyResult.X509_V_OK)
            {
                _Logger.Debug("[{3}][openSslRemoteCertificationCallback] Certificate is valid: Issuer:'{0}' Subject:'{1}' Serial:'{2}'",
                    strIssuerCommon, strSubjectCommon, strSerial, this._Id);
                return true;
            }

            //Try build Windows Store chain
            _Logger.Debug("[{3}][openSslRemoteCertificationCallback] Using Windows Store to check certificate: Issuer:'{0}' Subject:'{1}' Serial:'{2}'",
                       strIssuerCommon, strSubjectCommon, strSerial, this._Id);

            X509Chain chain2 = new X509Chain();

            // Check all properties
            chain2.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            // This setup does not have revocation information
            chain2.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            X509Certificate2 crtRq = null;

            try
            {
                //Create certificate from PEM data
                string strCertPemString = cert.PEM.Replace("-----BEGIN CERTIFICATE-----", null).Replace("-----END CERTIFICATE-----", null);
                crtRq = new X509Certificate2(Convert.FromBase64String(strCertPemString));

                // Build the chain
                chain2.Build(crtRq);
            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][openSslRemoteCertificationCallback] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                return false;
            }

            // Are there any failures from building the chain?
            if (chain2.ChainStatus.Length == 0)
                return true;

            // If there is a status, verify the status is NoError
            bool bResult = chain2.ChainStatus[0].Status == X509ChainStatusFlags.NoError;
            if (!bResult)
            {
                //Try find alternative one
                X509Store store = new X509Store("Root", StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                X509Certificate2Collection certs = (X509Certificate2Collection)store.Certificates;
                //Filter out specific certs
                X509Certificate2Collection fCerts = (X509Certificate2Collection)certs.Find(X509FindType.FindBySubjectName, strSubjectCommon, true);
                if (fCerts.Count > 0)
                {
                    X509KeyUsageFlags usageCrtRq = getCerticateUsageFlags(crtRq);

                    foreach (X509Certificate2 crt in fCerts)
                    {
                        //Check usage
                        if (usageCrtRq == X509KeyUsageFlags.None || (getCerticateUsageFlags(crt) & usageCrtRq) == usageCrtRq)
                        {
                            //Compare PublicKeys
                            byte[] pbCert = crt.PublicKey.EncodedKeyValue.RawData;
                            byte[] pbCertRq = crtRq.PublicKey.EncodedKeyValue.RawData;

                            if (pbCert.Length == pbCertRq.Length)
                            {
                                for (int i = 0; i < pbCert.Length; i++)
                                {
                                    if (pbCert[i] != pbCertRq[i])
                                        goto nxt;
                                }
                            }

                            //Check the crt in the X509Chain
                            chain2 = new X509Chain();
                            chain2.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                            chain2.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                            chain2.Build(crt);

                            if (chain2.ChainStatus.Length == 0 || chain2.ChainStatus[0].Status == X509ChainStatusFlags.NoError)
                            {
                                _Logger.Debug("[{3}][openSslRemoteCertificationCallback] Using alternative certificate: Issuer:'{0}' Subject:'{1}' Serial:'{2}'",
                                crt.IssuerName.Name, crt.SubjectName.Name, crt.SerialNumber, this._Id);
                                return true;
                            }
                        }

                    nxt:
                        continue;
                    }

                }

                _Logger.Error("[{4}][openSslRemoteCertificationCallback] Verify failed: Issuer:'{0}' Subject:'{1}' Serial:'{2}' Status: {3}",
                       strIssuerCommon, strSubjectCommon, strSerial, chain2.ChainStatus[0].Status, this._Id);
            }

            return bResult;

        }

        private OpenSSL.X509.X509Certificate openSslLocalCertificationCallback(
             Object sender,
             string targetHost,
             OpenSSL.X509.X509List localCerts,
             OpenSSL.X509.X509Certificate remoteCert,
             string[] acceptableIssuers)
        {
            return null;
        }


        private static X509KeyUsageFlags getCerticateUsageFlags(X509Certificate2 crt)
        {
            if (crt.Extensions != null)
            {
                foreach (X509Extension ext in crt.Extensions)
                {
                    if (ext is X509KeyUsageExtension)
                        return ((X509KeyUsageExtension)ext).KeyUsages;
                }
            }

            return X509KeyUsageFlags.None;
        }
        #endregion

    }
}
