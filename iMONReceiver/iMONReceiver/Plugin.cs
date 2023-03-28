using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net.Sockets;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MediaPortal.GUI.Library;

namespace MediaPortal.Plugins.iMONReceiver
{
    [MediaPortal.Configuration.PluginIcons("MediaPortal.Plugins.iMONReceiver.Logo-enabled.png", "MediaPortal.Plugins.iMONReceiver.Logo-disabled.png")]
    public class Plugin : GUIWindow, ISetupForm, IPlugin, IPluginReceiver
    {
        [DllImport("user32.dll")]
        private static extern int RegisterWindowMessage(string message); //Defines a new window message that is guaranteed to be unique throughout the system. The message value can be used when sending or posting messages.

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort GlobalAddAtom(string lpString);

        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        private const int HWND_BROADCAST = 0xFFFF; //The message is posted to all top-level windows in the system, including disabled or invisible unowned windows, overlapped windows, and pop-up windows. The message is not posted to child windows.

        #region Enums
        private enum CommandEnum
        {
            Unknown = 0,
            DriverMethod,

            ImonInit = 120,
            ImonUninit,
            ImonIsInited,
            ImonSetText,
            ImonSetEQ,
            ImonSetLCDData2,
            ImonSendData,
            ImonSendDataBuffer,

            ImonRCInit = 130,
            ImonRCUninit,
            ImonRCIsInited,
            ImonRCGetHWType,
            ImonRCGetFirmwareVer,
            ImonRCCheckDriverVersion,
            ImonRCChangeiMONRCSet,
            ImonRCChangeRC6,
            ImonRCGetLastRFMode,
            ImonRCGetPacket,
            ImonRCRegisterForEvents,
            ImonRCUnregisterFromEvents
        }

        [Flags]
        private enum RegisterEventEnum
        {
            None = 0,
            ImonRcButtonsEvents = 1
        }
        #endregion

        #region Constants
        internal const string PLUGIN_NAME = "iMONReceiver";
        internal const int PLUGIN_ID = 6586;
        internal const string PLUGIN_AUTHOR = "PBK";
        internal const string PLUGIN_DESCRIPTION = "iMON RC Receiver";

        internal const string CFG_SECTION = Plugin.PLUGIN_NAME;
        internal const string CFG_FILE = "ConfigFile";

        internal const string WM_IMON_RC_PLUGIN_NOTIFY = "WM_IMON_RC_PLUGIN_NOTIFY";
        #endregion

        #region Private fields
        private uint _WM_RC_PluginNotify;
        private MediaPortal.InputDevices.InputHandler _InputHandler;

        private TcpClient _ProxyConnection = null;
        private byte[] _ProxyResponse = new byte[8];

        private MemoryMappedFile _File = null;
        private MemoryMappedViewAccessor _FileAccessor = null;
        private int _ProxyProccessId = -1;
        private int _ProxyPort = -1;
        private byte[] _ProxyData = null;
        private int _MessageId;
        private ushort _Atom;
        private AsyncCallback _ProxyReceiveCallback;
        private System.Timers.Timer _TimerReconnect;
        private bool _Abort = false;
        #endregion

        #region ctor
        public Plugin()
        {
            this._WM_RC_PluginNotify = (uint)RegisterWindowMessage(WM_IMON_RC_PLUGIN_NOTIFY);
            this._ProxyReceiveCallback = new AsyncCallback(this.cbProxyReceive);
        }
        #endregion

        #region ISetupForm
        public string Author()
        {
            return PLUGIN_AUTHOR;
        }

        public bool CanEnable()
        {
            return true;
        }

        public bool DefaultEnabled()
        {
            return false;
        }

        public string Description()
        {
            return PLUGIN_DESCRIPTION;
        }

        public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
        {
            strButtonText = String.Empty;
            strButtonImage = String.Empty;
            strButtonImageFocus = String.Empty;
            strPictureImage = String.Empty;
            return false;
        }

        public int GetWindowId()
        {
            return PLUGIN_ID;
        }

        public bool HasSetup()
        {
            return true;
        }

        public string PluginName()
        {
            return PLUGIN_NAME;
        }

        public void ShowPlugin()
        {
            Settings.FormSettings form = new Settings.FormSettings();
            form.Show();
            return;
        }
        #endregion

        #region IPlugin
        public void Start()
        {
            this._Abort = false;

            if (Environment.Is64BitProcess)
                this.proxyConnect();
            else
                iMONApi.IMON_RcApi_Init(GUIGraphicsContext.form.Handle, this._WM_RC_PluginNotify);

            if (this._InputHandler == null)
            {
                using (MediaPortal.Profile.Settings set = new MediaPortal.Profile.MPSettings())
                {
                    string strCurrentFile = set.GetValueAsString(CFG_SECTION, CFG_FILE, string.Empty);
                    this._InputHandler = new MediaPortal.InputDevices.InputHandler(strCurrentFile);

                    Log.Debug("[iMONReceiver][Start] Mapping file: " + strCurrentFile);
                }
            }

            Log.Debug("[iMONReceiver][Start] Started");
        }

        public void Stop()
        {
            this._Abort = true;
            if (Environment.Is64BitProcess)
            {
                if (this._ProxyConnection != null)
                {
                    TcpClient client = this._ProxyConnection;
                    this._ProxyConnection = null;
                    try { client.Close(); }
                    catch { }
                    client = null;
                }

                if (this._TimerReconnect != null)
                {
                    this._TimerReconnect.Dispose();
                    this._TimerReconnect = null;
                }
            }
            else
                iMONApi.IMON_RcApi_Uninit();

            Log.Debug("[iMONReceiver][Stop] Stopped");
        }
        #endregion

        #region IPluginReceiver
        public bool WndProc(ref Message msg)
        {
            string strButton;

            if (msg.Msg == this._WM_RC_PluginNotify)
            {
                switch ((iMONApi.RCNotifyCode)msg.WParam)
                {
                    case iMONApi.RCNotifyCode.RCNM_HW_CONNECTED:
                        Log.Debug("[iMONReceiver][WndProc][RCNM_HW_CONNECTED]");
                        break;

                    case iMONApi.RCNotifyCode.RCNM_HW_DISCONNECTED:
                        Log.Debug("[iMONReceiver][WndProc][RCNM_HW_DISCONNECTED]");
                        break;

                    case iMONApi.RCNotifyCode.RCNM_IMON_CLOSED:
                        Log.Debug("[iMONReceiver][WndProc][RCNM_IMON_CLOSED]");
                        break;

                    case iMONApi.RCNotifyCode.RCNM_IMON_RESTARTED:
                        Log.Debug("[iMONReceiver][WndProc][RCNM_IMON_RESTARTED]");
                        break;

                    case iMONApi.RCNotifyCode.RCNM_PLUGIN_FAILED:
                        iMONApi.RCNInitResult errCode = (iMONApi.RCNInitResult)msg.LParam;
                        Log.Debug("[iMONReceiver][WndProc][RCNM_PLUGIN_FAILED] " + errCode);

                        //Try again if no reply
                        if (errCode == iMONApi.RCNInitResult.RCN_ERR_IMON_NO_REPLY)
                        {
                            iMONApi.IMON_RcApi_Uninit();
                            System.Threading.Thread.Sleep(200);
                            iMONApi.IMON_RcApi_Init(GUIGraphicsContext.form.Handle, this._WM_RC_PluginNotify);
                        }

                        break;

                    case iMONApi.RCNotifyCode.RCNM_PLUGIN_SUCCEED:
                        Log.Debug("[iMONReceiver][WndProc][RCNM_PLUGIN_SUCCEED]");
                        break;

                    case iMONApi.RCNotifyCode.RCNM_KNOB_ACTION:
                        strButton = ((iMONApi.RCButton)msg.LParam).ToString();
                        Log.Debug("[iMONReceiver][WndProc][RCNM_KNOB_ACTION] Action: " + strButton);

                        if (this._InputHandler != null)
                            this._InputHandler.MapAction(strButton);

                        break;

                    case iMONApi.RCNotifyCode.RCNM_RC_BUTTON_DOWN:
                        strButton = ((iMONApi.RCButton)msg.LParam).ToString();
                        Log.Debug("[iMONReceiver][WndProc][RCNM_RC_BUTTON_DOWN] Button: " + strButton);

                        if (this._InputHandler != null)
                            this._InputHandler.MapAction(strButton);

                        break;

                    case iMONApi.RCNotifyCode.RCNM_RC_BUTTON_UP:
                        strButton = ((iMONApi.RCButton)msg.LParam).ToString();
                        Log.Debug("[iMONReceiver][WndProc][RCNM_RC_BUTTON_UP] Button: " + strButton);
                        break;

                    case iMONApi.RCNotifyCode.RCNM_RC_REMOTE:
                        Log.Debug("[iMONReceiver][WndProc][RCNM_RC_REMOTE] " + ((iMONApi.RCRemote)msg.LParam).ToString());
                        break;

                    default:
                        break;
                }

                return true;
            }

            return false;
        }
        #endregion



        private void proxyReconnect()
        {
            if (this._TimerReconnect == null)
            {
                this._TimerReconnect = new System.Timers.Timer(5000);
                this._TimerReconnect.Elapsed += new System.Timers.ElapsedEventHandler(this.cbTimerReconnect);
            }

            this._TimerReconnect.AutoReset = false;
            this._TimerReconnect.Interval = 5000;
            this._TimerReconnect.Enabled = true;

            Log.Debug("[iMONReceiver][proxyReconnect] Attepmt to reconnect in next 5 sec.");
        }

        private int proxyConnect()
        {
            if (this._ProxyData == null)
                this._ProxyData = new byte[256];

            if (this._ProxyConnection == null)
            {
                //Read port from memory file
                MemoryMappedFile f = MemoryMappedFile.OpenExisting("MPx86ProxyDescription");
                byte[] data = new byte[512];
                MemoryMappedViewAccessor ac = f.CreateViewAccessor();
                ac.ReadArray<byte>(0, data, 0, data.Length);
                ac.Dispose();
                f.Dispose();
                XmlDocument xml = new XmlDocument();
                xml.LoadXml(Encoding.UTF8.GetString(data).Trim());
                this._ProxyProccessId = int.Parse(xml.SelectSingleNode("//MPx86ProxyDescription/PID/text()").Value);
                this._ProxyPort = int.Parse(xml.SelectSingleNode("//MPx86ProxyDescription/Port/text()").Value);
                xml = null;

                if (this._ProxyPort > 0)
                {
                    //Socket mode

                    this._ProxyConnection = new TcpClient();
                    this._ProxyConnection.Connect("127.0.0.1", this._ProxyPort);

                    if (!this._ProxyConnection.Connected)
                    {
                        Log.Error("[iMONReceiver][proxyConnect] Failed to connect.");
                        this._ProxyConnection = null;
                        this.proxyReconnect();
                        return -1;
                    }

                    Log.Debug("[iMONReceiver][proxyConnect] Connected to proxy server in socket mode.");
                }
                else if (this._ProxyProccessId > 0)
                {
                    //MemoryFile mode

                    //Our filename
                    string strFile = "MediaPortaliMONReceiverx86ProxyClient:" + System.Diagnostics.Process.GetCurrentProcess().Id;

                    //Create mapped file
                    this._File = MemoryMappedFile.CreateNew(strFile, 256);
                    this._FileAccessor = this._File.CreateViewAccessor();

                    //Get win message id
                    this._MessageId = RegisterWindowMessage("WM_MP_X86_PROXY_REQUEST");

                    //Register global atom
                    this._Atom = GlobalAddAtom(strFile);

                    Log.Debug("[iMONReceiver][proxyConnect] Connected to proxy server in memory file mode.");
                }
                else
                {
                    Log.Error("[iMONReceiver][proxyConnect] Unknown connection to the proxy.");
                    return -1;
                }

                //Register for iMON RC Events
                this._ProxyData[0] = 5; //length
                this._ProxyData[1] = 0; //length
                this._ProxyData[2] = 0; //length
                this._ProxyData[3] = 0; //length
                this._ProxyData[4] = (byte)CommandEnum.ImonRCRegisterForEvents; //request

                if (this.proxySend(5, 0, this._ProxyData) == 1)
                {
                    Log.Debug("[iMONReceiver][proxyConnect] Registered for events.");
                    this._ProxyConnection.Client.BeginReceive(this._ProxyData, 0, this._ProxyData.Length, SocketFlags.None, this._ProxyReceiveCallback, null);

                    if (this._TimerReconnect != null)
                    {
                        this._TimerReconnect.Dispose();
                        this._TimerReconnect = null;
                    }
                }
                else
                {
                    this._ProxyConnection.Close();
                    this._ProxyConnection = null;
                    this.proxyReconnect();
                }
            }

            return 0;
        }

        private int proxySend(int iSize, int iDataLength = 0, byte[] data = null)
        {
            if (this._ProxyPort > 0)
            {
                //Send request
                this._ProxyConnection.Client.Send(this._ProxyData, 0, iSize, SocketFlags.None);

                //Get response
                int iRec = this._ProxyConnection.Client.Receive(this._ProxyResponse);
                if (iRec != 4 + iDataLength)
                    return -1;

                if (iDataLength > 0)
                    Buffer.BlockCopy(this._ProxyData, 4, data, 0, iDataLength);

                return BitConverter.ToInt32(this._ProxyResponse, 0);
            }
            else
            {
                this._FileAccessor.WriteArray<byte>(0, this._ProxyData, 0, iSize);

                //Send request
                SendMessage((IntPtr)HWND_BROADCAST, this._MessageId, (IntPtr)this._Atom, IntPtr.Zero);

                if (iDataLength > 0)
                    this._FileAccessor.ReadArray<byte>(0, data, 4, iDataLength);

                //Get response
                return this._FileAccessor.ReadInt32(0);
            }
        }


        private void cbProxyReceive(IAsyncResult ar)
        {
            try
            {
                iMONApi.RCNotifyCode code;
                RegisterEventEnum evnt;
                int iSize;
                int iLength = this._ProxyConnection.Client.EndReceive(ar);

                while (!this._Abort)
                {
                    if (iLength > 0)
                    {
                        //Offset: type (length)
                        //0: Packet size (4)
                        //4: EventType (1)
                        //5: Arg1 (4)
                        //9: Arg2 (4)

                        iMONApi.RCButton button;
                        string strButton;

                        iSize = BitConverter.ToInt32(this._ProxyData, 0);
                        evnt = (RegisterEventEnum)this._ProxyData[4];

                        switch (evnt)
                        {
                            case RegisterEventEnum.ImonRcButtonsEvents:

                                code = (iMONApi.RCNotifyCode)BitConverter.ToInt32(this._ProxyData, 5);

                                switch (code)
                                {
                                    case iMONApi.RCNotifyCode.RCNM_RC_BUTTON_DOWN:
                                        button = (iMONApi.RCButton)BitConverter.ToInt32(this._ProxyData, 9);
                                        strButton = button.ToString();

                                        Log.Debug("[iMONReceiver][cbProxyReceive][RCNM_RC_BUTTON_DOWN] Button: " + strButton);

                                        if (this._InputHandler != null)
                                            this._InputHandler.MapAction(strButton);

                                        break;

                                    case iMONApi.RCNotifyCode.RCNM_KNOB_ACTION:
                                        button = (iMONApi.RCButton)BitConverter.ToInt32(this._ProxyData, 9);
                                        strButton = button.ToString();

                                        Log.Debug("[iMONReceiver][cbProxyReceive][RCNM_KNOB_ACTION] Action: " + strButton);

                                        if (this._InputHandler != null)
                                            this._InputHandler.MapAction(strButton);

                                        break;

                                    case iMONApi.RCNotifyCode.RCNM_RC_BUTTON_UP:
                                        button = (iMONApi.RCButton)BitConverter.ToInt32(this._ProxyData, 9);
                                        strButton = button.ToString();

                                        Log.Debug("[iMONReceiver][cbProxyReceive][RCNM_RC_BUTTON_UP] Button: " + strButton);
                                        break;
                                }
                                break;
                        }

                        //Next receive
                        iLength = this._ProxyConnection.Client.Receive(this._ProxyData, 0, this._ProxyData.Length, SocketFlags.None);
                    }
                    else
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[iMONReceiver][cbProxyReceive] Error: " + ex.Message);
            }

            if (!this._Abort && this._ProxyConnection != null)
            {
                this._ProxyConnection = null;
                this.proxyReconnect();
            }

            Log.Debug("[iMONReceiver][cbProxyReceive] Terminated.");
        }

        private void cbTimerReconnect(object sender, System.Timers.ElapsedEventArgs e)
        {
            this._TimerReconnect.Enabled = false;
            this.proxyConnect();
        }
    }
}
