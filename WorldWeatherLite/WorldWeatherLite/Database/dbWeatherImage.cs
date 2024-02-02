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
        [Browsable(false)]
        [DBFieldAttribute(FieldName = "idParent", Default = "0")]
        public int ParentID
        { get; set; }

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
        {
            get { return this._Description; }
            set { this._Description = SanityTextValue(value); }
        }private string _Description = string.Empty;

        [DBFieldAttribute(FieldName = "url", Default = "")]
        [DisplayName("URL")]
        [Description("Main URL image - can be any image supported by MediaPortal including animated gif.\r\nSupported UTC DateTime tags: {yyyy} - year, {MM} - month, {dd} - day in the month, {hh} - hour, {mm} - minute")]
        [Category("URL")]
        public string Url
        {
            get { return this._Url; }
            set { this._Url = SanityTextValue(value); }
        }private string _Url = string.Empty;

        [DBFieldAttribute(FieldName = "urlBackground", Default = "")]
        [DisplayName("URL: Background")]
        [Description("Just another image merged with main image as background")]
        [Category("URL")]
        public string UrlBackground
        {
            get { return this._UrlBackground; }
            set { this._UrlBackground = SanityTextValue(value); }
        }private string _UrlBackground = string.Empty;

        [DisplayName("URL: Overlay")]
        [DBFieldAttribute(FieldName = "urlOverlay", Default = "")]
        [Description("The same as Background but the image is apllied on top of main image")]
        [Category("URL")]
        public string UrlOverlay
        {
            get { return this._UrlOverlay; }
            set { this._UrlOverlay = SanityTextValue(value); }
        }private string _UrlOverlay = string.Empty;

        [DisplayName("Period")]
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

        [DisplayName("Period [safe]")]
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

        [DisplayName("Enabled")]
        [Description("When checked the image is considered as multiimage")]
        [DBFieldAttribute(FieldName = "multiimage", Default = "False")]
        [EditorAttribute(typeof(MediaPortal.Pbk.Controls.UIEditor.CheckBoxUIEditor), typeof(System.Drawing.Design.UITypeEditor))]
        [Category("Multiimage")]
        [DefaultValue(false)]
        public bool MultiImage
        { get; set; }

        [DisplayName("Max images")]
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

        [DisplayName("Max total period")]
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

        [DisplayName("Date & time watermark")]
        [Description("Print date & time text into the image.")]
        [DBFieldAttribute(FieldName = "multiImageDateTimeWatermark", Default = "None")]
        [Category("Multiimage")]
        [DefaultValue(DateImeWatermarkEnum.None)]
        public DateImeWatermarkEnum MultiImageDateImeWatermark
        {
            get { return this._MultiImageDateImeWatermark; }
            set { this._MultiImageDateImeWatermark = value; }
        }DateImeWatermarkEnum _MultiImageDateImeWatermark = DateImeWatermarkEnum.None;

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


        public void CopyTo(dbWeatherImage item)
        {
            foreach (System.Reflection.PropertyInfo pi in this.GetType().GetProperties().Where(f => f.Name != "ParentID" && f.GetCustomAttributes(typeof(DBFieldAttribute), false).Length > 0))
                pi.SetValue(item, pi.GetValue(this, null), null);
        }

        public static List<dbWeatherImage> Get(int iIdParent)
        {
            List<dbWeatherImage> list = Manager.Get<dbWeatherImage>(new BaseCriteria(DBField.GetFieldByDBName(typeof(dbWeatherImage), "idParent"), "=", iIdParent));

            if (list.Count < 11)
            {
                list.ForEach(o => o.Delete());
                list.Clear();

                for (int i = 0; i < 11; i++)
                {
                    dbWeatherImage img = new dbWeatherImage();
                    list.Add(img);
                }

                //Copy default values from first profile
                List<dbWeatherImage> listDefault = Manager.Get<dbWeatherImage>(new BaseCriteria(DBField.GetFieldByDBName(typeof(dbWeatherImage), "idParent"), "=", 1));
                if (listDefault.Count == list.Count)
                {
                    for (int i = 0; i < list.Count; i++)
                        listDefault[i].CopyTo(list[i]);
                }
                else
                {
                    InitDefaultWorld(list[0]);
                    InitDefaultSatellite(list[1]);
                    InitDefaultInfra(list[2]);

                    dbWeatherImage im = list[3];
                    im.Enable = true;
                    im.Description = "Rain - CZ";
                    im.Url = "https://www.in-pocasi.cz/data/chmi_v2/{yyyy}{MM}{dd}_{hh}{mm}_r.png";
                    im.UrlBackground = "https://www.in-pocasi.cz/media/images/content/mapa-radar.png";
                    im.MultiImage = true;
                    im.Period = 10;
                }
                
            }

            return list;
        }

        public static void InitDefaultWorld(dbWeatherImage im)
        {
            im.Enable = true;
            im.Description = "World";
            im.Url = "https://worldweather.wmo.int/cloud/graphic/colorMap-cloud.png";
            im.UrlBackground = string.Empty;
            im.UrlOverlay = string.Empty;
            im.Period = 15;
            im.MultiImage = false;
        }

        public static void InitDefaultSatellite(dbWeatherImage im)
        {
            im.Enable = true;
            im.Description = "Satellite";
            im.Url = "https://imn-api.meteoplaza.com/v4/nowcast/tiles/satellite-europe/{yyyy}{MM}{dd}{hh}{mm}/5/8/14/14/20?outputtype=jpeg";
            im.UrlBackground = string.Empty;
            im.UrlOverlay = "https://maptiler.infoplaza.io/api/maps/Border/static/11.22,48.95,4.02/1560x1560.png?attribution=false";
            im.Period = 15;
            im.PeriodSafe = 20;
            im.MultiImage = true;
            im.MultiImageDateImeWatermark = DateImeWatermarkEnum.UpperLeft;
        }

        public static void InitDefaultInfra(dbWeatherImage im)
        {
            im.Enable = true;
            im.Description = "Infra";
            im.Url = "https://imn-api.meteoplaza.com/v4/nowcast/tiles/satellite-europe-infrared/{yyyy}{MM}{dd}{hh}{mm}/5/8/14/14/20?outputtype=jpeg";
            im.UrlBackground = string.Empty;
            im.UrlOverlay = "https://maptiler.infoplaza.io/api/maps/Border/static/11.22,48.95,4.02/1560x1560.png?attribution=false";
            im.Period = 15;
            im.PeriodSafe = 20;
            im.MultiImage = true;
            im.MultiImageDateImeWatermark = DateImeWatermarkEnum.UpperLeft;
        }

        

    }
}
