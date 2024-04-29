using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class Widevine
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();
        private static readonly List<ContentProtectionKey> _Keys = new List<ContentProtectionKey>();
        private static readonly List<ContentProtectionBox> _Boxes = new List<ContentProtectionBox>();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static string GetKey(string strPSSH, string strKID, string strLicenceServer)
        {
            ContentProtectionBox box = _Boxes.Find(b => b.PSSH == strPSSH);
            while (true)
            {
                if (box != null)
                {
                    box.LastAccess = DateTime.Now;
                    ContentProtectionKey key = box.Keys.Find(k => k.KID == strKID);
                    if (key != null)
                        return key.Key; //got it

                    //Check last refresh
                    if ((DateTime.Now - box.LastRefresh).TotalSeconds <= 60)
                    {
                        _Logger.Error("[GetKey] Key not found: {0}", strKID);
                        return null;
                    }

                    //Clear all keys
                    box.Keys.Clear();
                }

                //Maintenance
                for (int i = _Boxes.Count - 1; i >= 0; i--)
                {
                    if ((DateTime.Now - _Boxes[i].LastAccess).TotalMinutes >= 60)
                        _Boxes.RemoveAt(i);
                }

                #region Retrieve keys from Widevine client

                _Keys.Clear();

                _Logger.Debug("[GetKey] Call Widevine client: PSSH:{0} LicenceServer:{1}", strPSSH, strLicenceServer);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    Arguments = (!string.IsNullOrWhiteSpace(strLicenceServer) ? " --licence-server " + strLicenceServer : string.Empty) + " --pssh " + strPSSH,
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
                pr.OutputDataReceived += cbWidevineClientOutputHandler;
                pr.ErrorDataReceived += cbWidevineClientErrorHandler;

                //Start
                pr.Start();
                pr.BeginOutputReadLine();
                pr.BeginErrorReadLine();

                //Wait for finish
                pr.WaitForExit();

                pr.CancelOutputRead();
                pr.CancelErrorRead();

                if (_Keys.Count == 0)
                {
                    _Logger.Error("[GetKey] Failed to get Widevine keys: {0}", strPSSH);
                    return null;
                }
                else
                {
                    if (box == null)
                    {
                        box = new ContentProtectionBox(strPSSH);
                        _Boxes.Add(box);
                    }

                    box.Keys.AddRange(_Keys);
                    box.LastRefresh = DateTime.Now;
                    box.LastAccess = DateTime.Now;
                }

                #endregion
            }
        }

        private static void cbWidevineClientOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _Logger.Debug("[cbWidevineClientOutputHandler] " + e.Data);
                if (e.Data.StartsWith("KEY: "))
                {
                    string[] parts = e.Data.Substring(5).Split(':');
                    if (parts.Length == 2)
                        _Keys.Add(new ContentProtectionKey(parts[0], parts[1]));
                }
            }
        }

        private static void cbWidevineClientErrorHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _Logger.Debug("[cbWidevineClientErrorHandler] " + e.Data);
        }
    }
}
