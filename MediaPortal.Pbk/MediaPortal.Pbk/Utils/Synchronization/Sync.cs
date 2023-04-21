using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using MediaPortal.Pbk.Utils.Encryption;
using NLog;

namespace MediaPortal.Pbk.Utils.Synchronization
{
    public class Sync
    {
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        #region ctor
        static Sync()
        {
            Logging.Log.Init();
        }
        #endregion

        /// <summary>
        /// Creates directory tree of encrypted files from given path
        /// </summary>
        /// <param name="crypto">Decryptor</param>
        /// <param name="strPath">Path to directory of encrypted files</param>
        /// <returns>Directory tree of encrypted files</returns>
        public static CryptoDirectory GetDirectories(ICryptoTransform crypto, string strPath)
        {
            CryptoDirectory result = new CryptoDirectory(null, null);

            //Get all files from given path
            string[] files = System.IO.Directory.GetFiles(strPath);

            foreach (string strFile in files)
            {
                if (strFile.Length >= 24 && strFile.IndexOf('.') < 0)
                {
                    //Decrypt the path
                    string strPathPlain = Crypto.PathDecrypt(crypto, strFile.Substring(strPath.Length + 1));

                    if (strPathPlain == null || !strPathPlain.StartsWith("\\") || strPathPlain.EndsWith("\\"))
                        continue; //unknown path

                    //Split path to the individual parts
                    string[] pathPlain = strPathPlain.Split('\\');

                    //Parent
                    CryptoDirectory cryptoDir = result;

                    for (int i = 1; i < pathPlain.Length - 1; i++)
                    {
                        CryptoDirectory dir = (CryptoDirectory)cryptoDir.Items.Find(d =>
                            d is CryptoDirectory && d.Name.Equals(pathPlain[i], StringComparison.CurrentCultureIgnoreCase));

                        if (dir == null)
                        {
                            //Create new directory
                            dir = new CryptoDirectory(pathPlain[i], cryptoDir);
                            cryptoDir.Items.Add(dir);
                        }

                        //Set new parent
                        cryptoDir = dir;
                    }

                    //Add file to the directory
                    cryptoDir.Items.Add(new CryptoFile(pathPlain[pathPlain.Length - 1], cryptoDir, strFile, File.GetLastWriteTime(strFile)));
                }
            }

            return result;
        }


        /// <summary>
        /// Updates destination path by adding non existing files/directories
        /// </summary>
        /// <param name="strSource">Source path</param>
        /// <param name="strDestination">Destination path</param>
        public static void AddMissingFiles(string strSource, string strDestination)
        {
            try
            {
                _Logger.Debug("[AddMissingFiles] Proccess: source '{0}', destination '{1}'", strSource, strDestination);

                if (!Directory.Exists(strSource))
                {
                    _Logger.Debug("[AddMissingFiles] Source path '{0}' doesn't exist.", strSource);
                    return;
                }

                if (!Directory.Exists(strDestination))
                {
                    _Logger.Debug("[AddMissingFiles] Destination path '{0}' doesn't exist.", strDestination);
                    return;
                }

                string[] dirs = Directory.GetDirectories(strSource);

                foreach (string strDir in dirs)
                {
                    string strSubDir = strDir.Substring(strSource.Length);

                    string strDirDest = strDestination + strSubDir;

                    if (!Directory.Exists(strDirDest))
                    {
                        try
                        {
                            Directory.CreateDirectory(strDirDest);

                            _Logger.Debug("[AddMissingFiles] New directory created: '{0}'", strDirDest);

                        }
                        catch (Exception ex) { _Logger.Error("AddMissingFiles] Failed to create new directory: '{0}' Error: {1}", strDirDest, ex.Message); }
                    }

                    //Recursion
                    AddMissingFiles(strDir, strDirDest);

                    //Thread.Sleep(10);
                }

                DirectoryInfo di = new DirectoryInfo(strSource);
                FileInfo[] files = di.GetFiles();
                foreach (FileInfo fSource in files)
                {
                    string strTargetFile = strDestination + "\\" + fSource.Name;
                    if (!File.Exists(strTargetFile))
                    {
                        try
                        {
                            File.Copy(fSource.FullName, strTargetFile);
                            _Logger.Debug("[AddMissingFiles] New file created: '{0}'", strTargetFile);
                        }
                        catch (Exception ex) { _Logger.Error("AddMissingFiles] Failed to create new file: '{0}' Error: {1}", strTargetFile, ex.Message); }
                    }
                    else
                    {
                        FileInfo fDest = new FileInfo(strTargetFile);

                        try
                        {
                            if (fDest.LastWriteTime != fSource.LastWriteTime || fDest.Length != fSource.Length)
                            {
                                File.Copy(fSource.FullName, strTargetFile, true);
                                _Logger.Debug("[AddMissingFiles] File updated: '{0}'", strTargetFile);
                            }
                        }
                        catch (Exception ex) { _Logger.Error("AddMissingFiles] Failed to update file: '{0}' Error: {1}", strTargetFile, ex.Message); }
                    }

                    //Thread.Sleep(10);
                }
            }
            catch { }
        }

        /// <summary>
        /// Updates destination path by adding/encrypting non existing files/directories 
        /// </summary>
        /// <param name="crypto">Encryptor</param>
        /// <param name="strSourceRoot">Source directory root path. This part is not beeing encrypted.</param>
        /// <param name="strSourcePath">Source directory path. This part is beeing encrypted together with the filename.</param>
        /// <param name="strDestination">Destination path where encrypted files will be stored.</param>
        public static void AddMissingFiles(ICryptoTransform crypto, string strSourceRoot, string strSourcePath, string strDestination)
        {
            byte[] bufferIn = new byte[1024 * 32];
            byte[] bufferOut = new byte[1024 * 32];

            AddMissingFiles(crypto, strSourceRoot, strSourcePath, strDestination, bufferIn, bufferOut);
        }

        /// <summary>
        /// Updates destination path by adding/encrypting non existing files/directories 
        /// </summary>
        /// <param name="crypto">Encryptor</param>
        /// <param name="strSourceRoot">Source directory root path. This part is not beeing encrypted.</param>
        /// <param name="strSourcePath">Source directory path. This part is beeing encrypted together with the filename.</param>
        /// <param name="strDestination">Destination path where encrypted files will be stored.</param>
        /// <param name="bufferIn">Input buffer for encrypting/decrypting. Input and output buffer must be the same size.</param>
        /// <param name="bufferOut">Output buffer for encrypting/decrypting. Input and output buffer must be the same size.</param>
        public static void AddMissingFiles(ICryptoTransform crypto, string strSourceRoot, string strSourcePath, string strDestination, byte[] bufferIn, byte[] bufferOut)
        {
            try
            {
                _Logger.Debug("AddMissingFiles] Proccess: source '{0}{1}', destination '{2}'", strSourceRoot, strSourcePath, strDestination);

                string strSourcePathFull = strSourceRoot + strSourcePath;

                if (!Directory.Exists(strSourcePathFull))
                {
                    _Logger.Debug("AddMissingFiles] Source path '{0}' doesn't exist.", strSourcePathFull);
                    return;
                }

                if (!Directory.Exists(strDestination))
                {
                    _Logger.Debug("AddMissingFiles] Destination path '{0}' doesn't exist.", strDestination);
                    return;
                }

                string[] dirs = Directory.GetDirectories(strSourcePathFull);

                foreach (string strDir in dirs)
                {
                    string strSubDir = strSourcePath + strDir.Substring(strSourcePathFull.Length);

                    //Recursion
                    AddMissingFiles(crypto, strSourceRoot, strSubDir, strDestination, bufferIn, bufferOut);

                    //Thread.Sleep(10);
                }

                DirectoryInfo di = new DirectoryInfo(strSourcePathFull);
                FileInfo[] files = di.GetFiles();
                foreach (FileInfo fSource in files)
                {
                    string strTargetFileEncrypted = Crypto.PathEncrypt(crypto, strSourcePath + "\\" + fSource.Name);
                    string strTargetFile = strDestination + "\\" + strTargetFileEncrypted;

                    if (!File.Exists(strTargetFile))
                    {
                        try
                        {
                            Crypto.TransformFile(crypto, fSource.FullName, strTargetFile, bufferIn, bufferOut);
                            File.SetLastWriteTime(strTargetFile, fSource.LastWriteTime);

                            _Logger.Debug("AddMissingFiles] New file created: '{0}'", strTargetFile);
                        }
                        catch (Exception ex) { _Logger.Error("AddMissingFiles] Failed to create new file: '{0}' Error: {1}", strTargetFile, ex.Message); }
                    }
                    else
                    {
                        FileInfo fDest = new FileInfo(strTargetFile);

                        try
                        {
                            if (fDest.LastWriteTime != fSource.LastWriteTime)
                            {
                                Crypto.TransformFile(crypto, fSource.FullName, strTargetFile, bufferIn, bufferOut);
                                File.SetLastWriteTime(strTargetFile, fSource.LastWriteTime);

                                _Logger.Debug("AddMissingFiles] File updated: '{0}'", strTargetFile);
                            }
                        }
                        catch (Exception ex) { _Logger.Error("AddMissingFiles] Failed to update file: '{0}' Error: {1}", strTargetFile, ex.Message); }
                    }

                    //Thread.Sleep(10);
                }
            }
            catch { }
        }

        /// <summary>
        /// Updates destination path by adding/decrypting non existing files/directories 
        /// </summary>
        /// <param name="crypto">Decryptor</param>
        /// <param name="cryptoDirSource">Source directory tree of encrypted files.</param>
        /// <param name="strDestinationRoot">Destination directory root path. This part is not beeing encrypted.</param>
        /// <param name="strDestinationPath">Destination directory path. This part is beeing encrypted together with the filename.</param>
        public static void AddMissingFiles(ICryptoTransform crypto, CryptoDirectory cryptoDirSource, string strDestinationRoot, string strDestinationPath)
        {
            byte[] bufferIn = new byte[1024 * 32];
            byte[] bufferOut = new byte[1024 * 32];

            AddMissingFiles(crypto, cryptoDirSource, strDestinationRoot, strDestinationPath, bufferIn, bufferOut);
        }

        /// <summary>
        /// Updates destination path by adding/decrypting non existing files/directories 
        /// </summary>
        /// <param name="crypto">Decryptor</param>
        /// <param name="cryptoDirSource">Source directory tree of encrypted files.</param>
        /// <param name="strDestinationRoot">Destination directory root path. This part is not beeing encrypted.</param>
        /// <param name="strDestinationPath">Destination directory path. This part is beeing encrypted together with the filename.</param>
        /// <param name="bufferIn">Input buffer for encrypting/decrypting. Input and output buffer must be the same size.</param>
        /// <param name="bufferOut">Output buffer for encrypting/decrypting. Input and output buffer must be the same size.</param>
        public static void AddMissingFiles(ICryptoTransform crypto, CryptoDirectory cryptoDirSource, string strDestinationRoot, string strDestinationPath, byte[] bufferIn, byte[] bufferOut)
        {
            try
            {
                _Logger.Debug("AddMissingFiles] Proccess: source '{0}', destination '{1}{2}'", cryptoDirSource.Path, strDestinationRoot, strDestinationPath);

                string strDestinationPathFull = strDestinationRoot + strDestinationPath;

                if (!Directory.Exists(strDestinationPathFull))
                {
                    _Logger.Debug("AddMissingFiles] Destination path '{0}' doesn't exist.", strDestinationPathFull);
                    return;
                }

                if (cryptoDirSource == null)
                {
                    _Logger.Debug("AddMissingFiles] Source path doesn't exist.");
                    return;
                }

                //Look for required directory
                CryptoDirectory cryptoDir = cryptoDirSource.FindDirectory(strDestinationPath);

                if (cryptoDir == null)
                {
                    _Logger.Debug("AddMissingFiles] Source path '{0}' doesn't exist.", strDestinationPath);
                    return; //not found
                }

                foreach (CryptoDirectory dir in cryptoDir.Directories)
                {
                    string strDirDest = strDestinationRoot + dir.Path;

                    if (!Directory.Exists(strDirDest))
                    {
                        try
                        {
                            Directory.CreateDirectory(strDirDest);

                            _Logger.Debug("AddMissingFiles] New directory created: '{0}'", strDirDest);

                        }
                        catch (Exception ex) { _Logger.Error("AddMissingFiles] Failed to create new directory: '{0}' Error: {1}", strDirDest, ex.Message); }
                    }


                    //Recursion
                    AddMissingFiles(crypto, dir, strDestinationRoot, dir.Path, bufferIn, bufferOut);

                    //Thread.Sleep(10);
                }

                foreach (CryptoFile fSource in cryptoDir.Files)
                {
                    string strTargetFile = strDestinationRoot + fSource.Path;

                    if (!File.Exists(strTargetFile))
                    {
                        try
                        {
                            Crypto.TransformFile(crypto, fSource.FullName, strTargetFile, bufferIn, bufferOut);
                            File.SetLastWriteTime(strTargetFile, fSource.LastWriteTime);

                            _Logger.Debug("AddMissingFiles] New file created: '{0}'", strTargetFile);
                        }
                        catch (Exception ex) { _Logger.Error("AddMissingFiles] Failed to create new file: '{0}' Error: {1}", strTargetFile, ex.Message); }
                    }
                    else
                    {
                        FileInfo fDest = new FileInfo(strTargetFile);

                        try
                        {
                            if (fDest.LastWriteTime != fSource.LastWriteTime)
                            {
                                Crypto.TransformFile(crypto, fSource.FullName, strTargetFile, bufferIn, bufferOut);
                                File.SetLastWriteTime(strTargetFile, fSource.LastWriteTime);

                                _Logger.Debug("AddMissingFiles] File updated: '{0}'", strTargetFile);
                            }
                        }
                        catch (Exception ex) { _Logger.Error("AddMissingFiles] Failed to update file: '{0}' Error: {1}", strTargetFile, ex.Message); }
                    }

                    //Thread.Sleep(10);
                }
            }
            catch { }
        }


        /// <summary>
        /// Updates destination path by deleting non existing files/directories
        /// </summary>
        /// <param name="strSource">Source path</param>
        /// <param name="strDestination">Destination path</param>
        public static void DeleteNonExistingFiles(string strSource, string strDestination)
        {
            try
            {
                _Logger.Debug("[DeleteNonExistingFiles] Proccess: source '{0}', destination '{1}'", strSource, strDestination);

                if (!Directory.Exists(strDestination))
                {
                    _Logger.Debug("[DeleteNonExistingFiles] Destination path '{0}' doesn't exist.", strDestination);
                    return;
                }

                string[] dirs = Directory.GetDirectories(strDestination);

                foreach (string strDir in dirs)
                {
                    string strSubDir = strDir.Substring(strDestination.Length);

                    string strDirSource = strSource + strSubDir;

                    //Recursion
                    DeleteNonExistingFiles(strDirSource, strDir);

                    if (!Directory.Exists(strDirSource))
                    {
                        try
                        {
                            Directory.Delete(strDir);

                            _Logger.Debug("[DeleteNonExistingFiles] Directory deleted: '{0}'", strDir);

                        }
                        catch (Exception ex) { _Logger.Error("DeleteNonExistingFiles] Failed to delete directory: '{0}' Error: {1}", strDir, ex.Message); }
                    }
                }

                DirectoryInfo di = new DirectoryInfo(strDestination);
                FileInfo[] files = di.GetFiles();
                foreach (FileInfo fi in files)
                {
                    string strTargetFile = strSource + "\\" + fi.Name;
                    if (!File.Exists(strTargetFile))
                    {
                        try
                        {
                            File.Delete(fi.FullName);

                            _Logger.Debug("[DeleteNonExistingFiles] File deleted: '{0}'", fi.FullName);
                        }
                        catch (Exception ex) { _Logger.Error("DeleteNonExistingFiles] Failed to delete file: '{0}' Error: {1}", fi.FullName, ex.Message); }
                    }
                }
            }
            catch { }


        }

        /// <summary>
        /// Updates destination path by deleting non existing files/directories
        /// </summary>
        /// <param name="strSourceRoot">Source directory root path. This part is not beeing encrypted.</param>
        /// <param name="strSourcePath">Source directory path. This part is beeing encrypted together with the filename.</param>
        /// <param name="cryptoDirDestination">Destination directory tree of encrypted files.</param>
        public static void DeleteNonExistingFiles(string strSourceRoot, string strSourcePath, CryptoDirectory cryptoDirDestination)
        {
            try
            {
                _Logger.Debug("[DeleteNonExistingFiles] Proccess: source '{0}{1}'", strSourceRoot, strSourcePath);

                if (cryptoDirDestination == null)
                {
                    _Logger.Debug("DeleteNonExistingFiles] Destination dir doesn't exist.");
                    return;
                }

                foreach (CryptoDirectory dir in cryptoDirDestination.Directories)
                {
                    //Recursion
                    DeleteNonExistingFiles(strSourceRoot, dir.Path, dir);
                }

                foreach (CryptoFile fi in cryptoDirDestination.Files)
                {
                    if (!File.Exists(strSourceRoot + fi.Path))
                    {
                        try
                        {
                            File.Delete(fi.FullName);

                            _Logger.Debug("[DeleteNonExistingFiles] File deleted: '{0}'", fi.FullName);
                        }
                        catch (Exception ex) { _Logger.Error("DeleteNonExistingFiles] Failed to delete file: '{0}' Error: {1}", fi.FullName, ex.Message); }
                    }
                }
            }
            catch { }


        }

        /// <summary>
        /// Updates destination path by deleting non existing files/directories
        /// </summary>
        /// <param name="cryptoDirSource">Source directory tree of encrypted files.</param>
        /// <param name="strDestinationRoot">Destination directory root path. This part is not beeing encrypted.</param>
        /// <param name="strDestinationPath">Destination directory path. This part is beeing encrypted together with the filename.</param>
        public static void DeleteNonExistingFiles(CryptoDirectory cryptoDirSource, string strDestinationRoot, string strDestinationPath)
        {
            try
            {
                _Logger.Debug("[DeleteNonExistingFiles] Proccess: destination '{0}{1}'", strDestinationRoot, strDestinationPath);

                string strDestinationPathFull = strDestinationRoot + strDestinationPath.TrimEnd('\\');

                if (!Directory.Exists(strDestinationPathFull))
                {
                    _Logger.Debug("DeleteNonExistingFiles] Destination path '{0}' doesn't exist.", strDestinationPathFull);
                    return;
                }

                if (cryptoDirSource != null && cryptoDirSource.Parent == null && string.IsNullOrWhiteSpace(cryptoDirSource.Path))
                    cryptoDirSource = cryptoDirSource.FindDirectory(strDestinationPath);

                string[] dirs = Directory.GetDirectories(strDestinationPathFull);

                foreach (string strDir in dirs)
                {
                    string strSubDir = strDir.Substring(strDestinationRoot.Length);

                    //Recursion
                    DeleteNonExistingFiles(cryptoDirSource == null ? null : cryptoDirSource.FindDirectory(strSubDir), strDestinationRoot, strSubDir);

                    if (cryptoDirSource == null || cryptoDirSource.FindDirectory(strSubDir) == null)
                    {
                        try
                        {
                            Directory.Delete(strDir);

                            _Logger.Debug("[DeleteNonExistingFiles] Directory deleted: '{0}'", strDir);

                        }
                        catch (Exception ex) { _Logger.Error("DeleteNonExistingFiles] Failed to delete directory: '{0}' Error: {1}", strDir, ex.Message); }
                    }
                }

                DirectoryInfo di = new DirectoryInfo(strDestinationPathFull);
                FileInfo[] files = di.GetFiles();
                foreach (FileInfo fi in files)
                {
                    if (cryptoDirSource == null || cryptoDirSource.Items.Find(f => f is CryptoFile && f.Name.Equals(fi.Name)) == null)
                    {
                        try
                        {
                            File.Delete(fi.FullName);

                            _Logger.Debug("[DeleteNonExistingFiles] File deleted: '{0}'", fi.FullName);
                        }
                        catch (Exception ex) { _Logger.Error("DeleteNonExistingFiles] Failed to delete file: '{0}' Error: {1}", fi.FullName, ex.Message); }
                    }
                }
            }
            catch { }


        }
    }
}
