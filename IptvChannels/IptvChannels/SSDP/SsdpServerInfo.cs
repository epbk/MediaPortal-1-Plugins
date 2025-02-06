using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using MediaPortal.Pbk.Net.Http;
using NLog;

namespace MediaPortal.IptvChannels.SSDP
{
    public class SsdpServerInfo
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Root device.
        /// </summary>
        public SsdpServerInfoDevice RootDevice { get; private set; }

        /// <summary>
        /// Search target.
        /// </summary>
        public string SearchTarget { get; private set; }

        /// <summary>
        /// URL to the UPnP description of the root device.
        /// </summary>
        public string Location { get; private set; }

        /// <summary>
        /// Unique Service Name.
        /// </summary>
        public string USN { get; private set; }

        /// <summary>
        /// Universally Unique Identifier.
        /// </summary>
        public string UUID { get; private set; }

        /// <summary>
        /// Specified by UPnP vendor.
        /// </summary>
        public string Server { get; private set; }

        /// <summary>
        /// Boot instance of the device.
        /// </summary>
        public int BootID { get; private set; } = -1;

        /// <summary>
        /// Configuration number of a root device.
        /// </summary>
        public int ConfigID { get; private set; } = -1;

        /// <summary>
        /// In device templates, defines the lowest version of the architecture on which the device can be implemented.
        /// </summary>
        public int SpecVersionMajor { get; private set; } = -1;

        /// <summary>
        /// In device templates, defines the lowest version of the architecture on which the device can be implemented.
        /// </summary>
        public int SpecVersionMinor { get; private set; } = -1;

        /// <summary>
        /// Specifies the number of seconds the advertisement is valid.
        /// </summary>
        public int MaxAge { get; private set; }

        /// <summary>
        /// Timestamp of last update.
        /// </summary>
        public DateTime RefreshTimeStamp { get; private set; }

        /// <summary>
        /// Returns remaining valid time period.
        /// </summary>
        public TimeSpan ExpiresIn
        {
            get
            {
                TimeSpan ts = this.RefreshTimeStamp.AddSeconds(this.MaxAge) - DateTime.Now;
                return ts < TimeSpan.Zero ? TimeSpan.Zero : ts;
            }
        }

        /// <summary>
        /// Returns true if this SsdpServerInfo is valid.
        /// </summary>
        public bool Parsed { get; private set; } = false;

        #region SatIP specific properties
        public int SatIpDeviceID { get; private set; } = -1;
        public int SatIpRtspPort { get; private set; } = -1;
        public string SatIpChannelListUrl { get; private set; }
        public string SatIpCapabilities { get; private set; }
        #endregion

        /// <summary>
        /// Current status.
        /// </summary>
        public SsdpServerInfoStatusEnum Status { get; internal set; } = SsdpServerInfoStatusEnum.Invalid;

        public bool IsValid
        {
            get
            {
                return this.Parsed && this.Status >= SsdpServerInfoStatusEnum.Alive && this.ExpiresIn.TotalSeconds >= 10;
            }
        }

        public SsdpServerInfo(string strSearchTarget, string strLocation, string strUSN, string strUUID, string strServer,
            int iCfgId, int iBootId, int iMaxAge, Dictionary<string, string> httpFields)
        {
            this.SearchTarget = strSearchTarget;
            this.Location = strLocation;
            this.USN = strUSN;
            this.UUID = strUUID;
            this.Server = strServer;
            this.ConfigID = iCfgId;
            this.BootID = iBootId;
            this.MaxAge = iMaxAge;
            this.RefreshTimeStamp = DateTime.Now;

            //SatIP
            if (httpFields != null)
            {
                if (httpFields.TryGetValue("DEVICEID.SES.COM", out string str) && int.TryParse(str, out int i))
                    this.SatIpDeviceID = i;
                else
                    this.SatIpDeviceID = -1;
            }
        }

        public StringBuilder PrintReport(StringBuilder sb)
        {
            sb.Append("Server: ").AppendLine(this.Server);
            sb.Append("Location: ").AppendLine(this.Location);
            sb.Append("MaxAge: ").Append(this.MaxAge).AppendLine();
            sb.Append("USN: ").AppendLine(this.USN);
            sb.Append("UUID: ").AppendLine(this.UUID);
            sb.Append("Version: ").Append(this.SpecVersionMajor).Append('.').Append(this.SpecVersionMinor).AppendLine();
            sb.Append("BootID: ").Append(this.BootID).AppendLine();
            sb.Append("ConfigID: ").Append(this.ConfigID).AppendLine();
            sb.AppendLine("Root:");
            this.RootDevice.PrintReport(sb, " ");

            sb.Append("Sat>IP: Capabilities: ").AppendLine(this.SatIpCapabilities);
            sb.Append("Sat>IP: ChannelListURL: ").AppendLine(this.SatIpChannelListUrl);
            sb.Append("Sat>IP: DeviceID: ").Append(this.SatIpDeviceID).AppendLine();
            sb.Append("Sat>IP: RtspPort: ").Append(this.SatIpRtspPort).AppendLine();

            return sb;
        }

        public bool IsMatch(SsdpServerInfo si)
        {
            return this.USN == si?.USN;
        }

        public int UpdateFrom(SsdpServerInfo si)
        {
            this.RefreshTimeStamp = si.RefreshTimeStamp;
            this.MaxAge = si.MaxAge;

            if (!this.Parsed || si.Status == SsdpServerInfoStatusEnum.Updated
                || (this.BootID >= 0 && this.BootID != si.BootID))
            {
                if (!si.Parsed || !si.LoadDescription())
                    return -1;

                this.Location = si.Location;
                this.Server = si.Server;
                this.ConfigID = si.ConfigID;
                this.BootID = si.BootID;
                this.RootDevice = si.RootDevice;

                this.SpecVersionMajor = si.SpecVersionMajor;
                this.SpecVersionMinor = si.SpecVersionMinor;

                this.SatIpCapabilities = si.SatIpCapabilities;
                this.SatIpChannelListUrl = si.SatIpChannelListUrl;
                this.SatIpRtspPort = si.SatIpRtspPort;
                this.SatIpDeviceID = si.SatIpDeviceID;

                this.Status = SsdpServerInfoStatusEnum.Updated;
                return 1; //updated
            }

            this.Status = SsdpServerInfoStatusEnum.Alive;
            return 0; //no change
        }

        public bool LoadDescription()
        {
            //Get description for the server
            try
            {
                using (HttpUserWebRequest rq = new HttpUserWebRequest(this.Location))
                {
                    XmlDocument xml = new XmlDocument();
                    xml.LoadXml(rq.Download<string>());
                    return this.parseDescription(xml, rq.HttpResponseFields);
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[loadDescription] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
            return false;
        }

        private bool parseDescription(XmlDocument xml, Dictionary<string, string> httpFields)
        {
            const string NS = "urn:schemas-upnp-org:device-1-0";
            const string NS_SATIP = "urn:ses-com:satip";

            try
            {
                XmlNode nodeRoot = xml["root", NS];
                if (nodeRoot == null)
                    return false;

                if (this.ConfigID >= 0 && (!int.TryParse(nodeRoot.Attributes["configId"]?.Value, out int i) || this.ConfigID != i))
                    return false;

                XmlNode nodeRootDev = nodeRoot["device", NS];
                if (nodeRootDev == null)
                    return false;

                this.RootDevice = new SsdpServerInfoDevice(nodeRootDev, NS);

                XmlNode nodeVersion = nodeRoot["specVersion", NS];
                if (nodeVersion != null)
                {
                    this.SpecVersionMajor = int.Parse(nodeVersion["major", NS].InnerText);
                    this.SpecVersionMinor = int.Parse(nodeVersion["minor", NS].InnerText);
                }

                #region SatIP
                this.SatIpCapabilities = nodeRootDev["X_SATIPCAP", NS_SATIP]?.InnerText;
                this.SatIpChannelListUrl = nodeRootDev["X_SATIPM3U", NS_SATIP]?.InnerText;

                if (httpFields.TryGetValue("X-SATIP-RTSP-Port", out string str) && int.TryParse(str, out i))
                    this.SatIpRtspPort = i;
                else
                    this.SatIpRtspPort = -1;
                #endregion

                this.Parsed = true;
                return true;
            }
            catch (Exception ex)
            {
                _Logger.Error("[parseDescription] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            return false;
        }
    }
}
