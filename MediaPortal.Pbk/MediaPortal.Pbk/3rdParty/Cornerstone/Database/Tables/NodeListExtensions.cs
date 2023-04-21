using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Cornerstone.Database.Tables
{
    public static class NodeListExtensions
    {
        public static void Normalize<T>(this IList<DBNode<T>> list, bool bCommit) where T : DatabaseTable
        {
            int iIndex = 0;
            foreach (DBNode<T> currNode in list)
            {
                if (currNode.SortPosition != iIndex)
                {
                    currNode.SortPosition = iIndex;

                    if (bCommit) 
                        currNode.Commit();
                }

                iIndex++;
            }
        }

        public static bool MoveUp<T>(this List<DBNode<T>> list, DBNode<T> item, bool bCommit) where T : DatabaseTable
        {
            int iIndex = list.IndexOf(item);
            if (iIndex <= 0)
                return false;

            list.Reverse(iIndex - 1, 2);
            list.Normalize(bCommit);

            return true;
        }

        public static bool MoveDown<T>(this List<DBNode<T>> list, DBNode<T> item, bool bCommit) where T : DatabaseTable
        {
            int iIndex = list.IndexOf(item);
            if (iIndex >= list.Count - 1 || iIndex < 0)
                return false;

            list.Reverse(iIndex, 2);
            list.Normalize(bCommit);

            return true;
        }

        public static void Normalize<T>(this IList<DBNode<T>> list) where T : DatabaseTable
        {
            list.Normalize(false);
        }

        public static bool MoveUp<T>(this List<DBNode<T>> list, DBNode<T> item) where T : DatabaseTable
        {
            return list.MoveUp(item, false);
        }

        public static bool MoveDown<T>(this List<DBNode<T>> list, DBNode<T> item) where T : DatabaseTable
        {
            return list.MoveDown(item, false);
        }


    }
}
