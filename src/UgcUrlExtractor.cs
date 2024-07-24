using Steamworks;

namespace TTSCloudSync;

class UgcUrlExtractor
{
    public static void Main(string[] args)
    {
        (_, List<string> arguments) = new CommandLineParser().Parse(args);

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
                Console.Error.WriteLine("Usage: extract-ugc-url [SAVE] [> URL_LST]");
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
