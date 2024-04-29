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
        protected bool _Enabled = true;
        protected string _Version = "1.0.0";
        protected string _Author = "Unknown";
        protected string _Description = "";
        protected Logger _Logger = LogManager.GetCurrentClassLogger();
        protected List<IptvChannel> _ChannelList = new List<IptvChannel>();
        protected DateTime _EpgLastRefresh = DateTime.MinValue;
        protected int _EpgRefreshPeriod = 1440 * 60000; //[minutes]
        protected VideoQualityTypes _VideoQuality = VideoQualityTypes.Highest;
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

        /// <summary>
        /// Get channel url
        /// </summary>
        /// <param name="channel">Channel for which to get the channel url</param>
        /// <returns>Channel url</returns>
        public virtual string GetChannelUrl(IptvChannel channel)
        {
            return Plugin.URL_FILTER_BASE + HttpUtility.UrlEncode("http://127.0.0.1:" + Database.dbSettings.Instance.HttpServerPort 
                + Plugin.HTTP_PATH_STREAM + "?site=" + HttpUtility.UrlEncode(this.Name) + "&channel=" + channel.Id)
                + (channel.PmtID >= 32 && channel.PmtID <= 8191 ? "&Mpeg2TsTransportStreamID=" + channel.TransportStreamID +
                    "&Mpeg2TsProgramNumber=" + channel.ServiceID + "&Mpeg2TsProgramMapPID=" + channel.PmtID + "&HttpOpenConnectionTimeout=30000" : Plugin.URL_FILTER_PARAM);
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
