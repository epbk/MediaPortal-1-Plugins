using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.IptvChannels.Proxy
{
    public class VlcControlManager
    {
        #region Constants
        private const string PATH_STATUS = "/requests/vlm.xml";
        private const string PATH_COMMAND = "/requests/vlm_cmd.xml?command=";
        #endregion

        #region Fields
        private readonly string _VlcOptions = null;
        private readonly string _VlcExePath = null;
        private readonly int _VlcPort = -1;

        private Process _Process_Vlc = null;

        private int _IdCounter = 0;

        private static VlcControlManager _Instance = null;

        private static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        public static VlcControlManager Instance
        {
            get
            {
                lock (typeof(VlcControlManager))
                {
                    if (_Instance == null)
                    {
                        _Instance = new VlcControlManager(Database.dbSettings.Instance.VlcPath, getAvailablePort(9000), Database.dbSettings.Instance.VlcOptions);
                        _Instance.Start();
                    }

                    return _Instance;
                }
            }
        }

        public bool IsRunning
        {
            get { return this._Process_Vlc != null; }
        }
        #endregion

        #region ctor
        public VlcControlManager(string strVlcExePath, int iPort, string strOptions)
        {
            this._VlcExePath = strVlcExePath;
            this._VlcPort = iPort;
            this._VlcOptions = strOptions;
    }
        #endregion

        #region dtor
        ~VlcControlManager()
        {
            this.Stop();
        }
        #endregion

        #region Public methods

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Start()
        {
            _Logger.Debug("[Start]");

            try
            {
                if (this._Process_Vlc != null)
                    return false;

                if (!File.Exists(this._VlcExePath))
                {
                    _Logger.Error("[Start] File path not found: {0}", this._VlcExePath);
                    return false;
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    FileName = "\"" + this._VlcExePath + "\"",
                    UseShellExecute = false,
                    ErrorDialog = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Arguments = " --intf=\"http\" --http-host 0.0.0.0 --http-port " + this._VlcPort + " --http-password 1234 " + this._VlcOptions
                };

                Process pr = new Process { StartInfo = psi };
                pr.Exited += this.exitHandler;
                pr.OutputDataReceived += this.outputHandler;
                pr.ErrorDataReceived += this.errorHandler;

                //Start VLC
                pr.Start();
                pr.BeginOutputReadLine();
                pr.BeginErrorReadLine();

                //Check for VLC's http server
                int iAttempts = 5;
                while (iAttempts-- > 0)
                {
                    Thread.Sleep(200);

                    if (this.vlmGetStatus() != null)
                    {
                        this._Process_Vlc = pr;
                        _Logger.Debug("[Started] Started: {0}:{1}", this._VlcExePath, this._VlcPort);
                        return true;
                    }
                }

                pr.Kill();

                return false;
            }
            catch (Exception ex)
            {
                _Logger.Error("[Start] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Stop()
        {
            _Logger?.Debug("[Stop]");

            try
            {
                if (this._Process_Vlc != null)
                {
                    this._Process_Vlc.CancelOutputRead();
                    this._Process_Vlc.CancelErrorRead();

                    if (!this._Process_Vlc.HasExited)
                        this._Process_Vlc.Kill();

                    this._Process_Vlc = null;

                    _Logger?.Debug("[Stop] Stopped.");

                    return true;
                }
            }
            catch (Exception ex)
            {
                _Logger?.Error("[Stop] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            return false;
        }

        public int StreamingStart(string strUrl, int iPort)
        {
            int iId = Interlocked.Increment(ref this._IdCounter);

            if (!this.vlmExecuteCommand("new ID_" + iId + " broadcast enabled"))
                return -1;

            if (!this.vlmExecuteCommand("setup ID_" + iId + " input " + strUrl))
                return -1;

            if (!this.vlmExecuteCommand("setup ID_" + iId + " output " + "#udp{mux=ts,dst=127.0.0.1:" + iPort + "}"))
                return -1;

            //if (!this.vlmExecuteCommand("setup ID_" + iId + " option audio-language=cze,eng"))
            //    return -1;

            return this.vlmExecuteCommand("control ID_" + iId + " play") ? iId : -1;
        }

        public bool StreamingDelete(int iId)
        {
            if (!this.vlmExecuteCommand("control ID_" + iId + " stop"))
                return false;

            return this.vlmExecuteCommand("del ID_" + iId);
        }

        public void StreamingDeleteAll()
        {
            this.vlmExecuteCommand("del all");
        }

        #endregion

        #region Private methods
        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool vlmExecuteCommand(string strCmd)
        {
            try
            {
                if (!this.IsRunning && !this.Start())
                    return false;

                WebClient wc = new WebClient();
                wc.Headers[HttpRequestHeader.Authorization] = "Basic OjEyMzQ=";
                string strResponse = wc.DownloadString("http://127.0.0.1:" + this._VlcPort + PATH_COMMAND + System.Web.HttpUtility.UrlEncode(strCmd).Replace("+", "%20"));
                return !string.IsNullOrWhiteSpace(strResponse) && (strResponse.IndexOf("<error></error>") > 0 || strResponse.IndexOf("<error/>") > 0);
            }
            catch (Exception ex)
            {
                _Logger.Error("[vlmExecuteCommand] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private string vlmGetStatus()
        {
            try
            {
                WebClient wc = new WebClient();
                wc.Headers[HttpRequestHeader.Authorization] = "Basic OjEyMzQ=";
                return wc.DownloadString("http://127.0.0.1:" + this._VlcPort + PATH_STATUS);
            }
            catch (Exception ex)
            {
                _Logger.Error("[vlmGetStatus] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            return null;
        }

        private void exitHandler(object sender, EventArgs e)
        {
            this._Process_Vlc = null;
        }

        private void outputHandler(object sender, DataReceivedEventArgs e)
        {
            if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[outputHandler] {0}", e.Data);
        }

        private void errorHandler(object sender, DataReceivedEventArgs e)
        {
            if (Log.LogLevel <= LogLevel.Trace) _Logger.Trace("[errorHandler] {0}", e.Data);
        }

        private static int getAvailablePort(int iStartingPort)
        {
            IPEndPoint[] eps;
            List<int> ports = new List<int>();

            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
            ports.AddRange(from n in connections where n.LocalEndPoint.Port >= iStartingPort select n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            eps = properties.GetActiveTcpListeners();
            ports.AddRange(from n in eps where n.Port >= iStartingPort select n.Port);

            //getting active udp listeners
            eps = properties.GetActiveUdpListeners();
            ports.AddRange(from n in eps where n.Port >= iStartingPort select n.Port);

            ports.Sort();

            for (int i = iStartingPort; i < UInt16.MaxValue; i++)
            {
                if (!ports.Contains(i))
                    return i;
            }

            return 0;
        }

        #endregion
    }
}
