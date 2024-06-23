using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using MediaPortal.Pbk.Logging;
using NLog;

namespace MediaPortal.Pbk.Net.Http
{
    public class Caching
    {
        public const string CACHE_FILENAME_EXT = "webcache";

        private const int _CLEAN_UP_PERIOD = 60 * 24; //[minutes]
        private const int _CACHE_FILES_LIFETIME = 60 * 24 * 7; //[minutes]
        private const int _CACHE_FILES_REFRESH = 60 * 24; //[minutes]

        public delegate object PostDownloadHandler(object sender, object dataIn, object Tag);

        public delegate void DeleteHandler(object sender, string strFilepath);

        private class CachedData
        {
            public object Data;
            public DateTime TimeStamp = DateTime.MinValue;
            public List<string> Tokens = new List<string>();
            public string CacheFilePath = null;
        }

        private List<string> _CacheRequests = new List<string>();

        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        private DateTime _LastCleanUp = DateTime.MinValue;

        private Dictionary<string, CachedData> _CachedList = new Dictionary<string, CachedData>();

        private int _Id = -1;
        private static int _IdCounter = -1;

        private static StringBuilder _SbHash = new StringBuilder(128);
        private static System.Security.Cryptography.MD5 _Md5Hash = System.Security.Cryptography.MD5.Create();

        public string CachePath
        {
            get
            {
                if (_CachePath == null)
                {
                    this._CachePath = MediaPortal.Configuration.Config.GetFolder(MediaPortal.Configuration.Config.Dir.Thumbs) + "\\Pbk\\Cache\\";

                    try
                    {
                        if (!Directory.Exists(this._CachePath))
                            Directory.CreateDirectory(this._CachePath);
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[{3}][CachePath] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                    }

                }
                return this._CachePath;
            }

            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (!value.EndsWith("\\"))
                        value += "\\";

                    this._CachePath = value;

                    try
                    {
                        if (!Directory.Exists(this._CachePath))
                            Directory.CreateDirectory(_CachePath);

                        _Logger.Debug("[{0}][CachePath] {1}", this._Id, value);
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[{3}][CachePath] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                    }
                }
            }

        }private string _CachePath = null;

        public event DeleteHandler Delete;

        public Utils.OptionEnum UseOpenSSL = Utils.OptionEnum.Default;
        public Utils.OptionEnum AllowSystemProxy = Utils.OptionEnum.Default;

        public static Caching Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Caching();
#if TV_SERVER
                    _Instance.CachePath =  TvLibrary.Log.Log.GetPathName() + "\\Pbk\\Cache\\";
#else
                    _Instance.CachePath = MediaPortal.Configuration.Config.GetFolder(MediaPortal.Configuration.Config.Dir.Thumbs) + "\\Pbk\\Cache\\";
#endif
                }

                return _Instance;
            }
        }private static Caching _Instance = null;


#region ctor
        static Caching()
        {
            Logging.Log.Init();
        }

        public Caching()
        {
            this._Id = Interlocked.Increment(ref _IdCounter);
        }
#endregion

        /// <summary>
        /// Delete expired cached files
        /// </summary>
        /// <param name="bAll">True: delete all files include non expired</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void CleanUp(bool bAll)
        {
            if (bAll || (DateTime.Now - this._LastCleanUp).TotalMinutes >= _CLEAN_UP_PERIOD)
            {
                _Logger.Debug("[{0}][CleanUp] CleanUp is in due.", this._Id);

                while (true)
                {
                    lock (this._CacheRequests)
                    {
                        if (this._CacheRequests.Count == 0)
                            break;
                    }

                    System.Threading.Thread.Sleep(100);
                }

                _Logger.Debug("[{0}][CleanUp] Cleaning...", this._Id);

                DirectoryInfo di = new DirectoryInfo(this.CachePath);
                FileInfo[] fiList = di.GetFiles("*." + CACHE_FILENAME_EXT);
                foreach (FileInfo fi in fiList)
                {
                    if (bAll || (DateTime.Now - fi.LastAccessTime).TotalMinutes >= _CACHE_FILES_LIFETIME)
                    {
                        try
                        {
                            _Logger.Debug("[{0}][CleanUp] Deleting file: {1}", this._Id, fi.FullName);

                            try
                            {
                                //Event
                                if (this.Delete != null)
                                    this.Delete(this, fi.FullName);
                            }
                            catch (Exception ex)
                            {
                                _Logger.Error("[{3}][CleanUp] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                            }

                            File.Delete(fi.FullName);
                        }
                        catch (Exception ex)
                        {
                            _Logger.Error("[{3}][CleanUp] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                        }
                    }
                }

                this._LastCleanUp = DateTime.Now;
            }
        }

        /// <summary>
        /// Returns cached memory data
        /// </summary>
        /// <param name="strKey">data key</param>
        /// <param name="strUrl">data url</param>
        /// <param name="strToken">request token; can be null</param>
        /// <param name="iLifeTime">max data age [s]</param>
        /// <param name="postDownload">post download process</param>
        /// <param name="postDownloadTag">post download process tag</param>
        /// <param name="iAge">age of data [s]; &lt; 0 new data</param>
        /// <returns>an cached data</returns>
        public T GetData<T>(string strKey, string strUrl, string strToken, int iLifeTime, PostDownloadHandler postDownload, object postDownloadTag, out int iAge)
        {
            try
            {
                if (string.IsNullOrEmpty(strKey) || string.IsNullOrEmpty(strUrl))
                {
                    _Logger.Error("[{1}][GetData] Invalid params.  KEY:'{0}' URL:", strKey, strUrl, this._Id);
                    iAge = -1;
                    return (T)(object)null;
                }

                if (iLifeTime < 5)
                    iLifeTime = 5;

                string strContent = null;
                byte[] content = null;
                CachedData cachedData = null;
                bool bTokenUsed = !string.IsNullOrEmpty(strToken);
                string strFilePath = this.CachePath + strKey;

                if (this._CachedList.TryGetValue(strKey, out cachedData))
                {
                    //Cached data exist

                    //Check life time
                    iAge = (int)(DateTime.Now - cachedData.TimeStamp).TotalSeconds;
                    if (cachedData.Data != null && iAge < iLifeTime)
                    {
                        if (bTokenUsed && !cachedData.Tokens.Exists(p => p == strToken))
                        {
                            //Token not exist; report as new data
                            iAge = -1;
                            cachedData.Tokens.Add(strToken);
                        }

                        return (T)(object)cachedData.Data;
                    }
                }

                //New data refresh
                iAge = -1;

                if (content == null && strContent == null)
                {
                    //download data
                    try
                    {
                        using (HttpUserWebRequest wc = new HttpUserWebRequest(string.Format(strUrl, strKey))
                            { UseOpenSSL = this.UseOpenSSL, AllowSystemProxy = this.AllowSystemProxy })
                        {
                            content = wc.Download<byte[]>();
                        }

                        if (typeof(T) == typeof(byte[]))
                        {
                            if (postDownload != null)
                                content = postDownload(this, content, postDownloadTag) as byte[];

                            if (content == null)
                            {
                                _Logger.Error("[{1}][GetDataFile] Failed to get web data. URL:'{0}'", strUrl, this._Id);
                                return (T)(object)null;
                            }

                            //save to file
                            using (FileStream fs = new FileStream(strFilePath, FileMode.Create))
                            {
                                fs.Write(content, 0, content.Length);
                            }

                        }
                        else
                        {
                            if (content != null)
                                strContent = Encoding.UTF8.GetString(content);

                            if (postDownload != null)
                                strContent = postDownload(this, strContent, postDownloadTag) as string;

                            if (string.IsNullOrEmpty(strContent))
                            {
                                _Logger.Error("[{1}][GetDataFile] Failed to get web data. URL:'{0}'", strUrl, this._Id);
                                return (T)(object)null;
                            }

                            //save to file
                            using (StreamWriter sw = new StreamWriter(strFilePath))
                            {
                                sw.Write(strContent);
                            }
                        }
                    }
                    catch
                    {
                    }

                    if (string.IsNullOrEmpty(strContent) && content == null)
                    {
                        _Logger.Error("[{1}][GetData] Failed to get web data. URL:'{0}'", strUrl, this._Id);
                        return (T)(object)null;
                    }
                }


                object result = null;

                if (typeof(T) == typeof(string))
                {
                    result = strContent;
                }
                else if (typeof(T) == typeof(XmlDocument))
                {
                    result = new XmlDocument();
                    ((XmlDocument)result).LoadXml(strContent);
                }
                else if (typeof(T) == typeof(byte[]))
                {
                    result = content;
                }


                if (cachedData == null)
                {
                    cachedData = new CachedData();
                    this._CachedList.Add(strKey, cachedData);
                }
                else
                    cachedData.Tokens.Clear(); //new data; clear all tokens

                cachedData.TimeStamp = DateTime.Now;
                cachedData.Data = result;

                if (bTokenUsed)
                    cachedData.Tokens.Add(strToken);

                return (T)(object)result;
            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][GetData] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                iAge = -1;
                return (T)(object)null;
            }

        }

        /// <summary>
        /// Returns cached file data
        /// </summary>
        /// <param name="strKey">data key</param>
        /// <param name="strUrl">data url</param>
        /// <param name="strToken">request token; can be null</param>
        /// <param name="iLifeTime">max data age [s]</param>
        /// <param name="postDownload">post download process</param>
        /// <param name="postDownloadTag">post download process tag</param>
        /// <param name="iAge">age of data [s]; &lt; 0 new data</param>
        /// <returns>an cached data</returns>
        public T GetDataFile<T>(string strKey, string strUrl, string strToken, int iLifeTime, PostDownloadHandler postDownload, object postDownloadTag, out int iAge)
        {
            iAge = -1;

            if (string.IsNullOrEmpty(strKey) || string.IsNullOrEmpty(strUrl))
            {
                _Logger.Error("[{1}][GetDataFile] Invalid params.  KEY:'{0}' URL:", strKey, strUrl, this._Id);
                return (T)(object)null;
            }

            if (iLifeTime < 5)
                iLifeTime = 5;

            string strContent = null;
            byte[] content = null;
            CachedData cachedData = null;
            bool bTokenUsed = !string.IsNullOrEmpty(strToken);

            this.CleanUp(false);

            string strFilePath = string.Format("{0}{1}.{2}", this.CachePath, strKey, CACHE_FILENAME_EXT);

            lock (this._CacheRequests)
            {
                while (this._CacheRequests.Exists(p => p.Equals(strFilePath)))
                {
                    _Logger.Debug("[{0}][GetDataFile] Wait: Url in the progress. {1}", this._Id,  strUrl);

                    //Wait, url is in the progress now
                    Monitor.Wait(this._CacheRequests);

                    //Now we can check the existing request again
                }

                this._CacheRequests.Add(strFilePath); //Add url to the progress list

            }

            try
            {
                bool bLoadFromCache = false;

                if (this._CachedList.TryGetValue(strKey, out cachedData))
                {
                    //Cached data exist

                    //Check life time
                    iAge = (int)(DateTime.Now - cachedData.TimeStamp).TotalSeconds;
                    if (iAge < iLifeTime)
                    {
                        if (bTokenUsed && !cachedData.Tokens.Exists(p => p == strToken))
                        {
                            //Token not exist; report as new data
                            iAge = -1;
                            cachedData.Tokens.Add(strToken);
                        }

                        bLoadFromCache = true;
                    }
                }
                else
                    bLoadFromCache = true;

                if (bLoadFromCache && File.Exists(strFilePath))
                {
                    //from cached file
                    FileInfo fi = new FileInfo(strFilePath);
                    if (fi.Length > 0)
                    {
                        iAge = (int)(DateTime.Now - fi.LastWriteTime).TotalSeconds;
                        if (iAge < iLifeTime)
                        {
                            if (typeof(T) == typeof(byte[]))
                            {
                                using (FileStream fs = new FileStream(strFilePath, FileMode.Open))
                                {
                                    content = new byte[fi.Length];
                                    fs.Read(content, 0, (int)fi.Length);
                                }
                            }
                            else
                            {
                                using (StreamReader sr = new StreamReader(strFilePath))
                                {
                                    strContent = sr.ReadToEnd();
                                }
                            }
                        }
                        else
                            iAge = -1;
                    }
                }

                if (content == null && strContent == null)
                {
                    //download data
                    try
                    {
                        using (HttpUserWebRequest wc = new HttpUserWebRequest(string.Format(strUrl, strKey))
                            { UseOpenSSL = this.UseOpenSSL, AllowSystemProxy = this.AllowSystemProxy})
                        {
                            content = wc.Download<byte[]>();
                        }

                        if (typeof(T) == typeof(byte[]))
                        {
                            if (postDownload != null)
                                content = postDownload(this, content, postDownload) as byte[];

                            if (content == null)
                            {
                                _Logger.Error("[{1}][GetDataFile] Failed to get web data. URL:'{0}'", strUrl, this._Id);
                                return (T)(object)null;
                            }

                            //save to file
                            using (FileStream fs = new FileStream(strFilePath, FileMode.Create))
                            {
                                fs.Write(content, 0, content.Length);
                            }

                        }
                        else
                        {
                            if (content != null)
                                strContent = Encoding.UTF8.GetString(content);

                            if (postDownload != null)
                                strContent = postDownload(this, strContent, postDownloadTag) as string;

                            if (string.IsNullOrEmpty(strContent))
                            {
                                _Logger.Error("[{1}][GetDataFile] Failed to get web data. URL:'{0}'", strUrl, this._Id);
                                return (T)(object)null;
                            }

                            //save to file
                            using (StreamWriter sw = new StreamWriter(strFilePath))
                            {
                                sw.Write(strContent);
                            }

                        }
                    }
                    catch
                    {
                    }

                    if (string.IsNullOrEmpty(strContent) && content == null)
                    {
                        _Logger.Error("[{1}][GetData] Failed to get web data. URL:'{0}'", strUrl, this._Id);
                        return (T)(object)null;
                    }
                }


                object result = null;

                if (typeof(T) == typeof(string))
                    result = strContent;
                else if (typeof(T) == typeof(XmlDocument))
                {
                    result = new XmlDocument();
                    ((XmlDocument)result).LoadXml(strContent);
                }
                else if (typeof(T) == typeof(byte[]))
                    result = content;


                if (!bLoadFromCache)
                {
                    if (cachedData == null)
                    {
                        cachedData = new CachedData();
                        this._CachedList.Add(strKey, cachedData);
                    }
                    else
                        cachedData.Tokens.Clear(); //new data; clear all tokens

                    cachedData.TimeStamp = DateTime.Now;

                    if (bTokenUsed)
                        cachedData.Tokens.Add(strToken);
                }

                return (T)(object)result;
            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][GetDataFile] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                iAge = -1;
                return (T)(object)null;
            }
            finally
            {
                lock (this._CacheRequests)
                {
                    //Remove url from the progress list
                    this._CacheRequests.Remove(strFilePath);

                    //Awake waiting threads
                    Monitor.PulseAll(this._CacheRequests);
                }
            }

        }

        /// <summary>
        /// Download file based on url or webrequest
        /// </summary>
        /// <param name="strFilename">filename to be saved</param>
        /// <param name="strUrl">url of the file to be downloaded</param>
        /// <param name="iLifeTime">liftime of the cached file in minutes; 0: no caching; -1: default lifetime</param>
        /// <param name="postDownload">post download callback</param>
        /// <param name="postDownloadTag">post download user tag</param>
        /// <param name="wr">Explict webrequest</param>
        /// <returns>Cached fullpath filename</returns>
        public string DownloadFile(string strUrl,
             string strFilename = null,
            int iLifeTime = -1,
            PostDownloadHandler postDownload = null,
            object postDownloadTag = null,
            HttpUserWebRequest wr = null)
        {
            if (wr == null && string.IsNullOrWhiteSpace(strUrl))
                throw new ArgumentException("Both url and webrequest is null");

            if (wr != null)
            {
                if (string.IsNullOrWhiteSpace(wr.Url))
                    throw new ArgumentException("Request url is invalid");

                strUrl = wr.Url;
            }

            if (string.IsNullOrWhiteSpace(strFilename))
                strFilename = GetFileNameHash(strUrl, wr != null ? wr.Post : null);
        
            FileInfo fi;

            this.CleanUp(false);

            //Sync of the same filenames

            string strCacheFullPath = this.CachePath + strFilename;

            lock (this._CacheRequests)
            {
                bool bInProgress = false;

                while (this._CacheRequests.Exists(p => p.Equals(strFilename)))
                {
                    _Logger.Debug("[{0}][DownloadFile] Wait: Url in the progress. '{1}'", this._Id, strUrl);

                    bInProgress = true;

                    //Wait, url is in the progress now
                    Monitor.Wait(this._CacheRequests);

                    //Now we can check the existing request again
                }

                if (bInProgress)
                {
                    int iAttempts = 5;
                    while (iAttempts-- > 0)
                    {
                        //Another task has finished the download. No need to download the file again.
                        if (File.Exists(strCacheFullPath))
                        {
                            _Logger.Debug("[{0}][DownloadFile] Abort: Url already processed. '{1}'", this._Id, strUrl);
                            return strCacheFullPath;
                        }

                        Thread.Sleep(200);
                    }
                    _Logger.Error("[{0}][DownloadFile] Abort: Url already processed but does not exist. '{1}'", this._Id, strUrl);
                    return null;

                }
                else
                    this._CacheRequests.Add(strFilename); //Add url to the progress list
            }

            try
            {
                //Load
                if (iLifeTime != 0 && File.Exists(strCacheFullPath))
                {
                    fi = new FileInfo(strCacheFullPath);
                    if (fi.Length > 0)
                    {
                        int iAge = (int)(DateTime.Now - fi.LastWriteTime).TotalMinutes;
                        if (fi.CreationTime > DateTime.Now || iAge < (iLifeTime > 0 ? iLifeTime : _CACHE_FILES_REFRESH))
                        {
                            int iAttempts = 3;
                            while (iAttempts-- > 0)
                            {
                                try
                                {
                                    fi.LastAccessTime = DateTime.Now;
                                    _Logger.Debug("[{0}][DownloadFile] Url request: '{1}' - Using cached file: '{2}'", this._Id, strUrl, strCacheFullPath);
                                    return strCacheFullPath;
                                }
                                catch (Exception ex) { }
                                Thread.Sleep(200);
                            }
                            _Logger.Error("[{0}][DownloadFile] File Access: '{1}'", this._Id, strCacheFullPath);
                            return strCacheFullPath;
                        }
                    }
                }

                if (wr == null)
                    wr = new HttpUserWebRequest(strUrl) { UseOpenSSL = this.UseOpenSSL, AllowSystemProxy = this.AllowSystemProxy };

                using (wr)
                {
                    if (wr.DownloadFile(strCacheFullPath, true))
                    {
                        
                        switch (wr.HttpResponseCode)
                        {
                            case HttpStatusCode.OK:
                            case HttpStatusCode.NotModified:

                                if (wr.HttpResponseCode == HttpStatusCode.OK && postDownload != null)
                                    postDownload(this, strCacheFullPath, postDownloadTag);

                                DateTime dtNow = DateTime.Now;
                                DateTime dtLastModified = dtNow;
                                if (wr.HttpResponseFields.TryGetValue("Last-Modified", out string strValue) && DateTime.TryParse(strValue, out DateTime dt) && dt > dtLastModified)
                                    dtLastModified = dt;

                                if (wr.HttpResponseFields.TryGetValue("Expires", out strValue) && DateTime.TryParse(strValue, out dt) && dt > dtNow)
                                    dtLastModified = dtNow.AddMinutes(Math.Min(_CACHE_FILES_LIFETIME, (dt - dtNow).TotalMinutes));

                                int iAttempts = 3;
                                while (iAttempts-- > 0)
                                {
                                    try
                                    {
                                        fi = new FileInfo(strCacheFullPath);

                                        if (dtLastModified > fi.CreationTime)
                                            fi.CreationTime = dtLastModified;

                                        fi.LastWriteTime = dtNow;
                                        fi.LastAccessTime = dtNow;
                                        return strCacheFullPath;
                                    }
                                    catch { }
                                    Thread.Sleep(200);
                                }
                                _Logger.Error("[{0}][DownloadFile] File Access: '{1}'", this._Id, strCacheFullPath);
                                return strCacheFullPath;

                            default:
                                return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[{3}][DownloadFile] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
            }
            finally
            {
                lock (this._CacheRequests)
                {
                    //Remove url from the progress list
                    this._CacheRequests.Remove(strFilename);

                    //Awake waiting threads
                    Monitor.PulseAll(this._CacheRequests);
                }
            }

            return null;
        }

        /// <summary>
        /// Download file based on url or webrequest
        /// </summary>
        /// <typeparam name="T">requested type</typeparam>
        /// <param name="strFilename">filename to be saved</param>
        /// <param name="strUrl">url of the file to be downloaded</param>
        /// <param name="iLifeTime">liftime of the cached file in minutes; 0: no caching; -1: default lifetime</param>
        /// <param name="wr">existing web request</param>
        /// <param name="encoding">text encoding; UTF8 default if null</param>
        /// <returns>requested type</returns>
        /// 
        public T DownloadFile<T>(string strUrl,
            string strFilename = null,
            int iLifeTime = -1,
            HttpUserWebRequest wr = null,
            Encoding encoding = null)
        {
            string strFile;
            return DownloadFile<T>(strUrl, out strFile,
                strFilename: strFilename,
                iLifeTime: iLifeTime,
                wr: wr, 
                encoding: encoding );
        }

        /// <summary>
        /// Download file based on url or webrequest
        /// </summary>
        /// <typeparam name="T">requested type</typeparam>
        /// <param name="strFilename">filename to be saved</param>
        /// <param name="strUrl">url of the file to be downloaded</param>
        /// <param name="iLifeTime">liftime of the cached file in minutes; 0: no caching; -1: default lifetime</param>
        /// <param name="wr">existing web request</param>
        /// <param name="strFile">output cached file fullpath</param>
        /// <param name="encoding">text encoding; UTF8 default if null</param>
        /// <returns>requested type</returns>
        /// 
        public T DownloadFile<T>(string strUrl, out string strFile,
            string strFilename = null,
            int iLifeTime = -1,
            HttpUserWebRequest wr = null,
            Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;

            //Download
            strFile = DownloadFile(strUrl,
                strFilename: strFilename,
                iLifeTime: iLifeTime, 
                wr: wr);

            if (string.IsNullOrWhiteSpace(strFile) || !File.Exists(strFile))
                return (T)(object)null;
            else if (typeof(T) == typeof(XmlDocument))
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(strFile);
                return (T)(object)xml;
            }
            else if (typeof(T) == typeof(HtmlDocument))
            {
                HtmlDocument html = new HtmlDocument();
                using (StreamReader rd = new StreamReader(strFile, encoding))
                {
                    html.LoadFromHtml(rd);
                }
                return (T)(object)html;
            }
            else if (typeof(T) == typeof(HtmlAgilityPack.HtmlDocument))
            {
                HtmlAgilityPack.HtmlDocument html = new HtmlAgilityPack.HtmlDocument();
                html.Load(strFile);
                return (T)(object)html;
            }
            else if (typeof(T) == typeof(System.Drawing.Image))
            {
                System.Drawing.Image img = System.Drawing.Image.FromFile(strFile);
                return (T)(object)img;
            }
            else if (typeof(T) == typeof(Newtonsoft.Json.Linq.JObject) || typeof(T) == typeof(Newtonsoft.Json.Linq.JToken))
                return (T)(object)Newtonsoft.Json.JsonConvert.DeserializeObject<T>(File.ReadAllText(strFile));
            else if (typeof(T) == typeof(string))
                return (T)(object)File.ReadAllText(strFile, encoding);
            else if (typeof(T) == typeof(byte[]))
                return (T)(object)File.ReadAllBytes(strFile);
            else
                return (T)(object)null;
        }

        public IAsyncResult BeginDownloadFile<T>(string strUrl, int iLifeTime, AsyncCallback asyncCallback, Object asyncState)
        {
            HttpUserWebRequest wr = new HttpUserWebRequest(strUrl);
            HttpUserWebRequestAsyncResult ia = new HttpUserWebRequestAsyncResult(wr, iLifeTime, asyncCallback, asyncState);

            Thread t = new Thread(new ParameterizedThreadStart((o) =>
            {
                HttpUserWebRequestAsyncResult a = (HttpUserWebRequestAsyncResult)o;
                try
                {
                    using (a.Request)
                    {
                        string strFile;
                        a.SetComplete(DownloadFile<T>(a.Request.Url, out strFile, GetFileNameHash(a.Request), a.LifeTime, a.Request, Encoding.UTF8), null);
                    }
                }
                catch (Exception ex)
                {
                    a.SetComplete(null, ex);
                }
            }));

            t.Start(ia);

            return ia;
        }

        public T EndDownloadFile<T>(IAsyncResult ia)
        {
            if (ia == null)
                throw new ArgumentException("IAsyncResult is null");

            return ia.IsCompleted && ia is HttpUserWebRequestAsyncResult ? (T)((HttpUserWebRequestAsyncResult)ia).Result : (T)(object)null;
        }


        /// <summary>
        /// Clear cached memory files
        /// </summary>
        public void Clear()
        {
            this._CachedList.Clear();
        }

        /// <summary>
        /// Get filename hash based on url
        /// </summary>
        /// <param name="strUrl"></param>
        /// <returns>Filename hash</returns>
        public static string GetFileNameHash(string strUrl)
        {
            if (string.IsNullOrWhiteSpace(strUrl))
                return string.Empty;
            return GetFileNameHash(new Uri(strUrl), null);
        }

        /// <summary>
        /// Get filename hash based on url
        /// </summary>
        /// <param name="strUrl"></param>
        /// <param name="data">Additional data to final hash. Can be null.</param>
        /// <returns>Filename hash</returns>
        public static string GetFileNameHash(string strUrl, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(strUrl))
                return string.Empty;
            return GetFileNameHash(new Uri(strUrl), data);
        }

        /// <summary>
        /// Get filename hash based on uri
        /// </summary>
        /// <param name="uri"></param>
        /// <returns>Filename hash</returns>
        public static string GetFileNameHash(Uri uri)
        {
            return GetFileNameHash(uri, null);
        }

        /// <summary>
        /// Get filename hash from http request
        /// </summary>
        /// <param name="wr"></param>
        /// <returns>Filename hash</returns>
        public static string GetFileNameHash(HttpUserWebRequest wr)
        {
            byte[] data = null;
            lock (_Md5Hash)
            {
                _SbHash.Clear();

                if (wr.HttpRequestFields != null && wr.HttpRequestFields.Count > 0)
                {
                    _SbHash.Append("FIELDS:");
                    foreach (string strKey in wr.HttpRequestFields)
                    {
                        _SbHash.Append(strKey);
                        _SbHash.Append('=');
                        _SbHash.Append(wr.HttpRequestFields[strKey]);
                        _SbHash.Append('&');
                    }
                }

                if (wr.Cookies != null)
                {
                    CookieCollection cookies = wr.Cookies.GetCookies(new Uri(wr.Url));
                    if (cookies.Count > 0)
                    {
                        _SbHash.Append("COOKIES:");
                        foreach (Cookie c in cookies)
                        {
                            _SbHash.Append(c.Name);
                            _SbHash.Append('=');
                            _SbHash.Append(c.Value);
                            _SbHash.Append('&');
                        }
                    }
                }

                if (_SbHash.Length > 0)
                {
                    byte[] d = Encoding.UTF8.GetBytes(_SbHash.ToString());
                    if (wr.Post != null && wr.Post.Length > 0)
                    {
                        data = new byte[d.Length + wr.Post.Length];
                        Buffer.BlockCopy(d, 0, data, 0, d.Length);
                        Buffer.BlockCopy(wr.Post, 0, data, d.Length, wr.Post.Length);
                    }
                    else
                        data = d;

                }
                else
                    data = wr.Post;
            }

            return GetFileNameHash(wr.Url, data);
        }

        /// <summary>
        /// Get filename hash based on uri
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data">Additional data to final hash. Can be null.</param>
        /// <returns>Filename hash</returns>
        public static string GetFileNameHash(Uri uri, byte[] data)
        {
            byte[] toHash = Encoding.ASCII.GetBytes(uri.AbsoluteUri);
            if (data != null && data.Length > 0)
            {
                //Concat uri & data
                byte[] tmp = new byte[toHash.Length + data.Length];
                Buffer.BlockCopy(toHash, 0, tmp, 0, toHash.Length);
                Buffer.BlockCopy(data, 0, tmp, toHash.Length, data.Length);
                toHash = tmp;
            }

            //Create hash string
            lock (_Md5Hash)
            {
                _SbHash.Clear();
                _SbHash.Append(uri.Host);
                _SbHash.Append('_');

                PrintHash(_SbHash, _Md5Hash.ComputeHash(toHash));

                //Append file extension
                _SbHash.Append('.');
                _SbHash.Append(CACHE_FILENAME_EXT);

                return _SbHash.ToString();
            }
        }

        /// <summary>
        /// Print hash data in hex format
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="hash"></param>
        public static void PrintHash(StringBuilder sb, byte[] hash)
        {
            //Print hash
            byte b;
            int iVal;
            for (int i = 0; i < hash.Length; i++)
            {
                b = hash[i];

                iVal = b >> 4;
                iVal += iVal >= 10 ? 87 : 48;
                sb.Append((char)iVal);

                iVal = b & 0x0F;
                iVal += iVal >= 10 ? 87 : 48;
                sb.Append((char)iVal);
            }
        }
    }
}
