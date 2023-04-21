using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.GUI.Library;

namespace MediaPortal.Pbk.GUI
{
    public class GUIItem : GUIListItem
    {
        public bool ImagesInitialised = false;
        
        public object Tag;

        private static object _PadlockImg = new object();

        private EventHandler _onCoverReady = null;
        private EventHandler _onBackdropReady = null;

        public virtual string Cover
        {
            get
            {
                return this._Cover;
            }
            set
            {
                lock (_PadlockImg)
                {
                    this._Cover = value;
                    if (this._onCoverReady != null)
                    {
                        this._onCoverReady(this, null);
                        this._onCoverReady = null;
                    }
                }
            }
        }private string _Cover = null;

        public virtual string Backdrop
        {
            get
            {
                return this._Backdrop;
            }

            set
            {
                lock (_PadlockImg)
                {
                    this._Backdrop = value;
                    if (this._onBackdropReady != null)
                    {
                        this._onBackdropReady(this, null);
                        this._onBackdropReady = null;
                    }
                }
            }
        }private string _Backdrop = null;

        public GUIItem(string strLabel)
            : base(strLabel)
        {
        }

        public void TerminateImageCallbacks()
        {
            lock (_PadlockImg)
            {
                this._onCoverReady = null;
                this._onBackdropReady = null;
            }
        }

        /// <summary>
        /// Get Cover method
        /// </summary>
        /// <param name="callback">callback to be fired when cover path become ready</param>
        /// <returns>path to the cover if ready; otherwise null</returns>
        public string GetCover(EventHandler callback)
        {
            lock (_PadlockImg)
            {
                if (this._Cover != null)
                    return this._Cover;
                else
                {
                    this._onCoverReady = callback;
                    return null;
                }
            }
        }

        /// <summary>
        /// Get Backdrop method
        /// </summary>
        /// <param name="callback">callback to be fired when backdrop path become ready</param>
        /// <returns>path to the backdrop if ready; otherwise null</returns>
        public string GetBackdrop(EventHandler callback)
        {
            lock (_PadlockImg)
            {
                if (this._Backdrop != null)
                    return this._Backdrop;
                else
                {
                    this._onBackdropReady = callback;
                    return null;
                }
            }
        }
    }
}
