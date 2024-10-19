using System;
using System.IO;

namespace FolderSyncUtility
{
    public static class FolderSync
    {
        private static readonly string LastSyncFilePath = "lastSync.txt";

        public static void SyncFolders(string sourceFolder, string targetFolder, bool isPreview, string[] excludePatterns)
        {
            DateTime lastSyncTime = GetLastSyncTime();
            PerformSync(sourceFolder, targetFolder, lastSyncTime, isPreview, excludePatterns);
        }

        private static DateTime GetLastSyncTime()
        {
            if (File.Exists(LastSyncFilePath))
            {
                string lastSyncTimeStr = File.ReadAllText(LastSyncFilePath);
                if (DateTime.TryParse(lastSyncTimeStr, out DateTime lastSyncTime))
                {
                    return lastSyncTime;
                }
            }
            return DateTime.MinValue;
        }

        public static void UpdateLastSyncTime()
        {
            File.WriteAllText(LastSyncFilePath, DateTime.Now.ToString());
        }

        public static void ResetLastSyncTime()
        {
            if (File.Exists(LastSyncFilePath))
            {
                File.Delete(LastSyncFilePath);
            }
        }

        private static void PerformSync(string sourceFolder, string targetFolder, DateTime lastSyncTime, bool isPreview, string[] excludePatterns)
        {
            if (!Directory.Exists(sourceFolder))
            {
                Console.WriteLine($"Source folder '{sourceFolder}' does not exist.");
                return;
            }

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            bool filesCopied = SyncDirectory(sourceFolder, targetFolder, lastSyncTime, isPreview, excludePatterns);

            if (!filesCopied)
            {
                Console.WriteLine($"Nothing to copy from '{sourceFolder}'.");
            }
        }

        private static bool SyncDirectory(string sourceDir, string targetDir, DateTime lastSyncTime, bool isPreview, string[] excludePatterns)
        {
            bool filesCopied = false;

            // Copy files
            foreach (string sourceFilePath in Directory.GetFiles(sourceDir))
            {
                FileInfo fileInfo = new FileInfo(sourceFilePath);

                // Check exclusion patterns for files
                bool isExcluded = false;
                foreach (var pattern in excludePatterns)
                {
                    if (sourceFilePath.Contains(pattern))
                    {
                        isExcluded = true;
                        break;
                    }
                }

                if (isExcluded)
                {
                    Console.WriteLine($"Excluded '{sourceFilePath}' based on exclusion patterns.");
                    continue;
                }

                if (fileInfo.LastWriteTime > lastSyncTime || fileInfo.CreationTime > lastSyncTime)
                {
                    string targetFilePath = Path.Combine(targetDir, fileInfo.Name);
                    if (isPreview)
                    {
                        Console.WriteLine($"[Preview] Would copy '{sourceFilePath}' to '{targetFilePath}'");
                    }
                    else
                    {
                        File.Copy(sourceFilePath, targetFilePath, true);
                        Console.WriteLine($"Copied '{sourceFilePath}' to '{targetFilePath}'");
                    }
                    filesCopied = true;
                }
            }

            // Copy directories
            foreach (string sourceSubDir in Directory.GetDirectories(sourceDir))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(sourceSubDir);

                // Check exclusion patterns for directories
                bool isExcluded = false;
                foreach (var pattern in excludePatterns)
                {
                    if (sourceSubDir.Contains(pattern))
                    {
                        isExcluded = true;
                        break;
                    }
                }

                if (isExcluded)
                {
                    Console.WriteLine($"Excluded directory '{sourceSubDir}' based on exclusion patterns.");
                    continue;
                }

                string targetSubDir = Path.Combine(targetDir, dirInfo.Name);

                if (!Directory.Exists(targetSubDir))
                {
                    if (isPreview)
                    {
                        Console.WriteLine($"[Preview] Would create directory '{targetSubDir}'");
                    }
                    else
                    {
                        Directory.CreateDirectory(targetSubDir);
                        Console.WriteLine($"Created directory '{targetSubDir}'");
                    }
                }

                // Recursively sync subdirectories
                bool subDirFilesCopied = SyncDirectory(sourceSubDir, targetSubDir, lastSyncTime, isPreview, excludePatterns);
                filesCopied = filesCopied || subDirFilesCopied;
            }

            return filesCopied;
        }
    }
}