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
        /// UPnP device type. Single URI.
        /// </summary>
        public string DeviceType { get; private set; }

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
        /// URL to presentation for device
        /// </summary>
        public string PresentationUrl { get; private set; }

        /// <summary>
        /// Model name.
        /// </summary>
        public string ModelName { get; private set; }

        /// <summary>
        /// Long description for end user.
        /// </summary>
        public string ModelDescription { get; private set; }

        /// <summary>
        /// Short description for end user.
        /// </summary>
        public string FriendlyName { get; private set; }

        /// <summary>
        /// Manufacturer's name.
        /// </summary>
        public string Manufacturer { get; private set; }

        /// <summary>
        /// Web site for Manufacturer.
        /// </summary>
        public string ManufacturerUrl { get; private set; }

        /// <summary>
        /// Model number.
        /// </summary>
        public string ModelNumber { get; private set; }

        /// <summary>
        /// Web site for model.
        /// </summary>
        public string ModelUrl { get; private set; }

        /// <summary>
        /// Serial number.
        /// </summary>
        public string SerialNumber { get; private set; }

        /// <summary>
        /// Unique Device Name.
        /// </summary>
        public string UDN { get; private set; }
                
        /// <summary>
        /// In device templates, defines the lowest version of the architecture on which the device can be implemented.
        /// </summary>
        public int SpecVersionMajor { get; private set; } = -1;

        /// <summary>
        /// In device templates, defines the lowest version of the architecture on which the device can be implemented.
        /// </summary>
        public int SpecVersionMinor { get; private set; } = -1;

        /// <summary>
        /// Universal Product Code.
        /// </summary>
        public string UPC { get; private set; }
        
        /// <summary>
        /// Icon list.
        /// </summary>
        public List<SsdpServerInfoIcon> Icons { get; private set; } = null;

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

        public SsdpServerInfo(string strDevType, string strLocation, string strUSN, string strUUID, string strServer,
            int iCfgId, int iBootId, int iMaxAge, Dictionary<string, string> httpFields)
        {
            this.DeviceType = strDevType;
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
            sb.Append("FriendlyName: ").AppendLine(this.FriendlyName);
            sb.Append("DeviceType: ").AppendLine(this.DeviceType);
            sb.Append("UDN: ").AppendLine(this.UDN);
            sb.Append("USN: ").AppendLine(this.USN);
            sb.Append("UUID: ").AppendLine(this.UUID);
            sb.Append("Version: ").Append(this.SpecVersionMajor).Append('.').Append(this.SpecVersionMinor).AppendLine();
            sb.Append("Manufacturer: ").AppendLine(this.Manufacturer);
            sb.Append("ManufacturerURL: ").AppendLine(this.ManufacturerUrl);
            sb.Append("ModelDescription: ").AppendLine(this.ModelDescription);
            sb.Append("ModelName: ").AppendLine(this.ModelName);
            sb.Append("ModelNumber: ").AppendLine(this.ModelNumber);
            sb.Append("ModelURL: ").AppendLine(this.ModelUrl);
            sb.Append("SerialNumber: ").AppendLine(this.SerialNumber);
            sb.Append("PresentationURL: ").AppendLine(this.PresentationUrl);
            sb.Append("UPC: ").AppendLine(this.UPC);

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

                this.PresentationUrl = si.PresentationUrl;
                this.FriendlyName = si.FriendlyName;
                this.Manufacturer = si.Manufacturer;
                this.ManufacturerUrl = si.ManufacturerUrl;
                this.ModelDescription = si.ModelDescription;
                this.ModelName = si.ModelName;
                this.ModelNumber = si.ModelNumber;
                this.ModelUrl = si.ModelUrl;
                this.SerialNumber = si.SerialNumber;
                this.UPC = si.UPC;
                this.Icons = si.Icons;
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
            try
            {
                const string NS = "urn:schemas-upnp-org:device-1-0";
                const string NS_SATIP = "urn:ses-com:satip";

                XmlNode nodeRoot = xml["root", NS];
                if (nodeRoot == null)
                    return false;

                if (this.ConfigID >= 0 && (!int.TryParse(nodeRoot.Attributes["configId"]?.Value, out int i) || this.ConfigID != i))
                    return false;

                XmlNode nodeRootDev = nodeRoot["device", NS];
                if (nodeRootDev == null)
                    return false;

                this.PresentationUrl = nodeRootDev["presentationURL", NS]?.InnerText;
                this.FriendlyName = nodeRootDev["friendlyName", NS]?.InnerText;
                this.Manufacturer = nodeRootDev["manufacturer", NS]?.InnerText;
                this.ManufacturerUrl = nodeRootDev["manufacturerURL", NS]?.InnerText;
                this.ModelDescription = nodeRootDev["modelDescription", NS]?.InnerText;
                this.ModelName = nodeRootDev["modelName", NS]?.InnerText;
                this.ModelNumber = nodeRootDev["modelNumber", NS]?.InnerText;
                this.ModelUrl = nodeRootDev["modelURL", NS]?.InnerText;
                this.SerialNumber = nodeRootDev["serialNumber", NS]?.InnerText;
                this.DeviceType = nodeRootDev["deviceType", NS]?.InnerText;
                this.UDN = nodeRootDev["UDN", NS]?.InnerText;
                this.UPC = nodeRootDev["UPC", NS]?.InnerText;

                XmlNode nodesIcon = nodeRootDev["iconList", NS];
                if (nodesIcon != null)
                {
                    this.Icons = new List<SsdpServerInfoIcon>();
                    foreach (XmlNode nodeIcon in nodesIcon.ChildNodes)
                    {
                        if (nodeIcon.Name == "icon" && nodeIcon.NamespaceURI == NS)
                        {
                            this.Icons.Add(new SsdpServerInfoIcon()
                            {
                                MimeType = nodeIcon["mimetype", NS].InnerText,
                                Url = nodeIcon["url", NS].InnerText,
                                Width = int.Parse(nodeIcon["width", NS].InnerText),
                                Height = int.Parse(nodeIcon["height", NS].InnerText),
                                Depth = int.Parse(nodeIcon["depth", NS].InnerText)
                            }
                            );
                        }
                    }
                }

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
            catch( Exception ex)
            {
                _Logger.Error("[parseDescription] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            return false;
        }
    }
}
