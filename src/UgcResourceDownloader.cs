using Steamworks;

namespace TTSCloudSync;

class UgcResourceDownloader
{
    private class FileDownloader
    {
        private readonly UGCHandle_t Handle;
        private readonly string OutputDir;
        private readonly CallResult<RemoteStorageDownloadUGCResult_t> Callback;
        private bool success;
        private bool Finished;

        public FileDownloader(UGCHandle_t handle, string outputDir)
        {
            Handle = handle;
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

                string filePath = Path.Combine(path, result.m_pchFileName);
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

    public static void Main(string[] args)
    {
        CommandLineParser parser = new();
        parser.AddOption("-o", true);
        (Dictionary<string, string?> options, List<string> arguments) = parser.Parse(args);

        string outputDir = options.GetValueOrDefault("-o") ?? ".";

        switch (arguments.Count)
        {
            case 0:
                ProcessingText(Console.In, outputDir);
                break;
            case 1:
                using (StreamReader reader = new(File.OpenRead(arguments[0])))
                {
                    Console.WriteLine(arguments[0] + " - " + outputDir);
                    ProcessingText(reader, outputDir);
                }
                break;
            default:
                Console.Error.WriteLine("Usage: download-ugc-resources [-o OUTPUT_DIR] [FILE]");
                Environment.Exit(1);
                break;
        }
    }

    private static void ProcessingText(TextReader reader, string outputDir)
    {
        SteamCloud.ConnectToSteam(TabletopSimulatorCloud.TTS_APP_ID);
        try
        {
            Console.WriteLine("waiting...");

            string? url;
            while ((url = reader.ReadLine()) != null)
            {
                UgcUrl? ugcUrl = UgcUrl.Parse(url);
                if (ugcUrl is not null)
                {
                    UGCHandle_t hContent = new(ugcUrl.Value.Handle);
                    var downloader = new FileDownloader(hContent, outputDir);
                    Task<bool> task = downloader.Download();
                    task.Wait();
                    if (task.Result)
                    {
                        Console.WriteLine(url + " -> success");
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
}
