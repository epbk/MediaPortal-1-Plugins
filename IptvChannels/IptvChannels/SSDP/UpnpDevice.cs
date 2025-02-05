using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace MediaPortal.IptvChannels.SSDP
{
    public class UpnpDevice
    {
        /// <summary>
        /// UPnP device type. REQUIRED.
        /// </summary>
        public string DeviceType { get; private set; }

        /// <summary>
        /// Unique Device Name. REQUIRED. 
        /// </summary>
        public Guid Udn { get; private set; }

        /// <summary>
        /// Short description for end user. REQUIRED.
        /// </summary>
        public string FriendlyName { get; private set; }

        /// <summary>
        /// Model name. REQUIRED.
        /// </summary>
        public string ModelName { get; private set; }

        /// <summary>
        /// Model number. RECOMMENDED.
        /// </summary>
        public string ModelNumber { get; private set; }

        /// <summary>
        /// Web site for model. OPTIONAL.
        /// </summary>
        public string ModelUrl { get; private set; }

        /// <summary>
        /// Long description for end user. RECOMMENDED.
        /// </summary>
        public string ModelDescription { get; private set; }

        /// <summary>
        /// Manufacturer's name.
        /// </summary>
        public string Manufacturer { get; private set; }

        /// <summary>
        /// Serial number. RECOMMENDED.
        /// </summary>
        public string SerialNumber { get; private set; }

        /// <summary>
        /// Web site for Manufacturer. OPTIONAL.
        /// </summary>
        public string ManufacturerUrl { get; private set; }

        /// <summary>
        /// Specifies the configuration number to which the device description belongs. REQUIRED.
        /// </summary>
        public int ConfigID { get; private set; }

        /// <summary>
        /// Universal Product Code. OPTIONAL.
        /// </summary>
        public string UPC { get; private set; }

        /// <summary>
        /// URL to presentation for device. RECOMMENDED.
        /// </summary>
        public string PresentationUrl { get; private set; }

        public string AdditionalSsdpNotify { get; private set; }
        public SsdpServerInfoIcon[] Icons { get; private set; }

        public IEnumerable<UpnpService> Services => this._Services;

        /// <summary>
        /// XML description data served on HttpDescriptionPath.
        /// </summary>
        public byte[] Description { get; private set; }

        /// <summary>
        /// Http server port.
        /// </summary>
        public int HttpServerPort { get; private set; }

        /// <summary>
        /// Http description xml path.
        /// </summary>
        public string HttpDescriptionPath { get; private set; }

        /// <summary>
        /// Number increased each time device sends an initial announce or an update message.
        /// </summary>
        public int BootID { get; private set; }

        protected readonly UpnpService[] _Services;
        protected readonly UpnpDevice[] _EmbedDevices;


        public UpnpDevice(Guid udn, string strDevType, int iServerPort, string strDescriptionPath,
            string strFriendlyName, string strManufacturer, string strModelName, int iConfigID = 0,
            string strModelDescription = null, string strModelNumber = null, string strModelURL = null, string strSerialNumber = null,
            string strManufacturerURL = null, string strUPC = null, SsdpServerInfoIcon[] icons = null, string strPresentationURL = null,
            string strAdditionalSsdpNotify = null, UpnpService[] services = null, UpnpDevice[] embedDevices = null)
        {
            this.Udn = udn;
            this.DeviceType = strDevType;
            this.HttpServerPort = iServerPort;
            this.HttpDescriptionPath = strDescriptionPath;
            this.FriendlyName = strFriendlyName;
            this.Manufacturer = strManufacturer;
            this.ModelName = strModelName;
            this.ConfigID = iConfigID;
            this.ModelDescription = strModelDescription;
            this.ModelNumber = strModelNumber;
            this.ModelUrl = strModelURL;
            this.SerialNumber = strSerialNumber;
            this.ManufacturerUrl = strManufacturerURL;
            this.UPC = strUPC;
            this.Icons = icons;
            this.PresentationUrl = strPresentationURL;
            this.AdditionalSsdpNotify = strAdditionalSsdpNotify;
            this._Services = services ?? new UpnpService[] { };
            this._EmbedDevices = embedDevices;

            this.BootID = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;

            this.initDescription();
        }

        private void initDescription()
        {
            MemoryStream ms = new MemoryStream();
            using (XmlTextWriter wr = new XmlTextWriter(ms, new UTF8Encoding(false)))
            {
                wr.Formatting = Formatting.Indented;

                //REQUIRED for all XML documents. Case sensitive.
                wr.WriteRaw("<?xml version=\"1.0\"?>");

                //REQUIRED. MUST have “urn:schemas-upnp-org:device-1-0” as the value for the xmlns attribute
                wr.WriteStartElement("root", "urn:schemas-upnp-org:device-1-0");

                //REQUIRED. Specifies the configuration number to which the device description belongs.
                wr.WriteAttributeString("configId", this.ConfigID.ToString());

                //REQUIRED. In device templates, defines the lowest version of the architecture on which the device can be implemented.
                wr.WriteStartElement("specVersion");

                //REQUIRED. Major version of the UPnP Device Architecture. MUST be 1 for devices implemented on a UPnP 1.1 architecture.
                wr.WriteElementString("major", "1");

                //REQUIRED. Minor version of the UPnP Device Architecture. MUST be 1 for devices implemented on a UPnP 1.1 architecture.
                wr.WriteElementString("minor", "1");
                wr.WriteEndElement();

                //REQUIRED.
                writeDevice(wr, this);

                wr.WriteEndElement(); //<root>
                wr.Flush();

                this.Description = ms.ToArray();
            }
        }

        private static void writeDevice(XmlTextWriter wr, UpnpDevice dev)
        {
            wr.WriteStartElement("device");

            //REQUIRED. UPnP device type.
            wr.WriteElementString("deviceType", dev.DeviceType);

            //REQUIRED. Short description for end user.
            wr.WriteElementString("friendlyName", dev.FriendlyName);

            //REQUIRED.Manufacturer's name.
            wr.WriteElementString("manufacturer", dev.Manufacturer);

            //OPTIONAL.Web site for Manufacturer.
            if (!string.IsNullOrWhiteSpace(dev.ManufacturerUrl))
                wr.WriteElementString("manufacturerURL", dev.ManufacturerUrl);

            //RECOMMENDED. Long description for end user.
            if (!string.IsNullOrWhiteSpace(dev.ModelDescription))
                wr.WriteElementString("modelDescription", dev.ModelDescription);

            //REQUIRED.Model name.
            wr.WriteElementString("modelName", dev.ModelName);

            //RECOMMENDED. Model number.
            if (!string.IsNullOrWhiteSpace(dev.ModelNumber))
                wr.WriteElementString("modelNumber", dev.ModelNumber);

            //OPTIONAL. Web site for model.
            if (!string.IsNullOrWhiteSpace(dev.ModelUrl))
                wr.WriteElementString("modelURL", dev.ModelUrl);

            //RECOMMENDED. Serial number.
            if (!string.IsNullOrWhiteSpace(dev.SerialNumber))
                wr.WriteElementString("serialNumber", dev.SerialNumber);

            //REQUIRED. Unique Device Name. MUST begin with “uuid:” followed by a UUID suffix specified by a UPnP vendor.
            wr.WriteElementString("UDN", "uuid:" + dev.Udn);

            //OPTIONAL. Universal Product Code. 12-digit, all-numeric code that identifies the consumer package.
            if (!string.IsNullOrWhiteSpace(dev.ModelUrl))
                wr.WriteElementString("UPC", dev.ModelUrl);

            if (dev.Icons?.Length > 0)
            {
                //REQUIRED if and only if device has one or more icons.Specified by UPnP vendor. Contains the following sub elements:
                wr.WriteStartElement("iconList");

                for (int i = 0; i < dev.Icons.Length; i++)
                {
                    SsdpServerInfoIcon icon = dev.Icons[i];

                    //RECOMMENDED.Icon to depict device in a control point UI. Contains the following sub elements: 
                    wr.WriteStartElement("icon");

                    //REQUIRED.Icon's MIME type (see RFC 2045, 2046, and 2387).
                    wr.WriteElementString("mimetype", icon.MimeType);

                    //REQUIRED.Horizontal dimension of icon in pixels.Integer.
                    wr.WriteElementString("width", icon.Width.ToString());

                    //REQUIRED.Vertical dimension of icon in pixels.Integer.
                    wr.WriteElementString("height", icon.Height.ToString());

                    //REQUIRED.Number of color bits per pixel.Integer.
                    wr.WriteElementString("depth", icon.Depth.ToString());

                    //REQUIRED.Pointer to icon image.
                    wr.WriteElementString("url", icon.Url);

                    wr.WriteEndElement(); //<icon>
                }
                wr.WriteEndElement(); //<iconList>
            }

            writeServices(wr, dev._Services);
            writeDevices(wr, dev._EmbedDevices);

            //RECOMMENDED. URL to presentation for device.
            if (!string.IsNullOrWhiteSpace(dev.PresentationUrl))
                wr.WriteElementString("presentationURL", dev.PresentationUrl);

            wr.WriteEndElement(); //<device>
        }

        private static void writeServices(XmlTextWriter wr, UpnpService[] services)
        {
            //OPTIONAL
            if (services?.Length > 0)
            {
                wr.WriteStartElement("serviceList");

                for (int i = 0; i < services.Length; i++)
                {
                    UpnpService srv = services[i];

                    wr.WriteStartElement("service");

                    //Required. UPnP service type.
                    wr.WriteElementString("serviceType", srv.ServiceType);

                    //REQUIRED.Service identifier.
                    wr.WriteElementString("serviceId", srv.ServiceID);

                    //REQUIRED. URL for service description.
                    wr.WriteElementString("SCPDURL", srv.ServiceDescriptionURL);

                    //REQUIRED.URL for control.
                    wr.WriteElementString("controlURL", srv.ServiceControlURL);

                    //REQUIRED.URL for eventing.
                    wr.WriteElementString("eventSubURL", srv.ServiceEventURL);

                    wr.WriteEndElement(); //<service>
                }

                wr.WriteEndElement(); //<serviceList>
            }
        }

        private static void writeDevices(XmlTextWriter wr, UpnpDevice[] devices)
        {
            //OPTIONAL
            if (devices?.Length > 0)
            {
                wr.WriteStartElement("deviceList");

                for (int i = 0; i < devices.Length; i++)
                    writeDevice(wr, devices[i]);

                wr.WriteEndElement(); //<deviceList>
            }
        }
    }
}
