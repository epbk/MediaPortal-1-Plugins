using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using NLog;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class Widevine
    {
        private static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        private static readonly List<ContentProtectionBox> _Boxes = new List<ContentProtectionBox>();

        public static string GetKey(string strPSSH, string strKID, string strLicenceServer, Pbk.Net.Http.HttpUserWebRequestArguments httpArgs)
        {
            bool bLocked = false;
            try
            {
                Monitor.Enter(_Boxes, ref bLocked);
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
                    }
                    else
                    {
                        //New box
                        box = new ContentProtectionBox(strPSSH) { LastAccess = DateTime.Now };
                        _Boxes.Add(box);
                    }

                    //Maintenance
                    for (int i = _Boxes.Count - 1; i >= 0; i--)
                    {
                        if ((DateTime.Now - _Boxes[i].LastAccess).TotalMinutes >= 60)
                            _Boxes.RemoveAt(i);
                    }

                    if (box.Refreshing)
                    {
                        Monitor.Exit(_Boxes);
                        bLocked = false;

                        //Already processing; wait for result
                        box.FlagRefreshDone.WaitOne();

                        //Reenter boxes lock
                        Monitor.Enter(_Boxes, ref bLocked);
                    }
                    else
                    {
                        //Clear all keys
                        box.Keys.Clear();

                        box.Refreshing = true;
                        box.FlagRefreshDone.Reset();

                        //Leave boxes lock
                        Monitor.Exit(_Boxes);
                        bLocked = false;

                        List<ContentProtectionKey> keys = null;

                        try
                        {
                            //Retrieve keys from Widevine client
                            WidevineProcess procWv = new WidevineProcess();
                            keys = procWv.GetKeys(strPSSH, strLicenceServer, httpArgs);
                        }
                        finally
                        {
                            //Reenter boxes lock
                            Monitor.Enter(_Boxes, ref bLocked);

                            //Refresh complete
                            box.Refreshing = false;
                            box.FlagRefreshDone.Set();
                        }

                        //Update the box
                        if (keys != null && keys.Count > 0)
                        {
                            box.Keys.AddRange(keys);
                            box.LastRefresh = DateTime.Now;
                            box.LastAccess = DateTime.Now;
                        }
                        else
                        {
                            _Logger.Error("[GetKey] Failed to get Widevine keys: {0}", strPSSH);
                            return null;
                        }
                    }
                }
            }
            finally
            {
                if (bLocked)
                    Monitor.Exit(_Boxes);
            }
        }
    }
}
