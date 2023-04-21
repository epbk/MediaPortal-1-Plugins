using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Controls
{
    public class PropertyObjectConfig
    {
        public IEnumerable<string> WriteProps;

        public IEnumerable<string> BrowsableProps;

        public PropertyObjectAttributeModeEnum WritableMode;

        public PropertyObjectAttributeModeEnum BrowsableMode;
    }
}
