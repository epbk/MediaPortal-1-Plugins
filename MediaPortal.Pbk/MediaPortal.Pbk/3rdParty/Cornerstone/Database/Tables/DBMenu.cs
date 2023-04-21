using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;

namespace MediaPortal.Pbk.Cornerstone.Database.Tables
{
    [DBTable("menu")]
    public class DBMenu<T> : DatabaseTable, IDBMenu where T : DatabaseTable
    {
        public DBMenu()
        {
            this.RootNodes.Changed += new ChangedEventHandler(this.rootNodes_Changed);
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
            }
        }private string _Name;

        [DBRelation(AutoRetrieve = true)]
        public RelationList<DBMenu<T>, DBNode<T>> RootNodes
        {
            get
            {
                if (this._RootNodes == null)
                {
                    this._RootNodes = new RelationList<DBMenu<T>, DBNode<T>>(this);
                }
                return this._RootNodes;
            }
        }RelationList<DBMenu<T>, DBNode<T>> _RootNodes;

        #endregion

        #region Helper Methods

        public List<DBNode<T>> FindNode(string strNodeName)
        {
            return this.findNode(strNodeName, RootNodes);
        }

        private List<DBNode<T>> findNode(string strNodeName, IList<DBNode<T>> nodes)
        {
            List<DBNode<T>> results = new List<DBNode<T>>();
            foreach (DBNode<T> currNode in nodes)
            {
                // check if this is the node we are looking for
                if (currNode.Name == strNodeName)
                    results.Add(currNode);

                // recursively search the children
                if (currNode.Children.Count > 0)
                    results.AddRange(findNode(strNodeName, currNode.Children));
            }

            return results;
        }

        #endregion

        private void rootNodes_Changed(object sender, EventArgs e)
        {
            this._CommitNeeded = true;
        }
    }

    public interface IDBMenu { }
}
