using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Utils.Buffering
{
    public interface IBuffer
    {
        int CurrentLevel
        { get; }

        int CurrentValue
        { get; }

        int BufferSize
        { get; }

        int BufferSizeMax
        { get; }

        int PositionRead
        { get; }

        int PositionWrite
        { get; }

        int Buffers
        { get; }

        int BuffersInUse
        { get; }

        int BuffersMax
        { get; set; }
    }
}
