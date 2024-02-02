using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.CompilerServices;
using NLog;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.Pbk.Utils.Buffering
{
    public class RingBuffer : IBuffer
    {
        #region Types
        #endregion

        #region Constants
        private const int MIN_BUFFER_SIZE = 1024 * 1;
        private const int MIN_SEGMENT_SIZE = 1024 * 1;
        #endregion

        #region Private Fields
        private static int _IdCounter = -1;

        private int _Id = 0;
        private int _IdFileCounter = 0;

        private ManualResetEvent _WriteSignal = new ManualResetEvent(false);
        private ManualResetEvent _ReadSignal = new ManualResetEvent(false);
        private ManualResetEvent _WaitSignal = new ManualResetEvent(false);
        private ManualResetEvent _ReleaseWaitSignal = new ManualResetEvent(false);
        private ManualResetEvent _FlushedSignal = new ManualResetEvent(false);

        private BufferDataHandler _Callback;
        private Thread _ProcessThread = null;
        private byte[] _Buffer = null;
        private FileStream _BufferFile = null;
        private int _BufferSize;
        private int _DataReadPosition = 0;
        private int _DataWritePosition = 0;
        private bool _BufferFull = false;
        private volatile bool _Run = false;
        private volatile bool _Wait = false;
        private volatile bool _Flush = false;
        private volatile bool _Empty = false;

        private volatile bool _Terminate = false;

        private object _Padlock = new object();

        static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Public Properties
        public bool Run
        {
            get
            {
                return !this._Terminate && this._Run;
            }

            set
            {
                this._Run = value;
                if (value)
                    this._WriteSignal.Set();

                _Logger.Debug("[" + this._Id + "][Run] " + value);
            }
        }

        public int CurrentLevel
        {
            get
            {
                return (int)(((float)this.CurrentValue / this._BufferSize) * 100);
            }
        }

        public int CurrentValue
        {
            get
            {
                lock (this._Padlock)
                {
                    if (this._DataWritePosition < this._DataReadPosition)
                        return (this._BufferSize - this._DataReadPosition) + this._DataWritePosition;
                    else if (this._DataWritePosition > this._DataReadPosition)
                        return this._DataWritePosition - this._DataReadPosition;
                    else if (this._BufferFull)
                        return this._BufferSize;
                    else return 0;
                }
            }
        }

        public int BufferSize
        {
            get
            {
                return this._BufferSize;
            }
        }

        public int BufferSizeMax
        {
            get
            {
                return this._BufferSize;
            }
        }

        public int PositionRead
        {
            get
            {
                return this._DataReadPosition;
            }
        }
        public int PositionWrite
        {
            get
            {
                return this._DataWritePosition;
            }
        }

        public int Buffers
        {
            get
            {
                return 1;
            }
        }

        public int BuffersMax
        {
            get
            {
                return 1;
            }
            set
            {
            }
        }

        public int BuffersInUse
        {
            get { return 1; }
        }

        public bool LogBufferOverrun = false;
        public bool LogBufferUnderrun = false;
        #endregion

        #region ctor
        static RingBuffer()
        {
            Logging.Log.Init();
        }

        public RingBuffer(BufferDataHandler callback, int iBufferSize)
        {
            if (callback == null)
                throw new ArgumentException("Callback is null.");

            this._Id = Interlocked.Increment(ref _IdCounter);

            this._Callback = callback;

            this._Buffer = new byte[Math.Max(MIN_BUFFER_SIZE, iBufferSize)];
            this._BufferSize = this._Buffer.Length;

            if (Log.LogLevel <= LogLevel.Debug)
                _Logger.Debug("[" + this._Id + "][RingBuffer] BufferSize:" + this._BufferSize);

            this.processStart();
        }
        public RingBuffer(BufferDataHandler callback, int iBufferSize, string strFileBufferPath)
        {
            if (callback == null)
                throw new ArgumentException("Callback is null.");

            this._Id = Interlocked.Increment(ref _IdCounter);

            this._Callback = callback;

            this._Buffer = new byte[1024 * 32];
            this._BufferFile = new FileStream(strFileBufferPath, FileMode.Create);
            this._BufferSize = Math.Max(MIN_BUFFER_SIZE, iBufferSize);

            if (Log.LogLevel <= LogLevel.Debug)
                _Logger.Debug("[" + this._Id + "][RingBuffer] BufferFileSize:" + this._BufferSize);


            this.processStart();
        }
        #endregion

        #region Public Prototypes

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            if (this._ProcessThread != null)
            {
                _Logger.Debug("[" + this._Id + "][Close]");

                this.Flush();

                this._Terminate = true;
                this._ReadSignal.Set();
                this._WriteSignal.Set();

                this._ProcessThread.Join();

                this._ProcessThread = null;

                if (this._BufferFile != null)
                    this._BufferFile.Close();

                this._Buffer = null;

                _Logger.Debug("[" + this._Id + "][Close] Done.");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public int Write(byte[] buffer, int iOffset, int iLength)
        {
            int iBufferLevel;
            bool bOver, bFull;
            int iIdxRd;

            while (!this._Terminate && iLength > 0)
            {
                bOver = false;

                lock (this._Padlock)
                {
                    iIdxRd = this._DataReadPosition;
                    bFull = this._BufferFull;
                }

                if (iIdxRd < this._DataWritePosition)
                {
                    iBufferLevel = this._DataWritePosition - iIdxRd;
                }
                else if (iIdxRd > this._DataWritePosition)
                {
                    iBufferLevel = (this._BufferSize - iIdxRd) + this._DataWritePosition;
                    bOver = true;
                }
                else if (bFull)
                    iBufferLevel = this._BufferSize;
                else
                    iBufferLevel = 0;

                if (iBufferLevel >= this._BufferSize)
                {
                    //Full

                    if (this.LogBufferOverrun)
                        _Logger.Warn("[" + this._Id + "][Write] Buffer full.");

                    this._ReadSignal.WaitOne();
                    this._ReadSignal.Reset();
                }
                else
                {
                    int iToWrite;

                    if (bOver)
                        iToWrite = Math.Min(iIdxRd - this._DataWritePosition, iLength);
                    else
                        iToWrite = Math.Min(this._BufferSize - this._DataWritePosition, iLength);

                    if (this._BufferFile != null)
                    {
                        lock (this._BufferFile)
                        {
                            this._BufferFile.Position = this._DataWritePosition;
                            this._BufferFile.Write(buffer, iOffset, iToWrite);
                        }
                    }
                    else
                        Buffer.BlockCopy(buffer, iOffset, this._Buffer, this._DataWritePosition, iToWrite);

                    lock (this._Padlock)
                    {
                        this._DataWritePosition += iToWrite;

                        if (this._DataWritePosition >= this._BufferSize)
                            this._DataWritePosition = 0;

                        this._BufferFull = iIdxRd == this._DataWritePosition;


                        if (this._Empty || this._BufferFull)
                        {
                            this._Empty = false;
                            this._WriteSignal.Set();
                        }
                    }

                    iOffset += iToWrite;
                    iLength -= iToWrite;

                }
            }

            return iLength;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Reset()
        {
            if (!this._Terminate)
            {
                _Logger.Debug("[" + this._Id + "][Reset]");

                this._ReleaseWaitSignal.Reset();
                this._Wait = true;
                this._WriteSignal.Set();
                this._WaitSignal.WaitOne();
                this._Wait = false;

                this._DataReadPosition = 0;
                this._DataWritePosition = 0;
                this._BufferFull = false;
                this._WriteSignal.Reset();
                this._ReadSignal.Reset();

                this._ReleaseWaitSignal.Set();
            }

            _Logger.Debug("[" + this._Id + "][Reset] Done.");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Flush()
        {
            if (!this._Terminate)
            {
                _Logger.Debug("[" + this._Id + "][Flush]");

                if (!this._Empty)
                {
                    this._FlushedSignal.Reset();
                    this._Flush = true;
                    this._WriteSignal.Set();
                    this._FlushedSignal.WaitOne();
                }

                _Logger.Debug("[" + this._Id + "][Flush] Done.");
            }
        }

        #endregion

        #region Private Prototypes

        private void processStart()
        {
            //Start sender
            if (this._ProcessThread == null)
            {
                this._ProcessThread = new Thread(new ThreadStart(() => this.process()));
                this._ProcessThread.Start();
            }
        }

        private void process()
        {
            int iBufferLevel;
            bool bOver, bFull;
            int iIdxWr;
            while (!this._Terminate)
            {
                bOver = false;

                lock (this._Padlock)
                {
                    iIdxWr = this._DataWritePosition;
                    bFull = this._BufferFull;
                }

                if (iIdxWr < this._DataReadPosition)
                {
                    iBufferLevel = (this._BufferSize - this._DataReadPosition) + iIdxWr;
                    bOver = true;
                }
                else if (iIdxWr > this._DataReadPosition)
                    iBufferLevel = iIdxWr - this._DataReadPosition;
                else if (bFull)
                    iBufferLevel = this._BufferSize;
                else
                    iBufferLevel = 0;

                if (this._Wait)
                {
                    this._WaitSignal.Set();
                    this._ReleaseWaitSignal.WaitOne();
                }
                else if (!this._Run || iBufferLevel < (this._Flush ? 1 : MIN_SEGMENT_SIZE))
                {
                    if (this._Run && this.LogBufferUnderrun)
                        _Logger.Warn("[" + this._Id + "][Write] Buffer empty.");

                    this._Empty = true;
                    this._WriteSignal.WaitOne();
                    this._WriteSignal.Reset();

                    if (this._Run && this._Flush)
                    {
                        this._FlushedSignal.Set();
                        this._Flush = false;
                    }
                }
                else
                {
                    int iToRead;

                    if (bOver || bFull)
                        iToRead = this._BufferSize - this._DataReadPosition;
                    else
                        iToRead = iIdxWr - this._DataReadPosition;

                    if (this._BufferFile != null)
                    {
                        iToRead = Math.Min(this._Buffer.Length, iToRead);
                        lock (this._BufferFile)
                        {
                            this._BufferFile.Position = this._DataReadPosition;
                            this._BufferFile.Read(this._Buffer, 0, iToRead);
                        }

                        iToRead -= this._Callback(this._Buffer, 0, iToRead);
                    }
                    else
                        iToRead -= this._Callback(this._Buffer, this._DataReadPosition, iToRead);

                    if (iToRead > 0)
                    {
                        lock (this._Padlock)
                        {
                            this._DataReadPosition += iToRead;

                            if (this._DataReadPosition >= this._BufferSize)
                                this._DataReadPosition = 0;

                            if (this._BufferFull)
                            {
                                this._BufferFull = false;
                                this._ReadSignal.Set();
                            }
                        }
                    }

                }

            }


        }

        #endregion

    }


}
