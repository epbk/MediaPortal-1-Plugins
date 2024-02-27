using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class ContentProtection
    {
        private Regex _RegexInit = null;
        private Regex _RegexMedia = null;

        public string RepresentationID;
        public string PSSH;
        public string KID;
        public string DecryptionKey;
        public string SegmentTemplateMedia;
        public string SegmentTemplateInit;

        public string InitFileFullPath = null;

        public ManualResetEvent FlagInitComplete = new ManualResetEvent(false);

        public bool IsMatch(string strPath, out bool bIsInit)
        {
            bIsInit = false;

            if (this._RegexMedia == null && !string.IsNullOrWhiteSpace(this.SegmentTemplateMedia))
                this._RegexMedia = createRegex(this.SegmentTemplateMedia);

            if (this._RegexMedia != null && this._RegexMedia.Match(strPath).Success)
            {
                return true;
            }


            if (this._RegexInit == null && !string.IsNullOrWhiteSpace(this.SegmentTemplateInit))
                this._RegexInit = createRegex(this.SegmentTemplateInit);

            if (this._RegexInit != null && this._RegexInit.Match(strPath).Success)
            {
                bIsInit = true;
                return true;
            }

            return false;
        }

        private static Regex createRegex(string strPattern)
        {
            //$RepresentationID$
            //$Number$

            strPattern = Tools.RegularExpressions.Escape(strPattern);
            strPattern = strPattern.Replace("\\$RepresentationID\\$", "(?<rid>[^/]+)");
            strPattern = strPattern.Replace("\\$Number\\$", "(?<nr>[^/]+)");
            strPattern += "\\z";

            return new Regex(strPattern, RegexOptions.Compiled);
        }
    }
}
