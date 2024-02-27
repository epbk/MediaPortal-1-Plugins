using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using NLog;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.IptvChannels.Proxy
{
    public class RemoteClient : Pbk.Utils.Buffering.IBuffer
    {
        
        public const int TS_BLOCK_SIZE = 188;

        static NLog.Logger _Logger = LogManager.GetCurrentClassLogger(); 

        public DateTime SendTs = new DateTime();
        public TimeSpan SendPeekTime = new TimeSpan();
        public DateTime SendPeekTimeLastTs = new DateTime();

        public const int DEFAULT_BUFFER_SIZE = Database.dbSettings.PACKET_BUFFSIZE * 8; //8 x 256kb = 2mb
        public bool HttpResponseSent = false;
        public bool FirstDataPacketSent = false;
        
        public Socket ClientSocket = null;
        public IAsyncResult SocketResult = null;
        public string RemotePoint
        {
            get
            {
                return this._RemotePoint;
            }
        }private string _RemotePoint = "";
        
        public byte[] BufferReceive = new byte[1024 * 8];
        
        public DateTime LastWarning = new DateTime();
        public SocketAsyncEventArgs SocketSendArguments = new SocketAsyncEventArgs();

        //public bool TimeshiftRq = false;
        //public string TimeshiftId = null;
        //public TimeShiftingTask Timeshift = null;

        public ConnectionHandler Handler = null;

        public bool IcyMetaDataRequest = false;

        private Pbk.Utils.Buffering.ArrayBuffer _ArrayBuffer;

        private bool _StartTimeshifting = false;

        public TimeSpan Duration
        {
            get
            {
                return DateTime.Now - this._StartTS;
            }
        }

        private DateTime _StartTS = DateTime.Now;

        protected volatile bool _Connected = false;
        protected volatile bool _Closing = false;

        private StringBuilder _SbInfo = new StringBuilder(256);

        private static int _IdCnt = -1;
        private int _Id;

        public virtual bool IsConnected
        {
            get
            {
                return this.ClientSocket != null && this.ClientSocket.Connected;
            }
        }

        #region IBuffer
        public int BufferSize
        {
            get
            {
                if (this._ArrayBuffer == null)
                    return 0;
                else
                    return this._ArrayBuffer.BufferSize;
            }

            set
            {
                if (this._ArrayBuffer == null)
                    this.initBuffer();

                if (value > this._ArrayBuffer.BufferSize)
                {
                    int iCnt = (int)Math.Ceiling((double)value / this._ArrayBuffer.BufferSegmentSize);
                    this._ArrayBuffer.BuffersMax = iCnt;
                }
            }
        }

        public int BufferLevel
        {
            get
            {
                if (this._ArrayBuffer == null)
                    return 0;
                else
                    return this._ArrayBuffer.CurrentLevel;
            }
        }

        public int Buffers
        {
            get
            {
                if (this._ArrayBuffer == null)
                    return 0;
                else
                    return this._ArrayBuffer.Buffers;
            }
        }

        public int CurrentLevel
        {
            get
            {
                if (this._ArrayBuffer == null)
                    return 0;
                else
                    return this._ArrayBuffer.CurrentLevel;
            }
        }

        public int CurrentValue
        {
            get
            {
                if (this._ArrayBuffer == null)
                    return 0;
                else
                    return this._ArrayBuffer.CurrentValue;
            }
        }

        public int BufferSizeMax
        {
            get
            {
                if (this._ArrayBuffer == null)
                    return 0;
                else
                    return this._ArrayBuffer.BufferSizeMax;
            }
        }

        public int PositionRead
        {
            get { return 0; }
        }

        public int PositionWrite
        {
            get { return 0; }
        }

        public int BuffersMax
        {
            get
            {
                if (this._ArrayBuffer == null)
                    return 0;
                else
                    return this._ArrayBuffer.BuffersMax;
            }
            set
            {
                if (this._ArrayBuffer != null)
                    this._ArrayBuffer.BuffersMax = value;
            }
        }

        public int BuffersInUse
        {
            get
            {
                if (this._ArrayBuffer == null)
                    return 0;
                else
                    return this._ArrayBuffer.BuffersInUse;
            }
        }

        #endregion

        public string Info
        {
            get
            {
                lock (this)
                {
                    this._SbInfo.Clear();
                    this._SbInfo.Append(" [");
                    this._SbInfo.Append(this.RemotePoint);
                    this._SbInfo.Append("]  ");

                    this._SbInfo.Append("ClientBuffer: [");
                    this._SbInfo.Append(this.BuffersInUse);
                    this._SbInfo.Append('/');
                    this._SbInfo.Append(this.Buffers);
                    this._SbInfo.Append('/');
                    this._SbInfo.Append(this.BuffersMax);
                    this._SbInfo.Append("][");
                    this._SbInfo.Append(MediaPortal.IptvChannels.Tools.Utils.PrintFileSize(this.BufferSizeMax, "0", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                    this._SbInfo.Append("] ");
                    this._SbInfo.Append(this.BufferLevel);
                    this._SbInfo.Append('%');
                    this._SbInfo.Append("   Duration: ");
                    this._SbInfo.Append(this.Duration.ToString("hh\\:mm\\:ss"));

                    return this._SbInfo.ToString();      
                }
            }
        }

        public RemoteClient(ConnectionHandler handler)
        {
            this._Id = Interlocked.Increment(ref _IdCnt);
            this.Handler = handler;
        }
        public RemoteClient(ConnectionHandler handler, Socket socket)
            : this(handler)
        {
            
            this.ClientSocket = socket;
            this._RemotePoint = socket.RemoteEndPoint.ToString();

            this.initBuffer();
        }

        public virtual int WriteData(byte[] buffer, int iOffset, int iLength)
        {
            if (!this._Closing && this._ArrayBuffer != null)
                return this._ArrayBuffer.Write(buffer, iOffset, iLength);
            else
                return 0;
        }

        public virtual bool WriteData(int iOffset, int iCount)
        {
            return true;
        }

        public virtual void Close()
        {
            this._Closing = true;

            if (this.ClientSocket != null && this.ClientSocket.Connected)
            {
                try
                {
                    this.ClientSocket.Shutdown(SocketShutdown.Both);
                    this.ClientSocket.Disconnect(true);
                    this.ClientSocket.Close();
                }
                catch { }
            }

            if (this._ArrayBuffer != null)
            {
                this._ArrayBuffer.Close();
                this._ArrayBuffer = null;
            }
        }

        public virtual void BeginFlush(bool bStartTimeshifting)
        {
            if (this._ArrayBuffer != null)
            {
                _Logger.Debug("[BeginFlush] Start... [0]", this.RemotePoint);

                this._StartTimeshifting = bStartTimeshifting;
                this._ArrayBuffer.BeginFlush(this.flushDoneCallback, true);
            }
        }

        private void initBuffer()
        {
            this._ArrayBuffer = new Pbk.Utils.Buffering.ArrayBuffer(this.writeToSocket, Database.dbSettings.PACKET_BUFFSIZE, 1, 8);
            this._ArrayBuffer.LogBufferOverrun = true;
            this._ArrayBuffer.Prebuffer = 0;
            this._ArrayBuffer.WriteWaitsWhenFull = false;
            this._ArrayBuffer.BufferSegmentLifeTime = 60000;
            this._ArrayBuffer.WriteAlign = TS_BLOCK_SIZE;
            this._ArrayBuffer.Run = true;
        }

        private int writeToSocket(byte[] buffer, int iOffset, int iLength)
        {
            try
            {
                if (this._Closing || this.ClientSocket == null || !this.ClientSocket.Connected)
                    iLength = 0;
                else
                    //Send data
                    iLength -= this.ClientSocket.Send(buffer, iOffset, iLength, SocketFlags.None);
            }
            catch
            {
                try
                {
                    this.ClientSocket.Close();
                }
                catch { }

                iLength = 0;
            }

            return iLength;
        }

        private void flushDoneCallback(object sender, EventArgs e)
        {
            _Logger.Debug("[flushDoneCallback] Done. [0]", this.RemotePoint);

            //Timeshift has been created.
            //if (this._StartTimeshifting && this.Timeshift != null)
            //{
            //    //Start streaming
            //    this.Timeshift.StartStreaming();

            //    //All data sent. Destroy the buffer
            //    ((Pbk.Utils.Buffering.BufferArgs)e).Close = true; //this._ArrayBuffer.Close();
            //    this._ArrayBuffer = null;
            //}
        }


        public StringBuilder SerializeJson(StringBuilder sb)
        {
            sb.Append('{');
            sb.Append("\"type\":\"RemoteClient\",");
            sb.Append("\"id\":\"");
            sb.Append(this._Id);
            sb.Append("\",\"parentId\":\"");
            sb.Append(this.Handler.HandlerId);
            sb.Append("\",\"endpoint\":\"");
            sb.Append(this.RemotePoint);
            sb.Append("\",\"info\":\"");
            Tools.Json.AppendAndValidate(this.Info, sb);
            sb.Append("\"}");

            return sb;
        }
    }
}
