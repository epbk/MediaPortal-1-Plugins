using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using NLog;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using System.Threading;
using System.Globalization;

namespace MediaPortal.Pbk.Cornerstone.Database.Tables
{
    [DBTableAttribute("settings")]
    public class DBSetting : DatabaseTable
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        public DBSetting() :
            base()
        {
        }

        public bool UpdatingFromObject
        {
            get { return this._UpdatingFromObject; }
        } private bool _UpdatingFromObject = false;

        public bool ManagerModifyingValue
        {
            get { return this._ManagerModifyingValue; }
            set { this._ManagerModifyingValue = value; }
        } private bool _ManagerModifyingValue = false;


        public SettingsManager SettingsManager
        {
            get { return this._SettingsManager; }
            set { this._SettingsManager = value; }
        } private SettingsManager _SettingsManager;

        public override void AfterDelete()
        {
        }

        #region Database Fields
        // The unique string id of the given setting.
        [DBFieldAttribute]
        public string Key
        {
            get { return this._Key; }

            set
            {
                this._Key = value;
                this._CommitNeeded = true;
            }
        } private string _Key;

        // The name of the given setting.
        [DBFieldAttribute]
        public string Name
        {
            get { return this._Name; }

            set
            {
                this._Name = value;
                this._CommitNeeded = true;
            }
        } private string _Name;

        // The description of the given setting.
        [DBFieldAttribute]
        public string Description
        {
            get { return this._Description; }

            set
            {
                this._Description = value;
                this._CommitNeeded = true;
            }
        } private string _Description;

        // A link to more information about this setting.
        [DBFieldAttribute]
        public string MoreInfoLink
        {
            get { return this._MoreInfoLink; }

            set
            {
                this._MoreInfoLink = value;
                this._CommitNeeded = true;
            }
        } private string _MoreInfoLink;

        [DBFieldAttribute]
        public StringList Grouping
        {

            get { return this._Grouping; }
            set
            {
                this._Grouping = value;
                this._CommitNeeded = true;
            }

        } private StringList _Grouping;


        [DBFieldAttribute(FieldName = "value")]
        public string StringValue
        {
            get { return this._Value; }

            set
            {
                this._Value = value;
                this._CommitNeeded = true;
            }
        } private string _Value;


        // The type of data in Value. Should be INT, FLOAT, BOOL, or STRING.
        [DBFieldAttribute]
        public string Type
        {
            get { return this._Type; }

            set
            {
                if (value != "INT" && value != "FLOAT" && value != "BOOL" && value != "STRING")
                    return;
                this._Type = value;
                this._CommitNeeded = true;
            }
        } private string _Type;

        #endregion

        public bool Hidden { get; set; }
        public bool Sensitive { get; set; }

        public object Value
        {
            get
            {
                try
                {
                    if (this.Type == "INT")
                        return int.Parse(this.StringValue);

                    if (this.Type == "UINT")
                        return uint.Parse(this.StringValue);

                    if (this.Type == "LONG")
                        return long.Parse(this.StringValue);

                    if (this.Type == "ULONG")
                        return ulong.Parse(this.StringValue);

                    if (this.Type == "FLOAT")
                        return float.Parse(this.StringValue, new CultureInfo("en-US", false));

                    if (this.Type == "DOUBLE")
                        return double.Parse(this.StringValue, new CultureInfo("en-US", false));

                    if (this.Type == "BOOL")
                        return bool.Parse(this.StringValue);

                    if (this.Type == "STRING")
                        return this.StringValue;

                    if (this.Type.StartsWith("ENUM."))
                    {
                        Type tEnum = System.Type.GetType(this.Type.Substring(5));
                        return Enum.Parse(tEnum, this.StringValue);
                    }

                    if (this.Type.StartsWith("OBJECT."))
                    {
                        Type tObject = System.Type.GetType(this.Type.Substring(7));
                        return Newtonsoft.Json.JsonConvert.DeserializeObject(this.StringValue, tObject);
                    }
                }
                catch (Exception e)
                {
                    if (e.GetType() == typeof(ThreadAbortException))
                        throw e;
                    _Logger.ErrorException("Error parsing Settings Value: ", e);
                }

                return null;
            }

            set
            {
                if (this._UpdatingFromObject)
                    return;

                if (this.Type == "FLOAT")
                    this.StringValue = ((float)value).ToString(new CultureInfo("en-US", false));
                else if (this.Type == "DOUBLE")
                    this.StringValue = ((double)value).ToString(new CultureInfo("en-US", false));
                else if (this.Type.StartsWith("OBJECT."))
                    this.StringValue = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                else
                    this.StringValue = value.ToString();

                this._UpdatingFromObject = true;

                if (this.SettingsManager != null)
                    this.SettingsManager.OnSettingChanged(_Key);

                this._UpdatingFromObject = false;

            }
        }

        public bool Validate(string strValue)
        {
            try
            {
                if (this.Type == "INT")
                {
                    int.Parse(strValue);
                    return true;
                }

                if (this.Type == "UINT")
                {
                    uint.Parse(strValue);
                    return true;
                }

                if (this.Type == "LONG")
                {
                    long.Parse(strValue);
                    return true;
                }

                if (this.Type == "ULONG")
                {
                    ulong.Parse(strValue);
                    return true;
                }

                if (this.Type == "FLOAT")
                {
                    float.Parse(strValue, new CultureInfo("en-US", false));
                    return true;
                }

                if (this.Type == "DOUBLE")
                {
                    double.Parse(strValue, new CultureInfo("en-US", false));
                    return true;
                }

                if (this.Type == "BOOL")
                {
                    bool.Parse(strValue);
                    return true;
                }

                if (this.Type == "STRING")
                    return true;

                if (this.Type.StartsWith("ENUM."))
                {
                    Type tEnum = System.Type.GetType(this.Type.Substring(5));
                    Enum.Parse(tEnum, this.StringValue);
                    return true;
                }

                if (this.Type.StartsWith("OBJECT."))
                {
                    Type tObject = System.Type.GetType(this.Type.Substring(7));
                    Newtonsoft.Json.JsonConvert.DeserializeObject(this.StringValue, tObject);
                    return true;
                }
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(ThreadAbortException))
                    throw e;
                return false;
            }

            _Logger.Warn("[Validate] Unknown Setting Type (" + this.Type + "), can not validate...");
            return false;
        }

        public static string TypeLookup(Type type)
        {
            if (type == typeof(int))
                return "INT";
            else if (type == typeof(uint))
                return "UINT";
            else if (type == typeof(long))
                return "LONG";
            else if (type == typeof(ulong))
                return "ULONG";
            else if (type == typeof(float))
                return "FLOAT";
            else if (type == typeof(double))
                return "DOUBLE";
            else if (type == typeof(bool))
                return "BOOL";
            else if (type == typeof(string))
                return "STRING";
            else if (type.IsEnum)
                return "ENUM." + type.FullName;
            else if (type.IsClass)
                return "OBJECT." + type.FullName;

            return null;
        }

        public override string ToString()
        {
            if (this.Sensitive)
                return "DBSetting: " + this.Name + " = \"*****\"";
            else
                return "DBSetting: " + this.Name + " = \"" + this.Value.ToString() + "\"";
        }
    }
}
