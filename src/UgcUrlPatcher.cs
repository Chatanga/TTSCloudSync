using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TTSCloudSync;

public class UgcUrlPatcher
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
            case 1:
                ProcessingText(ParseMappingFile(args[0]), Console.In);
                break;
            case 2:
                using (StreamReader reader = new(File.OpenRead(args[1])))
                {
                    ProcessingText(ParseMappingFile(args[0]), reader);
                }
                break;
            default:
                Console.Error.WriteLine("Usage: ... mapping.lst [FILE]");
                Environment.Exit(1);
                break;
        }
    }

    private static Dictionary<string, UgcUrl> ParseMappingFile(string mappingFilePath)
    {
        Dictionary<string, UgcUrl> ugcUrlByKey = new();
        using (StreamReader reader = new(File.OpenRead(mappingFilePath)))
        {
            Regex ugcUrlRegex = UgcUrl.Regex();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] tokens = line.Split(';');
                Debug.Assert(tokens.Length == 2);

                string registeredName = tokens[0];
                int underscoreIndex = registeredName.IndexOf('_');
                Debug.Assert(underscoreIndex != -1);
                string key = registeredName[0..underscoreIndex];
                registeredName = registeredName[(underscoreIndex + 1)..];

                Match match = ugcUrlRegex.Match(tokens[1]);
                Debug.Assert(match.Success);
                ulong ugcHandle = ulong.Parse(match.Groups[1].Value);
                string sha1 = match.Groups[2].Value;

                UgcUrl newUgcUrl = new(ugcHandle, sha1);
                if (!ugcUrlByKey.TryGetValue(key, out UgcUrl oldUgcUrl))
                {
                    ugcUrlByKey.TryAdd(key, newUgcUrl);
                }
                else if (!oldUgcUrl.Equals(newUgcUrl))
                {
                    Console.Error.WriteLine($"Duplicated content {key} ({registeredName})");
                }
            }
        }
        //ugcUrlByKey.Select(kvp => kvp.Key).ToList().ForEach(element => Console.WriteLine($"{element}"));
        return ugcUrlByKey;
    }

    private static void ProcessingText(Dictionary<string, UgcUrl> ugcUrlByChecksum, TextReader reader)
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
                    startIndex = match.Index + match.Length;

                    ulong ugcHandle = ulong.Parse(match.Groups[1].Value);
                    string sha1 = match.Groups[2].Value;
                    UgcUrl ugcUrl = new(ugcHandle, sha1);

                    if (ugcUrlByChecksum.TryGetValue(sha1, out UgcUrl newUgcUrl) && !newUgcUrl.Equals(ugcUrl))
                    {
                        Console.Out.WriteLine($"{ugcUrl} -> {newUgcUrl}");
                        // TODO replace.
                    }
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
