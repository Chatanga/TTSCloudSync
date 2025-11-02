using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace TTSCloudSync;

partial class RemoteCacheConverter
{
    private static readonly string USAGE =
        """

        Usage:
            convert-remote-cache [-o OUTPUT_DIR] [REMOTE_CACHE_DIR]
        """;

    private static readonly string DESCRIPTION =
        """

        Convert the content of a remote cache (steam/userdata/<user_id>/<game_id>/remote)
        as managed by TTS (with a pair of CloudInfo/CloudFolder files) into a structured
        tree of files with their SHA1 prefix removed. The resulting tree could be used as
        a resource directory for CloudSync.

        Options:

            --help
                This documentation.

            --o directory
                The directory where to store the resources (default is 'remote').

        """;

    [GeneratedRegex("[0-9A-Z]+_")]
    private static partial Regex Sha1PrefixRegex();

    public static void Main(string[] args)
    {
        CommandLineParser parser = new();
        parser.AddOption("--help");
        parser.AddOption("-o", true);
        (Dictionary<string, string?> options, List<string> arguments) = parser.Parse(args);

        if (options.ContainsKey("--help"))
        {
            Console.Out.WriteLine($"[TTSCloudSync {CommandLine.VERSION}]");
            Console.Out.WriteLine(USAGE);
            Console.Out.WriteLine(DESCRIPTION);
            Environment.Exit(0);
        }

        if (arguments.Count != 1)
        {
            Console.Error.WriteLine(USAGE);
            Environment.Exit(1);
        }

        string remoteCacheDirPath = arguments[0];

        if (!Directory.Exists(remoteCacheDirPath))
        {
            Console.Error.WriteLine($"Remote cache directory '{remoteCacheDirPath}' doesn't exist.");
            Environment.Exit(1);
        }

        string outputDirPath = options.GetValueOrDefault("-o") ?? "remote";

        if (Directory.Exists(outputDirPath))
        {
            Console.Error.WriteLine($"Output directory '{outputDirPath}' already exists.");
            Environment.Exit(1);
        }

        Dictionary<UniKey, TabletopSimulatorCloud.CloudItem> items = null;
        List<string> folders = null;

        foreach (var file in Directory.GetFiles(remoteCacheDirPath))
        {
            string fileName = Path.GetFileName(file);
            if (fileName == "CloudInfo.bson")
            {
                Console.WriteLine("ListItems");
                items = ListItems(file);
            }
            else if (fileName == "CloudFolder.bson")
            {
                Console.WriteLine("ListFolders");
                folders = ListFolders(file);
            }
            else
            {
                Regex regex = Sha1PrefixRegex();
                Match match = regex.Match(fileName);
                if (match.Success && match.Index == 0)
                {
                    fileName = fileName[match.Length..];
                }
                else
                {
                    //Console.Error.WriteLine($"Suspicious file name (missing SHA1 prefix): {fileName}");
                }
            }
        }

        if (items == null || folders == null)
        {
            Console.Error.WriteLine($"Missing CloudInfo and/or CloudFolder files!");
            Environment.Exit(1);
        }

        foreach (var folder in folders)
        {
            var path = Path.Join(outputDirPath, folder);
            Directory.CreateDirectory(path);
        }

        foreach (var (key, item) in items)
        {
            var folderPath = Path.Join(item.Folder.Split('/'));
            var srcPath = Path.Join(remoteCacheDirPath, key.ToString());
            var dstPath = Path.Join(outputDirPath, folderPath, item.Name);
            if (!File.Exists(srcPath))
            {
                Console.Error.WriteLine($"Source doesn't exists: {srcPath}");
            }
            else if (File.Exists(dstPath))
            {
                Console.Error.WriteLine($"Destination already exists: {dstPath}");
            }
            else
            {
                Console.WriteLine($"Converting {srcPath} into {dstPath}");
                File.Copy(srcPath, dstPath);
            }
        }
    }

    private static Dictionary<UniKey, TabletopSimulatorCloud.CloudItem> ListItems(string cloudInfoFilePath)
    {
        var data = File.ReadAllBytes(cloudInfoFilePath);
        var rawCloudInfo = ParseBson<Dictionary<string, TabletopSimulatorCloud.CloudItem>>(data);
        if (rawCloudInfo is null)
        {
            throw new Exception("Malformed 'CloudInfo.bson' file.");
        }
        return rawCloudInfo.ToDictionary(kvp => new UniKey(kvp.Key), kvp => kvp.Value);
    }

    private static List<string> ListFolders(string cloudFolderFilePath)
    {
        var data = File.ReadAllBytes(cloudFolderFilePath);
        var cloudFolder = ParseBson<Dictionary<string, string>>(data);
        if (cloudFolder is null)
        {
            throw new Exception("Malformed 'CloudFolder.bson' file.");
        }
        return new List<string>(cloudFolder.Values.ToArray());
    }

    private static T? ParseBson<T>(byte[] data)
    {
        using MemoryStream memoryStream = new(data);
        using BsonDataReader bsonReader = new(memoryStream);
        JsonSerializer serializer = new();
        return serializer.Deserialize<T>(bsonReader);
    }
}
