using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using NLog;
using System.Collections.ObjectModel;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using System.Globalization;
using System.Threading;

namespace MediaPortal.Pbk.Cornerstone.Database
{
    public class DBField
    {
        public enum DBDataType { INTEGER, REAL, TEXT, STRING_OBJECT, BOOL, TYPE, ENUM, DATE_TIME, DB_OBJECT, DB_FIELD, DB_RELATION, LONG }

        #region Private Variables

        private PropertyInfo _PropertyInfo;
        private DBFieldAttribute _Attribute;
        private DBDataType _Type;

        private static Dictionary<Type, List<DBField>> _FieldLists;
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        #endregion

        #region Constructors
        private DBField(PropertyInfo propertyInfo, DBFieldAttribute attribute)
        {
            this._PropertyInfo = propertyInfo;
            this._Attribute = attribute;

            // determine how this shoudl be stored in the DB
            this._Type = DBDataType.TEXT;

            if (propertyInfo.PropertyType == typeof(string))
                this._Type = DBDataType.TEXT;
            else if (propertyInfo.PropertyType == typeof(int))
                this._Type = DBDataType.INTEGER;
            else if (propertyInfo.PropertyType == typeof(int?))
                this._Type = DBDataType.INTEGER;
            else if (propertyInfo.PropertyType == typeof(long))
                this._Type = DBDataType.LONG;
            else if (propertyInfo.PropertyType == typeof(long?))
                this._Type = DBDataType.LONG;
            else if (propertyInfo.PropertyType == typeof(float))
                this._Type = DBDataType.REAL;
            else if (propertyInfo.PropertyType == typeof(float?))
                this._Type = DBDataType.REAL;
            else if (propertyInfo.PropertyType == typeof(double))
                this._Type = DBDataType.REAL;
            else if (propertyInfo.PropertyType == typeof(double?))
                this._Type = DBDataType.REAL;
            else if (propertyInfo.PropertyType == typeof(bool))
                this._Type = DBDataType.BOOL;
            else if (propertyInfo.PropertyType == typeof(bool?))
                this._Type = DBDataType.BOOL;
            else if (propertyInfo.PropertyType == typeof(Boolean))
                this._Type = DBDataType.BOOL;
            else if (propertyInfo.PropertyType == typeof(DateTime))
                this._Type = DBDataType.DATE_TIME;
            else if (propertyInfo.PropertyType == typeof(DateTime?))
                this._Type = DBDataType.DATE_TIME;
            else if (propertyInfo.PropertyType == typeof(Type))
                this._Type = DBDataType.TYPE;
            else if (propertyInfo.PropertyType.IsEnum)
                this._Type = DBDataType.ENUM;
            // nullable enum
            else if (Nullable.GetUnderlyingType(propertyInfo.PropertyType) != null ? Nullable.GetUnderlyingType(propertyInfo.PropertyType).IsEnum : false)
                this._Type = DBDataType.ENUM;
            else if (DatabaseManager.IsDatabaseTableType(propertyInfo.PropertyType))
                this._Type = DBDataType.DB_OBJECT;
            else if (propertyInfo.PropertyType == typeof(DBField))
                this._Type = DBDataType.DB_FIELD;
            else if (propertyInfo.PropertyType == typeof(DBRelation))
                this._Type = DBDataType.DB_RELATION;
            else
            {
                // check for string object types
                foreach (Type currInterface in propertyInfo.PropertyType.GetInterfaces())
                    if (currInterface == typeof(IStringSourcedObject))
                    {
                        this._Type = DBDataType.STRING_OBJECT;
                        return;
                    }
            }
        }

        static DBField()
        {
            _FieldLists = new Dictionary<Type, List<DBField>>();
        }

        #endregion

        #region Public Properties
        // Returns the name of this attribute.
        public string Name
        {
            get { return this._PropertyInfo.Name; }
        }

        public string FriendlyName
        {
            get
            {
                if (this._FriendlyName == null)
                    this._FriendlyName = DBField.MakeFriendlyName(Name);

                return this._FriendlyName;
            }
        } private string _FriendlyName = null;

        // Returns the name of this field in the database. Generally the same as Name,
        // but this is not gauranteed.
        public string FieldName
        {
            get
            {
                if (this._Attribute.FieldName == string.Empty)
                    return this.Name.ToLower();
                else
                    return this._Attribute.FieldName;
            }
        }

        // Returns the Type of database object this field belongs to.
        public Type OwnerType
        {
            get
            {
                return this._PropertyInfo.DeclaringType;
            }
        }

        // Returns the type the field will be stored as in the database.
        public DBDataType DBType
        {
            get { return this._Type; }
        }

        // Returns the C# type for the field.
        public Type Type
        {
            get { return this._PropertyInfo.PropertyType; }
        }

        public bool IsNullable
        {
            get
            {
                if (Type == typeof(StringList))
                    return false;

                if (!Type.IsValueType)
                    return true;

                if (Nullable.GetUnderlyingType(Type) != null)
                    return true; // Nullable<T>

                return false;
            }
        }

        // Returns the default value for the field. Currently always returns in type string.
        public object Default
        {
            get
            {
                if (this._Attribute.Default == null)
                    return null;

                switch (DBType)
                {
                    case DBDataType.INTEGER:
                        if (this._Attribute.Default == "")
                            return 0;
                        else
                            return int.Parse(this._Attribute.Default);

                    case DBDataType.LONG:
                        if (this._Attribute.Default == "")
                            return 0;
                        else
                            return long.Parse(this._Attribute.Default);

                    case DBDataType.REAL:
                        if (this._Attribute.Default == "")
                            return (float)0.0;
                        else
                            return float.Parse(this._Attribute.Default, new CultureInfo("en-US", false));

                    case DBDataType.BOOL:
                        if (this._Attribute.Default == "")
                            return false;
                        else
                            return this._Attribute.Default.ToLower() == "true" || this._Attribute.Default.ToString() == "1";

                    case DBDataType.DATE_TIME:
                        if (this._Attribute.Default == "")
                            return DateTime.Now;
                        else
                        {
                            try
                            {
                                return DateTime.Parse(this._Attribute.Default);
                            }
                            catch { }
                        }
                        return DateTime.Now;
                    case DBDataType.STRING_OBJECT:
                        IStringSourcedObject newObj = (IStringSourcedObject)this._PropertyInfo.PropertyType.GetConstructor(System.Type.EmptyTypes).Invoke(null);
                        newObj.LoadFromString(this._Attribute.Default);
                        return newObj;

                    case DBDataType.DB_OBJECT:
                        if (this._PropertyInfo.PropertyType == typeof(DatabaseTable))
                            return null;

                        DatabaseTable newDBObj = (DatabaseTable)this._PropertyInfo.PropertyType.GetConstructor(System.Type.EmptyTypes).Invoke(null);
                        return newDBObj;

                    default:
                        if (this._Attribute.Default == "")
                            return " ";
                        else
                            return this._Attribute.Default;
                }
            }
        }

        // Returns true if this field should be updated when pulling updated data in from
        // an external source. 
        public bool AutoUpdate
        {
            get { return this._Attribute.AllowAutoUpdate; }
        }

        // Returns true if this field should be available to the user for filtering purposes.
        public bool Filterable
        {
            get { return this._Attribute.Filterable; }
        }

        // returns true if the user should be able to manually enter a value for filtering
        public bool AllowManualFilterInput
        {
            get
            {
                if (this.Type == typeof(bool))
                    return false;

                return this._Attribute.AllowManualFilterInput;
            }
        }

        // returns true if dynamic filtering nodes are permissible for this field
        public bool AllowDynamicFiltering
        {
            get { return this._Attribute.AllowDynamicFiltering; }
        }

        #endregion

        #region Public Methods
        // Sets the value of this field for the given object.
        public void SetValue(DatabaseTable owner, object value)
        {
            try
            {

                // if we were passed a null value, try to set that. 
                if (value == null)
                {
                    this._PropertyInfo.GetSetMethod().Invoke(owner, new object[] { null });
                    return;
                }

                // if we were passed a matching object, just set it
                if (value.GetType() == this._PropertyInfo.PropertyType)
                {
                    this._PropertyInfo.GetSetMethod().Invoke(owner, new object[] { value });
                    return;
                }

                if (value is string)
                    this._PropertyInfo.GetSetMethod().Invoke(owner, new object[] { ConvertString(owner.DBManager, (string)value) });


            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(ThreadAbortException))
                    throw e;

                _Logger.Error("[SetValue] Error writing to " + owner.GetType().Name + "." + this.Name +
                                " Property: " + e.Message);
            }
        }

        // Returns the value of this field for the given object.
        public object GetValue(DatabaseTable owner)
        {
            try
            {
                return this._PropertyInfo.GetGetMethod().Invoke(owner, null);
            }
            catch (Exception)
            {
                throw new Exception("[GetValue] DBField does not belong to the Type of the supplied Owner.");
            }
        }

        public object ConvertString(DatabaseManager dbManager, string strVal)
        {
            try
            {
                if (string.IsNullOrEmpty(strVal.Trim()) && IsNullable)
                    return null;

                switch (DBType)
                {
                    case DBDataType.INTEGER:
                        string strTmp = strVal.ToString();
                        while (strTmp.Contains(","))
                            strTmp = strTmp.Remove(strTmp.IndexOf(','), 1);

                        return int.Parse(strTmp);

                    case DBDataType.LONG:
                        strTmp = strVal.ToString();
                        while (strTmp.Contains(","))
                            strTmp = strTmp.Remove(strTmp.IndexOf(','), 1);

                        return long.Parse(strTmp);

                    case DBDataType.REAL:
                        if (this._PropertyInfo.PropertyType == typeof(double))
                            return double.Parse(strVal, new CultureInfo("en-US", false));
                        else
                            return float.Parse(strVal, new CultureInfo("en-US", false));

                    case DBDataType.BOOL:
                        return (strVal.ToString() == "true" || strVal.ToString() == "1");

                    case DBDataType.STRING_OBJECT:
                        // create a new object and populate it
                        IStringSourcedObject newObj = (IStringSourcedObject)this._PropertyInfo.PropertyType.GetConstructor(System.Type.EmptyTypes).Invoke(null);
                        newObj.LoadFromString(strVal);
                        return newObj;

                    case DBDataType.TYPE:
                        return Type.GetType(strVal);

                    case DBDataType.ENUM:
                        if (strVal.Trim().Length != 0)
                        {
                            Type enumType = this._PropertyInfo.PropertyType;
                            if (Nullable.GetUnderlyingType(enumType) != null)
                                enumType = Nullable.GetUnderlyingType(enumType);

                            return Enum.Parse(enumType, strVal);
                        }
                        break;

                    case DBDataType.DATE_TIME:
                        DateTime newDateTimeObj = DateTime.Now;
                        if (strVal.Trim().Length != 0)
                            try
                            {
                                newDateTimeObj = DateTime.Parse(strVal);
                            }
                            catch { }

                        return newDateTimeObj;

                    case DBDataType.DB_OBJECT:
                        if (strVal.Trim().Length == 0)
                            return null;

                        string[] objectValues = strVal.Split(new string[] { "|||" }, StringSplitOptions.None);
                        if (objectValues.Length > 1)
                        {
                            return dbManager.Get(Type.GetType(objectValues[1]), int.Parse(objectValues[0]));
                        }
                        else
                            return dbManager.Get(this._PropertyInfo.PropertyType, int.Parse(strVal));

                    case DBDataType.DB_FIELD:
                        string[] fieldValues = strVal.Split(new string[] { "|||" }, StringSplitOptions.None);
                        if (fieldValues.Length != 2)
                            break;

                        return DBField.GetFieldByDBName(Type.GetType(fieldValues[0]), fieldValues[1]);

                    case DBDataType.DB_RELATION:
                        string[] relationValues = strVal.Split(new string[] { "|||" }, StringSplitOptions.None);
                        if (relationValues.Length != 3)
                            break;

                        return DBRelation.GetRelation(Type.GetType(relationValues[0]),
                                                      Type.GetType(relationValues[1]),
                                                      relationValues[2]);

                    default:
                        return strVal;
                }
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(ThreadAbortException))
                    throw e;

                _Logger.Error("[ConvertString] Error parsing " + this._PropertyInfo.DeclaringType.Name + "." + this.Name +
                                " Property: " + e.Message);
            }

            return null;
        }

        // sets the default value based on the datatype.
        public void InitializeValue(DatabaseTable owner)
        {
            this.SetValue(owner, Default);
        }

        public override string ToString()
        {
            return this.FriendlyName;
        }

        #endregion

        #region Public Static Methods

        // Returns the list of DBFields for the given type. Developer should normally
        // directly use the properties of the class, but this allows for iteration.
        public static ReadOnlyCollection<DBField> GetFieldList(Type tableType)
        {
            if (tableType == null || !DatabaseManager.IsDatabaseTableType(tableType))
                return new List<DBField>().AsReadOnly();

            if (!_FieldLists.ContainsKey(tableType))
            {
                List<DBField> newFieldList = new List<DBField>();

                // loop through each property in the class
                PropertyInfo[] propertyArray = tableType.GetProperties();
                foreach (PropertyInfo currProperty in propertyArray)
                {
                    object[] customAttrArray = currProperty.GetCustomAttributes(true);
                    // for each property, loop through it's custom attributes
                    // if one of them is ours, store the property info for later use
                    foreach (object currAttr in customAttrArray)
                    {
                        if (currAttr.GetType() == typeof(DBFieldAttribute))
                        {
                            DBField newField = new DBField(currProperty, (DBFieldAttribute)currAttr);
                            newFieldList.Add(newField);
                            break;
                        }
                    }
                }

                _FieldLists[tableType] = newFieldList;
            }

            return _FieldLists[tableType].AsReadOnly();
        }

        // Returns the DBField with the specified name for the specified table.
        public static DBField GetField(Type tableType, string strFieldName)
        {
            if (tableType == null)
            {
                return null;
            }

            ReadOnlyCollection<DBField> fieldList = GetFieldList(tableType);
            foreach (DBField currField in fieldList)
            {
                if (currField.Name.Equals(strFieldName))
                    return currField;
            }

            return null;
        }

        // Returns the DBField with the specified name for the specified table.
        public static DBField GetFieldByDBName(Type tableType, string strFieldName)
        {
            if (tableType == null)
            {
                return null;
            }

            ReadOnlyCollection<DBField> fieldList = GetFieldList(tableType);
            foreach (DBField currField in fieldList)
            {
                if (currField.FieldName.Equals(strFieldName))
                    return currField;
            }

            return null;
        }

        public static string MakeFriendlyName(string strInput)
        {
            string strFriendlyName = "";

            char cPrevChar = char.MinValue;
            foreach (char cCurrChar in strInput)
            {
                if (cPrevChar != char.MinValue && char.IsLower(cPrevChar) && char.IsUpper(cCurrChar))
                    strFriendlyName += " ";

                strFriendlyName += cCurrChar;
                cPrevChar = cCurrChar;
            }

            return strFriendlyName;
        }

        #endregion
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DBFieldAttribute : System.Attribute
    {
        #region Private
        private string _FieldName = string.Empty;
        private string _Description = string.Empty;
        private string _DefaultValue = string.Empty;
        private bool _AllowAutoUpdate = true;
        private bool _Filterable = true;
        private bool _AllowManualFilterInput = true;
        private bool _AllowDynamicFiltering = true;
        #endregion

        #region Properties
        // if unassigned, the name of the parameter should be used for the field name
        public string FieldName
        {
            get { return this._FieldName; }
            set { this._FieldName = value; }
        }

        public string Default
        {
            get { return this._DefaultValue; }
            set { this._DefaultValue = value; }
        }

        public bool AllowAutoUpdate
        {
            get { return this._AllowAutoUpdate; }
            set { this._AllowAutoUpdate = value; }
        }

        public bool Filterable
        {
            get { return this._Filterable; }
            set { this._Filterable = value; }
        }

        public bool AllowManualFilterInput
        {
            get { return this._AllowManualFilterInput; }
            set { this._AllowManualFilterInput = value; }
        }

        public bool AllowDynamicFiltering
        {
            get { return this._AllowDynamicFiltering; }
            set { this._AllowDynamicFiltering = value; }
        }

        #endregion

        public DBFieldAttribute()
        {
        }
    }
}
