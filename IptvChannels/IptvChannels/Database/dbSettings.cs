using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Drawing;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Reflection;
using MediaPortal.Pbk.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using NLog;

namespace MediaPortal.IptvChannels.Database
{
    [DBTableAttribute("settings")]
    public class dbSettings : DbTable
    {
        public const int DATABASE_VERSION_CURRENT = 1;
        
        public const int HTTP_SERVER_PORT_DEFAULT = 8100;

        public static readonly string TEMP_PATH_DEFAULT = System.IO.Path.GetTempPath();

        public const int TIMEOUT_PERIOD_NO_CLIENTS = 15000;  //[ms]
        public const int TIMEOUT_PERIOD_NO_DATA = 20000;  //[ms]

        public const int PACKET_BUFFSIZE = 1024 * 256; // packet buffer in bytes

        #region Database fields
        [DBFieldAttribute(FieldName = "dbVersion", Default = "1")]
        [Browsable(false)]
        [Category("Database")]
        public int DatabaseVersion
        { get; set; }

        [Category("Plugin")]
        [DisplayName("Wakeup For Epg Grabbing")]
        [DBFieldAttribute(FieldName = "wakeupForEpgGrabbing", Default = "False")]
        [DefaultValue(false)]
        [EditorAttribute(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public bool WakeupForEpgGrabbing
        { get; set; }

        [Category("Plugin")]
        [DisplayName("Delete Unreferenced Channels")]
        [DBFieldAttribute(FieldName = "deleteUnreferencedChannels", Default = "True")]
        [DefaultValue(true)]
        [EditorAttribute(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public bool DeleteUnreferencedChannels
        { get; set; }

        

        #region System

        #endregion

        #region Http

        [DBFieldAttribute(FieldName = "useOpenSsl", Default = "False")]
        [DisplayName("Use Open SSL")]
        [DefaultValue(false)]
        [Category("Http")]
        [EditorAttribute(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public bool UseOpenSsl
        {
            get { return this._UseOpenSsl; }
            set { this._UseOpenSsl = value; Pbk.Net.Http.HttpUserWebRequest.UseOpenSSLDefault = value; }
        }private bool _UseOpenSsl = false;

        [DBFieldAttribute(FieldName = "allowSystemProxy", Default = "True")]
        [DisplayName("Allow system proxy")]
        [DefaultValue(true)]
        [Category("Http")]
        [EditorAttribute(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public bool AllowSystemProxy
        {
            get { return this._AllowSystemProxy; }
            set { this._AllowSystemProxy = value; Pbk.Net.Http.HttpUserWebRequest.AllowSystemProxyDefault = value; }
        }private bool _AllowSystemProxy = true;

        [DBFieldAttribute(FieldName = "httpServerPort", Default = "8100")]
        [DisplayName("Http server port")]
        [DefaultValue(HTTP_SERVER_PORT_DEFAULT)]
        [Category("Http")]
        public int HttpServerPort
        { get; set; }

        #endregion

        #region UI
        //[DBFieldAttribute(FieldName = "uiPropertyGridDisabledItemForeColor", Default = "GrayText")]
        //[Category("UI")]
        //[DisplayName("PropertyGrid: Disabled Item Fore Color")]
        //[DefaultValue(typeof(Color), "GrayText")]
        //public Color PropertyGridDisabledItemForeColor
        //{ get; set; }

        #endregion

        #region Streaming

        [DBFieldAttribute(FieldName = "proxTempPath", Default = "")]
        [Description("Proxy temporary path.")]
        [DisplayName("Path: temporary")]
        [Category("Streaming")]
        [EditorAttribute(typeof(Pbk.Controls.UIEditor.SelectDirectoryUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string TempPath
        {
            get
            {
                return this._TempPath;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    value = TEMP_PATH_DEFAULT;

                value = value.Trim();

                if (!value.EndsWith("\\"))
                    value += '\\';

                if (!System.IO.Directory.Exists(value))
                    this._TempPath = TEMP_PATH_DEFAULT;
                else
                    this._TempPath = value;

                this._WorkPath = this._TempPath + "MediaPortalIptvChannels\\";
            }
        }private string _TempPath = TEMP_PATH_DEFAULT;

        [Browsable(false)]
        public string WorkPath
        {
            get
            {
                if (!System.IO.Directory.Exists(this._WorkPath))
                {
                    try { System.IO.Directory.CreateDirectory(this._WorkPath); }
                    catch
                    {
                        this._TempPath = System.IO.Path.GetTempPath();
                        this._WorkPath = this._TempPath + "MediaPortalIptvChannels\\";
                    }
                }

                return this._WorkPath;
            }
        }private string _WorkPath = TEMP_PATH_DEFAULT + "MediaPortalIptvChannels\\";

        [DBFieldAttribute(FieldName = "proxyTimeoutNoClients", Default = "15000")]
        [Description("Timeout - no clients.")]
        [DisplayName("Timeout: no clients")]
        [DefaultValue(TIMEOUT_PERIOD_NO_CLIENTS)]
        [Category("Streaming")]
        [TypeConverter(typeof(Controls.UIEditor.TimePeriodConverter))]
        public int TimeoutNoClients
        {
            get
            {
                return this._TimeoutNoClients;
            }
            set
            {
                if (value > 60000) 
                    this._TimeoutNoClients = 60000;
                else if (value < 100) 
                    this._TimeoutNoClients = 100;
                else 
                    this._TimeoutNoClients = value;
            }
        }private int _TimeoutNoClients = TIMEOUT_PERIOD_NO_CLIENTS;  //[ms]

        [DBFieldAttribute(FieldName = "proxyTimeoutNoData", Default = "20000")]
        [Description("Timeout - no data.")]
        [DisplayName("Timeout: no data")]
        [DefaultValue(TIMEOUT_PERIOD_NO_DATA)]
        [Category("Streaming")]
        [TypeConverter(typeof(Controls.UIEditor.TimePeriodConverter))]
        public int TimeoutNoData
        {
            get
            {
                return this._TimeoutNoData;
            }
            set
            {
                if (value > 60000) 
                    this._TimeoutNoData = 60000;
                else if (value < 100)
                    this._TimeoutNoData = 100;
                else
                    this._TimeoutNoData = value;
            }
        }private int _TimeoutNoData = TIMEOUT_PERIOD_NO_DATA;  //[ms]

        [DBFieldAttribute(FieldName = "proxyClientMemoryBufferSize", Default = "2097152")]
        [Description("Client memory buffer size.")]
        [DisplayName("Client memory buffer size")]
        [DefaultValue(Proxy.RemoteClient.DEFAULT_BUFFER_SIZE)]
        [Category("Streaming")]
        [TypeConverter(typeof(Controls.UIEditor.FileSizeConverter))]
        public int ClientMemoryBufferSize
        {
            get
            {
                return this._ClientMemoryBufferSize;
            }
            set
            {
                if (value < PACKET_BUFFSIZE)
                    this._ClientMemoryBufferSize = PACKET_BUFFSIZE;
                else if (value > 64 * 1024 * 1024)
                    this._ClientMemoryBufferSize = 64 * 1024 * 1024;
                else
                    this._ClientMemoryBufferSize = value / (256 * 1024) * (256 * 1024);
            }
        }private int _ClientMemoryBufferSize = Proxy.RemoteClient.DEFAULT_BUFFER_SIZE;

        #endregion

        #region MediaServer
        [DBFieldAttribute(FieldName = "mediaServerMaxSimultaneousDownloads", Default = "2")]
        [DefaultValue(2)]
        [Description("Maximum simultaneous downloads per application. Unlimited: < 1")]
        [Category("Media Server")]
        [DisplayName("Max simultaneous downloads")]
        public int MediaServerMaxSimultaneousDownloads
        {
            get { return this._MediaServerMaxSimultaneousDownloads; }
            set
            {
                if (value < 1)
                    this._MediaServerMaxSimultaneousDownloads = -1;
                else if (value > 10)
                    this._MediaServerMaxSimultaneousDownloads = 10;
                else
                    this._MediaServerMaxSimultaneousDownloads = value;
            }
        }private int _MediaServerMaxSimultaneousDownloads = 2;

        [DBFieldAttribute(FieldName = "mediaServerMaxSimultaneousDownloadsPerTask", Default = "2")]
        [DefaultValue(2)]
        [Description("Maximum simultaneous downloads per task. Unlimited: < 1")]
        [Category("Media Server")]
        [DisplayName("Max simultaneous downloads per task")]
        public int MediaServerMaxSimultaneousDownloadsPerTask
        {
            get { return this._MediaServerMaxSimultaneousDownloadsPerTask; }
            set
            {
                if (value < 1)
                    this._MediaServerMaxSimultaneousDownloadsPerTask = -1;
                else if (value > 10)
                    this._MediaServerMaxSimultaneousDownloadsPerTask = 10;
                else
                    this._MediaServerMaxSimultaneousDownloadsPerTask = value;
            }
        }private int _MediaServerMaxSimultaneousDownloadsPerTask = 2;

        [DBFieldAttribute(FieldName = "mediaServerDownloadOnRequest", Default = "True")]
        [DefaultValue(true)]
        [Description("True: chunk will start download upon http request.\r\nFalse: chunk will start download immediately.")]
        [Category("Media Server")]
        [DisplayName("Begin download on request")]
        [EditorAttribute(typeof(Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public bool MediaServerBeginDownloadOnRequest
        { get; set; }

        [DBFieldAttribute(FieldName = "mediaServerAutoterminatePeriod", Default = "60000")]
        [DefaultValue(60000)]
        [Description("Task/Segment autoterminate period.")]
        [Category("Media Server")]
        [DisplayName("Autoterminate period")]
        [TypeConverter(typeof(Controls.UIEditor.TimePeriodConverter))]
        public int MediaServerAutoterminatePeriod
        {
            get { return this._MediaServerAutoterminatePeriod; }
            set
            {
                if (value < 5000)
                    this._MediaServerAutoterminatePeriod = 5000;
                else
                    this._MediaServerAutoterminatePeriod = value;
            }
        }private int _MediaServerAutoterminatePeriod = 60000;

        #endregion


        #endregion

        public static dbSettings Instance
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                if (_Instance == null)
                {
                    _Instance = (dbSettings)Manager.Get(typeof(dbSettings), 1);

                    if (_Instance == null)
                    {
                        _Instance = new dbSettings();
                        _Instance.Commit();
                    }
                }

                return _Instance;

            }
        }private static dbSettings _Instance = null;



        public StringBuilder SerializeJson(StringBuilder sb)
        {
            //Copy properties
            IEnumerable<PropertyInfo> props = this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(prop =>
            {
                if (!prop.CanWrite)
                    return false; //read only

                if (!Attribute.IsDefined(prop, typeof(DBFieldAttribute)))
                    return false;

                return !Attribute.IsDefined(prop, typeof(BrowsableAttribute)) ||
                        Attribute.GetCustomAttributes(prop, typeof(BrowsableAttribute), true).Contains(BrowsableAttribute.Yes);
            });

            sb.Append('{');
            foreach (PropertyInfo pi in props)
            {
                object o = pi.GetValue(this, null);

                if (sb[sb.Length - 1] != '{')
                    sb.Append(',');

                sb.Append('\"');
                sb.Append(pi.Name);
                sb.Append("\":\"");
                Tools.Json.AppendAndValidate(o.ToString(), sb);
                sb.Append('\"');
            }
            sb.Append('}');

            return sb;
        }

        public void DeserializeFromJson(JToken j)
        {
            Type t = this.GetType();
            foreach (JToken jItem in j.Children())
            {
                PropertyInfo p = t.GetProperty(((JProperty)jItem).Name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null)
                    p.SetValue(this, Convert.ChangeType((string)jItem, p.PropertyType), null);
            }
        }

    }
}
