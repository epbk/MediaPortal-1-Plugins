using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using NLog;
using MediaPortal.IptvChannels.Database;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class Widevine
    {
        private static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        private static readonly List<dbContentProtectionBox> _Boxes = new List<dbContentProtectionBox>();

        public static string GetKey(string strPSSH, string strKID, string strLicenceServer, Pbk.Net.Http.HttpUserWebRequestArguments httpArgs, bool bPermanent = false)
        {
            bool bLocked = false;
            try
            {
                Monitor.Enter(_Boxes, ref bLocked);
                dbContentProtectionBox box = _Boxes.Find(b => b.LicenceServer == strLicenceServer && b.PSSH == strPSSH);
                while (true)
                {
                    if (box == null && bPermanent)
                    {
                        //Try get the BOX from DB
                        box = dbContentProtectionBox.Get(strLicenceServer, strPSSH);
                        if (box != null)
                        {
                            _Boxes.Add(box);
                            _Logger.Debug("[GetKey] PSSH found in DB: {0}, {1}", strLicenceServer, strPSSH);
                        }
                    }

                    if (box != null)
                    {
                        box.LastAccess = DateTime.Now;

                        if (bPermanent)
                        {
                            box.CommitNeeded = true;
                            box.Commit();
                        }

                        dbContentProtectionKey key = box.Keys.Find(k => k.KID == strKID);
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
                        box = new dbContentProtectionBox(strLicenceServer, strPSSH) { LastAccess = DateTime.Now };
                        _Boxes.Add(box);

                        if (bPermanent)
                        {
                            box.CommitNeeded = true;
                            box.Commit();
                        }
                    }

                    //Maintenance
                    for (int i = _Boxes.Count - 1; i >= 0; i--)
                    {
                        if (_Boxes[i].ID == null && (DateTime.Now - _Boxes[i].LastAccess).TotalMinutes >= 60)
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
                        if (!bPermanent)
                            box.Keys.Clear();

                        box.Refreshing = true;
                        box.FlagRefreshDone.Reset();

                        //Leave boxes lock
                        Monitor.Exit(_Boxes);
                        bLocked = false;

                        List<dbContentProtectionKey> keys = null;

                        try
                        {
                            //Retrieve keys from Widevine client
                            WidevineProcess procWv = new WidevineProcess();
                            keys = procWv.GetKeys(strPSSH, strLicenceServer, httpArgs);
                            if (keys != null && bPermanent)
                            {
                                keys.ForEach(k =>
                                {
                                    k.IdParent = (int)box.ID;
                                    k.LastRefresh = DateTime.Now;
                                    k.CommitNeeded = true;
                                    k.Commit();
                                });
                            }
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
                            if (bPermanent)
                            {
                                box.CommitNeeded = true;
                                box.Commit();
                            }
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
