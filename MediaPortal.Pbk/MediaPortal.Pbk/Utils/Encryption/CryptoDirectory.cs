using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Utils.Encryption
{
    public class CryptoDirectory : CryptoItem
    {
        public List<CryptoItem> Items = new List<CryptoItem>();

        public CryptoDirectory(string strName, CryptoDirectory parent)
            : base(strName, parent)
        {
        }

        public IEnumerable<CryptoDirectory> Directories
        {
            get
            {
                for (int i = 0; i < this.Items.Count; i++)
                {
                    if (this.Items[i] is CryptoDirectory)
                        yield return (CryptoDirectory)this.Items[i];
                }
            }
        }

        public IEnumerable<CryptoFile> Files
        {
            get
            {
                for (int i = 0; i < this.Items.Count; i++)
                {
                    if (this.Items[i] is CryptoFile)
                        yield return (CryptoFile)this.Items[i];
                }
            }
        }

        public CryptoDirectory FindDirectory(string strPath, bool bLocal = false)
        {
            if (this.Path.Equals(strPath, StringComparison.CurrentCultureIgnoreCase))
                return this;

            for (int i = 0; i < this.Items.Count; i++)
            {
                CryptoItem item = this.Items[i];

                if (item is CryptoDirectory)
                {
                    if (item.Path.Equals(strPath, StringComparison.CurrentCultureIgnoreCase))
                        return (CryptoDirectory)item;

                    if (!bLocal)
                    {
                        item = ((CryptoDirectory)item).FindDirectory(strPath);
                        if (item != null)
                            return (CryptoDirectory)item;
                    }
                }
            }

            return null;
        }

        public CryptoFile FindFile(string strPath, bool bLocal = false)
        {
            for (int i = 0; i < this.Items.Count; i++)
            {
                CryptoItem item = this.Items[i];

                if (item is CryptoFile)
                {
                    if (item.Path.Equals(strPath, StringComparison.CurrentCultureIgnoreCase))
                        return (CryptoFile)item;
                }
                else if (!bLocal && item is CryptoDirectory)
                {
                    item = ((CryptoDirectory)item).FindFile(strPath);
                    if (item != null)
                        return (CryptoFile)item;
                }
            }

            return null;
        }

    }
}
