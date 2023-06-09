﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using System.Collections.Generic;

namespace MediaPortal.Pbk.Cornerstone.Extensions.IO
{

    /// <summary>
    /// DriveInfo Manager Class that also provides DriveInfo extensions
    /// </summary>
    public static class DriveInfoExtensions
    {

        #region Imports

        [DllImport("kernel32.dll")]
        private static extern long GetVolumeInformation(string PathName, StringBuilder VolumeNameBuffer, UInt32 VolumeNameSize, ref UInt32 VolumeSerialNumber, ref UInt32 MaximumComponentLength, ref UInt32 FileSystemFlags, StringBuilder FileSystemNameBuffer, UInt32 FileSystemNameSize);

        #endregion

        /// <summary>
        /// Gets the driveletter in a two character format. 
        /// </summary>
        /// <param name="self"></param>
        /// <returns>example: "C:"</returns>
        public static string GetDriveLetter(this DriveInfo self)
        {
            return self.Name.Substring(0, 2);
        }

        /// <summary>
        /// Gets a value indicating whether this drive is removable
        /// </summary>
        /// <param name="self"></param>
        /// <returns>True if removable</returns>
        public static bool IsRemovable(this DriveInfo self)
        {
            // Every other type than fixed will be considered removable
            return (self.DriveType != DriveType.Fixed);
        }

        /// <summary>
        /// Gets a value indicating whether this is an optical drive
        /// </summary>
        /// <returns>True if this is an optical drive</returns>
        public static bool IsOptical(this DriveInfo self)
        {
            return (self.DriveType == DriveType.CDRom);
        }

        /// <summary>
        /// Gets a value indicating whether the drive exists
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool Exists(this DriveInfo self)
        {
            return (self.DriveType != DriveType.NoRootDirectory);
        }

        /// <summary>
        /// Gets the volume serial number of the drive
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string GetVolumeSerial(this DriveInfo self)
        {
            uint dwSerNum = 0;
            uint dwMaxCompLen = 0;
            UInt32 dwVolumeFlags = new UInt32();
            StringBuilder sbVolumeLabel = new StringBuilder(256);
            StringBuilder sbFSName = new StringBuilder(256);
            long lRt = GetVolumeInformation(self.Name, sbVolumeLabel, (UInt32)sbVolumeLabel.Capacity, ref dwSerNum, ref dwMaxCompLen, ref dwVolumeFlags, sbFSName, (UInt32)sbFSName.Capacity);
            // return the serial number as a hexidecimal string
            return ((dwSerNum != 0) ? Convert.ToString(dwSerNum, 16).ToUpper() : string.Empty);
        }

    }
}
