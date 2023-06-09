﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using System.Windows.Forms;
using System.Threading;
using NLog;
using System.Collections;

namespace MediaPortal.Pbk.Cornerstone.Database.Tables
{
    public delegate void DBNodeEventHandler(IDBNode node);

    [DBTable("node")]
    public class DBNode<T> : DatabaseTable, IDBNode where T : DatabaseTable
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();
        private static Random _Random = new Random();

        public event DBNodeEventHandler Modified;

        private bool _Updating = false;


        public DBNode()
        {
            //Children.Changed += new ChangedEventHandler(RelationListChanged);
        }

        #region Database Fields
        [DBField]
        public string Name
        {
            get { return this._Name; }

            set
            {
                this._Name = value;
                this._CommitNeeded = true;

                this.OnModified();
            }
        }private string _Name;

        [DBField(Default = null)]
        public DBNode<T> Parent
        {
            get { return this._Parent; }
            set
            {
                this._Parent = value;
                this._CommitNeeded = true;
            }
        } private DBNode<T> _Parent;

        public IDBNode GenericParent
        {
            get { return this.Parent; }
        }

        [DBField]
        public DBField BasicFilteringField
        {
            get { return this._BasicFilteringField; }

            set
            {
                this._BasicFilteringField = value;
                this._CommitNeeded = true;

                this.OnModified();
            }
        }private DBField _BasicFilteringField = null;

        [DBField]
        public DBRelation BasicFilteringRelation
        {
            get { return this._BasicFilteringRelation; }

            set
            {
                this._BasicFilteringRelation = value;
                this._CommitNeeded = true;

                this.OnModified();
            }
        }private DBRelation _BasicFilteringRelation;

        [DBField]
        public bool AutoGenerated
        {
            get { return this._AutoGenerated; }

            set
            {
                this._AutoGenerated = value;
                this._CommitNeeded = true;

                this.OnModified();
            }
        }private bool _AutoGenerated = false;

        [DBField]
        public bool DynamicNode
        {
            get { return this._DynamicNode; }

            set
            {
                this._DynamicNode = value;
                this._CommitNeeded = true;

                this.OnModified();
            }
        }private bool _DynamicNode = false;

        [DBField(Default = null)]
        public DBFilter<T> Filter
        {
            get { return this._Filter; }

            set
            {
                this._Filter = value;
                this._CommitNeeded = true;

                this.OnModified();
            }
        } private DBFilter<T> _Filter = null;

        public bool HasFilter
        {
            get { return this.Filter != null; }
        }

        [DBRelation(AutoRetrieve = true, OneWay = true)]
        public RelationList<DBNode<T>, DBNode<T>> Children
        {
            get
            {
                if (this._children == null)
                {
                    this._children = new RelationList<DBNode<T>, DBNode<T>>(this);
                }
                return this._children;
            }
        }
        RelationList<DBNode<T>, DBNode<T>> _children;

        public bool HasChildren
        {
            get
            {
                return this.Children.Count != 0;
            }
        }

        [DBField]
        public DatabaseTable AdditionalSettings
        {
            get { return this._AdditionalSettings; }
            set
            {
                this._AdditionalSettings = value;
                this._CommitNeeded = true;
            }
        } private DatabaseTable _AdditionalSettings;

        [DBField]
        public int SortPosition
        {
            get { return this._SortPosition; }
            set
            {
                this._SortPosition = value;
                this._CommitNeeded = true;
            }
        } private int _SortPosition;

        #endregion

        public void OnModified()
        {
            if (this.Modified != null && !this._Updating)
                this.Modified(this);
        }

        public override void Delete()
        {
            if (this.DBManager == null)
                return;

            this.DBManager.BeginTransaction();

            base.Delete();

            if (this.Filter != null)
                this.Filter.Delete();

            foreach (DBNode<T> currSubNode in this.Children)
            {
                currSubNode.Delete();
            }

            this.DBManager.EndTransaction();
        }

        /// <summary>
        /// Returns all items that should be displayed if this node is selected.
        /// </summary>
        /// <returns></returns>
        public HashSet<T> GetFilteredItems()
        {

            // seed all items
            HashSet<T> results = new HashSet<T>(this.DBManager.Get<T>(null));

            // apply filters
            HashSet<IFilter<T>> filters = this.GetAllFilters();
            foreach (IFilter<T> filter in filters)
            {
                results = filter.Filter(results);
            }

            return results;
        }

        public HashSet<IFilter<T>> GetAllFilters()
        {
            HashSet<IFilter<T>> results = new HashSet<IFilter<T>>();

            // get the filters for all parent nodes
            DBNode<T> currNode = this;
            while (currNode != null)
            {
                if (currNode.Filter != null)
                    results.Add(currNode.Filter);

                currNode = currNode.Parent;
            }

            return results;
        }

        /// <summary>
        /// Returns a list of all items that could result from filtering from any 
        /// of the sub nodes of this node.
        /// </summary>
        /// <returns></returns>
        public HashSet<T> GetPossibleFilteredItems()
        {
            return this.GetPossibleFilteredItemsWorker(this.GetFilteredItems(), false);
        }

        private HashSet<T> GetPossibleFilteredItemsWorker(HashSet<T> existingItems, bool bApplyFilter)
        {
            if (bApplyFilter && this.Filter != null)
                existingItems = this.Filter.Filter(existingItems);

            if (this.Children.Count == 0)
                return existingItems;

            HashSet<T> results = new HashSet<T>();
            foreach (DBNode<T> currSubNode in this.Children)
            {
                foreach (T currItem in currSubNode.GetPossibleFilteredItemsWorker(existingItems, true))
                    results.Add(currItem);
            }

            return results;
        }

        public T GetRandomSubItem()
        {
            HashSet<T> possibleItems = this.GetPossibleFilteredItems();
            int iIndex = _Random.Next(possibleItems.Count);

            HashSet<T>.Enumerator enumerator = possibleItems.GetEnumerator();
            for (int i = 0; i <= iIndex; i++)
                enumerator.MoveNext();

            return enumerator.Current;
        }

        public void UpdateDynamicNode()
        {
            if (!this.DynamicNode)
                return;

            this._Updating = true;

            // try using a filtering helper for dynamic node maintenance
            if (this.DBManager.GetFilterHelper<T>() != null)
            {
                bool bSuccess = this.DBManager.GetFilterHelper<T>().UpdateDynamicNode(this);

                if (bSuccess)
                {
                    this.Children.Sort();

                    this._Updating = false;
                    this.OnModified();
                    return;
                }
            }


            this.UpdateDynamicNodeGeneric();
            this.Children.Sort();

            this._Updating = false;
            this.OnModified();
        }

        public void UpdateDynamicNodeGeneric()
        {
            // grab list of possible values
            HashSet<string> possibleValues = DBManager.GetAllValues(this.BasicFilteringField, this.BasicFilteringRelation, this.GetFilteredItems());

            // build lookup for subnodes and build list of nodes to remove
            List<DBNode<T>> toRemove = new List<DBNode<T>>();
            Dictionary<string, DBNode<T>> nodeLookup = new Dictionary<string, DBNode<T>>();
            foreach (DBNode<T> currSubNode in Children)
            {
                try
                {
                    if (!currSubNode.AutoGenerated)
                        continue;

                    if (currSubNode.Filter == null || currSubNode.Filter.Criteria == null || currSubNode.Filter.Criteria.Count == 0 ||
                        !possibleValues.Contains(currSubNode.Filter.Criteria[0].Value.ToString()))

                        toRemove.Add(currSubNode);
                    else
                        nodeLookup[currSubNode.Filter.Criteria[0].Value.ToString()] = currSubNode;
                }
                catch (Exception e)
                {
                    _Logger.ErrorException("[UpdateDynamicNodeGeneric] Unexpected error updating dynamic node.", e);
                }
            }

            // remove subnodes that are no longer valid
            foreach (DBNode<T> currSubNode in toRemove)
            {
                this.Children.Remove(currSubNode);
                currSubNode.Delete();
            }

            // add subnodes that are missing
            foreach (string strCurrValue in possibleValues)
            {
                if (nodeLookup.ContainsKey(strCurrValue))
                    continue;

                DBNode<T> newSubNode = new DBNode<T>();
                newSubNode.Name = strCurrValue;
                newSubNode.AutoGenerated = true;

                DBFilter<T> newFilter = new DBFilter<T>();
                DBCriteria<T> newCriteria = new DBCriteria<T>();
                newCriteria.Field = this.BasicFilteringField;
                newCriteria.Relation = this.BasicFilteringRelation;
                newCriteria.Operator = DBCriteria<T>.OperatorEnum.EQUAL;
                newCriteria.Value = strCurrValue;

                newFilter.Criteria.Add(newCriteria);
                newSubNode.Filter = newFilter;

                this.Children.Add(newSubNode);
                newSubNode.Parent = this;
            }
        }

        public override string ToString()
        {
            return "DBNode: " + this.Name + " (" + this.ID + ")";
        }

        public override int CompareTo(object obj)
        {
            int iRt = this.SortPosition.CompareTo(((DBNode<T>)obj).SortPosition);
            if (iRt == 0)
                return this.ToString().CompareTo(obj.ToString());
            else
                return iRt;
        }


        private void relationListChanged(object sender, EventArgs e)
        {
            //commitNeeded = true;
            this.OnModified();
        }
    }

    public interface IDBNode
    {
        string Name
        {
            get;
        }

        bool HasChildren
        {
            get;
        }

        bool HasFilter
        {
            get;
        }

        IDBNode GenericParent
        {
            get;
        }
    }
}
