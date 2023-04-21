using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using System.Threading;

namespace MediaPortal.Pbk.Cornerstone.Database
{
    // This class is primarily intended to ensure that when a database object is selected
    // multiple times from two different places in the code, both places will be working
    // with the same physical c# object. It also reduces retrieval time for get(id) type
    // queries.
    class DatabaseCache
    {
        private Dictionary<Type, Dictionary<int, DatabaseTable>> _Cache;

        public DatabaseCache()
        {
            this._Cache = new Dictionary<Type, Dictionary<int, DatabaseTable>>();
        }

        public bool Contains(DatabaseTable obj)
        {
            if (obj == null || this._Cache[obj.GetType()] == null)
                return false;

            return this._Cache[obj.GetType()].ContainsValue(obj);
        }

        public DatabaseTable Get(Type type, int iId)
        {
            if (this._Cache.ContainsKey(type) && this._Cache[type].ContainsKey(iId))
                return this._Cache[type][iId];
            else 
                return null;
        }

        public ICollection<DatabaseTable> GetAll(Type type)
        {
            if (this._Cache.ContainsKey(type))
                return this._Cache[type].Values;

            return new List<DatabaseTable>();
        }

        // Adds the given element to the cacheing system.
        public DatabaseTable Add(DatabaseTable obj)
        {
            if (obj == null || obj.ID == null)
                return obj;

            if (!this._Cache.ContainsKey(obj.GetType()))
                this._Cache[obj.GetType()] = new Dictionary<int, DatabaseTable>();

            if (!this._Cache[obj.GetType()].ContainsKey((int)obj.ID))
                this._Cache[obj.GetType()][(int)obj.ID] = obj;

            return this._Cache[obj.GetType()][(int)obj.ID];
        }

        // Goes through the list and if any elements reference an object already in
        // memory, it updates the reference in the list with the in memory version.
        public void Sync(IList<DatabaseTable> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                DatabaseTable currObj = list[i];

                if (currObj == null || currObj.ID == null)
                    continue;

                try
                {
                    list[i] = (DatabaseTable)this._Cache[currObj.GetType()][(int)currObj.ID];
                }
                catch (Exception e)
                {
                    if (e.GetType() == typeof(ThreadAbortException))
                        throw e;

                    this.Add(currObj);
                }
            }
        }

        // Should only be called if an item has been deleted from the database.
        public void Remove(DatabaseTable obj)
        {
            if (obj == null || obj.ID == null)
                return;

            this._Cache[obj.GetType()].Remove((int)obj.ID);
        }

        // Remove the existing object with the same ID from the cache and store this one instead.
        public void Replace(DatabaseTable obj)
        {
            if (obj == null || obj.ID == null)
                return;

            if (!this._Cache.ContainsKey(obj.GetType()))
                this._Cache[obj.GetType()] = new Dictionary<int, DatabaseTable>();

            this._Cache[obj.GetType()][(int)obj.ID] = obj;
        }

    }
}
