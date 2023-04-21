using NLog;
using System;
using System.Threading;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Reflection;
using MediaPortal.GUI.Library;
using System.IO;

namespace MediaPortal.Pbk.ImageLoader
{
    public delegate void AsyncImageLoadComplete(AsyncImageResource image);

    public class AsyncImageResource
    {
        [DllImport("gdiplus.dll", CharSet = CharSet.Unicode)]
        private static extern int GdipLoadImageFromFile(string filename, out IntPtr image);

        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        private static Object _LoadingLock = new Object();
        private int _PendingToken = 0;
        private int _ThreadsWaiting = 0;
        private bool _Warned = false;

        static AsyncImageResource()
        {
            Logging.Log.Init();
        }


        /// <summary>
        /// This event is triggered when a new image file has been successfully loaded
        /// into memory.
        /// </summary>
        public event AsyncImageLoadComplete ImageLoadingComplete;

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
                if (this._Active == value)
                    return;

                this._Active = value;

                Thread newThread = new Thread(new ThreadStart(activeWorker));
                newThread.IsBackground = true;
                newThread.Name = "AsyncImageResource.activeWorker";
                newThread.Start();
            }
        }private bool _Active = true;

        /// <summary>
        /// If multiple changes to the Filename property are made in rapid succession, this delay
        /// will be used to prevent unecessary loading operations. Most useful for large images that
        /// take a non-trivial amount of time to load from memory.
        /// </summary>
        public int Delay
        {
            get { return this._Delay; }
            set { this._Delay = value; }
        } private int _Delay = 250;

        /// <summary>
        /// The identifier used by the MediaPortal GUITextureManager to identify this resource.
        /// This changes when a new file has been assigned, if you need to know when this changes
        /// use the this.ImageLoadingComplete event.
        /// </summary>
        public string Identifier
        {
            get { return this._Identifier; }
        }private string _Identifier = null;
        
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
                if (value == null)
                    value = " ";

                Thread newThread = new Thread(new ParameterizedThreadStart(this.setFilenameWorker));
                newThread.IsBackground = true;
                newThread.Name = "AsyncImageResource.setFilenameWorker";
                newThread.Start(value);
            }
        }private string _Filename = null;

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
        }private string _Property = null;


        private void activeWorker()
        {
            lock (_LoadingLock)
            {
                if (this._Active)
                {
                    // load the resource
                    this._Identifier = this.loadResourceSafe(this._Filename);

                    // notify any listeners a resource has been loaded
                    if (this.ImageLoadingComplete != null)
                        this.ImageLoadingComplete(this);
                }
                else
                {
                    this.unloadResource(this._Filename);
                    this._Identifier = null;
                }
            }
        }

        private void writeProperty()
        {
            if (this._Active && this._Property != null && this._Identifier != null)
                GUIPropertyManager.SetProperty(this._Property, this._Identifier);
            else
                if (this._Property != null)
                    GUIPropertyManager.SetProperty(this._Property, "-");
        }

        // Unloads the previous file and sets a new filename. 
        private void setFilenameWorker(object newFilenameObj)
        {
            int iLocalToken = ++this._PendingToken;
            string strOldFilename = this._Filename;

            // check if another thread has locked for loading
            bool loading = Monitor.TryEnter(_LoadingLock);
            if (loading) Monitor.Exit(_LoadingLock);

            // if a loading action is in progress or another thread is waiting, we wait too
            if (loading || this._ThreadsWaiting > 0)
            {
                this._ThreadsWaiting++;
                for (int i = 0; i < 5; i++)
                {
                    Thread.Sleep(this._Delay / 5);
                    if (iLocalToken < this._PendingToken)
                        return;
                }
                this._ThreadsWaiting--;
            }

            lock (_LoadingLock)
            {
                if (iLocalToken < this._PendingToken)
                    return;

                // type cast and clean our filename
                string newFilename = (string)newFilenameObj;
                if (newFilename != null && newFilename.Trim().Length == 0)
                    newFilename = null;
                else if (newFilename != null)
                    newFilename = newFilename.Trim();

                // if we are not active we should nto be assigning a filename
                if (!Active) newFilename = null;

                // if there is no change, quit
                if (this._Filename != null && this._Filename.Equals(newFilename))
                {
                    if (this.ImageLoadingComplete != null)
                        this.ImageLoadingComplete(this);

                    return;
                }

                string newIdentifier = this.loadResourceSafe(newFilename);

                // check if we have a new loading action pending, if so just quit
                if (iLocalToken < this._PendingToken)
                {
                    this.unloadResource(newIdentifier);
                    return;
                }

                // update MediaPortal about the image change
                this._Identifier = newIdentifier;
                this._Filename = newFilename;
                this.writeProperty();

                // notify any listeners a resource has been loaded
                if (this.ImageLoadingComplete != null)
                    this.ImageLoadingComplete(this);
            }

            // wait a few seconds in case we want to quickly reload the previous resource
            // if it's not reassigned, unload from memory.
            Thread.Sleep(5000);
            lock (_LoadingLock)
            {
                if (this._Filename != strOldFilename)
                    this.unloadResource(strOldFilename);
            }
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
                _Logger.Error("MediaPortal failed to load artwork: " + strFilename);
            }

            return false;
        }

        private string loadResourceSafe(string strFilename)
        {
            if (strFilename == null || strFilename.Trim().Length == 0)
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
                    _Logger.Warn("Cannot preform asynchronous loading with this version of MediaPortal. Please upgrade for improved performance.");
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
            return "[Cornerstone:" + strFilename.GetHashCode() + "]";
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
                    _Logger.Warn("gdiplus.dll method failed. Will degrade performance.");
                    image = Image.FromFile(strFilename);
                }

                else
                    image = (Image)typeof(Bitmap).InvokeMember("FromGDIplus", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, new object[] { imagePtr });
            }
            catch (Exception)
            {
                _Logger.Error("Failed to load image from " + strFilename);
                image = null;
            }

            return image;

        }

    }
}
