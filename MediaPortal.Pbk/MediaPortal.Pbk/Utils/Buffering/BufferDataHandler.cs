using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Utils.Buffering
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="iOffset"></param>
    /// <param name="iLength"></param>
    /// <returns>Number of unprocessed bytes. Max iLength.</returns>
    public delegate int BufferDataHandler(byte[] buffer, int iOffset, int iLength);
}
