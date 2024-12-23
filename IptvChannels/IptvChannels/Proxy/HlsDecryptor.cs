using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace MediaPortal.IptvChannels.Proxy
{
    public class HlsDecryptor
    {
        public string ID {get; private set;}
        public int SqId { get; private set; }
        public RijndaelManaged Rijndael { get; private set; }
        public byte[] IV { get; private set; }
        public long UID { get; }

        private static long _UIDcnt = 0;

        public HlsDecryptor(string strID, int iSqID, RijndaelManaged rjnd, byte[] iv)
        {
            this.UID = System.Threading.Interlocked.Increment(ref _UIDcnt);
            this.ID = strID;
            this.SqId = iSqID;
            this.Rijndael = rjnd;
            this.IV = iv;
        }

        public ICryptoTransform GetCryptoTranform(int iSqNr)
        {
            lock (this)
            {
                //Initialization Vector
                if (this.IV == null)
                {
                    //Create IV based on Sequence Number (Integer; Bigendian format; padding on left)
                    byte[] iv = new byte[16];
                    iv[15] = (byte)((iSqNr >> 24) & 0xFF);
                    iv[14] = (byte)((iSqNr >> 16) & 0xFF);
                    iv[13] = (byte)((iSqNr >> 8) & 0xFF);
                    iv[12] = (byte)(iSqNr & 0xFF);
                    this.Rijndael.IV = iv;
                }
                else
                    this.Rijndael.IV = this.IV;

                return this.Rijndael.CreateDecryptor();
            }
        }
    }
}
