using System.Text.RegularExpressions;

namespace TTSCloudSync;

public class UgcUrlExtractor
{
    private static void HandleException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.WriteLine("Unhandled exception (" + e.ExceptionObject.GetType() + "): " + e.ExceptionObject);
    }

    public static void Main(string[] args)
    {
        Console.Error.WriteLine("Started");

        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(HandleException);

        switch (args.Length)
        {
            case 0:
                ProcessingText(Console.In);
                break;
            case 1:
                using (StreamReader reader = new(File.OpenRead(args[0])))
                {
                    ProcessingText(reader);
                }
                break;
            default:
                Console.Error.WriteLine("Usage: ... [FILE]");
                Environment.Exit(1);
                break;
        }
    }

    private static void ProcessingText(TextReader reader)
    {
        HashSet<UgcUrl> urls = new();

        Regex regex = UgcUrl.Regex();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            int startIndex = 0;
            while (true)
            {
                Match match = regex.Match(line, startIndex);
                if (match.Success)
                {
                    ulong ugcHandle = ulong.Parse(match.Groups[1].Value);
                    string sha1 = match.Groups[2].Value;
                    urls.Add(new UgcUrl(ugcHandle, sha1));
                    startIndex = match.Index + match.Length;
                }
                else
                {
                    break;
                }
            }
        }

        foreach (UgcUrl url in urls)
        {
            Console.WriteLine(url);
        }
    }
}
