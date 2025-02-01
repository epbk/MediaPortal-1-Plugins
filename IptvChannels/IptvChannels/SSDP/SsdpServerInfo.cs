using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace MediaPortal.IptvChannels.SSDP
{
    public class SsdpServerInfo
    {
        public string DeviceType;
        public int DeviceID = -1;
        public string Location;
        public string USN;
        public string UUID;
        public string Server;
        public string Capabilities;
        public string PresentationUrl;
        public string ModelName;
        public string ModelDescription;
        public string FriendlyName;
        public string Manufacturer;
        public string ManufacturerUrl;
        public string ModelNumber;
        public string ModelUrl;
        public string SerialNumber;
        public string UDN;
        public string ChannelListUrl;
        public int SpecVersionMajor = -1;
        public int SpecVersionMinor = -1;
        public string UPC;
        public int ConfigID = -1;

        public List<SsdpServerInfoIcon> Icons = null;

        public int RtspPort = -1;

        public int MaxAge;
        public DateTime RefreshTimeStamp;

        public TimeSpan ExpiresIn
        {
            get
            {
                return this.RefreshTimeStamp.AddSeconds(this.MaxAge) - DateTime.Now;
            }
        }

        public bool Parsed = false;

        public bool ParseDescription(XmlDocument xml)
        {
            XmlNamespaceManager ns = new XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("x", "urn:schemas-upnp-org:device-1-0");
            ns.AddNamespace("satip", "urn:ses-com:satip");

            XmlNode node = xml.SelectSingleNode("//x:root/@configId", ns);
            if (node == null || !int.TryParse(node.Value, out int iCfgId) || this.ConfigID != iCfgId)
                return false;

            this.Capabilities = xml.SelectSingleNode("//x:root/x:device/satip:X_SATIPCAP/text()", ns)?.Value;
            this.ChannelListUrl = xml.SelectSingleNode("//x:root/x:device/satip:X_SATIPM3U/text()", ns)?.Value;
            this.PresentationUrl = xml.SelectSingleNode("//x:root/x:device/x:presentationURL/text()", ns)?.Value;
            this.FriendlyName = xml.SelectSingleNode("//x:root/x:device/x:friendlyName/text()", ns)?.Value;
            this.Manufacturer = xml.SelectSingleNode("//x:root/x:device/x:manufacturer/text()", ns)?.Value;
            this.ManufacturerUrl = xml.SelectSingleNode("//x:root/x:device/x:manufacturerURL/text()", ns)?.Value;
            this.ModelDescription = xml.SelectSingleNode("//x:root/x:device/x:modelDescription/text()", ns)?.Value;
            this.ModelName = xml.SelectSingleNode("//x:root/x:device/x:modelName/text()", ns)?.Value;
            this.ModelNumber = xml.SelectSingleNode("//x:root/x:device/x:modelNumber/text()", ns)?.Value;
            this.ModelUrl = xml.SelectSingleNode("//x:root/x:device/x:modelURL/text()", ns)?.Value;
            this.SerialNumber = xml.SelectSingleNode("//x:root/x:device/x:serialNumber/text()", ns)?.Value;
            this.DeviceType = xml.SelectSingleNode("//x:root/x:device/x:deviceType/text()", ns)?.Value;
            this.UDN = xml.SelectSingleNode("//x:root/x:device/x:UDN/text()", ns)?.Value;
            this.UPC = xml.SelectSingleNode("//x:root/x:device/x:UPC/text()", ns)?.Value;

            XmlNodeList nodesIcon = xml.SelectNodes("//x:root/x:device/x:iconList/x:icon", ns);
            if (nodesIcon?.Count > 0)
            {
                this.Icons = new List<SsdpServerInfoIcon>();
                foreach (XmlNode nodeIcon in nodesIcon)
                {
                    this.Icons.Add(new SsdpServerInfoIcon()
                    {
                        MimeType = nodeIcon.SelectSingleNode("./x:mimetype/text()", ns).Value,
                        Url = nodeIcon.SelectSingleNode("./x:url/text()", ns).Value,
                        Width = int.Parse(nodeIcon.SelectSingleNode("./x:width/text()", ns).Value),
                        Height = int.Parse(nodeIcon.SelectSingleNode("./x:height/text()", ns).Value),
                        Depth = int.Parse(nodeIcon.SelectSingleNode("./x:depth/text()", ns).Value)
                    }
                    );
                }
            }

            node = xml.SelectSingleNode("//x:root/x:specVersion/x:major/text()", ns);
            if (node != null)
                this.SpecVersionMajor = int.Parse(node.Value);

            node = xml.SelectSingleNode("//x:root/x:specVersion/x:minor/text()", ns);
            if (node != null)
                this.SpecVersionMinor = int.Parse(node.Value);


            this.Parsed = true;

            return true;
        }
    }
}
