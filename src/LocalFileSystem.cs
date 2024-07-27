using System.ComponentModel;
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

    public static Dictionary<UniKey, LocalItem> ListItems(SPath localRootPath, SPath remoteRootPath)
    {
        Dictionary<UniKey, LocalItem> localItems = new();
        DirectoryInfo dirInfo = new(localRootPath.ToNativePath());
        foreach (var fileInfo in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            Debug.Assert(fileInfo.DirectoryName != null);

            byte[] data = File.ReadAllBytes(fileInfo.FullName);
            string sha1 = BitConverter.ToString(SHA1.HashData(data)).Replace("-", "");

            SPath? folder = SPath.FromNativePath(fileInfo.DirectoryName).Relativize(localRootPath);
            Debug.Assert(folder != null);

            folder = remoteRootPath.Combine(folder);

            LocalItem item = new()
            {
                Name = fileInfo.Name,
                Size = fileInfo.Length,
                Sha1 = sha1,
                Date = fileInfo.CreationTime.ToString("d'/'M'/'yyyy' 'H':'mm':'ss tt"),
                DirectoryName = fileInfo.DirectoryName,
                Folder = folder.ToTTSPath(),
            };

            UniKey key = new(item.Name, sha1);

            if (localItems.TryGetValue(key, out LocalItem oldItem))
            {
                Console.WriteLine($"Relocating {item.Name} from {oldItem.Folder} to {item.Folder} (TTS doesn't allow multiple instances of the same file.)");
            }

            localItems.Add(key, item);
        }
        return localItems;
    }

    public static void DeleteFile(LocalItem fileItem)
    {
        string path = Path.Combine(fileItem.DirectoryName, fileItem.Name);
        File.Delete(path);
    }

    public static void MoveFile(SPath localRootPath, SPath remoteRootFolder, LocalItem fileItem, string newFolder)
    {
        SPath? oldFolderPath = SPath.FromNativePath(fileItem.Folder).Prune(remoteRootFolder);
        SPath? newFolderPath = SPath.FromTTSPath(newFolder).Prune(remoteRootFolder);

        if (oldFolderPath is not null && newFolderPath is not null)
        {
            SPath oldDirectoryPath = SPath.FromNativePath(fileItem.DirectoryName);
            SPath newDirectoryPath = localRootPath.Combine(newFolderPath);

            //Console.Error.WriteLine($"[DEBUG] oldFolderPath: {oldFolderPath}");
            //Console.Error.WriteLine($"[DEBUG] newFolderPath: {newFolderPath}");
            //Console.Error.WriteLine($"[DEBUG] oldDirectoryPath: {oldDirectoryPath}");
            //Console.Error.WriteLine($"[DEBUG] newDirectoryPath: {newDirectoryPath}");

            if (newDirectoryPath is not null)
            {
                Directory.CreateDirectory(newDirectoryPath.ToNativePath());
                File.Move(oldDirectoryPath.Resolve(fileItem.Name).ToNativePath(), newDirectoryPath.Resolve(fileItem.Name).ToNativePath());
                CleanUpTree(oldDirectoryPath, oldFolderPath.GetLength());
                fileItem.DirectoryName = newDirectoryPath.ToNativePath();
                fileItem.Folder = newFolder;
            }
        }
    }

    private static void CleanUpTree(SPath directoryPath, int endDepth)
    {
        //Console.Error.WriteLine($"[DEBUG] CleanUpTree({directoryPath}, {endDepth})");

        SPath? dirPath = directoryPath;
        int depth = 0;
        while (dirPath is not null && depth < endDepth)
        {
            string path = dirPath.ToNativePath();
            if (IsDirectoryEmpty(path))
            {
                Directory.Delete(path);
            }
            else
            {
                break;
            }
            dirPath = dirPath.GetParent();
            ++depth;
        }
    }

    private static bool IsDirectoryEmpty(string path)
    {
        return !Directory.EnumerateFileSystemEntries(path).Any();
    }

    public static void BackupFile(string name, byte[] data)
    {
        File.WriteAllBytes(DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_") + name, data);
    }
}
