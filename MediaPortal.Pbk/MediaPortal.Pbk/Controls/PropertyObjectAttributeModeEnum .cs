using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Controls
{
    public enum PropertyObjectAttributeModeEnum
    {
        /// <summary>
        /// Affects selected property only
        /// </summary>
        Include,

        /// <summary>
        /// Affects all properties
        /// </summary>
        IncludeAndRemoveOthers,

        /// <summary>
        /// Affects selected property only
        /// </summary>
        Exclude,

        /// <summary>
        /// Affects all properties
        /// </summary>
        ExcludeAndIncludeOthers

    }
}
