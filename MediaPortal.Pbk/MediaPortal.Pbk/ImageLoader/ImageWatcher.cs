using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.Pbk.ImageLoader
{
    public class ImageWatcher
    {
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();
        private string _WatcherFilePath = null;
        //private FileSystemWatcher _Watcher = null;
        private ImageSwapper _ImageSwapper= null;
        private AsyncImageResource _ImageAsync = null;
        private ImageLoadHandler _ImageLoadHandler = null;

        static ImageWatcher()
        {
            Logging.Log.Init();
        }

        public ImageWatcher(ImageLoadHandler imgLoadHandler, ImageSwapper image)
        {
            this._ImageLoadHandler = imgLoadHandler;
            this._ImageSwapper = image;
        }
        public ImageWatcher(ImageLoadHandler imgLoadHandler, AsyncImageResource image)
        {
            this._ImageLoadHandler = imgLoadHandler;
            this._ImageAsync = image;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Init(string strFilePath, ImageLoadHandler imgLoadHandler)
        {
            this.Terminate();

            this._WatcherFilePath = strFilePath;
            //this._Watcher = new FileSystemWatcher(strFilePath.Substring(0, strFilePath.LastIndexOf('\\')));
            //this._Watcher.IncludeSubdirectories = false;
            //this._Watcher.Created += this.watcherCallback;
            //this._Watcher.EnableRaisingEvents = true;

            lock (this._ImageLoadHandler)
            {
                if(!this.check())
                    this._ImageLoadHandler.RegisterForCompleteEvent(strFilePath, this.imageLoaderCallback, null);
            }

            _Logger.Debug("[Init] " + strFilePath);

        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Terminate()
        {
            //if (this._Watcher != null)
            //{
            //    this._Watcher.EnableRaisingEvents = false;
            //    this._Watcher.Created -= this.watcherCallback;
            //    this._Watcher.Dispose();
            //    this._Watcher = null;

            //    _logger.Debug("[Terminate] " + this._WatcherFilePath);
            //}

            if (this._WatcherFilePath != null)
            {
                _Logger.Debug("[Terminate] " + this._WatcherFilePath);

                this._ImageLoadHandler.UnRegisterForCompleteEvent(this._WatcherFilePath, this.imageLoaderCallback);

                this._WatcherFilePath = null;
            }
        }

        //[MethodImpl(MethodImplOptions.Synchronized)]
        //private void watcherCallback(object sender, FileSystemEventArgs e)
        //{
        //    if (this._Watcher != null)
        //    {
        //        if (File.Exists(this._WatcherFilePath))
        //        {
        //            _logger.Debug("[watcherCallback] Ready: " + this._WatcherFilePath);

        //            this.Terminate();

        //            System.Threading.Thread.Sleep(500);

        //            if (this._ImageSwapper != null)
        //                this._ImageSwapper.Filename = this._WatcherFilePath;
        //            else
        //                this._ImageAsync.Filename = this._WatcherFilePath;

                    
        //        }
        //    }

        //}

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void imageLoaderCallback(object sender, EventArgs e)
        {
            if (this._WatcherFilePath != null && ((ImageLoadEventArgs)e).FilePath == this._WatcherFilePath)
                this.check();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool check()
        {
            if (File.Exists(this._WatcherFilePath))
            {
                _Logger.Debug("[check] Ready: " + this._WatcherFilePath);
                                
                //System.Threading.Thread.Sleep(500);

                if (this._ImageSwapper != null)
                    this._ImageSwapper.Filename = this._WatcherFilePath;
                else
                    this._ImageAsync.Filename = this._WatcherFilePath;

                this.Terminate();

                return true;
            }

            return false;
        }
    }
}
