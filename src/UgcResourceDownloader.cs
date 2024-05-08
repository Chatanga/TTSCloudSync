using Steamworks;
using System.Text.RegularExpressions;

namespace TTSCloudSync;

public class UgcResourceDownloader
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

    private static void HandleException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.WriteLine("Unhandled exception (" + e.ExceptionObject.GetType() + "): " + e.ExceptionObject);
    }

    public static void Main(string[] args)
    {
        Console.Error.WriteLine("Started");

        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(HandleException);

        List<string> parameters = new();
        Dictionary<string, object> options = new();
        int i = 0;
        while (i < args.Length)
        {
            string arg = args[i++];
            if (arg.StartsWith('-'))
            {
                string key = arg[1..];
                object value;
                if (i < args.Length)
                {
                    value = args[i++];
                }
                else
                {
                    value = true;
                }
                options.Add(key, value);
            }
            else
            {
                parameters.Add(arg);
            }
        }

        string outputDir = "" + options.GetValueOrDefault("o", ".");

        switch (parameters.Count)
        {
            case 0:
                ProcessingText(Console.In, outputDir);
                break;
            case 1:
                using (StreamReader reader = new(File.OpenRead(parameters[0])))
                {
                    Console.Out.WriteLine(parameters[0] + " - " + outputDir);
                    ProcessingText(reader, outputDir);
                }
                break;
            default:
                Console.Error.WriteLine("Usage: ... [FILE]");
                Console.Out.WriteLine(parameters[0]);
                Console.Out.WriteLine(parameters[1]);
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
                Regex regex = UgcUrl.Regex();
                Match match = regex.Match(url);
                if (match.Success)
                {
                    ulong ugcHandle = ulong.Parse(match.Groups[1].Value);
                    string sha1 = match.Groups[2].Value;

                    UGCHandle_t hContent = new(ugcHandle);
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
