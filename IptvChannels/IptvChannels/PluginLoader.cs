#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using TvEngine;
using TvLibrary.Log;
using MediaPortal.Common.Utils;
using NLog;


namespace MediaPortal.IptvChannels
{
    internal class PluginLoader
    {
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        private readonly List<SiteUtils.SiteUtilBase> _Plugins = new List<SiteUtils.SiteUtilBase>();

        /// <summary>
        /// returns a list of all plugins loaded.
        /// </summary>
        /// <value>The plugins.</value>
        public List<SiteUtils.SiteUtilBase> Plugins
        {
            get { return _Plugins; }
        }

        /// <summary>
        /// Loads all plugins.
        /// </summary>
        public void Load()
        {
            this._Plugins.Clear();
            try
            {
                int iIdx = System.Reflection.Assembly.GetExecutingAssembly().Location.LastIndexOf("\\");
                string[] strFiles = System.IO.Directory.GetFiles(System.Reflection.Assembly.GetExecutingAssembly().Location.Substring(0, iIdx + 1) + "IptvChannelsPlugins\\", "*.dll");
                foreach (string strFile in strFiles)
                    this.LoadPlugin(strFile);
            }
            catch (Exception ex)
            {
                _Logger.Error("[Load] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }

        /// <summary>
        /// Loads the plugin.
        /// </summary>
        /// <param name="strFile">The STR file.</param>
        private void LoadPlugin(string strFile)
        {
            try
            {
                _Logger.Debug("[LoadPlugin] PluginManager: Loading:" + strFile);

                using (Stream stream = File.OpenRead(strFile))
                {
                    if (!ReferenceEquals(stream, null))
                    {
                        Byte[] assemblyData = new Byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);

                        //Assembly has to by loaded as 'Load' instead of 'LoadFrom' becouse the referencing assemblies are loaded as embeded resource
                        //otherwise GetExportedTypes() causes an exception of not implementhed method
                        Assembly assem = Assembly.Load(assemblyData);

                        if (assem != null)
                        {
                            Type[] types = assem.GetExportedTypes();

                            //_Logger.Debug("[LoadPlugin] Types:" + types.Length);

                            foreach (Type t in types)
                            {
                                try
                                {
                                    if (t.IsClass && t.BaseType != null && !t.IsAbstract)
                                    {
                                        try
                                        {
                                            if (this._Plugins.Exists(p => p.GetType().Name == t.Name))
                                            {
                                                _Logger.Error("[LoadPlugin] PluginManager: {0} already exists and won't be loaded!",
                                                    t.FullName);
                                                continue;
                                            }
                                            else
                                            {
                                                if (t.BaseType == typeof(SiteUtils.SiteUtilBase))
                                                {
                                                    SiteUtils.SiteUtilBase plugin = (SiteUtils.SiteUtilBase)Activator.CreateInstance(t);
                                                    this._Plugins.Add(plugin);
                                                    _Logger.Debug("[LoadPlugin] PluginManager: Loaded {0} version:{1} author:{2}",
                                                        plugin.Name, plugin.Version, plugin.Author);
                                                    return;
                                                }
                                                else
                                                {
                                                    _Logger.Debug("[LoadPlugin] Unkonwn type:" + t.BaseType.FullName);
                                                }
                                            }
                                        }
                                        catch (TargetInvocationException ex)
                                        {
                                            _Logger.Error(
                                            "[LoadPlugin] PluginManager: {0} is incompatible with the current plugin and won't be loaded! Exception:\r\n{1}",
                                            t.FullName, ex);
                                            continue;
                                        }
                                        catch (Exception ex)
                                        {
                                            _Logger.Error("[LoadPlugin] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                                        }
                                    }
                                }
                                catch (NullReferenceException) { }
                            }
                        }
                        else
                            _Logger.Error("[LoadPlugin] Invalid Assembly:" + strFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(
                "[LoadPlugin] PluginManager: Plugin file {0} is broken or incompatible with the current plugin and won't be loaded!",
                strFile.Substring(strFile.LastIndexOf(@"\") + 1));
                _Logger.Error("[LoadPlugin] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }
    }
}