using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using MediaPortal.Pbk.Logging;
using NLog;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class TaskSegmentCDN : TaskSegment, IJob
    {
        private class InterStream : Stream
        {
            private TaskSegmentCDN _Task;
            private long _Position = 0;

            public InterStream(TaskSegmentCDN task)
            {
                this._Task = task;
             }

            public override bool CanRead
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanSeek
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override long Length
            {
                get { throw new NotImplementedException(); }
            }

            public override long Position
            {
                get
                {
                    return this._Position;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                lock (this._Task.LockDataAvailable)
                {
                    while (true)
                    {
                        int iAvail = (int)(this._Task._BufferWriten - this._Position);
                        if (iAvail == 0)
                        {
                            if (this._Task.Status == TaskStatusEnum.Failed || this._Task.BytesProcessed == this._Task.Length)
                                return 0; //end

                            Monitor.Wait(this._Task.LockDataAvailable, 5000);
                        }
                        else
                        {
                            int iBufferIdx = (int)(this._Position / _BUFFER_SEGMENT_SIZE);
                            int iBufferOffset = (int)(this._Position % _BUFFER_SEGMENT_SIZE);


                            int iToRd = Math.Min(_BUFFER_SEGMENT_SIZE - iBufferOffset, Math.Min(iAvail, count));

                            Buffer.BlockCopy(this._Task._Buffers[iBufferIdx], iBufferOffset, buffer, offset, iToRd);

                            this._Position += iToRd;

                            return iToRd;
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
                throw new NotImplementedException();
            }

            public override void Close()
            {
                if (this._Task != null)
                {
                    lock (this._Task.LockDataAvailable)
                    {
                        this._Task._BufferTokensInUse--;
                        this._Task.destroyBuffer();
                    }

                    this._Task = null;
                }
            }
        }

        private const int _BUFFER_SEGMENT_SIZE = 1024 * 8;

        private int _BufferTokensInUse = 0;
        private long _BufferSize = 0;
        private long _BufferWriten = 0;
        private List<byte[]> _Buffers = null;

        public long Length = 0;
        public long BytesProcessed = 0;
        public System.Threading.ManualResetEvent Downloaded = new System.Threading.ManualResetEvent(false);
        public object LockDataAvailable = new object();
        public ManualResetEvent FlagProcessing = new ManualResetEvent(false);
        public volatile bool IsContentLengthKnown = false;

        public TaskSegmentTypeEnum TaskSegmentType = TaskSegmentTypeEnum.File;
        public string MP4_DecryptingKey = null;
        public string MP4_InitFilePath = null;
        public HlsDecryptor HLS_Decryptor = null;

        public Pbk.Net.Http.HttpUserWebRequest DownloadTask;

        public TaskSegmentCDN(Task task)
            : base(task)
        { }

        #region IJob

        public string JobTitle
        {
            get
            {
                return this.Filename;
            }
        }

        public JobStatus JobStatus
        {
            get
            {
                return this._JobStatus;
            }
            set
            {
                this._JobStatus = value;
            }
        }private JobStatus _JobStatus = JobStatus.Iddle;

        public ManualResetEvent JobFlagDone
        {
            get { return this._FlagDone; }
        }private ManualResetEvent _FlagDone = new ManualResetEvent(false);

        public JobStatus DoJob(ref JobResources resources)
        {
            //Init downloader if null
            if (resources == null)
            {
                resources = new JobResurcesDownload()
                {
                    Buffer = new byte[1024 * 8],
                    WebRequest = new Pbk.Net.Http.HttpUserWebRequest() 
                    {
                        AllowSystemProxy = Database.dbSettings.Instance.AllowSystemProxy ? Pbk.Utils.OptionEnum.Yes : Pbk.Utils.OptionEnum.No,
                        UseOpenSSL = Database.dbSettings.Instance.UseOpenSsl ? Pbk.Utils.OptionEnum.Yes : Pbk.Utils.OptionEnum.No
                    }
                };
            }

            return (this._JobStatus = this.doDownload((JobResurcesDownload)resources));
        }

        public void JobAbort()
        {
            this._Abort = true;
        }

        public int JobSlotsMax
        {
            get { return Database.dbSettings.Instance.MediaServerMaxSimultaneousDownloadsPerTask; }
        }

        public int JobSlotsInUse
        {
            get
            {
                return this._Task.RunningJobs;
            }
            set
            {
                this._Task.RunningJobs = value;
            }
        }

        public EventHandler JobEvent
        { get; set; }

        public object Parent
        { get { return this._Task; } }

        #endregion

        public Stream GetStream()
        {
            lock (this.LockDataAvailable)
            {
                if (this._Buffers == null)
                    return new FileStream(this.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                else
                {
                    this._BufferTokensInUse++;
                    return new InterStream(this);
                }
            }
        }

        protected void onSegmentStatusChanged(TaskStatusEnum status)
        {
            {
                this.Status = status;
                this._Task.OnEvent(this.ArgsStateChanged);
            }
        }

        private void cbDownloadReport(object sender, EventArgs e)
        {
            Pbk.Net.Http.HttpUserWebRequestEventArgs args = (Pbk.Net.Http.HttpUserWebRequestEventArgs)e;

            if (args.Type == Pbk.Net.Http.HttpUserWebRequestEventType.DownloadProgressUpdate)
            {
                TaskSegment task = (TaskSegment)((Pbk.Net.Http.HttpUserWebRequest)sender).Tag;
                this._Task.OnEvent(task.ArgsUpdate);
            }

        }

        private void initBuffer()
        {
            lock (this.LockDataAvailable)
            {
                if (this._Buffers == null)
                {
                    this._Buffers = new List<byte[]>();
                    this._Buffers.Add(new byte[_BUFFER_SEGMENT_SIZE]);
                    this._BufferSize = _BUFFER_SEGMENT_SIZE;
                    this._BufferWriten = 0;
                }
            }
        }

        private void destroyBuffer()
        {
            lock (this.LockDataAvailable)
            {
                if (this._BufferTokensInUse <= 0 && this._Buffers != null 
                    && this.BytesProcessed == this.Length && this.DownloadTask == null)
                {
                    this._Buffers = null;
                    this._BufferTokensInUse = 0;
                    this._BufferSize = 0;
                    this._BufferWriten = 0;
                }
            }
        }

        private void writeToBuffer(byte[] data, int iOffset, int iLength)
        {
            int iBufferIdx = (int)(this._BufferWriten / _BUFFER_SEGMENT_SIZE);
            int iBufferOffset = (int)(this._BufferWriten % _BUFFER_SEGMENT_SIZE);

            while (iLength > 0)
            {
                int iToWr = Math.Min(_BUFFER_SEGMENT_SIZE - iBufferOffset, iLength);
                Buffer.BlockCopy(data, iOffset, this._Buffers[iBufferIdx], iBufferOffset, iToWr);
                iLength -= iToWr;
                iOffset += iToWr;
                this._BufferWriten += iToWr;
                iBufferOffset += iToWr;

                if (iBufferOffset == _BUFFER_SEGMENT_SIZE)
                {
                    this._Buffers.Add(new byte[_BUFFER_SEGMENT_SIZE]);
                    this._BufferSize += _BUFFER_SEGMENT_SIZE;
                    iBufferIdx++;
                    iBufferOffset = 0;
                }
            }
        }

        private JobStatus doDownload(JobResurcesDownload resources)
        {
            lock (this) //lock to segment
            {
                try
                {
                    if (this.Status != TaskStatusEnum.InQueue)
                    {
                        _Logger.Warn("[doDownload][{0}] Not In Queue. [{1}]", this.FullPath, this.Status);
                        return JobStatus.Complete;
                    }

                    while (this.Attempts-- > 0)
                    {
                        string strUrl = this.Url;
                        if (strUrl != null)
                        {
                            //Initiate download
                            using (Pbk.Net.Http.HttpUserWebRequest wr = resources.WebRequest)
                            {
                                wr.Url = strUrl;
                                this.DownloadTask = wr;

                                const int _TIMEOUT = 5000;

                                wr.Event = this.cbDownloadReport;
                                wr.Tag = this;
                                wr.AllowSystemProxy = Database.dbSettings.Instance.AllowSystemProxy ? Pbk.Utils.OptionEnum.Yes : Pbk.Utils.OptionEnum.No;
                                wr.UseOpenSSL = Database.dbSettings.Instance.UseOpenSsl ? Pbk.Utils.OptionEnum.Yes : Pbk.Utils.OptionEnum.No;
                                wr.ConnectTimeout = _TIMEOUT;
                                wr.ResponseTimeout = _TIMEOUT;

                                //Resume
                                bool bResumeRequest = false;
                                if (this.BytesProcessed > 0)
                                {
                                    //Resume request
                                    wr.SetHttpRequestFieldRange(this.BytesProcessed);
                                    bResumeRequest = true;
                                }

                                this.onSegmentStatusChanged(TaskStatusEnum.Downloading);


                                //Create directory if needed
                                string strDir = Path.GetDirectoryName(this.FullPath);
                                if (!Directory.Exists(strDir))
                                    Directory.CreateDirectory(strDir);


                                //Start download
                                Stream stream = wr.GetResponseStream();
                                if (stream != null)
                                {
                                    try
                                    {
                                        //Open the file
                                        using (FileStream fs = new FileStream(this.FullPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                                        {
                                            if (this.HLS_Decryptor != null)
                                                stream = new CryptoStream(stream, this.HLS_Decryptor.GetCryptoTranform(this.ID), CryptoStreamMode.Read);
                                            else if (wr.ContentLength > 0)
                                            {
                                                if (this.Length != wr.ContentLength)
                                                {
                                                    //fs.SetLength(wr.ContentLength); //don't do this: other simultanious filestream might read zero data

                                                    //Set segment length
                                                    this.Length = wr.ContentLength;

                                                    this.IsContentLengthKnown = true;
                                                }
                                            }

                                            //Init buffer
                                            //this.initBuffer();

                                            //We are downloading
                                            this.FlagProcessing.Set();

                                            //Resume test
                                            if (bResumeRequest)
                                            {
                                                long lFrom;
                                                if (wr.TryGetHttpResponseFieldContentRange(out lFrom) && this.BytesProcessed == lFrom)
                                                {
                                                    fs.Position = this.BytesProcessed;
                                                    _Logger.Debug("[doDownload][{0}] Resuming from: {1}", this.FullPath, this.BytesProcessed);
                                                }
                                                else
                                                {
                                                    _Logger.Error("[doDownload][{0}] Resuming failed", this.FullPath);
                                                    this.ErrorDescription = "Download: Invalid resume";
                                                    goto failed;
                                                }
                                            }
                                            else
                                                this.BytesProcessed = 0;


                                            DateTime ts = DateTime.MinValue;
                                            byte[] buffer = resources.Buffer;
                                            int iRd;

                                            #region Read loop
                                            while (!this._Abort)
                                            {
                                                //Begin async receive

                                                iRd = stream.Read(buffer, 0, buffer.Length);
                                                if (iRd > 0)
                                                {
                                                    //Save to file
                                                    fs.Write(buffer, 0, iRd);

                                                    lock (this.LockDataAvailable)
                                                    {
                                                        //Write to buffer
                                                        //this.writeToBuffer(buffer, 0, iRd);

                                                        fs.Flush(true);

                                                        //Update processed bytes counter
                                                        this.BytesProcessed = fs.Position;

                                                        //Trigger waiting threads
                                                        Monitor.PulseAll(this.LockDataAvailable);
                                                    }

                                                    //Check end
                                                    if (this.IsContentLengthKnown && wr.ResponseBytesReceived == wr.ContentLength)
                                                    {
                                                        fs.Close();
                                                        return this.doPostProcess(strUrl);
                                                    }

                                                    //Report
                                                    if ((DateTime.Now - ts).TotalMilliseconds >= 1000)
                                                    {
                                                        this._Task.OnEvent(this.ArgsUpdate);
                                                        ts = DateTime.Now;
                                                    }
                                                }
                                                else if (!this.IsContentLengthKnown)
                                                {
                                                    lock (this.LockDataAvailable)
                                                    {
                                                        fs.Flush(true);
                                                        this.Length = fs.Length;
                                                        fs.Close();

                                                        //Update processed bytes counter
                                                        this.BytesProcessed = this.Length;

                                                        //Trigger waiting threads
                                                        Monitor.PulseAll(this.LockDataAvailable);
                                                    }
                                                    
                                                    return this.doPostProcess(strUrl);
                                                }
                                                else
                                                {
                                                    this.ErrorDescription = "Download: connection closed";
                                                    break;
                                                }
                                            }

                                            if (this._Abort)
                                            {
                                                this.onSegmentStatusChanged(TaskStatusEnum.Aborted);
                                                return JobStatus.Abort;
                                            }
                                            #endregion
                                        }
                                    }
                                    finally
                                    {
                                        stream.Close();
                                    }
                                }
                                else
                                    this.ErrorDescription = "Download: Connection timeout";
                            }
                        }
                        else
                        {
                            _Logger.Error("[doDownload][{0}] Invalide url: \"{1}\"", this.FullPath, strUrl);
                            this.ErrorDescription = "Download: invalid url: " + strUrl + '\"';
                        }

                    failed:
                        this.onSegmentStatusChanged(this.Attempts > 0 ? TaskStatusEnum.Error : TaskStatusEnum.Failed);

                        Thread.Sleep(500);
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Error("[doDownload] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    this.ErrorDescription = "Download: " + ex.Message;
                    this.onSegmentStatusChanged(TaskStatusEnum.Failed);
                }
                finally
                {
                    this.DownloadTask = null;
                    this.destroyBuffer();
                }

                return JobStatus.Failed;

            }//release segment lock
        }

        private JobStatus doPostProcess(string strUrl)
        {
            switch (this.TaskSegmentType)
            {
                default:
                    _Logger.Debug("[doPostProcess][{0}] Complete. URL: \"{1}\"  Size: {2}", this.FullPath, strUrl, this.Length);
                    break;

                case TaskSegmentTypeEnum.MP4EncryptedInit:
                case TaskSegmentTypeEnum.MP4EncryptedMedia:

                    string strFileSrc = this.FullPath + ".tmp";
                    string strFileDst = this.FullPath;
                    string strArgs;
                    
                    if (this.TaskSegmentType == TaskSegmentTypeEnum.MP4EncryptedInit)
                    {
                        //We need to backup encrypted init file for decrypting individual m4s fragments
                        if (!File.Exists(this.MP4_InitFilePath))
                            File.Copy(this.FullPath, this.MP4_InitFilePath);

                        strArgs = string.Format(" --key {0} \"{1}\" \"{2}\"", this.MP4_DecryptingKey, strFileSrc, strFileDst);
                    }
                    else
                    {
                        //For m4s decryption we need encrypted init file
                        if (!File.Exists(this.MP4_InitFilePath))
                        {
                            _Logger.Error("[doPostProcess][decrypt][{0}] Init file doesn't exist.", this.FullPath);
                            return MediaServer.JobStatus.Failed;
                        }

                        strArgs = string.Format(" --key {0} --fragments-info \"{1}\" \"{2}\" \"{3}\"", this.MP4_DecryptingKey, this.MP4_InitFilePath, strFileSrc, strFileDst);
                    }

                    ///Rename encrypted file to tmp
                    File.Move(this.FullPath, strFileSrc);

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.Arguments = strArgs;
                    startInfo.FileName = "\"Widevine\\mp4decrypt.exe\"";
                    startInfo.UseShellExecute = false;
                    startInfo.ErrorDialog = false;
                    startInfo.RedirectStandardError = true;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.CreateNoWindow = true;
                    Process pr = new Process();
                    pr.StartInfo = startInfo;
                    //test.Exited += new EventHandler(restream_Exited);
                    pr.OutputDataReceived += this.outputHandler;
                    pr.ErrorDataReceived += this.errorHandler;

                    //Start
                    pr.Start();
                    pr.BeginOutputReadLine();
                    pr.BeginErrorReadLine();

                    //Wait for finish
                    pr.WaitForExit();

                    pr.CancelOutputRead();
                    pr.CancelErrorRead();

                    if (!File.Exists(this.FullPath))
                    {
                        _Logger.Error("[doPostProcess][decrypt][{0}] Decrypting failed: result file doesn't exist.", this.FullPath);
                        return MediaServer.JobStatus.Failed;
                    }

                    FileInfo fi = new FileInfo(this.FullPath);
                    if (fi.Length == 0)
                    {
                        _Logger.Error("[doPostProcess][decrypt][{0}] Decrypting failed: result file length is zero.", this.FullPath);
                        return MediaServer.JobStatus.Failed;
                    }

                    lock (this.LockDataAvailable)
                    {
                        //Set new size
                        this.Length = fi.Length;
                        this.BytesProcessed = fi.Length;
                        this.IsContentLengthKnown = true;
                    }

                    //Rise init complete flag
                    if (this.TaskSegmentType == TaskSegmentTypeEnum.MP4EncryptedInit && this.Tag is ContentProtection)
                        ((ContentProtection)this.Tag).FlagInitComplete.Set();

                    _Logger.Debug("[doPostProcess][decrypt][{0}] Decrypting complete. Size: {1}", this.FullPath, this.Length);

                    //Delete encrypted file
                    File.Delete(strFileSrc);

                    break;
            }
            
            this.ErrorDescription = string.Empty;
            this.onSegmentStatusChanged(TaskStatusEnum.Available);
            
            return JobStatus.Complete;
        }

        private void outputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _Logger.Debug("[mp4decrypt][output] " + e.Data);
        }

        private void errorHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _Logger.Debug("[mp4decrypt][error] " + e.Data);
        }


        public StringBuilder SerializeJson(StringBuilder sb)
        {
            sb.Append('{');
            sb.Append("\"type\":\"TaskSegmentCDN\",");
            sb.Append("\"parentId\":\"");
            sb.Append(this._Task.Identifier);
            sb.Append("\",\"id\":\"");
            sb.Append(this.UID);
            sb.Append("\",\"status\":\"");
            sb.Append(this.Status);
            sb.Append("\",\"path\":\"");
            Tools.Json.AppendAndValidate(this.FullPath, sb);
            sb.Append("\",\"url\":\"");
            sb.Append(this.Url);
            sb.Append("\",\"isInHls\":\"");
            sb.Append(this.IsInCurrentHlsList);
            sb.Append("\"}");

            return sb;
        }
    }
}
