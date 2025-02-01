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
        public string DeviceType;
        public Guid Udn;
        public int ServerPort;
        public string FriendlyName;
        public string ModelName;
        public string ModelNumber;
        public string ModelUrl;
        public string ModelDescription;
        public string Manufacturer;
        public string SerialNumber;
        public string ManufacturerUrl;
        public int BootID = 2318;
        public int ConfigID = 0;
        public string UPC;
        public string PresentationUrl;
        public string AdditionalSsdpNotify = null;
        public SsdpServerInfoIcon[] Icons;
        public byte[] Description;

        public void InitDescription()
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
                wr.WriteStartElement("device");
                this.WriteDescription(wr);
                wr.WriteEndElement(); //<device>

                wr.WriteEndElement(); //<root>
                wr.Flush();

                this.Description = ms.ToArray();
            }
        }


        public void WriteDescription(XmlTextWriter wr)
        {
            //REQUIRED. UPnP device type.
            wr.WriteElementString("deviceType", this.DeviceType);

            //REQUIRED. Short description for end user.
            wr.WriteElementString("friendlyName", this.FriendlyName);

            //REQUIRED.Manufacturer's name.
            wr.WriteElementString("manufacturer", this.Manufacturer);

            //OPTIONAL.Web site for Manufacturer.
            if (!string.IsNullOrWhiteSpace(this.ManufacturerUrl))
                wr.WriteElementString("manufacturerURL", this.ManufacturerUrl);

            //RECOMMENDED. Long description for end user.
            if (!string.IsNullOrWhiteSpace(this.ModelDescription))
                wr.WriteElementString("modelDescription", this.ModelDescription);

            //REQUIRED.Model name.
            wr.WriteElementString("modelName", this.ModelName);

            //RECOMMENDED. Model number.
            if (!string.IsNullOrWhiteSpace(this.ModelNumber))
                wr.WriteElementString("modelNumber", this.ModelNumber);

            //OPTIONAL. Web site for model.
            if (!string.IsNullOrWhiteSpace(this.ModelUrl))
                wr.WriteElementString("modelURL", this.ModelUrl);

            //RECOMMENDED. Serial number.
            if (!string.IsNullOrWhiteSpace(this.SerialNumber))
                wr.WriteElementString("serialNumber", this.SerialNumber);

            //REQUIRED. Unique Device Name. MUST begin with “uuid:” followed by a UUID suffix specified by a UPnP vendor.
            wr.WriteElementString("UDN", "uuid:" + this.Udn);

            //OPTIONAL. Universal Product Code. 12-digit, all-numeric code that identifies the consumer package.
            if (!string.IsNullOrWhiteSpace(this.ModelUrl))
                wr.WriteElementString("UPC", this.ModelUrl);

            if (this.Icons?.Length > 0)
            {
                //REQUIRED if and only if device has one or more icons.Specified by UPnP vendor. Contains the following sub elements:
                wr.WriteStartElement("iconList");

                for (int i = 0; i < this.Icons.Length; i++)
                {
                    SsdpServerInfoIcon icon = this.Icons[i];

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

            this.WriteServices(wr);
            this.WriteDevices(wr);

            //RECOMMENDED. URL to presentation for device.
            if (!string.IsNullOrWhiteSpace(this.PresentationUrl))
                wr.WriteElementString("presentationURL", this.PresentationUrl);
        }

        public virtual void WriteServices(XmlTextWriter wr)
        {
            /*
           OPTIONAL. Contains the following sub elements:
           <serviceList>

               OPTIONAL.Repeated once for each service defined by a UPnP Forum working committee. Contains the following sub elements:
               <service>

                   Required. UPnP service type.
                   <serviceType>

                   <serviceId>
                   REQUIRED. Service identifier.

                   <SCPDURL>
                   REQUIRED. URL for service description.

                   <controlURL>
                   REQUIRED. URL for control.

                   <eventSubURL>
                   REQUIRED. URL for eventing.
           */
        }

        public virtual void WriteDevices(XmlTextWriter wr)
        {
            /*
           REQUIRED if and only if root device has embedded devices. Contains the following sub elements:
           <deviceList>

               REQUIRED. Repeat once for each embedded device defined by a UPnP Forum working committee.
               <device>
           */
        }
    }
}
