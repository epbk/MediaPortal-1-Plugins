using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using NLog;

namespace MediaPortal.IptvChannels.SiteUtils
{
    public abstract class SiteUtilBase
    {
        #region Types
        public enum VideoQualityTypes { Lowest, Highest, LQ, SD, HD720, HD1080, UHD2K, UHD4K, UHD8K };
        #endregion

        #region Variables
        protected bool _Enabled = true;
        protected string _Version = "1.0.0";
        protected string _Author = "Unknown";
        protected string _Description = "";
        protected NLog.Logger _Logger = LogManager.GetCurrentClassLogger();
        protected List<IptvChannel> _ChannelList = new List<IptvChannel>();
        protected DateTime _EpgLastRefresh = DateTime.MinValue;
        protected int _EpgRefreshPeriod = 60; //[minutes]
        protected VideoQualityTypes _VideoQuality = VideoQualityTypes.Highest;
        #endregion

        #region Properties
        [Category("IptvChannelsUserConfiguration"), Description("Preferred video quality."), DisplayName("Video Quality")]
        public virtual VideoQualityTypes VideoQuality
        {
            get
            {
                return this._VideoQuality;
            }
            set
            {
                this._VideoQuality = value;
            }
        }

        [Category("IptvChannelsUserConfiguration"), Description("EPG refresh enable."), DisplayName("Epg Refresh Enabled")]
        public bool EpgRefreshEnabled
        { get; set; }

        [Category("IptvChannelsUserConfiguration"), Description("EPG refresh period in minutes."), DisplayName("Epg Refresh Period")]
        public int EpgRefreshPeriod
        {
            get
            {
                if (this._EpgRefreshPeriod < 1) 
                    return 1;
                else
                    return this._EpgRefreshPeriod;
            }
            set
            {
                if (value < 1) 
                    this._EpgRefreshPeriod = 1;
                else 
                    this._EpgRefreshPeriod = value;
            }
        }

        [Category("IptvChannelsUserConfiguration"), Description("Author.")]
        public string Author
        {
            get
            {
                return this._Author;
            }
        }

        [Category("IptvChannelsUserConfiguration"), Description("Current version.")]
        public string Version
        {
            get
            {
                return this._Version;
            }
        }

        [Category("IptvChannelsUserConfiguration"), Description("Description.")]
        public string Description
        {
            get
            {
                return this._Description;
            }
        }

        [Category("IptvChannelsUserConfiguration"), Description("Enable or disable the site.")]
        public bool Enabled
        {
            get
            {
                return this._Enabled;
            }
            set
            {
                this._Enabled = value;
            }
        }

        [Browsable(false)]
        public string Name
        {
            get
            {
                return this.GetType().Name;
            }
        }

        [Browsable(false)]
        public DateTime EpgLastRefresh
        {
            get
            {
                return this._EpgLastRefresh;
            }
            set
            {
                this._EpgLastRefresh = value;
            }
        }

        [Browsable(false)]
        public IEnumerable<IptvChannel> Channels
        {
            get
            {
                return this._ChannelList;
            }
        }
        #endregion

        #region ctor

        static SiteUtilBase()
        {
            //LoadDll.InitDll();
        }

        #endregion

        #region Virtual methods

        /// <summary>
        /// Initialize site util
        /// </summary>
        public virtual void Initialize()
        {
            //System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            //this.Logger.Info(string.Format("Plugin has starded. Version " + assembly.GetName().Version.ToString() + " ."));
            this._Logger.Info(string.Format("Plugin has started. Version " + this._Version));
        }

        /// <summary>
        /// Get stream url
        /// </summary>
        /// <param name="channel">Channel for which to get the stream url</param>
        /// <returns>Stream url</returns>
        public virtual string GetStreamUrl(IptvChannel channel)
        {
            return null;
        }

        /// <summary>
        /// Refresh EPG of all channels
        /// </summary>
        /// <returns>True if refresh is successful</returns>
        public virtual bool RefreshEpg()
        {
            return false; 
        }
        #endregion

        #region Overrides
        public override string ToString()
        {
            return this.Name;
        }
        #endregion

        #region Private methods
        #endregion
    }
}
