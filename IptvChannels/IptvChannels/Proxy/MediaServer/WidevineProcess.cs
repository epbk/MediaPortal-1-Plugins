using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class WidevineProcess
    {
        private static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        private readonly List<ContentProtectionKey> _Keys = new List<ContentProtectionKey>();
        private string _Result = string.Empty;
        private string _Error = string.Empty;

        public string Error => _Error;

        public List<ContentProtectionKey> GetKeys(string strPSSH, string strLicenceServer, Pbk.Net.Http.HttpUserWebRequestArguments httpArgs)
        {
            try
            {
                StringBuilder sbArgs = new StringBuilder(256);
                if (!string.IsNullOrWhiteSpace(strLicenceServer))
                {
                    sbArgs.Append(" --licence-server ");
                    sbArgs.Append(strLicenceServer);
                }

                sbArgs.Append(" --pssh ");
                sbArgs.Append(strPSSH);

                if (httpArgs != null && httpArgs.Fields != null)
                {
                    for (int i = 0; i < httpArgs.Fields.Count; i++)
                    {
                        sbArgs.Append(" --header \"");
                        sbArgs.Append(httpArgs.Fields.GetKey(i));
                        sbArgs.Append("\" \"");
                        sbArgs.Append(httpArgs.Fields[i]);
                        sbArgs.Append('\"');
                    }
                }

                string strArgs = sbArgs.ToString();

                _Logger.Debug("[GetKeys] Call Widevine client: {0}", strArgs);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    Arguments = strArgs,
                    FileName = "\"Widevine\\WidevineClient.exe\"",
                    UseShellExecute = false,
                    ErrorDialog = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = "Widevine"
                };
                Process pr = new Process
                {
                    StartInfo = startInfo
                };
                //pr.Exited += new EventHandler(restream_Exited);
                pr.OutputDataReceived += this.cbWidevineClientOutputHandler;
                pr.ErrorDataReceived += this.cbWidevineClientErrorHandler;

                //Start
                pr.Start();
                pr.BeginOutputReadLine();
                pr.BeginErrorReadLine();

                //Wait for finish
                pr.WaitForExit();

                pr.CancelOutputRead();
                pr.CancelErrorRead();

                if (this._Result != "OK")
                {
                    _Logger.Error("[GetKeys] Widevine client: Result:'{0}' Error:'{1}'", this._Result, this._Error);
                    return null;
                }

                return _Keys;
            }
            catch (Exception ex)
            {
                _Logger.Error("[GetKeys] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return null;
            }
        }

        private void cbWidevineClientOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _Logger.Debug("[cbWidevineClientOutputHandler] {0}", e.Data);
                if (e.Data.StartsWith("KEY: "))
                {
                    string[] parts = e.Data.Substring(5).Split(':');
                    if (parts.Length == 2)
                        this._Keys.Add(new ContentProtectionKey(parts[0], parts[1]));
                }
                else if (e.Data.StartsWith("Result: "))
                    this._Result = e.Data.Substring(8);
                else if (e.Data.StartsWith("Error: "))
                    this._Error = e.Data.Substring(7);
            }
        }

        private void cbWidevineClientErrorHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _Logger.Debug("[cbWidevineClientErrorHandler] {0}", e.Data);
        }
    }
}
