using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using System.ComponentModel;

namespace MediaPortal.Plugins.WorldWeatherLite.Database
{
    [DBTableAttribute("weatherImage")]
    public class dbWeatherImage : dbTable
    {
        [DBFieldAttribute(FieldName = "enable", Default = "False")]
        [EditorAttribute(typeof(MediaPortal.Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [Description("The image is processed if checked")]
        [Category("Properties")]
        [DefaultValue(false)]
        public bool Enable
        { get; set; }

        [DBFieldAttribute(FieldName = "description", Default = "")]
        [Category("Properties")]
        [Description("Just a text attached to the image on the GUI")]
        public string Description
        { get; set; }

        [DBFieldAttribute(FieldName = "url", Default = "")]
        [DisplayName("URL")]
        [Description("Main URL image - can be any image supported by MediaPortal including animated gif.\r\nSupported UTC DateTime tags: {yyyy} - year, {MM} - month, {dd} - day in the month, {hh} - hour, {mm} - minute")]
        [Category("URL")]
        public string Url
        { get; set; }

        [DBFieldAttribute(FieldName = "urlBackground", Default = "")]
        [DisplayName("URL: Background")]
        [Description("Just another image merged with main image as background")]
        [Category("URL")]
        public string UrlBackground
        { get; set; }

        [DisplayName("URL: Overlay")]
        [DBFieldAttribute(FieldName = "urlOverlay", Default = "")]
        [Description("The same as Background but the image is apllied on top of main image")]
        [Category("URL")]
        public string UrlOverlay
        { get; set; }

        [DisplayName("Multiimage: period")]
        [DBFieldAttribute(FieldName = "period", Default = "15")]
        [Description("Time period between each image (in mins).\r\nRepresents lifetime(cache) of image files including non multiimage.")]
        [Category("Multiimage")]
        [DefaultValue(15)]
        public int Period
        {
            get { return this._Period; }
            set
            {
                if (value < 10)
                    value = 5;
                else if (value < 15)
                    value = 10;
                else if (value < 20)
                    value = 15;
                else if (value < 30)
                    value = 20;
                else if (value < 60)
                    value = 30;
                else if (value < 120)
                    value = 60;
                else if (value < 180)
                    value = 120;
                else
                    value = 180;

                this._Period = value;
            }
        }private int _Period = 15;

        [DisplayName("Multiimage: period safe")]
        [Description("Time period substracted from current UTC time before generating the first image url (in mins)")]
        [DBFieldAttribute(FieldName = "periodSafe", Default = "5")]
        [Category("Multiimage")]
        [DefaultValue(5)]
        public int PeriodSafe
        {
            get { return this._PeriodSafe; }
            set
            {
                if (value < 0)
                    value = 0;

                this._PeriodSafe = value;
            }
        }private int _PeriodSafe = 5;

        [DisplayName("Multiimage: enabled")]
        [Description("When checked the image is considered as multiimage")]
        [DBFieldAttribute(FieldName = "multiimage", Default = "False")]
        [EditorAttribute(typeof(MediaPortal.Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [Category("Multiimage")]
        [DefaultValue(false)]
        public bool MultiImage
        { get; set; }

        [DisplayName("Multiimage: max images")]
        [Description("Maximum number of images to be generated.")]
        [DBFieldAttribute(FieldName = "multiImageMaxImages", Default = "10")]
        [Category("Multiimage")]
        [DefaultValue(10)]
        public int MultiImageMaxImages
        {
            get { return this._MultiImageMaxImages; }
            set
            {
                if (value < 1)
                    this._MultiImageMaxImages = 1;
                else if (value > 50)
                    this._MultiImageMaxImages = 50;
                else
                    this._MultiImageMaxImages = value;
            }
        }private int _MultiImageMaxImages = 10;

        [DisplayName("Multiimage: max total period")]
        [Description("Maximum total time period for images to be generated (in minutes).")]
        [DBFieldAttribute(FieldName = "multiImageMaxPeriod", Default = "60")]
        [Category("Multiimage")]
        [DefaultValue(60)]
        public int MultiImageMaxPeriod
        {
            get { return this._MultiImageMaxPeriod; }
            set
            {
                if (value < 1)
                    this._MultiImageMaxPeriod = 1;
                else if (value > 1440)
                    this._MultiImageMaxPeriod = 1440;
                else
                    this._MultiImageMaxPeriod = value;
            }
        }private int _MultiImageMaxPeriod = 60;

        [DisplayName("GIF override")]
        [Description("Override GIF frame duration.")]
        [DBFieldAttribute(FieldName = "guiGifFrameDurationOverride", Default = "True")]
        [EditorAttribute(typeof(MediaPortal.Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [Category("Frames")]
        [DefaultValue(true)]
        public bool GUIGifFrameDurationOverride
        { get; set; }

        [DisplayName("Frame duration")]
        [Description("Duration of each frame (in millisec)")]
        [DBFieldAttribute(FieldName = "guiMediaImageFrameDuration", Default = "500")]
        [Category("Frames")]
        [DefaultValue(500)]
        public int GUIMediaImageFrameDuration
        { get; set; }

        [DisplayName("Last frame extra duration")]
        [Description("Additional duration to last frame (in millisec)")]
        [DBFieldAttribute(FieldName = "guiMediaImageLastFrameAddTime", Default = "1500")]
        [Category("Frames")]
        [DefaultValue(1500)]
        public int GUIMediaImageLastFrameAddTime
        { get; set; }



        public static List<dbWeatherImage> GetAll()
        {
            List<dbWeatherImage> list = Manager.Get<dbWeatherImage>(null);

            if (list.Count < 11)
            {
                list.ForEach(o => o.Delete());
                list.Clear();

                for (int i = 0; i < 11; i++)
                {
                    dbWeatherImage img = new dbWeatherImage();
                    img.CommitNeeded = true;
                    list.Add(img);
                }

                list[1].Enable = true;
                list[1].Description = "Satellite";
                list[1].Url = "https://api.sat24.com/animated/EU/visual/3/";
                list[1].GUIGifFrameDurationOverride = true;

                list[2].Enable = true;
                list[2].Description = "Infra";
                list[2].Url = "https://api.sat24.com/animated/EU/infraPolair/3/";
                list[2].GUIGifFrameDurationOverride = true;

                list[3].Enable = true;
                list[3].Description = "Rain";
                list[3].Url = "https://api.sat24.com/animated/EU/rainTMC/3/";
                list[3].GUIGifFrameDurationOverride = true;

                list[4].Enable = true;
                list[4].Description = "Rain - CZ";
                list[4].Url = "https://www.in-pocasi.cz/data/chmi_v2/{yyyy}{MM}{dd}_{hh}{mm}_r.png";
                list[4].UrlBackground = "https://www.in-pocasi.cz/media/images/content/mapa-radar.png";
                list[4].MultiImage = true;
                list[4].Period = 10;

                list.ForEach(o => o.Commit());
            }

            return list;
        }

    }
}
