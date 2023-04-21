using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.IO.VirtualDrive
{
    public enum CryptoVirtualDriveMountResultEnum
    {
        None = 0,
        Failed,
        Mounted,
        AlreadyMounted,
        NoFreeLetter,
        DirectoryNotExists,
        InvalidKey,
        WrongKey,
        InvalidSource
    }
}
