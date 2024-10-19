using System;
using System.Collections.Generic;
using System.IO;

namespace FolderSyncUtility
{
    internal class Program
    {
        private static readonly string DefaultExcludePatternsFile = "defaultExcludePatterns.txt";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: FolderSyncUtility <folderListFile> [--preview] [--reset]");
                return;
            }

            bool isPreview = false;
            bool resetLastSyncTime = false;
            string folderListFile = null;

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
                else
                {
                    folderListFile = arg;
                }
            }

            if (resetLastSyncTime)
            {
                FolderSync.ResetLastSyncTime();
                Console.WriteLine("Last sync time has been reset.");
                return;
            }

            if (string.IsNullOrEmpty(folderListFile))
            {
                Console.WriteLine("Usage: FolderSyncUtility <folderListFile> [--preview] [--reset]");
                return;
            }

            if (!File.Exists(folderListFile))
            {
                Console.WriteLine($"File '{folderListFile}' does not exist.");
                return;
            }

            string[] defaultExcludePatterns = ReadDefaultExcludePatterns();

            List<(string source, string target, string[] excludePatterns)> folderPairs = new List<(string source, string target, string[] excludePatterns)>();

            foreach (var line in File.ReadAllLines(folderListFile))
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
                FolderSync.SyncFolders(source, target, isPreview, excludePatterns);
            }

            // Update last sync time after all sync operations are completed
            if (!isPreview)
            {
                FolderSync.UpdateLastSyncTime();
            }
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