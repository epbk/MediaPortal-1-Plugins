using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MediaPortal.GUI.Library;
using MediaPortal.Configuration;
using System.Runtime.CompilerServices;
#if MS_DIRECTX
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
#else
using SharpDX.Direct3D9;
#endif

namespace MediaPortal.Plugins.WorldWeatherLite.GUI
{
    public class GUIWeatherImage : GUIListItem
    {
        private static string _EmptyImage = Config.GetFolder(Config.Dir.Skin) + '\\' + Config.SkinName + "\\Media\\black.png";

        private static Regex _RegexTag = new Regex("\\{((yyyy)|(MM)|(dd)|(hh)|(mm))\\}", RegexOptions.Compiled);

        private System.Timers.Timer _TimerAnimation = null;
        private int _CurrentFrameIdx = 0;
        private int _RefreshActive = 0;
        private int _FramesCount = 0;

        public System.Drawing.Size ImageSize
        {
            get { return this._ImageSize; }
        }private System.Drawing.Size _ImageSize;

        public System.Drawing.RectangleF FullscreenRectangle
        {
            get { return this._FullscreenRectangle; }
        }private System.Drawing.RectangleF _FullscreenRectangle;
        
        public string Id
        { get; private set; }

        public string GuiTag
        {
            get
            {
                return this._GuiTag;
            }
            set
            {
                this._GuiTag = value;

            }
        }private string _GuiTag = null;

        public DateTime LastRefresh = DateTime.MinValue;

        public bool FramesAvailable
        { get { return _FramesCount > 0; } }

        public bool Active
        { get; private set; }

        public bool Terminating
        { get; private set; }

        public int OrderId;


        /// <summary>
        /// Gets current image frame to be visible
        /// </summary>
        public Texture CurrentTexture
        {
            get
            {
                return this._CurrentTexture;
            }
        }private Texture _CurrentTexture = null;

        /// <summary>
        /// Gets weather image properties
        /// </summary>
        public Database.dbWeatherImage WeatherImage
        {
            get;
            private set;
        }

        #region ctor
        public GUIWeatherImage(Database.dbWeatherImage wi, string strId, string strGuiTag)
            : base(wi.Description)
        {
            this.WeatherImage = wi;
            this.Id = strId;
            this._GuiTag = strGuiTag;

            this.ThumbnailImage = _EmptyImage;
            GUIPropertyManager.SetProperty(this._GuiTag, _EmptyImage);
        }
        #endregion


        /// <summary>
        /// Destroy the image and release all textures
        /// </summary>
        /// <param name="bDeleteFiles">True to delete temporary download files.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Destroy()
        {
            GUIPropertyManager.SetProperty(this.GuiTag, string.Empty);

            if (this._TimerAnimation != null)
            {
                this._TimerAnimation.Stop();
                this._TimerAnimation.Elapsed -= this.cbTimerAnimation;
                this._TimerAnimation.Dispose();
                this._TimerAnimation = null;
            }

            this._FramesCount = 0;
            this._CurrentTexture = null;
            this._CurrentFrameIdx = 0;

            GUITextureManager.ReleaseTexture(this.Id);

            this.LastRefresh = DateTime.MinValue;
        }

        /// <summary>
        /// Get formated url by datetime
        /// </summary>
        /// <param name="dt">Datetime to be used in formating</param>
        /// <returns>Formated url</returns>
        public string FormatUrl(DateTime dt)
        {
            MatchCollection tags = _RegexTag.Matches(this.WeatherImage.Url);
            if (tags.Count == 0)
                return this.WeatherImage.Url;

            StringBuilder sb = new StringBuilder(this.WeatherImage.Url.Length);

            int iIdx = 0;
            for (int i = 0; i < tags.Count; i++)
            {
                Match tag = tags[i];

                //Text before the tag
                if (tag.Index > iIdx)
                    sb.Append(this.WeatherImage.Url, iIdx, tag.Index - iIdx);

                switch (tag.Value)
                {
                    case "{yyyy}":
                        sb.Append(dt.Year);
                        break;

                    case "{MM}":
                        sb.Append((char)('0' + (dt.Month / 10)));
                        sb.Append((char)('0' + (dt.Month % 10)));
                        break;

                    case "{dd}":
                        sb.Append((char)('0' + (dt.Day / 10)));
                        sb.Append((char)('0' + (dt.Day % 10)));
                        break;

                    case "{hh}":
                        sb.Append((char)('0' + (dt.Hour / 10)));
                        sb.Append((char)('0' + (dt.Hour % 10)));
                        break;

                    case "{mm}":
                        int iMin = (int)(dt.Minute / this.WeatherImage.Period) * this.WeatherImage.Period;
                        sb.Append((char)('0' + (iMin / 10)));
                        sb.Append((char)('0' + (iMin % 10)));

                        break;
                }

                //Advance position
                iIdx = tag.Index + tag.Length;
            }

            //Rest
            if (iIdx < this.WeatherImage.Url.Length)
                sb.Append(this.WeatherImage.Url, iIdx, this.WeatherImage.Url.Length - iIdx);

            return sb.ToString();
        }

        /// <summary>
        /// Set new image
        /// </summary>
        /// <param name="images">images to set</param>
        /// <param name="durations">duration of each image in ms</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetImage(System.Drawing.Image[] images, int[] durations)
        {
            try
            {
                //If we are in terminatig state, exit
                if (this.Terminating)
                    return;

                lock (GUIGraphicsContext.RenderLock)
                {
                    //Destroy current image if exists
                    this.Destroy();

                    if (GUIWindowManager.ActiveWindow != GUIWorldWeaterLite.PLUGIN_ID)
                        return; //If we aren't active then destroy images and return

                    if (this.Active)
                        GUIPropertyManager.SetProperty(this.GuiTag, string.Empty);

                    if (images != null && images.Length > 0)
                    {
                        //Create multiframe cached texture
                        if (GUITextureManager.LoadFromMemoryEx(images, durations, this.Id, 0) != images.Length)
                        {
                            this._FramesCount = 0;
                            this.SetEmptyImage();
                        }
                        else
                        {
                            this._ImageSize = images[0].Size;
                            this._FramesCount = images.Length;

                            //Calculate fullscreen destination rectangle
                            float fWidthSource = this._ImageSize.Width;
                            float fHeightSource = this._ImageSize.Height;
                            float fZoom = calculateBestZoom(fWidthSource, fHeightSource);
                            float fX, fY, fWidth, fHeight;

                            //Calculate target rectangle
                            getOutputRect(fWidthSource, fHeightSource, fZoom, out fX, out fY, out fWidth, out fHeight);
                            this._FullscreenRectangle = new System.Drawing.RectangleF(fX, fY, fWidth, fHeight);

                            //get texture from first frame
                            int iDur;
                            Texture tx = GUITextureManager.GetTexture(this.Id, 0, out iDur);

                            //Frame rotation timer init
                            if (images.Length > 1)
                            {
                                if (this._TimerAnimation == null)
                                {
                                    this._TimerAnimation = new System.Timers.Timer();
                                    this._TimerAnimation.Elapsed += this.cbTimerAnimation;
                                }

                                this._TimerAnimation.Interval = iDur;
                                this._TimerAnimation.AutoReset = true;
                                this._TimerAnimation.Enabled = this.Active;
                            }

                            //Set first frame to GUI
                            this._CurrentTexture = tx;
                            if (this.Active)
                                GUIPropertyManager.SetProperty(this.GuiTag, this.Id);
                            this.ThumbnailImage = this.Id;
                        }
                    }
                    else
                        this.SetEmptyImage(); //no images; set as black
                }
            }
            finally
            {
                //Dispose all images
                if (images != null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        System.Drawing.Image im = images[i];
                        if (im != null)
                            im.Dispose();
                    }

                    images = null;
                }

                this.LastRefresh = DateTime.Now;
            }
        }

        /// <summary>
        /// Set image as empty(black)
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetEmptyImage()
        {
            this.Destroy();

            this.ThumbnailImage = _EmptyImage;

            if (this.Active)
                GUIPropertyManager.SetProperty(this.GuiTag, _EmptyImage);
        }

        public bool RefreshBegin()
        {
            return System.Threading.Interlocked.CompareExchange(ref this._RefreshActive, 1, 0) == 0;
        }

        public void RefreshEnd()
        {
            this._RefreshActive = 0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (this._TimerAnimation != null)
                this._TimerAnimation.Stop();

            GUIPropertyManager.SetProperty(this._GuiTag, string.Empty);
            this.Active = false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            if (this._TimerAnimation != null && !this._TimerAnimation.Enabled)
                this._TimerAnimation.Start();


            if (this.ThumbnailImage == _EmptyImage)
                GUIPropertyManager.SetProperty(this._GuiTag, _EmptyImage);
            else
                GUIPropertyManager.SetProperty(this._GuiTag, this.Id);

            this.Active = true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Terminate()
        {
            this.Stop();
            this.Destroy();
            this.Terminating = true;
        }


        private static void getOutputRect(float fSourceWidth, float fSourceHeight, float fZoomLevel, out float fX, out float fY,
                       out float fWidth, out float fHeight)
        {
            float fOffsetX1 = GUIGraphicsContext.OverScanLeft;
            float fOffsetY1 = GUIGraphicsContext.OverScanTop;
            float fScreenWidth = GUIGraphicsContext.OverScanWidth;
            float fScreenHeight = GUIGraphicsContext.OverScanHeight;
            float fPixelRatio = GUIGraphicsContext.PixelRatio;

            float fSourceFrameAR = ((float)fSourceWidth) / ((float)fSourceHeight);
            float fOutputFrameAR = fSourceFrameAR / fPixelRatio;

            fWidth = (fSourceWidth / fPixelRatio) * fZoomLevel;
            fHeight = fSourceHeight * fZoomLevel;

            fX = (fScreenWidth - fWidth) / 2 + fOffsetX1;
            fY = (fScreenHeight - fHeight) / 2 + fOffsetY1;
        }

        private static float calculateBestZoom(float fWidth, float fHeight)
        {
            float fPixelRatio = GUIGraphicsContext.PixelRatio;
            float fZoomFactorX = (float)(GUIGraphicsContext.OverScanWidth * fPixelRatio) / fWidth;
            float fZoomFactorY = (float)GUIGraphicsContext.OverScanHeight / fHeight;

            if (fZoomFactorY < fZoomFactorX)
                return fZoomFactorY;
            else
                return fZoomFactorX;
        }


        /// <summary>
        /// Callback from frame animation timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void cbTimerAnimation(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this._CurrentFrameIdx++ >= this._FramesCount)
                this._CurrentFrameIdx = 0; //reset to begining

            if (this._CurrentFrameIdx < this._FramesCount)
            {
                int iDur;
                Texture tx = GUITextureManager.GetTexture(this.Id, this._CurrentFrameIdx, out iDur);

                lock (GUIGraphicsContext.RenderLock)
                {
                    this._CurrentTexture = tx;
                    this._TimerAnimation.Interval = iDur;
                    this._TimerAnimation.Start();
                }
            }
        }

    }
}
