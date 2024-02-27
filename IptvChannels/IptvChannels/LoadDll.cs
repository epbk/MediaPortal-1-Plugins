using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace MediaPortal.IptvChannels
{
    static class LoadDll
    {
        static bool _init = false;
        static LoadDll()
        {
            LoadDll.InitDll();
        }

        internal static void InitDll()
        {
            if (_init) return;
            TvLibrary.Log.Log.Debug(string.Format("[IptvChannels][Load Dll] Init"));
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                return LoadAssembly(args.Name);
            };
            _init = true;
        }

        private static Assembly LoadAssembly(string strDllName)
        {
            String resourceName = "MediaPortal." + Assembly.GetExecutingAssembly().GetName().Name + ".external." + new AssemblyName(strDllName).Name + ".dll";

            try
            {
                if (resourceName.EndsWith("NLog.dll") || resourceName.EndsWith("SgmlReaderDll.dll"))
                {
                    TvLibrary.Log.Log.Debug(string.Format("[IptvChannels][LoadAssembly] Loading: {0}", resourceName));

                    string[] names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                    foreach (string name in names) TvLibrary.Log.Log.Debug(string.Format("[IptvChannels][Load Dll] Available resource: {0}", name));

                    using (System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                        {
                            TvLibrary.Log.Log.Error(string.Format("[IptvChannels][LoadAssembly] Missing assembly: {0}", resourceName));
                            return null;
                        }
                        Byte[] assemblyData = new Byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);
                        TvLibrary.Log.Log.Debug(string.Format("[IptvChannels][LoadAssembly] Assembly found: {0}", resourceName));
                        return Assembly.Load(assemblyData);
                    }
                }
            }
            catch { TvLibrary.Log.Log.Error(string.Format("[IptvChannels][LoadAssembly] Error loading assembly: {0}", resourceName)); }

            return null;
        }
    }
}
