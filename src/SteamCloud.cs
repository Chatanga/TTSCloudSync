using Steamworks;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace TTSCloudSync;

partial class SteamCloud
{
    public struct RemoteItem
    {
        public string Name;
        public string ShareName;
        public long Size;
        public string Sha1;
    }

    public static void ConnectToSteam(string appId)
    {
        // Ignored (not the right process?).
        Environment.SetEnvironmentVariable("SteamAppID", appId);
        //bool initialized = SteamAPI.Init();
        bool initialized = false;
        if (!initialized)
        {
            Console.Error.WriteLine("Failure");
            // SteamAPI uses the app id from the environment variable, but if it's
            // not available, it uses a steam_appid file.
            File.WriteAllText("steam_appid.txt", appId);
            initialized = SteamAPI.Init();
            File.Delete("steam_appid.txt");
        }

        if (!initialized)
        {
            throw new Exception("Cannot connect to with ID '" + appId + "'.");
        }
    }

    public static void DisconnectFromSteam()
    {
        SteamAPI.Shutdown();
    }

    [GeneratedRegex("^[a-z]")]
    private static partial Regex FirstLetterAsChar();

    public static Dictionary<UniKey, RemoteItem> ListItems()
    {
        Dictionary<UniKey, RemoteItem> remoteItems = new();
        int fileCount = SteamRemoteStorage.GetFileCount();
        for (int i = 0; i < fileCount; ++i)
        {
            // A Steam Cloud has a flat structure, without folders.
            string fileName = SteamRemoteStorage.GetFileNameAndSize(i, out int fileSizeInBytes);

            byte[] data = new byte[fileSizeInBytes];
            SteamRemoteStorage.FileRead(fileName, data, fileSizeInBytes);

            string sha1 = BitConverter.ToString(SHA1.HashData(data)).Replace("-", "");

            string name = fileName;
            if (name.StartsWith(sha1 + "_"))
            {
                name = name[(sha1.Length + 1)..];
            }
            else if (!TabletopSimulatorCloud.TTS_SPECIAL_FILE_NAMES.Contains(fileName))
            {
                Console.Error.WriteLine($"Suspicious file name (missing SHA1 prefix): {fileName}");
            }

            RemoteItem item = new()
            {
                Name = name,
                ShareName = fileName,
                Size = fileSizeInBytes,
                Sha1 = sha1,
            };

            if (!remoteItems.TryAdd(new UniKey(name, sha1), item))
            {
                Console.Error.WriteLine($"Ignoring remote item '{fileName}'.");
            }
        }
        return remoteItems;
    }

    public static byte[] GetFile(string name)
    {
        int size = SteamRemoteStorage.GetFileSize(name);
        byte[] bytes = new byte[size];
        SteamRemoteStorage.FileRead(name, bytes, size);
        return bytes;
    }

    public static void DeleteFile(string name, string? sha1)
    {
        List<string> potentialNames = new()
        {
            name
        };
        if (sha1 != null)
        {
            int underscoreIndex = name.IndexOf(sha1);
            if (underscoreIndex != -1)
            {
                potentialNames.Add(name[(underscoreIndex + 1)..]);
            }
            else
            {
                potentialNames.Add($"{sha1}_{name}");
            }
        }
        bool deleted = false;
        foreach (string potentialName in potentialNames)
        {
            if (SteamRemoteStorage.FileDelete(potentialName))
            {
                deleted = true;
            }
        }
        if (!deleted)
        {
            Console.Error.WriteLine("Failed to delete file: " + name);
        }
    }

    // TTS use the combination of the file name and sha1 for the shared name
    // and TTS expects it to properly delete it when done though its UI.
    public static string ToSteamName(string sha1, string name)
    {
        return $"{sha1}_{name}";
    }

    // Beware: the naming rule is not the same here (only used for metadata file)!
    public static void UploadFile(string name, byte[] data)
    {
        if (!SteamRemoteStorage.FileWrite(name, data, data.Length))
        {
            Console.Error.WriteLine("Failed to upload file: " + name);
        }
    }

    public static bool UploadFileAndShare(string name, byte[] data, [NotNullWhen(true)] out RemoteItem? remoteItem, [NotNullWhen(true)] out UgcUrl? ugcUrl)
    {
        string sha1 = BitConverter.ToString(SHA1.HashData(data)).Replace("-", "");
        if (SteamRemoteStorage.FileWrite(ToSteamName(sha1, name), data, data.Length))
        {
            var sharer = new FileSharer(name, sha1);
            sharer.Share().Wait();
            Debug.Assert(sharer.UgcUrl != null);
            remoteItem = new RemoteItem()
            {
                Name = name,
                ShareName = sharer.Name,
                Size = data.Length,
                Sha1 = sha1,
            };
            ugcUrl = sharer.UgcUrl;
            return true;
        }
        else
        {
            remoteItem = null;
            ugcUrl = null;
            return false;
        }
    }

    private class FileSharer
    {
        private readonly CallResult<RemoteStorageFileShareResult_t> Callback;
        public readonly string Name;
        private readonly string Sha1;
        private bool Finished;
        public UgcUrl? UgcUrl;

        public FileSharer(string name, string sha1)
        {
            Callback = CallResult<RemoteStorageFileShareResult_t>.Create(OnShareFinished);
            Name = name;
            Sha1 = sha1;
            Finished = false;
            UgcUrl = null;
        }

        public async Task<UgcUrl?> Share()
        {
            var result = SteamRemoteStorage.FileShare(ToSteamName(Sha1, Name));
            Callback.Set(result);
            while (!Finished)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(100);
            }
            return UgcUrl;
        }

        private void OnShareFinished(RemoteStorageFileShareResult_t result, bool fail)
        {
            if (fail || result.m_eResult != EResult.k_EResultOK)
            {
                Console.Error.WriteLine($"Failed to share the file '{Name}' ({result.m_eResult}).");
                UgcUrl = null;
                Finished = true;
            }
            else
            {
                UgcUrl = new UgcUrl((ulong)result.m_hFile, Sha1);
                Finished = true;
            }
        }
    }

    public static bool DownloadFile(string url, string directoryName, string name)
    {
        UgcUrl? ugcUrl = UgcUrl.Parse(url);
        if (ugcUrl is not null)
        {
            UGCHandle_t hContent = new(ugcUrl.Value.Handle);
            var downloader = new FileDownloader(hContent, directoryName, name);
            Task<bool> task = downloader.Download();
            task.Wait();
            if (task.Result)
            {
                return true;
            }
            else
            {
                Console.Error.WriteLine("Failed to donwnload content: " + hContent);
            }
        }
        else
        {
            Console.Error.WriteLine("Malformed UGC URL: " + url);
        }
        return false;
    }

    private class FileDownloader
    {
        private readonly UGCHandle_t Handle;
        private readonly string DirectoryName;
        private readonly string Name;
        private readonly CallResult<RemoteStorageDownloadUGCResult_t> Callback;
        private bool success;
        private bool Finished;

        public FileDownloader(UGCHandle_t handle, string directoryName, string name)
        {
            Handle = handle;
            DirectoryName = directoryName;
            Name = name;
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
                Directory.CreateDirectory(DirectoryName);

                string filePath = Path.Combine(DirectoryName, Name);
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
