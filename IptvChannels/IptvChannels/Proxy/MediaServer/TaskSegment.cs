﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using MediaPortal.Pbk.Logging;
using NLog;


namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public abstract class TaskSegment
    {
        public string GroupID = null;
        public string Filename;
        public string FullPath;
        public string Url;
        public Pbk.Net.Http.HttpUserWebRequestArguments HttpArguments;
        public int ID;
        public int UID { get; private set; }

        public TaskEventArgs ArgsUpdate;
        public TaskEventArgs ArgsStateChanged;

        public TaskStatusEnum Status = TaskStatusEnum.Iddle;

        public int Attempts = 3;
        public DateTime DateTime;
        public int Index = -1;
        public double Duration;

        public string ErrorDescription = string.Empty;

        public bool IsInCurrentList = false;
        public bool IsInCurrentListTmp = false;

        public bool Discontinuity = false;

        public DateTime LastAccess = DateTime.MinValue;

        public int InUse = 0;

        public object Tag;

        protected Task _Task;
        protected Logger _Logger = LogManager.GetCurrentClassLogger();
        protected int _Starting = 0;
        protected bool _Abort = false;
        private static int _UidCounter = -1;

        public TaskSegment(Task task)
        {
            this.UID = Interlocked.Increment(ref _UidCounter);
            this._Task = task;
            this.ArgsUpdate = new TaskEventArgs() { Type = TaskEventTypeEnum.SegmentUpdate, Tag = this };
            this.ArgsStateChanged = new TaskEventArgs() { Type = TaskEventTypeEnum.SegmentStateChanged, Tag = this };
        }

        //protected NLog.Logger Logger
        //{
        //    get
        //    {
        //        if (this._Logger == null)
        //        {
        //            Type t = this.GetType();
        //            this._Logger = LogManager.GetLogger(t.FullName);
        //            Log.AddRule(t.FullName);
        //        }
        //        return this._Logger;
        //    }
        //}

    }
}
