//#define DOKAN_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;
using DokanNet;
using DokanNet.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.Pbk.IO.VirtualDrive
{
    public class CryptoVirtualDrive : IDisposable
    {
        [DllImport("kernel32.dll", PreserveSig = true, CharSet = CharSet.Auto)]
        public static extern int GetDiskFreeSpaceEx(
                                                   IntPtr lpDirectoryName,                 // directory name
                                                   out long lpFreeBytesAvailable,    // bytes available to caller
                                                   out long lpTotalNumberOfBytes,    // bytes on disk
                                                   out long lpTotalNumberOfFreeBytes // free bytes on disk
                                                   );

        public const string FILE_META = "VirtualDrive.ini";

        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private DokanInstance _DokanInst;
        private Dokan _Dokan;
        private CryptoVirtualDriveOperations _CryptoOperations = null;

        internal byte[] Key
        { get { return this._Key; } }private byte[] _Key;

        internal byte[] IV
        { get { return this._IV; } }private byte[] _IV;

        List<CryptoVirtualDriveSource> _Sources = new List<CryptoVirtualDriveSource>();

        /// <summary>
        /// Drive's volume label.
        /// </summary>
        public string VolumeLabel
        { get { return this._VolumeLabel; } }private string _VolumeLabel = "Virtual Drive";

        /// <summary>
        /// Readonly mode.
        /// </summary>
        public bool IsReadOnly
        { get { return this._ReadOnly; } }private bool _ReadOnly = false;

        /// <summary>
        /// Virtual drive's mounted point.
        /// </summary>
        public string MountPoint
        { get { return this._MountPoint; } }private string _MountPoint = null;

        public bool IsPool
        {
            get { return this._Sources.Count > 1; }
        }

        #region ctor
        /// <summary>
        /// Create and mount  virtual drive form specified directory. Drive letter will be selected automaticaly.
        /// </summary>
        /// <param name="strVolumeLabel">Volume label</param>
        /// <param name="source">Sources to mount as virtual drive</param>
        /// <param name="strKey">Encryption key in hex format (128 or 256 bits). </param>
        /// <param name="strNonce">Nonce (optional)</param>
        /// <param name="bReadOnly">If true, the drive will be mounted as readonly.</param>
        /// <param name="bIgnoreDir">If true, the drive will by mounted even if the destination directory does not exist.</param>
        public CryptoVirtualDrive(string strVolumeLabel, IEnumerable<CryptoVirtualDriveSource> source, string strKey, string strNonce, bool bReadOnly, bool bIgnoreDir)
        {
            string strMountPoint = null;
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
            for (char c = 'z'; c >= 'a'; c++)
            {
                for (int i = 0; i < drives.Length; i++)
                {
                    if (Char.ToLowerInvariant(drives[i].Name[0]).Equals(c))
                        goto nxt;
                }

                strMountPoint = c.ToString() + ":\\";
                break;

            nxt:
                continue;
            }

            if (strMountPoint == null)
                throw new Exception("[CryptoVirtualDrive] No free drive letter.");

            this.init(strMountPoint, strVolumeLabel, source, strKey, strNonce, bReadOnly, bIgnoreDir);

        }

        /// <summary>
        /// Create and mount virtual drive form specified directory.
        /// </summary>
        /// <param name="strMountPoint">Selected drive letter.</param>
        /// <param name="strVolumeLabel">Volume label</param>
        /// <param name="source">Sources to mount as virtual drive</param>
        /// <param name="strKey">Encryption key in hex format (128 or 256 bits). </param>
        /// <param name="strNonce">Nonce (optional)</param>
        /// <param name="bReadOnly">If true, the drive will be mounted as readonly.</param>
        /// <param name="bIgnoreDir">If true, the drive will by mounted even if the destination directory does not exist.</param>
        public CryptoVirtualDrive(string strMountPoint, string strVolumeLabel, IEnumerable<CryptoVirtualDriveSource> source, string strKey, string strNonce, bool bReadOnly, bool bIgnoreDir)
        {
            this.init(strMountPoint, strVolumeLabel, source, strKey, strNonce, bReadOnly, bIgnoreDir);
        }
        #endregion

        /// <summary>
        /// Create and mount specified directory as virtual drive.
        /// </summary>
        /// <param name="strVolumeLabel">Volume label</param>
        /// <param name="source">Sources to mount as virtual drive</param>
        /// <param name="strKey">Encryption key in hex format (128 or 256 bits). </param>
        /// <param name="strNonce">Nonce (optional)</param>
        /// <param name="bReadOnly">If true, the drive will be mounted as readonly.</param>
        /// <param name="bIgnoreDir">If true, the drive will by mounted even if the destination directory does not exist.</param>
        /// <param name="drive">Resulted virtual drive.</param>
        /// <param name="cDriveLetter">Assigned drive letter.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static CryptoVirtualDriveMountResultEnum Mount(string strVolumeLabel, IEnumerable<CryptoVirtualDriveSource> source, string strKey, string strNonce, bool bReadOnly, bool bIgnoreDir,
            out CryptoVirtualDrive drive, out char cDriveLetter)
        {
            drive = null;
            cDriveLetter = '\0';

            //string strFriendly;
            //try
            //{
            //    checkSource(source, bIgnoreDir, out strFriendly);
            //}
            //catch (Exception ex)
            //{
            //    _Logger.Error("[Mount] Error: " + ex.Message);
            //    return CryptoVirtualDriveMountResultEnum.DirectoryNotExists;
            //}

            if (string.IsNullOrWhiteSpace(strKey))
                return CryptoVirtualDriveMountResultEnum.InvalidKey;

            string strMountPoint = null;
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
            for (char c = 'z'; c >= 'a'; c--)
            {
                for (int i = 0; i < drives.Length; i++)
                {
                    if (drives[i].IsReady && drives[i].VolumeLabel.Equals(strVolumeLabel))
                    {
                        cDriveLetter = c;
                        return CryptoVirtualDriveMountResultEnum.AlreadyMounted;
                    }

                    if (Char.ToLowerInvariant(drives[i].Name[0]).Equals(c))
                        goto nxt;
                }

                strMountPoint = c.ToString() + ":\\";
                break;

            nxt:
                continue;
            }

            if (strMountPoint == null)
                return CryptoVirtualDriveMountResultEnum.NoFreeLetter;

            try
            {
                drive = new CryptoVirtualDrive(strMountPoint, strVolumeLabel, source, strKey, strNonce, bReadOnly, bIgnoreDir);
                if (!drive._CryptoOperations.Initialised)
                    return CryptoVirtualDriveMountResultEnum.WrongKey;
            }
            catch (Exception ex)
            {
                _Logger.Error("[Mount] Error: " + ex.Message);
                drive = null;
                return CryptoVirtualDriveMountResultEnum.Failed;
            }

            cDriveLetter = drive._MountPoint[0];
            return CryptoVirtualDriveMountResultEnum.Mounted;
        }


        /// <summary>
        /// Unmount virtual drive.
        /// </summary>
        public void UnMount()
        {
            this.Dispose();
        }

        public bool IsPathRooted(string strPath, out string strSourceId)
        {
            strSourceId = null;

            if (strPath == "\\")
                return true;

            if (this._Sources.Count > 1)
            {
                //Multi source 

                int iIdx = 1;
                while (iIdx < strPath.Length)
                {
                    if (strPath[iIdx] == '\\' || strPath[iIdx] == '/')
                        break;

                    iIdx++;
                }

                strSourceId = strPath.Substring(1, iIdx - 1);

                return (iIdx + 1) >= strPath.Length;
            }
            else
                return false;
        }

        public List<FileInformation> GetSourceList()
        {
            List<FileInformation> result = new List<FileInformation>();

            this._Sources.ForEach(p =>
                {
                    result.Add(new FileInformation()
                    {
                        FileName = p.ID,
                        LastAccessTime = DateTime.Now,
                        CreationTime = null,
                        LastWriteTime = null,
                        Attributes = FileAttributes.Directory
                    });
                });

            return result;
        }

        public string GetSourcePath(string strSourceId)
        {
            if (string.IsNullOrWhiteSpace(strSourceId))
                return this._Sources[0].DestinationDirectory;

            CryptoVirtualDriveSource src = this._Sources.First(p => p.ID.Equals(strSourceId, StringComparison.CurrentCultureIgnoreCase));

            return src != null ? src.DestinationDirectory : null;
        }

        public long GetFreeSpace(out long lTotalNumberOfBytes)
        {
            lTotalNumberOfBytes = -1;

            for (int i = 0; i < this._Sources.Count; i++)
            {
                if (!this.IsSourceAvailable(this._Sources[i]))
                    return -1;
            }

            long lTotalFreeSpace = 0;
            long lTotalSize = 0;

            this._Sources.ForEach(src =>
            {
                //string strPath = Path.GetPathRoot(src.DestinationDirectory);

                long lBytesAvailable = -1;
                long lTotalBytes;
                long lTotalFreeBytes;
                IntPtr p = Marshal.StringToHGlobalAuto(src.DestinationDirectory);
                try
                {
                    int iVal = GetDiskFreeSpaceEx(p, out lBytesAvailable, out lTotalBytes, out lTotalFreeBytes);

                    lTotalFreeSpace += lTotalFreeBytes;
                    lTotalSize += lTotalBytes;
                }
                finally
                {
                    Marshal.FreeHGlobal(p);
                }


                //DriveInfo di = new DriveInfo(strPath);
                //lTotalFreeSpace += di.TotalFreeSpace;
                //lTotalSize += di.TotalSize;
            });

            lTotalNumberOfBytes = lTotalSize;
            return lTotalFreeSpace;
        }

        public bool IsSourceAvailable(string strSourceId)
        {
            if (string.IsNullOrWhiteSpace(strSourceId))
                return this.IsSourceAvailable(this._Sources[0]);

            return this.IsSourceAvailable(this._Sources.First(p => p.ID.Equals(strSourceId, StringComparison.CurrentCultureIgnoreCase)));
        }
        public bool IsSourceAvailable(CryptoVirtualDriveSource src)
        {
            if (!Directory.Exists(src.DestinationDirectory))
            {
                src.Checked = false;
                return false;
            }

            if (!src.Checked)
                src.Checked = this.checkSource(src.DestinationDirectory);

            return src.Checked;
        }

        public bool IsSourceValid(string strSourceId)
        {
            return this.IsPool && !string.IsNullOrWhiteSpace(strSourceId) && this._Sources.Exists(p => p.ID.Equals(strSourceId, StringComparison.CurrentCultureIgnoreCase));
        }

        #region Private methods
        private static List<CryptoVirtualDriveSource> buildSource(IEnumerable<CryptoVirtualDriveSource> source, bool bIgnoreDir, out string strFriendly)
        {
            if (source == null)
                throw new ArgumentException("[buildSource] Source is null.");

            List<CryptoVirtualDriveSource> pool = new List<CryptoVirtualDriveSource>();

            foreach (CryptoVirtualDriveSource src in source)
            {
                if (src.DestinationDirectory == null || (!bIgnoreDir && (!Directory.Exists(src.DestinationDirectory))))
                    throw new ArgumentException("[buildSource] Invalid source: " + src.DestinationDirectory);

                src.DestinationDirectory = src.DestinationDirectory.Trim().TrimEnd(new char[] { '\\', '/' });

                if (src.ID != null)
                    src.ID = src.ID.Trim().ToLowerInvariant();

                //Make new copy
                pool.Add(new CryptoVirtualDriveSource() { DestinationDirectory = src.DestinationDirectory, ID = src.ID});
            }

            if (pool.Count == 0)
                throw new ArgumentException("[buildSource] No source.");

            StringBuilder sbPoolFriendly = new StringBuilder(256);

            if (pool.Count > 1)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    CryptoVirtualDriveSource src = pool[i];

                    if (string.IsNullOrWhiteSpace(src.ID))
                        throw new ArgumentException("[buildSource] Invalid source ID: " + src.DestinationDirectory);

                    if (pool.Count(p => p.ID.Equals(src.ID)) != 1)
                        throw new ArgumentException("[buildSource] Multiple source ID: " + src.ID);

                    if (sbPoolFriendly.Length > 0)
                        sbPoolFriendly.Append(", ");

                    sbPoolFriendly.Append(src.DestinationDirectory);
                    sbPoolFriendly.Append('|');
                    sbPoolFriendly.Append(src.ID);
                }
            }
            else
                sbPoolFriendly.Append(pool[0].DestinationDirectory);

            strFriendly = sbPoolFriendly.ToString();

            return pool;
        }

        private void init(string strMountPoint, string strVolumeLabel, IEnumerable<CryptoVirtualDriveSource> source, string strKey, string strNonce, bool bReadOnly, bool bIgnoreDir)
        {
            if (string.IsNullOrWhiteSpace(strMountPoint))
                throw new ArgumentException("[CryptoVirtualDrive] Invalid mount point.");

            if (string.IsNullOrWhiteSpace(strKey))
                throw new ArgumentException("[CryptoVirtualDrive] Invalid key.");

            string strPoolFriendly;
            this._Sources.AddRange(buildSource(source, bIgnoreDir, out strPoolFriendly));

            byte[] key = Pbk.Utils.Tools.ParseByteArrayFromHex(strKey);
            if ((key.Length == 16 || key.Length == 32))
            {
                byte[] iv = new byte[16];

                if (!string.IsNullOrWhiteSpace(strNonce))
                {
                    byte[] nonce = Pbk.Utils.Tools.ParseByteArrayFromHex(strNonce);
                    if (nonce.Length > 0)
                        Buffer.BlockCopy(nonce, 0, iv, 0, Math.Min(16, nonce.Length));
                }

                this._MountPoint = strMountPoint;
                this._Key = key;
                this._IV = iv;
                this._ReadOnly = bReadOnly || this._Sources.Count > 1;

                if (!string.IsNullOrWhiteSpace(strVolumeLabel))
                    this._VolumeLabel = strVolumeLabel;

                //Check all sources
                this._Sources.ForEach(p => this.IsSourceAvailable(p));

                this._CryptoOperations = new CryptoVirtualDriveOperations(this);
                if (!this._CryptoOperations.Initialised)
                    return;

#if DOKAN_DEBUG
                CryptoLogger logger = new CryptoLogger(_Logger);
                this._Dokan = new Dokan(logger);
#else
                this._Dokan = new Dokan(null);
#endif

                DokanInstanceBuilder dokanBuilder = new DokanInstanceBuilder(this._Dokan);
                dokanBuilder.ConfigureOptions(options =>
                {
#if DOKAN_DEBUG
                    options.Options = DokanOptions.DebugMode | DokanOptions.StderrOutput;// | DokanOptions.WriteProtection;
#endif
                    options.Options = this._ReadOnly ? DokanOptions.WriteProtection : 0;
                    options.MountPoint = strMountPoint;
                });
#if DOKAN_DEBUG
                dokanBuilder.ConfigureLogger(() => logger);
#endif


                this._DokanInst = dokanBuilder.Build(this._CryptoOperations);

                _Logger.Debug("[init] Virtual drive mounted: '{0}' - '{1}'  ReadOnly: {2}", strMountPoint, strPoolFriendly, this._ReadOnly);
            }
            else
                throw new ArgumentException("[CryptoVirtualDrive] Invalid key length.");
        }

        private bool checkSource(string strDestinationDirectory)
        {
            //Create OpenSSL AES Counter Mode encryptor
            OpenSSL.Crypto.CipherContext crypto = new OpenSSL.Crypto.CipherContext(this._Key.Length == 16 ? OpenSSL.Crypto.Cipher.AES_128_CTR : OpenSSL.Crypto.Cipher.AES_256_CTR);

            byte[] iv = new byte[this._IV.Length];
            Buffer.BlockCopy(this._IV, 0, iv, 0, 8);

            try
            {
                string strFullPath = strDestinationDirectory + '\\' + FILE_META;
                if (!File.Exists(strFullPath) || new FileInfo(strFullPath).Length == 0)
                {
                    using (FileStream fs = new FileStream(strFullPath, FileMode.Create))
                    {
                        crypto.EncryptInit(this._Key, iv);

                        using (CryptoStream stream = new CryptoStream(fs, crypto, this._Key, iv, true))
                        {
                            byte[] data = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?><CryptoVirtualDrive></CryptoVirtualDrive>");
                            stream.Write(data, 0, data.Length);
                        }
                    }

                    new FileInfo(strFullPath).Attributes |= FileAttributes.Hidden;
                }
                else
                {
                    using (FileStream fs = new FileStream(strFullPath, FileMode.Open, System.IO.FileAccess.Read))
                    {
                        crypto.DecryptInit(this._Key, iv);

                        using (CryptoStream stream = new CryptoStream(fs, crypto, this._Key, iv, false))
                        {
                            byte[] data = new byte[fs.Length];
                            int iRd = stream.Read(data, 0, data.Length);
                            if (iRd < 1)
                                return false;

                            string strData = Encoding.UTF8.GetString(data);
                            if (!strData.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?><CryptoVirtualDrive>"))
                                return false;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                crypto = null;
            }

            return true;
        }
        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose virtual drive.
        /// </summary>
        public void Dispose()
        {
            if (this._Dokan != null)
            {
                this._DokanInst.Dispose();
                this._DokanInst = null;

                this._Dokan.Dispose();
                this._Dokan = null;

                this._CryptoOperations = null;

                _Logger.Debug("[Dispose] Virtual drive unmounted: " + this._MountPoint);
            }
        }

        #endregion
    }
}
