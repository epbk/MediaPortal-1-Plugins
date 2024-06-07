using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.IptvChannels
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class HttpUserWebRequestArgumentsWrapper
    {
        private readonly Pbk.Net.Http.HttpUserWebRequestArguments _Args;

        public string UserAgent
        {
            get { return this._Args.UserAgent; }
            set { this._Args.UserAgent = value; }
        }

        public string Accept
        {
            get { return this._Args.Accept; }
            set { this._Args.Accept = value; }
        }

        public string AcceptLanguage
        {
            get { return this._Args.AcceptLanguage; }
            set { this._Args.AcceptLanguage = value; }
        }

        public string ContentType
        {
            get { return this._Args.ContentType; }
            set { this._Args.ContentType = value; }
        }

        public string Referer
        {
            get { return this._Args.Referer; }
            set { this._Args.Referer = value; }
        }

        [EditorAttribute(typeof(Controls.UIEditor.HttpCookiesUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public NameValueCollection Cookies
        {
            get { return this._Args.Cookies; }
            set { this._Args.Cookies = value; }
        }

        [EditorAttribute(typeof(Controls.UIEditor.HttpFieldsUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public NameValueCollection Fields
        {
            get { return this._Args.Fields; }
            set { this._Args.Fields = value; }
        }

        

        public HttpUserWebRequestArgumentsWrapper(Pbk.Net.Http.HttpUserWebRequestArguments args)
        {
            this._Args = args;
        }

        public string Serialize()
        {
            return this._Args.Serialize();
        }

        public override string ToString()
        {
            if (this._Args != null &&
                (!string.IsNullOrEmpty(this._Args.UserAgent)
                || !string.IsNullOrEmpty(this._Args.Accept)
                || !string.IsNullOrEmpty(this._Args.AcceptLanguage)
                || !string.IsNullOrEmpty(this._Args.ContentType)
                || !string.IsNullOrEmpty(this._Args.Referer)
                || (this._Args.Fields != null && this._Args.Fields.Count > 0)
                || (this._Args.Cookies != null && this._Args.Cookies.Count > 0))
                )
                return "Arguments";
            else
                return string.Empty;
        }
    }
}
