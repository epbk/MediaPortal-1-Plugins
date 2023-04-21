using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.GUI.Library;
using MediaPortal.Dialogs;
using Common.GUIPlugins;
using NLog;

namespace MediaPortal.Pbk.Net
{
    public class ServerUtils
    {
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        public static bool ServerWakeUp(string strUncPath)
        {
            if (string.IsNullOrWhiteSpace(strUncPath))
                return false;

            string strPath = null;

            if (strUncPath.Length > 0 && Char.ToLowerInvariant(strUncPath[0]) == Pbk.IO.VirtualDrive.CryptoVirtualDriveShared.VirtualDriveLetter)
                strPath = Pbk.IO.VirtualDrive.CryptoVirtualDriveShared.GetSourceDestination(strUncPath);

            if (strPath == null)
                strPath = strUncPath;

            if (!Util.Utils.IsUNCNetwork(strPath))
            {
                //Folder is not UNC path
                _Logger.Debug("[ServerWakeUp] FilePath is not UNC. No need to wakeup.");
                return true;
            }

            string strServerName = Util.Utils.GetServerNameFromUNCPath(strPath);

            //Call the handler to wakeup the server if needeed
            if (!WakeupUtils.HandleWakeUpServer(strServerName, 60))
            {
                _Logger.Debug("[ServerWakeUp] Failed to wakeup the server: '{0}'", strServerName);
                return false; //Failed to wake up the server
            }

            if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(strUncPath)))
            {
                //Folder exists
                _Logger.Debug("[ServerWakeUp] FilePath exists.");
                return true;
            }

            GUIDialogProgress progressDialog = (GUIDialogProgress)GUIWindowManager.GetWindow(101); //(int)Window.WINDOW_DIALOG_PROGRESS
            progressDialog.Reset();
            progressDialog.SetHeading("Processing");
            progressDialog.ShowProgressBar(true);
            progressDialog.SetLine(1, "Please wait...");
            progressDialog.StartModal(GUIWindowManager.ActiveWindow);

            try
            {
                DateTime ts = DateTime.Now;
                int iSecElapsed = 0;
                const int MAX_TIME = 60;

                while (!System.IO.File.Exists(strUncPath))
                {
                    iSecElapsed = (int)(DateTime.Now - ts).TotalSeconds;
                    if (iSecElapsed > MAX_TIME)
                    {
                        _Logger.Warn("[ServerWakeUp] Warning: the requested file is still not ready.");
                        return false;
                    }
                    else
                    {
                        progressDialog.SetPercentage((int)((float)iSecElapsed / MAX_TIME * 100));
                        progressDialog.Progress();
                        System.Threading.Thread.Sleep(1000);
                    }
                }

                _Logger.Debug("[ServerWakeUp] The server is awake and ready: '{0}'", strServerName);
                return true;
            }
            finally
            {
                if (progressDialog != null)
                    progressDialog.Close();
            }
        }
    }
}
