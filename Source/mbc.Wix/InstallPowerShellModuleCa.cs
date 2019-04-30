//-----------------------------------------------------------------------------
// Copyright (c) 2019 by mbc engineering GmbH, CH-6015 Luzern
// Licensed under the Apache License, Version 2.0
//-----------------------------------------------------------------------------

using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;

namespace mbc.Wix
{
    /// <summary>
    /// Custom actions for install/uninstall process.
    /// </summary>
    public static class InstallPowerShellModuleCa
    {
        [CustomAction]
        public static ActionResult DeferredInstallPowerShellModule(Session session)
        {
            session.Log("Begin to install powerShell module into all user profile");

            var psInstaller = CreatePsInstaller(session);

            // Check prerequisites
            psInstaller.CheckPowerShellPrerequisites();

            // Install
            var data = session.CustomActionData;
            var powerShellModuleLibraryPath = data["PowershellModuleLibraryPath"];
            psInstaller.AddModule(powerShellModuleLibraryPath);

            session.Log("PowerShell module installation is done");
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DeferredUnInstallPowerShellModule(Session session)
        {
            session.Log("Begin to uninstall powerShell module form all user profile");

            var psInstaller = CreatePsInstaller(session);

            // Check prerequisites
            psInstaller.CheckPowerShellPrerequisites();

            // Uninstall
            var data = session.CustomActionData;
            var powerShellModuleLibraryPath = data["PowershellModuleLibraryPath"];
            psInstaller.RemoveModule(powerShellModuleLibraryPath);

            session.Log("PowerShell module uninstalling is done");
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult DeferredCheckPowerShellVersion(Session session)
        {
            session.Log("Begin to check prerequisite of PowerShell");

            var psInstaller = CreatePsInstaller(session);
            try
            {
                psInstaller.CheckPowerShellPrerequisites();
            }
            catch (Exception e)
            {
                session.Message(InstallMessage.Error, new Record(1) { [0] = e.Message });
                return ActionResult.Failure;
            }

            return ActionResult.Success;
        }

        private static PowerShellModuleInstaller CreatePsInstaller(Session session)
        {
            // Read Configuration values
            var data = session.CustomActionData;
            var minPowerShellMajorVersion = 5;
            if (data.ContainsKey("MinPowerShellMajorVersion") && int.TryParse(data["MinPowerShellMajorVersion"], out var minPsVersion))
            {
                minPowerShellMajorVersion = minPsVersion;
            }

            return new PowerShellModuleInstaller(session, minPowerShellMajorVersion);
        }

        private class PowerShellModuleInstaller
        {
            private const string ProfilePath = "WindowsPowerShell\\v1.0\\Microsoft.PowerShell_profile.ps1";

            private readonly Session _session;
            private readonly int _minPowerShellMajorVersion;

            public PowerShellModuleInstaller(Session session, int minPowerShellMajorVersion = 5)
            {
                _session = session;
                _minPowerShellMajorVersion = minPowerShellMajorVersion;
            }

            public void CheckPowerShellPrerequisites()
            {
                _session.Log($"Check PowerShell Major-Version {_minPowerShellMajorVersion} exist.");

                // Check that PS is installed
                if (!PowerShellExists() || GetPowerShellVersion() < _minPowerShellMajorVersion)
                {
                    var errorMsg = $"PowerShell Major-Version {_minPowerShellMajorVersion} must be installed.";
                    _session.Log(errorMsg);
                    throw new Exception(errorMsg);
                }
            }

            public void AddModule(string powerShellModuleLibraryPath)
            {
                // If OS is x64
                if (Environment.Is64BitOperatingSystem)
                {
                    var systemFolderX86 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86, Environment.SpecialFolderOption.DoNotVerify);
                    var allUserHostProfileX86Path = Path.Combine(systemFolderX86, ProfilePath);

                    // Install x86 on a x64 Windows
                    AddModuleImportIfNotExist(allUserHostProfileX86Path, powerShellModuleLibraryPath);
                }

                // Install x64 on a x64 msi or x86 on a x86 msi.
                var systemFolder = Environment.SystemDirectory;
                if (!Environment.Is64BitProcess)
                {
                    // If MSI is  x32
                    systemFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows, Environment.SpecialFolderOption.DoNotVerify), "sysnative"); // For redirection to c:\windows\system32
                }

                var allUserHostProfilePath = Path.Combine(systemFolder, ProfilePath);
                AddModuleImportIfNotExist(allUserHostProfilePath, powerShellModuleLibraryPath);
            }

            public void RemoveModule(string powerShellModuleLibraryPath)
            {
                // If OS is x64
                if (Environment.Is64BitOperatingSystem)
                {
                    var systemFolderX86 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86, Environment.SpecialFolderOption.DoNotVerify);
                    var allUserHostProfileX86Path = Path.Combine(systemFolderX86, ProfilePath);

                    // remove x86 on a x64 Windows
                    RemoveModuleImport(allUserHostProfileX86Path, powerShellModuleLibraryPath);
                }

                // remove x64 on a x64 msi or x86 on a x86 msi.
                var systemFolder = Environment.SystemDirectory;
                if (!Environment.Is64BitProcess)
                {
                    // If MSI is  x32
                    systemFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows, Environment.SpecialFolderOption.DoNotVerify), "sysnative"); // For redirection to c:\windows\system32
                }

                var allUserHostProfilePath = Path.Combine(systemFolder, ProfilePath);
                RemoveModuleImport(allUserHostProfilePath, powerShellModuleLibraryPath);
            }

            private void AddModuleImportIfNotExist(string allUserHostProfilePath, string powerShellModuleLibraryPath)
            {
                List<string> content;
                if (File.Exists(allUserHostProfilePath))
                {
                    content = File.ReadAllLines(allUserHostProfilePath).ToList();
                    var entry = content.FirstOrDefault(x => x.Contains(powerShellModuleLibraryPath));
                    if (!string.IsNullOrWhiteSpace(entry))
                    {
                        // Exists already
                        _session.Log($"PowerShell module already registered in {allUserHostProfilePath}");
                        return;
                    }
                }
                else
                {
                    content = new List<string>();
                    using (var fs = File.Create(allUserHostProfilePath))
                    {
                        fs.Close();
                    }

                    _session.Log($"PowerShell module file {allUserHostProfilePath} are now created.");
                }

                // Import entry
                var path = powerShellModuleLibraryPath.Replace(@"\\", @"\");
                content.Add("Import-Module " + $"'{path}'");
                File.WriteAllLines(allUserHostProfilePath, content);
                _session.Log($"PowerShell module {powerShellModuleLibraryPath} in file {allUserHostProfilePath} are now added.");
            }

            private void RemoveModuleImport(string allUserHostProfilePath, string powerShellModuleLibraryPath)
            {
                if (File.Exists(allUserHostProfilePath))
                {
                    var content = File.ReadAllLines(allUserHostProfilePath).ToList();
                    var entry = content.FirstOrDefault(x => x.Contains(powerShellModuleLibraryPath));

                    if (!string.IsNullOrWhiteSpace(entry))
                    {
                        content.Remove(entry);
                        File.WriteAllLines(allUserHostProfilePath, content);
                        _session.Log($"PowerShell module {powerShellModuleLibraryPath} in file {allUserHostProfilePath} are now removed.");
                    }
                }
            }

            private bool PowerShellExists()
            {
                var regval = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PowerShell\1", "Install", null).ToString();
                var exist = regval.Equals("1");
                _session.Log($"PowerShell is {(exist ? string.Empty : "NOT")} installed on target machine");
                return exist;
            }

            private int GetPowerShellVersion()
            {
                var psinstance = PowerShell.Create();
                psinstance.AddScript("$PSVersionTable");
                dynamic vt = psinstance.Invoke().Single();
                var powerShellVersion = (int)vt.PSVersion.Major;

                _session.Log($"PowerShell version '{powerShellVersion}' is installed on target machine");
                return powerShellVersion;
            }
        }
    }
}
