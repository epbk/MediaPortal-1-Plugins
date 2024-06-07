using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using MediaPortal.Pbk.Cornerstone.Database;
using System.Web;
using NLog;

namespace MediaPortal.IptvChannels.SiteUtils
{
    public abstract class SiteUtilBase
    {
        #region Types
        public enum VideoQualityTypes { Lowest, Highest, LQ, SD, HD720, HD1080, UHD2K, UHD4K, UHD8K };
        #endregion

        #region Variables
        protected Plugin _ParentPlugin = null;
        protected bool _Initialized = true;
        protected bool _Enabled = true;
        protected string _Version = "1.0.0";
        protected string _Author = "Unknown";
        protected string _Description = "";
        protected Logger _Logger = LogManager.GetCurrentClassLogger();
        protected List<IptvChannel> _ChannelList = new List<IptvChannel>();
        protected DateTime _EpgLastRefresh = DateTime.MinValue;
        protected int _EpgRefreshPeriod = 1440 * 60000; //[minutes]
        protected VideoQualityTypes _VideoQuality = VideoQualityTypes.Highest;
        protected bool _UpdateTvServerChannelLink = true;
        #endregion

        #region Properties
        [Category("Video"), Description("Preferred video quality."), DisplayName("Video Quality")]
        [DBField()]
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

        [Category("EPG"), Description("EPG refresh enable."), DisplayName("Epg Refresh Enabled")]
        [Editor(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [DBField()]
        [DefaultValue(false)]
        public bool EpgRefreshEnabled
        { get; set; }

        [Category("EPG"), Description("EPG refresh period."), DisplayName("Epg Refresh Period")]
        [TypeConverter(typeof(Controls.UIEditor.TimePeriodConverter))]
        [DefaultValue(1440 * 60000)]
        [DBField()]
        public int EpgRefreshPeriod
        {
            get
            {
                if (this._EpgRefreshPeriod < 60000) 
                    return 60000;
                else
                    return this._EpgRefreshPeriod;
            }
            set
            {
                if (value < 60000) 
                    this._EpgRefreshPeriod = 60000;
                else 
                    this._EpgRefreshPeriod = value;
            }
        }

        [Category("Plugin"), Description("Author.")]
        [DBField()]
        public string Author
        {
            get
            {
                return this._Author;
            }
        }

        [Category("Plugin"), Description("Current version.")]
        public string Version
        {
            get
            {
                return this._Version;
            }
        }

        [Category("Plugin"), Description("Description.")]
        public string Description
        {
            get
            {
                return this._Description;
            }
        }

        [Category("Plugin"), Description("Enable or disable the site.")]
        [Editor(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [DBField()]
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

        [Category("Channels"), Description("Automatically update TV Server channel link.")]
        [Editor(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [DisplayName("Update TvServer Channel Link")]
        [DBField()]
        [DefaultValue(true)]
        public bool UpdateTvServerChannelLink
        {
            get
            {
                return this._UpdateTvServerChannelLink;
            }
            set
            {
                this._UpdateTvServerChannelLink = value;
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
        /// <param name="plugin">IptvChannels plugin reference</param>
        public virtual void Initialize(Plugin plugin)
        {
            //System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            //this.Logger.Info(string.Format("Plugin has starded. Version " + assembly.GetName().Version.ToString() + " ."));
            this._Logger.Info(string.Format("Plugin has started. Version " + this._Version));
            this._ParentPlugin = plugin;
            this._Initialized = true;
        }

        /// <summary>
        /// Get stream url
        /// </summary>
        /// <param name="channel">Channel for which to get the stream url</param>
        /// <param name="prms">Extra url arguments</param>
        /// <returns>Stream url</returns>
        public virtual LinkResult GetStreamUrl(IptvChannel channel)
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
