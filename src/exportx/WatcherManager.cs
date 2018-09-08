using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace exportx
{
    internal class WatcherManager
    {
        /// <summary>
        /// Whether or not the watcher is running
        /// </summary>
        public static bool WatcherMode = false;
        /// <summary>
        /// A list of accepted files
        /// </summary>
        public static List<string> FileExtensions = new List<string>();
        /// <summary>
        /// A list of files to convert
        /// </summary>
        public static List<string> FilesToConvert = new List<string>();
        /// <summary>
        /// The input format
        /// </summary>
        public static InFormat InputMode = InFormat.Export;

        /// <summary>
        /// Prepare the watcher to convert files
        /// </summary>
        public static void RunWatcher(string Path, string Filter, InFormat Format)
        {
            // Log
            Console.WriteLine("Watcher mode started...");
            Console.WriteLine("  - Path: \"" + Path + "\"");
            Console.WriteLine("  - Watching for: \"" + Filter + "\" files");
            // Set
            WatcherMode = true;
            InputMode = Format;
            
            // File watcher
            var Watcher = new FileSystemWatcher();
            Watcher.Path = Path;
            Watcher.NotifyFilter = NotifyFilters.FileName;
            Watcher.Filter = "*.*";
            Watcher.Created += Watcher_Created;
            Watcher.EnableRaisingEvents = true;
            Watcher.IncludeSubdirectories = true;

            // Split it
            FileExtensions = Filter.Split('|').ToList();

            // Start conversion thread
            var Converter = Task.Run((Action)ConversionWatcher);

            // Wait until end
            new System.Threading.AutoResetEvent(false).WaitOne();

            // Stop
            WatcherMode = false;
            Converter.Dispose();
        }

        private static void ConversionWatcher()
        {
            // Loop until end
            while (WatcherMode)
            {
                // Result
                string ToConvert = string.Empty;

                // Get an item
                lock (FilesToConvert)
                {
                    // Check
                    if (FilesToConvert.Count > 0)
                    {
                        // Grab it
                        ToConvert = FilesToConvert[0].ToLower();
                        // Remove
                        FilesToConvert.RemoveAt(0);
                    }
                }

                // Prepare to run
                if (!string.IsNullOrEmpty(ToConvert))
                {
                    // Make sure it isn't already there (The result file)
                    var ResultFile = (InputMode == InFormat.Export) ? ToConvert.Replace(".xanim_export", ".xanim_bin").Replace(".xmodel_export", ".xmodel_bin") : ToConvert.ToLower().Replace(".xanim_bin", ".xanim_export").Replace(".xmodel_bin", ".xmodel_export");
                    // Check it
                    if (!File.Exists(ResultFile))
                    {
                        // Wait until it can be read, (Read/Write access)
                        try
                        {
                            // Get read access
                            WaitForFile(ToConvert);
                            // Convert it
                            Console.WriteLine("  - Converting \"" + Path.GetFileName(ToConvert) + "\"");

                            // Process the file
                            using (XAssetFile Converter = new XAssetFile(ToConvert))
                            {
                                // Save it to opposite type
                                switch (InputMode)
                                {
                                    case InFormat.Bin:
                                        Converter.WriteExport(ResultFile);
                                        break;
                                    case InFormat.Export:
                                        Converter.WriteBin(ResultFile);
                                        break;
                                }
                            }
                        }
                        catch
                        {
                            // Move on, we failed
                        }
                    }
                }
                else
                {
                    // Wait
                    System.Threading.Thread.Sleep(100);
                }
            }
        }

        private static void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            // Prepare to check file info
            if (FileExtensions.Any(x => e.FullPath.ToLower().EndsWith(x)))
            {
                // Add it to the queue
                lock (FilesToConvert)
                {
                    // Add it
                    FilesToConvert.Add(e.FullPath);
                }
            }
        }

        private static void WaitForFile(string FilePath)
        {
            // Loop and wait
            while (File.Exists(FilePath) && !IsFileReady(FilePath))
            {
                // Wait
                System.Threading.Thread.Sleep(100);
            }
        }

        private static bool IsFileReady(string FilePath)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                // Open with no sharing
                using (var inputStream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // See if we can
                    if (inputStream.Length > 0)
                    {
                        // Worked
                        return true;
                    }
                    else
                    {
                        // Failed
                        return false;
                    }
                }
            }
            catch
            {
                // Error, failed
                return false;
            }
        }
    }
}
