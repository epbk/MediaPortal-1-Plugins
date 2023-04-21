using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.Pbk.IO.VirtualDrive
{
    public class CryptoVirtualDriveShared
    {
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private static CryptoVirtualDrive _VirtualDrive = null;

        
        public static char VirtualDriveLetter
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                if (_VirtualDriveLetter != '\0')
                    return _VirtualDriveLetter;

                return _VirtualDrive != null ? _VirtualDrive.MountPoint[0] : '\0';
            }
        }private static char _VirtualDriveLetter = '\0';

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static CryptoVirtualDriveMountResultEnum Mount(string strPath, string strKey)
        {
            if (_VirtualDrive != null)
                return CryptoVirtualDriveMountResultEnum.AlreadyMounted;

            if (string.IsNullOrWhiteSpace(strPath))
                return CryptoVirtualDriveMountResultEnum.InvalidSource;

            List<Pbk.IO.VirtualDrive.CryptoVirtualDriveSource> source = new List<Pbk.IO.VirtualDrive.CryptoVirtualDriveSource>();
            string[] parts = strPath.Split('|');
            if (parts.Length > 1)
            {
                //Pool

                if ((parts.Length % 2) != 0)
                    return CryptoVirtualDriveMountResultEnum.InvalidSource;

                for (int i = 0; i < parts.Length; i += 2)
                {
                    source.Add(new Pbk.IO.VirtualDrive.CryptoVirtualDriveSource() { DestinationDirectory = parts[i], ID = parts[i + 1] });
                }
            }
            else
                source.Add(new Pbk.IO.VirtualDrive.CryptoVirtualDriveSource() { DestinationDirectory = strPath });


            CryptoVirtualDriveMountResultEnum result = MediaPortal.Pbk.IO.VirtualDrive.CryptoVirtualDrive.Mount(
                "MediaPortal Crypto Drive",
                source,
                strKey,
                null,
                true,
                true,
                out _VirtualDrive,
                out _VirtualDriveLetter);

            _Logger.Debug("[Mount] Mount virtual drive: '{0}' {1}", _VirtualDriveLetter, result);

            return result;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Unmount()
        {
            //Close virtual drive
            if (_VirtualDrive != null)
            {
                _Logger.Debug("[Unmount] UnMount virtual drive: '{0}'", _VirtualDriveLetter);

                _VirtualDrive.UnMount();
                _VirtualDrive = null;
                _VirtualDriveLetter = '\0';
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static string GetSourceDestination(string strPath)
        {
            if (_VirtualDrive != null && !string.IsNullOrWhiteSpace(strPath))
            {
                string strSourceId = null;

                if (_VirtualDrive.IsPool)
                {
                    int iIdxStart = strPath.IndexOf('\\') + 1;
                    int iIdx = iIdxStart;
                    while (iIdx < strPath.Length)
                    {
                        if (strPath[iIdx] == '\\' || strPath[iIdx] == '/')
                            break;

                        iIdx++;
                    }

                    strSourceId = strPath.Substring(iIdxStart, iIdx - iIdxStart);
                }

                return _VirtualDrive.GetSourcePath(strSourceId);
            }

            return null;
        }
    }
}
