#define USE_MP4LIB

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
        public ManualResetEvent Downloaded = new ManualResetEvent(false);
        public object LockDataAvailable = new object();
        public ManualResetEvent FlagProcessing = new ManualResetEvent(false);
        public volatile bool IsContentLengthKnown = false;

        public TaskSegmentTypeEnum TaskSegmentType = TaskSegmentTypeEnum.File;
        public ContentProtection ContentProtection = null;
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
                                wr.HttpArguments = this.HttpArguments;

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

                    #region MP4 decryption
                    string strFileSrc = this.FullPath + ".tmp";
                    string strFileDst = this.FullPath;
                    string strArgs = null;
                    string strInitPath = this.ContentProtection.InitFileFullPath;
                    string strKID = this.ContentProtection.KID;
                    string strPSSH = this.ContentProtection.PSSH;
                    string strKey = this.ContentProtection.DecryptionKey;

                    byte[] kid, key;

                    if (this.TaskSegmentType == TaskSegmentTypeEnum.MP4EncryptedInit)
                    {
                        //We need to backup init file for decrypting individual m4s fragments
                        if (!File.Exists(strInitPath))
                            File.Copy(this.FullPath, strInitPath);

                        if (strKID == null)
                        {
                            kid = mp4GetDefaultKID(File.ReadAllBytes(this.FullPath));
                            if (kid != null)
                            {
                                strKID = Pbk.Utils.Tools.PrintDataToHex(kid, false, "x2");
                                if (!kid.All(b => b == 0))
                                {
                                    //Non zero KID
                                    this.ContentProtection.KID = strKID;
                                    _Logger.Debug("[doPostProcess][{0}] MP4 Init - default KID: {1}", this.FullPath, this.ContentProtection.KID);
                                }
                            }
                            else
                            {
                                _Logger.Error("[doPostProcess][decrypt][{0}] MP4 Init - KID not found.", this.FullPath);
                                return JobStatus.Failed;
                            }
                        }
#if USE_MP4LIB
                        else
                            kid = Pbk.Utils.Tools.ParseByteArrayFromHex(strKID);

                        key = new byte[16];
#else
                        //Remove encryption from init file
                        strArgs = string.Format(" --key 1:00000000000000000000000000000000 \"{1}\" \"{2}\"", strKID, strFileSrc, strFileDst);
#endif
                    }
                    else
                    {
                        //For m4s decryption we need init file
                        if (!File.Exists(strInitPath))
                        {
                            _Logger.Error("[doPostProcess][decrypt][{0}] Init file doesn't exist.", this.FullPath);
                            return JobStatus.Failed;
                        }

                        if (strKey == null)
                        {
                            //Key not specified; get PSSH & KID from m4s segment file
                            using (FileStream fs = new FileStream(this.FullPath, FileMode.Open))
                            {
                                if (mp4TryGetEncryptionData(fs, strPSSH == null, strKID == null, out byte[] pssh, out kid))
                                {
                                    if (strPSSH == null)
                                        strPSSH = Convert.ToBase64String(pssh);

                                    if (strKID == null)
                                        strKID = Pbk.Utils.Tools.PrintDataToHex(kid, false, "x2");

                                    strKey = Widevine.GetKey(strPSSH, strKID,
                                        this.ContentProtection.LicenceServer, this.ContentProtection.HttpArguments);
                                    if (strKey == null)
                                    {
                                        _Logger.Error("[doPostProcess][decrypt][{0}] Failed to get decryption Widevine key.", this.FullPath);
                                        return JobStatus.Failed;
                                    }

                                    _Logger.Debug("[doPostProcess][decrypt][{0}] Mp4Decrypt Key: {1}:{2}", this.FullPath, strKID, strKey);
                                }
                                else
                                {
                                    _Logger.Error("[doPostProcess][decrypt][{0}] Failed to get Widevine data from M4S file.", this.FullPath);
                                    return JobStatus.Failed;
                                }
                            }
                        }
#if USE_MP4LIB
                        kid = Pbk.Utils.Tools.ParseByteArrayFromHex(strKID);
                        key = Pbk.Utils.Tools.ParseByteArrayFromHex(strKey);
#else
                        strArgs = string.Format(" --key 1:{1} --fragments-info \"{2}\" \"{3}\" \"{4}\"", strKID, strKey, strInitPath, strFileSrc, strFileDst);
#endif
                    }

                    //Rename encrypted file to tmp
                    File.Move(this.FullPath, strFileSrc);

                    #region Mp4Decrypt process

#if USE_MP4LIB
                    MP4LibNative.MP4_API_RESULT res = MP4LibNative.Decrypt(getNullTerminatedUtf8(strFileSrc), getNullTerminatedUtf8(strFileDst),
                        this.TaskSegmentType != TaskSegmentTypeEnum.MP4EncryptedInit ? getNullTerminatedUtf8(strInitPath) : null, kid, key, 1);

                    if (res != MP4LibNative.MP4_API_RESULT.AP4_SUCCESS)
                    {
                        _Logger.Error("[doPostProcess][decrypt][{0}] Failed to decrypt file: {1}", this.FullPath, res);
                        return JobStatus.Failed;
                    }
#else
                    ProcessStartInfo psi = new ProcessStartInfo()
                    {
                        Arguments = strArgs,
                        FileName = "\"Widevine\\mp4decrypt.exe\"",
                        UseShellExecute = false,
                        ErrorDialog = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    Process pr = new Process
                    {
                        StartInfo = psi
                    };
                    //pr.Exited += new EventHandler(restream_Exited);
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
#endif
#endregion

                    if (!File.Exists(this.FullPath))
                    {
                        _Logger.Error("[doPostProcess][decrypt][{0}] Decrypting failed: result file doesn't exist.", this.FullPath);
                        return JobStatus.Failed;
                    }

                    FileInfo fi = new FileInfo(this.FullPath);
                    if (fi.Length == 0)
                    {
                        _Logger.Error("[doPostProcess][decrypt][{0}] Decrypting failed: result file length is zero.", this.FullPath);
                        return JobStatus.Failed;
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
                        this.ContentProtection.FlagInitComplete.Set();

                    _Logger.Debug("[doPostProcess][decrypt][{0}] Decrypting complete. Size: {1}", this.FullPath, this.Length);

                    //Delete encrypted file
                    File.Delete(strFileSrc);
#endregion

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

        private static bool mp4TryGetEncryptionData(Stream stream, bool bPssh, bool bKID, out byte[] pssh, out byte[] kid)
        {
            pssh = null; //path: moof/pssh
            kid = null;  //path: moof/traf/sgpd/seig

            if (!bPssh && !bKID)
                return false;

            //box structure:
            // int32 size
            // int32 code
            //  int8 data[size - 8]

            const int HEADER_SIZE = 8;

            while (stream.Position < stream.Length)
            {
                int iSize = mp4ReadInt32(stream) - HEADER_SIZE;
                string strCode = mp4ReadCode(stream);
                long lEndBox1 = stream.Position + iSize;
                if (strCode == "moof")
                {
                    while (stream.Position < lEndBox1)
                    {
                        iSize = mp4ReadInt32(stream) - HEADER_SIZE;
                        strCode = mp4ReadCode(stream);
                        long lEndBox2 = stream.Position + iSize;
                        switch (strCode)
                        {
                            case "pssh":
                                stream.Position -= HEADER_SIZE;
                                pssh = new byte[iSize + HEADER_SIZE];
                                stream.Read(pssh, 0, pssh.Length);

                                if (!bKID || kid != null)
                                    return true;

                                break;

                            case "traf":
                                while (stream.Position < lEndBox2)
                                {
                                    iSize = mp4ReadInt32(stream) - HEADER_SIZE;
                                    strCode = mp4ReadCode(stream);
                                    long lEndBox3 = stream.Position + iSize;
                                    if (strCode == "sgpd")
                                    {
                                        stream.Position += 4; //version & flag
                                        if (mp4ReadCode(stream) == "seig")
                                        {
                                            int iLength = mp4ReadInt32(stream); //length
                                            stream.Position += 4; //count
                                            stream.Position += 4; //0,0,flag,iv_size
                                            kid = new byte[16];
                                            stream.Read(kid, 0, kid.Length);
                                            if (!bPssh || pssh != null)
                                                return true;

                                            break;
                                        }
                                    }
                                    //next box
                                    stream.Position = lEndBox3;
                                }
                                break;
                        }
                        //next box
                        stream.Position = lEndBox2;
                    }
                    //end of moof
                    break;
                }
                //next box
                stream.Position = lEndBox1;
            }
            //end of stream
            return false;
        }

        private static byte[] mp4GetDefaultKID(byte[] mp4InitData)
        {
            //Pattern to search; this is faster than parse every box

            int iIdx = 4;
            while (iIdx < mp4InitData.Length)
            {
                if (mp4InitData[iIdx] == 't' && mp4InitData[iIdx + 1] == 'e' && mp4InitData[iIdx + 2] == 'n' && mp4InitData[iIdx + 3] == 'c'
                    && mp4InitData[iIdx - 4] == 0 && mp4InitData[iIdx - 3] == 0 && mp4InitData[iIdx - 2] == 0 && mp4InitData[iIdx - 1] >= 32)
                {
                    //Match
                    iIdx += 12; //offset to KID
                    break;
                }
                iIdx++;
            }

            if (iIdx + 16 <= mp4InitData.Length)
            {
                byte[] kid = new byte[16];
                Buffer.BlockCopy(mp4InitData, iIdx, kid, 0, 16);

                //Check for non empty KID
                //if (!kid.All(b => b == 0))
                return kid;
            }

            return null;
        }
        private static string mp4ReadCode(Stream stream)
        {
            char[] code = new char[4];
            int i = 0;
            while(i < code.Length)
             code[i++] = (char)stream.ReadByte();
            return new String(code);
        }

        private static byte[] getNullTerminatedUtf8(string strText)
        {
            byte[] t = Encoding.UTF8.GetBytes(strText);
            byte[] utf8 = new byte[t.Length + 1];
            Buffer.BlockCopy(t, 0, utf8, 0, t.Length);
            return utf8;
        }

        private static int mp4ReadInt32(Stream stream)
        {
            return (stream.ReadByte() << 24) | (stream.ReadByte() << 16) | (stream.ReadByte() << 8) | (stream.ReadByte());
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
            sb.Append(this.IsInCurrentList);
            sb.Append("\"}");

            return sb;
        }
    }
}
