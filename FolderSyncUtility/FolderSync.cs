using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace FolderSyncUtility
{
    public static class FolderSync
    {
        private static readonly string LastSyncFilePath = "lastSync.txt";
        private static readonly string LogFilePath = "syncLog.txt";
        private const long MaxZipSize = 100 * 1024 * 1024 * 5; // 500MB

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

            int zipIndex = 1;
            string tempZipFilePath = GetTempZipFilePath(sourceFolder, zipIndex);
            List<string> createdZipFiles = new List<string>();

            Console.WriteLine($"Temp zip file is at - {tempZipFilePath}");

            bool filesCopied;
            ZipArchive zipArchive = ZipFile.Open(tempZipFilePath, ZipArchiveMode.Create);
            try
            {
                long currentZipSize = 0;
                filesCopied = SyncDirectory(sourceFolder, ref zipArchive, lastSyncTime, isPreview, excludePatterns, ref zipIndex, ref currentZipSize, targetFolder, createdZipFiles, ref tempZipFilePath);
            }
            finally
            {
                zipArchive.Dispose();
                createdZipFiles.Add(tempZipFilePath); // Add the zip file path after disposing
            }

            if (!isPreview && filesCopied)
            {
                CopyZipFilesToTarget(createdZipFiles, targetFolder, sourceFolder);
            }

            DeleteTempZipFiles(createdZipFiles);
        }

        private static bool SyncDirectory(string sourceDir, ref ZipArchive zipArchive, DateTime lastSyncTime, bool isPreview, string[] excludePatterns, ref int zipIndex, ref long currentZipSize, string targetFolder, List<string> createdZipFiles, ref string tempZipFilePath)
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
                    if (isPreview)
                    {
                        Console.WriteLine($"[Preview] Would add '{sourceFilePath}' to zip");
                    }
                    else
                    {
                        string relativePath = GetRelativePath(sourceDir, sourceFilePath);
                        zipArchive.CreateEntryFromFile(sourceFilePath, relativePath);
                        Console.WriteLine($"Added '{sourceFilePath}' to zip");

                        currentZipSize += fileInfo.Length;
                        if (currentZipSize > MaxZipSize)
                        {
                            zipArchive.Dispose();
                            createdZipFiles.Add(tempZipFilePath);
                            zipIndex++;
                            currentZipSize = 0;
                            tempZipFilePath = GetTempZipFilePath(sourceDir, zipIndex);
                            zipArchive = ZipFile.Open(tempZipFilePath, ZipArchiveMode.Create);
                        }
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

                // Recursively sync subdirectories
                bool subDirFilesCopied = SyncDirectory(sourceSubDir, ref zipArchive, lastSyncTime, isPreview, excludePatterns, ref zipIndex, ref currentZipSize, targetFolder, createdZipFiles, ref tempZipFilePath);
                filesCopied = filesCopied || subDirFilesCopied;
            }

            // Append log data
            string logData = $"Sync completed at {DateTime.Now.ToString()}";
            File.AppendAllText(LogFilePath, logData);

            return filesCopied;
        }

        private static string GetRelativePath(string baseDir, string filePath)
        {
            Uri baseUri = new Uri(baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()) ? baseDir : baseDir + Path.DirectorySeparatorChar);
            Uri fileUri = new Uri(filePath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private static string GetTempZipFilePath(string sourceFolder, int index)
        {
            string sourceFolderName = Path.GetFileName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar));
            return Path.Combine(Path.GetTempPath(), $"{sourceFolderName}_{index}.zip");
        }

        private static void CopyZipFilesToTarget(List<string> createdZipFiles, string targetFolder, string sourceFolder)
        {
            string sourceFolderName = Path.GetFileName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar));
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            for (int i = 0; i < createdZipFiles.Count; i++)
            {
                string tempZipFilePath = createdZipFiles[i];
                if (File.Exists(tempZipFilePath)) // Ensure the file exists before copying
                {
                    var index = i + 1;
                    if (index == 1)
                    {
                        string targetZipFileName = $"{sourceFolderName}_{timestamp}.zip";
                        string targetZipFilePath = Path.Combine(targetFolder, targetZipFileName);
                        File.Copy(tempZipFilePath, targetZipFilePath, true);
                        Console.WriteLine($"Copied zip file to '{targetZipFilePath}'");
                        continue;
                    }
                    else
                    {
                        string targetZipFileName = $"{sourceFolderName}_{timestamp}_{i + 1}.zip";
                        string targetZipFilePath = Path.Combine(targetFolder, targetZipFileName);
                        File.Copy(tempZipFilePath, targetZipFilePath, true);
                        Console.WriteLine($"Copied zip file to '{targetZipFilePath}'");
                    }
                    
                }
                else
                {
                    Console.WriteLine($"Temp zip file '{tempZipFilePath}' does not exist.");
                }
            }
        }

        private static void DeleteTempZipFiles(List<string> createdZipFiles)
        {
            foreach (var tempZipFilePath in createdZipFiles)
            {
                if (File.Exists(tempZipFilePath))
                {
                    File.Delete(tempZipFilePath);
                }
            }
        }
    }
}