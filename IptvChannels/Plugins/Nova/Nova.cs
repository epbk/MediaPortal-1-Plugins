using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TvDatabase;
using System.ComponentModel;
using NLog;
using MediaPortal.Pbk.Cornerstone.Database;

namespace MediaPortal.IptvChannels.SiteUtils.Sites
{
    public class Nova : SiteUtilBase
    {
        #region Constants
        #endregion

        #region Variables
        #endregion

        #region Properties
        [Browsable(false)]
        public override VideoQualityTypes VideoQuality
        { get; set; }
        #endregion

        #region ctor
        public Nova()
        {
            //Basics
            this._Version = "1.0.0";
            this._Author = "Pbk";
            this._Description = "Nova";
        }
        #endregion

        #region Overrides
        public override void Initialize(Plugin plugin)
        {
            this._ChannelList.Add(new IptvChannel(this, "NOVA", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova/live_fullhd", "Nova (Web)") { Tag = "nova" });
            this._ChannelList.Add(new IptvChannel(this, "CINEMA", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-cinema/live_fullhd", "Nova Cinema (Web)") { Tag = "nova-cinema" });
            this._ChannelList.Add(new IptvChannel(this, "ACTION", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-action/live_fullhd", "Nova Action (Web)") { Tag = "nova-action" });
            this._ChannelList.Add(new IptvChannel(this, "FUN", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova2/live_fullhd", "Nova Fun (Web)") { Tag = "nova-fun" });
            this._ChannelList.Add(new IptvChannel(this, "GOLD", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-gold/live_fullhd", "Nova Gold (Web)") { Tag = "nova-gold" });
            this._ChannelList.Add(new IptvChannel(this, "LADY", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-lady/live_fullhd", "Nova Lady (Web)") { Tag = "nova-lady" });
            this._ChannelList.Add(new IptvChannel(this, "NEWS", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-news/live_fullhd", "Nova Live News (Web)") { Tag = "nova-news" });
            this._ChannelList.Add(new IptvChannel(this, "SVET", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-aptn/live_fullhd", "Nova Live Svět (Web)") { Tag = "nova-svet" });
            this._ChannelList.Add(new IptvChannel(this, "LIVE3", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-4/live_fullhd", "Nova Live 3 (Web)") { Tag = "nova-live3" });
            this._ChannelList.Add(new IptvChannel(this, "LIVE4", "http://rpprod.hbbtv.cdn.cra.cz:8080/hbbTV/nova-gc1/live_fullhd", "Nova Live 4 (Web)") { Tag = "nova-live4" });

            //Initialize base
            base.Initialize(plugin);
        }

        public override LinkResult GetStreamUrl(IptvChannel channel)
        {
            lock (channel)
            {
                this._Logger.Debug("[GetStreamUrl] Query:{0}  ID:{1}", channel.Name, channel.Id);

                return new LinkResult() { Url = channel.Url };
            }
        }

        #endregion

        #region Private methods

        #endregion
    }
}
