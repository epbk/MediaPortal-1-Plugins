#define REAL_PATH_CACHE
#define ENCRYPTOR_CACHING
#define DECRYPTOR_CACHING

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using Microsoft.Win32.SafeHandles;
using DokanNet;
using DokanNet.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.Pbk.IO.VirtualDrive
{
    public class CryptoVirtualDriveOperations : IDokanOperations, IDokanOperationsUnsafe
    {
        private const int _LENTGH_MAX_SEGMENT_PLAIN = 167;
        private const int _LENTGH_HASH = 8;

#if REAL_PATH_CACHE
        private const int _CACHED_PATH_LIFTIME = 10000;

        private class CachedPath
        {
            public bool IsDirectory = false;
            public DateTime TimeStamp = DateTime.Now;
        }
#endif

        private static readonly char[] _Base64TableEx = new char[64]
            {
	            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', //0-25
	            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', //26-51
	            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', //52-61
                '+', '_' //62, 63
            };

        private static readonly byte[] _Base64TableReverseEx = new byte[128] //value 255 = trap
            {
	            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, //0-15
                255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, //16-31
                255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 62, 255, 255, 255, 255, //32-47
                52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 255, 255, 255, 64, 255, 255, //48-63
                255, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, //64-79
                15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 255, 255, 255, 255, 63, //80-95
                255, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, //96-111
                41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 255, 255, 255, 255, 255 //112-127
            };

        private CryptoVirtualDrive _Drive = null;

        private ICryptoTransform _Encryptor;
        private ICryptoTransform _Decryptor;

        private StringBuilder _SbRealPath = new StringBuilder(1024);
        private char[] _RealPathSegment = new char[256];
        private byte[] _EncryptedPathSegment = new byte[256];
        private byte[] _DataPlain = new byte[256];

        private static MD5 _CryptoMD5 = MD5.Create();

#if REAL_PATH_CACHE
        private DateTime _RealPathCacheLastUse = DateTime.MinValue;
        private Dictionary<string, CachedPath> _RealPathCache = new Dictionary<string, CachedPath>();
#endif

#if ENCRYPTOR_CACHING
        private const int _ENCRYPTOR_CACHE_LIFETIME = 1000 * 60 * 5; //5mins
        private DateTime _EncryptorCacheLastUse = DateTime.MinValue;
        private Dictionary<string, string> _EncryptorCache = new Dictionary<string, string>();
#endif

#if DECRYPTOR_CACHING
        private const int _DECRYPTOR_CACHE_LIFETIME = 1000 * 60 * 5; //5mins
        private DateTime _DecryptorCacheLastUse = DateTime.MinValue;
        private Dictionary<string, string> _DecryptorCache = new Dictionary<string, string>();
#endif

        private const DokanNet.FileAccess _DATA_ACCESS = DokanNet.FileAccess.ReadData | DokanNet.FileAccess.WriteData | DokanNet.FileAccess.AppendData |
                              DokanNet.FileAccess.Execute |
                              DokanNet.FileAccess.GenericExecute | DokanNet.FileAccess.GenericWrite |
                              DokanNet.FileAccess.GenericRead;

        private const DokanNet.FileAccess _DATA_WRITE_ACCESS = DokanNet.FileAccess.WriteData | DokanNet.FileAccess.AppendData |
                                                   DokanNet.FileAccess.Delete |
                                                   DokanNet.FileAccess.GenericWrite;



        public bool Initialised
        {
            get { return this._Initialised; }
        }private bool _Initialised = false;



        #region ctor
        public CryptoVirtualDriveOperations(CryptoVirtualDrive drive)
        {
            try
            {
                this._Drive = drive;

                //Init crypto
                RijndaelManaged rijndael = new RijndaelManaged()
                {
                    Key = drive.Key,
                    IV = drive.IV
                };

                this._Encryptor = rijndael.CreateEncryptor();
                this._Decryptor = rijndael.CreateDecryptor();

                this._Initialised = true;
            }
            catch { }

        }
        #endregion

        #region Private methods

#if REAL_PATH_CACHE
        private bool realPathCacheExists(string strRealPath, out bool bIsDir)
        {
            lock (this._RealPathCache)
            {
                CachedPath path = null;

                if ((DateTime.Now - this._RealPathCacheLastUse).TotalMilliseconds >= _CACHED_PATH_LIFTIME)
                {
                    this._RealPathCache.Clear();
                    this._RealPathCacheLastUse = DateTime.Now;
                }
                else
                {
                    this._RealPathCacheLastUse = DateTime.Now;

                    if (this._RealPathCache.TryGetValue(strRealPath, out path))
                    {
                        if ((DateTime.Now - path.TimeStamp).TotalMilliseconds < _CACHED_PATH_LIFTIME)
                        {
                            bIsDir = path.IsDirectory;
                            return true;
                        }
                    }
                }

                bool bResult = LongPath.Common.Exists(strRealPath, out bIsDir);

                if (bResult)
                {
                    if (path != null)
                        path.TimeStamp = DateTime.Now;
                    else
                        this._RealPathCache.Add(strRealPath, new CachedPath() { IsDirectory = bIsDir });
                }
                else if (path != null)
                    this._RealPathCache.Remove(strRealPath);

                return bResult;
            }
        }

        private void realPathCacheAdd(string strRealPath, bool bIsDir)
        {
            lock (this._RealPathCache)
            {
                CachedPath path;
                if (this._RealPathCache.TryGetValue(strRealPath, out path))
                {
                    if ((DateTime.Now - path.TimeStamp).TotalMilliseconds >= _CACHED_PATH_LIFTIME)
                        path.TimeStamp = DateTime.Now;

                    return;
                }

                this._RealPathCache.Add(strRealPath, new CachedPath() { IsDirectory = bIsDir });
            }
        }

        private void realPathCacheRemove(string strRealPath)
        {
            lock (this._RealPathCache)
            {
                this._RealPathCache.Remove(strRealPath);
            }
        }
#endif

        private void deleteFolder(string strDir)
        {
            if (!LongPath.Directory.Exists(strDir))
                return;

            string[] dirs = LongPath.Directory.GetDirectories(strDir);
            for (int i = 0; i < dirs.Length; i++)
                this.deleteFolder(dirs[i]);

            string[] files = LongPath.Directory.GetFiles(strDir);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
#if REAL_PATH_CACHE
                    this.realPathCacheRemove(files[i]);
#endif
                    LongPath.File.Delete(files[i]);
                }
                catch { };
            }

            try
            {
#if REAL_PATH_CACHE
                this.realPathCacheRemove(strDir);
#endif
                LongPath.Directory.Delete(strDir);
            }
            catch { }
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [System.Runtime.InteropServices.ComVisible(false)]
        private unsafe int encrypt(string strPlain, StringBuilder sbOut)
        {
            try
            {
                if (strPlain.Length == 0)
                    return 0;

#if ENCRYPTOR_CACHING
                if ((DateTime.Now - this._EncryptorCacheLastUse).TotalMilliseconds >= _ENCRYPTOR_CACHE_LIFETIME)
                {
                    this._EncryptorCache.Clear();
                    this._EncryptorCacheLastUse = DateTime.Now;
                }
                else
                {
                    this._EncryptorCacheLastUse = DateTime.Now;

                    string strResultCached;
                    if (this._EncryptorCache.TryGetValue(strPlain, out strResultCached))
                    {
                        //Result
                        sbOut.Append(strResultCached);
                        return strResultCached.Length;
                    }
                }
#endif

                //Plain data preparation

                int iPlainLength = strPlain.Length;
                int iSize;

                fixed (char* pPlain = strPlain)
                {
                    fixed (byte* pData = this._DataPlain)
                    {
                        //UTF8 encoding
                        iSize = Encoding.UTF8.GetBytes(pPlain, iPlainLength, pData + _LENTGH_HASH, 256 - _LENTGH_HASH);

                        if (iSize > _LENTGH_MAX_SEGMENT_PLAIN) //167 bytes
                            throw new Exception("Path segment length greater then " + _LENTGH_MAX_SEGMENT_PLAIN + " chars.");

                        //Hash calculation
                        byte[] hash = _CryptoMD5.ComputeHash(this._DataPlain, _LENTGH_HASH, iSize);
                        int iHashLength = hash.Length;
                        fixed (byte* pHash = hash)
                        {
                            //Place hash(last 8 bytes) at the beginning
                            for (int i = 0; i < _LENTGH_HASH; i++)
                                pData[i] = pHash[iHashLength - _LENTGH_HASH + i];
                        }
                    }
                }

                // Encryption
                // Rule: max file segment length = 255 chars + \0
                // max plain input: 175; max encrypted output: 176; max base64 output: 236
                byte[] dataEnc = this._Encryptor.TransformFinalBlock(this._DataPlain, 0, iSize + _LENTGH_HASH);

                int iLength = dataEnc.Length;

                #region Base64 encoding

                int iReminder = iLength % 3;

                int iIdxOut = 0;

                //Pin the path segment array
                fixed (char* pDataOut = this._RealPathSegment)
                {
                    fixed (byte* pDataIn = dataEnc)
                    {
                        // get a pointer to the base64Table to avoid unnecessary range checking
                        fixed (char* pTable = _Base64TableEx)
                        {
                            int iIdxInEnd = (iLength - iReminder);
                            int iIdxIn = 0;

                            while (iIdxIn < iIdxInEnd) //every 3 bytes into 4 chars
                            {
                                pDataOut[iIdxOut++] = pTable[pDataIn[iIdxIn] >> 2];
                                pDataOut[iIdxOut++] = pTable[((pDataIn[iIdxIn++] & 0x03) << 4) | (pDataIn[iIdxIn] >> 4)];
                                pDataOut[iIdxOut++] = pTable[((pDataIn[iIdxIn++] & 0x0F) << 2) | (pDataIn[iIdxIn] >> 6)];
                                pDataOut[iIdxOut++] = pTable[(pDataIn[iIdxIn++] & 0x3F)];
                            }

                            switch (iReminder)
                            {
                                case 2: //One character padding needed
                                    pDataOut[iIdxOut++] = pTable[pDataIn[iIdxIn] >> 2];
                                    pDataOut[iIdxOut++] = pTable[((pDataIn[iIdxIn++] & 0x03) << 4) | (pDataIn[iIdxIn] >> 4)];
                                    pDataOut[iIdxOut++] = pTable[(pDataIn[iIdxIn] & 0x0F) << 2];
                                    pDataOut[iIdxOut++] = '='; //Pad
                                    break;

                                case 1: // Two character padding needed
                                    pDataOut[iIdxOut++] = pTable[pDataIn[iIdxIn] >> 2];
                                    pDataOut[iIdxOut++] = pTable[(pDataIn[iIdxIn] & 0x03) << 4];
                                    pDataOut[iIdxOut++] = '='; //Pad
                                    pDataOut[iIdxOut++] = '='; //Pad
                                    break;
                            }
                        }
                    }
                }
                #endregion

                //Result
#if ENCRYPTOR_CACHING
                this._EncryptorCache.Add(strPlain, new string(this._RealPathSegment, 0, iIdxOut));
#endif
                sbOut.Append(this._RealPathSegment, 0, iIdxOut);
                return iIdxOut;
            }
            catch { ((RijndaelManagedTransform)this._Encryptor).Reset(); }

            return 0;
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [System.Runtime.InteropServices.ComVisible(false)]
        private unsafe string decrypt(string strEncrypted)
        {
            try
            {
                int iDataInLength = strEncrypted.Length;

                if ((iDataInLength & 0x03) != 0 || iDataInLength < 4)
                    return null;

#if DECRYPTOR_CACHING
                if ((DateTime.Now - this._DecryptorCacheLastUse).TotalMilliseconds >= _DECRYPTOR_CACHE_LIFETIME)
                {
                    this._DecryptorCache.Clear();
                    this._DecryptorCacheLastUse = DateTime.Now;
                }
                else
                {
                    this._DecryptorCacheLastUse = DateTime.Now;

                    string strResultCached;
                    if (this._DecryptorCache.TryGetValue(strEncrypted, out strResultCached))
                    {
                        //Result
                        return strResultCached;
                    }
                }
#endif

                #region Base64 decode

                int iDataOutLength = (iDataInLength >> 2) * 3;

                fixed (char* pDataIn = strEncrypted)
                {
                    if (pDataIn[iDataInLength - 1] == '=')
                    {
                        iDataOutLength--;

                        if (pDataIn[iDataInLength - 2] == '=')
                            iDataOutLength--;
                    }

                    if (iDataOutLength > 255)
                        throw new Exception("Path segment length greater then 255 chars.");

                    fixed (byte* pDataOut = this._EncryptedPathSegment)
                    {
                        char* pSrc = pDataIn;
                        byte* pDst = pDataOut;

                        char* pSrcEnd = pSrc + iDataInLength;
                        byte* pDstEnd = pDst + iDataOutLength;

                        uint wCode;
                        uint wBlock = 0x000000FFu; //block with the ending marker

                        unchecked
                        {
                            fixed (byte* pTable = _Base64TableReverseEx)
                            {
                                while (true)
                                {
                                    if (pSrc >= pSrcEnd)
                                        goto ext; //end

                                    //Decoding

                                    wCode = (uint)(*pSrc++);
                                    if (wCode >= 128u) //out of table
                                        throw new FormatException("Format_BadBase64Char");

                                    wCode = pTable[wCode]; //decode the value with the help of reverse table; fastest way

                                    if (wCode == 64u)
                                        goto padding; //padding char reached
                                    else if (wCode > 64u) //trap
                                        throw new FormatException("Format_BadBase64Char");

                                    wBlock = (wBlock << 6) | wCode; //save the decoded value into the block

                                    if ((wBlock & 0x80000000u) != 0u) //4 char shift complete; save the result to 3 bytes
                                    {
                                        if ((int)(pDstEnd - pDst) < 3)
                                            return null; //not enough space

                                        *(pDst++) = (byte)(wBlock >> 16);
                                        *(pDst++) = (byte)(wBlock >> 8);
                                        *(pDst++) = (byte)(wBlock);

                                        wBlock = 0x000000FFu; //init again
                                    }
                                }
                            }
                        }

                    padding:
                        if (pSrc == pSrcEnd) //single padding
                        {
                            wBlock <<= 6;

                            if ((wBlock & 0x80000000u) == 0u) //verify the marker
                                throw new FormatException("Format_BadBase64CharArrayLength");

                            if ((int)(pDstEnd - pDst) < 2)
                                return null; //not enough space

                            *(pDst++) = (byte)(wBlock >> 16);
                            *(pDst) = (byte)(wBlock >> 8);

                            wBlock = 0x000000FFu;
                        }
                        else if (pSrc == (pSrcEnd - 1) && *(pSrc) == '=') //double padding
                        {
                            wBlock <<= 12;

                            if ((wBlock & 0x80000000u) == 0u) //verify the marker
                                throw new FormatException("Format_BadBase64CharArrayLength");

                            if ((int)(pDstEnd - pDst) < 1)
                                return null; //not enough space

                            *(pDst) = (byte)(wBlock >> 16);

                            wBlock = 0x000000FFu;
                        }
                        else
                            throw new FormatException("Format_BadBase64Char");

                    ext:
                        if (wBlock != 0x000000FFu) //check the new block
                            throw new FormatException("Format_BadBase64CharArrayLength");
                    }
                }
                #endregion

                if ((iDataOutLength & 0x0F) != 0 || iDataOutLength == 0)
                    return null;

                //Decrypt
                byte[] dataPlain = this._Decryptor.TransformFinalBlock(this._EncryptedPathSegment, 0, iDataOutLength);

                //Get plain text
                string strResult = Encoding.UTF8.GetString(dataPlain, _LENTGH_HASH, dataPlain.Length - _LENTGH_HASH); //skip hash
#if ENCRYPTOR_CACHING
                this._DecryptorCache.Add(strEncrypted, strResult);
#endif
                return strResult;
            }
            catch { ((RijndaelManagedTransform)this._Decryptor).Reset(); }

            return null;
        }


        private string getRealPath(string strPathPlain)
        {
            string strRootReal;

            bool bIsPool = this._Drive.IsPool;

            if (bIsPool)
            {
                //Multi source

                if (strPathPlain == "\\")
                    return strPathPlain;

                string strSourceId;
                bool bIsRooted = this._Drive.IsPathRooted(strPathPlain, out strSourceId);
                strRootReal = this._Drive.GetSourcePath(strSourceId);
                if (bIsRooted)
                    return strRootReal;
            }
            else
            {
                //Single source 
                strRootReal = this._Drive.GetSourcePath(null);

                if (strPathPlain == "\\")
                    return strRootReal;
            }

            lock (this._Encryptor)
            {
                this._SbRealPath.Clear();
                this._SbRealPath.Append(strRootReal);

                //In case of pool we need to skip first path element(sourceiD)
                bool bProceed = !bIsPool;

                bool bCheck = true;

                string[] parts = strPathPlain.Split('\\');
                for (int i = 0; i < parts.Length; i++)
                {
                    string strPart = parts[i];

                    if (strPart.Length > 0)
                    {
                        if (bProceed)
                        {
                            //Remember current path
                            int iPathParent = this._SbRealPath.Length;

                            this._SbRealPath.Append('\\');
                            this.encrypt(strPart, this._SbRealPath);

                            if (bCheck)
                            {
                                bool bIsDir;
#if REAL_PATH_CACHE
                                if (!this.realPathCacheExists(this._SbRealPath.ToString(), out bIsDir))
#else
                                if (!LongPath.Common.Exists(this._SbRealPath.ToString(), out bIsDir))
#endif
                                {
                                    //Real path does not exist; try look for matching one (ignore case)

                                    //Parent dir
                                    string strPathParent = this._SbRealPath.ToString(0, iPathParent);
#if REAL_PATH_CACHE
                                    if (this.realPathCacheExists(strPathParent, out bIsDir) && bIsDir)
#else
                                    //if (LongPath.Common.Exists(strPathParent, out bIsDir) && bIsDir)
#endif
                                    {
                                        //Get all files/directories
                                        foreach (LongPath.NativeMethods.WIN32_FIND_DATA item in getDirectoryItems(strPathParent))
                                        {
                                            string strFilename = item.cFileName;
                                            if (!strFilename.Equals(CryptoVirtualDrive.FILE_META, StringComparison.CurrentCultureIgnoreCase))
                                            {
                                                string strFilenamePlain;
                                                lock (this._Decryptor)
                                                {
                                                    strFilenamePlain = this.decrypt(strFilename);
                                                }

                                                if (strFilenamePlain != null && strFilenamePlain.Equals(strPart, StringComparison.CurrentCultureIgnoreCase))
                                                {
                                                    //Remove last case invalid part
                                                    this._SbRealPath.Length = iPathParent + 1;

                                                    //Place the matching one
                                                    this._SbRealPath.Append(strFilename);

#if REAL_PATH_CACHE
                                                    //Add to cache
                                                    this.realPathCacheAdd(this._SbRealPath.ToString(), (item.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory);
#endif

                                                    //Proceed with next part
                                                    goto cont;
                                                }
                                            }
                                        }
                                    }

                                    //not found; do not check anymore
                                    bCheck = false;
                                }
                            }
                        }
                        else
                            bProceed = true; // pool: now we can proceed with the next path elements
                    }

                cont:
                    continue;
                }

                return this._SbRealPath.ToString();
            }
        }


        private static IEnumerable<LongPath.NativeMethods.WIN32_FIND_DATA> getDirectoryItems(string strPath)
        {
            string strNormalizedPath = LongPath.Path.NormalizeLongPath(strPath);

            // First check whether the specified path refers to a directory and exists
            System.IO.FileAttributes attributes;
            int iErrorCode = LongPath.Common.TryGetDirectoryAttributes(strNormalizedPath, out attributes);
            if (iErrorCode != 0)
                throw LongPath.Common.GetExceptionFromWin32Error(iErrorCode);

            LongPath.NativeMethods.WIN32_FIND_DATA data;
            using (LongPath.SafeFindHandle handle = LongPath.Directory.BeginFind(Path.Combine(strNormalizedPath, "*"), out data))
            {
                if (handle == null)
                    yield break;

                do
                {
                    yield return data;

                } while (LongPath.NativeMethods.FindNextFile(handle, out data));

                iErrorCode = Marshal.GetLastWin32Error();
                if (iErrorCode != LongPath.NativeMethods.ERROR_NO_MORE_FILES)
                    throw LongPath.Common.GetExceptionFromWin32Error(iErrorCode);
            }
        }

        private List<FileInformation> getDirectory(string strPath, string strFilter)
        {
            if (!string.IsNullOrWhiteSpace(strPath))
            {
                //Root source list
                if (this._Drive.IsPool && strPath == "\\")
                    return this._Drive.GetSourceList();

                bool bFilterInUse = !string.IsNullOrWhiteSpace(strFilter);

                string strPathReal = this.getRealPath(strPath);

                List<FileInformation> result = new List<FileInformation>();

                lock (this._Decryptor)
                {
                    foreach (LongPath.NativeMethods.WIN32_FIND_DATA item in getDirectoryItems(strPathReal))
                    {
                        if (item.cFileName.Equals(CryptoVirtualDrive.FILE_META, StringComparison.CurrentCultureIgnoreCase))
                            continue;

                        string strNamePlain = this.decrypt(item.cFileName);
                        if (strNamePlain != null && (!bFilterInUse || DokanHelper.DokanIsNameInExpression(strFilter, strNamePlain, true)))
                        {
                            result.Add(new FileInformation()
                            {
                                FileName = strNamePlain,
                                CreationTime = DateTime.FromFileTimeUtc(((long)item.ftCreationTime.dwHighDateTime << 32) | (item.ftCreationTime.dwLowDateTime & 0xffffffff)),
                                LastAccessTime = DateTime.FromFileTimeUtc(((long)item.ftLastAccessTime.dwHighDateTime << 32) | (item.ftLastAccessTime.dwLowDateTime & 0xffffffff)),
                                LastWriteTime = DateTime.FromFileTimeUtc(((long)item.ftLastWriteTime.dwHighDateTime << 32) | (item.ftLastWriteTime.dwLowDateTime & 0xffffffff)),
                                Attributes = item.dwFileAttributes,
                                Length = ((long)item.nFileSizeHigh << 32) | (uint)item.nFileSizeLow
                            });
                        }
                    }

                    ////Dirs
                    //foreach (string strItem in LongPath.Directory.EnumerateFileSystemEntries(strPathReal, "*", true, false, SearchOption.TopDirectoryOnly))
                    //{
                    //    LongPath.DirectoryInfo di = new LongPath.DirectoryInfo(strItem);
                    //    string strNamePlain = this.decrypt(di.Name);
                    //    if (strNamePlain != null && (!bFilterInUse || DokanHelper.DokanIsNameInExpression(strFilter, strNamePlain, true)))
                    //    {
                    //        result.Add(new FileInformation()
                    //        {
                    //            FileName = strNamePlain,
                    //            CreationTime = di.CreationTime,
                    //            LastAccessTime = di.LastAccessTime,
                    //            LastWriteTime = di.LastWriteTime,
                    //            Attributes = FileAttributes.Directory
                    //        });
                    //    }
                    //}

                    ////Files
                    //foreach (string strFile in LongPath.Directory.EnumerateFileSystemEntries(strPathReal, "*", false, true, SearchOption.TopDirectoryOnly))
                    //{
                    //    LongPath.FileInfo fi = new LongPath.FileInfo(strFile);
                    //    if (fi.Name.Equals(CryptoVirtualDrive.FILE_META, StringComparison.CurrentCultureIgnoreCase))
                    //        continue;

                    //    string strNamePlain = this.decrypt(fi.Name);
                    //    if (strNamePlain != null && (!bFilterInUse || DokanHelper.DokanIsNameInExpression(strFilter, strNamePlain, true)))
                    //    {
                    //        result.Add(new FileInformation()
                    //        {
                    //            FileName = strNamePlain,
                    //            Length = fi.Length,
                    //            CreationTime = fi.CreationTime,
                    //            LastAccessTime = fi.LastAccessTime,
                    //            LastWriteTime = fi.LastWriteTime
                    //        });
                    //    }
                    //}
                }


                return result;
            }
            else
                return null;
        }

        private FileInformation getFileInfo(string strPath)
        {
            if (string.IsNullOrWhiteSpace(strPath))
                return default(FileInformation);

            string strSourceId;
            this._Drive.IsPathRooted(strPath, out strSourceId);
            if (!this._Drive.IsSourceAvailable(strSourceId))
                return default(FileInformation);


            string strPathReal = this.getRealPath(strPath);

            if (LongPath.File.Exists(strPathReal))
            {
                //File
                LongPath.FileInfo fi = new LongPath.FileInfo(strPathReal);
                return new FileInformation()
                {
                    FileName = strPath,
                    Length = fi.Length,
                    CreationTime = fi.CreationTime,
                    LastAccessTime = fi.LastAccessTime,
                    LastWriteTime = fi.LastWriteTime

                };
            }

            if (LongPath.Directory.Exists(strPathReal))
            {
                //File
                LongPath.DirectoryInfo di = new LongPath.DirectoryInfo(strPathReal);
                return new FileInformation()
                {
                    FileName = strPath,
                    CreationTime = di.CreationTime,
                    LastAccessTime = di.LastAccessTime,
                    LastWriteTime = di.LastWriteTime,
                    Attributes = FileAttributes.Directory
                };
            }


            return default(FileInformation);
        }

        private CryptoStream getFileCryptoStream(string strPath, System.IO.FileMode mode, System.IO.FileShare share, DokanNet.FileAccess access)
        {
            string strSourceId;
            if (string.IsNullOrWhiteSpace(strPath) || this._Drive.IsPathRooted(strPath, out strSourceId))
                return null;

            if (!this._Drive.IsSourceAvailable(strSourceId))
                return null;

            string strRealPath = this.getRealPath(strPath);

            if (mode == FileMode.Open)
            {
                //Read mode; check if file exists
                if (!LongPath.File.Exists(strRealPath))
                    return null;
            }

            //Create directory if not exist
            string strDir = LongPath.Path.GetDirectoryName(strRealPath);
            if (!LongPath.Directory.Exists(strDir))
                LongPath.Directory.CreateDirectory(strDir);

            //Create OpenSSL AES Counter Mode crypto context
            OpenSSL.Crypto.CipherContext crypto = new OpenSSL.Crypto.CipherContext(
                this._Drive.Key.Length == 16 ? OpenSSL.Crypto.Cipher.AES_128_CTR : OpenSSL.Crypto.Cipher.AES_256_CTR);

            //Make copy of IV
            byte[] iv = new byte[this._Drive.IV.Length];
            Buffer.BlockCopy(this._Drive.IV, 0, iv, 0, 8);

            //Open destination file stream
            FileStream fsDst = LongPath.File.Open(strRealPath, mode,
                this._Drive.IsReadOnly || (access & _DATA_WRITE_ACCESS) == 0 ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite, share, 4096, FileOptions.None);

            //Return crypto stream
            return new CryptoStream(fsDst, crypto, this._Drive.Key, iv, mode != FileMode.Open);
        }




        private bool directoryCreate(string strPath)
        {
            try
            {
                string strSourceId;
                if (string.IsNullOrWhiteSpace(strPath) || this._Drive.IsPathRooted(strPath, out strSourceId))
                    return false;

                if (!this._Drive.IsSourceAvailable(strSourceId))
                    return false;

                string strRealPath = this.getRealPath(strPath);
                if (!LongPath.Directory.Exists(strRealPath))
                {
                    LongPath.Directory.CreateDirectory(strRealPath);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private bool directoryDelete(string strPath)
        {
            try
            {
                string strSourceId;
                if (string.IsNullOrWhiteSpace(strPath) || this._Drive.IsPathRooted(strPath, out strSourceId))
                    return false;

                if (!this._Drive.IsSourceAvailable(strSourceId))
                    return false;

                string strRealPath = this.getRealPath(strPath);
#if REAL_PATH_CACHE
                this.realPathCacheRemove(strRealPath);
#endif
                if (LongPath.Directory.Exists(strRealPath))
                {
                    LongPath.Directory.Delete(strRealPath);
                    return true;
                }
            }
            catch
            { }

            return false;

        }

        private bool directoryMove(string strPathOld, string strPathNew, bool bReplace)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(strPathOld) || string.IsNullOrWhiteSpace(strPathNew))
                    return false;

                string strSourceId;
                if (this._Drive.IsPathRooted(strPathOld, out strSourceId) || !this._Drive.IsSourceAvailable(strSourceId))
                    return false;

                if (this._Drive.IsPathRooted(strPathNew, out strSourceId) || !this._Drive.IsSourceAvailable(strSourceId))
                    return false;


                strPathOld = this.getRealPath(strPathOld);
                strPathNew = this.getRealPath(strPathNew);

#if REAL_PATH_CACHE
                this.realPathCacheRemove(strPathOld);
#endif

                if (LongPath.Directory.Exists(strPathOld))
                {
                    if (LongPath.Directory.Exists(strPathNew))
                    {
                        if (!bReplace)
                            return false;

                        deleteFolder(strPathNew);
                    }

                    LongPath.Directory.Move(strPathOld, strPathNew);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private bool directoryExists(string strPath)
        {
            if (string.IsNullOrWhiteSpace(strPath))
                return false;

            string strSourceId;
            if (this._Drive.IsPathRooted(strPath, out strSourceId))
                return true;

            if (!this._Drive.IsSourceAvailable(strSourceId))
                return false;

            return LongPath.Directory.Exists(this.getRealPath(strPath));
        }


        private bool fileExists(string strPath)
        {
            string strSourceId;
            if (string.IsNullOrWhiteSpace(strPath) || this._Drive.IsPathRooted(strPath, out strSourceId))
                return false;

            if (!this._Drive.IsSourceAvailable(strSourceId))
                return false;

            return LongPath.File.Exists(this.getRealPath(strPath));
        }

        private bool fileMove(string strPathOld, string strPathNew, bool bReplace)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(strPathOld) || string.IsNullOrWhiteSpace(strPathNew))
                    return false;

                string strSourceId;
                if (this._Drive.IsPathRooted(strPathOld, out strSourceId) || !this._Drive.IsSourceAvailable(strSourceId))
                    return false;

                if (this._Drive.IsPathRooted(strPathNew, out strSourceId) || !this._Drive.IsSourceAvailable(strSourceId))
                    return false;

                strPathOld = this.getRealPath(strPathOld);
                strPathNew = this.getRealPath(strPathNew);

#if REAL_PATH_CACHE
                this.realPathCacheRemove(strPathOld);
#endif

                if (LongPath.File.Exists(strPathOld))
                {
                    if (LongPath.File.Exists(strPathNew))
                    {
                        if (!bReplace)
                            return false;

                        LongPath.File.Delete(strPathNew);
                    }

                    LongPath.File.Move(strPathOld, strPathNew);

                    return true;
                }
            }
            catch { }
            return false;
        }

        private bool fileDelete(string strPath)
        {
            string strSourceId;
            if (string.IsNullOrWhiteSpace(strPath) || this._Drive.IsPathRooted(strPath, out strSourceId))
                return false;

            if (!this._Drive.IsSourceAvailable(strSourceId))
                return false;

            string strRealPath = this.getRealPath(strPath);
#if REAL_PATH_CACHE
            this.realPathCacheRemove(strRealPath);
#endif
            if (LongPath.File.Exists(strRealPath))
            {
                try
                {
                    LongPath.File.Delete(strRealPath);
                    return true;
                }
                catch { }
            }

            return false;
        }

        #endregion

        #region IDokanOperations

        /// <summary>
        /// Receipt of this request indicates that the last handle for a file object that is associated 
        /// with the target device object has been closed (but, due to outstanding I/O requests, 
        /// might not have been released). 
        /// 
        /// Cleanup is requested before <see cref="CloseFile"/> is called.
        /// </summary>
        /// <remarks>
        /// When <see cref="IDokanFileInfo.DeleteOnClose"/> is <c>true</c>, you must delete the file in Cleanup.
        /// Refer to <see cref="DeleteFile"/> and <see cref="DeleteDirectory"/> for explanation.
        /// </remarks>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <seealso cref="DeleteFile"/>
        /// <seealso cref="DeleteDirectory"/>
        /// <seealso cref="CloseFile"/> 
        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            if (info.Context is CryptoStream)
                ((CryptoStream)info.Context).Close();

            if (info.DeleteOnClose)
            {
                if (info.IsDirectory)
                    this.directoryDelete(fileName);
                else
                    this.fileDelete(fileName);
            }
        }

        /// <summary>
        /// CloseFile is called at the end of the life of the context.
        /// 
        /// Receipt of this request indicates that the last handle of the file object that is associated 
        /// with the target device object has been closed and released. All outstanding I/O requests 
        /// have been completed or canceled.
        /// 
        /// CloseFile is requested after <see cref="Cleanup"/> is called.
        /// 
        /// Remainings in <see cref="IDokanFileInfo.Context"/> has to be cleared before return.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <seealso cref="Cleanup"/> 
        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            info.Context = null;
        }

        /// <summary>
        /// CreateFile is called each time a request is made on a file system object.
        /// 
        /// In case <paramref name="mode"/> is <c><see cref="FileMode.OpenOrCreate"/></c> and
        /// <c><see cref="FileMode.Create"/></c> and CreateFile are successfully opening a already
        /// existing file, you have to return <see cref="DokanResult.AlreadyExists"/> instead of <see cref="NtStatus.Success"/>.
        /// 
        /// If the file is a directory, CreateFile is also called.
        /// In this case, CreateFile should return <see cref="NtStatus.Success"/> when that directory
        /// can be opened and <see cref="IDokanFileInfo.IsDirectory"/> must be set to <c>true</c>.
        /// On the other hand, if <see cref="IDokanFileInfo.IsDirectory"/> is set to <c>true</c>
        /// but the path target a file, you need to return <see cref="DokanResult.NotADirectory"/>
        /// 
        /// <see cref="IDokanFileInfo.Context"/> can be used to store data (like <c><see cref="FileStream"/></c>)
        /// that can be retrieved in all other request related to the context.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="access">A <see cref="FileAccess"/> with permissions for file or directory.</param>
        /// <param name="share">Type of share access to other threads, which is specified as
        /// <see cref="FileShare.None"/> or any combination of <see cref="FileShare"/>.
        /// Device and intermediate drivers usually set ShareAccess to zero,
        /// which gives the caller exclusive access to the open file.</param>
        /// <param name="mode">Specifies how the operating system should open a file. See <a href="https://msdn.microsoft.com/en-us/library/system.io.filemode(v=vs.110).aspx">FileMode Enumeration (MSDN)</a>.</param>
        /// <param name="options">Represents advanced options for creating a FileStream object. See <a href="https://msdn.microsoft.com/en-us/library/system.io.fileoptions(v=vs.110).aspx">FileOptions Enumeration (MSDN)</a>.</param>
        /// <param name="attributes">Provides attributes for files and directories. See <a href="https://msdn.microsoft.com/en-us/library/system.io.fileattributes(v=vs.110).aspx">FileAttributes Enumeration (MSDN></a>.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// \see See <a href="https://msdn.microsoft.com/en-us/library/windows/hardware/ff566424(v=vs.85).aspx">ZwCreateFile (MSDN)</a> for more information about the parameters of this callback. 
        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, System.IO.FileShare share, System.IO.FileMode mode, System.IO.FileOptions options, System.IO.FileAttributes attributes, IDokanFileInfo info)
        {
            string strSourceId;

            if (info.IsDirectory && !this._Drive.IsPathRooted(fileName, out strSourceId))
            {
                switch (mode)
                {
                    case System.IO.FileMode.CreateNew:

                        if (this._Drive.IsReadOnly)
                            return DokanResult.AccessDenied;

                        if (this.directoryExists(fileName))
                            return DokanResult.FileExists;
                        else if (this.fileExists(fileName))
                            return DokanResult.AlreadyExists;
                        else
                            return this.directoryCreate(fileName) ? DokanResult.Success : DokanResult.Error;

                    case FileMode.Open:
                        if (this.fileExists(fileName))
                            return DokanResult.NotADirectory;
                        else
                            return this.directoryExists(fileName) ? DokanResult.Success : DokanResult.PathNotFound;

                    default:
                        return DokanResult.AccessDenied;
                }

            }

            if (this._Drive.IsPathRooted(fileName, out strSourceId))
            {
                info.IsDirectory = true;
                info.Context = new object();
                return DokanResult.Success;
            }
            else
            {
                switch (mode)
                {
                    case System.IO.FileMode.Open:
                        info.Context = this.getFileCryptoStream(fileName, mode, share, access);

                        if (info.Context != null)
                            return DokanResult.Success;
                        else if (this.directoryExists(fileName))
                        {
                            info.IsDirectory = true;
                            info.Context = new object();
                            return DokanResult.Success;
                        }
                        else
                            return DokanResult.FileNotFound;

                    case System.IO.FileMode.Create:
                    case System.IO.FileMode.CreateNew:
                    case System.IO.FileMode.OpenOrCreate:
                    case System.IO.FileMode.Truncate:

                        if (this._Drive.IsReadOnly)
                            return DokanResult.AccessDenied;

                        bool bDirExists = this.directoryExists(fileName);
                        bool bFileExists = this.fileExists(fileName);

                        if (mode == FileMode.CreateNew && (bFileExists || bDirExists))
                            return DokanResult.FileExists;

                        if (mode == FileMode.Truncate && !(bFileExists || bDirExists))
                            return DokanResult.FileNotFound;

                        info.Context = this.getFileCryptoStream(fileName, mode, share, access);

                        if (info.Context != null)
                        {
                            if ((bFileExists || bDirExists)
                                && (mode == FileMode.OpenOrCreate || mode == FileMode.Create))
                                return DokanResult.AlreadyExists;

                            return DokanResult.Success;
                        }

                        break;

                    case System.IO.FileMode.Append:
                        break;
                }

            }


            return DokanResult.Error;
        }

        /// <summary>
        /// Check if it is possible to delete a directory.
        /// </summary>
        /// <remarks>
        /// You should NOT delete the file in <see cref="DeleteDirectory"/>, but instead
        /// you must only check whether you can delete the file or not,
        /// and return <see cref="NtStatus.Success"/> (when you can delete it) or appropriate error
        /// codes such as <see cref="NtStatus.AccessDenied"/>, <see cref="NtStatus.ObjectPathNotFound"/>,
        /// <see cref="NtStatus.ObjectNameNotFound"/>.
        ///
        /// DeleteFile will also be called with <see cref="IDokanFileInfo.DeleteOnClose"/> set to <c>false</c>
        /// to notify the driver when the file is no longer requested to be deleted.
        ///
        /// When you return <see cref="NtStatus.Success"/>, you get a <see cref="Cleanup"/> call afterwards with
        /// <see cref="IDokanFileInfo.DeleteOnClose"/> set to <c>true</c> and only then you have to actually
        /// delete the file being closed.
        /// </remarks>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns>Return <see cref="DokanResult.Success"/> if file can be delete or <see cref="NtStatus"/> appropriate.</returns>
        /// <seealso cref="DeleteFile"/>
        /// <seealso cref="Cleanup"/> 
        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
                return NtStatus.AccessDenied;

            return this.directoryExists(fileName) ? NtStatus.Success : NtStatus.Error;
        }

        /// <summary>
        /// Check if it is possible to delete a file.
        /// </summary>
        /// <remarks>
        /// You should NOT delete the file in DeleteFile, but instead
        /// you must only check whether you can delete the file or not,
        /// and return <see cref="NtStatus.Success"/> (when you can delete it) or appropriate error
        /// codes such as <see cref="NtStatus.AccessDenied"/>, <see cref="NtStatus.ObjectNameNotFound"/>.
        ///
        /// DeleteFile will also be called with <see cref="IDokanFileInfo.DeleteOnClose"/> set to <c>false</c>
        /// to notify the driver when the file is no longer requested to be deleted.
        /// 
        /// When you return <see cref="NtStatus.Success"/>, you get a <see cref="Cleanup"/> call afterwards with
        /// <see cref="IDokanFileInfo.DeleteOnClose"/> set to <c>true</c> and only then you have to actually
        /// delete the file being closed.
        /// </remarks>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns>Return <see cref="DokanResult.Success"/> if file can be delete or <see cref="NtStatus"/> appropriate.</returns>
        /// <seealso cref="DeleteDirectory"/>
        /// <seealso cref="Cleanup"/> 
        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
                return NtStatus.AccessDenied;

            return this.fileExists(fileName) ? NtStatus.Success : NtStatus.Error;
        }

        /// <summary>
        /// List all files in the path requested
        /// 
        /// <see cref="FindFilesWithPattern"/> is checking first. If it is not implemented or
        /// returns <see cref="NtStatus.NotImplemented"/>, then FindFiles is called.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="files">A list of <see cref="FileInformation"/> to return.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="FindFilesWithPattern"/> 
        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = this.getDirectory(fileName, null);
            if (this._Drive.IsReadOnly)
                ((List<FileInformation>)files).ForEach(fi => fi.Attributes |= FileAttributes.ReadOnly);

            return DokanResult.Success;
        }

        /// <summary>
        /// Same as <see cref="FindFiles"/> but with a search pattern to filter the result.
        /// </summary>
        /// <param name="fileName">Path requested by the Kernel on the FileSystem.</param>
        /// <param name="searchPattern">Search pattern</param>
        /// <param name="files">A list of <see cref="FileInformation"/> to return.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="FindFiles"/> 
        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = this.getDirectory(fileName, searchPattern);
            if (this._Drive.IsReadOnly)
                ((List<FileInformation>)files).ForEach(fi => fi.Attributes |= FileAttributes.ReadOnly);

            return DokanResult.Success;
        }

        /// <summary>
        /// Retrieve all NTFS Streams informations on the file.
        /// This is only called if <see cref="DokanOptions.AltStream"/> is enabled.
        /// </summary>
        /// <remarks>For files, the first item in <paramref name="streams"/> is information about the 
        /// default data stream <c>"::$DATA"</c>.</remarks>
        /// \since Supported since version 0.8.0. You must specify the version in <see cref="Dokan.Mount(IDokanOperations, string, DokanOptions,int, int, TimeSpan, string, int,int, Logging.ILogger)"/>.
        /// 
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="streams">List of <see cref="FileInformation"/> for each streams present on the file.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns>Return <see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// \see <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/aa364424(v=vs.85).aspx">FindFirstStreamW function (MSDN)</a>
        /// \see <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/aa365993(v=vs.85).aspx">About KTM (MSDN)</a> 
        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        /// <summary>
        /// Clears buffers for this context and causes any buffered data to be written to the file.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns> 
        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
                return NtStatus.AccessDenied;

            if (info.Context is CryptoStream)
            {
                ((CryptoStream)info.Context).Flush();
                return NtStatus.Success;
            }

            return DokanResult.Error;
        }

        /// <summary>
        /// Retrieves information about the amount of space that is available on a disk volume, which is the total amount of space, 
        /// the total amount of free space, and the total amount of free space available to the user that is associated with the calling thread.
        /// </summary>
        /// <remarks>
        /// Neither GetDiskFreeSpace nor <see cref="GetVolumeInformation"/> save the <see cref="IDokanFileInfo.Context"/>.
        /// Before these methods are called, <see cref="CreateFile"/> may not be called. (ditto <see cref="CloseFile"/> and <see cref="Cleanup"/>).
        /// </remarks>
        /// <param name="freeBytesAvailable">Amount of available space.</param>
        /// <param name="totalNumberOfBytes">Total size of storage space.</param>
        /// <param name="totalNumberOfFreeBytes">Amount of free space.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// \see <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/aa364937(v=vs.85).aspx"> GetDiskFreeSpaceEx function (MSDN)</a>
        /// <seealso cref="GetVolumeInformation"/> 
        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            totalNumberOfFreeBytes = this._Drive.GetFreeSpace(out totalNumberOfBytes);
            if (totalNumberOfFreeBytes >= 0)
            {
                freeBytesAvailable = totalNumberOfFreeBytes;
                return DokanResult.Success;
            }
            else
            {
                freeBytesAvailable = -1;
                return DokanResult.Error;
            }
        }

        /// <summary>
        /// Get specific informations on a file.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="fileInfo"><see cref="FileInformation"/> struct to fill</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns> 
        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            string strSourceId;

            if (this._Drive.IsPathRooted(fileName, out strSourceId))
            {
                fileInfo = new FileInformation { FileName = fileName };
                fileInfo.Attributes = System.IO.FileAttributes.Directory;
                fileInfo.LastAccessTime = DateTime.Now;
                fileInfo.LastWriteTime = null;
                fileInfo.CreationTime = null;

                if (this._Drive.IsPool && fileName != "\\" && !this._Drive.IsSourceValid(strSourceId))
                    return DokanResult.Error;
                else
                    return DokanResult.Success;
            }
            else
            {
                fileInfo = this.getFileInfo(fileName);
                if (!string.IsNullOrWhiteSpace(fileInfo.FileName))
                    return DokanResult.Success;
                else
                {
                    fileInfo.FileName = fileName;
                    return DokanResult.Error;
                }
            }
        }

        /// <summary>
        /// Get specified information about the security of a file or directory. 
        /// </summary>
        /// <remarks>
        /// If <see cref="NtStatus.NotImplemented"/> is returned, dokan library will handle the request by
        /// building a sddl of the current process user with authenticate user rights for context menu.
        /// </remarks>
        /// \since Supported since version 0.6.0. You must specify the version in <see cref="Dokan.Mount(IDokanOperations, string, DokanOptions,int, int, TimeSpan, string, int,int, Logging.ILogger)"/>.
        /// 
        /// <param name="fileName">File or directory name.</param>
        /// <param name="security">A <see cref="FileSystemSecurity"/> with security information to return.</param>
        /// <param name="sections">A <see cref="AccessControlSections"/> with access sections to return.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="SetFileSecurity"/>
        /// \see <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/aa446639(v=vs.85).aspx">GetFileSecurity function (MSDN)</a> 
        public NtStatus GetFileSecurity(string fileName, out System.Security.AccessControl.FileSystemSecurity security, System.Security.AccessControl.AccessControlSections sections, IDokanFileInfo info)
        {
            security = null;
            return DokanResult.Error;
        }

        /// <summary>
        /// Retrieves information about the file system and volume associated with the specified root directory.
        /// </summary>
        /// <remarks>
        /// Neither GetVolumeInformation nor <see cref="GetDiskFreeSpace"/> save the <see cref="IDokanFileInfo.Context"/>.
        /// Before these methods are called, <see cref="CreateFile"/> may not be called. (ditto <see cref="CloseFile"/> and <see cref="Cleanup"/>).
        /// 
        /// <see cref="FileSystemFeatures.ReadOnlyVolume"/> is automatically added to the <paramref name="features"/> if <see cref="DokanOptions.WriteProtection"/> was
        /// specified when the volume was mounted.
        /// 
        /// If <see cref="NtStatus.NotImplemented"/> is returned, the %Dokan kernel driver use following settings by default:
        /// | Parameter                    | Default value                                                                                    |
        /// |------------------------------|--------------------------------------------------------------------------------------------------|
        /// | \a rawVolumeNameBuffer       | <c>"DOKAN"</c>                                                                                   |
        /// | \a rawVolumeSerialNumber     | <c>0x19831116</c>                                                                                |
        /// | \a rawMaximumComponentLength | <c>256</c>                                                                                       |
        /// | \a rawFileSystemFlags        | <c>CaseSensitiveSearch \|\| CasePreservedNames \|\| SupportsRemoteStorage \|\| UnicodeOnDisk</c> |
        /// | \a rawFileSystemNameBuffer   | <c>"NTFS"</c>                                                                                    |
        /// </remarks>
        /// <param name="volumeLabel">Volume name</param>
        /// <param name="features"><see cref="FileSystemFeatures"/> with features enabled on the volume.</param>
        /// <param name="fileSystemName">The name of the specified volume.</param>
        /// <param name="maximumComponentLength">File name component that the specified file system supports.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// \see <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/aa364993(v=vs.85).aspx"> GetVolumeInformation function (MSDN)</a> 
        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = this._Drive.VolumeLabel;
            features = this._Drive.IsReadOnly ? FileSystemFeatures.ReadOnlyVolume : FileSystemFeatures.None;
            fileSystemName = string.Empty;
            maximumComponentLength = 256;
            return DokanResult.Success;
        }

        /// <summary>
        /// Lock file at a specific offset and data length.
        /// This is only used if <see cref="DokanOptions.UserModeLock"/> is enabled.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="offset">Offset from where the lock has to be proceed.</param>
        /// <param name="length">Data length to lock.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="UnlockFile"/> 
        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            try
            {
                ((CryptoStream)(info.Context)).Lock(offset, length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.AccessDenied;
            }

        }

        /// <summary>
        /// Is called when %Dokan succeed to mount the volume.
        /// 
        /// If <see cref="DokanOptions.MountManager"/> is enabled and the drive letter requested is busy,
        /// the <paramref name="mountPoint"/> can contain a different drive letter that the mount manager assigned us.
        /// </summary>
        /// <param name="mountPoint">The mount point assign to the instance.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <see cref="Unmounted"/> 
        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        /// <summary>
        /// Move a file or directory to a new location.
        /// </summary>
        /// <param name="oldName">Path to the file to move.</param>
        /// <param name="newName">Path to the new location for the file.</param>
        /// <param name="replace">If the file should be replaced if it already exist a file with path <paramref name="newName"/>.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns> 
        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
                return NtStatus.AccessDenied;

            if (info.Context is CryptoStream)
                ((CryptoStream)info.Context).Close();

            if (info.IsDirectory)
                return this.directoryMove(oldName, newName, replace) ? NtStatus.Success : NtStatus.Error;
            else
                return this.fileMove(oldName, newName, replace) ? NtStatus.Success : NtStatus.Error;
        }

        /// <summary>
        /// ReadFile callback on the file previously opened in <see cref="CreateFile"/>.
        /// It can be called by different thread at the same time,
        /// therefor the read has to be thread safe.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="buffer">Read buffer that has to be fill with the read result.
        /// The buffer size depend of the read size requested by the kernel.</param>
        /// <param name="bytesRead">Total number of bytes that has been read.</param>
        /// <param name="offset">Offset from where the read has to be proceed.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="WriteFile"/> 
        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            CryptoStream stream = info.Context as CryptoStream;

            int iToRead = Math.Min(buffer.Length, stream.Length > int.MaxValue ? int.MaxValue : (int)stream.Length);

            lock (stream)
            {
                stream.Position = offset < 0 ? 0 : offset;
                bytesRead = stream.Read(buffer, 0, iToRead);
            }
            return DokanResult.Success;
        }

        /// <summary>
        /// SetAllocationSize is used to truncate or extend a file.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="length">File length to set</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns> 
        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
                return DokanResult.AccessDenied;

            try
            {
                ((CryptoStream)(info.Context)).SetLength(length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.DiskFull;
            }
        }

        /// <summary>
        /// SetEndOfFile is used to truncate or extend a file (physical file size).
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="length">File length to set</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns> 
        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
                return DokanResult.AccessDenied;

            try
            {
                ((CryptoStream)(info.Context)).SetLength(length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.DiskFull;
            }
        }

        /// <summary>
        /// Set file attributes on a specific file.
        /// </summary>
        /// <remarks>SetFileAttributes and <see cref="SetFileTime"/> are called only if both of them are implemented.</remarks>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="attributes"><see cref="FileAttributes"/> to set on file</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns> 
        public NtStatus SetFileAttributes(string fileName, System.IO.FileAttributes attributes, IDokanFileInfo info)
        {
            return DokanResult.Error;
        }

        /// <summary>
        /// Sets the security of a file or directory object.
        /// </summary>
        /// \since Supported since version 0.6.0. You must specify the version in <see cref="Dokan.Mount(IDokanOperations, string, DokanOptions,int, int, TimeSpan, string, int,int, Logging.ILogger)"/>.
        /// 
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="security">A <see cref="FileSystemSecurity"/> with security information to set.</param>
        /// <param name="sections">A <see cref="AccessControlSections"/> with access sections on which.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="GetFileSecurity"/>
        /// \see <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/aa379577(v=vs.85).aspx">SetFileSecurity function (MSDN)</a> 
        public NtStatus SetFileSecurity(string fileName, System.Security.AccessControl.FileSystemSecurity security, System.Security.AccessControl.AccessControlSections sections, IDokanFileInfo info)
        {
            return DokanResult.Error;
        }

        /// <summary>
        /// Set file times on a specific file.
        /// If <see cref="DateTime"/> is <c>null</c>, this should not be updated.
        /// </summary>
        /// <remarks><see cref="SetFileAttributes"/> and SetFileTime are called only if both of them are implemented.</remarks>
        /// <param name="fileName">File or directory name.</param>
        /// <param name="creationTime"><see cref="DateTime"/> when the file was created.</param>
        /// <param name="lastAccessTime"><see cref="DateTime"/> when the file was last accessed.</param>
        /// <param name="lastWriteTime"><see cref="DateTime"/> when the file was last written to.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns> 
        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
                return DokanResult.AccessDenied;

            try
            {
                if (info.Context is CryptoStream)
                {
                    ((CryptoStream)info.Context).SetFileTime(creationTime, lastAccessTime, lastWriteTime);
                    return NtStatus.Success;
                }

                string strRealPath = this.getRealPath(fileName);

                if (creationTime.HasValue)
                    LongPath.File.SetCreationTime(strRealPath, creationTime.Value);

                if (lastAccessTime.HasValue)
                    LongPath.File.SetLastAccessTime(strRealPath, lastAccessTime.Value);

                if (lastWriteTime.HasValue)
                    LongPath.File.SetLastWriteTime(strRealPath, lastWriteTime.Value);

                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
            catch (FileNotFoundException)
            {
                return DokanResult.FileNotFound;
            }
            catch
            {
                return DokanResult.Error;
            }
        }

        /// <summary>
        /// Unlock file at a specific offset and data length.
        /// This is only used if <see cref="DokanOptions.UserModeLock"/> is enabled.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="offset">Offset from where the unlock has to be proceed.</param>
        /// <param name="length">Data length to lock.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="LockFile"/> 
        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            try
            {
                ((CryptoStream)(info.Context)).Unlock(offset, length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.AccessDenied;
            }
        }

        /// <summary>
        /// Is called when %Dokan is unmounting the volume.
        /// </summary>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="Mounted"/> 
        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        /// <summary>
        /// WriteFile callback on the file previously opened in <see cref="CreateFile"/>
        /// It can be called by different thread at the same time,
        /// therefor the write/context has to be thread safe.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="buffer">Data that has to be written.</param>
        /// <param name="bytesWritten">Total number of bytes that has been write.</param>
        /// <param name="offset">Offset from where the write has to be proceed.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="ReadFile"/> 
        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
            {
                bytesWritten = 0;
                return NtStatus.AccessDenied;
            }

            CryptoStream stream = info.Context as CryptoStream;

            int iLength = buffer.Length;

            lock (stream)
            {
                stream.Position = offset < 0 ? stream.Length : offset;
                stream.Write(buffer, 0, iLength);
            }

            bytesWritten = iLength;
            return DokanResult.Success;
        }

        #endregion

        /// <summary>
        /// This is a sub-interface of <see cref="IDokanOperations"/> that can optionally be implemented
        /// to get access to the raw, unmanaged buffers for ReadFile() and WriteFile() for performance optimization.
        /// Marshalling the unmanaged buffers to and from byte[] arrays for every call of these APIs incurs an extra copy
        /// that can be avoided by reading from or writing directly to the unmanaged buffers.
        /// 
        /// Implementation of this interface is optional. If it is implemented, the overloads of
        /// Read/WriteFile(IntPtr, length) will be called instead of Read/WriteFile(byte[]). The caller can fill or read
        /// from the unmanaged API with Marshal.Copy, Buffer.MemoryCopy or similar.
        /// </summary>
        #region IDokanOperationsUnsafe

        /// <summary>
        /// ReadFile callback on the file previously opened in <see cref="CreateFile"/>.
        /// It can be called by different thread at the same time,
        /// therefore the read has to be thread safe.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="buffer">Read buffer that has to be fill with the read result.</param>
        /// <param name="bufferLength">The size of 'buffer' in bytes.
        /// The buffer size depends of the read size requested by the kernel.</param>
        /// <param name="bytesRead">Total number of bytes that has been read.</param>
        /// <param name="offset">Offset from where the read has to be proceed.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="WriteFile"/>
        public NtStatus ReadFile(string fileName, IntPtr buffer, uint bufferLength, out int bytesRead, long offset, IDokanFileInfo info)
        {
            CryptoStream stream = info.Context as CryptoStream;

            int iToRead = Math.Min((int)bufferLength, stream.Length > int.MaxValue ? int.MaxValue : (int)stream.Length);

            lock (stream)
            {
                stream.Position = offset < 0 ? 0 : offset;
                bytesRead = stream.ReadNative(buffer, iToRead);
            }

            return DokanResult.Success;
        }

        /// <summary>
        /// WriteFile callback on the file previously opened in <see cref="CreateFile"/>
        /// It can be called by different thread at the same time,
        /// therefore the write/context has to be thread safe.
        /// </summary>
        /// <param name="fileName">File path requested by the Kernel on the FileSystem.</param>
        /// <param name="buffer">Data that has to be written.</param>
        /// <param name="bufferLength">The size of 'buffer' in bytes.</param>
        /// <param name="bytesWritten">Total number of bytes that has been write.</param>
        /// <param name="offset">Offset from where the write has to be proceed.</param>
        /// <param name="info">An <see cref="IDokanFileInfo"/> with information about the file or directory.</param>
        /// <returns><see cref="NtStatus"/> or <see cref="DokanResult"/> appropriate to the request result.</returns>
        /// <seealso cref="ReadFile"/>
        public NtStatus WriteFile(string fileName, IntPtr buffer, uint bufferLength, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            if (this._Drive.IsReadOnly)
            {
                bytesWritten = 0;
                return NtStatus.AccessDenied;
            }

            CryptoStream stream = info.Context as CryptoStream;

            lock (stream)
            {
                stream.Position = offset < 0 ? stream.Length : offset;
                bytesWritten = stream.WriteNative(buffer, (int)bufferLength);
            }

            return DokanResult.Success;
        }

        #endregion

        public override int GetHashCode()
        {
            return 0x25F3D81C; //serial number
        }
    }
}
