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
        private int _CurrentFrameIdx = -1;
        private List<GUIImageFrame> _ImageFrames = null;

        public string Id
        { get; private set; }

        public string GuiTag
        { get; private set; }

        public DateTime LastRefresh = DateTime.MinValue;

        public bool FramesAvailable
        { get { return this._ImageFrames != null; } }

        /// <summary>
        /// Gets current image frame to be visible
        /// </summary>
        public GUIImageFrame CurrentImageFrame
        {
            get
            {
                return this._CurrentImageFrame;
            }
        }private GUIImageFrame _CurrentImageFrame = null;

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
            this.GuiTag = strGuiTag;

        }
        #endregion

        public static void DestroyFrames(List<GUIImageFrame> frames)
        {
            if (frames != null)
            {
                frames.ForEach(o =>
                {
                    GUITextureManager.ReleaseTexture(o.ID);
                    o.Texture = null;
                });

                frames.Clear();
            }
        }

        /// <summary>
        /// Destroy the image and release all textures
        /// </summary>
        /// <param name="bDeleteFiles">True to delete temporary download files.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Destroy()
        {
            GUIPropertyManager.SetProperty(this.GuiTag, string.Empty);

            if (this._ImageFrames != null)
            {
                if (this._TimerAnimation != null)
                {
                    this._TimerAnimation.Stop();

                    this._TimerAnimation.Dispose();
                    this._TimerAnimation = null;
                }

                DestroyFrames(this._ImageFrames);


                this._CurrentImageFrame = null;
                this._ImageFrames = null;

            }

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
        /// Initialize the image
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetImage(List<GUIImageFrame> frames)
        {
            lock (GUIGraphicsContext.RenderLock)
            {
                if (this._ImageFrames != null)
                    this.Destroy();

                if (GUIWindowManager.ActiveWindow != GUIWorldWeaterLite.PLUGIN_ID)
                {
                    //If we aren't active then destroy images and return
                    DestroyFrames(frames);
                    return;
                }

                if (frames != null && frames.Count > 0)
                {
                    this._CurrentFrameIdx = 0;

                    if (frames.Count > 1)
                    {
                        //Frame rotation timer init

                        if (this._TimerAnimation == null)
                        {
                            this._TimerAnimation = new System.Timers.Timer();
                            this._TimerAnimation.Elapsed += this.cbTimerAnimation;
                        }

                        this._TimerAnimation.Interval = frames[0].Duration;
                        this._TimerAnimation.AutoReset = true;
                        this._TimerAnimation.Enabled = true;
                    }

                    //Set first frame to GUI
                    this._CurrentImageFrame = frames[0];
                    GUIPropertyManager.SetProperty(this.GuiTag, this._CurrentImageFrame.ID);
                    this.ThumbnailImage = this._CurrentImageFrame.ID;

                    this._ImageFrames = frames;
                }
                else
                    this.SetEmptyImage();
            }

            this.LastRefresh = DateTime.Now;
        }

        /// <summary>
        /// Set image as empty(black)
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetEmptyImage()
        {
            if (this._ImageFrames != null)
                this.Destroy();

            this.ThumbnailImage = _EmptyImage;
            GUIPropertyManager.SetProperty(this.GuiTag, _EmptyImage);

        }


        /// <summary>
        /// Callback from frame animation timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void cbTimerAnimation(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this._CurrentFrameIdx++ >= this._ImageFrames.Count)
                this._CurrentFrameIdx = 0; //reset to begining

            if (this._CurrentFrameIdx < this._ImageFrames.Count)
            {
                GUIImageFrame frame = this._ImageFrames[this._CurrentFrameIdx];

                lock (GUIGraphicsContext.RenderLock)
                {
                    this.ThumbnailImage = frame.ID;
                    GUIPropertyManager.SetProperty(this.GuiTag, frame.ID);
                    this._CurrentImageFrame = frame;
                    this._TimerAnimation.Interval = frame.Duration;
                    this._TimerAnimation.Start();
                }
            }
        }

    }
}
