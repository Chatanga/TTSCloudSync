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

            RemoteItem item = new()
            {
                Name = name,
                ShareName = fileName,
                Size = fileSizeInBytes,
                Sha1 = sha1,
            };

            remoteItems.Add(new UniKey(name, sha1), item);
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
        List<string> potentialNames = new();
        potentialNames.Add(name);
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
        foreach (string potentialName in potentialNames)
        {
            if (SteamRemoteStorage.FileDelete(potentialName))
            {
                return;
            }
        }
        Console.Error.WriteLine("Failed to delete file: " + name);
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
            // TTS UI use the combination of the file name and sha1 for the shared name.
            // We don't replicate this behavior? Seems to matter in the end.
            // For Steam, the Name is the key and the case doesn't matter.
            // Using the Sha1_Name format is also mandatory since TTS layer expect it (and won't be able do delete it otherwise).
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
