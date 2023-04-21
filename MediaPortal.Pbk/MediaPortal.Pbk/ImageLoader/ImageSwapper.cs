using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.GUI.Library;
using NLog;
using System.Threading;

namespace MediaPortal.Pbk.ImageLoader
{
    /// <summary>
    /// This class takes two GUIImage objects so that you can treat them as one. When you assign
    /// a new image to this object using the Filename property, the currently active image is 
    /// hidden and the second is made visibile (with the new image file displayed). This allows
    /// for animations on image change, such as a fading transition.
    /// 
    /// This class also uses the AsyncImageResource class for asynchronus image loading, 
    /// dramtically improving GUI performance. It also takes advantage of the Delay feature of
    /// the AsyncImageResource to prevent unnecessary loads when rapid image changes are made.
    /// </summary>
    public class ImageSwapper
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();
        private bool _ImagesNeedSwapping = false;
        private object _LoadingLock = new object();

        /// <summary>
        /// Image loading only occurs when set to true. If false all resources will be unloaded
        /// and all GUIImage objects set to invisible. Setting Active to false also clears the
        /// Filename property.
        /// </summary>
        public bool Active
        {
            get { return this._Active; }
            set
            {
                if (this._Active == value)
                    return;

                this._Active = value;
                this._ImageResource.Active = this._Active;

                // if we are inactive be sure both properties are cleared
                if (!Active)
                {
                    _Logger.Info("Clearing Properties");
                    this._ImageResource.Property = this._PropertyTwo;
                    this._ImageResource.Property = this._PropertyOne;
                    this._ImageResource.Filename = null;
                }
            }
        }private bool _Active = true;

        /// <summary>
        /// The filename of the image backing this resource. Reassign to change textures.
        /// </summary>
        public string Filename
        {
            get
            {
                return this._Filename;
            }

            set
            {
                lock (this._LoadingLock)
                {
                    if (!Active)
                        value = null;

                    if ((value != null && value.Equals(this._Filename)) || this._GuiImageOne == null)
                        return;

                    // if we have a second backdrop image object, alternate between the two
                    if (this._GuiImageTwo != null && this._ImagesNeedSwapping)
                    {
                        if (this._ImageResource.Property.Equals(this._PropertyOne))
                            this._ImageResource.Property = this._PropertyTwo;
                        else
                            this._ImageResource.Property = this._PropertyOne;

                        this._ImagesNeedSwapping = false;
                    }

                    // update resource with new file
                    this._Filename = value;

                    if (this._LoadingImage != null)
                        this._LoadingImage.Visible = true;

                    this._ImageResource.Filename = this._Filename;
                }
            }
        }private string _Filename = null;

        /// <summary>
        /// First GUIImage used for the visibilty toggle behavior. If set to NULL the ImageSwapper
        /// behaves as if inactive.
        /// </summary>
        public GUIImage GUIImageOne
        {
            get { return this._GuiImageOne; }
            set
            {
                if (this._GuiImageOne == value)
                    return;

                this._GuiImageOne = value;
                if (this._GuiImageOne != null)
                {
                    this._GuiImageOne.FileName = this._PropertyOne;
                    this._Filename = null;
                }
            }
        }private GUIImage _GuiImageOne;

        /// <summary>
        /// Second GUIImage used for the visibility toggle behavior. If set to NULL no toggling
        /// occurs and only GUIImageOne is used. This provides backwards compatibility if a skin
        /// does not implement the second GUIImage control.
        /// </summary>
        public GUIImage GUIImageTwo
        {
            get { return this._GuiImageTwo; }
            set
            {
                if (this._GuiImageTwo == value)
                    return;

                this._GuiImageTwo = value;
                if (this._GuiImageTwo != null)
                {
                    this._GuiImageTwo.FileName = this._PropertyTwo;
                    this._Filename = null;
                }
            }
        }private GUIImage _GuiImageTwo;

        /// <summary>
        /// If set, this image object will be set to visible during the load process and will
        /// be set to hidden when the next image has completed loading.
        /// </summary>
        public GUIImage LoadingImage
        {
            get { return this._LoadingImage; }
            set
            {
                this._LoadingImage = value;
            }
        } private GUIImage _LoadingImage;

        /// <summary>
        /// The property assigned to the first GUIImage. Assigning this property to the texture
        /// field of another GUIImage object will result in the image being loaded there. This
        /// can also be useful for backwards compatibility.
        /// </summary>
        public string PropertyOne
        {
            get { return this._PropertyOne; }
            set
            {
                if (this._ImageResource.Property.Equals(this._PropertyOne))
                    this._ImageResource.Property = value;

                this._PropertyOne = value;
            }
        }private string _PropertyOne = "#Cornerstone.ImageSwapper1";

        /// <summary>
        /// The property field used for the second GUIImage.
        /// </summary>
        public string PropertyTwo
        {
            get { return this._PropertyTwo; }
            set
            {
                if (this._ImageResource.Property.Equals(this._PropertyTwo))
                    this._ImageResource.Property = value;

                this._PropertyTwo = value;
            }
        }private string _PropertyTwo = "#Cornerstone.ImageSwapper2";

        /// <summary>
        /// The AsyncImageResource backing this object. All image loading and unloading is done
        /// in the background by this object.
        /// </summary>
        public AsyncImageResource ImageResource
        {
            get { return this._ImageResource; }
        }private AsyncImageResource _ImageResource;

        static ImageSwapper()
        {
            Logging.Log.Init();
        }

        public ImageSwapper()
        {
            this._ImageResource = new AsyncImageResource();
            this._ImageResource.Property = this._PropertyOne;
            this._ImageResource.ImageLoadingComplete += new AsyncImageLoadComplete(this.imageResource_ImageLoadingComplete);
        }

        // Once image loading is complete this method is called and the visibility of the
        // two GUIImages is swapped.
        private void imageResource_ImageLoadingComplete(AsyncImageResource image)
        {
            lock (this._LoadingLock)
            {
                if (this._GuiImageOne == null)
                    return;

                if (this._Filename == null)
                {
                    if (this._GuiImageOne != null)
                        this._GuiImageOne.Visible = false;

                    if (this._GuiImageTwo != null)
                        this._GuiImageTwo.Visible = false;

                    return;
                }

                this._GuiImageOne.ResetAnimations();

                if (this._GuiImageTwo != null)
                    this._GuiImageTwo.ResetAnimations();

                // if we have a second backdrop image object, alternate between the two
                if (this._GuiImageTwo != null)
                {
                    if (this._ImageResource.Property.Equals(this._PropertyOne))
                    {
                        this._GuiImageOne.Visible = this._Active;
                        this._GuiImageTwo.Visible = false;
                    }
                    else
                    {
                        this._GuiImageOne.Visible = false;
                        this._GuiImageTwo.Visible = this._Active;
                    }

                    this._ImagesNeedSwapping = true;
                }
                // if no 2nd backdrop control, just update normally
                else
                    this._GuiImageOne.Visible = this._Active;

                if (this._LoadingImage != null)
                    this._LoadingImage.Visible = false;
            }
        }

    }
}
