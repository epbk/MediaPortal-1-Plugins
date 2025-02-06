using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NLog;

namespace MediaPortal.IptvChannels.SSDP
{
    public class SsdpServerInfoDevice
    {
        private List<SsdpServerInfoIcon> _Icons = new List<SsdpServerInfoIcon>();
        private List<SsdpServerInfoService> _Services = new List<SsdpServerInfoService>();
        private List<SsdpServerInfoDevice> _Devices = new List<SsdpServerInfoDevice>();

        /// <summary>
        /// UPnP device type. Single URI.
        /// </summary>
        public string DeviceType { get; private set; }

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
        /// Universal Product Code.
        /// </summary>
        public string UPC { get; private set; }

        /// <summary>
        /// Icon list.
        /// </summary>
        public IEnumerable<SsdpServerInfoIcon> Icons => this._Icons;

        /// <summary>
        /// Service list.
        /// </summary>
        public IEnumerable<SsdpServerInfoService> Services => this._Services;

        /// <summary>
        /// Embed device list.
        /// </summary>
        public IEnumerable<SsdpServerInfoDevice> Devices => this._Devices;

        public SsdpServerInfoDevice(XmlNode nodeDev, string strNS)
        {
            this.PresentationUrl = nodeDev["presentationURL", strNS]?.InnerText;
            this.FriendlyName = nodeDev["friendlyName", strNS]?.InnerText;
            this.Manufacturer = nodeDev["manufacturer", strNS]?.InnerText;
            this.ManufacturerUrl = nodeDev["manufacturerURL", strNS]?.InnerText;
            this.ModelDescription = nodeDev["modelDescription", strNS]?.InnerText;
            this.ModelName = nodeDev["modelName", strNS]?.InnerText;
            this.ModelNumber = nodeDev["modelNumber", strNS]?.InnerText;
            this.ModelUrl = nodeDev["modelURL", strNS]?.InnerText;
            this.SerialNumber = nodeDev["serialNumber", strNS]?.InnerText;
            this.DeviceType = nodeDev["deviceType", strNS]?.InnerText;
            this.UDN = nodeDev["UDN", strNS]?.InnerText;
            this.UPC = nodeDev["UPC", strNS]?.InnerText;

            XmlNode nodes = nodeDev["iconList", strNS];
            if (nodes != null)
            {
                this._Icons.Clear();
                foreach (XmlNode node in nodes.ChildNodes)
                {
                    if (node.Name == "icon" && node.NamespaceURI == strNS)
                    {
                        this._Icons.Add(new SsdpServerInfoIcon(
                            node["mimetype", strNS].InnerText,
                            node["url", strNS].InnerText,
                            int.Parse(node["width", strNS].InnerText),
                            int.Parse(node["height", strNS].InnerText),
                            int.Parse(node["depth", strNS].InnerText)
                        ));
                    }
                }
            }

            nodes = nodeDev["serviceList", strNS];
            if (nodes != null)
            {
                this._Services.Clear();
                foreach (XmlNode node in nodes.ChildNodes)
                {
                    if (node.Name == "service" && node.NamespaceURI == strNS)
                    {
                        this._Services.Add(new SsdpServerInfoService(
                            node["serviceType", strNS].InnerText,
                            node["serviceId", strNS].InnerText,
                            node["SCPDURL", strNS].InnerText,
                            node["controlURL", strNS].InnerText,
                            node["eventSubURL", strNS].InnerText
                        ));
                    }
                }
            }

            nodes = nodeDev["deviceList", strNS];
            if (nodes != null)
            {
                this._Services.Clear();
                foreach (XmlNode node in nodes.ChildNodes)
                {
                    if (node.Name == "device" && node.NamespaceURI == strNS)
                        this._Devices.Add(new SsdpServerInfoDevice(node, strNS));
                }
            }
        }

        public StringBuilder PrintReport(StringBuilder sb, string strPad)
        {
            sb.Append(strPad).Append("FriendlyName: ").AppendLine(this.FriendlyName);
            sb.Append(strPad).Append("DeviceType: ").AppendLine(this.DeviceType);
            sb.Append(strPad).Append("UDN: ").AppendLine(this.UDN);
            sb.Append(strPad).Append("Manufacturer: ").AppendLine(this.Manufacturer);
            sb.Append(strPad).Append("ManufacturerURL: ").AppendLine(this.ManufacturerUrl);
            sb.Append(strPad).Append("ModelDescription: ").AppendLine(this.ModelDescription);
            sb.Append(strPad).Append("ModelName: ").AppendLine(this.ModelName);
            sb.Append(strPad).Append("ModelNumber: ").AppendLine(this.ModelNumber);
            sb.Append(strPad).Append("ModelURL: ").AppendLine(this.ModelUrl);
            sb.Append(strPad).Append("SerialNumber: ").AppendLine(this.SerialNumber);
            sb.Append(strPad).Append("PresentationURL: ").AppendLine(this.PresentationUrl);
            sb.Append(strPad).Append("UPC: ").AppendLine(this.UPC);

            this._Services.ForEach(svc =>
            {
                sb.Append(strPad).AppendLine("Service: ");
                svc.PrintReport(sb, strPad + ' ');
            });
            return sb;
        }
    }
}
