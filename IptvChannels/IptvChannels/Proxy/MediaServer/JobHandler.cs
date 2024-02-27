using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Reflection;
using NLog;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class JobHandler
    {
        #region Types
        class JobSlot
        {
            #region Constants

            #endregion

            #region Private Fields
            static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

            private bool _Abort = false;
            private bool _Terminate = false;


            private Thread _Process = null;
            private ManualResetEvent _FlagWake = new ManualResetEvent(false);
            private ManualResetEvent _FlagDone = new ManualResetEvent(false);

            private EventHandler _CallbackJobDone = null;

            private static int _IdCounter = 0;
            private int _Id;
            #endregion

            #region Public Properties
            public IJob Item
            {
                get
                {
                    return this._Item;
                }
            }private IJob _Item = null;

            public bool IsAvailable
            {
                get
                {
                    return this._Result == JobStatus.Iddle;
                }
            }

            public bool IsRunning
            {
                get
                {
                    return this._Result >= JobStatus.Started;
                }
            }

            public bool IsFinished
            {
                get
                {
                    return this._Result < JobStatus.Started;
                }
            }

            public bool IsTerminated
            {
                get
                {
                    return this._Process == null || (!this._Process.IsAlive && this._Result < JobStatus.Started);
                }
            }

            public JobStatus Result
            {
                get
                {
                    return this._Result;
                }
            }private JobStatus _Result = JobStatus.Iddle;

            public bool ReleaseUponDone
            {
                get
                {
                    return this._ReleaseUponDone;
                }
            }private bool _ReleaseUponDone = false;

            public DateTime TimeStampStart
            {
                get
                {
                    return this._TimeStampStart;
                }
            }private DateTime _TimeStampStart;

            public DateTime TimeStampEnd
            {
                get
                {
                    return this._TimeStampEnd;
                }
            }private DateTime _TimeStampEnd;

            public JobResources Resources = null;
            #endregion

            #region ctor
            public JobSlot(EventHandler callbackJobDone)
            {
                this._Id = System.Threading.Interlocked.Increment(ref _IdCounter);

                this._CallbackJobDone = callbackJobDone;
            }
            #endregion

            #region Public Methods

            [MethodImpl(MethodImplOptions.Synchronized)]
            public void Release()
            {
                if (this._Result < JobStatus.Started)
                {
                    this._Result = JobStatus.Iddle;
                    this._Item = null;
                }
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public void Abort()
            {
                this._Abort = true;
                IJob job = this._Item;
                if (job != null)
                    job.JobAbort();
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public void Terminate()
            {
                this._Terminate = true;
                this.Abort();
                this._FlagWake.Set();
            }

            public bool WaitForFinish(int iTimeout)
            {
                return this._FlagDone.WaitOne(iTimeout);
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public void Start(IJob item, bool bReleaseUponDone)
            {
                if (this._Result != JobStatus.Iddle)
                    return;

                //Init new job
                this._Abort = false;
                this._Result = JobStatus.Started;
                this._Item = item;
                this._Item.JobStatus = JobStatus.Started;
                this._ReleaseUponDone = bReleaseUponDone;
                this._FlagWake.Set();

                //Start process thread if not running
                if (this._Process == null)
                {
                    this._Process = new Thread(new ThreadStart(() => this.process()));
                    this._Process.Start();
                }
            }

            public bool Join()
            {
                return this.Join(-1);
            }
            public bool Join(int iTimeout)
            {
                Thread t = this._Process;
                if (t == null)
                    return true;

                return t.Join(iTimeout);
                    
            }
            #endregion

            #region Private Methods
            private void process()
            {
                //Main loop
                while (!this._Terminate)
                {
                    this._Item.JobStatus = JobStatus.Running;
                    this._FlagWake.Reset();
                    this._FlagDone.Reset();
                    this._Result = JobStatus.Running;

                    JobEventArgs args = new JobEventArgs() { Job = this._Item };

                    #region Job
                    try
                    {
                        this._TimeStampStart = DateTime.Now;
                        this._Item.JobFlagDone.Reset();

                        //Event
                        if (this._Item.JobEvent != null)
                        {
                            try { this._Item.JobEvent(this, args); }
                            catch { }
                        }

                        //Do the job
                        this._Result = this._Item.DoJob(ref this.Resources);

                        //Event
                        if (this._Item.JobEvent != null)
                        {
                            try { this._Item.JobEvent(this, args); }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[process] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                        this._Result = JobStatus.Error;
                        this._Item.JobStatus = JobStatus.Error;
                    }
                    finally
                    {
                        this._TimeStampEnd = DateTime.Now;
                    }

                    #endregion

                    //Rise done flag
                    this._Item.JobFlagDone.Set();
                    this._FlagDone.Set();

                    if (this._ReleaseUponDone)
                        this.Release();

                    if (this._CallbackJobDone != null)
                        this._CallbackJobDone(this, args);

                    //Wait for another job
                    this._FlagWake.WaitOne();
                }

                this._Result = JobStatus.Terminated;
            }
            #endregion
        }
        #endregion

        #region Private Fields
        private bool _Terminate = false;
        private List<JobSlot> _Slots = new List<JobSlot>();
        private List<IJob> _Queue = new List<IJob>();
        private System.Timers.Timer _TimerMaintenance = null;
        static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Public Properties
        public bool IsSlotAvailable
        {
            get
            {
                lock (this._Slots)
                {
                    for (int i = 0; i < this._Slots.Count; i++)
                    {
                        if (this._Slots[i].IsAvailable)
                            return true;
                    }

                    return false;
                }

            }
        }

        public bool IsAnySlotRunning
        {
            get
            {
                lock (this._Slots)
                {
                    for (int i = 0; i < this._Slots.Count; i++)
                    {
                        if (this._Slots[i].IsRunning)
                            return true;
                    }

                    return false;
                }

            }
        }

        public int SlotsCurrent
        {
            get
            {
                lock (this._Slots)
                {
                    return this._Slots.Count;
                }
            }
        }

        public int SlotsMax
        {
            get
            {
                return this._SlotsMax;
            }

            set
            {
                if (value < 1)
                    this._SlotsMax = -1;
                else if (value > 100)
                    this._SlotsMax = 100;
                else
                    this._SlotsMax = value;
            }
        }private int _SlotsMax = 3;

        public int SlotLifeTime
        {
            get
            {
                return this._SlotLifeTime;
            }

            set
            {
                if (value < 1)
                    this._SlotLifeTime = -1;
                else if (value > 600000)
                    this._SlotLifeTime = 600000;
                else
                    this._SlotLifeTime = value;
            }
        }private int _SlotLifeTime = 60000;
        #endregion

        #region ctor
        public JobHandler()
        {
            this._TimerMaintenance = new System.Timers.Timer();
            this._TimerMaintenance.Interval = 30 * 1000;
            this._TimerMaintenance.AutoReset = true;
            this._TimerMaintenance.Elapsed += new System.Timers.ElapsedEventHandler(this.cbTimerElapsed);
            this._TimerMaintenance.Enabled = true;
        }
        #endregion

        #region Public Methods
        [MethodImpl(MethodImplOptions.Synchronized)]
        public JobHandlerStatus StartNewJob(IJob item, bool bReleaseUponDone)
        {
            if (this._Terminate)
                return JobHandlerStatus.Terminated;

            lock (this._Slots)
            {
                //Check for already existing
                for (int i = 0; i < this._Slots.Count; i++)
                {
                    if (this._Slots[i].Item == item)
                    {
                        _Logger.Warn("[StartNewJob] Already exist: " + item.JobTitle);
                        return JobHandlerStatus.JobAlreadyExist; //already exist
                    }
                }

                if (item.JobSlotsMax <= 0 || item.JobSlotsInUse < item.JobSlotsMax) //Max limit check
                {
                    //Try find free slot
                    for (int i = 0; i < this._Slots.Count; i++)
                    {
                        if (this._Slots[i].IsAvailable)
                        {
                            _Logger.Debug("[StartNewJob] " + item.JobTitle);

                            item.JobSlotsInUse++;
                            this._Slots[i].Start(item, bReleaseUponDone);
                            return JobHandlerStatus.JobCreated;
                        }
                    }

                    //Try create new slot
                    if (this._SlotsMax <= 0 || this._Slots.Count < this._SlotsMax)
                    {
                        JobSlot slot = new JobSlot(this.cbJobSlotDone);
                        this._Slots.Add(slot);
                        _Logger.Debug("[StartNewJob] New slot created. Current slots:" + this._Slots.Count);

                        _Logger.Debug("[StartNewJob] " + item.JobTitle);

                        item.JobSlotsInUse++;
                        slot.Start(item, bReleaseUponDone);

                        return JobHandlerStatus.JobCreated;
                    }
                }


                ////Check maximum slots in case of change max value
                //while (this._SlotsMax > 0 && this._Slots.Count > this._SlotsMax)
                //{
                //    for (int i = 0; i < this._Slots.Count; i++)
                //    {
                //        JobSlot slot = this._Slots[i];
                //        if (slot.IsAvailable)
                //        {
                //            slot.Terminate();
                //            slot.Join(-1);
                //            this._Slots.RemoveAt(i);
                //            _Logger.Debug("[StartNewJob] Removing slot. Current slots:" + this._Slots.Count);
                //            goto try_again; //check limit again
                //        }
                //    }

                //    break; //no more free slots to remove so far

                //try_again:
                //    continue;
                //}

                this.doMaintenance();
            }

            return JobHandlerStatus.NoSlotAvailable; //no slot available
        }

        public JobStatus WaitForJob(IJob item, int iTimeout, bool bReleaseWhenDone)
        {
            JobSlot slot = null;

            lock (this._Slots)
            {
                slot = this._Slots.Find(p => p.Item == item);
            }

            if (slot != null)
            {
                if (slot.WaitForFinish(iTimeout))
                {
                    if (bReleaseWhenDone && slot.Result == JobStatus.Complete)
                    {
                        slot.Release();
                        return JobStatus.Complete;
                    }
                    else
                        return slot.Result;
                }
                else
                    return JobStatus.Timeout;
            }   
            
            return JobStatus.NotAvailable;
        }

        public void WaitForAll(Predicate<IJob> match, int iTimeout)
        {
            lock (this._Slots)
            {
                for (int i = 0; i < this._Slots.Count; i++)
                {
                    JobSlot slot = this._Slots[i];

                    if (match(slot.Item))
                        slot.WaitForFinish(iTimeout);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool JobExist(IJob item)
        {
            lock (this._Slots)
            {
                for (int i = 0; i < this._Slots.Count; i++)
                {
                    JobSlot slot = this._Slots[i];

                    if (slot.Item == item)
                        return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ReleaseJob(IJob item)
        {
            lock (this._Slots)
            {
                for (int i = 0; i < this._Slots.Count; i++)
                {
                    JobSlot slot = this._Slots[i];

                    if (slot.Item == item)
                    {
                        if (slot.IsFinished)
                            slot.Release();

                        return;
                    }
                }
            }

            this.runJobs();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ReleaseFinishedJobs()
        {
            lock (this._Slots)
            {
                for (int i = 0; i < this._Slots.Count; i++)
                {
                    JobSlot slot = this._Slots[i];

                    if (slot.Result == JobStatus.Complete)
                        slot.Release();
                }
            }

            this.runJobs();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Abort(IJob item)
        {
            lock (this._Slots)
            {
                for (int i = 0; i < this._Slots.Count; i++)
                {
                    JobSlot slot = this._Slots[i];

                    if (slot.Item == item)
                    {
                        slot.Abort();
                        return;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AbortAll(Predicate<IJob> match)
        {
            lock (this._Slots)
            {
                for (int i = 0; i < this._Slots.Count; i++)
                {
                    JobSlot slot = this._Slots[i];

                    if (match(slot.Item))
                        slot.Abort();
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Terminate()
        {
            this._Terminate = true;

            this.ClearQueue();

            for (int i = 0; i < this._Slots.Count; i++)
            {
                this._Slots[i].Terminate();
            }

            for (int i = 0; i < this._Slots.Count; i++)
            {
                //while (!this._Slots[i].IsTerminated)
                //{
                //    Thread.Sleep(10);
                //}

                this._Slots[i].Join();
            }

            if (this._TimerMaintenance != null)
            {
                this._TimerMaintenance.Enabled = false;
                this._TimerMaintenance.Elapsed -= new System.Timers.ElapsedEventHandler(this.cbTimerElapsed);
                this._TimerMaintenance.Dispose();
                this._TimerMaintenance = null;
            }

            this._Slots.Clear();
            this._Slots = null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool AddToQueue(IJob job, EventHandler callbackAdded)
        {
            if (this._Terminate)
                return false;

            lock (this._Queue)
            {
                if (this._Queue.Exists(p => p == job))
                    return false;

                this._Queue.Add(job);

                _Logger.Debug("[AddToQueue] " + job.JobTitle);

                if (callbackAdded != null)
                {
                    try { callbackAdded(this, new JobEventArgs() { Job = job }); }
                    catch { }
                }

                this.runJobs();

                return true;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool RemoveFromQueue(IJob job)
        {
            lock (this._Queue)
            {
                bool bResult = this._Queue.Remove(job);

                if (bResult)
                    _Logger.Debug("[RemoveFromQueue] " + job.JobTitle);
                return bResult;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveAllFromQueue(Predicate<IJob> match)
        {
            lock (this._Queue)
            {
                this._Queue.RemoveAll(match);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ClearQueue()
        {
            lock (this._Queue)
            {
                this._Queue.Clear();
            }
        }
        #endregion

        private void cbTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.doMaintenance();
        }

        private void cbJobSlotDone(object sender, EventArgs e)
        {
            _Logger.Debug("[cbJobSlotDone] Result:{0} {1}", ((JobEventArgs)e).Job.JobStatus, ((JobEventArgs)e).Job.JobTitle);

            lock (this._Slots)
            {
                ((JobEventArgs)e).Job.JobSlotsInUse--;
            }

            lock (this._Queue)
            {
                this._Queue.Remove(((JobEventArgs)e).Job);
            }

            if (!this._Terminate)
                this.runJobs();

        }


        private void runJobs()
        {
            if (this._Terminate)
                return;

            lock (this._Queue)
            {
                int i = 0;
                while (i < this._Queue.Count)
                {
                    if (this._Queue[i].JobStatus == JobStatus.Iddle)
                        this.StartNewJob(this._Queue[i], true);
                    
                    i++;
                }
            }
        }

        private void doMaintenance()
        {
            lock (this._Slots)
            {
                for (int i = this._Slots.Count - 1; i >= 0 ; i--)
                {
                    JobSlot slot = this._Slots[i];
                    if (slot.IsAvailable && 
                        ((this._SlotsMax > 0 && this._Slots.Count > this._SlotsMax) || //max slot limit
                        (this._SlotLifeTime > 0 && (DateTime.Now - slot.TimeStampEnd).TotalMilliseconds >= this._SlotLifeTime)) //slot lifetime
                        )
                    {
                        slot.Terminate();
                        slot.Join();
                        this._Slots.RemoveAt(i);
                        _Logger.Debug("[doMaintenance] Removing slot. Current slots:" + this._Slots.Count);
                    }
                }
            }
        }
    }
}

