using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Sequlite.WPF.Framework
{
    /// <summary>
    /// Simplifies the creation of folders in the CommonApplicationData folder
    /// and setting of permissions for all users.
    /// </summary>
    public class CommonApplicationData
    {
        private string _ApplicationFolder;
        private string _CompanyFolder;
        private static readonly string _BaseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        /// <summary>
        /// Gets the path of the application's data folder.    
        /// </summary>
        public string ApplicationFolderPath
        {
            get { return Path.Combine(CompanyFolderPath, _ApplicationFolder); }
        }

        /// <summary>
        /// Gets the path of the company's data folder.    
        /// </summary>
        public string CompanyFolderPath
        {
            get { return Path.Combine(_BaseDirectory, _CompanyFolder); }
        }



        /// <summary>
        /// Creates a new instance of this class creating the specified company and application folders
        /// if they don't already exist and optionally allows write/modify to all users.    
        /// </summary>    
        /// <param name="companyFolder">The name of the company's folder (normally the company name).</param>    
        /// <param name="applicationFolder">The name of the application's folder (normally the application name).</param>    
        /// <remarks>If the application folder already exists then permissions if requested are NOT altered.</remarks>
        public CommonApplicationData(string companyFolder, string applicationFolder)
           : this(companyFolder, applicationFolder, false)
        {
        }

        /// <summary>
        /// Creates a new instance of this class creating the specified company and application folders
        /// if they don't already exist and optionally allows write/modify to all users.    
        /// </summary>    
        /// <param name="companyFolder">The name of the company's folder (normally the company name).</param>    
        /// <param name="applicationFolder">The name of the application's folder (normally the application name).</param>    
        /// <param name="allUsers">true to allow write/modify to all users; otherwise, false.</param>    
        /// <remarks>If the application folder already exists then permissions if requested are NOT altered.</remarks>
        public CommonApplicationData(string companyFolder, string applicationFolder, bool allUsers)
        {
            this._ApplicationFolder = applicationFolder;
            this._CompanyFolder = companyFolder;
            CreateFolders(allUsers);
        }

        /// <summary>
        /// Worker method.
        /// </summary>
        /// <param name="allUsers"></param>
        private void CreateFolders(bool allUsers)
        {
            DirectoryInfo directoryInfo;
            DirectorySecurity directorySecurity;
            AccessRule rule;
            SecurityIdentifier securityIdentifier = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            bool modified;

            if (!Directory.Exists(CompanyFolderPath))
            {
                directoryInfo = Directory.CreateDirectory(CompanyFolderPath);
                directorySecurity = directoryInfo.GetAccessControl();
                rule = new FileSystemAccessRule(
                               securityIdentifier,
                               FileSystemRights.Write |
                               FileSystemRights.ReadAndExecute |
                               FileSystemRights.Modify,
                               AccessControlType.Allow);
                directorySecurity.ModifyAccessRule(AccessControlModification.Add, rule, out modified);
                directoryInfo.SetAccessControl(directorySecurity);
            }
            //else
            //{
            //   directoryInfo = new DirectoryInfo(CompanyFolderPath);
            //}


            if (!Directory.Exists(ApplicationFolderPath))
            {
                directoryInfo = Directory.CreateDirectory(ApplicationFolderPath);
                if (allUsers)
                {
                    directorySecurity = directoryInfo.GetAccessControl();
                    rule = new FileSystemAccessRule(
                                   securityIdentifier,
                                   FileSystemRights.Write |
                                   FileSystemRights.ReadAndExecute |
                                   FileSystemRights.Modify,
                                   InheritanceFlags.ContainerInherit |
                                   InheritanceFlags.ObjectInherit,
                                   PropagationFlags.InheritOnly,
                                   AccessControlType.Allow);
                    directorySecurity.ModifyAccessRule(AccessControlModification.Add, rule, out modified);
                    directoryInfo.SetAccessControl(directorySecurity);
                }
            }
            //else
            //{
            //   directoryInfo = new DirectoryInfo(ApplicationFolderPath);
            //}


            //if (allUsers)
            //{
            //   directorySecurity = directoryInfo.GetAccessControl();
            //   rule = new FileSystemAccessRule(
            //                  securityIdentifier,
            //                  FileSystemRights.Write |
            //                  FileSystemRights.ReadAndExecute |
            //                  FileSystemRights.Modify,
            //                  InheritanceFlags.ContainerInherit |
            //                  InheritanceFlags.ObjectInherit,
            //                  PropagationFlags.InheritOnly,
            //                  AccessControlType.Allow);
            //   directorySecurity.ModifyAccessRule(AccessControlModification.Add, rule, out modified);
            //   directoryInfo.SetAccessControl(directorySecurity);
            //}

        }

        /// <summary>
        /// Returns the path of the application's data folder.    
        /// </summary>    
        /// <returns>The path of the application's data folder.</returns>
        public override string ToString()
        {
            return ApplicationFolderPath;
        }

    }



}
