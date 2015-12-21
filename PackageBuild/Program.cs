using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace PackageBuild
{
    class Program
    {
        const string DLLs = "*.dll";
        const string AllFiles = "*";
        const string binDebug = "bin/debug/";


        static string pathToNugetExe = @"C:\Nuget\nuget.exe";
        static string NugetAPIKey = "****";  
       

        static void Main(string[] args)
        {

            if (args.Length != 1)
            {
                Console.WriteLine("Only the version number as arg plx");
                Console.ReadKey(true);
            }
            else
            {

                string version = args[0];

                Console.WriteLine("Building version " + version);

                Directory.SetCurrentDirectory("..");
                Directory.SetCurrentDirectory("..");
                Directory.SetCurrentDirectory("..");

                string projectLocation = Directory.GetCurrentDirectory() + "/PowerProductivityStudio/";
                string baseLocation = Directory.GetCurrentDirectory() + "/builds/PowerProductivityStudio." + version + "/";
                string clientLocation = baseLocation + "ClientRefs/";
                string serverLocation = baseLocation + "ServerRefs/";
                string msBuildLocation = baseLocation + "MsBuild/";
                string clientNugetPackage = baseLocation + "ClientNugetPackage/";
                string serverNugetPackage = baseLocation + "ServerNugetPackage/";









                #region gather resources

                if (Directory.Exists(baseLocation))
                    Directory.Delete(baseLocation, true);
                Directory.CreateDirectory(baseLocation);


                cdir(projectLocation + "PowerProductivityStudio.Client/" + binDebug, clientLocation, true, DLLs);


                cdir(projectLocation + "PowerProductivityStudio.MSBuild/" + binDebug, msBuildLocation, true, DLLs);

                cFile(projectLocation + "PowerProductivityStudio.Server/" + binDebug + "PowerProductivityStudio.Server.dll", serverLocation);

                #endregion

                #region nuget
                cdir(msBuildLocation, serverNugetPackage + "tools/", true, DLLs);
                cdir(msBuildLocation, clientNugetPackage + "tools/", true, DLLs);
                cdir(clientLocation, clientNugetPackage + "lib/", true, DLLs);
                cdir(serverLocation, serverNugetPackage + "lib/", true, DLLs);
                mkDir(serverNugetPackage + "build");
                mkDir(clientNugetPackage + "build");

                Directory.SetCurrentDirectory(baseLocation);

                createNugetPackage(serverNugetPackage, "server", version);
                createNugetPackage(clientNugetPackage, "SLclient", version);

                #endregion

                #region cleanup
                Directory.Delete(clientLocation, true);
                Directory.Delete(serverLocation, true);
                Directory.Delete(msBuildLocation, true);
                #endregion

                #region publish or end?
                Console.WriteLine("Publish?");
                if (Console.ReadKey(true).KeyChar == 'y')
                {

                    var cmd = "push \"" + baseLocation + "PowerProductivityStudio.Server." + version + ".nupkg\" " + NugetAPIKey ;
                    Process.Start(pathToNugetExe, cmd).WaitForExit();

                    cmd = "push \"" + baseLocation + "PowerProductivityStudio.SLclient." + version + ".nupkg\" " + NugetAPIKey;
                    Process.Start(pathToNugetExe, cmd).WaitForExit();

                    Process.Start("https://www.nuget.org/account/Packages");

                }
                else
                {
                    Process.Start(baseLocation);
                }
                #endregion
            }
        }



        #region utility methods
        private static void createNugetPackage(string nugetPackageLocation, string name, string version)
        {
            File.WriteAllLines(nugetPackageLocation + "build/PowerProductivityStudio." + name + ".targets", new string[] { 
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine + 
                "<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +Environment.NewLine + 
                "  <UsingTask TaskName=\"NotifyUserCodeWeaverTask\" AssemblyFile=\"$(MSBuildProjectDirectory)\\..\\..\\packages\\PowerProductivityStudio." + name + "." + version + "\\tools\\PowerProductivityStudio.MSBuild.dll\" />" + Environment.NewLine + 
                "  <Target Name=\"AfterCompile\">" + Environment.NewLine + 
                "    <PowerProductivityStudio.MSBuild.NotifyUserCodeWeaverTask />" + Environment.NewLine + 
                "  </Target>" + Environment.NewLine + 
                "</Project>"
            });

            File.WriteAllLines(nugetPackageLocation + "PowerProductivityStudio." + name + "." + version + ".nuspec", new string[]{
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>"+ Environment.NewLine + 
                "<package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\">"+ Environment.NewLine + 
                "    <metadata>"+ Environment.NewLine + 
                "        <id>PowerProductivityStudio." + name + "</id>"+ Environment.NewLine + 
                "        <version>"+ version +"</version>"+ Environment.NewLine + 
                "        <authors>Blue Breeze Software, inc</authors>"+ Environment.NewLine +  
                "        <description>PPS contains a code weaving task that adds a generic entry for some common 'write code' points. Intellectual property of Blue Breeze Software, Inc. Usage is royalty free.</description>"+ Environment.NewLine +  
                "        <summary>PPS contains a code weaving task that adds a generic entry for some common 'write code' points.  </summary>"+ Environment.NewLine +  
                "        <releaseNotes>Keep rocking LS!</releaseNotes>"+ Environment.NewLine +  
                "        <language>en-US</language>"+ Environment.NewLine +   
                "        <requireLicenseAcceptance>false</requireLicenseAcceptance>"+ Environment.NewLine +  
                "    </metadata>"     + Environment.NewLine +
                "</package>"
       
            }); 
            var cmd = "pack " + nugetPackageLocation + "PowerProductivityStudio." + name + "." + version + ".nuspec";
            Process.Start(pathToNugetExe, cmd).WaitForExit();
        }

        private static void cFile(string fileName, string destDirName) {


            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }
            FileInfo file = new FileInfo(fileName);
            string temppath = Path.Combine(destDirName, file.Name);
            file.CopyTo(temppath, true);


        }

        private static void cdir(string sourceDirName, string destDirName, bool copySubDirs, string searchPattern)
        {
            Console.WriteLine("Copying '{0}' to '{1}'", sourceDirName, destDirName);
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            FileInfo[] files = dir.GetFiles(searchPattern);
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    cdir(subdir.FullName, temppath, copySubDirs, searchPattern);
                }
            }
        }

        private static void mkDir(string directoryName) {
            Directory.CreateDirectory(directoryName);
        }
        #endregion

    }
}
