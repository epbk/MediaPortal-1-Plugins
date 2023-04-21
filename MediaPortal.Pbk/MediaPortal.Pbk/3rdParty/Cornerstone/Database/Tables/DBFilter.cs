using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;

namespace MediaPortal.Pbk.Cornerstone.Database.Tables
{
    [DBTableAttribute("filters")]
    public class DBFilter<T> : GenericDatabaseTable<T>, IFilter<T>, IDBFilter, IGenericFilter
        where T : DatabaseTable
    {

        public enum CriteriaGroupingEnum
        {
            ALL,
            ONE,
            NONE
        }

        #region IFilter<T> Members

        public event FilterUpdatedDelegate<T> Updated;

        public HashSet<T> Filter(ICollection<T> input)
        {
            return this.Filter(input, false);
        }

        public HashSet<T> Filter(ICollection<T> input, bool bAorceActive)
        {
            bool bActive = bAorceActive || this._Active;
            HashSet<T> results = new HashSet<T>();

            // if we are not active, or the filter has no inclusive rules start
            // with everything and remove the blacklist
            if (!bActive || (this.Criteria.Count == 0 && this.WhiteList.Count == 0))
            {
                if (!bActive && input is HashSet<T>)
                    return input as HashSet<T>;

                foreach (T currItem in input)
                    results.Add(currItem);

                if (!bActive)
                    return results;

                // remove blacklist items
                if (bActive)
                    foreach (T currItem in this.BlackList)
                    {
                        if (this.BlackList.Contains(currItem))
                            results.Remove(currItem);
                    }

                return this.checkInversion(input, results);
            }


            // if there is no criteria and no blacklisted items, just use the white list
            if (this.Criteria.Count == 0 && this.BlackList.Count == 0)
            {
                foreach (T currItem in WhiteList)
                {
                    if (input.Contains(currItem))
                        results.Add(currItem);
                }

                return this.checkInversion(input, results);
            }

            // handle AND type criteria
            bool bFirst = true;
            if (this.CriteriaGrouping == CriteriaGroupingEnum.ALL)
            {
                foreach (DBCriteria<T> currCriteria in this.Criteria)
                {
                    results = currCriteria.Filter(bFirst ? input : results, bActive);
                    bFirst = false;
                }
            }

            // handle OR type criteria
            if (this.CriteriaGrouping == CriteriaGroupingEnum.ONE)
            {
                HashSet<T> okItems = new HashSet<T>();
                foreach (DBCriteria<T> currCriteria in Criteria)
                {
                    HashSet<T> tmp = currCriteria.Filter(input, bActive);
                    okItems.UnionWith(tmp);
                }

                results = okItems;
            }

            // handle NONE type criteria
            if (this.CriteriaGrouping == CriteriaGroupingEnum.NONE)
            {
                foreach (T currItem in input)
                    results.Add(currItem);

                HashSet<T> excludeItems = new HashSet<T>();
                foreach (T currItem in input)
                    excludeItems.Add(currItem);

                foreach (DBCriteria<T> currCriteria in this.Criteria)
                    excludeItems = currCriteria.Filter(excludeItems, bActive);

                foreach (T item in excludeItems)
                    results.Remove(item);
            }

            // remove blacklist items
            foreach (T currItem in this.BlackList)
            {
                if (this.BlackList.Contains(currItem))
                    results.Remove(currItem);
            }

            // make sure all whitelist items are in the result list
            foreach (T item in this.WhiteList)
                if (!results.Contains(item) && input.Contains(item))
                    results.Add(item);

            return this.checkInversion(input, results);
        }

        private HashSet<T> checkInversion(ICollection<T> input, HashSet<T> filtered)
        {
            if (!this.Invert)
                return filtered;

            HashSet<T> output = new HashSet<T>();
            foreach (T currItem in input)
                if (!filtered.Contains(currItem))
                    output.Add(currItem);

            return output;
        }

        public bool Active
        {
            get { return this._Active; }
            set
            {
                if (this._Active != value)
                {
                    this._Active = value;

                    if (this.Updated != null)
                        this.Updated(this);
                }
            }
        }private bool _Active = true;

        public bool Invert
        {
            get { return this._Invert; }
            set
            {
                this._Invert = value;

                if (this.Updated != null)
                    this.Updated(this);
            }
        } private bool _Invert = false;

        #endregion

        #region Database Fields

        [DBField]
        public string Name
        {
            get { return this._Name; }
            set
            {
                this._Name = value;
                this._CommitNeeded = true;
            }
        } private string _Name;

        [DBField]
        public CriteriaGroupingEnum CriteriaGrouping
        {
            get { return this._CriteriaGrouping; }

            set
            {
                this._CriteriaGrouping = value;
                this._CommitNeeded = true;
            }
        } private CriteriaGroupingEnum _CriteriaGrouping;

        [DBRelation(AutoRetrieve = true)]
        public RelationList<DBFilter<T>, DBCriteria<T>> Criteria
        {
            get
            {
                if (this._Criteria == null)
                {
                    this._Criteria = new RelationList<DBFilter<T>, DBCriteria<T>>(this);
                }
                return this._Criteria;
            }
        } RelationList<DBFilter<T>, DBCriteria<T>> _Criteria;

        [DBRelation(AutoRetrieve = true, Identifier = "white_list")]
        public RelationList<DBFilter<T>, T> WhiteList
        {
            get
            {
                if (this._WhiteList == null)
                {
                    this._WhiteList = new RelationList<DBFilter<T>, T>(this);
                }
                return this._WhiteList;
            }
        } RelationList<DBFilter<T>, T> _WhiteList;

        [DBRelation(AutoRetrieve = true, Identifier = "black_list")]
        public RelationList<DBFilter<T>, T> BlackList
        {
            get
            {
                if (this._BlackList == null)
                {
                    this._BlackList = new RelationList<DBFilter<T>, T>(this);
                }
                return this._BlackList;
            }
        } RelationList<DBFilter<T>, T> _BlackList;


        #endregion

        public override void Delete()
        {
            base.Delete();

            foreach (DBCriteria<T> currCriteria in Criteria)
                currCriteria.Delete();
        }

        public override string ToString()
        {
            return Name;
        }
    }

    // empty interface to handle DBFilters generically
    public interface IDBFilter { }

    // empty interface to handle DBFilters generically
    public interface IGenericFilter { }
}
