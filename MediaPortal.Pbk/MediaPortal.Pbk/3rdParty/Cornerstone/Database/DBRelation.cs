using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Collections;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;

namespace MediaPortal.Pbk.Cornerstone.Database
{
    public class DBRelation
    {
        #region Private

        private static Dictionary<Type, List<DBRelation>> _Relations;
        private MethodInfo _GetRelationListMethod;
        private PropertyInfo _PropertyInfo;

        #endregion

        #region Properties

        /// <summary>
        /// The primary DatabaseTable type that hosts this relation. This is the
        /// one in the one-to-many relationship.
        /// </summary>
        public Type PrimaryType
        {
            get { return this._PrimaryType; }
        } private Type _PrimaryType;

        /// <summary>
        /// The secondary DatabaseTable type that links to the primary type. This
        /// is the many in the one-to-many relationship.
        /// </summary>
        public Type SecondaryType
        {
            get { return this._SecondaryType; }
        } private Type _SecondaryType;

        public string PrimaryColumnName
        {
            get
            {
                if (this._PrimaryColumnName == null)
                {
                    if (this.PrimaryType == this.SecondaryType)
                        this._PrimaryColumnName = DatabaseManager.GetTableName(this.PrimaryType) + "1_id";
                    else
                        this._PrimaryColumnName = DatabaseManager.GetTableName(this.PrimaryType) + "_id";
                }

                return this._PrimaryColumnName;
            }
        } private string _PrimaryColumnName = null;

        public string SecondaryColumnName
        {
            get
            {
                if (this._SecondaryColumnName == null)
                {
                    if (this.PrimaryType == this.SecondaryType)
                        this._SecondaryColumnName = DatabaseManager.GetTableName(SecondaryType) + "2_id";
                    else
                        this._SecondaryColumnName = DatabaseManager.GetTableName(SecondaryType) + "_id";
                }

                return this._SecondaryColumnName;
            }
        } private string _SecondaryColumnName = null;



        /// <summary>
        /// The optional unique identifier for this relationship. Without a unique ID,
        /// relations gcan go both ways if defined from each type.
        /// </summary>
        public string Identifier
        {
            get { return this._Identifier; }
        } private string _Identifier;

        /// <summary>
        /// If true, this relation will automatically be retrieved when the owner is retrieved.
        /// </summary>
        public bool AutoRetrieve
        {
            get { return this._AutoRetrieve; }
        } private bool _AutoRetrieve;

        public bool Filterable
        {
            get { return this._Filterable; }
        } private bool _Filterable;

        /// <summary>
        /// The name of the table this relationship data is stored in.
        /// </summary>
        public string TableName
        {
            get
            {
                if (this._TableName == null)
                {
                    List<string> names = new List<string>();
                    names.Add(DatabaseManager.GetTableName(this.PrimaryType));
                    names.Add(DatabaseManager.GetTableName(this.SecondaryType));
                    names.Sort();

                    this._TableName = names[0] + "__" + names[1];

                    if (this.Identifier != null && this.Identifier.Trim().Length > 0)
                    {
                        this._TableName += "__" + this.Identifier;
                    }
                }

                return this._TableName;
            }
        } private string _TableName = null;

        #endregion

        #region Public Methods

        public IRelationList GetRelationList(DatabaseTable dbObject)
        {
            return (IRelationList)this._GetRelationListMethod.Invoke(dbObject, null);
        }

        #endregion

        #region Public Static Methods

        public static ReadOnlyCollection<DBRelation> GetRelations(Type primaryType)
        {
            loadRelations(primaryType);
            return _Relations[primaryType].AsReadOnly();
        }

        public static DBRelation GetRelation(Type primaryType, Type secondaryType, string strIdentifier)
        {
            loadRelations(primaryType);
            foreach (DBRelation currRelation in _Relations[primaryType])
            {
                if (currRelation.SecondaryType == secondaryType && currRelation.Identifier.Equals(strIdentifier))
                    return currRelation;
            }

            return null;
        }

        #endregion

        #region Private Methods

        static DBRelation()
        {
            _Relations = new Dictionary<Type, List<DBRelation>>();
        }

        private DBRelation()
        {
        }

        private static void loadRelations(Type primaryType)
        {
            if (_Relations.ContainsKey(primaryType))
                return;

            List<DBRelation> newRelations = new List<DBRelation>();

            foreach (PropertyInfo currProperty in primaryType.GetProperties())
                foreach (object currAttr in currProperty.GetCustomAttributes(true))
                {
                    // if we have come to a relation property, lets process it
                    if (currAttr.GetType() == typeof(DBRelationAttribute))
                    {
                        DBRelation newRelation = new DBRelation();
                        newRelation._PrimaryType = primaryType;
                        newRelation._SecondaryType = currProperty.PropertyType.GetGenericArguments()[1];
                        newRelation._Identifier = ((DBRelationAttribute)currAttr).Identifier;
                        newRelation._PropertyInfo = currProperty;
                        newRelation._AutoRetrieve = ((DBRelationAttribute)currAttr).AutoRetrieve;
                        newRelation._Filterable = ((DBRelationAttribute)currAttr).Filterable;
                        newRelation._GetRelationListMethod = currProperty.GetGetMethod();

                        newRelations.Add(newRelation);
                    }
                }

            _Relations[primaryType] = newRelations;
        }

        #endregion

    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DBRelationAttribute : System.Attribute
    {
        // DBRelations work two way. If you multiple or single directional relations are needed 
        // a unique identifier should be supplied. 
        public string Identifier
        {
            get { return this._Identifier; }
            set { this._Identifier = value; }
        } string _Identifier = "";

        public bool AutoRetrieve
        {
            get { return this._AutoRetrieve; }
            set { this._AutoRetrieve = value; }
        } private bool _AutoRetrieve = false;

        public bool OneWay
        {
            get { return this._OneWay; }
            set { this._OneWay = value; }
        } private bool _OneWay = false;

        public bool Filterable
        {
            get { return this._Filterable; }
            set { this._Filterable = value; }
        } private bool _Filterable = true;
    }
}
