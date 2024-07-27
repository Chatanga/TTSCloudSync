using Steamworks;

namespace TTSCloudSync;

class UgcUrlExtractor
{
    private static readonly string USAGE =
        """

        Usage:
            extract-ugc-url [SAVE]
        """;

    private static readonly string DESCRIPTION =
        """

        Extract all the URLs for UGC (User-Generated Content) resources found in a JSON
        save (any kind of text file actually).

        """;

    public static void Main(string[] args)
    {
        CommandLineParser parser = new();
        parser.AddOption("--help");
        (Dictionary<string, string?> options, List<string> arguments) = parser.Parse(args);

        if (options.ContainsKey("--help"))
        {
            Console.Out.WriteLine(USAGE);
            Console.Out.WriteLine(DESCRIPTION);
            Environment.Exit(0);
        }

        switch (arguments.Count)
        {
            case 0:
                ProcessingText(Console.In);
                break;
            case 1:
                using (StreamReader reader = new(File.OpenRead(arguments[0])))
                {
                    ProcessingText(reader);
                }
                break;
            default:
                Console.Error.WriteLine(USAGE);
                Environment.Exit(1);
                break;
        }
    }

    private static void ProcessingText(TextReader reader)
    {
        HashSet<UgcUrl> ugcUrls = new();

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            int startIndex = 0;
            while (true)
            {
                (UgcUrl? ugcUrl, _, int endIndex) = UgcUrl.Find(line, startIndex);
                if (ugcUrl is not null)
                {
                    if (ugcUrls.Add(ugcUrl.Value))
                    {
                        Console.WriteLine(ugcUrl);
                    }
                    startIndex = endIndex;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
