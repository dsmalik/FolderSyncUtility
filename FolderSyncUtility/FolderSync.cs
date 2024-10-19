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

            if (!isPreview)
            {
                UpdateLastSyncTime();
            }
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

        private static void UpdateLastSyncTime()
        {
            File.WriteAllText(LastSyncFilePath, DateTime.Now.ToString());
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

            bool filesCopied = false;

            foreach (string sourceFilePath in Directory.GetFiles(sourceFolder))
            {
                FileInfo fileInfo = new FileInfo(sourceFilePath);

                // Check exclusion patterns
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
                    string targetFilePath = Path.Combine(targetFolder, fileInfo.Name);
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

            if (!filesCopied)
            {
                Console.WriteLine($"Nothing to copy from '{sourceFolder}'.");
            }
        }
    }
}