using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Net;
using MediaPortal.GUI.Library;
using System.Runtime.CompilerServices;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using NLog;

namespace MediaPortal.Pbk.ImageLoader
{
    public class ImageLoadHandler
    {
        #region Constants
        private const int _TIME_MIN_WAIT = 500;

        private const int _LIFETIME_IMAGES_DEFAULT = 60 * 24 * 7; //[min]

        private const int _THREADS_MAX = 5;

        private const int _WIDTH_MAX_IMAGE_DEFAULT = 1920;
        private const int _WIDTH_MAX_POSTER_DEFAULT = 400;
        private const int _WIDTH_MAX_ICON_DEFAULT = 50;
        #endregion

        #region Types
        private class JobGuiItem
        {
            public GUI.GUIItem GuiItem;
            public bool IsCover = false;
        }

        private class Job
        {
            public string Url;
            public string FileName;
            public bool InProgress = false;
            public List<JobGuiItem> GuiItems = new List<JobGuiItem>();
            public bool IsPoster = false;
        }

        private class CallbackJob
        {
            public ImageLoadEventHandler Callback;
            public string FilePath;
            public object Tag;
        }
        #endregion

        #region Private fileds
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private List<CallbackJob> _Callbacks = new List<CallbackJob>();

        private Net.Http.Caching _Caching;

        private int _Id = -1;
        private static int _IdCounter = -1;

        private ManualResetEvent _FlagComplete = new ManualResetEvent(false);

        private Tasks.TaskQueue _JobPool;
        #endregion

        #region Events

        #endregion

        #region Public fields
        public static ImageLoadHandler Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new ImageLoadHandler(Pbk.Net.Http.Caching.Instance);
                    Pbk.Net.Http.Caching.Instance.Delete += _Instance.OnDeleteCachingFile;
                }

                return _Instance;
            }
        }private static ImageLoadHandler _Instance = null;

        public int CoverIconMaxWidth
        {
            get { return this._IconMaxWidth; }
            set
            {
                if (value < 10)
                    this._IconMaxWidth = 10;
                else
                    this._IconMaxWidth = value;
            }
        }private int _IconMaxWidth = _WIDTH_MAX_ICON_DEFAULT;

        public int CoverPosterMaxWidth
        {
            get { return this._PosterMaxWidth; }
            set
            {
                if (value < 10)
                    this._PosterMaxWidth = 10;
                else
                    this._PosterMaxWidth = value;
            }
        }private int _PosterMaxWidth = _WIDTH_MAX_POSTER_DEFAULT;

        public int ImageMaxWidth
        {
            get { return this._ImageMaxWidth; }
            set
            {
                if (value < 10)
                    this._ImageMaxWidth = 10;
                else
                    this._ImageMaxWidth = value;
            }
        }private int _ImageMaxWidth = _WIDTH_MAX_IMAGE_DEFAULT;

        /// <summary>
        /// Enable or disable queue. Default = true.
        /// </summary>
        public bool Run
        {
            set
            {
                this._JobPool.Run = true;
            }
        }

        /// <summary>
        /// Maxium concurrent threads. Default = 5
        /// </summary>
        public int MaxConcurrentThreads
        {
            get
            {
                return this._JobPool.MaxConcurrentThreads;
            }

            set
            {
                this._JobPool.MaxConcurrentThreads = value;
            }
        }
        #endregion

        #region ctor
        static ImageLoadHandler()
        {
            Logging.Log.Init();
        }

        public ImageLoadHandler(Net.Http.Caching caching)
            : this(caching, ThreadPriority.Normal)
        {
        }
        public ImageLoadHandler(Net.Http.Caching caching, ThreadPriority threadPriority)
        {
            this._JobPool = new Tasks.TaskQueue("ImageLoadHandler", null, null, threadPriority);
            this._Id = Interlocked.Increment(ref _IdCounter);
            this._Caching = caching;
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Add new download task.
        /// </summary>
        /// <param name="strUrl">Url of the file to be downloaded</param>
        /// <param name="guiItem"></param>
        /// <param name="bIsCover"></param>
        /// <returns></returns>
        public string AddNewTask(string strUrl, GUI.GUIItem guiItem, bool bIsCover)
        {
            return this.AddNewTask(strUrl, guiItem, bIsCover, null, null);
        }

        /// <summary>
        /// Add new download task.
        /// </summary>
        /// <param name="strUrl">Url of the file to be downloaded</param>
        /// <param name="guiItem"></param>
        /// <param name="bIsCover"></param>
        /// <param name="callback">Optional callback to be executed upon task completation</param>
        /// <param name="callbackTag">Optional user tag passed to the callback</param>
        /// <returns></returns>
        public string AddNewTask(string strUrl, GUI.GUIItem guiItem, bool bIsCover, ImageLoadEventHandler callback, object callbackTag)
        {
            if (string.IsNullOrWhiteSpace(strUrl))
            {
                _Logger.Warn("[{0}][AddNewTask] Invalid url.", this._Id);
                return null;
            }

            string strFilename = Net.Http.Caching.GetFileNameHash(strUrl);

            if (callback != null)
                this.RegisterForCompleteEvent(this._Caching.CachePath + strFilename, callback, callbackTag);

            this._JobPool.Find(
                (j) => ((Job)j).Url.Equals(strUrl), //check for existing job
                (j) => //callback after check
                    {
                        if (j == null)
                        {
                            //New
                            j = new Job()
                            {
                                Url = strUrl,
                                FileName = strFilename,
                                InProgress = true,
                                IsPoster = bIsCover
                            };
                            ((Job)j).GuiItems.Add(new JobGuiItem() { GuiItem = guiItem, IsCover = bIsCover });

                            this._JobPool.Add((o, state) => this.jobProcess((Job)o), j, strName: strUrl);
                        }
                        else
                        {
                            //Existing
                            _Logger.Debug("[{0}][AddNewTask] Url already exists: {1}", this._Id, strUrl);
                            ((Job)j).GuiItems.Add(new JobGuiItem() { GuiItem = guiItem, IsCover = bIsCover });
                            if (bIsCover)
                                ((Job)j).IsPoster = true;
                        }
                    }
            );

            return strFilename;
        }

        /// <summary>
        /// Register for task done event
        /// </summary>
        /// <param name="strFilePath">Cached filename fullpath</param>
        /// <param name="callback">Callback to be executed upon task completation</param>
        /// <param name="callbackTag">Optional user tag passed to the callback</param>
        public void RegisterForCompleteEvent(string strFilePath, ImageLoadEventHandler callback, object callbackTag)
        {
            lock (this._Callbacks)
            {
                this._Callbacks.Add(new CallbackJob() { FilePath = strFilePath, Callback = callback, Tag = callbackTag });
            }
        }

        /// <summary>
        /// Unregister from task done event
        /// </summary>
        /// <param name="strFilePath">Cached filename fullpath</param>
        /// <param name="callback">Callback to be executed upon task completation</param>
        public void UnRegisterForCompleteEvent(string strFilePath, ImageLoadEventHandler callback)
        {
            lock (this._Callbacks)
            {
                CallbackJob c = this._Callbacks.Find(p => p.Callback == callback && p.FilePath == strFilePath);

                if (c != null)
                    this._Callbacks.Remove(c);
            }
        }

        /// <summary>
        /// Remove cached icon file upon deleting cached file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="strFilepath">Filepath of the deleted cached file</param>
        public void OnDeleteCachingFile(object sender, string strFilepath)
        {
            string str = strFilepath + ".icon";
            if (System.IO.File.Exists(str))
            {
                _Logger.Debug("[{0}][OnDeleteCachingFile] Deleting file: {1}", this._Id, str);
                try { System.IO.File.Delete(str); }
                catch (Exception ex)
                { _Logger.Error("[{3}][OnDeleteCachingFile] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id); }
            }
        }

        /// <summary>
        /// Wait for completation of all tasks
        /// </summary>
        /// <param name="iTime">The number of milliseconds to wait, or System.Threading.Timeout. Infinite (-1) to wait indefinitely.</param>
        public void WaitForAll(int iTime)
        {
            this._JobPool.WaitForAll(iTime);
        }
        #endregion

        #region Private methods
        private object cbCacheFileDownloaded(object sender, object dataIn, object tag)
        {
            if (dataIn != null)
            {
                int iWidth = -1;
                int iAttempts = 3;
                while (iAttempts-- > 0)
                {
                    Image im = null;
                    Image imNew = null;
                    Image imIcon = null;

                    try
                    {
                        if (((Job)tag).IsPoster) //icon
                        {
                            //Create image from downloaded file
                            im = Image.FromFile((string)dataIn);

                            //Create gui icon image
                            imIcon = new Bitmap(im, new Size(this._IconMaxWidth, (int)((float)im.Height / im.Width * this._IconMaxWidth)));
                            imIcon.Save(((string)dataIn) + ".icon", System.Drawing.Imaging.ImageFormat.Jpeg);
                            imIcon.Dispose();
                            imIcon = null;

                            if (im.Width > this._PosterMaxWidth)
                            {
                                _Logger.Debug("[{2}][cbCacheFileDownloaded] Poster resolution reducing from w{0} to w{1}", im.Width, this._PosterMaxWidth, this._Id);

                                //Reduce resolution and overwrite downloaded file
                                imNew = new Bitmap(im, new Size(this._PosterMaxWidth, (int)((float)im.Height / im.Width * this._PosterMaxWidth)));
                                im.Dispose();
                                im = null;
                                imNew.Save((string)dataIn, System.Drawing.Imaging.ImageFormat.Jpeg);
                                imNew.Dispose();
                                imNew = null;
                            }
                        }
                        else
                        {
                            //Extract width
                            if (iWidth < 0)
                            {
                                IEnumerable<MetadataExtractor.Directory> directories = MetadataExtractor.ImageMetadataReader.ReadMetadata((string)dataIn);
                                foreach (MetadataExtractor.Directory directory in directories)
                                {
                                    if (directory is MetadataExtractor.Formats.Jpeg.JpegDirectory
                                        || directory is MetadataExtractor.Formats.Png.PngDirectory
                                        || directory is MetadataExtractor.Formats.Bmp.BmpHeaderDirectory
                                        || directory is MetadataExtractor.Formats.Gif.GifHeaderDirectory)
                                    {
                                        Tag mTag = directory.Tags.Where((x) => x.Name == "Image Width").FirstOrDefault();
                                        if (mTag != null)
                                        {
                                            iWidth = directory.GetInt32(mTag.Type);
                                            if (iWidth > 1)
                                                break;
                                            else
                                                iWidth = 0;
                                        }
                                    }
                                }
                            }

                            if (iWidth > this._ImageMaxWidth)
                            {
                                _Logger.Debug("[{2}][cbCacheFileDownloaded] Image resolution reducing from w{0} to w{1}", iWidth, this._ImageMaxWidth, this._Id);

                                //Create image from downloaded file
                                im = Image.FromFile((string)dataIn);

                                //Reduce resolution and overwrite downloaded file
                                imNew = new Bitmap(im, new Size(this._ImageMaxWidth, (int)((float)im.Height / im.Width * this._ImageMaxWidth)));
                                im.Dispose();
                                im = null;
                                imNew.Save((string)dataIn, System.Drawing.Imaging.ImageFormat.Jpeg);
                                imNew.Dispose();
                                imNew = null;
                            }

                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[{3}][cbCacheFileDownloaded] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id);
                    }
                    finally
                    {
                        if (im != null)
                        {
                            im.Dispose();
                            im = null;
                        }

                        if (imNew != null)
                        {
                            imNew.Dispose();
                            imNew = null;
                        }

                        if (imIcon != null)
                        {
                            imIcon.Dispose();
                            imIcon = null;
                        }
                    }

                    Thread.Sleep(1000);
                }

                //Callback
                this.jobOnCallback((string)dataIn, (Job)tag, true);
            }

            return null;
        }

        private void jobOnCallback(string strFilePath, Job job, bool bDownloaded)
        {
            //Callback
            lock (this._Callbacks)
            {
                int i = 0;
                while (i < this._Callbacks.Count)
                {
                    CallbackJob callback = this._Callbacks[i];

                    if (callback.FilePath == strFilePath)
                    {
                        try
                        {
                            callback.Callback(this, new ImageLoadEventArgs()
                            {
                                Url = job.Url,
                                FilePath = strFilePath,
                                Tag = callback.Tag,
                                DownloadComplete = bDownloaded

                            });
                        }
                        catch (Exception ex)
                        { _Logger.Error("[jobOnCallback] Error: {0}", ex.Message); }

                        this._Callbacks.Remove(callback);
                        continue;
                    }

                    i++;
                }
            }
        }

        private Tasks.TaskActionResultEnum jobProcess(Job j)
        {
            string strPath = null;
            try
            {
                strPath = this._Caching.DownloadFile(j.Url,
                    strFilename: j.FileName,
                    iLifeTime: _LIFETIME_IMAGES_DEFAULT,
                    postDownload: this.cbCacheFileDownloaded,
                    postDownloadTag: j);
            }
            catch (Exception ex) { _Logger.Error("[{3}][jobProcess] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, this._Id); }

            for (int i = 0; i < j.GuiItems.Count; i++)
            {
                JobGuiItem jPost = j.GuiItems[i];
                if (jPost.GuiItem != null)
                {
                    if (jPost.IsCover)
                    {
                        //Cover
                        jPost.GuiItem.Cover = strPath;
                    }
                    else
                        //Backdrop
                        jPost.GuiItem.Backdrop = strPath;
                }
            }

            //Callback
            this.jobOnCallback(strPath, j, false);

            return Tasks.TaskActionResultEnum.Complete;
        }
        #endregion
    }
}
