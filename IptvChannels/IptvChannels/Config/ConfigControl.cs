using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Drawing;
using TvDatabase;
using TvEngine.PowerScheduler;
using TvLibrary.Interfaces;
using MediaPortal.IptvChannels.Proxy;
using MediaPortal.IptvChannels.Proxy.MediaServer;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using MediaPortal.Pbk.Extensions;
using NLog;

namespace SetupTv.Sections
{
    public partial class Setup : SectionSettings, IDisposable
    {
        #region Constants
        private const int _COLUMN_STREAM_INDEX_ID = 0;
        private const int _COLUMN_STREAM_INDEX_URL = 1;
        private const int _COLUMN_STREAM_INDEX_INFO = 2;
        private const int _COLUMN_STREAM_INDEX_CLIENTS = 3;

        private const int _COLUMN_CDN_INDEX_ID = 0;
        private const int _COLUMN_CDN_INDEX_TITLE = 1;
        private const int _COLUMN_CDN_INDEX_URL = 2;
        private const int _COLUMN_CDN_INDEX_STATUS = 3;

        private const int _ICON_IDX_ERROR = 0;
        private const int _ICON_IDX_DONE = 1;
        private const int _ICON_IDX_IDDLE = 2;
        private const int _ICON_IDX_RUN = 3;
        private const int _ICON_IDX_DOWNLOAD = 4;
        private const int _ICON_IDX_DISABLED = 5;
        private const int _ICON_IDX_SCHEDULE = 6;
        private const int _ICON_IDX_FOLDER = 7;
        private const int _ICON_IDX_FOLDER_OPEN = 8;
        private const int _ICON_IDX_WARN = 9;
        private const int _ICON_IDX_SCHEDULE_DISABLED = 10;
        private const int _ICON_IDX_STOPPING = 11;
        #endregion

        #region Types
        #endregion

        #region Variables
        private bool _Initialized = false;

        private bool _SaveRq = false;

        private MediaPortal.IptvChannels.Plugin _Plugin = null;

        private MediaPortal.Pbk.Net.Http.HttpUserWebRequest _Connection;
        private byte[] _ConnectionData;
        private int _LastEventId = -1;

        private static Logger _Logger;

        #endregion

        #region ctor
        static Setup()
        {
            MediaPortal.IptvChannels.Log.AddRule("SetupTv.Sections.*");
            _Logger = LogManager.GetCurrentClassLogger();
        }

        public Setup(MediaPortal.IptvChannels.Plugin plugin)
            : this("IptvChannels", plugin) { }

        public Setup(string name, MediaPortal.IptvChannels.Plugin plugin)
            : base(name)
        {
            try
            {
                this.InitializeComponent();

                this.dataGridView_ConList.MainColumnIndex = _COLUMN_STREAM_INDEX_INFO;
                this.dataGridViewCDN.MainColumnIndex = _COLUMN_CDN_INDEX_TITLE;

                this._Plugin = plugin;

                this.HandleCreated += this.windowHandleCreated;
                
                this.propertyGridSettings.SelectedObject = plugin.Settings;

                //Load plugin combobox
                foreach (MediaPortal.IptvChannels.SiteUtils.SiteUtilBase site in this._Plugin.Sites)
                {
                    this.comboBox_Sites.Items.Add(site);
                }

                if (this.comboBox_Sites.Items.Count > 0)
                {
                    this.comboBox_Sites.SelectedIndex = 0;
                    this.propertyGridPlugins.SelectedObject = this._Plugin.Sites[0];
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[Setup] ctor Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            //this.propertyGrid.BrowsableAttributes = new System.ComponentModel.AttributeCollection(new CategoryAttribute("IptvChannelsUserConfiguration"));
        }
        #endregion

        #region Overrides
        public override void OnSectionActivated()
        {
            Initialize();

            base.OnSectionActivated();
        }

        public override void OnSectionDeActivated()
        {
            base.OnSectionDeActivated();
        }

        public override void LoadSettings()
        {
            base.LoadSettings();

            Initialize();

        }

        public override void SaveSettings()
        {
            if (this._SaveRq)
            {
                this._Plugin.PluginSettings.Save();

                MediaPortal.Pbk.Net.Http.HttpUserWebRequest wr = new MediaPortal.Pbk.Net.Http.HttpUserWebRequest("http://127.0.0.1:" + this._Plugin.Settings.HttpServerPort 
                    + MediaPortal.IptvChannels.Plugin.HTTP_PATH_APPLY_SETTINGS);
                wr.Post = Encoding.UTF8.GetBytes(this._Plugin.Settings.SerializeJson(new StringBuilder(1024)).ToString());
                wr.Download<string>();    
            }

            base.SaveSettings();
        }
        #endregion

        #region Private

        private void Initialize()
        {
            if (!_Initialized)
            {
            }
            _Initialized = true;
        }

        #endregion

        #region Callbacks

        private void windowHandleCreated(object sender, EventArgs e)
        {
            //Hook to the plugin events
            this._Connection = new MediaPortal.Pbk.Net.Http.HttpUserWebRequest("http://127.0.0.1:" + this._Plugin.Settings.HttpServerPort
                + MediaPortal.IptvChannels.Plugin.HTTP_PATH_EVENTS);
            this._Connection.AllowKeepAlive = false;
            this._Connection.AllowSystemProxy = this._Plugin.Settings.AllowSystemProxy ? MediaPortal.Pbk.Utils.OptionEnum.Yes : MediaPortal.Pbk.Utils.OptionEnum.No;
            this._Connection.UseOpenSSL = this._Plugin.Settings.UseOpenSsl ? MediaPortal.Pbk.Utils.OptionEnum.Yes : MediaPortal.Pbk.Utils.OptionEnum.No;
            Stream stream = this._Connection.GetResponseStream();
            if (stream != null)
            {
                if (this._Connection.StreamChunked != null)
                {
                    _Logger.Debug("[Setup] ctor() Init Chunked stream");
                    this._Connection.StreamChunked.CheckZeroChunk = false;
                    this._Connection.StreamChunked.DeliverEntireChunk = true;
                }

                this._ConnectionData = new byte[1024 * 8];

                //Begin receive events
                stream.BeginRead(this._ConnectionData, 0, this._ConnectionData.Length, this.cbConnectionReceive, stream);
            }

            //Load Connection handlers
            JToken j = MediaPortal.Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("http://127.0.0.1:" + this._Plugin.Settings.HttpServerPort 
                + MediaPortal.IptvChannels.Plugin.HTTP_PATH_STREAM_CONNECTIONS);
            if (j != null)
            {
                foreach (JToken jItem in j["result"])
                    this.connectionHandlerAdd(jItem);
            }

            //Load CDN tasks
            j = MediaPortal.Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("http://127.0.0.1:" + this._Plugin.Settings.HttpServerPort 
                + MediaPortal.IptvChannels.Plugin.HTTP_PATH_CDN_TASKS);
            if (j != null)
            {
                foreach (JToken jItem in j["result"])
                    this.cdnTaskAdd(jItem);
            }
        }

        private void cbConnectionReceive(IAsyncResult ar)
        {
            try
            {
                Stream stream = (Stream)ar.AsyncState;
                int iRd = stream.EndRead(ar);
                if (iRd > 0)
                {
                    string strContent = Encoding.UTF8.GetString(this._ConnectionData, 0, iRd);
                    //_Logger.Debug("[cbConnection] JSON:\r\n{0}", strContent);

                    JToken j;
                    try
                    {
                        j = JsonConvert.DeserializeObject<JToken>(strContent);
                        if (j != null)
                        {
                            MediaPortal.IptvChannels.SendEventTypeEnum type = (MediaPortal.IptvChannels.SendEventTypeEnum)Enum.Parse(typeof(MediaPortal.IptvChannels.SendEventTypeEnum), (string)j["eventType"]);

                            if (type > MediaPortal.IptvChannels.SendEventTypeEnum.Ping)
                            {
                                int iEventId = int.Parse((string)j["eventId"]);
                                if (this._LastEventId >= 0 && (this._LastEventId + 1) != iEventId)
                                    _Logger.Warn("[cbConnection] EventId misorder: {0}/{1}", this._LastEventId, iEventId);

                                this._LastEventId = iEventId;

                                JToken jObject = j["object"];
                                switch (type)
                                {
                                    case MediaPortal.IptvChannels.SendEventTypeEnum.ConnectionHandlerAdded:
                                        this.BeginInvoke(new MethodInvoker(() => this.connectionHandlerAdd(jObject)));
                                        break;

                                    case MediaPortal.IptvChannels.SendEventTypeEnum.ConnectionHandlerRemoved:
                                        this.BeginInvoke(new MethodInvoker(() => this.connectionHandlerRemove(jObject)));
                                        break;

                                    case MediaPortal.IptvChannels.SendEventTypeEnum.ConnectionHandlerChanged:
                                        this.BeginInvoke(new MethodInvoker(() => this.connectionHandlerUpdate(jObject)));
                                        break;


                                    case MediaPortal.IptvChannels.SendEventTypeEnum.ConnectionClientAdded:
                                        this.BeginInvoke(new MethodInvoker(() => this.connectionClientAdd(jObject)));
                                        break;

                                    case MediaPortal.IptvChannels.SendEventTypeEnum.ConnectionClientRemoved:
                                        this.BeginInvoke(new MethodInvoker(() => this.connectionClientRemove(jObject)));
                                        break;

                                    case MediaPortal.IptvChannels.SendEventTypeEnum.ConnectionClientChanged:
                                        this.BeginInvoke(new MethodInvoker(() => this.connectionClientUpdate(jObject)));
                                        break;


                                    case MediaPortal.IptvChannels.SendEventTypeEnum.CDNTaskAdded:
                                        this.BeginInvoke(new MethodInvoker(() => this.cdnTaskAdd(jObject)));
                                        break;

                                    case MediaPortal.IptvChannels.SendEventTypeEnum.CDNTaskRemoved:
                                        this.BeginInvoke(new MethodInvoker(() => this.cdnTaskRemove(jObject)));
                                        break;

                                    case MediaPortal.IptvChannels.SendEventTypeEnum.CDNTaskChanged:
                                        this.BeginInvoke(new MethodInvoker(() => this.cdnTaskUpdate(jObject)));
                                        break;


                                    case MediaPortal.IptvChannels.SendEventTypeEnum.CDNSegmentAdded:
                                        this.BeginInvoke(new MethodInvoker(() => this.cdnSegmentAdd(jObject)));
                                        break;

                                    case MediaPortal.IptvChannels.SendEventTypeEnum.CDNSegmentRemoved:
                                        this.BeginInvoke(new MethodInvoker(() => this.cdnSegmentRemove(jObject)));
                                        break;

                                    case MediaPortal.IptvChannels.SendEventTypeEnum.CDNSegmentChanged:
                                        this.BeginInvoke(new MethodInvoker(() => this.cdnSegmentUpdate(jObject)));
                                        break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[cbConnectionReceive] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    }

                    //Next read
                    stream.BeginRead(this._ConnectionData, 0, this._ConnectionData.Length, this.cbConnectionReceive, stream);
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[cbConnectionReceive] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }
        #endregion

        #region IDisposable
        void IDisposable.Dispose()
        {
            this._Connection.Close();
        }
        #endregion

        #region GUI

        private void comboBox_Sites_SelectionChangeCommitted(object sender, EventArgs e)
        {
            this.propertyGridPlugins.SelectedObject = this.comboBox_Sites.SelectedItem;
        }

        private void bSave_Click(object sender, EventArgs e)
        {
            if (!this._SaveRq)
            {
                MessageBox.Show("Changes will be saved after pressing 'OK' button. 'Cancel' button will discard all changes. Then TV service restart is needed.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this._SaveRq = true;
            }
        }


        private void checkBoxCDN_CheckedChanged(object sender, EventArgs e)
        {
            this.updateLinkResult();
        }

        private void checkBoxFfmpeg_CheckedChanged(object sender, EventArgs e)
        {
            this.updateLinkResult();
        }

        private void checkBoxUseSplitter_CheckedChanged(object sender, EventArgs e)
        {
            this.updateLinkResult();
        }

        private void buttonCopyToClipboard_Click(object sender, EventArgs e)
        {
            Clipboard.Clear();
            Clipboard.SetData("System.String", this.textBoxResult.Text);
        }

        private void textBoxSource_TextChanged(object sender, EventArgs e)
        {
            this.updateLinkResult();
        }

        private void textBoxSource_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                this.updateLinkResult();
        }

        private void updateLinkResult()
        {
            if (Uri.IsWellFormedUriString(this.textBoxSource.Text, UriKind.Absolute))
            {
                MediaPortal.IptvChannels.GenerateLinkConfigEnum cfg = this.checkBoxUseSplitter.Checked ?
                    MediaPortal.IptvChannels.GenerateLinkConfigEnum.MPURL_SOURCE_SPLITTER |
                    MediaPortal.IptvChannels.GenerateLinkConfigEnum.MPURL_SOURCE_SPLITTER_ARGS : MediaPortal.IptvChannels.GenerateLinkConfigEnum.NONE;

                if (this.checkBoxFfmpeg.Checked)
                    cfg |= MediaPortal.IptvChannels.GenerateLinkConfigEnum.FFMPEG;

                if (this.checkBoxCDN.Checked)
                    cfg |= MediaPortal.IptvChannels.GenerateLinkConfigEnum.CDN;

                this.textBoxResult.Text = MediaPortal.IptvChannels.Plugin.GenerateLink(this.textBoxSource.Text, cfg, this.textBoxFFMPEG.Text.Trim());

                this.buttonCreateChannel.Enabled = this.buttonCopyToClipboard.Enabled = this.checkBoxUseSplitter.Checked;
            }
            else
                this.buttonCreateChannel.Enabled = this.buttonCopyToClipboard.Enabled = false;
        }

        private void buttonCreateChannel_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(this.textBoxResult.Text))
            {
                MediaPortal.Pbk.Controls.TextQueryForm f = new MediaPortal.Pbk.Controls.TextQueryForm()
                {
                    Text = "Enter Channel name"
                };

                f.StartPosition = FormStartPosition.CenterParent;

                if (f.ShowDialog() == DialogResult.OK && this._Plugin.CreateChannel(f.Query, this.textBoxResult.Text))
                    MessageBox.Show("Channel has been created.");
            }
        }


        #region CDN

        private DataGridViewRow createCDNTaskRow(JToken j)
        {
            DataGridViewRow row = this.dataGridViewCDN.CreateGroupRow(j, false, (string)j["title"]);
            row.Cells[_COLUMN_CDN_INDEX_ID].Value = (string)j["id"];
            row.Cells[_COLUMN_CDN_INDEX_STATUS].Value = (string)j["status"];
            row.Cells[_COLUMN_CDN_INDEX_URL].Value = (string)j["url"];
            row.Cells[_COLUMN_CDN_INDEX_STATUS].Style.Font = new Font(this.dataGridViewCDN.DefaultCellStyle.Font, FontStyle.Bold);
            row.Tag = j;
            return row;
        }

        private DataGridViewRow createCDNSegmentRow(JToken j)
        {
            DataGridViewRow row = this.dataGridViewCDN.CreateRow(j, MediaPortal.IptvChannels.Controls.DataGridViewRowTypeEnum.GroupItem);
            row.Cells[_COLUMN_CDN_INDEX_TITLE].Value = (string)j["path"];
            row.Cells[_COLUMN_CDN_INDEX_URL].Value = (string)j["url"];
            row.Cells[_COLUMN_CDN_INDEX_TITLE].Value = (string)j["path"];
            row.Cells[_COLUMN_CDN_INDEX_STATUS].Value = (string)j["status"];
            row.Tag = j;
            return row;
        }


        private DataGridViewRow findCDNTaskRow(JToken j)
        {
            return this.findCDNTaskRow((string)j["id"]);
        }
        private DataGridViewRow findCDNTaskRow(string strId)
        {
            for (int i = 0; i < this.dataGridViewCDN.Rows.Count; i++)
            {
                DataGridViewRow r = this.dataGridViewCDN.Rows[i];

                if ((string)((JToken)r.Tag)["id"] == strId)
                    return r;
            }

            return null;
        }

        private DataGridViewRow findCDNSegmentRow(JToken j)
        {
            for (int i = 0; i < this.dataGridViewCDN.Rows.Count; i++)
            {
                DataGridViewRow r = this.dataGridViewCDN.Rows[i];
                JToken jRow = (JToken)r.Tag;

                if ((string)jRow["parentId"] == (string)j["parentId"] && (string)jRow["id"] == (string)j["id"])
                    return r;
            }

            return null;
        }


        private void cdnTaskAdd(JToken j)
        {
            if (this.findCDNTaskRow(j) == null)
                this.dataGridViewCDN.Rows.Add(this.createCDNTaskRow(j));
        }

        private void cdnTaskRemove(JToken j)
        {
            DataGridViewRow row = this.findCDNTaskRow(j);

            if (row != null)
            {
                this.dataGridViewCDN.RowColapse(row);
                this.dataGridViewCDN.Rows.RemoveAt(row.Index);
            }
        }

        private void cdnTaskUpdate(JToken j)
        {
            DataGridViewRow row = this.findCDNTaskRow(j);

            if (row != null)
            {
                row.Cells[_COLUMN_CDN_INDEX_STATUS].Value = (string)j["status"];
                this.dataGridViewCDN.InvalidateCell(this.dataGridViewCDN.MainColumnIndex, row.Index);
            }
        }

        private void cdnSegmentAdd(JToken j)
        {
            DataGridViewRow row = this.findCDNTaskRow((string)j["parentId"]);
            if (row != null &&
                ((MediaPortal.IptvChannels.Controls.DataGridViewCustomRow)row).ItemType == MediaPortal.IptvChannels.Controls.DataGridViewRowTypeEnum.GroupUncolapsed)
            {
                this.dataGridViewCDN.InsertNewGroupItem(row.Tag, (MediaPortal.IptvChannels.Controls.DataGridViewCustomRow)this.createCDNSegmentRow(j));
            }
        }

        private void cdnSegmentRemove(JToken j)
        {
            DataGridViewRow row = this.findCDNSegmentRow(j);
            if (row != null)
                this.dataGridViewCDN.Rows.Remove(row);
        }

        private void cdnSegmentUpdate(JToken j)
        {
            DataGridViewRow row = this.findCDNSegmentRow(j);
            if (row != null)
            {
                if (string.IsNullOrWhiteSpace((string)row.Cells[_COLUMN_CDN_INDEX_URL].Value) &&
                    !string.IsNullOrWhiteSpace((string)j["url"]))
                {
                    row.Cells[_COLUMN_CDN_INDEX_URL].Value = (string)j["url"];
                }

                row.Cells[_COLUMN_CDN_INDEX_STATUS].Value = (string)j["status"];
                this.dataGridViewCDN.InvalidateCell(row.Cells[this.dataGridViewCDN.MainColumnIndex]);
            }
        }


        private Image getImageFromStatus(string strState)
        {
            switch (strState)
            {
                case "Running":
                    return this.imageList.Images[_ICON_IDX_RUN];

                case "Available":
                    return this.imageList.Images[_ICON_IDX_DONE];

                case "Failed":
                case "Error":
                    return this.imageList.Images[_ICON_IDX_ERROR];

                case "Done":
                    return this.imageList.Images[_ICON_IDX_DONE];

                case "Iddle":
                    return this.imageList.Images[_ICON_IDX_IDDLE];

                case "Downloading":
                    return this.imageList.Images[_ICON_IDX_DOWNLOAD];

                case "Stopping":
                    return this.imageList.Images[_ICON_IDX_STOPPING];

                default:
                    return null;
            }
        }

        private void dataGridViewCDN_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            ////bool bBold = (string)((JToken)row.Tag)["type"] == "TaskSegmentCDN" && (bool)((JToken)row.Tag)["isInHls"];

            DataGridViewRow row = this.dataGridViewCDN.Rows[e.RowIndex];
            if (MediaPortal.IptvChannels.Controls.DataGridViewCustom.IsRowGroup(row))
            {
                //Plugin icon space
                const int _TEXT_OFFSET = 20;

                DataGridViewCell cell = row.Cells[this.dataGridViewCDN.MainColumnIndex];
                DataGridViewColumn col = this.dataGridViewCDN.Columns[cell.ColumnIndex];
                Rectangle cellBounds = this.dataGridViewCDN.GetCellDisplayRectangle(this.dataGridViewCDN.MainColumnIndex, row.Index, false);
                //Draw group row text
                int iX = cellBounds.X + MediaPortal.IptvChannels.Controls.DataGridViewCustom.GROUP_ROW_CONTENT_OFFSET + _TEXT_OFFSET;
                Rectangle rectText = new Rectangle(
                    iX - 1,
                    cellBounds.Y + col.DefaultCellStyle.Padding.Top - 1,
                    e.RowBounds.Width - iX - row.Cells[_COLUMN_CDN_INDEX_STATUS].Size.Width,
                    cellBounds.Height);
                Pen pen = MediaPortal.Pbk.Controls.Renderer.Renderer.GetCachedPen(row.Selected ?
                    this.dataGridViewCDN.DefaultCellStyle.SelectionForeColor : this.dataGridViewCDN.DefaultCellStyle.ForeColor);
                TextRenderer.DrawText(e.Graphics, (string)cell.Value, cell.Style.Font, rectText, pen.Color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                //icon
                Image img = this.getImageFromStatus((string)row.Cells[_COLUMN_CDN_INDEX_STATUS].Value);
                if (img != null)
                    e.Graphics.DrawImage(img, rectText.X - _TEXT_OFFSET + 2, cellBounds.Y + 3, 16, 16);

                //Clear invalidation flag
                ((MediaPortal.IptvChannels.Controls.DataGridViewCustomRow)row).InvalidateNeeded = false;
            }
        }

        private void dataGridViewCDN_CellPostPaint(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                DataGridViewRow row = this.dataGridViewCDN.Rows[e.RowIndex];
                DataGridViewColumn col = this.dataGridViewCDN.Columns[e.ColumnIndex];

                switch (e.ColumnIndex)
                {
                    case _COLUMN_CDN_INDEX_TITLE:
                        if (!MediaPortal.IptvChannels.Controls.DataGridViewCustom.IsRowGroup(row))
                        {
                            //Add Status icon
                            Image img = this.getImageFromStatus((string)row.Cells[_COLUMN_CDN_INDEX_STATUS].Value);
                            if (img != null)
                                e.Graphics.DrawImage(img,
                                new Rectangle(e.CellBounds.X + 7 + e.CellStyle.Padding.Left - col.DefaultCellStyle.Padding.Left, e.CellBounds.Y + 3, 16, 16));

                            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground); //render text only
                            e.Handled = true;
                        }
                        break;

                    default:
                        if (!MediaPortal.IptvChannels.Controls.DataGridViewCustom.IsRowGroup(row))
                        {
                            e.AdvancedBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;
                            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground | DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
                        }
                        else
                            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground); //render text only
                        e.Handled = true;
                        break;
                }
            }
        }

        private void dataGridViewCDN_BeforeUncolapse(object sender, DataGridViewRowEventArgs e)
        {
            JToken j = MediaPortal.Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("http://127.0.0.1:" + this._Plugin.Settings.HttpServerPort
                + MediaPortal.IptvChannels.Plugin.HTTP_PATH_CDN_SEGMENTS + "?id=" + ((JToken)e.Row.Tag)["id"]);
            if (j != null)
            {
                //Insert new rows after group row
                int iIdx = e.Row.Index;

                foreach (JToken jItem in j["result"])
                    this.dataGridViewCDN.Rows.Insert(++iIdx, this.createCDNSegmentRow(jItem));
            }
        }

        #endregion

        #region Connection handlers

        private DataGridViewRow createConnectionRow(JToken con)
        {
            DataGridViewRow row = this.dataGridView_ConList.CreateGroupRow(con, false, (string)con["url"]);

            row.Cells[_COLUMN_STREAM_INDEX_ID].Value = (string)con["id"];
            row.Cells[_COLUMN_STREAM_INDEX_URL].Value = (string)con["url"];
            row.Cells[_COLUMN_STREAM_INDEX_INFO].Value = (string)con["info"];
            row.Cells[_COLUMN_STREAM_INDEX_CLIENTS].Value = (string)con["clients"];
            row.Tag = con;

            return row;
        }

        private void dataGridView_ConList_BeforeUncolapse(object sender, DataGridViewRowEventArgs e)
        {
            JToken j = MediaPortal.Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("http://127.0.0.1:" +
                this._Plugin.Settings.HttpServerPort + MediaPortal.IptvChannels.Plugin.HTTP_PATH_STREAM_CLIENTS + "?id=" + ((JToken)e.Row.Tag)["id"]);

            if (j != null)
            {
                //Insert new rows after group row
                int iIdx = e.Row.Index;

                foreach (JToken jItem in j["result"])
                {
                    DataGridViewRow rowNew = this.dataGridView_ConList.CreateRow(jItem, MediaPortal.IptvChannels.Controls.DataGridViewRowTypeEnum.GroupItem);
                    rowNew.Cells[_COLUMN_STREAM_INDEX_INFO].Value = (string)jItem["info"];
                    this.dataGridView_ConList.Rows.Insert(++iIdx, rowNew);

                }
            }
        }

        private void dataGridView_ConList_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridViewRow row = this.dataGridView_ConList.Rows[e.RowIndex];
            if (MediaPortal.IptvChannels.Controls.DataGridViewCustom.IsRowGroup(row))
            {
                //Plugin icon space
                const int _TEXT_OFFSET = 0;
                const int _TEXT_CLIENTS_WIDTH = 40;

                DataGridViewCell cell = row.Cells[this.dataGridView_ConList.MainColumnIndex];
                DataGridViewColumn col = this.dataGridView_ConList.Columns[cell.ColumnIndex];
                Rectangle cellBounds = this.dataGridView_ConList.GetCellDisplayRectangle(this.dataGridView_ConList.MainColumnIndex, row.Index, false);
                //Draw group row text
                int iX = cellBounds.X + MediaPortal.IptvChannels.Controls.DataGridViewCustom.GROUP_ROW_CONTENT_OFFSET + _TEXT_OFFSET;
                Rectangle rectText = new Rectangle(
                    iX - 1,
                    cellBounds.Y + col.DefaultCellStyle.Padding.Top - 1,
                    e.RowBounds.Width - iX,
                    cellBounds.Height);
                Pen pen = MediaPortal.Pbk.Controls.Renderer.Renderer.GetCachedPen(row.Selected ?
                    this.dataGridView_ConList.DefaultCellStyle.SelectionForeColor : this.dataGridView_ConList.DefaultCellStyle.ForeColor);
                rectText.Width -= _TEXT_CLIENTS_WIDTH;
                TextRenderer.DrawText(e.Graphics, (string)cell.Value, cell.Style.Font, rectText, pen.Color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                //Child count text
                string strText = "[" + row.Cells[_COLUMN_STREAM_INDEX_CLIENTS].Value + "]";
                rectText.Width += _TEXT_CLIENTS_WIDTH;
                TextRenderer.DrawText(e.Graphics, strText,
                    cell.Style.Font, rectText, pen.Color, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                //Clear invalidation flag
                ((MediaPortal.IptvChannels.Controls.DataGridViewCustomRow)row).InvalidateNeeded = false;
            }
        }

        private void dataGridView_ConList_CellPostPaint(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                DataGridViewRow row = this.dataGridView_ConList.Rows[e.RowIndex];
                //DataGridViewColumn col = this.dataGridView_ConList.Columns[e.ColumnIndex];

                switch (e.ColumnIndex)
                {
                    case _COLUMN_STREAM_INDEX_INFO:
                        if (!MediaPortal.IptvChannels.Controls.DataGridViewCustom.IsRowGroup(row))
                        {
                            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground); //render text only
                            e.Handled = true;
                        }
                        break;

                    default:
                        if (!MediaPortal.IptvChannels.Controls.DataGridViewCustom.IsRowGroup(row))
                        {
                            e.AdvancedBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;
                            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
                        }
                        else
                            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground); //render text only
                        e.Handled = true;
                        break;  
                }
            }
        }


        private DataGridViewRow findConnectionHandlerRow(JToken j)
        {
            return findConnectiontRow(j, "ConnectionHandler");
        }

        private DataGridViewRow findConnectionClientRow(JToken j)
        {
            return findConnectiontRow(j, "RemoteClient");
        }

        private DataGridViewRow findConnectiontRow(JToken j, string strType)
        {
            for (int i = 0; i < this.dataGridView_ConList.Rows.Count; i++)
            {
                DataGridViewRow row = this.dataGridView_ConList.Rows[i];
                JToken jRow = (JToken)row.Tag;
                if ((string)jRow["type"] == strType && (string)jRow["id"] == (string)j["id"])
                    return row;
            }

            return null;
        }


        private void connectionHandlerAdd(JToken j)
        {
            if (this.findConnectionHandlerRow(j) == null)
                this.dataGridView_ConList.Rows.Add(this.createConnectionRow(j));
        }

        private void connectionHandlerRemove(JToken j)
        {
            DataGridViewRow row = this.findConnectionHandlerRow(j);
            if (row != null)
            {
                this.dataGridView_ConList.RowColapse(row);
                this.dataGridView_ConList.Rows.RemoveAt(row.Index);
            }
        }

        private void connectionHandlerUpdate(JToken j)
        {
            DataGridViewRow row = this.findConnectionHandlerRow(j);
            if (row != null)
            {
                row.Cells[_COLUMN_STREAM_INDEX_URL].Value = (string)j["url"];
                row.Cells[_COLUMN_STREAM_INDEX_INFO].Value = (string)j["info"];
                row.Cells[_COLUMN_STREAM_INDEX_CLIENTS].Value = (string)j["clients"];

                if (((MediaPortal.IptvChannels.Controls.DataGridViewCustomRow)row).ItemType == MediaPortal.IptvChannels.Controls.DataGridViewRowTypeEnum.GroupUncolapsed)
                {
                    //Update all clients too

                    JToken jClients = MediaPortal.Pbk.Net.Http.HttpUserWebRequest.Download<JToken>("http://127.0.0.1:" +
                        this._Plugin.Settings.HttpServerPort + MediaPortal.IptvChannels.Plugin.HTTP_PATH_STREAM_CLIENTS + "?id=" + j["id"]);

                    if (jClients != null)
                    {
                        foreach (JToken jItem in jClients["result"])
                            this.connectionClientUpdate(jItem);
                    }
                }
            }
        }

        private void connectionClientAdd(JToken j)
        {
            DataGridViewRow row = this.findConnectionHandlerRow(j);
            if (row != null &&
                ((MediaPortal.IptvChannels.Controls.DataGridViewCustomRow)row).ItemType == MediaPortal.IptvChannels.Controls.DataGridViewRowTypeEnum.GroupUncolapsed)
            {
                this.dataGridView_ConList.InsertNewGroupItem(row.Tag,
                    this.dataGridView_ConList.CreateRow(j, MediaPortal.IptvChannels.Controls.DataGridViewRowTypeEnum.GroupItem));
            }

        }

        private void connectionClientRemove(JToken j)
        {
            DataGridViewRow row = findConnectionClientRow(j);
            if (row != null)
                this.dataGridView_ConList.Rows.RemoveAt(row.Index);
        }

        private void connectionClientUpdate(JToken j)
        {
            DataGridViewRow row = findConnectionClientRow(j);
            if (row != null)
                row.Cells[_COLUMN_STREAM_INDEX_INFO].Value = (string)j["info"];
        }




        #endregion

        #endregion
    }
}