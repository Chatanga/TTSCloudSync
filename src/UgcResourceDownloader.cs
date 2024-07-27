using Steamworks;
using System.Text.RegularExpressions;

namespace TTSCloudSync;

partial class UgcResourceDownloader
{
    private static readonly string USAGE =
        """

        Usage:
            download-ugc-resources [--no-sha1] [-o OUTPUT_DIR] [FILE]
        """;

    private static readonly string DESCRIPTION =
        """

        Important:
            A Steam client with the TTS application must be running for this command to
            work.

        Download the UGC URLs from a file (or the standard input) and store them in the
        provided output directory (or the current one). Each resource ends up in a
        subdirectory named after the Steam ID of its owner.

        Options:

            --help
                This documentation.

            --no-sha1
                Do not prepend resource names with their checksum (SHA1).

            --o directory
                To directory where to store the resources.

        """;

    [GeneratedRegex("[0-9A-Z]+_")]
    private static partial Regex Sha1PrefixRegex();

    public static void Main(string[] args)
    {
        CommandLineParser parser = new();
        parser.AddOption("--help");
        parser.AddOption("--no-sha1");
        parser.AddOption("-o", true);
        (Dictionary<string, string?> options, List<string> arguments) = parser.Parse(args);

        if (options.ContainsKey("--help"))
        {
            Console.Out.WriteLine(USAGE);
            Console.Out.WriteLine(DESCRIPTION);
            Environment.Exit(0);
        }

        bool noSha1 = options.ContainsKey("--no-sha1");
        string outputDir = options.GetValueOrDefault("-o") ?? ".";

        if (!Directory.Exists(outputDir))
        {
            Console.Error.WriteLine($"Output directory '{outputDir}' doesn't exist.");
            Environment.Exit(1);
        }

        switch (arguments.Count)
        {
            case 0:
                ProcessingText(Console.In, noSha1, outputDir);
                break;
            case 1:
                using (StreamReader reader = new(File.OpenRead(arguments[0])))
                {
                    Console.Out.WriteLine(arguments[0] + " - " + outputDir);
                    ProcessingText(reader, noSha1, outputDir);
                }
                break;
            default:
                Console.Error.WriteLine(USAGE);
                Environment.Exit(1);
                break;
        }
    }

    private static void ProcessingText(TextReader reader, bool noSha1, string outputDir)
    {
        SteamCloud.ConnectToSteam(TabletopSimulatorCloud.TTS_APP_ID);
        try
        {
            Console.Out.WriteLine("waiting...");

            string? url;
            while ((url = reader.ReadLine()) != null)
            {
                UgcUrl? ugcUrl = UgcUrl.Parse(url);
                if (ugcUrl is not null)
                {
                    UGCHandle_t hContent = new(ugcUrl.Value.Handle);
                    var downloader = new FileDownloader(hContent, noSha1, outputDir);
                    Task<bool> task = downloader.Download();
                    task.Wait();
                    if (task.Result)
                    {
                        Console.Out.WriteLine(url + " -> success");
                    }
                    else
                    {
                        Console.Error.WriteLine(url + " -> unresolvable");
                    }
                }
                else
                {
                    Console.Error.WriteLine(url + " -> not a proper URL");
                }
            }
        }
        finally
        {
            SteamAPI.Shutdown();
        }
    }

    private class FileDownloader
    {
        private readonly UGCHandle_t Handle;
        private readonly bool NoSha1;
        private readonly string OutputDir;
        private readonly CallResult<RemoteStorageDownloadUGCResult_t> Callback;
        private bool success;
        private bool Finished;

        public FileDownloader(UGCHandle_t handle, bool noSha1, string outputDir)
        {
            Handle = handle;
            NoSha1 = noSha1;
            OutputDir = outputDir;
            Callback = CallResult<RemoteStorageDownloadUGCResult_t>.Create(OnDownloadFinished);
        }

        public async Task<bool> Download()
        {
            var ret = SteamRemoteStorage.UGCDownload(Handle, 0);
            Callback.Set(ret);
            while (!Finished)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(100);
            }
            return success;
        }

        private void OnDownloadFinished(RemoteStorageDownloadUGCResult_t result, bool fail)
        {
            if (fail || result.m_eResult != EResult.k_EResultOK)
            {
                success = false;
                Finished = true;
            }
            else
            {
                string path = Path.Combine(OutputDir, $"{result.m_ulSteamIDOwner}");
                Directory.CreateDirectory(path);

                string fileName = result.m_pchFileName;
                if (NoSha1)
                {
                    Regex regex = Sha1PrefixRegex();
                    Match match = regex.Match(fileName);
                    if (match.Success && match.Index == 0)
                    {
                        fileName = fileName[match.Length..];
                        if (File.Exists(Path.Combine(path, fileName)))
                        {
                            Console.Error.WriteLine($"Forcing SHA1 prefix to avoid name collision with file '{fileName}'.");
                            fileName = result.m_pchFileName;
                        }
                    }
                }

                string filePath = Path.Combine(path, fileName);
                using (BinaryWriter binWriter = new(File.Open(filePath, FileMode.Create)))
                {
                    byte[] data = new byte[4096];
                    uint start = 0;
                    while (true)
                    {
                        int readCount = SteamRemoteStorage.UGCRead(Handle, data, data.Length, start, EUGCReadAction.k_EUGCRead_ContinueReadingUntilFinished);
                        binWriter.Write(data, 0, readCount);
                        start += (uint)readCount;
                        if (readCount < data.Length)
                        {
                            break;
                        }
                    }
                }

                success = true;
                Finished = true;
            }
        }
    }
}
