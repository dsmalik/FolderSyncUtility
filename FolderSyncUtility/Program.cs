using System;
using System.Collections.Generic;
using System.IO;

namespace FolderSyncUtility
{
    internal class Program
    {
        private static readonly string DefaultExcludePatternsFile = "defaultExcludePatterns.txt";
        private static readonly string DefaultExcludePatternsDirectory = "defaultExludePatternsForDirectories.txt";
        private static readonly string FolderListFile = "sync_folders.txt";
        private static readonly string LastSyncFile = "lastSync.txt";

        internal static int TotalFilesToProcess = 0;
        internal static int TotalFilesExcluded = 0;
        internal static int TotalDirectoriesExcluded = 0;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: FolderSyncUtility [--preview] [--reset]");
                return;
            }

            bool isPreview = false;
            bool resetLastSyncTime = false;
            bool showLastSyncTime = false;

            foreach (var arg in args)
            {
                if (arg == "--preview")
                {
                    isPreview = true;
                }
                else if (arg == "--reset")
                {
                    resetLastSyncTime = true;
                }
                else if (arg == "--lastsynctime")
                {
                    showLastSyncTime = true;
                }
            }

            if (resetLastSyncTime)
            {
                FolderSync.ResetLastSyncTime();
                Console.WriteLine("Last sync time has been reset.");
                return;
            }

            if (showLastSyncTime)
            {
                if (!File.Exists(LastSyncFile))
                {
                    Console.WriteLine("Never synced in the past.");
                    return;
                }

                Console.WriteLine($"Last sync time - {File.ReadAllText(LastSyncFile)}");
                return;
            }

            if (!File.Exists(FolderListFile))
            {
                Console.WriteLine($"File '{FolderListFile}' does not exist.");
                return;
            }

            string[] defaultExcludePatterns = ReadDefaultExcludePatterns();
            string[] defaultExcludePatternsForDirectories = ReadDefaultExcludePatternsForDirectories();

            List<(string source, string target, string[] excludePatterns)> folderPairs = new List<(string source, string target, string[] excludePatterns)>();

            foreach (var line in File.ReadAllLines(FolderListFile))
            {
                var parts = line.Split(new[] { "::::" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string[] excludePatterns = parts.Length == 3 ? parts[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) : new string[0];
                    excludePatterns = MergeExcludePatterns(defaultExcludePatterns, excludePatterns);
                    folderPairs.Add((parts[0].Trim(), parts[1].Trim(), excludePatterns));
                }
                else
                {
                    Console.WriteLine($"Invalid line in folder list file: '{line}'");
                }
            }

            if (folderPairs.Count == 0)
            {
                Console.WriteLine("Nothing to do.");
                return;
            }

            foreach (var (source, target, excludePatterns) in folderPairs)
            {
                FolderSync.SyncFolders(source, target, isPreview, excludePatterns, defaultExcludePatternsForDirectories);
            }

            // Update last sync time after all sync operations are completed
            if (!isPreview)
            {
                FolderSync.UpdateLastSyncTime();
            }

            Console.WriteLine($"Total files - {TotalFilesToProcess}, Files excluded - {TotalFilesExcluded}, Directories excluded - {TotalDirectoriesExcluded}");
        }

        private static string[] ReadDefaultExcludePatternsForDirectories()
        {
            if (File.Exists(DefaultExcludePatternsDirectory))
            {
                return File.ReadAllLines(DefaultExcludePatternsDirectory);
            }
            return new string[0];
        }

        private static string[] ReadDefaultExcludePatterns()
        {
            if (File.Exists(DefaultExcludePatternsFile))
            {
                return File.ReadAllLines(DefaultExcludePatternsFile);
            }

            return new string[0];
        }

        private static string[] MergeExcludePatterns(string[] defaultPatterns, string[] customPatterns)
        {
            HashSet<string> mergedPatterns = new HashSet<string>(defaultPatterns);
            foreach (var pattern in customPatterns)
            {
                mergedPatterns.Add(pattern);
            }
            return new List<string>(mergedPatterns).ToArray();
        }
    }
}