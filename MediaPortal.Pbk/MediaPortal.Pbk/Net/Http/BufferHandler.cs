using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Net.Http
{
    public delegate int BufferHandler(byte[] data, int iOffset, int iLength, long lPosition);
}
