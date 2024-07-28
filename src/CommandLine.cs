namespace TTSCloudSync;

public class CommandLine
{
    public static readonly string VERSION = "1.0";

    static void Main(string[] args)
    {
        Console.Error.WriteLine($"[TTSCloudSync {VERSION}]");
        if (args.Length > 0)
        {
            string toolName = args[0];
            string[] shiftedArgs = args.Skip(1).Take(args.Length - 1).ToArray();
            switch (toolName)
            {
                case "extract-ugc-url":
                    UgcUrlExtractor.Main(shiftedArgs);
                    break;
                case "download-ugc-resources":
                    UgcResourceDownloader.Main(shiftedArgs);
                    break;
                case "sync-with-cloud":
                    CloudSync.Main(shiftedArgs);
                    break;
                case "patch-ugc-url":
                    UgcUrlPatcher.Main(shiftedArgs);
                    break;
                default:
                    Console.Error.WriteLine("Unknown tool: " + toolName);
                    Environment.Exit(1);
                    break;
            }
        }
        else
        {
            Console.Error.WriteLine("No tool specified!");
            Environment.Exit(1);
        }
    }

    private CommandLine()
    {
        // Not constructible.
    }
}
