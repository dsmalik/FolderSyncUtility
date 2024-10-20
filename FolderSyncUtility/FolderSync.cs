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

        public static void SyncFolders(string sourceFolder, string targetFolder, bool isPreview, string[] fileExcludePatterns, string[] dirExcludePatterns)
        {
            DateTime lastSyncTime = GetLastSyncTime();
            PerformSync(sourceFolder, targetFolder, lastSyncTime, isPreview, fileExcludePatterns, dirExcludePatterns);
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

        private static void PerformSync(string sourceFolder, string targetFolder, DateTime lastSyncTime, bool isPreview, string[] fileExcludePatterns, string[] dirExcludePatterns)
        {
            if (!Directory.Exists(sourceFolder))
            {
                LogMessage($"Source folder '{sourceFolder}' does not exist.");
                return;
            }

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            int zipIndex = 1;
            string tempZipFilePath = GetTempZipFilePath(sourceFolder, zipIndex);
            List<string> createdZipFiles = new List<string>();

            LogMessage($"Temp zip file is at - {tempZipFilePath}");

            if (File.Exists(tempZipFilePath))
            {
                LogMessage($"Temp zip file already exists. Deleting before creating new.");
                File.Delete(tempZipFilePath);
            }

            bool filesCopied;
            ZipArchive zipArchive = ZipFile.Open(tempZipFilePath, ZipArchiveMode.Create);
            try
            {
                long currentZipSize = 0;
                filesCopied = SyncDirectory(sourceFolder, sourceFolder, ref zipArchive, lastSyncTime, isPreview, fileExcludePatterns, dirExcludePatterns, ref zipIndex, ref currentZipSize, targetFolder, createdZipFiles, ref tempZipFilePath);
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

        private static bool SyncDirectory(string baseDir, string sourceDir, ref ZipArchive zipArchive, DateTime lastSyncTime, bool isPreview, string[] fileExcludePatterns, string[] dirExcludePatterns, ref int zipIndex, ref long currentZipSize, string targetFolder, List<string> createdZipFiles, ref string tempZipFilePath)
        {
            bool filesCopied = false;

            // Copy files
            foreach (string sourceFilePath in Directory.GetFiles(sourceDir))
            {
                FileInfo fileInfo = new FileInfo(sourceFilePath);

                // Check exclusion patterns for files
                bool isExcluded = false;
                foreach (var pattern in fileExcludePatterns)
                {
                    if (sourceFilePath.Contains(pattern))
                    {
                        isExcluded = true;
                        break;
                    }
                }

                if (isExcluded)
                {
                    Program.TotalFilesExcluded++;
                    LogMessage($"Excluded '{sourceFilePath}' based on exclusion patterns.");
                    continue;
                }

                if (fileInfo.LastWriteTime > lastSyncTime || fileInfo.CreationTime > lastSyncTime)
                {
                    if (isPreview)
                    {
                        Program.TotalFilesToProcess++;
                        LogMessage($"[Preview] Would add '{sourceFilePath}' to zip");
                    }
                    else
                    {
                        try
                        {
                            string relativePath = GetRelativePath(baseDir, sourceFilePath);
                            LogMessage($"Adding '{sourceFilePath}' to zip");
                            zipArchive.CreateEntryFromFile(sourceFilePath, relativePath);

                            currentZipSize += fileInfo.Length;
                            if (currentZipSize > MaxZipSize)
                            {
                                zipArchive.Dispose();
                                createdZipFiles.Add(tempZipFilePath);
                                zipIndex++;
                                currentZipSize = 0;
                                tempZipFilePath = GetTempZipFilePath(baseDir, zipIndex);
                                zipArchive = ZipFile.Open(tempZipFilePath, ZipArchiveMode.Create);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Error adding '{sourceFilePath}' to zip: {ex.Message}");
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
                foreach (var pattern in dirExcludePatterns)
                {
                    if (sourceSubDir.EndsWith(pattern))
                    {
                        isExcluded = true;
                        break;
                    }
                }

                if (isExcluded)
                {
                    Program.TotalDirectoriesExcluded++;
                    LogMessage($"Excluded directory '{sourceSubDir}' based on exclusion patterns.");
                    continue;
                }

                // Recursively sync subdirectories
                bool subDirFilesCopied = SyncDirectory(baseDir, sourceSubDir, ref zipArchive, lastSyncTime, isPreview, fileExcludePatterns, dirExcludePatterns, ref zipIndex, ref currentZipSize, targetFolder, createdZipFiles, ref tempZipFilePath);
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
                    try
                    {
                        var index = i + 1;
                        string targetZipFileName = index == 1
                            ? $"{sourceFolderName}_{timestamp}.zip"
                            : $"{sourceFolderName}_{timestamp}_{index}.zip";
                        string targetZipFilePath = Path.Combine(targetFolder, targetZipFileName);
                        LogMessage($"Copying zip file to '{targetZipFilePath}'");
                        File.Copy(tempZipFilePath, targetZipFilePath, true);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error copying zip file '{tempZipFilePath}' to target: {ex.Message}");
                    }
                }
                else
                {
                    LogMessage($"Temp zip file '{tempZipFilePath}' does not exist.");
                }
            }
        }

        private static void DeleteTempZipFiles(List<string> createdZipFiles)
        {
            foreach (var tempZipFilePath in createdZipFiles)
            {
                if (File.Exists(tempZipFilePath))
                {
                    try
                    {
                        LogMessage($"Deleting temp zip file '{tempZipFilePath}'");
                        File.Delete(tempZipFilePath);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error deleting temp zip file '{tempZipFilePath}': {ex.Message}");
                    }
                }
            }
        }

        private static void LogMessage(string message)
        {
            string timestampedMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            Console.WriteLine(timestampedMessage);
            File.AppendAllText(LogFilePath, timestampedMessage + Environment.NewLine);
        }
    }
}