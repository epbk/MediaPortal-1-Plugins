using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpHeaderField
    {
        public const string EOL = "\r\n";
        public const string HTTP_HEADER_END = EOL + EOL;


        public const string HTTP_USER_AGENT_MOZILLA = "Mozilla/5.0 (Windows NT 6.1; rv:80.0) Gecko/20100101 Firefox/80.0";
        public const string HTTP_USER_AGENT_OPERA = "Opera/9.80 (Windows NT 6.1) Presto/2.12.388 Version/12.18";
        //"application/json, text/javascript, */*; q=0.01, text/html, application/xml;q=0.9, application/xhtml+xml, image/png, image/jpeg, image/gif, image/x-xbitmap, */*;q=0.1";
        public const string HTTP_DEFAULT_ACCEPT = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
        public const string HTTP_DEFAULT_ACCEPT_LANGUAGE = "cs,sk;q=0.8,en-US;q=0.5,en;q=0.3";

        #region Content Type
        public const string HTTP_CONTENT_TYPE_MULTIPART_FORM_DATA = "multipart/form-data; charset=UTF-8";

        public const string HTTP_CONTENT_TYPE_APPLICATION_X_WWW_FORM_URLENCODED = "application/x-www-form-urlencoded; charset=UTF-8";
        public const string HTTP_CONTENT_TYPE_APPLICATION_JSON = "application/json; charset=UTF-8";
        public const string HTTP_CONTENT_TYPE_APPLICATION_XML = "application/xml; charset=UTF-8";
        public const string HTTP_CONTENT_TYPE_APPLICATION_OCTET_STREAM = "application/octet-stream";
        public const string HTTP_CONTENT_TYPE_APPLICATION_X_JAVASCRIPT = "application/x-javascript; charset=UTF-8";
        public const string HTTP_CONTENT_TYPE_APPLICATION_VND_APPLE_MPEGURL = "application/vnd.apple.mpegurl";
        public const string HTTP_CONTENT_TYPE_APPLICATION_RSS = "application/rss+xml; charset=UTF-8";

        public const string HTTP_CONTENT_TYPE_TEXT_HTML = "text/html; charset=UTF-8";
        public const string HTTP_CONTENT_TYPE_TEXT_XML = "text/xml; charset=UTF-8";
        public const string HTTP_CONTENT_TYPE_TEXT_PLAIN = "text/plain; charset=UTF-8";
        public const string HTTP_CONTENT_TYPE_TEXT_CSS = "text/css; charset=UTF-8";
        public const string HTTP_CONTENT_TYPE_TEXT_OPML = "text/x-opml; charset=UTF-8";

        public const string HTTP_CONTENT_TYPE_IMAGE_GIFF = "image/gif";
        public const string HTTP_CONTENT_TYPE_IMAGE_JPG = "image/jpeg";
        public const string HTTP_CONTENT_TYPE_IMAGE_PNG = "image/png";
        public const string HTTP_CONTENT_TYPE_IMAGE_X_ICON = "image/x-icon";
        #endregion

        public const string HTTP_DEFAULT_USER_AGENT = HTTP_USER_AGENT_MOZILLA;
        public const string HTTP_DEFAULT_CONTENT_TYPE = HTTP_CONTENT_TYPE_APPLICATION_X_WWW_FORM_URLENCODED;



        public const string HTTP_FIELD_ACCEPT = "Accept";
        public const string HTTP_FIELD_ACCEPT_ENCODING = "Accept-Encoding";
        public const string HTTP_FIELD_ACCEPT_LANGUAGE = "Accept-Language";

        public const string HTTP_FIELD_HOST = "Host";

        public const string HTTP_FIELD_LOCATION = "Location";

        public const string HTTP_FIELD_REFERER = "Referer";

        public const string HTTP_FIELD_COOKIE = "Cookie";

        public const string HTTP_FIELD_USER_AGENT = "User-Agent";

        public const string HTTP_FIELD_CONTENT_TYPE = "Content-Type";
        public const string HTTP_FIELD_CONTENT_RANGE = "Content-Range";
        public const string HTTP_FIELD_CONTENT_LENGTH = "Content-Length";

        public const string HTTP_FIELD_CONTENT_DISPOSITION = "Content-Disposition";

        public const string HTTP_FIELD_CONTENT_ENCODING = "Content-Encoding";

        public const string HTTP_FIELD_TRANSFER_ENCODING = "Transfer-Encoding";

        public const string HTTP_FIELD_RANGE = "Range";
        public const string HTTP_FIELD_CONNECTION = "Connection";

        public const string HTTP_FIELD_CLOSE = "Close";
        public const string HTTP_FIELD_KEEP_ALIVE = "Keep-Alive";

        public const string HTTP_FIELD_IF_MODIFIED_SINCE = "If-Modified-Since";

        public const string HTTP_FIELD_DO_NOT_TRACK = "DNT";

        public const string HTTP_FIELD_COLON = ": ";
    }
}
