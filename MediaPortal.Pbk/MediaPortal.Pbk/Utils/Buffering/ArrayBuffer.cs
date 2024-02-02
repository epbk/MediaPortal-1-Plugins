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
    public class ArrayBuffer : IBuffer
    {
        #region Types
        private class BufferSegment
        {
            static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

            public byte[] Buffer;

            public FileStream FileBuffer;

            public int Written = 0;
            public int PositionRead = 0;
            public int Index = -1;

            public bool IsWritting = false;

            /// <summary>
            /// In shared mode writing is processed from beginning of the buffer while still reading end of the buffer.
            /// Activated only if number of buffers is > 1.
            /// </summary>
            public bool IsShared = false;

            public bool IsEmpty
            {
                get
                {
                    return this.Written == 0 && !this.IsWritting;
                }
            }

            public int PositionWrite
            {
                get
                {
                    if (this.IsShared)
                        return (this.PositionRead + this.Written) % this._Size;
                    else
                        return this.PositionRead + this.Written;
                }
            }

            private int _Size;

            public bool IsFull
            {
                get
                {
                    //if (this.Buffer == null)
                    //    throw new NullReferenceException("Buffer is null");

                    return this.PositionWrite >= this._Size;
                }
            }

            public int FreeSize
            {
                get
                {
                    return this._Size - this.PositionWrite;
                }
            }

            public BufferSegment(int iSize, string strFileBufferPath)
            {
                if (!string.IsNullOrWhiteSpace(strFileBufferPath))
                    this.FileBuffer = new FileStream(strFileBufferPath, FileMode.Create);
                else
                    this.Buffer = new byte[iSize];

                this._Size = iSize;
            }

            public void Close()
            {
                if (this.FileBuffer != null)
                {
                    try
                    {
                        this.FileBuffer.Close();
                        File.Delete(this.FileBuffer.Name);
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[destroyBuffers] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    }

                    this.FileBuffer = null;
                }
            }
        }
        #endregion

        #region Constants
        private const int MIN_BUFFER_SIZE = 1024 * 1;
        private const int PREBUFFER_DEFAULT = MIN_BUFFER_SIZE * 1;

        private const int BUFFER_SEGMENT_LIFE_TIME = 30000; //[ms]
        #endregion

        #region Private Fields
        private static int _IdCounter = -1;

        private int _Id = 0;
        private int _IdFileCounter = 0;

        private List<BufferSegment> _Buffers = new List<BufferSegment>();
        private int _BuffersMin = 1;
        private int _BuffersMax = 8;
        private int _BufferSegmentSize = 256 * 1024;
        private BufferSegment _BufferRd = null;
        private BufferSegment _BufferWr = null;
        private int _BuffersLevel = 0;

        private byte[] _Buffer = null;

        private string _FileBufferDir = null;

        private int _BuffersInUse = 0;
        private int _BuffersInUseDiffMin = int.MaxValue;
        private DateTime _BuffersInUseDiffMinTs = DateTime.Now;

        private ManualResetEvent _WriteSignal = new ManualResetEvent(false);
        private ManualResetEvent _ReadSignal = new ManualResetEvent(false);
        private ManualResetEvent _WaitSignal = new ManualResetEvent(false);
        private ManualResetEvent _ReleaseWaitSignal = new ManualResetEvent(false);

        private BufferDataHandler _Callback;
        private Thread _ProcessThread = null;
        //private int _DataReadPosition = 0;
        //private int _DataWritePosition = 0;
        private volatile bool _BufferFull = false;
        private volatile bool _Run = false;
        private volatile bool _Wait = false;
        private volatile bool _Flush = false;
        private volatile bool _Empty = false;

        private volatile bool _Terminate = false;

        private volatile bool _Closing = false;

        private object _Padlock = new object();

        private EventHandler _FlushDoneCallback = null;

        private int _WriteAlign = 1;

        static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Public Properties
        public bool Run
        {
            get
            {
                return this._Run;
            }

            set
            {
                this._Run = value;
                if (value)
                    this._WriteSignal.Set();

                _Logger.Debug("[Run] " + value);
            }
        }

        public int CurrentLevel
        {
            get
            {
                lock (this._Padlock)
                {
                    return (int)(((float)this.CurrentValue / this.BufferSizeMax) * 100);
                }
            }
        }

        public int CurrentValue
        {
            get
            {
                lock (this._Padlock)
                {
                    if (this._BufferFull)
                        return this.BufferSize;
                    else
                        return this._BuffersLevel;
                }
            }
        }

        public int BufferSize
        {
            get
            {
                return this._BufferSegmentSize * this._Buffers.Count;
            }
        }

        public int BufferSizeMax
        {
            get
            {
                return this._BufferSegmentSize * this._BuffersMax;
            }
        }

        public int PositionRead
        {
            get
            {
                BufferSegment buf = this._BufferRd;
                if (buf != null)
                    return buf.Index;
                else
                    return -1;
            }
        }

        public int PositionWrite
        {
            get
            {
                BufferSegment buf = this._BufferWr;
                if (buf != null)
                    return buf.Index;
                else
                    return -1;
            }
        }

        public int Buffers
        {
            get
            {
                List<BufferSegment> res = this._Buffers;
                return res != null ? this._Buffers.Count : 0;
            }
        }

        public bool LogBufferOverrun = false;
        public bool LogBufferUnderrun = false;

        public int Prebuffer
        {
            get
            {
                return this._Prebuffer;
            }

            set
            {
                if (value < 1)
                    this._Prebuffer = 1;
                else
                    this._Prebuffer = value;
            }
        }private int _Prebuffer = PREBUFFER_DEFAULT;

        public int BufferSegmentSize
        {
            get
            {
                return this._BufferSegmentSize;
            }
        }

        public int BuffersMax
        {
            get
            {
                return this._BuffersMax;
            }

            set
            {
                if (value > this._BuffersMax)
                {
                    this._BuffersMax = value;

                    _Logger.Debug("[" + this._Id + "][BuffersMax] Set: " + value);
                }
            }
        }

        public bool WriteWaitsWhenFull = true;

        public bool IsClosing
        {
            get
            {
                return this._Closing;
            }
        }

        public bool IsClosed
        {
            get
            {
                return this._Closing && this._ProcessThread == null;
            }
        }

        public int BuffersInUse
        {
            get { return _BuffersInUse; }
        }

        public int BufferSegmentLifeTime
        {
            get
            {
                return this._BufferSegmentLifeTime;
            }

            set
            {
                if (value < 100)
                    this._BufferSegmentLifeTime = 100;
                else
                    this._BufferSegmentLifeTime = value;
            }
        }private int _BufferSegmentLifeTime = BUFFER_SEGMENT_LIFE_TIME;

        public int WriteAlign
        {
            get
            {
                return this._WriteAlign;
            }

            set
            {
                if (value < 1)
                    this._WriteAlign = 1;
                else
                    this._WriteAlign = value;
            }
        }

        public int RemoveUnusedBuffersTreshold
        {
            get
            {
                return this._RemoveUnusedBuffersTreshold;
            }

            set
            {
                if (value < 1)
                    this._RemoveUnusedBuffersTreshold = 1;
                else
                    this._RemoveUnusedBuffersTreshold = value;
            }
        }private int _RemoveUnusedBuffersTreshold = 3;

        #endregion

        #region ctor
        static ArrayBuffer()
        {
            Logging.Log.Init();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback">callback to read data from buffer</param>
        /// <param name="iBufferSize">size of one buffer segment</param>
        /// <param name="iMinBuffers">max buffer segments</param>
        /// <param name="iMaxBuffers">min buffer segments</param>
        public ArrayBuffer(BufferDataHandler callback, int iBufferSize, int iMinBuffers, int iMaxBuffers)
        {
            this._Id = Interlocked.Increment(ref _IdCounter);

            this.init(callback, iBufferSize, iMinBuffers, iMaxBuffers, null);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback">callback to read data from buffer</param>
        /// <param name="iBufferSize">size of one buffer segment</param>
        /// <param name="iMinBuffers">max buffer segments</param>
        /// <param name="iMaxBuffers">min buffer segments (2)</param>
        /// <param name="strFileBufferDir">file directory for buffer segments</param>
        public ArrayBuffer(BufferDataHandler callback, int iBufferSize, int iMinBuffers, int iMaxBuffers, string strFileBufferDir)
        {
            this._Id = Interlocked.Increment(ref _IdCounter);

            this.init(callback, iBufferSize, iMinBuffers, iMaxBuffers, strFileBufferDir);
        }
        #endregion

        #region Public Prototypes

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            if (this._Closing)
                return;

            _Logger.Debug("[" + this._Id + "][Close]");

            this._Closing = true;

            if (this._ProcessThread != null)
            {
                this.Flush();

                this._Terminate = true;
                this._ReadSignal.Set();
                this._WriteSignal.Set();

                while (this._ProcessThread.IsAlive)
                { Thread.Sleep(20); }

                this._ProcessThread = null;

                //if (this._BufferFile != null)
                //    this._BufferFile.Close();

                this.buffersDestroy();
            }

            _Logger.Debug("[" + this._Id + "][Close] Done.");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public int Write(byte[] buffer, int iOffset, int iLength)
        {
            if (this._Closing)
                return 0;

            bool bFull;
            int iIdxWr;
            int iIdxRd;

            while (!this._Terminate && iLength > 0)
            {
                iIdxRd = -1;
                lock (this._Padlock)
                {
                    bFull = false;

                    if (this._BufferWr.IsShared)
                    {
                        //We are still in share with read buffer
                        if ((this._BufferSegmentSize - this._BufferWr.Written) < this._WriteAlign)
                            bFull = true; //no room for another data
                        else
                            iIdxRd = this._BufferWr.PositionRead; //We can start writting up to iIdxRd point
                    }
                    else if (this._BufferWr.FreeSize < this._WriteAlign)
                    {
                        //Current buffer is already full

                        //get next buffer index
                        int iIdxNext = this._BufferWr.Index + 1;
                        if (iIdxNext >= this._Buffers.Count)
                            iIdxNext = 0;

                        if (this._Buffers[iIdxNext].IsEmpty)
                            this._BufferWr = this._Buffers[iIdxNext]; //next buffer is empty - us it
                        else
                        {
                            //Next buffer is not empty; try insert a new one

                            if (this._Buffers.Count < this._BuffersMax)
                            {
                                //Insert a new buffer segment
                                this.bufferInsert(iIdxNext, this._BufferSegmentSize);
                                this._BufferWr = this._Buffers[iIdxNext];
                            }
                            else
                            {
                                //Buffers limit reached

                                if (this._Buffers.Count > 1 && (iIdxRd = this._Buffers[iIdxNext].PositionRead) >= this._WriteAlign)
                                {
                                    //Take next buffer and switch it to shared mode
                                    //We can start writting from begining up to iIdxRd point
                                    this._BufferWr = this._Buffers[iIdxNext];
                                    this._BufferWr.IsShared = true;
                                }
                                else
                                    bFull = true;
                            }
                        }

                    }


                    //Get destination write position
                    iIdxWr = this._BufferWr.PositionWrite;

                    if (!bFull)
                        this._BufferWr.IsWritting = true; //We are going to write some data
                    else
                    {
                        //if (this.LogBufferOverrun)
                        //    _Logger.Warn("[" + this._Id + "][Write] Buffers limit reached:" + this._BuffersMax);

                        //Entire buffer is full
                        this._BufferFull = true;

                        this._WriteSignal.Set();
                    }

                }//release lock

                if (bFull)
                {
                    //Full

                    if (this.LogBufferOverrun)
                        _Logger.Warn("[" + this._Id + "][Write] Buffer full.");

                    if (this.WriteWaitsWhenFull)
                    {
                        this._ReadSignal.WaitOne();
                        this._ReadSignal.Reset();

                        if (this.LogBufferOverrun)
                            _Logger.Debug("[" + this._Id + "][Write] Buffer full. Signal received.");
                    }
                    else
                        return iLength; //return remaining data length
                }
                else
                {
                    int iToWrite = (iIdxRd > 0 ? iIdxRd : this._BufferSegmentSize) - iIdxWr; //max to write
                    if (iLength > iToWrite)
                        iToWrite -= (iToWrite % this._WriteAlign);
                    else
                        iToWrite = iLength;

                    if (this._BufferWr.FileBuffer != null)
                    {
                        lock (this._BufferWr.FileBuffer)
                        {
                            this._BufferWr.FileBuffer.Position = iIdxWr;
                            this._BufferWr.FileBuffer.Write(buffer, iOffset, iToWrite);
                        }
                    }
                    else
                        //Copy data to current buffer segment
                        Buffer.BlockCopy(buffer, iOffset, this._BufferWr.Buffer, iIdxWr, iToWrite);

                    lock (this._Padlock)
                    {
                        //Advance total buffer level
                        this._BuffersLevel += iToWrite;

                        //Advance current buffer level
                        this._BufferWr.Written += iToWrite;

                        //Writting done
                        this._BufferWr.IsWritting = false;

                        //Rise the signal if read process is waiting for a new data
                        if (this._Empty)
                        {
                            this._Empty = false;
                            this._WriteSignal.Set();
                        }

                        //Calculate number of buffers in use
                        if (this._BufferWr.IsShared)
                            this._BuffersInUse = this._Buffers.Count;
                        else if (this._BufferWr.Index >= this._BufferRd.Index)
                            this._BuffersInUse = this._BufferWr.Index - this._BufferRd.Index + 1;
                        else
                            this._BuffersInUse = this._Buffers.Count - this._BufferRd.Index + this._BufferWr.Index + 1;

                        //Buffers maintanance
                        int iDiff = this._Buffers.Count - this._BuffersInUse;
                        if (iDiff < this._RemoveUnusedBuffersTreshold)
                        {
                            this._BuffersInUseDiffMin = int.MaxValue;
                            this._BuffersInUseDiffMinTs = DateTime.Now;
                        }
                        else if ((DateTime.Now - this._BuffersInUseDiffMinTs).TotalMilliseconds > this._BufferSegmentLifeTime)
                        {
                            //Get first buffer before readBuffer
                            int iIdxToRemove = this._BufferRd.Index - 1;
                            if (iIdxToRemove < 0)
                                iIdxToRemove = this._Buffers.Count - 1;

                            BufferSegment buf;
                            int iCnt = Math.Max(1, this._BuffersInUseDiffMin / 2); //take half of min difference
                            while (iCnt-- > 0)
                            {
                                buf = this._Buffers[iIdxToRemove];

                                if (buf.IsEmpty && buf != this._BufferWr && buf != this._BufferRd)
                                    this.bufferRemove(iIdxToRemove); //buffer is free; remove it
                                else
                                    break;
                            }
                            buf = null;

                            this._BuffersInUseDiffMin = int.MaxValue;
                            this._BuffersInUseDiffMinTs = DateTime.Now;
                        }
                        else if (iDiff < this._BuffersInUseDiffMin)
                            this._BuffersInUseDiffMin = iDiff; //meassure min difference during check period

                    }//release lock

                    iOffset += iToWrite;
                    iLength -= iToWrite;

                }
            }

            return iLength;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Reset()
        {
            if (this._Closing)
                return;

            _Logger.Debug("[" + this._Id + "][Reset]");

            this._ReleaseWaitSignal.Reset();
            this._Wait = true;
            this._WriteSignal.Set();
            this._WaitSignal.WaitOne();
            this._Wait = false;

            this._BuffersLevel = 0;
            this._BufferRd = this._Buffers[0];
            this._BufferWr = this._BufferRd;
            foreach (BufferSegment buff in this._Buffers)
            {
                buff.PositionRead = 0;
                buff.Written = 0;
                buff.IsWritting = false;
                buff.IsShared = false;
            }

            //this._DataReadPosition = 0;
            //this._DataWritePosition = 0;
            this._BufferFull = false;

            this._WriteSignal.Reset();
            this._ReadSignal.Reset();

            this._ReleaseWaitSignal.Set();

            _Logger.Debug("[" + this._Id + "][Reset] Done.");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Flush()
        {
            _Logger.Debug("[" + this._Id + "][Flush]");

            this._Flush = true;
            this._WriteSignal.Set();
            while (this.CurrentValue > 0)
            { Thread.Sleep(20); }

            this._Flush = false;

            _Logger.Debug("[" + this._Id + "][Flush] Done.");
        }

        //[MethodImpl(MethodImplOptions.Synchronized)]
        public void BeginFlush(EventHandler callback, bool bInitClosing)
        {
            _Logger.Debug("[" + this._Id + "][BeginFlush]");

            this._Closing = bInitClosing;
            this._FlushDoneCallback = callback;
            this._Flush = true;
            this._WriteSignal.Set();
        }

        #endregion

        #region Private Prototypes

        private void init(BufferDataHandler callback, int iBufferSize, int iMinBuffers, int iMaxBuffers, string strFileBufferPath)
        {
            if (callback == null)
                throw new ArgumentException("Callback is null.");

            if (iMinBuffers < 1 || iMaxBuffers < 1 || iMinBuffers > iMaxBuffers)
                throw new ArgumentException("Invalid buffer paramaters.");

            if (iBufferSize < MIN_BUFFER_SIZE)
                iBufferSize = MIN_BUFFER_SIZE;

            this._Callback = callback;

            this._BufferSegmentSize = iBufferSize;
            this._BuffersMin = iMinBuffers;
            this._BuffersMax = iMaxBuffers;
            this._BuffersInUse = 1;
            this._BuffersInUseDiffMinTs = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(strFileBufferPath) && Directory.Exists(strFileBufferPath))
            {
                this._FileBufferDir = strFileBufferPath.TrimEnd('\\');
                this._Buffer = new byte[1024 * 32];
            }

            while (iMinBuffers-- > 0)
            {
                this.bufferInsert(-1, this._BufferSegmentSize);
            }

            this._BufferRd = this._Buffers[0];
            this._BufferWr = this._BufferRd;

            //Start sender
            if (this._ProcessThread == null)
            {
                this._ProcessThread = new Thread(new ThreadStart(() => this.process()));
                this._ProcessThread.Start();
            }
        }

        private void bufferInsert(int iIdx, int iBufferSize)
        {
            string strFile = !string.IsNullOrWhiteSpace(this._FileBufferDir) ? this._FileBufferDir + "\\buffer_" + this._Id + '_' + this._IdFileCounter++ + ".tmp" : null;

            if (iIdx < 0 || iIdx >= this._Buffers.Count)
            {
                this._Buffers.Add(new BufferSegment(iBufferSize, strFile));

                //Set index
                this._Buffers[this._Buffers.Count - 1].Index = this._Buffers.Count - 1;

                if (Log.LogLevel <= LogLevel.Debug)
                    _Logger.Debug("[" + this._Id + "][bufferInsert] New buffer added. BufferSize:" + iBufferSize);
            }
            else
            {
                this._Buffers.Insert(iIdx, new BufferSegment(iBufferSize, strFile));

                if (Log.LogLevel <= LogLevel.Debug)
                    _Logger.Debug("[" + this._Id + "][bufferInsert] New buffer inserted at idx " + iIdx + ". BufferSize:" + iBufferSize);

                //Reindex following buffers
                for (int i = iIdx; i < this._Buffers.Count; i++)
                {
                    this._Buffers[i].Index = i;
                }
            }
        }

        private void bufferRemove(int iIdx)
        {
            if (iIdx >= 0 && iIdx < this._Buffers.Count)
            {
                this._Buffers[iIdx].Close();
                this._Buffers.RemoveAt(iIdx);

                if (Log.LogLevel <= LogLevel.Debug)
                    _Logger.Debug("[" + this._Id + "][bufferRemove] Buffer removed at idx " + iIdx);

                //Reindex following buffers
                for (int i = iIdx; i < this._Buffers.Count; i++)
                {
                    this._Buffers[i].Index = i;
                }
            }
            else
                _Logger.Error("[" + this._Id + "][bufferRemove] Invalid idx:" + iIdx + "  Buffers:" + this._Buffers.Count);
        }

        private void process()
        {
            int iBufferLevel;
            bool bFull;
            int iToRead;
            while (!this._Terminate)
            {
                lock (this._Padlock)
                {
                    //Get current data size in the buffer
                    if (this._BufferRd.IsShared)
                        iToRead = this._BufferSegmentSize - this._BufferRd.PositionRead;
                    else
                        iToRead = this._BufferRd.Written;

                    bFull = this._BufferFull;

                    if (bFull && iToRead == 0)
                        _Logger.Error("[" + this._Id + "][process] Buffer full flag set while no data in rdBuffer idx:" + this._BufferRd.Index);

                    if (bFull)
                        iBufferLevel = this._BufferSegmentSize * this._Buffers.Count;
                    else
                        iBufferLevel = this._BuffersLevel;
                }//release lock

                if (this._Wait)
                {
                    this._WaitSignal.Set();
                    this._ReleaseWaitSignal.WaitOne();
                }
                else if (!this._Run || iBufferLevel < (this._Flush ? 1 : this._Prebuffer) || iToRead == 0)
                {
                    if (this._Run && this.LogBufferUnderrun)
                        _Logger.Warn("[" + this._Id + "][process] Buffer empty.");

                    if (this._Flush && iBufferLevel < 1 && this._FlushDoneCallback != null)
                    {
                        BufferArgs e = new BufferArgs();
                        this._FlushDoneCallback(this, e);
                        this._FlushDoneCallback = null;
                        this._Flush = false;

                        if (e.Close)
                        {
                            //Request to close the buffer
                            this._Closing = true;
                            this._Terminate = true;
                            this._ReadSignal.Set();
                            this._WriteSignal.Set();
                            lock (this)
                            {
                                this.buffersDestroy();
                            }
                            this._ProcessThread = null;
                            _Logger.Debug("[" + this._Id + "][process] Close done.");
                            break;
                        }
                    }

                    this._Empty = true;
                    this._WriteSignal.WaitOne();
                    this._WriteSignal.Reset();

                    if (this._Run && this.LogBufferUnderrun)
                        _Logger.Warn("[" + this._Id + "][process] Buffer empty. Signal received.");
                }
                else
                {
                    if (iToRead == 0)
                    {
                        _Logger.Error("[" + this._Id + "][process] Reading empty buffer idx:" + this._BufferRd.Index);
                        continue;
                    }
                    else if (this._BufferRd.FileBuffer != null)
                    {
                        iToRead = Math.Min(this._Buffer.Length, iToRead);
                        lock (this._BufferRd.FileBuffer)
                        {
                            this._BufferRd.FileBuffer.Position = this._BufferRd.PositionRead;
                            this._BufferRd.FileBuffer.Read(this._Buffer, 0, iToRead);
                        }

                        iToRead -= this._Callback(this._Buffer, 0, iToRead); //Call delegate with the buffer data
                    }
                    else
                        iToRead -= this._Callback(this._BufferRd.Buffer, this._BufferRd.PositionRead, iToRead); //Call delegate with the buffer data

                    if (iToRead > 0)
                    {
                        lock (this._Padlock)
                        {
                            //Decrease total buffer level
                            this._BuffersLevel -= iToRead;

                            //Decrease current buffer level
                            this._BufferRd.Written -= iToRead;

                            //Advance read position
                            this._BufferRd.PositionRead += iToRead;

                            if (this._BufferRd.IsEmpty || (this._BufferRd.IsShared && this._BufferRd.PositionRead >= this._BufferSegmentSize))
                            {
                                //Current buffer is empty & no writing is in progress or buffer is shared with writing(from beginning of the buffer)

                                //Reset read position
                                this._BufferRd.PositionRead = 0;

                                //Reset shared flag
                                this._BufferRd.IsShared = false;

                                //Get next buffer idx
                                int iIdxNext = this._BufferRd.Index + 1;
                                if (iIdxNext >= this._Buffers.Count)
                                    iIdxNext = 0;

                                //Move to next buffer if there are new data or currently beeing written
                                if (!this._Buffers[iIdxNext].IsEmpty)
                                    this._BufferRd = this._Buffers[iIdxNext];
                            }

                            if (this._BufferFull)
                            {
                                //Clear buffer full flag
                                this._BufferFull = false;
                                this._ReadSignal.Set();
                            }

                            if (this._BuffersLevel < (this._Flush ? 1 : this._Prebuffer))
                                this._Empty = true; //We are empty

                        }//release lock
                    }
                    else
                        _Logger.Warn("[" + this._Id + "][process] No data accepted from callback.");

                }

            }

            //Make sure the write process do not hang
            this._ReadSignal.Set();

            _Logger.Debug("[" + this._Id + "][process] Terminated.");
        }

        private void buffersDestroy()
        {
            foreach (BufferSegment buf in this._Buffers)
            {
                buf.Close();
            }
            this._Buffers.Clear();
            this._BufferRd = null;
            this._BufferWr = null;
            this._Buffer = null;
        }

        #endregion

    }


}
