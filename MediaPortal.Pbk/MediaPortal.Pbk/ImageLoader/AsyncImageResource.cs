using NLog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Reflection;
using MediaPortal.GUI.Library;
using System.IO;
using System.Runtime.CompilerServices;

namespace MediaPortal.Pbk.ImageLoader
{
    public delegate void AsyncImageLoadComplete(AsyncImageResource image);

    public class AsyncImageResource
    {
        #region Native
        [DllImport("gdiplus.dll", CharSet = CharSet.Unicode)]
        private static extern int GdipLoadImageFromFile(string filename, out IntPtr image);
        #endregion

        #region Private fields
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        private static Object _LoadingLock = new Object();
        private volatile bool _IsPendingRequest = false;
        private string _FileNameRequest = null;
        private ManualResetEvent _FlagWakeUp = new ManualResetEvent(false);
        private bool _Warned = false;
        private static int _IdCounter = -1;
        private int _Id = -1;
        private bool _Active = false;
        private int _Delay = 250;
        private string _Identifier = null;
        private string _Filename = null;
        private string _Property = null;
        #endregion

        #region Public properties

        /// <summary>
        /// True if this resources will actively load into memory when assigned a file.
        /// </summary>
        public bool Active
        {
            get
            {
                return this._Active;
            }

            set
            {
                lock (_LoadingLock)
                {
                    if (this._Active == value)
                        return;

                    this._Active = value;

                    this._FlagWakeUp.Set();

                    if (value)
                    {
                        Thread newThread = new Thread(new ThreadStart(this.process));
                        newThread.IsBackground = true;
                        newThread.Name = "AsyncImageResource.process.[" + this._Id + ']';
                        newThread.Start();
                    }
                }
            }
        }

        /// <summary>
        /// If multiple changes to the Filename property are made in rapid succession, this delay
        /// will be used to prevent unecessary loading operations. Most useful for large images that
        /// take a non-trivial amount of time to load from memory.
        /// </summary>
        public int Delay
        {
            get { return this._Delay; }
            set
            {
                if (value < 1)
                    value = 1;

                this._Delay = value;
            }
        }

        /// <summary>
        /// The identifier used by the MediaPortal GUITextureManager to identify this resource.
        /// This changes when a new file has been assigned, if you need to know when this changes
        /// use the this.ImageLoadingComplete event.
        /// </summary>
        public string Identifier
        {
            get { return this._Identifier; }
        }

        /// <summary>
        /// The filename of the image backing this resource. Reassign to change textures.
        /// </summary>
        public string Filename
        {
            get
            {
                return this._Filename;
            }

            set
            {
                lock (_LoadingLock)
                {
                    if (!this.Active)
                        this.Active = true;

                    this._IsPendingRequest = true;
                    this._FileNameRequest = value;
                    this._FlagWakeUp.Set();
                }
            }
        }

        /// <summary>
        /// This MediaPortal property will automatically be set with the renderable identifier
        /// once the resource has been loaded. Appropriate for a texture field of a GUIImage 
        /// control.
        /// </summary>
        public string Property
        {
            get { return this._Property; }
            set
            {
                this._Property = value;

                this.writeProperty();
            }
        }

        #endregion

        #region Events
        /// <summary>
        /// This event is triggered when a new image file has been successfully loaded
        /// into memory.
        /// </summary>
        public event AsyncImageLoadComplete ImageLoadingComplete;
        #endregion

        #region ctor
        static AsyncImageResource()
        {
            Logging.Log.Init();
        }

        public AsyncImageResource()
            : this(false)
        {
        }

        public AsyncImageResource(bool bActive)
        {
            this._Id = Interlocked.Increment(ref _IdCounter);
            this.Active = bActive;
        }

        #endregion

        #region private methods

        private void writeProperty()
        {
            if (this._Active && this._Property != null && this._Identifier != null)
                GUIPropertyManager.SetProperty(this._Property, this._Identifier);
            else
                if (this._Property != null)
                GUIPropertyManager.SetProperty(this._Property, "-");
        }

        private void process()
        {
            const int _CLEAN_PERIOD = 1000;
            bool bActive = false;
            string strNewFilename = null;
            string strNewIdentifier;
            int iWait;
            Dictionary<string, DateTime> unloadList = new Dictionary<string, DateTime>();
            this._FlagWakeUp.Set();
            try
            {
                while (true)
                {
                    //Set wake-up time
                    if (unloadList.Count > 0)
                        iWait = _CLEAN_PERIOD;
                    else
                    {
                        _Logger.Debug("[{0}][process] Sleep...", this._Id);
                        iWait = -1;
                    }

                    //Wait for an event
                    this._FlagWakeUp.WaitOne(iWait);

                    //Handle resources no longer needed
                    if (unloadList.Count > 0)
                    {
                        DateTime dtNow = DateTime.Now;
                        string[] keys = new string[unloadList.Keys.Count];
                        unloadList.Keys.CopyTo(keys, 0);
                        foreach (string strKey in keys)
                        {
                            if (!this.Active || (dtNow - unloadList[strKey]).TotalMilliseconds >= 5000)
                            {
                                this.unloadResource(strKey);
                                unloadList.Remove(strKey);
                                _Logger.Debug("[{0}][process] Clean: {1}", this._Id, strKey);
                            }
                        }
                    }

                    lock (_LoadingLock)
                    {
                        //Reset wake-up flag
                        this._FlagWakeUp.Reset();

                        //Check for 'active' change
                        if (this._Active != bActive)
                        {
                            bActive = this._Active;

                            _Logger.Debug("[{0}][process] Active:{1}", this._Id, bActive);

                            if (!this._Active)
                                break;
                            else
                            {
                                //Reload current file
                                this.loadResource(this._Filename);

                                if (this._IsPendingRequest)
                                    this._FlagWakeUp.Set();

                                goto notify;
                            }
                        }

                        //Check pending request
                        if (this._IsPendingRequest)
                        {
                            //Reset pending request
                            this._IsPendingRequest = false;

                            //Validate new filename
                            strNewFilename = this._FileNameRequest != null ? this._FileNameRequest.Trim() : string.Empty;
                            if (strNewFilename.Length == 0)
                                strNewFilename = null;

                            //No change
                            if (this._Filename == null && strNewFilename == null)
                                continue;

                            //No change
                            if (this._Filename != null && this._Filename.Equals(strNewFilename))
                                continue;
                        }
                        else
                            continue;
                    }

                    //Load new image
                    strNewIdentifier = this.loadResourceSafe(strNewFilename);

                    //Check for pending request now
                    if (this._IsPendingRequest)
                    {
                        unloadList[strNewIdentifier] = DateTime.Now;
                        this._FlagWakeUp.Set();
                        continue;
                    }

                    //Put currrent image to unload list
                    if (this._Filename != null)
                        unloadList[this._Filename] = DateTime.Now;

                    //Update MediaPortal about the image change
                    this._Identifier = strNewIdentifier;
                    this._Filename = strNewFilename;
                    this._FileNameRequest = null;
                    _Logger.Debug("[{0}][process] Change: {1}", this._Id, strNewIdentifier);

                notify:
                    this.writeProperty();
                                    
                    //Notify any listeners a resource has been loaded
                    if (bActive && this.ImageLoadingComplete != null)
                    {
                        try
                        {
                            this.ImageLoadingComplete(this);
                        }
                        catch { }
                    }

                    //Wait after change
                    Thread.Sleep(this._Delay);
                }
            }
            finally
            {

            }

            //Clean
            this.unloadResource(this._Filename);
            if (unloadList.Count > 0)
            {
                foreach (string strKey in unloadList.Keys)
                    this.unloadResource(strKey);
            }

            _Logger.Debug("[{0}][process] Terminated", this._Id);
        }

        /// <summary>
        /// Loads the given file into memory and registers it with MediaPortal.
        /// </summary>
        /// <param name="strFilename">The image file to be loaded.</param>
        private bool loadResource(string strFilename)
        {
            if (!this._Active || strFilename == null || !File.Exists(strFilename))
                return false;

            try
            {
                if (GUITextureManager.Load(strFilename, 0, 0, 0, true) > 0)
                    return true;
            }
            catch (Exception)
            {
                _Logger.Error("[{0}][loadResource] MediaPortal failed to load artwork: {1}", this._Id, strFilename);
            }

            return false;
        }

        private string loadResourceSafe(string strFilename)
        {
            if (strFilename == null)
                return null;

            // try to load with new persistent load feature
            try
            {
                if (this.loadResource(strFilename))
                    return strFilename;
            }
            catch (MissingMethodException)
            {
                if (!this._Warned)
                {
                    _Logger.Warn("[{0}][loadResourceSafe] Cannot preform asynchronous loading with this version of MediaPortal. Please upgrade for improved performance.", this._Id);
                    this._Warned = true;
                }
            }

            // if not available load image ourselves and pass to MediaPortal. Much slower but this still
            // gives us asynchronous loading. 
            Image image = loadImageFastFromFile(strFilename);
            if (GUITextureManager.LoadFromMemory(image, this.getIdentifier(strFilename), 0, 0, 0) > 0)
            {
                return this.getIdentifier(strFilename);
            }

            return null;
        }

        private string getIdentifier(string strFilename)
        {
            return "[MediaPortal.Pbk.ImageLoader:" + strFilename.GetHashCode() + "]";
        }

        /// <summary>
        /// If previously loaded, unloads the resource from memory and removes it 
        /// from the MediaPortal GUITextureManager.
        /// </summary>
        private void unloadResource(string strFilename)
        {
            if (strFilename == null)
                return;

            // double duty since we dont know if we loaded via new fast way or old
            // slow way
            GUITextureManager.ReleaseTexture(this.getIdentifier(strFilename));
            GUITextureManager.ReleaseTexture(strFilename);
        }

        // Loads an Image from a File by invoking GDI Plus instead of using build-in 
        // .NET methods, or falls back to Image.FromFile. GDI Plus should be faster.
        private static Image loadImageFastFromFile(string strFilename)
        {
            IntPtr imagePtr = IntPtr.Zero;
            Image image = null;

            try
            {
                if (GdipLoadImageFromFile(strFilename, out imagePtr) != 0)
                {
                    _Logger.Warn("[loadImageFastFromFile] gdiplus.dll method failed. Will degrade performance.");
                    image = Image.FromFile(strFilename);
                }

                else
                    image = (Image)typeof(Bitmap).InvokeMember("FromGDIplus", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new object[] { imagePtr });
            }
            catch (Exception)
            {
                _Logger.Error("[loadImageFastFromFile] Failed to load image from " + strFilename);
                image = null;
            }

            return image;

        }

        #endregion
    }
}
