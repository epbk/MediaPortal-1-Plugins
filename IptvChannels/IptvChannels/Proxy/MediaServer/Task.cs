using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Reflection;
using MediaPortal.Pbk.Logging;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Net;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public abstract class Task// : DbTable
    {
        protected class AsyncResultInternal : IAsyncResult
        {
            private object _State;
            private bool _IsCompleted = false;

            public bool Result
            {
                get
                {
                    return this._Result;
                }
            }private bool _Result = false;

            private ManualResetEvent _WaitHandle = new ManualResetEvent(false);

            public AsyncResultInternal(object state)
            {
                this._State = state;
            }

            public object AsyncState
            {
                get { return this._State; }
            }

            public WaitHandle AsyncWaitHandle
            {
                get { return this._WaitHandle; }
            }

            public bool CompletedSynchronously
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsCompleted
            {
                get { return this._IsCompleted; }
            }

            public void SetComplete(bool bResult, AsyncCallback cb)
            {
                this._Result = bResult;
                this._IsCompleted = true;
                this._WaitHandle.Set();

                if (cb != null)
                {
                    try { cb(this); }
                    catch { }
                }
            }
        }


        public const string HTTP_FILENAME_M3U_LIST = "Playlist.m3u";
        public const string HTTP_FILENAME_M3U_KEY = "Key.bin";

        #region Database Fields
        //[DBFieldAttribute(FieldName = "enable", Default = "False")]
        [DefaultValue(false)]
        [Description("Start upon application run.")]
        [Category("Task")]
        [Browsable(false)]
        public bool Enable
        { get; set; }

        //[DBFieldAttribute(FieldName = "title", Default = "")]
        [DefaultValue("")]
        [Category("Task")]
        public string Title
        { get; set; }

        //[DBFieldAttribute(FieldName = "url", Default = "")]
        [DefaultValue("")]
        [Category("Task")]
        [DisplayName("URL")]
        public string Url
        { get; set; }

        public int Identifier
        { get { return this._Identifier; } } protected int _Identifier;

        #endregion

        private Logger _Logger;

        private static int _IdentifierCounter = 0;

        protected List<TaskSegment> _Segments = new List<TaskSegment>();

        protected int _Stopping = 0;
        protected int _Starting = 0;

        public int RunningJobs = 0;

        public bool Abort = false;

        public event EventHandler Event;

        [ReadOnly(true)]
        [Category("Task")]
        public TaskStatusEnum Status
        {
            get
            {
                return this._Status;
            }

            set
            {
                if (value == this._Status)
                    return;

                this._Status = value;

                if (this.Event != null)
                {
                    try
                    {
                        this.Event(this, new TaskEventArgs() { Type = TaskEventTypeEnum.TaskStateChanged });
                    }
                    catch { }
                }
            }
        }private TaskStatusEnum _Status = TaskStatusEnum.Iddle;


        [Browsable(false)]
        public TaskSegment[] Segments
        {
            get
            {
                lock (this._Segments)
                {
                    return this._Segments.ToArray();
                }
            }
        }

        [ReadOnly(true)]
        [Category("Task")]
        [DisplayName("Playlist URL")]
        public string PlaylistUrl
        {
            get { return this.getPlaylistUrl(); }
        }

        [Browsable(false)]
        public abstract string WorkFolder
        {
            get;
        }

        [Browsable(false)]
        public abstract string Prefix
        {
            get;
        }

        //chunk_5_10_20210224-122225_00000013_0000009520000.ts
        protected static Regex _RegexChunk = new Regex("chunk_(?<hls_size>\\d+)_(?<hls_time>\\d+)_(?<date>\\d{8})-(?<time>\\d{6})_(?<index>\\d+)_(?<chunk_duration>\\d{13})\\.ts");

        public Task()
        {
            this._Identifier = System.Threading.Interlocked.Increment(ref _IdentifierCounter);
        }


        public abstract bool Start();

        public abstract bool Stop();

        public virtual IAsyncResult BeginStart(AsyncCallback callback, object state)
        {
            if (Interlocked.CompareExchange(ref this._Starting, 1, 0) == 0)
            {
                AsyncResultInternal ar = new AsyncResultInternal(state);

                new Thread(new ParameterizedThreadStart((o) =>
                {
                    ((AsyncResultInternal)((object[])o)[1]).SetComplete(this.Start(), (AsyncCallback)((object[])o)[0]);

                })).Start(new object[] { callback, ar });

                return ar;
            }
            else
                return null;
        }

        public virtual bool EndStart(IAsyncResult ar)
        {
            return ((AsyncResultInternal)ar).IsCompleted && ((AsyncResultInternal)ar).Result;
        }

        public virtual IAsyncResult BeginStop(AsyncCallback callback, object state)
        {
            if (Interlocked.CompareExchange(ref this._Stopping, 1, 0) == 0)
            {
                AsyncResultInternal ar = new AsyncResultInternal(state);

                new Thread(new ParameterizedThreadStart((o) =>
                {
                    ((AsyncResultInternal)((object[])o)[1]).SetComplete(this.Stop(), (AsyncCallback)((object[])o)[0]);

                })).Start(new object[] { callback, ar });

                return ar;
            }
            else
                return null;
        }

        public virtual bool EndStop(IAsyncResult ar)
        {
            return ((AsyncResultInternal)ar).IsCompleted && ((AsyncResultInternal)ar).Result;
        }

        public void OnEvent(EventArgs e)
        {
            if (this.Event != null)
            {
                try { this.Event(this, e); }
                catch (Exception ex)
                { 
                    _Logger.Error("[OnEvent] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                }
            }
        }

        protected void onSegmentStatusChanged(TaskSegment seg, TaskStatusEnum status)
        {
            //lock (seg)
            {
                seg.Status = status;
                this.OnEvent(seg.ArgsStateChanged);
            }
        }

        protected void cleanUpWorkDirectory()
        {
            //Clean work directory
            string strPath = this.WorkFolder;
            try
            {
                if (System.IO.Directory.Exists(strPath))
                {
                    DirectoryInfo di = new DirectoryInfo(strPath);
                    foreach (FileInfo fi in di.GetFiles())
                    {
                        fi.Delete();
                    }
                }
            }
            catch { }
        }

        private static IPAddress getLocalIpAddress()
        {
            try
            {
                // get local IP addresses
                IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
                if (localIPs != null)
                {
                    foreach (IPAddress ip in localIPs)
                    {
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            return ip;
                    }
                }


            }
            catch { }
            return null;
        }

        protected string getPlaylistUrl()
        {
            IPAddress ip = getLocalIpAddress();
            if (ip != null)
            {
                StringBuilder sb = new StringBuilder(128);
                sb.Append("http://");
                sb.Append(ip);
                sb.Append(':');
                sb.Append(Database.dbSettings.Instance.HttpServerPort);
                sb.Append("/cdn/stream/");
                sb.Append(this._Identifier);
                sb.Append('/');
                sb.Append(HTTP_FILENAME_M3U_LIST);

                return sb.ToString();
            }

            return null;
        }

        protected string getPlaylistUrlRelative()
        {
            IPAddress ip = getLocalIpAddress();
            if (ip != null)
            {
                StringBuilder sb = new StringBuilder(128);
                sb.Append("/cdn/stream/");
                sb.Append(this._Identifier);
                sb.Append('/');
                sb.Append(HTTP_FILENAME_M3U_LIST);

                return sb.ToString();
            }

            return null;
        }

        protected void appendServer(StringBuilder sb)
        {
            IPAddress ip = getLocalIpAddress();
            if (ip != null)
            {
                sb.Append("http://");
                sb.Append(ip);
                sb.Append(':');
                sb.Append(Database.dbSettings.Instance.HttpServerPort);
            }
        }

        protected NLog.Logger Logger
        {
            get
            {
                if (this._Logger == null)
                {
                    Type t = this.GetType();
                    this._Logger = LogManager.GetLogger(t.FullName);
                    //this._Logger = this._Logger.WithProperty("ID", t.Name);
                    //Log.AddRule(t.FullName);
                }
                return this._Logger;
            }
        }
    }
}
