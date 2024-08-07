using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Steamworks;
using System.Security.Cryptography;

namespace TTSCloudSync;

class TabletopSimulatorCloud
{
    public static readonly string TTS_APP_ID = "286160";

    public static readonly char FOLDER_SEPARATOR = '/';

    public static readonly HashSet<string> TTS_SPECIAL_FILE_NAMES = new(){
        "WorkshopImageUpload.png",
        "WorkshopUpload",
        "CloudFolder.bson",
        "CloudInfo.bson",
    };

    public struct CloudItem
    {
        public string Name;
        public string URL;
        public long Size;
        public string Date;
        public string Folder;

        public CloudItem(string name, string url, long size, string date, string folder)
        {
            Name = name;
            URL = url;
            Size = size;
            Date = date;
            Folder = folder;
        }
    }

    public static Dictionary<UniKey, CloudItem> ListItems()
    {
        if (SteamRemoteStorage.FileExists("CloudInfo.bson"))
        {
            var data = SteamCloud.GetFile("CloudInfo.bson");
            var rawCloudInfo = ParseBson<Dictionary<string, CloudItem>>(data);
            if (rawCloudInfo is null)
            {
                throw new Exception("Malformed 'CloudInfo.bson' file.");
            }
            return rawCloudInfo.ToDictionary(kvp => new UniKey(kvp.Key), kvp => kvp.Value);
        }
        else
        {
            return new Dictionary<UniKey, CloudItem>();
        }
    }

    public static List<string> ListFolders()
    {
        if (SteamRemoteStorage.FileExists("CloudFolder.bson"))
        {
            var data = SteamCloud.GetFile("CloudFolder.bson");

            var cloudFolder = ParseBson<List<string>>(data);
            if (cloudFolder is null)
            {
                throw new Exception("Malformed 'CloudFolder.bson' file.");
            }
            return cloudFolder;
        }
        else
        {
            return new List<string>();
        }
    }

    public static bool IsDistinct(Dictionary<UniKey, CloudItem> cloudItems, LocalFileSystem.LocalItem localItem, byte[] data)
    {
        string sha1 = BitConverter.ToString(SHA1.HashData(data)).Replace("-", "");
        foreach (var entry in cloudItems)
        {
            if (entry.Key.Name.ToUpper() == localItem.Name.ToUpper() && entry.Key.Sha1 == sha1)
            {
                return false;
            }
        }
        return true;
    }

    public static void UploadTableOfContent(Dictionary<UniKey, CloudItem> cloudInfo)
    {
        Dictionary<string, CloudItem> rawCloudInfo = cloudInfo.ToDictionary(kvp => "" + kvp.Key.ToString(), kvp => kvp.Value);
        //rawCloudInfo.ToList().ForEach(kvp => Console.Error.WriteLine($"{kvp.Key} -> {kvp.Value.Folder}"));
        byte[] cloudInfoData = ToBson(rawCloudInfo);
        SteamCloud.UploadFile("CloudInfo.bson", cloudInfoData);

        SortedSet<string> allFolders = new();
        foreach (var entry in cloudInfo)
        {
            string folder = entry.Value.Folder;
            while (folder != "")
            {
                allFolders.Add(folder);
                int lastPathSeparatorIndex = folder.LastIndexOf('/');
                folder = lastPathSeparatorIndex != -1 ? folder[0..lastPathSeparatorIndex] : "";
            }
        }

        List<string> folders = allFolders.ToList();
        //folders.ForEach(f => Console.Error.WriteLine($"Folder: {f}"));

        byte[] folderData = ToBson(folders);
        SteamCloud.UploadFile("CloudFolder.bson", folderData);
    }

    private static T? ParseBson<T>(byte[] data)
    {
        using MemoryStream memoryStream = new(data);
        using BsonDataReader bsonReader = new(memoryStream);
        JsonSerializer serializer = new();
        return serializer.Deserialize<T>(bsonReader);
    }

    private static byte[] ToBson(object obj)
    {
        byte[] result;
        using (MemoryStream memoryStream = new())
        {
            using (BsonDataWriter bsonWriter = new(memoryStream))
            {
                JsonSerializer serializer = new()
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                serializer.Serialize(bsonWriter, obj);
            }
            result = memoryStream.ToArray();
        }
        return result;
    }
}
