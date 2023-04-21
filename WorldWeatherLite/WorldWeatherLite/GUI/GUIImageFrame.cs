using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if MS_DIRECTX
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
#else
using SharpDX.Direct3D9;
#endif

namespace MediaPortal.Plugins.WorldWeatherLite.GUI
{
    public class GUIImageFrame
    {
        public string ID
        { get; private set; }

        public Texture Texture = null;

        public System.Drawing.Size Size
        { get; private set; }

        /// <summary>
        /// Get or set frame duration in ms
        /// </summary>
        public int Duration
        {
            get { return this._Duration; }
            set
            {
                if (value > 0)
                    this._Duration = value;
            }
        }private int _Duration = 500;

        public GUIImageFrame(string strId, System.Drawing.Size size)
        {
            this.ID = strId;
            this.Size = size;
        }
    }
}
