using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MediaPortal.Pbk.Tasks
{
    public class TaskCompletation
    {
        /// <summary>
        /// Raised upon decreasing <seealso cref="InProgress"/> to zero.
        /// </summary>
        public ManualResetEvent Complete
        {
            get
            {
                return this._Complete;
            }
        }private ManualResetEvent _Complete = new ManualResetEvent(false);

        /// <summary>
        /// Number of tasks in progress.
        /// </summary>
        public int InProgress
        {
            get
            {
                return this._InProgress;
            }

            internal set
            {
                if (value < 0)
                    this._InProgress = 0;
                else
                    this._InProgress = value;
            }
        }private int _InProgress = 0;

        /// <summary>
        /// User tag
        /// </summary>
        public object Tag;
    }
}
