using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace exportx
{
    // Command line options
    class CommandLineOptions
    {
        [Option('f', "file", Required = true, HelpText = "Input file or directory to be processed")]
        public string InputFile { get; set; }

        [Option('o', "out", HelpText = "The output file (or directory path of which to export to)")]
        public string OutputFile { get; set; }

        [Option('m', "mode", HelpText = "Tool export mode (export, bin)", DefaultValue = "bin")]
        public string Mode { get; set; }

        [Option('w', "watcher", HelpText = "Start a watcher for new export/bin files", DefaultValue = false)]
        public bool Watcher { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            // Instance a helper
            var help = new HelpText
            {
                AdditionalNewLineAfterOption = false,
                AddDashesToOption = true
            };
            // Add actual usage
            help.AddPreOptionsLine("Usage: " + Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location) + " -f <file to process> (Or drag and drop a file)");
            // Add all available options
            help.AddOptions(this);
            // Return it
            return help;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Title
            Console.Title = "ExportX";
            // Header
            Console.WriteLine("-- ExportX (DTZxPorter) --" + Environment.NewLine);
            // Handle arguments as follows:
            // If all args are files, and it each contains a bin or export, convert to opposite format, in same dir
            // Else, send to command parser,
            // If no args, print usage
            bool NeedsArgParse = (args.Length > 0) ? false : true;
            // Check for them
            foreach (string arg in args)
            {
                // Check it
                if (!arg.StartsWith("-") && !arg.Contains("\"") && File.Exists(arg))
                {
                    // Not needed yet
                    NeedsArgParse = false;
                }
                else
                {
                    // We need to parse args
                    NeedsArgParse = true;
                    // Stop
                    break;
                }
            }
            
            // Check for multi-drag-drop
            if (!NeedsArgParse)
            {
                // We detected multiple drag and drop, loop and convert
                Parallel.ForEach<string>(args, (FilePath) =>
                {
                    // Check for bin / export
                    string Compare = FilePath.ToLower();
                    // Check type
                    if (Compare.EndsWith(".xmodel_bin") || Compare.EndsWith(".xanim_bin"))
                    {
                        // Log it
                        Console.WriteLine(":  Converting \"" + Path.GetFileName(FilePath) + "\"");
                        // Convert it
                        using (XAssetFile file = new XAssetFile(FilePath))
                        {
                            // Write to export
                            file.WriteExport(Path.Combine(Path.GetDirectoryName(FilePath.ToLower()), Path.GetFileName(FilePath.ToLower()).Replace(".xanim_bin", ".xanim_export").Replace(".xmodel_bin", ".xmodel_export")));
                        }
                    }
                    else if (Compare.EndsWith(".xmodel_export") || Compare.EndsWith(".xanim_export"))
                    {
                        // Log it
                        Console.WriteLine(":  Converting \"" + Path.GetFileName(FilePath) + "\"");
                        // Convert it
                        using (XAssetFile file = new XAssetFile(FilePath))
                        {
                            // Write to bin
                            file.WriteBin(Path.Combine(Path.GetDirectoryName(FilePath.ToLower()), Path.GetFileName(FilePath.ToLower()).Replace(".xanim_export", ".xanim_bin").Replace(".xmodel_export", ".xmodel_bin")));
                        }
                    }
                });

                // Done
                Console.Write(":  Finished converting...");
                Console.ReadKey();
            }
            else
            {
                // We must parse the flags because we had one that wasn't a file
                var options = new CommandLineOptions();
                // Commands
                if (CommandLine.Parser.Default.ParseArguments(args, options))
                {
                    // Check for *, if so, search current dir
                    if (options.InputFile == "*")
                    {
                        // Set to this
                        options.InputFile = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }
                    // Check for watcher
                    if (options.Watcher)
                    {
                        // Watcher only mode
                        if (Directory.Exists(options.InputFile))
                        {
                            // Setup filters
                            switch (options.Mode.ToLower())
                            {
                                case "bin":
                                    WatcherManager.RunWatcher(options.InputFile, ".xmodel_export|.xanim_export", InFormat.Export);
                                    break;
                                case "export":
                                    WatcherManager.RunWatcher(options.InputFile, ".xmodel_bin|.xanim_bin", InFormat.Bin);
                                    break;
                            }
                            
                        }
                        else
                        {
                            // Failed

                        }
                    }
                    else
                    {
                        // Check whether or not we got a file or directory
                        var info = File.GetAttributes(options.InputFile);
                        // Check
                        if (info.HasFlag(FileAttributes.Directory))
                        {
                            // Search directory for files based on the mode
                            if (Directory.Exists(options.InputFile))
                            {
                                // Search for files based on mode in dir
                                List<string> FilesToProcess = new List<string>();
                                // Search for files opposite of the mode
                                if (options.Mode.ToLower() == "bin")
                                {
                                    // Get exports
                                    FilesToProcess.AddRange(Directory.GetFiles(options.InputFile, "*.xmodel_export", SearchOption.AllDirectories));
                                    FilesToProcess.AddRange(Directory.GetFiles(options.InputFile, "*.xanim_export", SearchOption.AllDirectories));
                                    // Log count
                                    Console.WriteLine(":  Converting " + FilesToProcess.Count + " files to bins");
                                }
                                else if (options.Mode.ToLower() == "export")
                                {
                                    // Get bins
                                    FilesToProcess.AddRange(Directory.GetFiles(options.InputFile, "*.xmodel_bin", SearchOption.AllDirectories));
                                    FilesToProcess.AddRange(Directory.GetFiles(options.InputFile, "*.xanim_bin", SearchOption.AllDirectories));
                                    // Log count
                                    Console.WriteLine(":  Converting " + FilesToProcess.Count + " files to exports");
                                }
                                // Check output directory
                                if (!string.IsNullOrEmpty(options.OutputFile) && !Directory.Exists(options.OutputFile))
                                {
                                    // Make it
                                    Directory.CreateDirectory(options.OutputFile);
                                }
                                // Loop in parallel to convert them
                                Parallel.ForEach<string>(FilesToProcess, (ToConv) =>
                                {
                                    // Process it
                                    var ExportPath = string.Empty;
                                    // Check
                                    if (options.Mode.ToLower() == "export")
                                    {
                                        ExportPath = (string.IsNullOrEmpty(options.OutputFile)) ? ToConv.ToLower().Replace(".xanim_bin", ".xanim_export").Replace(".xmodel_bin", ".xmodel_export") : Path.Combine(options.OutputFile, Path.GetFileName(ToConv.ToLower().Replace(".xanim_bin", ".xanim_export").Replace(".xmodel_bin", ".xmodel_export")));
                                    }
                                    else
                                    {
                                        ExportPath = (string.IsNullOrEmpty(options.OutputFile)) ? ToConv.ToLower().Replace(".xanim_export", ".xanim_bin").Replace(".xmodel_export", ".xmodel_bin") : Path.Combine(options.OutputFile, Path.GetFileName(ToConv.ToLower().Replace(".xanim_export", ".xanim_bin").Replace(".xmodel_export", ".xmodel_bin")));
                                    }
                                    // Process and export
                                    using (XAssetFile Converter = new XAssetFile(ToConv))
                                    {
                                        // Save it to opposite type
                                        switch (Converter.Format)
                                        {
                                            case InFormat.Bin:
                                                Converter.WriteExport(ExportPath);
                                                break;
                                            case InFormat.Export:
                                                Converter.WriteBin(ExportPath);
                                                break;
                                        }
                                    }
                                });
                                // Finished
                                Console.WriteLine(":  Finished converting...");
                            }
                        }
                        else
                        {
                            // Process single file
                            if (File.Exists(options.InputFile))
                            {
                                // Process input
                                using (XAssetFile Converter = new XAssetFile(options.InputFile))
                                {
                                    // Check what we have
                                    if (options.Mode.ToLower() == "bin" && Converter.Format == InFormat.Export)
                                    {
                                        // Log
                                        Console.WriteLine(":  Converting \"" + Path.GetFileNameWithoutExtension(options.InputFile) + "\"");
                                        // Save to bin
                                        var ExportPath = (string.IsNullOrEmpty(options.OutputFile)) ? options.InputFile.ToLower().Replace(".xanim_export", ".xanim_bin").Replace(".xmodel_export", ".xmodel_bin") : options.OutputFile;
                                        // Save
                                        Converter.WriteBin(ExportPath);
                                        // Done
                                        Console.WriteLine(":  Finished converting...");
                                    }
                                    else if (options.Mode.ToLower() == "export" && Converter.Format == InFormat.Bin)
                                    {
                                        // Log
                                        Console.WriteLine(":  Converting \"" + Path.GetFileNameWithoutExtension(options.InputFile) + "\"");
                                        // Save to export
                                        var ExportPath = (string.IsNullOrEmpty(options.OutputFile)) ? options.InputFile.ToLower().Replace(".xanim_bin", ".xanim_export").Replace(".xmodel_bin", ".xmodel_export") : options.OutputFile;
                                        // Save
                                        Converter.WriteExport(ExportPath);
                                        // Done
                                        Console.WriteLine(":  Finished converting...");
                                    }
                                    else
                                    {
                                        // Format match
                                        Console.WriteLine(":  Input and output formats match.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
