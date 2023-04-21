using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using NLog;

namespace MediaPortal.Pbk.Cornerstone.Extensions.IO
{

    public static class FileInfoExtensions
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Gets a value indicating whether this file is accessible (for reading)
        /// </summary>
        /// <param name="self"></param>
        /// <returns>True if accessible</returns>
        public static bool IsLocked(this FileInfo self)
        {
            FileStream stream = null;
            try
            {
                stream = self.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            return false;
        }

        /// <summary>
        /// Calculates a unique hash for the contents of the file.
        /// Use this method to compute hashes of large files.
        /// </summary>
        /// <param name="self"></param>
        /// <returns>a unique hash or null when error</returns>
        public static string ComputeSmartHash(this FileInfo self)
        {
            string strHexHash = null;
            byte[] bytes = null;
            try
            {
                using (Stream streamInput = self.OpenRead())
                {
                    ulong lHash;
                    long lStreamsize;
                    lStreamsize = streamInput.Length;
                    lHash = (ulong)lStreamsize;

                    long l = 0;
                    byte[] buffer = new byte[sizeof(long)];
                    streamInput.Position = 0;
                    while (l < 65536 / sizeof(long) && (streamInput.Read(buffer, 0, sizeof(long)) > 0))
                    {
                        l++;
                        unchecked { lHash += BitConverter.ToUInt64(buffer, 0); }
                    }

                    streamInput.Position = Math.Max(0, lStreamsize - 65536);
                    l = 0;
                    while (l < 65536 / sizeof(long) && (streamInput.Read(buffer, 0, sizeof(long)) > 0))
                    {
                        l++;
                        unchecked { lHash += BitConverter.ToUInt64(buffer, 0); }
                    }
                    bytes = BitConverter.GetBytes(lHash);
                    Array.Reverse(bytes);

                    // convert to hexadecimal string
                    strHexHash = bytes.ToHexString();
                }
            }
            catch (Exception e)
            {
                logger.DebugException("[ComputeSmartHash] Error computing smart hash: ", e);
            }
            return strHexHash;
        }

        /// <summary>
        /// Generates a SHA1-Hash from a given filepath
        /// </summary>
        /// <param name="filePath">path to the file</param>
        /// <returns>hash as an hexadecimal string </returns>
        public static string ComputeSHA1Hash(this FileInfo self)
        {
            string strHashHex = null;
            if (self.Exists)
            {
                Stream streamFile = null;
                try
                {
                    streamFile = self.OpenRead();
                    HashAlgorithm hashObj = new SHA1Managed();
                    byte[] hash = hashObj.ComputeHash(streamFile);
                    strHashHex = hash.ToHexString();
                    logger.Debug("[ComputeSHA1Hash] SHA1: Success, File='{0}', Hash='{1}'", self.FullName, strHashHex);
                }
                catch (Exception e)
                {
                    if (e.GetType() == typeof(ThreadAbortException))
                        throw e;

                    logger.DebugException("[ComputeSHA1Hash] SHA1: Failed, File='" + self.FullName + "' ", e);
                }
                finally
                {
                    if (streamFile != null)
                        streamFile.Close();
                }
            }
            else
            {
                // File does not exist
                logger.Debug("[ComputeSHA1Hash] SHA1: Failed, File='{0}', Reason='File is not available'", self.FullName);
            }

            // Return
            return strHashHex;
        }

    }
}
