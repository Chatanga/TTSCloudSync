using System.Diagnostics;

namespace TTSCloudSync;

class UgcUrlPatcher
{

    private static readonly string USAGE =
        """

        Usage:
            patch-ugc-url [-i] [--no-backup] MAPPING [SAVE]
        """;

    private static readonly string DESCRIPTION =
        """

        Patch all UGC URLs in the provided save (or standard input) to use one
        from your cloud with the same SHA1 (whatever the name) if one exists.

        Options:

            --help
                This documentation.

            -i
                Modify the SAVE file (if provided) in place instead of outputting the
                patched content.

            --no-backup
                Do not create a backup of the modified file if the in place option is
                enabled.

        """;

    public static void Main(string[] args)
    {
        CommandLineParser parser = new();
        parser.AddOption("--help");
        parser.AddOption("-i");
        parser.AddOption("--no-backup");
        (Dictionary<string, string?> options, List<string> arguments) = parser.Parse(args);

        if (options.ContainsKey("--help"))
        {
            Console.Out.WriteLine(USAGE);
            Console.Out.WriteLine(DESCRIPTION);
            Environment.Exit(0);
        }

        bool inPlace = options.ContainsKey("-i");
        bool noBackup = options.ContainsKey("--no-backup");

        switch (arguments.Count)
        {
            case 1:
                ProcessingText(ParseMappingFile(arguments[0]), Console.In, Console.Out);
                break;
            case 2:
                string fileName = arguments[1];
                if (inPlace)
                {
                    string tempFilePath = Path.GetTempFileName();
                    {
                        using StreamReader reader = new(File.OpenRead(fileName));
                        using StreamWriter writer = new(File.OpenWrite(tempFilePath));
                        ProcessingText(ParseMappingFile(arguments[0]), reader, writer);
                    }

                    if (noBackup)
                    {
                        File.Delete(fileName);
                    }
                    else
                    {
                        File.Move(fileName, fileName + ".bak");
                    }
                    File.Move(tempFilePath, fileName);
                }
                else
                {
                    using StreamReader reader = new(File.OpenRead(fileName));
                    ProcessingText(ParseMappingFile(arguments[0]), reader, Console.Out);
                }
                break;
            default:
                Console.Error.WriteLine(USAGE);
                Environment.Exit(1);
                break;
        }
    }

    private static Dictionary<string, UgcUrl> ParseMappingFile(string mappingFilePath)
    {
        Dictionary<string, UgcUrl> ugcUrlByKey = new();
        using (StreamReader reader = new(File.OpenRead(mappingFilePath)))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] tokens = line.Split(';');
                if (tokens.Length == 2)
                {
                    string registeredName = tokens[0];
                    int underscoreIndex = registeredName.IndexOf('_');
                    Debug.Assert(underscoreIndex != -1);
                    string key = registeredName[0..underscoreIndex];
                    registeredName = registeredName[(underscoreIndex + 1)..];

                    UgcUrl? newUgcUrl = UgcUrl.Parse(tokens[1]);
                    if (newUgcUrl is not null)
                    {
                        if (!ugcUrlByKey.TryGetValue(key, out UgcUrl oldUgcUrl))
                        {
                            ugcUrlByKey.TryAdd(key, newUgcUrl.Value);
                        }
                        else if (oldUgcUrl != newUgcUrl)
                        {
                            Console.Error.WriteLine($"Duplicated content {key} ({registeredName})");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Malformed UGC URL: '{tokens[1]}'");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Malformed UGC URL: '{line}'");
                }
            }
        }
        return ugcUrlByKey;
    }

    private static void ProcessingText(Dictionary<string, UgcUrl> ugcUrlByChecksum, TextReader reader, TextWriter writer)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            int startIndex = 0;
            while (true)
            {
                (UgcUrl? ugcUrl, int beginIndex, int endIndex) = UgcUrl.Find(line, startIndex);
                if (ugcUrl is not null)
                {
                    writer.Write(line.AsSpan(startIndex, beginIndex - startIndex));
                    startIndex = endIndex;
                    if (ugcUrlByChecksum.TryGetValue(ugcUrl.Value.Sha1, out UgcUrl newUgcUrl) && newUgcUrl != ugcUrl)
                    {
                        writer.Write(newUgcUrl);
                    }
                    else
                    {
                        writer.Write(ugcUrl);
                    }
                }
                else
                {
                    writer.WriteLine(line.AsSpan(startIndex));
                    break;
                }
            }
        }
    }
}
