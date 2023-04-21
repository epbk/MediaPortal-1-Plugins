using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Utils.Encryption
{
    public class CryptoFile : CryptoItem
    {
        public string FullName { get; private set; }
        public DateTime LastWriteTime { get; private set; }

        public CryptoFile(string strName, CryptoDirectory parent, string strFullName, DateTime dtLastWriteTime)
            : base(strName, parent)
        {
            this.FullName = strFullName;
            this.LastWriteTime = dtLastWriteTime;
        }
    }
}
