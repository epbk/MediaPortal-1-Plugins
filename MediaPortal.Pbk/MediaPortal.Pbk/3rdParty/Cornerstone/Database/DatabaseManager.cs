using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using NLog;
using SQLite.NET;

namespace MediaPortal.Pbk.Cornerstone.Database
{
    public class DatabaseManager
    {

        #region Private

        private string _DbFilename;
        private string _DbBackupDirectory;
        private DatabaseCache _Cache;
        private Dictionary<Type, bool> _IsVerified;
        private Dictionary<Type, bool> _DoneFullRetrieve;

        private Dictionary<Type, IDynamicFilterHelper> _FilterHelperLookup;

        private HashSet<Type> _Preloading;

        private bool _TransactionInProgress = false;

        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        private readonly object _LockObject = new object();

        private SQLiteClient _DbClient
        {
            get
            {
                lock (this._LockObject)
                {
                    if (!this._Connected)
                    {
                        try
                        {
                            this._SqliteClient = new SQLiteClient(this._DbFilename);
                            // verify integrity of database before going any further
                            if (this.verifyIntegrity(this._SqliteClient))
                            {
                                this.backupDatabase();
                            }
                            else
                            {
                                this.restoreDatabase();
                            }
                            this._SqliteClient.Execute("PRAGMA synchronous=OFF");
                            _Logger.Info("[{0}][_DbClient] Successfully Opened Database: {1}", this._InstanceId, this._DbFilename);
                            this._Connected = true;
                        }
                        catch (Exception e)
                        {
                            _Logger.FatalException(string.Format("[{0}][_DbClient] Could Not Open Database: {1}", this._InstanceId, this._DbFilename), e);
                            this._SqliteClient = null;
                        }
                    }
                }

                return this._SqliteClient;
            }
        } private SQLiteClient _SqliteClient; private bool _Connected = false;

        private static int _InstanceCounter = -1;

        private int _InstanceId = -1;

        #endregion

        #region Events

        public delegate void ObjectAffectedDelegate(DatabaseTable obj);
        public delegate void ObjectUpdatedDelegate(DatabaseTable obj, TableUpdateInfo updateInfo);

        public event ObjectAffectedDelegate ObjectInserted;
        public event ObjectAffectedDelegate ObjectDeleted;
        public event ObjectAffectedDelegate ObjectUpdated;
        public event ObjectUpdatedDelegate ObjectUpdatedEx;

        #endregion

        #region ctor

        static DatabaseManager()
        {
            Logging.Log.Init();
        }

        /// <summary>
        /// Creates a new DatabaseManager based on the given filename.
        /// </summary>
        /// <param name="strDbFilename">Filename of database</param>
        public DatabaseManager(string strDbFilename)
            : this(strDbFilename, null)
        {
        }

        /// <summary>
        /// Creates a new DatabaseManager based on the given filename.
        /// </summary>
        /// <param name="strDbFilename">Filename of database</param>
        /// <param name="strDbBackupDirectory">Optionally specify location to store backups</param>
        public DatabaseManager(string strDbFilename, string strDbBackupDirectory = null)
        {
            this._InstanceId = System.Threading.Interlocked.Increment(ref _InstanceCounter);

            this._DbFilename = strDbFilename;
            this._DbBackupDirectory = strDbBackupDirectory;

            this._IsVerified = new Dictionary<Type, bool>();
            this._DoneFullRetrieve = new Dictionary<Type, bool>();
            this._Preloading = new HashSet<Type>();
            this._FilterHelperLookup = new Dictionary<Type, IDynamicFilterHelper>();

            this._Cache = new DatabaseCache();
        }

        #endregion

        #region Public Methods

        public bool IsClosed()
        {
            return _Connected == false;
        }

        public void Close()
        {
            lock (this._LockObject)
            {
                if (!this._Connected)
                    return;

                _Logger.Info("[{0}][Close] Closing database connection...", this._InstanceId);
                try
                {
                    this._SqliteClient.Close();
                    this._SqliteClient.Dispose();
                    _Logger.Info("[{1}][Close] Successfully closed Database: {0}", this._DbFilename, this._InstanceId);
                    this._Connected = false;
                }
                catch (Exception e)
                {
                    _Logger.ErrorException(string.Format("[{0}][Close] Failed closing Database: {1}", this._InstanceId, this._DbFilename), e);
                }
            }
        }



        /// <summary>
        /// Quickly retrieves all items in the database of the specified type and loads them into
        /// memory. This will provide much faster access time for some configurations.
        /// </summary>
        /// <param name="tableType"></param>
        public void PreLoad(Type tableType)
        {
            this._Preloading.Add(tableType);
            List<DatabaseTable> items = this.Get(tableType, null);
            this._Preloading.Remove(tableType);

            foreach (DatabaseTable currItem in items)
                this.getAllRelationData(currItem);
        }

        /// <summary>
        /// Returns a list of objects of the specified type, based on the specified criteria. 
        /// </summary>
        /// <typeparam name="T">Specified type</typeparam>
        /// <param name="criteria">Specified criteria</param>
        /// <returns>List of objects of the specified type</returns>
        public List<T> Get<T>(ICriteria criteria) where T : DatabaseTable
        {
            List<T> rtn = new List<T>();
            List<DatabaseTable> objList = Get(typeof(T), criteria);
            foreach (DatabaseTable currObj in objList)
            {
                rtn.Add((T)currObj);
            }

            return rtn;
        }

        /// <summary>
        /// Returns a list of objects of the specified table, based on the specified criteria.
        /// </summary>
        /// <param name="tableType">Specified table</param>
        /// <param name="criteria">Specified criteria</param>
        /// <returns>List of objects of the specified table</returns>
        public List<DatabaseTable> Get(Type tableType, ICriteria criteria)
        {
            this.verifyTable(tableType);

            // if this is a request for all object of this type, if we already have done this 
            // type of request, just return the cached objects. This assumes no one else is changing
            // the DB.
            if (criteria == null)
            {
                if (this._DoneFullRetrieve.ContainsKey(tableType))
                    return new List<DatabaseTable>(this._Cache.GetAll(tableType));

                this._DoneFullRetrieve[tableType] = true;
            }

            lock (this._LockObject)
            {
                List<DatabaseTable> rtn = new List<DatabaseTable>();

                try
                {
                    // build and execute the query
                    string strQuery = getSelectQuery(tableType);
                    if (criteria != null)
                        strQuery += criteria.GetWhereClause();

                    SQLiteResultSet resultSet = this._DbClient.Execute(strQuery);

                    // store each one
                    foreach (SQLiteResultSet.Row row in resultSet.Rows)
                    {
                        // create the new entry
                        DatabaseTable newRecord = (DatabaseTable)tableType.GetConstructor(System.Type.EmptyTypes).Invoke(null);
                        newRecord.DBManager = this;
                        newRecord.LoadByRow(row);

                        // if it is already cached, just use the cached object
                        if (this._Cache.Get(tableType, (int)newRecord.ID) != null)
                            rtn.Add(this._Cache.Get(tableType, (int)newRecord.ID));

                        // otherwise use the new record and cache it
                        else
                        {
                            newRecord = this._Cache.Add(newRecord);
                            this.getAllRelationData(newRecord);
                            rtn.Add(newRecord);
                        }
                    }
                }
                catch (SQLiteException e)
                {
                    _Logger.ErrorException(string.Format("[{0}][Get] Error retrieving with criteria from {1} table.", this._InstanceId, tableType.Name), e);
                }

                return rtn;
            }
        }

        /// <summary>
        /// Based on the given table type and id, returns the cooresponding record.
        /// </summary>
        /// <typeparam name="T">>Specified type</typeparam>
        /// <param name="iId">Specified type record id</param>
        /// <returns>Ccooresponding record</returns>
        public T Get<T>(int iId) where T : DatabaseTable
        {
            return (T)this.Get(typeof(T), iId);
        }

        /// <summary>
        /// Based on the given table type and id, returns the cooresponding record.
        /// </summary>
        /// <param name="tableType">Specified table type</param>
        /// <param name="iId">Specified type record id</param>
        /// <returns>Cooresponding record</returns>
        public DatabaseTable Get(Type tableType, int iId)
        {
            // if we have already pulled this record down, don't query the DB
            DatabaseTable cachedObj = this._Cache.Get(tableType, iId);
            if (cachedObj != null)
                return cachedObj;

            this.verifyTable(tableType);

            lock (this._LockObject)
            {
                try
                {
                    // build and execute the query
                    string strQuery = getSelectQuery(tableType);
                    strQuery += "where id = " + iId;
                    SQLiteResultSet resultSet = this._DbClient.Execute(strQuery);

                    // make new object
                    DatabaseTable newRecord = (DatabaseTable)tableType.GetConstructor(System.Type.EmptyTypes).Invoke(null);

                    // if the given id doesn't exist, create a new uncommited record 
                    if (resultSet.Rows.Count == 0)
                    {
                        newRecord.Clear();
                        return null;
                    }

                    // otherwise load it into the object
                    newRecord.DBManager = this;
                    newRecord.LoadByRow(resultSet.Rows[0]);
                    this._Cache.Add(newRecord);
                    this.getAllRelationData(newRecord);
                    return newRecord;
                }
                catch (SQLiteException e)
                {
                    _Logger.ErrorException(string.Format("[{0}][Get] Error getting by ID from {1} table.", this._InstanceId, GetTableName(tableType)), e);
                    return null;
                }
            }
        }

        public void Revert(DatabaseTable dbObject)
        {
            if (dbObject == null || dbObject.RevertInProcess)
                return;

            dbObject.RevertInProcess = true;

            // recursively revert any child objects
            foreach (DBRelation currRelation in DBRelation.GetRelations(dbObject.GetType()))
                foreach (DatabaseTable subObj in currRelation.GetRelationList(dbObject))
                    this.Revert(subObj);

            // revert any objects directly linked to
            foreach (DBField currField in DBField.GetFieldList(dbObject.GetType()))
                if (currField.DBType == DBField.DBDataType.DB_OBJECT)
                    this.Commit((DatabaseTable)currField.GetValue(dbObject));

            // revert any modified relationships
            foreach (DBRelation currRelation in DBRelation.GetRelations(dbObject.GetType()))
                if (currRelation.GetRelationList(dbObject).CommitNeeded)
                {
                    currRelation.GetRelationList(dbObject).Populated = false;
                    this.getRelationData(dbObject, currRelation);
                }

            dbObject.RevertInProcess = false;

            // if this object has never been committed or has not been changed, just quit
            if (dbObject.ID == null || !dbObject.CommitNeeded)
                return;

            // regrab, copy values and reupdate cache
            this._Cache.Remove(dbObject);
            DatabaseTable oldVersion = this.Get(dbObject.GetType(), (int)dbObject.ID);
            dbObject.Copy(oldVersion);
            this._Cache.Replace(dbObject);
        }

        public void Populate(IRelationList relationList)
        {
            this.getRelationData(relationList.Owner, relationList.MetaData);
        }

        /// <summary>
        /// Begin new SQL transaction
        /// </summary>
        public void BeginTransaction()
        {
            if (!this._TransactionInProgress)
            {
                this._TransactionInProgress = true;
                try
                {
                    lock (this._LockObject)
                    {
                        this._DbClient.Execute("BEGIN");
                    }
                }
                catch (SQLiteException)
                {
                    _Logger.Error("[{0}][BeginTransaction] Failed to BEGIN a SQLite Transaction.", this._InstanceId);
                }
            }
        }

        /// <summary>
        /// End current transaction
        /// </summary>
        public void EndTransaction()
        {
            if (this._TransactionInProgress)
            {
                this._TransactionInProgress = false;
                try
                {
                    lock (_LockObject)
                    {
                        this._DbClient.Execute("COMMIT");
                    }
                }

                catch (SQLiteException)
                {
                    _Logger.Error("[{0}][EndTransaction] Failed to COMMIT a SQLite Transaction.", this._InstanceId);
                }
            }
        }

        public void Commit(IRelationList relationList)
        {
            this.updateRelationTable(relationList.Owner, relationList.MetaData);
        }

        /// <summary>
        /// Writes the given object to the database. 
        /// </summary>
        /// <param name="dbObject">Object to be written</param>
        public void Commit(DatabaseTable dbObject)
        {
            if (dbObject == null)
                return;

            if (dbObject.CommitInProcess)
                return;

            if (dbObject.DBManager == null)
                dbObject.DBManager = this;

            dbObject.CommitInProcess = true;

            if (dbObject.CommitNeeded)
            {
                this.verifyTable(dbObject.GetType());

                dbObject.BeforeCommit();

                if (dbObject.ID == null)
                    this.insert(dbObject);
                else
                    this.update(dbObject);
            }

            this.CommitRelations(dbObject);

            dbObject.CommitInProcess = false;
            dbObject.CommitNeeded = false;
            dbObject.AfterCommit();
        }

        public void CommitRelations(DatabaseTable dbObject)
        {
            if (dbObject == null)
                return;

            foreach (DBRelation currRelation in DBRelation.GetRelations(dbObject.GetType()))
            {
                if (currRelation.AutoRetrieve)
                {
                    foreach (DatabaseTable subObj in currRelation.GetRelationList(dbObject))
                        this.Commit(subObj);

                    this.updateRelationTable(dbObject, currRelation);
                }
            }

            foreach (DBField currField in DBField.GetFieldList(dbObject.GetType()))
            {
                if (currField.DBType == DBField.DBDataType.DB_OBJECT)
                {
                    this.Commit((DatabaseTable)currField.GetValue(dbObject));
                }
            }

        }

        /// <summary>
        /// Deletes a given object from the database, object in memory persists and could be recommited. 
        /// </summary>
        /// <param name="dbObject">Object to be deleted</param>
        public void Delete(DatabaseTable dbObject)
        {
            try
            {
                if (dbObject.ID == null)
                {
                    _Logger.Warn("[{0}][Delete] Tried to delete an uncommited object...", this._InstanceId);
                    return;
                }

                dbObject.BeforeDelete();

                string strQuery = "delete from " + GetTableName(dbObject) + " where ID = " + dbObject.ID;
                _Logger.Debug("[{0}][Delete] Deleting: {1}", this._InstanceId, dbObject);
                _Logger.Debug("[{0}][Delete] SQL query: '{1}'", this._InstanceId, strQuery);

                deleteAllRelationData(dbObject);
                lock (_LockObject)
                {
                    this._DbClient.Execute(strQuery);
                }

                this._Cache.Remove(dbObject);
                dbObject.ID = null;
                dbObject.AfterDelete();

                if (this.ObjectDeleted != null)
                {
                    _Logger.Debug("[{0}][Delete] Calling listeners for {1}", this._InstanceId, dbObject.ToString());
                    this.ObjectDeleted(dbObject);
                }
            }
            catch (SQLiteException e)
            {
                _Logger.ErrorException(string.Format("[{0}][Delete] Error deleting object from {1} table.", this._InstanceId, GetTableName(dbObject)), e);
                return;
            }

        }

        public HashSet<string> GetAllValues(DBField field)
        {
            ICollection items = this.Get(field.OwnerType, null);

            // loop through all items in the DB and grab all existing values for this field
            HashSet<string> uniqueStrings = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (DatabaseTable currItem in items)
            {
                List<string> values = getValues(field.GetValue(currItem));
                foreach (string strCurrStr in values)
                    uniqueStrings.Add(strCurrStr);
            }

            return uniqueStrings;
        }

        public HashSet<string> GetAllValues<T>(DBField field, DBRelation relation, ICollection<T> items) where T : DatabaseTable
        {
            // loop through all items in the DB and grab all existing values for this field
            HashSet<string> uniqueStrings = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (T currItem in items)
            {
                if (relation == null)
                {
                    List<string> values = getValues(field.GetValue(currItem));
                    foreach (string strCurrStr in values)
                        uniqueStrings.Add(strCurrStr);
                }
                else
                {
                    foreach (DatabaseTable currSubItem in relation.GetRelationList(currItem))
                    {
                        List<string> values = getValues(field.GetValue(currSubItem));
                        foreach (string strCurrStr in values)
                            uniqueStrings.Add(strCurrStr);
                    }
                }
            }

            return uniqueStrings;
        }

        public DynamicFilterHelper<T> GetFilterHelper<T>() where T : DatabaseTable
        {
            if (this._FilterHelperLookup.ContainsKey(typeof(T)))
                return (DynamicFilterHelper<T>)this._FilterHelperLookup[typeof(T)];

            return null;
        }

        public void AddFilterHelper<T>(DynamicFilterHelper<T> helper) where T : DatabaseTable
        {
            this._FilterHelperLookup[typeof(T)] = helper;
        }

        #endregion

        #region Public Static Methods

        // Returns the name of the table of the given type.
        public static string GetTableName(Type tableType)
        {
            return getDBTableAttribute(tableType).TableName;
        }

        // Returns the name of the table of the given type.
        public static string GetTableName(DatabaseTable tableObject)
        {
            return GetTableName(tableObject.GetType());
        }

        public static bool IsDatabaseTableType(Type t)
        {
            Type currType = t;
            while (currType != null)
            {
                if (currType == typeof(DatabaseTable))
                {
                    return true;
                }
                currType = currType.BaseType;
            }

            return false;
        }

        #endregion

        #region Private Methods

        private static List<string> getValues(object obj)
        {
            List<string> results = new List<string>();

            if (obj == null)
                return results;

            if (obj is string)
            {
                if (((string)obj).Trim().Length != 0)
                    results.Add((string)obj);
            }
            else if (obj is StringList)
            {
                foreach (string strCurrValue in (StringList)obj)
                {
                    if (strCurrValue != null && strCurrValue.Trim().Length != 0)
                        results.Add(strCurrValue);
                }
            }
            else if (obj is bool || obj is bool?)
            {
                results.Add("true");
                results.Add("false");
            }
            else
            {
                results.Add(obj.ToString());
            }

            return results;
        }

        private bool verifyIntegrity(SQLiteClient client)
        {
            string strQuery = "PRAGMA integrity_check;";
            _Logger.Info("[{0}][verifyIntegrity] Executing SQL integrity check", this._InstanceId);

            try
            {
                SQLiteResultSet results = client.Execute(strQuery);
                if (results != null)
                {
                    if (results.Rows.Count == 1)
                    {
                        SQLiteResultSet.Row arr = results.Rows[0];
                        if (arr.fields.Count == 1)
                        {
                            if (arr.fields[0] == "ok")
                            {
                                _Logger.Info("[{0}][verifyIntegrity] Database integrity check succeeded", this._InstanceId);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _Logger.Info("[{2}][verifyIntegrity] Integrity check failed, database is corrupt. Reason = '{0}', Filename = '{1}'",
                    e.Message, client.DatabaseName, this._InstanceId);
                return false;
            }
            _Logger.Info("[{1}][verifyIntegrity] Integrity check failed, database is corrupt. Filename = '{0}'", client.DatabaseName, this._InstanceId);
            return false;
        }

        // Checks that the table corresponding to this type exists, and if it is missing, it creates it.
        // Also verifies all columns represented in the class are also present in the table, creating 
        // any missing. Needs to be enhanced to allow for changed defaults.
        private void verifyTable(Type tableType)
        {
            lock (this._LockObject)
            {
                // check that we haven't already verified this table
                if (this._IsVerified.ContainsKey(tableType))
                    return;

                // attempt to grab table info for the type. if none exists, it's not tagged to be a table
                DBTableAttribute tableAttr = getDBTableAttribute(tableType);
                if (tableAttr == null)
                    return;

                try
                {
                    // check if the table exists in the database, if not, create it
                    SQLiteResultSet resultSet = this._DbClient.Execute("select * from sqlite_master where type='table' and name = '" + tableAttr.TableName + "'");
                    if (resultSet.Rows.Count == 0)
                    {
                        resultSet = this._DbClient.Execute("create table " + tableAttr.TableName + " (id INTEGER primary key )");
                        _Logger.Info("[{0}][verifyTable] Created {1} table.", this._InstanceId, tableAttr.TableName);
                    }

                    // grab existing table info from the DB
                    resultSet = this._DbClient.Execute("PRAGMA table_info(" + tableAttr.TableName + ")");

                    // loop through the CLASS DEFINED fields, and verify each is contained in the result set
                    foreach (DBField currField in DBField.GetFieldList(tableType))
                    {

                        // loop through all defined columns in DB to ensure this col exists 
                        bool bExists = false;
                        foreach (SQLiteResultSet.Row currRow in resultSet.Rows)
                        {
                            if (currField.FieldName == currRow.fields[1])
                            {
                                bExists = true;
                                break;
                            }
                        }

                        // if we couldn't find the column create it
                        if (!bExists)
                        {
                            string strDefaultValue;
                            if (currField.Default == null)
                                strDefaultValue = "NULL";
                            else
                                strDefaultValue = getSQLiteString(currField, currField.Default);

                            this._DbClient.Execute("alter table " + tableAttr.TableName + " add column " + currField.FieldName + " " +
                                             currField.DBType.ToString() + " default " + strDefaultValue);
                            // logger.Debug("Added " + tableAttr.TableName + "." + currField.FieldName + " column.");
                        }
                    }

                    this.verifyRelationTables(tableType);
                    this._IsVerified[tableType] = true;
                }
                catch (SQLiteException e)
                {
                    _Logger.ErrorException(string.Format("[{0}][verifyTable] Internal error verifying {1} ({2}) table.",
                        this._InstanceId, tableAttr.TableName, tableType.ToString()), e);
                }
            }
        }

        private void verifyRelationTables(Type primaryType)
        {
            foreach (DBRelation currRelation in DBRelation.GetRelations(primaryType))
            {
                try
                {
                    // check if the table exists in the database, if not, create it
                    SQLiteResultSet resultSet = this._DbClient.Execute("select * from sqlite_master where type='table' and name = '" + currRelation.TableName + "'");
                    if (resultSet.Rows.Count == 0)
                    {
                        // create table
                        string createQuery =
                            "create table " + currRelation.TableName + " (id INTEGER primary key, " +
                            currRelation.PrimaryColumnName + " INTEGER, " +
                            currRelation.SecondaryColumnName + " INTEGER)";

                        resultSet = _DbClient.Execute(createQuery);

                        // create index1
                        resultSet = this._DbClient.Execute("create index " + currRelation.TableName + "__index1 on " +
                            currRelation.TableName + " (" + currRelation.PrimaryColumnName + ")");

                        // create index2
                        resultSet = this._DbClient.Execute("create index " + currRelation.TableName + "__index2 on " +
                            currRelation.TableName + " (" + currRelation.SecondaryColumnName + ")");

                        _Logger.Debug("[{0}][verifyRelationTables] Created {1} sub-table.", this._InstanceId, currRelation.TableName);
                    }
                }
                catch (SQLiteException e)
                {
                    _Logger.FatalException(string.Format("[{0}][verifyRelationTables] Error verifying {1} subtable.", 
                        this._InstanceId, currRelation.TableName), e);
                }
            }
        }

        // Returns the table attribute information for the given type.
        private static DBTableAttribute getDBTableAttribute(Type tableType)
        {
            // loop through the custom attributes of the type, if one of them is the type
            // we want, return it.
            object[] customAttrArray = tableType.GetCustomAttributes(true);
            foreach (object currAttr in customAttrArray)
            {
                if (currAttr.GetType() == typeof(DBTableAttribute))
                    return (DBTableAttribute)currAttr;
            }

            throw new Exception("[getDBTableAttribute] Table class " + tableType.Name + " not tagged with DBTable attribute.");
        }

        // Returns a select statement retrieving all fields ordered as defined by FieldList
        // for the given Table Type. A where clause can be appended
        private static string getSelectQuery(Type tableType)
        {
            string strQuery = "select ";
            foreach (DBField currField in DBField.GetFieldList(tableType))
            {
                if (strQuery != "select ")
                    strQuery += ", ";

                strQuery += currField.FieldName;
            }
            strQuery += ", id from " + GetTableName(tableType) + " ";
            return strQuery;
        }

        public static string getSQLiteString(object value)
        {
            return getSQLiteString(null, value);
        }

        // creates an escaped, quoted string representation of the given object
        public static string getSQLiteString(DBField ownerField, object value)
        {
            if (value == null)
                return "NULL";

            string strVal = "";

            // handle boolean types
            if (value.GetType() == typeof(bool) || value.GetType() == typeof(Boolean))
            {
                if ((Boolean)value == true)
                    strVal = "1";
                else
                    strVal = "0";
            }
            // handle double types
            else if (value.GetType() == typeof(double) || value.GetType() == typeof(Double))
                strVal = ((double)value).ToString(new CultureInfo("en-US", false));

            // handle float types
            else if (value.GetType() == typeof(float) || value.GetType() == typeof(Single))
                strVal = ((float)value).ToString(new CultureInfo("en-US", false));

            // handle database table types
            else if (IsDatabaseTableType(value.GetType()))
            {
                if (ownerField != null && ownerField.Type != value.GetType())
                    strVal = ((DatabaseTable)value).ID.ToString() + "|||" + value.GetType().AssemblyQualifiedName;
                else
                    strVal = ((DatabaseTable)value).ID.ToString();

            }

            // if field represents metadata about another dbfield
            else if (value is DBField)
            {
                DBField field = (DBField)value;
                strVal = field.OwnerType.AssemblyQualifiedName + "|||" + field.FieldName;
            }

            // if field represents metadata about a relation (subtable)
            else if (value is DBRelation)
            {
                DBRelation relation = (DBRelation)value;
                strVal = relation.PrimaryType.AssemblyQualifiedName + "|||" +
                         relation.SecondaryType.AssemblyQualifiedName + "|||" +
                         relation.Identifier;
            }

            // handle C# Types, Need full qualified name to load types from other aseemblies
            else if (value is Type)
                strVal = ((Type)value).AssemblyQualifiedName;

            else if (value is DateTime)
            {
                strVal = ((DateTime)value).ToUniversalTime().ToString("u");
            }
            // everythign else just uses ToString()
            else
                strVal = value.ToString();


            // if we ended up with an empty string, save a space. an empty string is interpreted
            // as null by SQLite, and thats not what we want.
            if (strVal == "")
                strVal = " ";

            // escape all quotes
            strVal = strVal.Replace("'", "''");

            return "'" + strVal + "'";
        }

        // inserts a new object to the database
        private void insert(DatabaseTable dbObject)
        {
            try
            {
                string strQueryFieldList = "";
                string strQueryValueList = "";

                // loop through the fields and build the strings for the query
                foreach (DBField currField in DBField.GetFieldList(dbObject.GetType()))
                {
                    if (strQueryFieldList != "")
                    {
                        strQueryFieldList += ", ";
                        strQueryValueList += ", ";
                    }

                    // if we dont have an ID, commit as needed
                    if (currField.DBType == DBField.DBDataType.DB_OBJECT && currField.GetValue(dbObject) != null &&
                        ((DatabaseTable)currField.GetValue(dbObject)).ID == null)
                        Commit((DatabaseTable)currField.GetValue(dbObject));

                    strQueryFieldList += currField.FieldName;
                    strQueryValueList += getSQLiteString(currField, currField.GetValue(dbObject));
                }

                string strQuery = "insert into " + GetTableName(dbObject.GetType()) +
                               " (" + strQueryFieldList + ") values (" + strQueryValueList + ")";

                _Logger.Debug("[{0}][insert] Inserting: {1}", this._InstanceId, dbObject.ToString());

                lock (this._LockObject)
                {
                    this._DbClient.Execute(strQuery);
                    dbObject.ID = this._DbClient.LastInsertID();
                }
                dbObject.DBManager = this;
                this._Cache.Add(dbObject);

                // loop through the fields and commit attached objects as needed
                foreach (DBField currField in DBField.GetFieldList(dbObject.GetType()))
                {
                    if (currField.DBType == DBField.DBDataType.DB_OBJECT)
                        this.Commit((DatabaseTable)currField.GetValue(dbObject));
                }

                // notify any listeners of the status change
                if (this.ObjectInserted != null)
                    this.ObjectInserted(dbObject);
            }
            catch (SQLiteException e)
            {
                _Logger.ErrorException(string.Format("[{0}][insert] Could not commit to {1} table.",
                    this._InstanceId, GetTableName(dbObject.GetType())), e);
            }
        }

        // updates the given object in the database. assumes the object was previously retrieved 
        // via a Get call from this class.
        private void update(DatabaseTable dbObject)
        {
            try
            {
                string strQuery = "update " + GetTableName(dbObject.GetType()) + " set ";

                // loop through the fields and build the strings for the query
                bool bFirstField = true;
                foreach (DBField currField in DBField.GetFieldList(dbObject.GetType()))
                {
                    if (!bFirstField)
                    {
                        strQuery += ", ";
                    }

                    bFirstField = false;

                    // if this is a linked db object commit it as needed
                    if (currField.DBType == DBField.DBDataType.DB_OBJECT)
                        this.Commit((DatabaseTable)currField.GetValue(dbObject));

                    strQuery += currField.FieldName + " = " + getSQLiteString(currField, currField.GetValue(dbObject));

                }

                // add the where clause
                strQuery += " where id = " + dbObject.ID;

                // execute the query
                _Logger.Debug("[{0}][update] Updating: {1}", this._InstanceId, dbObject.ToString());

                lock (this._LockObject)
                {
                    this._DbClient.Execute(strQuery);
                }

                dbObject.DBManager = this;
                
                this.updateRelationTables(dbObject);

                // notify any listeners of the status change
                TableUpdateInfo ui = new TableUpdateInfo();
                ui.UpdatedFields = new HashSet<DBField>(dbObject.ChangedFields);

                if (this.ObjectUpdatedEx != null)
                    this.ObjectUpdatedEx(dbObject, ui);

                if (this.ObjectUpdated != null)
                    this.ObjectUpdated(dbObject);

                dbObject.ChangedFields.Clear();
            }
            catch (SQLiteException e)
            {
                _Logger.ErrorException(string.Format("[{0}][update] Could not commit to {1} table.", 
                    this._InstanceId, GetTableName(dbObject.GetType())), e);
            }

        }

        /// <summary>
        /// Inserts into the database all relation information. Dependent objects will be commited.
        /// </summary>
        /// <param name="dbObject">The primary object owning the RelationList to be populated.</param>
        /// <param name="forceRetrieval">Determines if ALL relations will be retrieved.</param>
        private void updateRelationTables(DatabaseTable dbObject)
        {
            foreach (DBRelation currRelation in DBRelation.GetRelations(dbObject.GetType()))
            {
                this.updateRelationTable(dbObject, currRelation);
            }
        }

        private void updateRelationTable(DatabaseTable dbObject, DBRelation currRelation)
        {
            if (!currRelation.GetRelationList(dbObject).CommitNeeded)
                return;

            // clear out old values then insert the new
            this.deleteRelationData(dbObject, currRelation);

            // insert all relations to the database
            foreach (object currObj in (IList)currRelation.GetRelationList(dbObject))
            {
                DatabaseTable currDBObj = (DatabaseTable)currObj;
                this.Commit(currDBObj);
                string insertQuery = "insert into " + currRelation.TableName + "(" +
                    currRelation.PrimaryColumnName + ", " +
                    currRelation.SecondaryColumnName + ") values (" +
                    dbObject.ID + ", " + currDBObj.ID + ")";

                lock (this._LockObject)
                {
                    this._DbClient.Execute(insertQuery);
                }
            }

            currRelation.GetRelationList(dbObject).CommitNeeded = false;
        }

        // deletes all subtable data for the given object.
        private void deleteAllRelationData(DatabaseTable dbObject)
        {
            foreach (DBRelation currRelation in DBRelation.GetRelations(dbObject.GetType()))
                this.deleteRelationData(dbObject, currRelation);
        }

        private void deleteRelationData(DatabaseTable dbObject, DBRelation relation)
        {
            if (relation.PrimaryType != dbObject.GetType())
                return;

            string strDeleteQuery = "delete from " + relation.TableName + " where " + relation.PrimaryColumnName + "=" + dbObject.ID;

            lock (this._LockObject)
            {
                this._DbClient.Execute(strDeleteQuery);
            }
        }

        private void getAllRelationData(DatabaseTable dbObject)
        {
            if (this._Preloading.Contains(dbObject.GetType()))
                return;

            foreach (DBRelation currRelation in DBRelation.GetRelations(dbObject.GetType()))
            {
                if (currRelation.AutoRetrieve)
                    this.getRelationData(dbObject, currRelation);
            }
        }

        private void getRelationData(DatabaseTable dbObject, DBRelation relation)
        {
            IRelationList list = relation.GetRelationList(dbObject);

            if (list.Populated)
                return;

            bool bOldCommitNeededFlag = dbObject.CommitNeeded;
            list.Populated = true;

            // build query
            string strSelectQuery = "select " + relation.SecondaryColumnName + " from " +
                       relation.TableName + " where " + relation.PrimaryColumnName + "=" + dbObject.ID;

            // and retireve relations
            SQLiteResultSet resultSet;
            lock (this._LockObject)
            {
                resultSet = this._DbClient.Execute(strSelectQuery);
            }

            // parse results and add them to the list
            list.Clear();
            foreach (SQLiteResultSet.Row currRow in resultSet.Rows)
            {
                int iObjID = int.Parse(currRow.fields[0]);
                DatabaseTable newObj = this.Get(relation.SecondaryType, iObjID);
                list.AddIgnoreSisterList(newObj);
            }

            // update flags as needed
            list.CommitNeeded = false;
            dbObject.CommitNeeded = bOldCommitNeededFlag;
        }

        private void backupDatabase()
        {
            if (this._DbBackupDirectory == null)
                return;

            _Logger.Info("[{0}][backupDatabase] Backing up database", this._InstanceId);

            string strBackupDirectory = this._DbBackupDirectory;
            string strSourceFile = this._DbFilename;
            string strDestinationFile = Path.Combine(strBackupDirectory, Path.GetFileName(this._DbFilename));

            try
            {
                if (!Directory.Exists(strBackupDirectory))
                {
                    Directory.CreateDirectory(strBackupDirectory);
                }

                File.Copy(strSourceFile, strDestinationFile, true);
            }
            catch (Exception ex)
            {
                _Logger.Warn("[{3}][backupDatabase] Failed to backup database. Source File = '{0}', Destination File = '{1}', Reason = '{2}'",
                    strSourceFile, strDestinationFile, ex.Message, this._InstanceId);
            }
        }

        private void restoreDatabase()
        {
            if (this._DbBackupDirectory == null)
                return;

            // backup the corrupt database in case user wants to fix themselves
            string strBackupDirectory = this._DbBackupDirectory;
            string strSourceFile = this._DbFilename;
            string strDestinationFile = Path.Combine(strBackupDirectory, string.Format("{0}-Corrupt-{1}.db3", Path.GetFileNameWithoutExtension(this._DbFilename), DateTime.Now.ToString("yyyyMMddHHmmss")));

            _Logger.Info("[{1}][restoreDatabase] Backing up corrupt database. Filename = '{0}'", strDestinationFile, this._InstanceId);

            try
            {
                File.Copy(strSourceFile, strDestinationFile, true);
            }
            catch (Exception ex)
            {
                _Logger.Warn("[{3}][restoreDatabase] Failed to backup corrupt database. Source File = '{0}', Destination File = '{1}', Reason = '{2}'", 
                    strSourceFile, strDestinationFile, ex.Message, this._InstanceId);
            }

            _Logger.Info("[{0}][restoreDatabase] Restoring last known good database", this._InstanceId);

            strSourceFile = Path.Combine(strBackupDirectory, Path.GetFileName(this._DbFilename));
            strDestinationFile = this._DbFilename;

            try
            {
                File.Copy(strSourceFile, strDestinationFile, true);
                // it may not be immediately accessible so sleep a little
                System.Threading.Thread.Sleep(250);
            }
            catch (Exception ex)
            {
                _Logger.Warn("[{3}][restoreDatabase] Failed to restore database. Source File = '{0}', Destination File = '{1}', Reason = '{2}'", 
                    strSourceFile, strDestinationFile, ex.Message, this._InstanceId);
            }
        }

        #endregion

    }

    public class TableUpdateInfo
    {
        public HashSet<DBField> UpdatedFields
        {
            get;
            set;
        }
    }


    #region Criteria Classes
    public interface ICriteria
    {
        string GetWhereClause();
        string GetClause();
    }

    public class GroupedCriteria : ICriteria
    {
        public enum Operator { AND, OR }

        private ICriteria _CritA;
        private ICriteria _CritB;
        private Operator _Op;

        public GroupedCriteria(ICriteria critA, Operator op, ICriteria critB)
        {
            this._CritA = critA;
            this._CritB = critB;
            this._Op = op;
        }

        public string GetWhereClause()
        {
            return " where " + this.GetClause();
        }

        public string GetClause()
        {
            return " (" + this._CritA.GetClause() + " " + this._Op.ToString() + " " + this._CritB.GetClause() + ") ";
        }

        public override string ToString()
        {
            return this.GetWhereClause();
        }

    }

    public class BaseCriteria : ICriteria
    {
        private DBField _Field;
        private object _Value;
        private string _Op;

        public BaseCriteria(DBField field, string strOp, object value)
        {
            this._Field = field;
            this._Op = strOp;
            this._Value = value;
        }

        public string GetWhereClause()
        {
            if (this._Field == null)
                return "";

            return " where " + this.GetClause();
        }

        public string GetClause()
        {
            return " (" + this._Field.FieldName + " " + this._Op + " " + DatabaseManager.getSQLiteString(this._Field, this._Value) + ") ";
        }

        public override string ToString()
        {
            return this.GetWhereClause();
        }
    }

    public class ListCriteria : ICriteria
    {

        List<DatabaseTable> _List;
        private bool _Exclude;

        public ListCriteria(List<DatabaseTable> list, bool bExclude)
        {
            this._List = list;
            this._Exclude = bExclude;
        }

        public string GetWhereClause()
        {
            return " where " + this.GetClause();
        }

        public string GetClause()
        {
            if (this._List == null) 
                return "1=1";

            string strRtn = " ID" + (this._Exclude ? " not " : " ") + "in ( ";
            bool bFirst = true;
            foreach (DatabaseTable currItem in this._List)
            {
                if (currItem.ID == null)
                    continue;

                if (bFirst)
                    bFirst = false;
                else
                    strRtn += ", ";

                strRtn += currItem.ID;
            }

            strRtn += ")";

            return strRtn;
        }

        public override string ToString()
        {
            return this.GetWhereClause();
        }
    }

    #endregion
}
