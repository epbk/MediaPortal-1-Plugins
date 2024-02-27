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
        }private string _Id;
        public string Name
        {
            get
            {
                return this._Name;
            }
        }private string _Name;
        public string Url
        {
            get
            {
                return this._Url;
            }
        }private string _Url;

        public SiteUtilBase SiteUtil
        {
            get
            {
                return this._SiteUtil;
            }
        }private SiteUtilBase _SiteUtil;
        #endregion
        
        #region ctor
        public IptvChannel(SiteUtilBase site, string id, string url, string name )
        {
            this._SiteUtil = site;
            this._Id = id;
            this._Name = name;
            this._Url = url;
        }
        #endregion
    }
}
