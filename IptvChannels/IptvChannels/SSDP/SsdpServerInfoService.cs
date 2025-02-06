using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.SSDP
{
    public class SsdpServerInfoService
    {
        public string Type { get; private set; }
        public string ID { get; private set; }
        public string DescriptionURL { get; private set; }
        public string ControlURL { get; private set; }
        public string EventURL { get; private set; }

        public SsdpServerInfoService(string strType, string strID, string strDesciptionUrl, string strControlUrl, string strEventUrl)
        {
            this.Type = strType;
            this.ID = strID;
            this.DescriptionURL = strDesciptionUrl;
            this.ControlURL = strControlUrl;
            this.EventURL = strEventUrl;
        }

        public StringBuilder PrintReport(StringBuilder sb, string strPad)
        {
            sb.Append(strPad).Append("Type: ").AppendLine(this.Type);
            sb.Append(strPad).Append("ID: ").AppendLine(this.ID);
            sb.Append(strPad).Append("Description URL: ").AppendLine(this.DescriptionURL);
            sb.Append(strPad).Append("Control URL: ").AppendLine(this.ControlURL);
            sb.Append(strPad).Append("Event URL: ").AppendLine(this.EventURL);
            return sb;
        }
    }
}
