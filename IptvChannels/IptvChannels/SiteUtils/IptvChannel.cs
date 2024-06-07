using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TvDatabase;

namespace MediaPortal.IptvChannels.SiteUtils
{
    public class IptvChannel
    {
        #region Variables
        public bool Enabled = true;
        public Channel Channel = null;
        public ProgramList EpgProgramList = null;

        public string LastFinalUrl = string.Empty;
        public DateTime LastFinalUrlTS = DateTime.MinValue;
        public DateTime LastEpgRefreshTS = DateTime.MinValue;

        public int TransportStreamID = -1;
        public int ServiceID = -1;
        public int NetworkID = -1;
        public int PmtID = -1;

        public object Tag;
        #endregion

        #region Properties
        public string Id
        {
            get
            {
                return this._Id;
            }
        }private readonly string _Id;
        public string Name
        {
            get
            {
                return this._Name;
            }
        }private readonly string _Name;
        public string Url
        {
            get
            {
                return this._Url;
            }
        }private readonly string _Url;
        public string TvServerLink
        {
            get
            {
                if (this._TvServerLink == null)
                {
                    //Default channel link used in TV Server Channel

                    StringBuilder sb = new StringBuilder(256);

                    sb.Append("http://127.0.0.1:");
                    sb.Append(Database.dbSettings.Instance.HttpServerPort);
                    sb.Append(Plugin.HTTP_PATH_STREAM);
                    sb.Append("?site=");
                    sb.Append(this.Name);
                    sb.Append("&channel=");
                    sb.Append(this.Id);

                    string strUrl = System.Web.HttpUtility.UrlEncode(sb.ToString());
                    sb.Clear();
                    sb.Append(Plugin.URL_FILTER_BASE);
                    sb.Append(strUrl);
                    if (this.PmtID >= 32 && this.PmtID <= 8191)
                    {
                        //Custom MPURLSourceSplitter arguments
                        sb.Append("&Mpeg2TsTransportStreamID=");
                        sb.Append(this.TransportStreamID);
                        sb.Append("&Mpeg2TsProgramNumber=");
                        sb.Append(this.ServiceID);
                        sb.Append("&Mpeg2TsProgramMapPID=");
                        sb.Append(this.PmtID);
                        sb.Append("&HttpOpenConnectionTimeout=30000");
                    }
                    else
                        sb.Append(Plugin.URL_FILTER_PARAM);

                    this._TvServerLink = sb.ToString();
                }

                return this._TvServerLink;
            }
        }private string _TvServerLink = null;


        public SiteUtilBase SiteUtil
        {
            get
            {
                return this._SiteUtil;
            }
        }private readonly SiteUtilBase _SiteUtil;
        #endregion

        #region ctor
        public IptvChannel(SiteUtilBase site, string strId, string strUrl, string StrName, string strTvServerLink = null)
        {
            this._SiteUtil = site;
            this._Id = strId;
            this._Name = StrName;
            this._Url = strUrl;
            this._TvServerLink = strTvServerLink;
        }
        #endregion
    }
}
