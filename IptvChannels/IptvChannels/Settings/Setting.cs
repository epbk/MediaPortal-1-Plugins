using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.ComponentModel;
using System.Reflection;
using NLog;
using MediaPortal.Pbk.Cornerstone.Database;

namespace MediaPortal.IptvChannels.Settings
{
    public class Setting
    {
        #region Variables
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private Plugin _Plugin;
        #endregion

        #region ctor
        static Setting()
        {
        }

        public Setting(Plugin plugin)
        {
            this._Plugin = plugin;
        }
        #endregion

        #region Public methods
        public void Load()
        {
            XmlDocument xmldoc = new XmlDocument();

            if (File.Exists(GetConfigPath()))
            {
                try
                {
                    xmldoc.Load(GetConfigPath());

                    //Directory list
                    XmlNodeList siteList = xmldoc.SelectNodes("//IptvChannels/Sites/Site[@name]");
                    foreach (XmlNode siteNode in siteList)
                    {
                        foreach (SiteUtils.SiteUtilBase site in this._Plugin.Sites)
                        {
                            if (site.Name == siteNode.Attributes["name"].Value)
                            {
                                //Load all properties
                                IEnumerable<PropertyInfo> properties = site.GetType().GetProperties().Where(prop => prop.IsDefined(typeof(DBFieldAttribute), false));
                                foreach (PropertyInfo p in properties)
                                {
                                    if (p.Name != "Version" && p.Name != "Author" && p.Name != "Description")
                                    {
                                        object[] atr = p.GetCustomAttributes(typeof(DBFieldAttribute), false);
                                        if (atr != null && atr.Length > 0)
                                        {

                                            XmlNode pNode = siteNode.SelectSingleNode("./" + p.Name + "/text()");
                                            try
                                            {
                                                if (pNode != null)
                                                {
                                                    if (p.PropertyType.IsEnum)
                                                    {
                                                        p.SetValue(site, Enum.Parse(p.PropertyType, pNode.Value), null);
                                                    }
                                                    else
                                                    {
                                                        p.SetValue(site, Convert.ChangeType(pNode.Value, p.PropertyType), null);
                                                    }

                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _Logger.Error("[Load] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                                            }
                                        }
                                    }
                                }

                                site.ChannelSettings = siteNode.SelectSingleNode("./ChannelList");

                                //Next site
                                break;
                            }
                        }
                    }

                }

                catch (Exception ex)
                {

                    _Logger.Error("[Load] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                }

            }
            else
            {
                //XML file does not exist
                //Create default XML file
                this.Save();
            }

        }

        public void Save()
        {
            XmlDocument xmldoc = new XmlDocument();

            try
            {

                //Write down the XML declaration
                XmlDeclaration xmlDeclaration = xmldoc.CreateXmlDeclaration("1.0", "utf-8", "");

                //Create the root element
                XmlElement rootNode = xmldoc.CreateElement("IptvChannels");
                xmldoc.InsertBefore(xmlDeclaration, xmldoc.DocumentElement);
                xmldoc.AppendChild(rootNode);

                //Sites
                XmlElement siteList = xmldoc.CreateElement("Sites");
                rootNode.AppendChild(siteList);

                foreach (SiteUtils.SiteUtilBase site in this._Plugin.Sites)
                {
                    XmlElement siteNode = xmldoc.CreateElement("Site");

                    XmlAttribute atrName = xmldoc.CreateAttribute("name");
                    atrName.Value = site.Name;
                    siteNode.Attributes.Append(atrName);
                    siteList.AppendChild(siteNode);

                    //Save all properties
                    IEnumerable<PropertyInfo> properties = site.GetType().GetProperties().Where(prop => prop.IsDefined(typeof(DBFieldAttribute), false));
                    foreach (PropertyInfo p in properties)
                    {
                        if (p.Name != "Version" && p.Name != "Author" && p.Name != "Description")
                        {
                            object[] atr = p.GetCustomAttributes(typeof(DBFieldAttribute), false);
                            if (atr != null && atr.Length > 0)
                            {
                                AppendValue(xmldoc, siteNode, p.Name, p.GetValue(site, null).ToString());
                            }
                        }
                    }

                    //Export channel's settings
                    site.ExportChannels(xmldoc, siteNode.AppendChild(xmldoc.CreateElement("ChannelList")));
                }

                xmldoc.Save(GetConfigPath());
            }
            catch (Exception ex)
            {
                _Logger.Error("[Save] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }

        #endregion

        #region Private methods
        private static string GetConfigPath()
        {
            return TvLibrary.Log.Log.GetPathName() + @"\IptvChannels.xml";
        }

        private static bool TryParse(XmlNode param, bool defval)
        {
            bool bVal = defval;
            if (param != null) bool.TryParse(param.Value, out bVal);
            return bVal;
        }
        private static bool? TryParse(XmlNode param, bool? defval)
        {
            bool? bVal = defval;
            if (param != null)
            {
                bool bTmp;
                if (bool.TryParse(param.Value, out bTmp)) bVal = bTmp;
            }
            return bVal;
        }
        private static byte TryParse(XmlNode param, byte defval, System.Globalization.NumberStyles format)
        {
            byte bVal = defval;
            if (param != null) byte.TryParse(param.Value, format, null, out bVal);
            return bVal;
        }
        private static int TryParse(XmlNode param, int defval, System.Globalization.NumberStyles format)
        {
            int bVal = defval;
            if (param != null) int.TryParse(param.Value, format, null, out bVal);
            return bVal;
        }
        private static ulong TryParse(XmlNode param, ulong defval, System.Globalization.NumberStyles format)
        {
            ulong bVal = defval;
            if (param != null) ulong.TryParse(param.Value, format, null, out bVal);
            return bVal;
        }
        private static string TryParse(XmlNode param, string defval)
        {
            string bVal = defval;
            if (param != null) bVal = param.Value;
            return bVal;
        }

        private static void AppendValue(XmlDocument xml, XmlElement node, string name, string value)
        {
            AppendValue(xml, node, name, value, null);
        }
        private static void AppendValue(XmlDocument xml, XmlElement node, string name, string value, List<XmlAttribute> atr)
        {
            XmlElement element = xml.CreateElement(name);
            element.AppendChild(xml.CreateTextNode(!string.IsNullOrEmpty(value) ? value : ""));
            if (atr != null)
            {
                foreach (XmlAttribute a in atr) element.Attributes.Append(a);
            }
            node.AppendChild(element);
        }
        private static void AppendValueWithAttr(XmlDocument xml, XmlElement node, string name, string value, XmlAttribute[] atr)
        {
            XmlElement element = xml.CreateElement(name);
            element.AppendChild(xml.CreateTextNode(!string.IsNullOrEmpty(value) ? value : ""));
            if (atr != null)
            {
                foreach (XmlAttribute a in atr) element.Attributes.Append(a);
            }
            node.AppendChild(element);
        }
        private static void AppendValueWithAttr(XmlDocument xml, XmlElement node, string name, string value, XmlAttribute atr)
        {
            XmlElement element = xml.CreateElement(name);
            element.AppendChild(xml.CreateTextNode(!string.IsNullOrEmpty(value) ? value : ""));
            if (atr != null)
            {
                element.Attributes.Append(atr);
            }
            node.AppendChild(element);
        }
        #endregion
    }
}
