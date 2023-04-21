using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace MediaPortal.Pbk.IO.VirtualDrive
{
    public class CryptoStream : Stream
    {
#if x64
        const string OPEN_SSL_DLLNAME = "libcrypto-1_1-x64";
        const string OPEN_SSL_SSLDLLNAME = "libssl-1_1-x64.dll";
#else
        const string OPEN_SSL_DLLNAME = "libcrypto-1_1";
        const string OPEN_SSL_SSLDLLNAME = "libssl-1_1";
#endif

        //[DllImport("kernel32.dll", SetLastError = true)]
        //[ResourceExposure(ResourceScope.None)]
        //unsafe static extern int ReadFile(SafeFileHandle handle, byte* bytes, int numBytesToRead, IntPtr numBytesRead_mustBeZero, NativeOverlapped* overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        unsafe static extern int ReadFile(SafeFileHandle handle, byte* bytes, int numBytesToRead, out int numBytesRead, IntPtr mustBeZero);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //[ResourceExposure(ResourceScope.None)]
        //unsafe static extern int WriteFile(SafeFileHandle handle, byte* bytes, int numBytesToWrite, IntPtr numBytesWritten_mustBeZero, NativeOverlapped* lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        unsafe static extern int WriteFile(SafeFileHandle handle, byte* bytes, int numBytesToWrite, out int numBytesWritten, IntPtr mustBeZero);

        [DllImport(OPEN_SSL_DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        unsafe extern static int EVP_DecryptUpdate(IntPtr ctx, byte* output, out int outl, byte* input, int inl);

        [DllImport(OPEN_SSL_DLLNAME, CallingConvention = CallingConvention.Cdecl)]
        unsafe extern static int EVP_EncryptUpdate(IntPtr ctx, byte* output, out int outl, byte* input, int inl);

        //[DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        //unsafe static extern IntPtr memcpy(byte* dest, byte* src, uint count);

        /// <summary>
        /// Sets the date and time that the specified file or directory was created, last accessed, or last modified.
        /// </summary>
        /// <param name="hFile">A <see cref="SafeFileHandle"/> to the file or directory. 
        /// To get the handler, <see cref="System.IO.FileStream.SafeFileHandle"/> can be used.</param>
        /// <param name="lpCreationTime">A Windows File Time that contains the new creation date and time 
        /// for the file or directory. 
        /// If the application does not need to change this information, set this parameter to 0.</param>
        /// <param name="lpLastAccessTime">A Windows File Time that contains the new last access date and time 
        /// for the file or directory. The last access time includes the last time the file or directory 
        /// was written to, read from, or (in the case of executable files) run. 
        /// If the application does not need to change this information, set this parameter to 0.</param>
        /// <param name="lpLastWriteTime">A Windows File Time that contains the new last modified date and time 
        /// for the file or directory. If the application does not need to change this information, 
        /// set this parameter to 0.</param>
        /// <returns>If the function succeeds, the return value is <c>true</c>.</returns>
        /// \see <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/ms724933">SetFileTime function (MSDN)</a>
        [DllImport("kernel32", SetLastError = true)]
        static extern bool SetFileTime(SafeFileHandle hFile, ref long lpCreationTime, ref long lpLastAccessTime, ref long lpLastWriteTime);

        [DllImport("ws2_32.dll", SetLastError = true)]
        static extern unsafe int send([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len, [In] SocketFlags socketFlags);

        [DllImport("ws2_32.dll", SetLastError = true)]
        static extern unsafe int recv([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len, [In] SocketFlags socketFlags);


        private OpenSSL.Crypto.CipherContext _Crypto;
        private FileStream _StreamSource;
        private SafeFileHandle _StreamSourceHandle;
        private bool _CanRead = false;
        private bool _CanWrite = false;
        private byte[] _Key;
        private byte[] _IV;
        private byte[] _Block = new byte[16];

        private Socket _SocketSource;

        private long _SocketPosition = 0;

        #region ctor
        public CryptoStream(FileStream stream, OpenSSL.Crypto.CipherContext crypto, byte[] key, byte[] iv, bool bCanWrite)
            : this(crypto, key, iv, bCanWrite)
        {
            this._StreamSource = stream;
            this._StreamSourceHandle = stream.SafeFileHandle;
        }

        public CryptoStream(Socket socket, OpenSSL.Crypto.CipherContext crypto, byte[] key, byte[] iv, bool bCanWrite)
            : this(crypto, key, iv, bCanWrite)
        {
            this._SocketSource = socket;
        }

        private CryptoStream(OpenSSL.Crypto.CipherContext crypto, byte[] key, byte[] iv, bool bCanWrite)
        {
            this._Crypto = crypto;
            this._CanWrite = bCanWrite;
            this._CanRead = !bCanWrite;
            this._Key = key;
            this._IV = iv;

            if (bCanWrite)
                this._Crypto.EncryptInit(this._Key, this._IV);
            else
                this._Crypto.DecryptInit(this._Key, this._IV);
        }

        #endregion

        #region Stream
        public override bool CanRead
        {
            get
            {
                return this._CanRead &&
                ((this._StreamSource != null && this._StreamSource.CanRead) || (this._SocketSource != null && this._SocketSource.Connected));
            }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get
            {
                return this._CanWrite &&
                    ((this._StreamSource != null && this._StreamSource.CanWrite) || (this._SocketSource != null && this._SocketSource.Connected));
            }
        }

        public override void Flush()
        {
            this._StreamSource.Flush();
        }

        public override long Length
        {
            get { return this._StreamSource.Length; }
        }

        public override long Position
        {
            get
            {
                if (this._SocketSource != null)
                    return this._SocketPosition;

                return this._StreamSource.Position;
            }
            set
            {
                if (this._SocketSource != null)
                {
                    if (value != this._SocketPosition)
                        throw new NotSupportedException();

                    return;
                }

                if (value != this._StreamSource.Position)
                {
                    //New position
                    this._StreamSource.Position = value;

                    //Skip offset
                    int iSkip = (int)(value & 0xF); //Calculate skip value

                    //Calculate block counter
                    value >>= 4;

                    //Copy block counter to IV; MSB
                    for (int i = 15; i >= 8; i--)
                    {
                        this._IV[i] = (byte)value;
                        value >>= 8;
                    }

                    //ReInit
                    int iLen;
                    if (this._CanWrite)
                    {
                        this._Crypto.EncryptInit(this._Key, this._IV); //encryptor
                        if (iSkip > 0)
                            this._Crypto.EncryptUpdate(this._Block, this._Block, iSkip, out iLen);
                    }
                    else
                    {
                        this._Crypto.DecryptInit(this._Key, this._IV); //decryptor
                        if (iSkip > 0)
                            this._Crypto.DecryptUpdate(this._Block, this._Block, iSkip, out iLen);
                    }
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!this.CanRead)
                throw new ArgumentException("Invalid read operation");

            if (count < 1)
                throw new ArgumentException("Invalid count");

            if (buffer == null)
                throw new ArgumentException("Buffer is null");

            if (offset != 0)
                throw new ArgumentException("Invalid offset");

            if (count > buffer.Length)
                throw new ArgumentException("Invalid buffer size");

            Stream s = this._StreamSource;
            if (s == null && this._SocketSource == null)
                return 0;

            //Read source
            int iRd;
            if (this._SocketSource != null)
                iRd = this._SocketSource.Receive(buffer, 0, count, SocketFlags.None);
            else
                iRd = s.Read(buffer, 0, count);

            if (iRd > 0)
            {
                //Decrypt;
                int iLen;
                this._Crypto.DecryptUpdate(buffer, buffer, iRd, out iLen);

                return iRd;
            }
            else
                return 0; //stream closed

        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            this._StreamSource.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!this.CanWrite)
                throw new ArgumentException("Invalid write operation");

            if (offset != 0)
                throw new ArgumentException("Invalid offset");

            if (count < 1)
                throw new ArgumentException("Invalid count");

            if (buffer == null)
                throw new ArgumentException("Buffer is null");

            Stream s = this._StreamSource;
            if (s == null && this._SocketSource == null)
                return;

            //Encrypt the requested buffer
            int iLen;
            this._Crypto.EncryptUpdate(buffer, buffer, count, out iLen);

            //Write encrypted data to the filestream
            if (iLen > 0)
            {
                if (this._SocketSource != null)
                    this._SocketSource.Send(buffer, 0, iLen, SocketFlags.None);
                else
                    s.Write(buffer, offset, iLen);
            }
        }

        public override void Close()
        {
            Stream s = this._StreamSource;
            if (s != null)
            {
                s.Close();
                this._StreamSourceHandle = null;
            }

            Socket sc = this._SocketSource;
            if (sc != null)
            {
                sc.Close();
            }
        }
        #endregion


        public unsafe int ReadNative(IntPtr buffer, int iCount)
        {
            if (!this.CanRead)
                throw new ArgumentException("Invalid read operation");

            if (iCount < 1)
                throw new ArgumentException("Invalid count");

            if (buffer == IntPtr.Zero)
                throw new ArgumentException("Buffer is null");

            Stream s = this._StreamSource;
            if (s == null && this._SocketSource == null)
                return 0;

            //Read source native
            int iRd;
            if (this._SocketSource != null)
            {
                iRd = recv(this._SocketSource.Handle, (byte*)buffer, iCount, SocketFlags.None);
                if (iRd < 0)
                    throw new ArgumentException("Failed to read native socket");

                this._SocketPosition += iRd;
            }
            else
            {
                int iResult = ReadFile(this._StreamSourceHandle, (byte*)buffer, iCount, out iRd, IntPtr.Zero);
                if (iResult == 0)
                    throw new ArgumentException("Failed to read native stream");
            }

            if (iRd > 0)
            {
                //Decrypt;
                int iLen;
                EVP_DecryptUpdate(this._Crypto.Handle, (byte*)buffer, out iLen, (byte*)buffer, iRd);

                return iRd;
            }
            else
                return 0; //stream closed
        }

        public unsafe int WriteNative(IntPtr buffer, int iCount)
        {
            if (!this.CanWrite)
                throw new ArgumentException("Invalid write operation");

            if (iCount < 1)
                throw new ArgumentException("Invalid count");

            if (buffer == IntPtr.Zero)
                throw new ArgumentException("Buffer is null");

            Stream s = this._StreamSource;
            if (s == null && this._SocketSource == null)
                return 0;

            //Encrypt the requested buffer
            int iLen;
            EVP_EncryptUpdate(this._Crypto.Handle, (byte*)buffer, out iLen, (byte*)buffer, iCount);

            //Write encrypted data to the filestream
            if (iLen > 0)
            {
                //Warite native
                int iWr;
                if (this._SocketSource != null)
                {
                    iWr = send(this._SocketSource.Handle, (byte*)buffer, iLen, SocketFlags.None);

                    if (iWr < 0)
                        throw new ArgumentException("Failed to write native socket");

                    this._SocketPosition += iWr;
                }
                else
                {
                    int iResult = WriteFile(this._StreamSourceHandle, (byte*)buffer, iLen, out iWr, IntPtr.Zero);

                    if (iResult == 0)
                        throw new ArgumentException("Failed to write native stream");
                }
                //if (iWr != iLen)
                //    throw new ArgumentException("Incomplete write native stream");

                return iWr;
            }
            else
                return 0;
        }


        public void SetFileTime(DateTime? dtCreationTime, DateTime? dtLastAccessTime, DateTime? dtLastWriteTime)
        {
            long lCt = dtCreationTime != null ? ((DateTime)dtCreationTime).ToFileTime() : 0;
            long lAt = dtLastAccessTime != null ? ((DateTime)dtLastAccessTime).ToFileTime() : 0;
            long lWt = dtLastWriteTime != null ? ((DateTime)dtLastWriteTime).ToFileTime() : 0;

            if (SetFileTime(this._StreamSourceHandle, ref lCt, ref lAt, ref lWt))
                return;

            throw Marshal.GetExceptionForHR(Marshal.GetLastWin32Error());
        }


        public void Lock(long lOffset, long lLength)
        {
            this._StreamSource.Lock(lOffset, lLength);
        }

        public void Unlock(long lOffset, long lLength)
        {
            this._StreamSource.Unlock(lOffset, lLength);
        }
    }
}
