using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using NLog;

namespace MediaPortal.Pbk.Utils.Encryption
{
    public class Crypto
    {
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();
        private static MD5 _CryptoMD5 = MD5.Create();

        #region ctor
        static Crypto()
        {
            Logging.Log.Init();
        }
        #endregion

        /// <summary>
        /// Encrypts/decrypts source file to destination file
        /// </summary>
        /// <param name="crypto">Encryptor or decryptor</param>
        /// <param name="strFileSource">Source file full path</param>
        /// <param name="strFileDestination">Destination file full path</param>
        /// <returns>True if tranform is successfull</returns>
        public static bool TransformFile(ICryptoTransform crypto, string strFileSource, string strFileDestination)
        {
            byte[] bufferIn = new byte[1024 * 32];
            byte[] bufferOut = new byte[1024 * 32];

            return TransformFile(crypto, strFileSource, strFileDestination, bufferIn, bufferOut);
        }

        /// <summary>
        /// Encrypts/decrypts source file to destination file
        /// </summary>
        /// <param name="crypto">Encryptor or decryptor</param>
        /// <param name="strFileSource">Source file full path</param>
        /// <param name="strFileDestination">Destination file full path</param>
        /// <param name="bufferIn">Input buffer for encrypting/decrypting. Input and output buffer must be the same size.</param>
        /// <param name="bufferOut">Output buffer for encrypting/decrypting. Input and output buffer must be the same size.</param>
        /// <returns>True if tranform is successfull</returns>
        public static bool TransformFile(ICryptoTransform crypto, string strFileSource, string strFileDestination, byte[] bufferIn, byte[] bufferOut)
        {
            try
            {
                if (!File.Exists(strFileSource))
                    return false;

                int iToRead;
                int iRd, iWr;
                using (FileStream fsSrc = new FileStream(strFileSource, FileMode.Open))
                {
                    using (FileStream fsDst = new FileStream(strFileDestination, FileMode.Create))
                    {
                        while (fsSrc.Position < fsSrc.Length)
                        {
                            //Max length to read
                            iToRead = (int)Math.Min((long)bufferIn.Length, fsSrc.Length - fsSrc.Position);

                            //Alligning
                            if (iToRead > crypto.InputBlockSize)
                                iToRead &= ~(crypto.InputBlockSize - 1);

                            //Read source file
                            iRd = 0;
                            while (iRd < iToRead)
                                iRd += fsSrc.Read(bufferIn, iRd, iToRead - iRd);

                            if (fsSrc.Position == fsSrc.Length)
                            {
                                //Last data block
                                byte[] last = crypto.TransformFinalBlock(bufferIn, 0, iRd);
                                fsDst.Write(last, 0, last.Length);
                                return true;
                            }
                            else
                            {
                                iWr = crypto.TransformBlock(bufferIn, 0, iRd, bufferOut, 0);
                                fsDst.Write(bufferOut, 0, iWr);
                            }
                        }
                    }
                }

            }
            catch (Exception ex) { _Logger.Error("TransformFile] Error: {0}", ex.Message); }

            ((RijndaelManagedTransform)crypto).Reset();

            _Logger.Error("TransformFile] Failed tranform file: '{0}' to '{1}'", strFileSource, strFileDestination);

            return false;
        }


        /// <summary>
        /// Encrypts plain path
        /// </summary>
        /// <param name="crypto">Encryptor</param>
        /// <param name="strPathPlain">Plain path</param>
        /// <returns>Encrypted path</returns>
        public static string PathEncrypt(ICryptoTransform crypto, string strPathPlain)
        {
            try
            {
                byte[] dataPath = Encoding.UTF8.GetBytes(strPathPlain);

                byte[] hash = _CryptoMD5.ComputeHash(dataPath);

                //Place hash(last 8 bytes) at the beginning
                byte[] dataPlain = new byte[8 + dataPath.Length];
                Buffer.BlockCopy(hash, hash.Length - 8, dataPlain, 0, 8);
                Buffer.BlockCopy(dataPath, 0, dataPlain, 8, dataPath.Length);

                byte[] dataEnc = crypto.TransformFinalBlock(dataPlain, 0, dataPlain.Length);
                return System.Convert.ToBase64String(dataEnc).Replace('/', '_');
            }
            catch { ((RijndaelManagedTransform)crypto).Reset(); }
            return null;
        }

        /// <summary>
        /// Decrypts encrypted path
        /// </summary>
        /// <param name="crypto">Decryptor</param>
        /// <param name="strPathEncrypted">Encrypted path</param>
        /// <returns>Plain path</returns>
        public static string PathDecrypt(ICryptoTransform crypto, string strPathEncrypted)
        {
            try
            {
                byte[] dataEnc = System.Convert.FromBase64String(strPathEncrypted.Replace('_', '/'));
                byte[] dataPlain = crypto.TransformFinalBlock(dataEnc, 0, dataEnc.Length);
                return Encoding.UTF8.GetString(dataPlain, 8, dataPlain.Length - 8); //skip hash
            }
            catch { ((RijndaelManagedTransform)crypto).Reset(); }
            return null;
        }


    }
}
