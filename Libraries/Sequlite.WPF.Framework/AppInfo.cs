using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Sequlite.WPF.Framework
{

    public sealed class AppInfo
    {
        private static object _SyncRoot = new object();
        private static volatile AppInfo _AppInfoInstance = null;

        private List<AssemblyDisplayInfo> _AssemblyInfo = new List<AssemblyDisplayInfo>();

        #region public static AppInfo Instance
        /// <summary>
        /// Get the single <see cref="AppInfo"/> instance.
        /// </summary>
        public static AppInfo Instance
        {
            get
            {
                if (_AppInfoInstance == null)
                {
                    lock (_SyncRoot)
                    {
                        if (_AppInfoInstance == null)
                            _AppInfoInstance = new AppInfo();
                    }
                }

                _AppInfoInstance.GetAssemblies();

                return _AppInfoInstance;
            }
        }
        #endregion

        #region public List<AssemblyDisplayInfo> AssemblyInfo
        /// <summary>
        /// 
        /// </summary>
        public List<AssemblyDisplayInfo> AssemblyInfo
        {
            get { return _AssemblyInfo; }
        }
        #endregion


        #region Constructors...
        private AppInfo()
        {
        }
        #endregion


        /// <summary>
        /// Get information of the applications loaded assemblies.
        /// </summary>
        public void GetAssemblies()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Version version = assembly.GetName().Version;
                string versionStr = string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
                FileInfo fInfo = new FileInfo(assembly.Location);
                AssemblyDisplayInfo assemInfo = new AssemblyDisplayInfo();
                assemInfo.FullName = assembly.FullName;
                assemInfo.Version = versionStr;
                assemInfo.Path = assembly.Location;

                if (fInfo.Exists)
                {
                    assemInfo.LastModifiedDate = fInfo.LastWriteTime.ToLongDateString();
                }
                _AssemblyInfo.Add(assemInfo);
            }
        }

    }

    public class AssemblyDisplayInfo
    {
        public string FullName { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
        public string LastModifiedDate { get; set; }
    }

}
