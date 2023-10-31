using Steamworks;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace TTSCloudSync;

class SteamCloud
{
    public struct RemoteItem
    {
        public string Name;
        public string Folder;
        public long Size;
        public string Sha1;
    }

    public static void ConnectToSteam(string appId)
    {
        Environment.SetEnvironmentVariable("SteamAppID", appId);
        //bool initialized = SteamAPI.Init();
        bool initialized = false;
        if (!initialized)
        {
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

    public static Dictionary<string, RemoteItem> ListItems()
    {
        Dictionary<string, RemoteItem> remoteItems = new ();
        int fileCount = SteamRemoteStorage.GetFileCount();
        for (int i = 0; i < fileCount; ++i)
        {
            string fileName = SteamRemoteStorage.GetFileNameAndSize(i, out int fileSizeInBytes);
            int lastPathSeparatorIndex = fileName.LastIndexOf('/');
            string name = fileName[(lastPathSeparatorIndex + 1)..];
            string folder = lastPathSeparatorIndex != -1 ? fileName[0..lastPathSeparatorIndex] : "";

            byte[] data = new byte[fileSizeInBytes];
            SteamRemoteStorage.FileRead(fileName, data, fileSizeInBytes);

            string sha1 = BitConverter.ToString(SHA1.HashData(data)).Replace("-", "");

            RemoteItem item = new()
            {
                Name = name,
                Folder = folder,
                Size = fileSizeInBytes,
                Sha1 = sha1,
            };

            string key;
            if (fileName.StartsWith(sha1))
            {
                key = item.Name;
            }
            else
            {
                key = sha1 + '_' + item.Name;
            }

            remoteItems.Add(key, item);
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

    public static void DeleteFile(string name)
    {
        if (!SteamRemoteStorage.FileDelete(name))
        {
            Console.Error.WriteLine("Failed to delete file: " + name);
        }
    }

    public static void UploadFile(string name, byte[] data)
    {
        if (!SteamRemoteStorage.FileWrite(name, data, data.Length))
        {
            Console.Error.WriteLine("Failed to upload file: " + name);
        }
    }

    public static bool UploadFileAndShare(string name, byte[] data, [MaybeNullWhen(false)] out string URL)
    {
        if (SteamRemoteStorage.FileWrite(name, data, data.Length))
        {
            string sha1 = BitConverter.ToString(SHA1.HashData(data)).Replace("-", "");
            var sharer = new FileSharer(name, sha1);
            sharer.Share().Wait();
            Debug.Assert(sharer.URL != null);
            URL = sharer.URL;
            return true;
        }
        else
        {
            URL = null;
            return false;
        }
    }

    private class FileSharer
    {
        private readonly CallResult<RemoteStorageFileShareResult_t> Callback;
        private readonly string Name;
        private readonly string Sha1;
        private bool Finished;
        public string? URL;

        public FileSharer(string name, string sha1)
        {
            Callback = CallResult<RemoteStorageFileShareResult_t>.Create(OnShareFinished);
            Name = name;
            Sha1 = sha1;
            Finished = false;
            URL = "";
        }

        public async Task<string?> Share()
        {
            var ret = SteamRemoteStorage.FileShare(Name);
            Callback.Set(ret);
            while (!Finished)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(100);
            }
            return URL;
        }

        private void OnShareFinished(RemoteStorageFileShareResult_t result, bool fail)
        {
            if (fail || result.m_eResult != EResult.k_EResultOK)
            {
                URL = null;
                Finished = true;
            }
            else
            {
                URL = "http://cloud-3.steamusercontent.com/ugc/" + result.m_hFile.ToString() + "/" + Sha1 + "/";
                Finished = true;
            }
        }
    }

}
