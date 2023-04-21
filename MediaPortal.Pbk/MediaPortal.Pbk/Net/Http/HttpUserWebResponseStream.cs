using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebResponseStream : Stream
    {
        private Stream _Stream;
        private HttpUserWebRequest _Request;
        public HttpUserWebResponseStream(Stream stream, HttpUserWebRequest req)
        {
            this._Stream = stream;
            this._Request = req;
        }

        public override bool CanRead
        {
            get { return this._Stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return this._Stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return this._Stream.CanWrite; }
        }

        public override void Flush()
        {
            this._Stream.Flush();
        }

        public override long Length
        {
            get { return this._Stream.Length; }
        }

        public override long Position
        {
            get
            {
                return this._Stream.Position;
            }
            set
            {
                this._Stream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this._Stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this._Stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this._Stream.SetLength(value); ;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this._Stream.Write(buffer, offset, count);
        }

        public override void Close()
        {
            
        }

        public override int ReadByte()
        {
            return this._Stream.ReadByte();
        }

        public override void WriteByte(byte value)
        {
            this._Stream.WriteByte(value);
        }

        public override int ReadTimeout
        {
            get
            {
                return this._Stream.ReadTimeout;
            }
            set
            {
                this._Stream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return this._Stream.WriteTimeout;
            }
            set
            {
                this._Stream.WriteTimeout = value;
            }
        }

        public void CloseStream()
        {
            this._Stream.Close();
        }
    }
}
