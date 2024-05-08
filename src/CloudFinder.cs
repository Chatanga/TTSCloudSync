using Steamworks;
using System.Text.RegularExpressions;

namespace TTSCloudSync;

class CloudFinder
{
    private static void HandleException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.Error.WriteLine("Unhandled exception (" + e.ExceptionObject.GetType() + "): " + e.ExceptionObject);
    }

    private class FileDownloader
    {
        private readonly CallResult<RemoteStorageDownloadUGCResult_t> Callback;
        private readonly UGCHandle_t Handle;
        public RemoteStorageDownloadUGCResult_t? UGCResult;
        private bool Finished;

        public FileDownloader(UGCHandle_t handle)
        {
            Callback = CallResult<RemoteStorageDownloadUGCResult_t>.Create(OnDownloadFinished);
            Handle = handle;
        }

        public async Task<RemoteStorageDownloadUGCResult_t?> Download()
        {
            var ret = SteamRemoteStorage.UGCDownload(Handle, 0);
            Callback.Set(ret);
            while (!Finished)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(100);
            }
            return UGCResult;
        }

        private void OnDownloadFinished(RemoteStorageDownloadUGCResult_t result, bool fail)
        {
            if (fail || result.m_eResult != EResult.k_EResultOK)
            {
                UGCResult = null;
                Finished = true;
            }
            else
            {
                UGCResult = result;
                Finished = true;

                /*
                AppId_t appID;
                string name;
                int fileSizeInBytes;
                CSteamID steamIDOwner;

                Console.WriteLine("SteamRemoteStorage.GetUGCDetails...");
                bool success = SteamRemoteStorage.GetUGCDetails(Handle, out appID, out name, out fileSizeInBytes, out steamIDOwner);

                Console.WriteLine("success: " + success);
                Console.WriteLine("appID: " + appID);
                Console.WriteLine("name: " + name);
                Console.WriteLine("fileSizeInBytes: " + fileSizeInBytes);
                Console.WriteLine("steamIDOwner: " + steamIDOwner);
                */
            }
        }
    }

    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(HandleException);

        if (args.Length == 1)
        {
            SteamCloud.ConnectToSteam(TabletopSimulatorCloud.TTS_APP_ID);
            try
            {
                foreach (string url in File.ReadLines(args[0]))
                {
                    Regex regex = UgcUrl.Regex();
                    Match match = regex.Match(url);
                    if (match.Success)
                    {
                        ulong ugcHandle = ulong.Parse(match.Groups[1].Value);
                        string sha1 = match.Groups[2].Value;

                        UGCHandle_t hContent = new(ugcHandle);
                        var downloader = new FileDownloader(hContent);
                        Task<RemoteStorageDownloadUGCResult_t?> task = downloader.Download();
                        task.Wait();
                        if (task.Result is RemoteStorageDownloadUGCResult_t result)
                        {
                            Console.WriteLine(url + " -> " + result.m_ulSteamIDOwner);
                        }
                        else
                        {
                            Console.WriteLine(url + " -> unresolvable");
                        }
                    }
                    else
                    {
                        Console.WriteLine(url + " -> not a proper URL");
                    }
                }
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }
        else
        {
            Console.Error.WriteLine("Usage: ./run FILE");
            Environment.Exit(1);
        }
    }
}
