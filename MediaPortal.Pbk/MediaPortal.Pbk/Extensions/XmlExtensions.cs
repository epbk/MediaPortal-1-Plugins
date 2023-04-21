using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace MediaPortal.Pbk.Extensions
{
    /// <summary>
    /// Xml extensions
    /// </summary>
    public static class XmlExtensions
    {
        /// <summary>
        /// Performs the specified action on each <see cref="System.Xml.XmlNode" /> of the <see cref="System.Xml.XmlNodeList" />.
        /// </summary>
        /// <param name="self"><see cref="System.Xml.XmlNodeList" /> instance.</param>
        /// <param name="action">The <see cref="T:System.Action" /> delegate to perform on each <see cref="System.Xml.XmlNode" /> of the <see cref="System.Xml.XmlNodeList" />.</param>
        /// <exception><paramref name="self" /> is null.</exception>
        /// <exception><paramref name="action" /> is null.</exception>
        public static void ForEach(this XmlNodeList self, Action<XmlNode> action)
        {
            if (self == null || action == null)
                throw new ArgumentNullException();

            for (int i = 0; i < self.Count; i++)
                action(self[i]);
        }
    }
}
