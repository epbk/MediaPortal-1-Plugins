using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Tasks
{
    public enum TaskPriority
    {
        Highest = 100000,
        High = 10000,
        AboveNormal = 1000,
        Normal = 0,
        BelowNormal = -1000,
        Low = -10000,
        Lowest = -100000
    }
}
