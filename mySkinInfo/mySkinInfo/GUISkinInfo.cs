using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using MediaPortal.Profile;

namespace MediaPortal.Plugins.mySkinInfo
{
    [PluginIcons("MediaPortal.Plugins.mySkinInfo.SkinInfo-enabled.png", "MediaPortal.Plugins.mySkinInfo.SkinInfo-disabled.png")]
    public class GUISkinInfo : GUIWindow, ISetupForm
    {
        #region Constants
        internal const int PLUGIN_ID = 7982;
        internal const string PLUGIN_NAME = "SkinInfo";
        internal const string PLUGIN_TITLE = "My SkinInfo";
        internal const string PLUGIN_AUTHOR = "PBK";
        internal const string PLUGIN_DESCRIPTION = "";

        internal const string TAG_PREFIX = "#SkinInfo.";

        internal const string TAG_DAY_LZ = TAG_PREFIX + "DayLZ";
        internal const string TAG_DAY = TAG_PREFIX + "Day";
        internal const string TAG_MONTH_LZ = TAG_PREFIX + "MonthLZ";
        internal const string TAG_MONTH = TAG_PREFIX + "Month";
        internal const string TAG_YEAR = TAG_PREFIX + "Year";
        internal const string TAG_HOUR_LZ = TAG_PREFIX + "HourLZ";
        internal const string TAG_HOUR = TAG_PREFIX + "Hour";
        internal const string TAG_MINUTE_LZ = TAG_PREFIX + "MinuteLZ";
        internal const string TAG_MINUTE = TAG_PREFIX + "Minute";
        internal const string TAG_SECOND_LZ = TAG_PREFIX + "SecondLZ";
        internal const string TAG_SECOND = TAG_PREFIX + "Second";
        #endregion

        #region Private
        private bool _TimerEnabled;
        private Timer _Timer;
        private StringBuilder _Sb = new StringBuilder();
        #endregion

        #region ctor
        public GUISkinInfo()
        {
        }
        #endregion

        #region Overrides
        protected override void OnPageDestroy(int windowId)
        {
            base.OnPageDestroy(windowId);
        }

        public override bool Init()
        {
            return base.Load(string.Concat(Config.Dir.Skin, "\\", "SkinInfo.xml"));
        }

        public override void PreInit()
        {
            this.init();
            base.PreInit();
        }

        public override void DeInit()
        {
            this.deInit();
            base.DeInit();
        }

        public override int GetID
        {
            get { return (PLUGIN_ID); }
            set { }
        }

        #endregion

        #region ISetupForm
        public string Author()
        {
            return PLUGIN_AUTHOR;
        }

        public bool CanEnable()
        {
            return true;
        }

        public bool DefaultEnabled()
        {
            return true;
        }

        public string Description()
        {
            return PLUGIN_DESCRIPTION;
        }

        public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
        {
            strButtonText = "SkinInfo";
            strButtonImage = string.Empty;
            strButtonImageFocus = string.Empty;
            strPictureImage = "hover_skininfo.png";
            return true;
        }

        public int GetWindowId()
        {
            return PLUGIN_ID;
        }

        public bool HasSetup()
        {
            //return true;
            return false;
        }

        public string PluginName()
        {
            return PLUGIN_NAME;
        }

        public void ShowPlugin()
        {
            this.showPlugin();
        }

        //public bool ShowDefaultHome()
        //{
        //    return true;
        //}

        #endregion


        private void loadSettings()
        {
            using (Settings settings = new MPSettings())
            {
                try
                {
                    this._TimerEnabled = getSettingAsBool(settings, "PropertiesEnabled", true);
                }
                catch
                {
                }
            }
        }

        //private static void setProperty(string strProperty, string strValue)
        //{
        //    GUIPropertyManager.SetProperty(strProperty, !string.IsNullOrEmpty(strValue) ? strValue : string.Empty);
        //}

        private static bool getSettingAsBool(Settings settings, string strName, bool strDefaultValue)
        {
            return getSettingAsBool(settings, PLUGIN_NAME, strName, strDefaultValue);
        }

        private static bool getSettingAsBool(Settings settings, string strSection, string strName, bool strDefaultValue)
        {
            return settings.GetValueAsBool(strSection, strName, strDefaultValue);
        }

        private void showPlugin()
        {
            //SkinInfoSetup skinInfoSetup = new SkinInfoSetup();
            //skinInfoSetup.ShowDialog();
        }

        private void init()
        {
            this.loadSettings();
            this.timerStart();
        }

        private void deInit()
        {
            this.timerStop();
        }

        private void timerStart()
        {
            if (this._TimerEnabled)
            {
                this._Timer = new Timer();
                this._Timer.Interval = 1000;
                this._Timer.Tick += this.cbTimer;
                this._Timer.Start();
            }
        }

        private void timerStop()
        {
            if (this._TimerEnabled && this._Timer != null)
            {
                this._Timer.Stop();
                this._Timer.Dispose();
            }
        }

        private string getLeadZeroString(int iValue)
        {
            this._Sb.Clear();
            this._Sb.Append((char)(48 + (iValue / 10)));
            this._Sb.Append((char)(48 + (iValue % 10)));
            return this._Sb.ToString();
        }

        private void cbTimer(object sender, EventArgs e)
        {
            DateTime dtNow = DateTime.Now;
            GUIPropertyManager.SetProperty(TAG_DAY_LZ, getLeadZeroString(dtNow.Day));
            GUIPropertyManager.SetProperty(TAG_DAY, dtNow.Day.ToString(CultureInfo.InvariantCulture));
            GUIPropertyManager.SetProperty(TAG_MONTH_LZ, getLeadZeroString(dtNow.Month));
            GUIPropertyManager.SetProperty(TAG_MONTH, dtNow.Month.ToString(CultureInfo.InvariantCulture));
            GUIPropertyManager.SetProperty(TAG_YEAR, dtNow.Year.ToString(CultureInfo.InvariantCulture));
            GUIPropertyManager.SetProperty(TAG_HOUR_LZ, getLeadZeroString(dtNow.Hour));
            GUIPropertyManager.SetProperty(TAG_HOUR, dtNow.Hour.ToString(CultureInfo.InvariantCulture));
            GUIPropertyManager.SetProperty(TAG_MINUTE_LZ, getLeadZeroString(dtNow.Minute));
            GUIPropertyManager.SetProperty(TAG_MINUTE, dtNow.Minute.ToString(CultureInfo.InvariantCulture));
            GUIPropertyManager.SetProperty(TAG_SECOND_LZ, getLeadZeroString(dtNow.Second));
            GUIPropertyManager.SetProperty(TAG_SECOND, dtNow.Second.ToString(CultureInfo.InvariantCulture));
        }
    }
}
