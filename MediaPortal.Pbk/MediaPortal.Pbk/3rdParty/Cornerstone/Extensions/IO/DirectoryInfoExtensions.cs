using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NLog;

namespace MediaPortal.Pbk.Cornerstone.Extensions.IO
{
    public static class DirectoryInfoExtensions
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Gets a value indicating wether this directory is available
        /// </summary>
        /// <param name="self"></param>
        /// <returns>True if available</returns>
        public static bool IsAccessible(this DirectoryInfo self)
        {
            if (!self.Exists)
                return false;

            // unless this is a special case path, trust the DirectoryInfo.Exists call
            // if (!self.IsReparsePoint() && !self.IsUncPath()) 
            // return true;

            // turns out we can't trust the Exists call so attempt to get a directory listing, if this succeeds the path is online
            try
            {
                self.GetDirectories();
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Get all files from directory and it's subdirectories.
        /// </summary>
        /// <param name="inputDir"></param>
        /// <returns></returns>
        public static List<FileInfo> GetFilesRecursive(this DirectoryInfo self)
        {
            List<FileInfo> fileList = new List<FileInfo>();
            DirectoryInfo[] subdirectories = new DirectoryInfo[] { };

            try
            {
                fileList.AddRange(self.GetFiles("*"));
                subdirectories = self.GetDirectories();
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(ThreadAbortException))
                    throw e;

                _Logger.Debug("[GetFilesRecursive] Error while retrieving files/directories for: {0} {1}", self.FullName, e);
            }

            foreach (DirectoryInfo subdirectory in subdirectories)
            {
                try
                {
                    if ((subdirectory.Attributes & FileAttributes.System) == 0)
                        fileList.AddRange(GetFilesRecursive(subdirectory));
                    else
                        _Logger.Debug("[GetFilesRecursive] Skipping directory {0} because it is flagged as a System folder.", subdirectory.FullName);
                }
                catch (Exception e)
                {
                    if (e.GetType() == typeof(ThreadAbortException))
                        throw e;

                    _Logger.Debug("[GetFilesRecursive] Error during attribute check for: {0} {1}", subdirectory.FullName, e);
                }
            }

            return fileList;
        }

        /// <summary>
        /// Get the largest file from a directory matching the specified file mask
        /// </summary>
        /// <param name="strFileMask">the filemask to match</param>
        /// <returns>path to the largest file or null if no file was found or an error occured</returns>
        public static string GetLargestFile(this DirectoryInfo self, string strFileMask)
        {
            string strLargestFile = null;
            long lLargestSize = 0;
            try
            {
                FileInfo[] files = self.GetFiles(strFileMask);
                foreach (FileInfo file in files)
                {
                    long lFileSize = file.Length;
                    if (lFileSize > lLargestSize)
                    {
                        lLargestSize = lFileSize;
                        strLargestFile = file.FullName;
                    }
                }
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException)
                    throw e;

                _Logger.ErrorException("[GetLargestFile] Error while retrieving files for: " + self.FullName, e);
            }
            return strLargestFile;
        }

    }
}
