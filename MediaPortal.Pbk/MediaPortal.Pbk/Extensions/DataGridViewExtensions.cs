using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MediaPortal.Pbk.Extensions
{
    /// <summary>
    /// DataGridView extensions
    /// </summary>
    public static class DataGridViewExtensions
    {
        /// <summary>
        /// Performs the specified action on each <see cref="System.Windows.Forms.DataGridViewRow" /> of the <see cref="System.Windows.Forms.DataGridViewSelectedRowCollection" />.
        /// </summary>
        /// <param name="self"><see cref="System.Windows.Forms.DataGridViewSelectedRowCollection" /> instance.</param>
        /// <param name="action">The <see cref="T:System.Action`1" /> delegate to perform on each <see cref="System.Windows.Forms.DataGridViewRow" /> of the <see cref="System.Windows.Forms.DataGridViewSelectedRowCollection" />.</param>
        /// <exception><paramref name="self" /> is null.</exception>
        /// <exception><paramref name="action" /> is null.</exception>
        public static void ForEach(this DataGridViewSelectedRowCollection self, Action<DataGridViewRow> action)
        {
            if (self == null || action == null)
                throw new ArgumentNullException();

            for (int i = 0; i < self.Count; i++)
                action(self[i]);
        }
    }
}
