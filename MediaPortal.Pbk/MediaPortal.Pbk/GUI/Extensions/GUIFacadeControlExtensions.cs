using MediaPortal.GUI.Library;
using NLog;
using System.Reflection;
using System;
using System.Collections.Generic;

namespace MediaPortal.Pbk.GUI.Extensions
{
    /// <summary>
    /// a set of extension methods for the MediaPortal GUIFacadeControl
    /// </summary>
    public static class GUIFacadeControlExtensions
    {

        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Get a value indicating wether the given control is related to this facade
        /// </summary>
        /// <param name="self"></param>
        /// <param name="parent">GUIControl instance to check</param>
        /// <returns>True if the control is related</returns>
        public static bool IsRelated(this GUIFacadeControl self, GUIControl parent)
        {
            return (parent == self || parent == self.FilmstripLayout() || parent == self.CoverFlowLayout || parent == self.ThumbnailLayout() || parent == self.ListLayout() || parent == self.AlbumListLayout());
        }

        /// <summary>
        /// Performs clear on the facade and all children lists
        /// </summary>
        /// <param name="self"></param>
        public static void ClearAll(this GUIFacadeControl self)
        {
            self.Clear();

            if (self.ListLayout() != null)
                self.ListLayout().Clear();

            if (self.ThumbnailLayout() != null)
                self.ThumbnailLayout().Clear();

            if (self.FilmstripLayout() != null)
                self.FilmstripLayout().Clear();

            if (self.AlbumListLayout() != null)
                self.AlbumListLayout().Clear();

            if (self.CoverFlowLayout != null)
                self.CoverFlowLayout.Clear();
        }

        /// <summary>
        /// Sets the specified visible property on the facade and all children lists
        /// </summary>
        /// <param name="self"></param>
        /// <param name="bValue"></param>
        public static void Visible(this GUIFacadeControl self, bool bValue)
        {
            self.Visible = bValue;

            if (self.ListLayout() != null)
                self.ListLayout().Visible = bValue;

            if (self.ThumbnailLayout() != null)
                self.ThumbnailLayout().Visible = bValue;

            if (self.AlbumListLayout() != null)
                self.AlbumListLayout().Visible = bValue;

            if (self.FilmstripLayout() != null)
                self.FilmstripLayout().Visible = bValue;

            if (self.CoverFlowLayout != null)
                self.CoverFlowLayout.Visible = bValue;
        }

        /// <summary>
        /// Selects the requested, or if not available the first, object of the specified type in the facade
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <param name="item">the object to select or null </param>
        /// <returns>selected object</returns>
        public static T SyncToFacade<T>(this GUIFacadeControl self, T item) where T : class
        {
            int i = 0;
            return self.SyncToFacade<T>(item, out i);
        }

        /// <summary>
        /// Selects the requested, or if not available the first, object of the specified type in the facade
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <param name="item">the object to select or null </param>
        /// <param name="iSelectedIndex">the index of the selection</param>
        /// <returns>selected object</returns>
        public static T SyncToFacade<T>(this GUIFacadeControl self, T item, out int iSelectedIndex) where T : class
        {
            lock (self)
            {

                object selectedItem = null;
                iSelectedIndex = -1;

                // no use in syncing when we got no items
                if (self.Count == 0)
                    return null;

                // Check if the current selected item already is the item we want
                // if so we don't have to do the routine check
                if (item != null & self.SelectedListItem != null && self.SelectedListItem.TVTag is T)
                {
                    if (self.SelectedListItem.TVTag == item)
                    {
                        selectedItem = self.SelectedListItem.TVTag;
                        iSelectedIndex = self.SelectedListItemIndex;
                    }
                }

                // Find the item in the facade and mark the first item found
                if (selectedItem == null)
                {
                    for (int i = 0; i < self.Count; i++)
                    {
                        if (self[i].TVTag is T)
                        {
                            // mark the first item found
                            if (iSelectedIndex == -1)
                            {
                                iSelectedIndex = i;
                            }
                            // if we found the item or had no selection break the loop;
                            if (item == null || item == self[i].TVTag)
                            {
                                iSelectedIndex = i;
                                selectedItem = self[i].TVTag;
                                break;
                            }
                        }
                    }

                    // if no item was found during the iteration we use the first item found
                    if (selectedItem == null)
                    {
                        if (iSelectedIndex == -1)
                            return null;

                        selectedItem = self[iSelectedIndex].TVTag;
                    }

                    // select the item in the facade
                    if (self.SelectedListItemIndex != iSelectedIndex)
                    {
                        _Logger.Debug("SyncToFacade<{0}>: Index={1}, Item={2}", typeof(T).Name, iSelectedIndex, selectedItem);
                        self.SelectIndex(iSelectedIndex);
                    }
                }

                // return the (new) selected item
                return selectedItem as T;
            }
        }

        /// <summary>
        /// Gets a value indicating wether the item is selected in the facade
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <param name="item"></param>
        /// <returns>True if the item is selected</returns>
        public static bool IsSelected<T>(this GUIFacadeControl self, T item) where T : class
        {
            if (item != null & self.SelectedListItem != null && self.SelectedListItem.TVTag is T)
            {
                return (self.SelectedListItem.TVTag == item);
            }

            return false;
        }

        /// <summary>
        /// Selects the specified item in the facade
        /// </summary>
        /// <param name="self"></param>
        /// <param name="iIndex">index of the item</param>
        public static void SelectIndex(this GUIFacadeControl self, int iIndex)
        {
            GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_ITEM_SELECT, self.WindowId, 0, self.GetID, iIndex, 0, null);
            GUIGraphicsContext.SendMessage(msg);
        }

        #region MediaPortal 1.1 <> 1.2 Compatibility

        private static Dictionary<string, PropertyInfo> _PropertyCache = new Dictionary<string, PropertyInfo>();

        /// <summary>
        /// Gets the property info object for a property using reflection.
        /// The property info object will be cached in memory for later requests.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="strNewName">The name of the property in 1.2</param>
        /// <param name="strOldName">The name of the property in 1.1</param>
        /// <returns>instance PropertyInfo or null if not found</returns>
        public static PropertyInfo GetPropertyInfo<T>(string strNewName, string strOldName)
        {
            PropertyInfo property = null;
            Type type = typeof(T);
            string strKey = type.FullName + "|" + strNewName;

            if (!_PropertyCache.TryGetValue(strKey, out property))
            {
                property = type.GetProperty(strNewName);
                if (property == null)
                {
                    property = type.GetProperty(strOldName);
                }

                _PropertyCache[strKey] = property;
            }

            return property;
        }


        public static List<GUIListItem> Items(this GUIFacadeControl self)
        {
            if (self.ListLayout() != null)
                return self.ListLayout().ListItems;

            if (self.AlbumListLayout() != null)
                return self.AlbumListLayout().ListItems;

            // if the skin does not implement the list control we are out of luck :(
            return null;
        }

        /// <summary>
        /// Acts the same as the ListLayout / ListView property.
        /// </summary>
        /// <remarks>this extension method was added to allow backwards compatibility with MediaPortal 1.1</remarks>
        /// <param name="self"></param>
        /// <returns>instance of GUIListControl or null</returns>
        public static GUIListControl ListLayout(this GUIFacadeControl self)
        {
            PropertyInfo property = GetPropertyInfo<GUIFacadeControl>("ListLayout", "ListView");
            return (GUIListControl)property.GetValue(self, null);
        }

        /// <summary>
        /// Acts the same as the FilmstripLayout / FilmstripView property.
        /// </summary>
        /// <remarks>this extension method was added to allow backwards compatibility with MediaPortal 1.1</remarks>
        /// <param name="self"></param>
        /// <returns>instance of GUIListControl or null</returns>
        public static GUIFilmstripControl FilmstripLayout(this GUIFacadeControl self)
        {
            PropertyInfo property = GetPropertyInfo<GUIFacadeControl>("FilmstripLayout", "FilmstripView");
            return (GUIFilmstripControl)property.GetValue(self, null);
        }

        /// <summary>
        /// Acts the same as the ThumbnailLayout / ThumbnailView property.
        /// </summary>
        /// <remarks>this extension method was added to allow backwards compatibility with MediaPortal 1.1</remarks>
        /// <param name="self"></param>
        /// <returns>instance of GUIListControl or null</returns>
        public static GUIThumbnailPanel ThumbnailLayout(this GUIFacadeControl self)
        {
            PropertyInfo property = GetPropertyInfo<GUIFacadeControl>("ThumbnailLayout", "ThumbnailView");
            return (GUIThumbnailPanel)property.GetValue(self, null);
        }

        /// <summary>
        /// Acts the same as the AlbumListLayout / AlbumListView property.
        /// </summary>
        /// <remarks>this extension method was added to allow backwards compatibility with MediaPortal 1.1</remarks>
        /// <param name="self"></param>
        /// <returns>instance of GUIListControl or null</returns>
        public static GUIListControl AlbumListLayout(this GUIFacadeControl self)
        {
            PropertyInfo property = GetPropertyInfo<GUIFacadeControl>("AlbumListLayout", "AlbumListView");
            return (GUIListControl)property.GetValue(self, null);
        }

        /// <summary>
        /// Acts the same as the CurrentLayout / View property.
        /// </summary>
        /// <remarks>this extension method was added to allow backwards compatibility with MediaPortal 1.1</remarks>
        /// <param name="self"></param>
        /// <returns>instance of GUIListControl or null</returns>
        public static void SetCurrentLayout(this GUIFacadeControl self, string layout)
        {
            PropertyInfo property = GetPropertyInfo<GUIFacadeControl>("CurrentLayout", "View");
            property.SetValue(self, Enum.Parse(property.PropertyType, layout), null);
        }

        #endregion

    }

}
