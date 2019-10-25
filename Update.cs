using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Avantgarde.Lib;
using System.Diagnostics;

namespace Avantgarde.Bin
{
    public class Update
    {
        FileManifest Manifest { get; set; }
        Settings SettingsFile { get; }

        public Update(string OriginalAppPath)
        {
            SettingsFile = Settings.Load();
            SettingsFile.OriginalAppPath = OriginalAppPath;
            string filesJson = File.ReadAllText(Settings.FILES_FILENAME);
            Manifest = JsonConvert.DeserializeObject<FileManifest>(filesJson);
            try
            {
                FetchFiles();
                TerminateApp();
                UpdateFiles();
                UpdateVersion();                
                ReloadSourceApp();
                Utils.Log("Update completed.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Utils.Log(ex.Message, Utils.MsgType.error);
            }
        }       
      
        void FetchFiles()
        {
            DownloadManager dm = new DownloadManager();
            Utils.Log("Downloading remote files...");
            if (Manifest.Files == null)
            {
                Utils.Log("The list of files is null.", Utils.MsgType.error);
                Environment.Exit(2);
            }
            if(Manifest.Files.Count == 0)
            {
                Utils.Log("No files to fetch.");
                return;
            }
            if(!Directory.Exists(SettingsFile.TempDir))
            {
                Directory.CreateDirectory(SettingsFile.TempDir);
            }
            if(SettingsFile.IsArchive)
            {
                bool gotMaster = false;
                foreach(AGFile f in Manifest.Files)
                {                    
                    if(f.Tags.Contains("master"))
                    {
                        gotMaster = true;
                        if (f.Source.StartsWith("http"))
                        {
                            dm.DownloadRemoteFile(f.Source, SettingsFile.TempDir + Path.GetFileName(f.Destination));
                            Utils.Log("Decompressing master archive...");
                            ZipFile.ExtractToDirectory(SettingsFile.TempDir + Path.GetFileName(f.Destination), SettingsFile.TempDir);
                        }                                
                    }
                }

                if (!gotMaster)
                {
                    // If no master file is found and archive is true, exit non-zero
                    Utils.Log("Need a master file when IsArchive is true.", Utils.MsgType.error);
                    Environment.Exit(2);
                }
            }
            // Download the remainder of the files.
            foreach (AGFile f in Manifest.Files)
            {
                if (f.Source.StartsWith("http"))
                {
                    if (f.Tags.Contains("master")) { continue; } // Skip the master file
                    dm.DownloadRemoteFile(f.Source, SettingsFile.TempDir + Path.GetFileName(f.Destination));
                }
            }
        }

        // All source files are relative to temp dir
        // All destination are relative to appdir unless "static" tag
        // "skipExisting" wont overwrite
        // "master" will unpack first to temp dir then move other files.

        // TODO : Need a createDir (to create dir if not existing)
        void UpdateFiles()
        {
            Utils.Log("Moving files...");
            foreach (AGFile f in Manifest.Files)
            {
                // If the path is not tagged static, we append the original app path to ensure relative path structure.
                if (!f.Tags.Contains("static"))
                {
                    f.Destination = SettingsFile.OriginalAppPath + f.Destination;
                }

                // Creating target directories is the default behaviour
                if (!Directory.Exists(Path.GetDirectoryName(f.Destination)))
                {
                    if (!f.Tags.Contains("dontCreateTargetDir"))
                    {
                        Utils.Log($"Creating missing directory {Path.GetDirectoryName(f.Destination)}...");
                        Directory.CreateDirectory(Path.GetDirectoryName(f.Destination));
                    }
                }

                // Overwrite is the default behaviour
                if (File.Exists(f.Destination) && f.Tags.Contains("skipExisting"))
                {
                    Utils.Log($"Skipping {f.Destination}, already exists and is marked 'skipExisting'.");
                    continue;
                }
                else if(f.Source.StartsWith("http")) // Path is remote
                {
                    File.Copy(SettingsFile.TempDir + Path.GetFileName(f.Source), f.Destination, true);
                    Utils.Log($"Updated {f.Destination}");
                }
                else // Source path is static
                {
                    File.Copy(f.Source, f.Destination, true);
                    Utils.Log($"Updated {f.Destination}");
                }
            }
            Utils.Log("Cleaning up...");
            if (Directory.Exists(SettingsFile.TempDir))
            {
                Directory.Delete(SettingsFile.TempDir, true);
            }
        }    
        void TerminateApp()
        {
            if (SettingsFile.CloseMethod == "none") return;
            bool kill = false;
            bool waitAndKill = false;
            if (SettingsFile.CloseMethod == "kill")
                kill = true;
            if (SettingsFile.CloseMethod == "waitAndKill")
                waitAndKill = true;
            Utils.CloseProcess(SettingsFile.ExeName, kill, waitAndKill);
        }
    
        void ReloadSourceApp()
        {
            if (!SettingsFile.Relaunch) return;
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.WorkingDirectory = SettingsFile.OriginalAppPath;
            psi.FileName = SettingsFile.ExeName;
            Process.Start(psi);
            Environment.Exit(0);
        }
    
        void UpdateVersion()
        {
            SettingsFile.CurrentVersion = Manifest.TargetVersion;
            Settings.Save(SettingsFile, SettingsFile.OriginalAppPath + Settings.SETTINGS_FILENAME);
        }
    }
}
