using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;

namespace MediaPortal.Pbk.Net.Http
{
    public class ChunkedStream : Stream
    {
        private Stream _Stream;
        private int _ChunkSize = -1;
        private bool _ReadMode;
        private int _ChunkSizeTextLength = 0;
        private char[] _ChunkSizeText = new char[8];

        /// <summary>
        /// If 'True' zero chunk is checked right after receiving the previous chunk(read mode only).
        /// </summary>
        public bool CheckZeroChunk = true;

        /// <summary>
        /// Returns 'True' if zero chunk is received.
        /// </summary>
        public bool IsEnded
        {
            get
            {
                return this._Ended;
            }
        }private bool _Ended = false;


        public ChunkedStream(Stream stream, bool bReadMode)
        {
            this._Stream = stream;
            this._ReadMode = bReadMode;
        }


        public override bool CanRead
        {
            get { return this._ReadMode; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return !this._ReadMode; }
        }

        public override bool CanTimeout
        {
            get { return this._Stream.CanTimeout; }
        }

        public override void Flush()
        {
            this._Stream.Flush();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int iOffset, int iLength)
        {
            if (!this._ReadMode)
                throw new NotSupportedException();

            if (this._Ended)
                return 0;

            //Expecting chunk header
            if (this._ChunkSize < 0 && this.readChunkSizeAndCheckZero())
                return 0; //Chunk stream ended
            else
            {
                //Chunk is receiving
                int iCount = Math.Min(iLength, this._ChunkSize);
                int iRead = this._Stream.Read(buffer, iOffset, iCount);
                this._ChunkSize -= iRead;

                if (this._ChunkSize == 0)
                {
                    //End of chunk
                    if (this._Stream.ReadByte() != '\r' || this._Stream.ReadByte() != '\n')
                    {
                        this._Stream.Close();
                        throw new Exception("Invalid chunk termination");
                    }

                    if (this.CheckZeroChunk)
                        //We need to check zero chunk now;
                        //Gzip returns 0 without reading zero chunk
                        this.readChunkSizeAndCheckZero();
                    else
                        this._ChunkSize = -1; //Expecting a new chunk
                }

                return iRead;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int iOffset, int iLength)
        {
            if (this._ReadMode)
                throw new NotSupportedException();

            byte[] chunkHeader = Encoding.ASCII.GetBytes(string.Format("{0:X}\r\n", iLength));

            this._Stream.Write(chunkHeader, 0, chunkHeader.Length);
            this._Stream.Write(buffer, iOffset, iLength);
            this._Stream.Write(new byte[] { (byte)'\r', (byte)'\n' }, 0, 2);
            this._Stream.Flush();
        }

        public override void Close()
        {
            if (!this._ReadMode)
                this._Stream.Write(new byte[] { 0, (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' }, 0, 5);

            this._Stream.Close();
        }

        public void Reset()
        {
            this._ChunkSize = -1;
            this._Ended = false;
        }


        private bool readChunkSizeAndCheckZero()
        {
            if ((this._ChunkSize = this.readChunkSize()) == 0)
            {
                //Zero terminating chunk
                if (this._Stream.ReadByte() != '\r' || this._Stream.ReadByte() != '\n')
                {
                    this._Stream.Close();
                    throw new Exception("Invalid chunk termination");
                }
                else
                {
                    //Chunk stream ended
                    this._Ended = true;
                    return true;
                }
            }

            return false;
        }

        private int readChunkSize()
        {
            this._ChunkSizeTextLength = 0;
            bool bCarrierReturnReceived = false;
            bool bSizeCaptured = false;
            int iByte;
            while ((iByte = this._Stream.ReadByte()) >= 0)
            {
                if (iByte == '\n' && bCarrierReturnReceived)
                {
                    //Get chunk size from hex format
                    if (this._ChunkSizeTextLength == 0)
                        throw new Exception("Invalid chunk size length.");

                    int iValue = 0;
                    int iShift = 0;
                    while (this._ChunkSizeTextLength-- > 0)
                    {
                        int iChar = this._ChunkSizeText[this._ChunkSizeTextLength];

                        if (iChar >= '0' && iChar <= '9')
                            iChar -= '0';
                        else if (iChar >= 'A' && iChar <= 'F')
                            iChar -= ('A' - 10);
                        else if (iChar >= 'a' && iChar <= 'f')
                            iChar -= ('a' - 10);
                        else
                            throw new Exception("Invalid chunk size char.");

                        iValue |= iChar << iShift;
                        iShift += 4;
                    }

                    if (iValue < 0)
                        throw new Exception("Invalid chunk size value.");

                    return iValue;
                }
                else
                {
                    if (!bSizeCaptured)
                    {
                        if (iByte == ';')
                        {
                            bSizeCaptured = true;
                            continue;
                        }
                        else if (iByte != ' ' && iByte != '\r')
                        {
                            if (this._ChunkSizeTextLength >= this._ChunkSizeText.Length)
                                throw new Exception("Invalid chunk size length.");

                            this._ChunkSizeText[this._ChunkSizeTextLength++] = (char)iByte;
                        }
                    }

                    bCarrierReturnReceived = iByte == '\r';
                }
            }

            throw new Exception("Bad chunk header");
        }
    }
}
