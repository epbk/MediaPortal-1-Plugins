using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MediaPortal.Pbk.Extensions
{
    public static class JsonExtensions
    {
        public static void ForEach(this JToken self, Action<JToken> action)
        {
            for (int i = 0; i < ((JArray)self).Count; i++)
                action(self[i]);
        }
    }
}
