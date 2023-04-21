using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using MediaPortal.GUI.Library;

namespace MediaPortal.Pbk.Utils
{
    public class MemoryImage
    {
        public static string BuildFromFile(string strPluginName, string strFileName, Size size)
        {
            try
            {
                if ((string.IsNullOrEmpty(strFileName) || !System.IO.File.Exists(strFileName)))
                    return string.Empty;

                string strIdent = BuildIdentifierName(strPluginName, strFileName);

                if ((GUITextureManager.LoadFromMemory(null, strIdent, 0, size.Width, size.Height) > 0))
                    return strIdent;
                else
                {
                    Build(Image.FromFile(strFileName), strIdent, size);
                    return strIdent;
                }

            }
            catch { return string.Empty; }
        }

        public static string BuildIdentifierName(string strPluginName, string strName)
        {
            return "[" + strPluginName + ":" + strName + "]";
        }
                
        public static int Build(Image image, string strIdentifier, Size size)
        {
            try
            {
                // we don't have to try first, if name already exists mp will not do anything with the image
                //resize
                if ((size.Height > 0 && (size.Height != image.Size.Height || size.Width != image.Size.Width)))
                    image = new Bitmap(image, size);

                return GUITextureManager.LoadFromMemory(image, strIdentifier, 0, size.Width, size.Height);
            }
            catch {}
            return -1;
        }

        public static void Destroy(string strIdentifier)
        {
            GUITextureManager.ReleaseTexture(strIdentifier);
        }
    }
}
