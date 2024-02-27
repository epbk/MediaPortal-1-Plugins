//#define _WIDEVINE_TEST

using System;
using System.Globalization;
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
using MediaPortal.Pbk.Extensions;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;
using System.Security.Cryptography;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    //[DBTableAttribute("playerTasks")]
    public class TaskCDN : Task
    {
        private const int BUFFER_DECRYPTORS_LIMIT = 5;

        private enum VideoTypeEnum
        {
            Unknown = 0,
            HLS,
            MPD
        }

        #region Database Fields
        #endregion

        #region Private fields
        
        private VideoTypeEnum _VideoType = VideoTypeEnum.Unknown;
        private StringBuilder _SbMasterList = new StringBuilder(1024);
        private volatile byte[] _MasterListRaw = null;
        DateTime _MasterListRefreshTs = DateTime.MinValue;

        private System.Timers.Timer _TimerRefresh = null;
        private System.Timers.Timer _TimerTerminate = null;

        private bool _QueueStarted = false;

        private static System.Globalization.CultureInfo _Culture_EN = new System.Globalization.CultureInfo("en-US");

        private int _HttpRequestCounter = 0;

        private ManualResetEvent _FlagHttpRequestExit = new ManualResetEvent(false);


        private Pbk.Net.Http.HttpUserWebRequest _Request;
        private int _FileIdCounter = 0;
        private static CultureInfo _CiEn = CultureInfo.GetCultureInfo("en-US");

        private string _UrlFinal;

        private List<ContentProtection> _ContentProtection = null;
        private List<Database.dbContentProtectionKey> _WidevineKeys = null;

        private List<HlsDecryptor> _DecryptorBuffer = new List<HlsDecryptor>();
        private object _DecryptorPadlock = new object();

        #endregion

        public static JobHandler JobHandler = new JobHandler();


        [Browsable(false)]
        [Category("Task")]
        public bool Autoterminate
        { get; set; }

        [Browsable(false)]
        public bool Persistent
        {
            get { return this._Persistent; }
        }private bool _Persistent = false;

        [Browsable(false)]
        public override string WorkFolder
        {
            get
            {
                if (this._WorkFolder == null)
                    this._WorkFolder = Database.dbSettings.Instance.WorkPath + "CDN\\" + this._Identifier + '\\';

                return this._WorkFolder;
            }
        }private string _WorkFolder = null;

        [Browsable(false)]
        public override string Prefix
        {
            get { return "Player"; }
        }

        /// <summary>
        /// Start task
        /// </summary>
        /// <returns>True if sucessfully started</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public override bool Start()
        {
            if (this._TimerRefresh == null)
            {
                this.Logger.Debug("[Start] " + this.Title);

                this._Persistent = false;
                this._MasterListRaw = null;
                //this._FlagQueueFlushed.Reset();
                this._Stopping = 0;
                this.Status = TaskStatusEnum.Starting;

                this._TimerRefresh = new System.Timers.Timer();
                this._TimerRefresh.Interval = 5000;
                this._TimerRefresh.AutoReset = true;
                this._TimerRefresh.Elapsed += new System.Timers.ElapsedEventHandler(this.cbRefreshTmerElapsed);
                this._TimerRefresh.Enabled = true;

                this._MasterListRefreshTs = DateTime.MinValue;

                this.refreshMasterList();

                if (this.Autoterminate)
                {
                    this._TimerTerminate = new System.Timers.Timer();
                    this._TimerTerminate.Interval = Database.dbSettings.Instance.MediaServerAutoterminatePeriod * 1000;
                    this._TimerTerminate.AutoReset = false;
                    this._TimerTerminate.Elapsed += new System.Timers.ElapsedEventHandler(this.cbTerminateTimerElapsed);
                    this._TimerTerminate.Enabled = true;
                }


                if (!Directory.Exists(this.WorkFolder))
                    Directory.CreateDirectory(this.WorkFolder);

                this.Status = TaskStatusEnum.Running;

                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Stop running task
        /// </summary>
        /// <returns>True if succesfully stopped</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public override bool Stop()
        {
            if (this._TimerRefresh != null)
            {
                this.Logger.Debug("[Stop] " + this.Title);

                this._Starting = 0;

                this.Abort = true;

                //Status change
                this.Status = TaskStatusEnum.Stopping;

                //Wait until all http requests are handled
                this._FlagHttpRequestExit.Reset();
                while (this._HttpRequestCounter > 0)
                {
                    //Some hhtp request being processed
                    this._FlagHttpRequestExit.WaitOne(); //wait for http request exit
                    this._FlagHttpRequestExit.Reset();
                }

                this._MasterListRaw = null;

                this.terminate();

                //Remove all download tasks from queue
                JobHandler.RemoveAllFromQueue(p => p != null && p.Parent == this);

                //Terminate all ongoing download tasks
                JobHandler.AbortAll(p => p != null && p.Parent == this);
                JobHandler.WaitForAll(p => p != null && p.Parent == this, -1);

                //Clean work directory
                this.cleanUpWorkDirectory();

                //Status change
                this.Status = TaskStatusEnum.Iddle;

                return true;
            }
            else
                return false;
        }

        public string LeadingUriPath
        {
            get
            {
                if (this._LeadingUriPath == null)
                    this._LeadingUriPath = "/cdn/stream/" + this._Identifier + '/';

                return this._LeadingUriPath;
            }
        }private string _LeadingUriPath = null;


        /// <summary>
        /// Handle http request
        /// </summary>
        /// <param name="args">Http request arguments</param>
        /// <param name="strFile">File to access</param>
        /// <returns>True if sucessfully handled</returns>
        public bool HandleHttpRequest(Pbk.Net.Http.HttpUserServerEventArgs args, Uri uri)
        {
            try
            {
                Interlocked.Increment(ref this._HttpRequestCounter);

                if (this.Status != TaskStatusEnum.Running)
                    return false;

                //Reset autoterminate timer
                System.Timers.Timer timer = this._TimerTerminate;
                if (timer != null)
                    timer.Interval = Database.dbSettings.Instance.MediaServerAutoterminatePeriod * 1000;

                //Process http request
                if (!uri.LocalPath.StartsWith(this.LeadingUriPath))
                    return false;

                string strFile;
                string strUrlPath = null;
                if (uri.Segments[4] == "files/") //directory/file structures
                {
                    strUrlPath = uri.LocalPath.Substring(this.LeadingUriPath.Length + 5);
                    strFile = this.WorkFolder + uri.LocalPath.Substring(this.LeadingUriPath.Length).Replace('/', '\\');
                }
                else
                    strFile = uri.Segments[uri.Segments.Length - 1];

                switch (strFile)
                {
                    case HTTP_FILENAME_M3U_LIST:
                        if (this._MasterListRaw != null)
                        {
                            //Refresh master list if needed
                            this.refreshMasterList();

                            args.ResponseCode = System.Net.HttpStatusCode.OK;
                            args.ResponseHeaderFields = new Dictionary<string, string>();
                            args.ResponseHeaderFields.Add(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_CONTENT_TYPE, Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_VND_APPLE_MPEGURL);
                            args.ResponseData = this._MasterListRaw;
                            args.Handled = true;
                        }
                        break;

                    default:
                        //Try find segment of the same filename
                        TaskSegmentCDN segment;
                        bool bLocked = false;
                        try
                        {
                            //Segments lock
                            Monitor.Enter(this._Segments, ref bLocked);

                            //Try find requesting segment
                            segment = (TaskSegmentCDN)this._Segments.Find(p => p.Filename == strFile || p.FullPath == strFile);

                            bool bIsInit = false;
                            ContentProtection cp = null;

                            if (segment == null && strUrlPath != null)
                            {
                                //Dynamic files

                                if (this._ContentProtection != null)
                                {
                                    #region DRM
                                    //DRM active: get the matching protection based on url
                                    for (int i = 0; i < this._ContentProtection.Count; i++)
                                    {
                                        if (this._ContentProtection[i].IsMatch(uri.LocalPath, out bIsInit))
                                        {
                                            cp = this._ContentProtection[i];
                                            break;
                                        }
                                    }

                                    if (cp == null)
                                    {
                                        this.Logger.Error("[{0}][HandleHttpRequest] ContentProtection not found.", this._Identifier);
                                        return false;
                                    }
                                    else
                                    {
                                        if (!bIsInit && !File.Exists(cp.InitFileFullPath))
                                        {
                                            if (!cp.FlagInitComplete.WaitOne(10000))
                                            {
                                                this.Logger.Error("[{0}][HandleHttpRequest] MP4 init file not ready yet.", this._Identifier);
                                                return false;
                                            }
                                        }
                                    }
                                    #endregion
                                }

                                segment = new TaskSegmentCDN(this)
                                {
                                    Index = this._FileIdCounter,
                                    Url = this.getFullUrl(strUrlPath),
                                    Filename = Path.GetFileName(strFile),
                                    FullPath = strFile,
                                    LastAccess = DateTime.Now
                                };

                                if (cp != null)
                                {
                                    //DRM data
                                    segment.TaskSegmentType = bIsInit ? TaskSegmentTypeEnum.MP4EncryptedInit : TaskSegmentTypeEnum.MP4EncryptedMedia;
                                    segment.MP4_InitFilePath = cp.InitFileFullPath;
                                    segment.MP4_DecryptingKey = cp.KID + ':' + cp.DecryptionKey;
                                    segment.Tag = cp;
                                }


                                this.addNewSegment(segment);

                                this.Logger.Debug("[{0}][HandleHttpRequest] New segment created: {1}", this._Identifier, segment.FullPath);
                            }

                            if (segment != null)
                            {
                                segment.LastAccess = DateTime.Now;

                                string strContentType = segment.Filename.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? "video/mp4" : Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_OCTET_STREAM;

                                //got it
                                if (segment.Status == TaskStatusEnum.Available || segment.Status == TaskStatusEnum.Done)
                                {
                                    //File already cached
                                    args.ResponseCode = System.Net.HttpStatusCode.OK;
                                    args.ResponseHeaderFields = new Dictionary<string, string>();
                                    args.ResponseHeaderFields.Add(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_CONTENT_TYPE, strContentType);
                                    args.ResponseStream = segment.GetStream();
                                    args.Handled = true;
                                    this.Logger.Debug("[{0}][HandleHttpRequest] File is ready. Delivered: {1}", this._Identifier, segment.FullPath);
                                }
                                else //possible running; make request & wait for data
                                {
                                    this.Logger.Debug("[{0}][HandleHttpRequest] Requesting file: {1}", this._Identifier, segment.FullPath);

                                    //Request
                                    this.segmentRequest(segment);

                                    //We have to release segments lock now
                                    Monitor.Exit(this._Segments);
                                    bLocked = false;

                                    //In case of DRM, we need to wait until file is downloaded & decrypted
                                    if (segment.TaskSegmentType == TaskSegmentTypeEnum.MP4EncryptedInit || segment.TaskSegmentType == TaskSegmentTypeEnum.MP4EncryptedMedia)
                                    {
                                        segment.JobFlagDone.WaitOne();
                                        if (segment.JobStatus != JobStatus.Complete)
                                            return false;
                                    }
                                    

                                    #region Currently processing the download
                                    if (segment.FlagProcessing.WaitOne(10000)) //wait up to 10s for the requested file
                                    {
                                        //File available

                                        byte[] chunk = null;
                                        if (!segment.IsContentLengthKnown)
                                            chunk = new byte[16]; //prepare buffer for chunk size


                                        //Send http response to the client
                                        args.RemoteSocket.Send(createHttpResponse(chunk == null ? segment.Length : -1, args.KeepAlive, strContentType));

                                        //Open the file with shared access
                                        using (Stream fs = segment.GetStream())
                                        {
                                            byte[] buffer = new byte[8192];

                                            while (segment.Length <= 0 || fs.Position < segment.Length)
                                            {
                                                int iMax;
                                                lock (segment.LockDataAvailable)
                                                {
                                                    //Get size of available data
                                                    while ((iMax = (int)Math.Min(buffer.Length - 2, segment.BytesProcessed - fs.Position)) <= 0)
                                                    {
                                                        if (segment.Length > 0 && fs.Position >= segment.Length)
                                                            goto done;

                                                        //No more data so far; wait

                                                        if (!Monitor.Wait(segment.LockDataAvailable, 5000))
                                                        {
                                                            this.Logger.Error("[{0}][HandleHttpRequest] No response to request file within 5s: {1}", this._Identifier, segment.FullPath);
                                                            return false;
                                                        }
                                                    }
                                                }

                                                //Read file
                                                int iRd = fs.Read(buffer, 0, iMax);
                                                if (iRd > 0)
                                                {
                                                    //Send to client the data currently available

                                                    if (chunk != null)
                                                    {
                                                        //Send chunk size
                                                        args.RemoteSocket.Send(chunk, 0, printChunkSize((uint)iRd, chunk), System.Net.Sockets.SocketFlags.None);

                                                        //Append termination to the data
                                                        buffer[iRd++] = (byte)'\r';
                                                        buffer[iRd++] = (byte)'\n';
                                                    }
                                                    
                                                    // send data
                                                    args.RemoteSocket.Send(buffer, 0, iRd, System.Net.Sockets.SocketFlags.None);
                                                }
                                                else
                                                    Thread.Sleep(50);
                                            }

                                        done:
                                            if (chunk != null)
                                                args.RemoteSocket.Send(chunk, 0, printChunkSize(0, chunk), System.Net.Sockets.SocketFlags.None); //null termination chunk

                                            args.ResponseSent = true; //inform http server that we have already handled entire response including content
                                            args.Handled = true;
                                            this.Logger.Debug("[{0}][HandleHttpRequest] Delivered: {1} Length: {2}", this._Identifier, segment.FullPath, fs.Position);
                                            return true;
                                        }
                                    }
                                    else
                                        this.Logger.Error("[{0}][HandleHttpRequest] No response to request file within 10s: {1}", this._Identifier, segment.FullPath);
                                    #endregion
                                }

                            }
                            else
                                this.Logger.Error("[{0}][HandleHttpRequest] File not found: {1}", this._Identifier, strFile);
                        }
                        catch { }
                        finally
                        {
                            //Exit segments lock if locked
                            if (bLocked)
                                Monitor.Exit(this._Segments);
                        }

                        break;
                }
            }
            catch { }
            finally
            {
                Interlocked.Decrement(ref this._HttpRequestCounter);
                this._FlagHttpRequestExit.Set();
            }

            //Result
            return args.Handled;
        }

        #region Private Methods
        private static byte[] createHttpResponse(long lContentLength, bool bKeepAlive, string strContentType = Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_APPLICATION_OCTET_STREAM)
        {
            StringBuilder sbHttpResponse = new StringBuilder(256);

            sbHttpResponse.Append("HTTP/1.1 200 OK\r\n");
            //Content-Type
            sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_CONTENT_TYPE);
            sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_COLON);
            sbHttpResponse.Append(strContentType);
            sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.EOL);
            //Content-Length
            if (lContentLength > 0)
            {
                sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_CONTENT_LENGTH);
                sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_COLON);
                sbHttpResponse.Append(lContentLength);
                sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.EOL);
            }
            else
            {
                sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_TRANSFER_ENCODING);
                sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_COLON);
                sbHttpResponse.Append("Chunked");
                sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.EOL);
            }
            //Connection
            sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_CONNECTION);
            sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_COLON);
            if (bKeepAlive)
                sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_KEEP_ALIVE);
            else
                sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_CLOSE);
            sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.EOL);
            //EOL
            sbHttpResponse.Append(Pbk.Net.Http.HttpHeaderField.EOL);

            return Encoding.ASCII.GetBytes(sbHttpResponse.ToString());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void terminate()
        {
            if (this._TimerRefresh != null)
            {
                this._TimerRefresh.Enabled = false;
                this._TimerRefresh.Elapsed -= new System.Timers.ElapsedEventHandler(this.cbRefreshTmerElapsed);
                this._TimerRefresh.Dispose();
                this._TimerRefresh = null;

                if (this._TimerTerminate != null)
                {
                    this._TimerTerminate.Enabled = false;
                    this._TimerTerminate.Elapsed -= new System.Timers.ElapsedEventHandler(this.cbTerminateTimerElapsed);
                    this._TimerTerminate.Dispose();
                    this._TimerTerminate = null;
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void refreshMasterList()
        {
            if ((DateTime.Now - this._MasterListRefreshTs).TotalMilliseconds >= (this._TimerRefresh.Interval - 1000))
            {
                DateTime dtRefreshRequest = DateTime.Now;

                this.Logger.Debug("[{0}][refreshMasterList] Begin...", this._Identifier);

                double dTotalListDuration = 0;

                if (this._Request == null)
                {
                    this._Request = new Pbk.Net.Http.HttpUserWebRequest(this.Url);
                    this._Request.AllowSystemProxy = Database.dbSettings.Instance.AllowSystemProxy ? Pbk.Utils.OptionEnum.Yes : Pbk.Utils.OptionEnum.No;
                    this._Request.UseOpenSSL = Database.dbSettings.Instance.UseOpenSsl ? Pbk.Utils.OptionEnum.Yes : Pbk.Utils.OptionEnum.No;
                }

                string strResult = this._Request.Download<string>();
                if (this._UrlFinal == null)
                {
                    this._UrlFinal = this._Request.ServerUrlRedirect != null ? this._Request.ServerUrlRedirect : this.Url;
                    this._Request.Url = this._UrlFinal;
                }

                if (strResult != null)
                {
                    lock (this._Segments)
                    {
                        string strPath = this.WorkFolder;
                        this._SbMasterList.Clear();

                        bool bFlagM3uMarker = false;
                        bool bFlagUrlChunk = false;
                        bool bFlagUrlMasterLink = false;
                        bool bFlagList = false;
                        int iMediaSequenceCounter = 0;
                        bool bFlagDiscontinuity = false;
                        bool bFlagSequenceInit = false;
                        bool bFlagSequenceReset = false;
                        int iSegmentsAdded = 0;
                        double dTargetDuration = 0;
                        HlsDecryptor dec = null;

                        const string MPD_NS = "urn:mpeg:dash:schema:mpd:2011";

                        if (strResult.Contains(MPD_NS))
                        {
                            #region MPD_DASH
                            //https://cdn.bitmovin.com/content/assets/sintel/sintel.mpd

                            //https://dash.akamaized.net/dash264/TestCasesIOP33/adapatationSetSwitching/5/manifest.mpd

                            //DRM Widevine
                            //https://bitmovin-a.akamaihd.net/content/art-of-motion_drm/mpds/11331.mpd

                            XmlDocument xml = new XmlDocument();
                            xml.LoadXml(strResult);
                            XmlNamespaceManager mgr = new XmlNamespaceManager(xml.NameTable);
                            mgr.AddNamespace("ns", MPD_NS);
                            XmlNode nodeMPD = xml.SelectSingleNode("ns:MPD", mgr);
                            XmlNode nodeBaseURL = nodeMPD.SelectSingleNode("./ns:BaseURL", mgr);
                            XmlNode nodeLocation = nodeMPD.SelectSingleNode("./ns:Location", mgr);
                            XmlAttribute atrMinimumUpdatePeriod = nodeMPD.Attributes["minimumUpdatePeriod"];

                            //MPD location url
                            if (nodeLocation != null)
                            {
                                //Update local MPD url
                                this._UrlFinal = this.getFullUrl(nodeLocation.InnerText);
                                this._Request.Url = this._UrlFinal;
                                nodeLocation.ParentNode.RemoveChild(nodeLocation); //remove the location from MPD
                            }

                            //BaseURL
                            if (nodeBaseURL == null)
                            {
                                nodeBaseURL = nodeMPD.AppendChild(xml.CreateElement("BaseURL", MPD_NS));
                                Uri uri = new Uri(this._UrlFinal);
                                nodeBaseURL.InnerText = "/cdn/stream/" + this._Identifier + "/files" +
                                (uri.LocalPath.EndsWith("/") ? uri.LocalPath : uri.LocalPath.Substring(0, uri.LocalPath.Length - uri.Segments[uri.Segments.Length - 1].Length));
                            }
                            else
                            {
                                //Modify existing
                                string strFullUrl = this.getFullUrl(nodeBaseURL.InnerText);
                                nodeBaseURL.InnerText = "/cdn/stream/" + this._Identifier + "/files" + strFullUrl.Substring(strFullUrl.IndexOf('/', 8));
                                //to do: in case of abs url the domain can be different!!
                            }

                            //Update all elements
                            List<XmlNode> nodesToRemove = new List<XmlNode>();
                            List<string> listTemp = new List<string>();
                            nodeMPD.SelectNodes(".//@media").ForEach(n => replaceUrl(this.Url, n, mgr, listTemp));
                            nodeMPD.SelectNodes(".//@initialization").ForEach(n => replaceUrl(this.Url, n, mgr, listTemp));
                            nodeMPD.SelectNodes(".//ns:BaseURL", mgr).ForEach(n =>
                            {
                                string strText = n.InnerText;
                                if (strText != null && strText.Length > 0)
                                {
                                    if (strText[strText.Length - 1] != '/')
                                        replaceUrl(this.Url, n, mgr, listTemp); //subtitles
                                    else
                                        nodesToRemove.Add(n);// add to the remove list
                                }
                            });

                            //Remove all BaseURLs including root BaseURL
                            nodesToRemove.ForEach(n => n.ParentNode.RemoveChild(n));

                            #region ContentProtection
                            List<ContentProtection> prot = new List<ContentProtection>();
                            XmlNodeList nodesDRM = nodeMPD.SelectNodes(".//ns:Representation|.//ns:AdaptationSet", mgr);
                            nodesDRM.ForEach(n =>
                                {
                                    XmlNode nodeKID = n.SelectSingleNode("./ns:ContentProtection[@schemeIdUri='urn:mpeg:dash:mp4protection:2011']/@*[name()='cenc:default_KID']", mgr);
                                    XmlNode nodePssh = n.SelectSingleNode("./ns:ContentProtection[@schemeIdUri='urn:uuid:edef8ba9-79d6-4ace-a3c8-27dcd51d21ed']/*[name()='cenc:pssh']/text()", mgr);
                                    XmlNode nodeID = n.Name == "Representation" ? n.SelectSingleNode("./@id", mgr) : null;
                                    XmlNode nodeTemplate = n.SelectSingleNode("./ns:SegmentTemplate", mgr);
                                    if (nodeTemplate == null && n.Name == "Representation")
                                        nodeTemplate = n.ParentNode.SelectSingleNode("./ns:SegmentTemplate", mgr);

                                    if (nodeKID != null && nodePssh != null)
                                        prot.Add(new ContentProtection()
                                        {
                                            RepresentationID =  nodeID != null ? nodeID.Value : null,
                                            KID = nodeKID.Value.Replace("-" , ""),
                                            PSSH = nodePssh.Value,
                                            SegmentTemplateInit = nodeTemplate != null ? nodeTemplate.Attributes["initialization"].Value : null,
                                            SegmentTemplateMedia = nodeTemplate != null ? nodeTemplate.Attributes["media"].Value : null,
                                            InitFileFullPath = this.WorkFolder + Guid.NewGuid().ToString()
                                        });
                                });

                            if (prot.Count > 0)
                            {
                                Database.dbContentProtection dbCp;
                                List<Database.dbContentProtectionKey> keys;
                                prot.ForEach(p =>
                                    {
                                        dbCp = Database.dbContentProtection.Get(p.PSSH);
                                        if (dbCp == null)
                                        {
                                            #region Retrieve keys from Widevine client

#if _WIDEVINE_TEST
                                            //just for testing;
                                            k = new DecryptingKey[4]
                                                {
                                                    new DecryptingKey(){ ID = "9bf0e9cf0d7b55aeb4b289a63bab8610", Key = "90f52fd8ca48717b21d0c2fed7a12ae1"},
                                                    new DecryptingKey(){ ID = "eb676abbcb345e96bbcf616630f1a3da", Key = "100b6c20940f779a4589152b57d2dacb"},
                                                    new DecryptingKey(){ ID = "0294b9599d755de2bbf0fdca3fa5eab7", Key = "3bda2f40344c7def614227b9c0f03e26"},
                                                    new DecryptingKey(){ ID = "639da80cf23b55f3b8cab3f64cfa5df6", Key = "229f5f29b643e203004b30c4eaf348f4"},
                                                };
#else

                                            if (this._WidevineKeys == null)
                                                this._WidevineKeys = new List<Database.dbContentProtectionKey>();
                                            else
                                                this._WidevineKeys.Clear();

                                            ProcessStartInfo startInfo = new ProcessStartInfo();
                                            startInfo.Arguments = " --pssh " + p.PSSH;
                                            startInfo.FileName = "\"Widevine\\WidevineClient.exe\"";
                                            startInfo.UseShellExecute = false;
                                            startInfo.ErrorDialog = false;
                                            startInfo.RedirectStandardError = true;
                                            startInfo.RedirectStandardOutput = true;
                                            startInfo.CreateNoWindow = true;
                                            startInfo.WorkingDirectory = "Widevine";
                                            Process pr = new Process();
                                            pr.StartInfo = startInfo;
                                            //test.Exited += new EventHandler(restream_Exited);
                                            pr.OutputDataReceived += this.cbWidevineClientOutputHandler;
                                            pr.ErrorDataReceived += this.cbWidevineClientErrorHandler;

                                            //Start
                                            pr.Start();
                                            pr.BeginOutputReadLine();
                                            pr.BeginErrorReadLine();

                                            //Wait for finish
                                            pr.WaitForExit();

                                            pr.CancelOutputRead();
                                            pr.CancelErrorRead();

                                            if (this._WidevineKeys.Count == 0)
                                            {
                                                this.Logger.Error("[{0}][refreshMasterList] Failed to get Widevine keys: {1}", this._Identifier, p.PSSH);
                                                return;
                                            }
                                            else
                                            {
                                                keys = this._WidevineKeys;

                                                dbCp = new Database.dbContentProtection() { PSSH = p.PSSH };
                                                dbCp.CommitNeeded = true;
                                                dbCp.Commit();

                                                keys.ForEach(k =>
                                                    {
                                                        k.IdParent = (int)dbCp.ID;
                                                        k.CommitNeeded = true;
                                                        k.Commit();
                                                    });
                                            }
#endif
                                            #endregion
                                        }
                                        else
                                            keys = Database.dbContentProtectionKey.Get((int)dbCp.ID);

                                        Database.dbContentProtectionKey key = keys.Find(k => k.KID.Equals(p.KID));
                                        if (key != null)
                                            p.DecryptionKey = key.Key;
                                        else
                                        {
                                            this.Logger.Error("[{0}][refreshMasterList] Key not found: {1}", this._Identifier, p.KID);
                                            return;
                                        }
                                    });

                                if (prot.All(p => p.DecryptionKey != null))
                                {
                                    //Remove all protections
                                    nodeMPD.SelectNodes(".//ns:ContentProtection", mgr).ForEach(n => n.ParentNode.RemoveChild(n));

                                    //Set protection list
                                    this._ContentProtection = prot;
                                }
                            }
                            #endregion

                            //Generate a new MPD
                            this._SbMasterList.Append(xml.OuterXml);

                            //Get min update period
                            if (atrMinimumUpdatePeriod != null)
                            {
                                TimeSpan ts = parsePT(atrMinimumUpdatePeriod.Value);
                                if (ts.Ticks > 0)
                                    dTargetDuration = ts.TotalSeconds;
                            }

                            this._VideoType = VideoTypeEnum.MPD;

                            #endregion
                        }
                        else
                        {
                            #region M3U

                            //hls: 'https://cdn.bitmovin.com/content/assets/sintel/hls/playlist.m3u8',

                            //#EXTM3U

                            //#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="stereo",LANGUAGE="en",NAME="English",DEFAULT=YES,AUTOSELECT=YES,URI="audio/stereo/en/128kbit.m3u8"
                            //#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="stereo",LANGUAGE="dubbing",NAME="Dubbing",DEFAULT=NO,AUTOSELECT=YES,URI="audio/stereo/none/128kbit.m3u8"

                            //#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="surround",LANGUAGE="en",NAME="English",DEFAULT=YES,AUTOSELECT=YES,URI="audio/surround/en/320kbit.m3u8"
                            //#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="surround",LANGUAGE="dubbing",NAME="Dubbing",DEFAULT=NO,AUTOSELECT=YES,URI="audio/stereo/none/128kbit.m3u8"

                            //#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subs",NAME="Deutsch",DEFAULT=NO,AUTOSELECT=YES,FORCED=NO,LANGUAGE="de",URI="subtitles_de.m3u8"
                            //#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subs",NAME="English",DEFAULT=YES,AUTOSELECT=YES,FORCED=NO,LANGUAGE="en",URI="subtitles_en.m3u8"
                            //#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subs",NAME="Espanol",DEFAULT=NO,AUTOSELECT=YES,FORCED=NO,LANGUAGE="es",URI="subtitles_es.m3u8"
                            //#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subs",NAME="Français",DEFAULT=NO,AUTOSELECT=YES,FORCED=NO,LANGUAGE="fr",URI="subtitles_fr.m3u8"

                            //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=258157,CODECS="avc1.4d400d,mp4a.40.2",AUDIO="stereo",RESOLUTION=422x180,SUBTITLES="subs"
                            //video/250kbit.m3u8
                            //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=520929,CODECS="avc1.4d4015,mp4a.40.2",AUDIO="stereo",RESOLUTION=638x272,SUBTITLES="subs"
                            //video/500kbit.m3u8
                            //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=831270,CODECS="avc1.4d4015,mp4a.40.2",AUDIO="stereo",RESOLUTION=638x272,SUBTITLES="subs"
                            //video/800kbit.m3u8
                            //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=1144430,CODECS="avc1.4d401f,mp4a.40.2",AUDIO="surround",RESOLUTION=958x408,SUBTITLES="subs"
                            //video/1100kbit.m3u8
                            //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=1558322,CODECS="avc1.4d401f,mp4a.40.2",AUDIO="surround",RESOLUTION=1277x554,SUBTITLES="subs"
                            //video/1500kbit.m3u8
                            //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=4149264,CODECS="avc1.4d4028,mp4a.40.2",AUDIO="surround",RESOLUTION=1921x818,SUBTITLES="subs"
                            //video/4000kbit.m3u8
                            //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=6214307,CODECS="avc1.4d4028,mp4a.40.2",AUDIO="surround",RESOLUTION=1921x818,SUBTITLES="subs"
                            //video/6000kbit.m3u8
                            //#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=10285391,CODECS="avc1.4d4033,mp4a.40.2",AUDIO="surround",RESOLUTION=4096x1744,SUBTITLES="subs"
                            //video/10000kbit.m3u8


                            this._Segments.ForEach(t => t.IsInCurrentHlsListTmp = false);
                            string[] lines = strResult.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                            for (int i = 0; i < lines.Length; i++)
                            {
                                string strLine = lines[i].Trim();

                                if (string.IsNullOrEmpty(strLine))
                                    continue;

                                if (bFlagM3uMarker)
                                {
                                    if (bFlagList && !bFlagUrlChunk && strLine.StartsWith("#EXTINF:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        string strTitle = null;
                                        double dDuration = 0.0;

                                        Match match = HlsSequencer.RegexTitle.Match(strLine.Substring(8).Trim());
                                        if (match.Success)
                                        {
                                            strTitle = match.Groups["tit"].Value;
                                            double.TryParse(match.Groups["dur"].Value, NumberStyles.AllowDecimalPoint, _CiEn, out dDuration);
                                        }
                                        else
                                        {
                                            //_Logger.Error("[RefreshList] Invalid title/duration: '{0}'", strLine);
                                            continue;
                                        }

                                        dTotalListDuration += dDuration;
                                        if (bFlagSequenceInit)
                                            bFlagSequenceInit = false;
                                        else
                                            iMediaSequenceCounter++;

                                        bFlagUrlChunk = true;
                                    }
                                    else if (string.Compare(strLine, "#EXT-X-DISCONTINUITY", StringComparison.CurrentCultureIgnoreCase) == 0)
                                    {
                                        this.Logger.Debug("[{0}][refreshMasterList] Sequence discontinuity detected: {1}", this._Identifier, strLine);
                                        bFlagDiscontinuity = true;
                                    }
                                    else if (strLine.StartsWith("#EXT-X-STREAM-INF:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        bFlagUrlMasterLink = true;
                                    }
                                    else if (strLine.StartsWith("#EXT-X-PROGRAM-DATE-TIME:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                    }
                                    else if (string.Compare(strLine, "#EXT-X-ENDLIST", StringComparison.CurrentCultureIgnoreCase) == 0)
                                    {
                                    }
                                    else if (strLine.StartsWith("#EXT-X-TARGETDURATION:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        if (!double.TryParse(strLine.Substring(22), NumberStyles.AllowDecimalPoint, _CiEn, out dTargetDuration))
                                            dTargetDuration = 0;
                                    }
                                    else if (strLine.StartsWith("#EXT-X-MEDIA-SEQUENCE:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        if (!int.TryParse(strLine.Substring(22), out iMediaSequenceCounter))
                                        {
                                            this.Logger.Error("[{0}][refreshMasterList] Invalid M3U content. Bad sequence number: {1}", this._Identifier, strLine);
                                            return;
                                        }

                                        bFlagSequenceInit = true;
                                        bFlagList = true;

                                        this.Logger.Debug("[{0}][refreshMasterList] Media Sequence: {1}", this._Identifier, iMediaSequenceCounter);
                                    }
                                    else if (strLine.StartsWith("#EXT-X-KEY:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        #region AES

                                        this.Logger.Debug("[{0}][refreshMasterList] AES-128 decryptor detected. SQ_ID: {1}", this._Identifier, iMediaSequenceCounter);

                                        try
                                        {
                                            MatchCollection mc = HlsSequencer.RegexXparam.Matches(strLine.Substring(11).Trim());
                                            if (mc.Count > 0)
                                            {
                                                string strMethod = "";
                                                string strKeyUri = "";
                                                string strIV = "";
                                                string strKeyformat = "";

                                                foreach (Match m in mc)
                                                {
                                                    string strValue = m.Groups["value"].Value.Trim();
                                                    switch (m.Groups["key"].Value)
                                                    {
                                                        case "METHOD":
                                                            strMethod = strValue;
                                                            break;

                                                        case "URI":
                                                            strKeyUri = this.getFullUrl(strValue.Trim('\"'));
                                                            break;

                                                        case "IV":
                                                            if (strValue.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase))
                                                                strIV = strValue.Substring(2).Trim();
                                                            break;

                                                        case "KEYFORMAT":
                                                            strKeyformat = strValue.Trim('\"');
                                                            break;

                                                        case "KEYFORMATVERSIONS":
                                                            break;
                                                    }
                                                }

                                                if (strMethod == "AES-128" && !string.IsNullOrEmpty(strKeyUri) && (string.IsNullOrEmpty(strKeyformat) || strKeyformat == "identity"))
                                                {
                                                    dec = this.getDecryptor(strLine);
                                                    if (dec == null)
                                                    {
                                                        byte[] aesKey;
                                                        using (Pbk.Net.Http.HttpUserWebRequest wr = new Pbk.Net.Http.HttpUserWebRequest(strKeyUri))
                                                        {
                                                            wr.AllowSystemProxy = Database.dbSettings.Instance.AllowSystemProxy ? Pbk.Utils.OptionEnum.Yes : Pbk.Utils.OptionEnum.No;
                                                            wr.UseOpenSSL = Database.dbSettings.Instance.UseOpenSsl ? Pbk.Utils.OptionEnum.Yes : Pbk.Utils.OptionEnum.No;
                                                            aesKey = wr.Download<byte[]>();
                                                        }


                                                        if (aesKey == null)
                                                        {
                                                            this.Logger.Error("[{0}][refreshMasterList] Failed to get AES key.", this._Identifier);
                                                            this.clearDecryptors();
                                                            return;
                                                        }

                                                        byte[] aesIV = null;

                                                        if (!string.IsNullOrEmpty(strIV))
                                                            aesIV = Pbk.Utils.Tools.ParseByteArrayFromHex(strIV);

                                                        dec = this.pushNewDecryptor(strLine, iMediaSequenceCounter, aesKey, aesIV);

                                                        this.Logger.Debug("[{0}][refreshMasterList] AES-128 decryptor created: {1}", this._Identifier, strLine);
                                                    }
                                                }
                                                else if (strMethod.Equals("none", StringComparison.CurrentCultureIgnoreCase))
                                                {
                                                    dec = null;
                                                }
                                                else
                                                {
                                                    this.Logger.Error("[{0}][refreshMasterList] Invalid decryptor: {1}", this._Identifier, strLine);
                                                    this.clearDecryptors();
                                                    return;
                                                }
                                            }

                                            continue;
                                        }
                                        catch
                                        {
                                            this.Logger.Error("[{0}][refreshMasterList] Invalid decryptor: {1}", this._Identifier, strLine);
                                            this.clearDecryptors();
                                            return;
                                        }
                                        //#EXT-X-KEY:METHOD=AES-128,URI="https://sslhls.m6tv.cdn.sfr.net/hls-key/m6_music_hits_hls_aes.bin",IV=0xa690c26b8a71ad0916caf88d363b6303
                                        #endregion
                                    }
                                    else if (strLine.StartsWith("#EXT-X-VERSION:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                    }
                                    else if (strLine.StartsWith("#EXT-X-DISCONTINUITY-SEQUENCE:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        this.Logger.Debug("[{0}][refreshMasterList] Sequence discontinuity detected: {1}", this._Identifier, strLine);
                                        bFlagSequenceReset = true;
                                    }
                                    else if (strLine.StartsWith("#EXT-X-MEDIA:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        int iIdx = strLine.IndexOf("URI=\"");
                                        if (iIdx > 0)
                                        {
                                            iIdx += 5;
                                            this._SbMasterList.Append(strLine, 0, iIdx);
                                            int iIdxEnd = strLine.IndexOf("\"", iIdx);

                                            this._SbMasterList.Append("/get?url=");
                                            this._SbMasterList.Append(System.Web.HttpUtility.UrlEncode(this.getFullUrl(strLine.Substring(iIdx, iIdxEnd - iIdx))));
                                            this._SbMasterList.Append(strLine, iIdxEnd, strLine.Length - iIdxEnd);
                                            this._SbMasterList.Append("\r\n");

                                            continue;

                                            //#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="stereo",LANGUAGE="en",NAME="English",DEFAULT=YES,AUTOSELECT=YES,URI="audio/stereo/en/128kbit.m3u8"
                                        }
                                    }
                                    else if (strLine.StartsWith("#EXT-X-", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                    }
                                    else if (bFlagUrlMasterLink)
                                    {
                                        this._SbMasterList.Append("/GetMediaHandler?url=");
                                        this._SbMasterList.AppendLine(System.Web.HttpUtility.UrlEncode(this.getFullUrl(strLine)));
                                        bFlagUrlMasterLink = false;
                                        continue;
                                    }
                                    else if (bFlagUrlChunk)
                                    {
                                        string strFullUrl = this.getFullUrl(strLine);

                                        TaskSegment task = this._Segments.Find(t => t.Url == strFullUrl);

                                        if (task == null)
                                        {
                                            string strFilename = "file_" + this._FileIdCounter + ".bin";
                                            this.addNewSegment(new TaskSegmentCDN(this)
                                            {
                                                Index = this._FileIdCounter,
                                                Url = strFullUrl,
                                                Filename = strFilename,
                                                FullPath = strPath + strFilename,
                                                IsInCurrentHlsList = true,
                                                IsInCurrentHlsListTmp = true,
                                                HLS_Decryptor = dec,
                                                ID = iMediaSequenceCounter
                                            });

                                            this.appendFile(this._FileIdCounter);
                                            this._FileIdCounter++;

                                            iSegmentsAdded++;

                                            this.Logger.Debug("[{0}][refreshMasterList] New segment: {1} {2} {3}", this._Identifier, iMediaSequenceCounter, strFilename, strFullUrl);
                                        }
                                        else
                                        {
                                            task.IsInCurrentHlsListTmp = true;
                                            this.appendFile(task.Index);
                                        }

                                        this._SbMasterList.Append("\r\n");

                                        bFlagUrlChunk = false;

                                        continue;
                                    }
                                }
                                else if (strLine == "#EXTM3U")
                                    bFlagM3uMarker = true;
                                else
                                    continue;

                                this._SbMasterList.AppendLine(strLine);

                                this._VideoType = VideoTypeEnum.HLS;
                            }
                            #endregion
                        }

                        this.Logger.Debug("[{0}][refreshMasterList] Type:{1}  DurationTotal:{2}  DurationTarget:{3}", 
                            this._Identifier, this._VideoType, dTotalListDuration, dTargetDuration);

                        //Refresh interval for maintenance
                        int iInterval = Math.Min(30000, Math.Max(2000, (int)((dTargetDuration > 0 ? (dTargetDuration * 1.2) : (dTotalListDuration * 0.33)) * 1000)));
                        if (this._TimerRefresh.Interval != iInterval)
                        {
                            this._TimerRefresh.Interval = iInterval;
                            this.Logger.Debug("[{0}][refreshMasterList] RefreshInterval set to: {1}", this._Identifier, iInterval);
                        }

                        //update http data
                        string strMasterList = this._SbMasterList.ToString();
                        this._MasterListRaw = Encoding.ASCII.GetBytes(strMasterList);
                        this._MasterListRefreshTs = dtRefreshRequest;

                        if (Log.LogLevel <= LogLevel.Trace) this.Logger.Trace("[{0}][refreshMasterList] Master list:\r\n{1}", this._Identifier, strMasterList);


                    }//Release lock(this._Segments)

                }
                else
                    this.Logger.Error("[{0}][refreshMasterList] Download failed.", this._Identifier);

            }
        }

        private void segmentCleaning()
        {
            this.Logger.Debug("[segmentCleaning] Run...");

            #region Remove old
            int i = 0;
            while (i < this._Segments.Count)
            {
                TaskSegment seg = this._Segments[i];

                if (seg.IsInCurrentHlsList != seg.IsInCurrentHlsListTmp)
                {
                    seg.IsInCurrentHlsList = seg.IsInCurrentHlsListTmp;

                    //Event
                    this.OnEvent(new TaskEventArgs() { Type = TaskEventTypeEnum.SegmentStateChanged, Tag = seg });
                }

                if (!seg.IsInCurrentHlsList && (DateTime.Now - seg.LastAccess).TotalSeconds >= 60)
                {
                    try
                    {
                        File.Delete(seg.FullPath);
                        //bChange = true;
                        this._Segments.RemoveAt(i);

                        //Event
                        this.OnEvent(new TaskEventArgs() { Type = TaskEventTypeEnum.SegmentDeleted, Tag = seg });

                        this.Logger.Debug("[segmentCleaning] File removed: " + seg.FullPath);

                        continue;
                    }
                    catch { }//probably still using by http
                }

                i++;
            }
            #endregion
        }

        private void segmentRequest(TaskSegmentCDN segment)
        {
            if (Database.dbSettings.Instance.MediaServerBeginDownloadOnRequest)
                JobHandler.AddToQueue(segment, cbSegmentAddedToDownloadQueue);
            else
            {
                //Automatic download mode

                if (this._QueueStarted)
                    return; //queue already running
                else
                {
                    //First run
                    //Add to queue all other segments following the requested one
                    lock (this._Segments)
                    {
                        int iIdx = this._Segments.IndexOf(segment);
                        if (iIdx >= 0)
                        {
                            do
                            {
                                TaskSegmentCDN seg = (TaskSegmentCDN)this._Segments[iIdx];
                                if (seg.Status == TaskStatusEnum.Ready)
                                    JobHandler.AddToQueue(seg, cbSegmentAddedToDownloadQueue);

                            }
                            while (++iIdx < this._Segments.Count);
                        }
                    }

                    this._QueueStarted = true;

                }
            }

        }

        private void addNewSegment(TaskSegment seg)
        {
            this._Segments.Add(seg);

            //Event
            this.OnEvent(new TaskEventArgs() { Type = TaskEventTypeEnum.SegmentNew, Tag = seg });
        }

        private string getFullUrl(string strUrl)
        {
            return getFullUrl(strUrl, !string.IsNullOrEmpty(this._UrlFinal) ? this._UrlFinal : this.Url);
        }
        private static string getFullUrl(string strUrl, string strUrlMain)
        {
            if (strUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || strUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return strUrl; //strUrl is absolute url
            else if (strUrl.StartsWith("./"))
                return strUrlMain.Substring(0, strUrlMain.LastIndexOf("/") + 1) + strUrl.Substring(2); //strUrl points to current folder
            else if (strUrl.StartsWith("//"))
                return strUrlMain.Substring(0, strUrlMain.IndexOf(":") + 1) + strUrl; //strUrl is absolute without scheme
            else if (strUrl.StartsWith("/"))
                return strUrlMain.Substring(0, strUrlMain.IndexOf("/", 8)) + strUrl; //strUrl is path & filename
            else if (strUrl.StartsWith("../"))
            {
                int iCnt = 2;
                int iIdx = 3;
                while (iIdx < strUrl.Length)
                {
                    int iT = strUrl.IndexOf("../", iIdx);
                    if (iT >= 0)
                    {
                        iIdx = iT + 3;
                        iCnt++;
                    }
                    else
                        break;
                }

                int iIdx2 = strUrlMain.Length;
                while (iCnt-- > 0)
                {
                    iIdx2 = strUrlMain.LastIndexOf("/", iIdx2) - 1;
                }

                return strUrlMain.Substring(0, iIdx2 + 2) + strUrl.Substring(iIdx);
            }
            else
                return strUrlMain.Substring(0, strUrlMain.LastIndexOf("/") + 1) + strUrl; //strUrl is subpath
        }

        private static string replaceUrl(string strUrlAbsolute, XmlNode node, XmlNamespaceManager m, List<string> list)
        {
            list.Clear();

            XmlNode nodeOrig = node;
            if (node is XmlAttribute)
            {
                list.Insert(0, node.Value);
                node = ((XmlAttribute)node).OwnerElement;
            }
            else
            {
                list.Insert(0, node.InnerText);
                node = node.ParentNode;
            }

            node = node.ParentNode;

            do
            {
                XmlNode n = node.SelectSingleNode("./ns:BaseURL", m);
                if (n != null)
                    list.Insert(0, n.InnerText);

                node = node.ParentNode;

            } while (node.ParentNode != null);

            string strResult = strUrlAbsolute;

            list.ForEach(s =>
            {
                strResult = getFullUrl(s, strResult);
            });

            //Get relative part
            strResult = strResult.Substring(strResult.IndexOf('/', 8));

            if (nodeOrig is XmlAttribute)
                nodeOrig.Value = strResult;
            else
                nodeOrig.InnerText = strResult;

            return strResult;
        }

        private void appendFile(int iId)
        {
            this._SbMasterList.Append("/cdn/stream/");
            this._SbMasterList.Append(this._Identifier);
            this._SbMasterList.Append("/file_");
            this._SbMasterList.Append(iId);
            this._SbMasterList.Append(".bin");
        }

        private static TimeSpan parsePT(string s)
        {
            //PT24H44M42.24S

            long l = 0;

            if (s.StartsWith("PT"))
            {
                int i = 2;
                int iStart = i;
                int iCnt = 0;
                while (i < s.Length)
                {
                    char c = s[i++];
                    switch (c)
                    {
                        case 'H':
                        case 'M':
                        case 'S':
                            if (iCnt > 0)
                            {
                                l += (long)(double.Parse(s.Substring(iStart, i - iStart - 1), _CiEn) * (c == 'H' ? 36000000000 : (c == 'M' ? 600000000 : 10000000)));
                                iCnt = 0;
                                iStart = i;
                            }
                            else
                                return new TimeSpan();

                            break;

                        default:
                            if ((c >= '0' && c <= '9') || c == '.')
                                iCnt++;
                            else
                                return new TimeSpan();
                            break;
                    }
                }
            }

            return new TimeSpan(l);
        }

        private static int printChunkSize(uint wValue, byte[] array)
        {
            int iIdx = 0;
            if (wValue > 0)
            {
                uint w;
                int iShift = 28;
                do
                {
                    w = (wValue >> iShift) & 0xF;
                    if (w > 0 || iIdx > 0)
                        array[iIdx++] = (byte)(w + (w > 9 ? 'W' : '0'));
                    iShift -= 4;
                } while (iShift >= 0);

                array[iIdx++] = (byte)'\r';
                array[iIdx++] = (byte)'\n';
                return iIdx;
            }
            else
            {
                array[0] = (byte)'0';
                array[1] = (byte)'\r';
                array[2] = (byte)'\n';
                array[3] = (byte)'\r';
                array[4] = (byte)'\n';
                return 5;
            }
        }

        #region Callbacks
        private void cbRefreshTmerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.segmentCleaning(); //Do maintanenece
        }

        private void cbTerminateTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.Stop();
        }

        private void cbSegmentAddedToDownloadQueue(object sender, EventArgs e)
        {
            JobEventArgs args = (JobEventArgs)e;

            this.onSegmentStatusChanged((TaskSegmentCDN)args.Job, TaskStatusEnum.InQueue);

        }


        private void cbWidevineClientOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                this.Logger.Debug("[cbWidevineClientOutputHandler] " + e.Data);
                if (e.Data.StartsWith("KEY: "))
                {
                    string[] parts = e.Data.Substring(5).Split(':');
                    if (parts.Length == 2)
                        this._WidevineKeys.Add(new Database.dbContentProtectionKey() { KID = parts[0], Key = parts[1] });
                }
            }
        }

        private void cbWidevineClientErrorHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                this.Logger.Debug("[cbWidevineClientErrorHandler] " + e.Data);
        }
        #endregion

        #region Decryptor buffer
        private bool decryptorExist(int iSqId)
        {
            lock (this._DecryptorBuffer)
            {
                return this._DecryptorBuffer.Exists(p => p.SqId == iSqId);
            }
        }

        private HlsDecryptor pushNewDecryptor(string strID, int iSqId, byte[] aesKey, byte[] aesIV)
        {
            lock (this._DecryptorBuffer)
            {
                HlsDecryptor dec = new HlsDecryptor(strID, iSqId,
                     new RijndaelManaged() { Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7, Key = aesKey },
                    aesIV);

                this._DecryptorBuffer.Add(dec);

                if (Log.LogLevel <= LogLevel.Debug) this.Logger.Debug("[pushDecryptor] Decryptor created. SQ_ID: " + iSqId);

                if (this._DecryptorBuffer.Count > BUFFER_DECRYPTORS_LIMIT)
                {
                    this._DecryptorBuffer.RemoveAt(0);
                }

                return dec;
            }
        }

        private void clearDecryptors()
        {
            lock (this._DecryptorBuffer)
            {
                this._DecryptorBuffer.Clear();
            }
        }

        private HlsDecryptor getDecryptor(int iSqId)
        {
            lock (this._DecryptorBuffer)
            {
                for (int i = this._DecryptorBuffer.Count - 1; i >= 0; i--)
                {
                    HlsDecryptor dec = this._DecryptorBuffer[i];

                    if (iSqId >= dec.SqId)
                        return dec;
                }

                return null;
            }
        }

        private HlsDecryptor getDecryptor(string strID)
        {
            lock (this._DecryptorBuffer)
            {
                for (int i = this._DecryptorBuffer.Count - 1; i >= 0; i--)
                {
                    HlsDecryptor dec = this._DecryptorBuffer[i];

                    if (strID.Equals(dec.ID))
                        return dec;
                }

                return null;
            }
        }

        #endregion

        #endregion

        public StringBuilder SerializeJson(StringBuilder sb)
        {
            sb.Append('{');
            sb.Append("\"type\":\"TaskCDN\",");
            sb.Append("\"id\":\"");
            sb.Append(this.Identifier);
            sb.Append("\",\"status\":\"");
            sb.Append(this.Status);
            sb.Append("\",\"title\":\"");
            Tools.Json.AppendAndValidate(this.Title, sb);
            sb.Append("\"}");

            return sb;
        }
    }
}

