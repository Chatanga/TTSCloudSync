using System.Diagnostics;
using System.Security.Cryptography;

namespace TTSCloudSync;

class LocalFileSystem
{
    public struct LocalItem
    {
        public string Name;
        public long Size;
        public string Sha1;
        public string Date;
        public string DirectoryName;
        public string Folder;
    }

    public static Dictionary<string, LocalItem> ListItems(string rootFolder, string virtualRootFolder)
    {
        Dictionary<string, LocalItem> localItems = new ();
        DirectoryInfo dirInfo = new (rootFolder);
        foreach (var fileInfo in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            Debug.Assert(fileInfo.DirectoryName != null && fileInfo.DirectoryName.StartsWith(rootFolder));

            byte[] data = File.ReadAllBytes(fileInfo.FullName);
            string sha1 = BitConverter.ToString(SHA1.HashData(data)).Replace("-", "");

            LocalItem item = new()
            {
                Name = fileInfo.Name,
                Size = fileInfo.Length,
                Sha1 = sha1,
                Date = fileInfo.CreationTime.ToString("d'/'M'/'yyyy' 'H':'mm':'ss tt"),
                DirectoryName = fileInfo.DirectoryName,
                Folder = virtualRootFolder + ("/" + fileInfo.DirectoryName)[(rootFolder.Length + 1)..],
            };

            string key = sha1 + '_' + item.Name;

            if (localItems.TryGetValue(key, out LocalItem oldItem))
            {
                Console.WriteLine("Relocating " + item.Name + " from " + oldItem.Folder + " to " + item.Folder + " (TTS doesn't allow multiple instance of the same file.)");
            }

            localItems.Add(key, item);
        }
        return localItems;
    }

    public static void BackupFile(string name, byte[] data)
    {
        File.WriteAllBytes(DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_") + name, data);
    }

}
