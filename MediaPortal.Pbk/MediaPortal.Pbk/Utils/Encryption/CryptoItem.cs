using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Utils.Encryption
{
    public abstract class CryptoItem
    {
        public string Name
        {
            get;
            private set;
        }

        public CryptoItem Parent
        {
            get;
            private set;
        }

        public CryptoItem(string strName, CryptoItem parent)
        {
            this.Parent = parent;
            this.Name = strName;
        }

        public virtual string Path
        {
            get
            {
                if (this._Path == null)
                {
                    if (this.Parent != null)
                        this._Path = this.Parent.Path + "\\" + this.Name;
                    else if (this.Name != null)
                        this._Path = "\\" + this.Name;
                    else
                        this._Path = string.Empty;
                }

                return this._Path;
            }
        }protected string _Path = null;

    }
}
