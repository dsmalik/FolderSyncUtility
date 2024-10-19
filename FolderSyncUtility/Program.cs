using System;
using System.Collections.Generic;
using System.IO;

namespace FolderSyncUtility
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: FolderSyncUtility <folderListFile> [--preview]");
                return;
            }

            string folderListFile = args[0];
            bool isPreview = args.Length > 1 && args[1] == "--preview";

            if (!File.Exists(folderListFile))
            {
                Console.WriteLine($"File '{folderListFile}' does not exist.");
                return;
            }

            List<(string source, string target, string[] excludePatterns)> folderPairs = new List<(string source, string target, string[] excludePatterns)>();

            foreach (var line in File.ReadAllLines(folderListFile))
            {
                var parts = line.Split(new[] { "::::" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string[] excludePatterns = parts.Length == 3 ? parts[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) : new string[0];
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
        }
    }
}