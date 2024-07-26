using System.Diagnostics;
using System.Security.Cryptography;
using Steamworks;

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

    public static Dictionary<UniKey, LocalItem> ListItems(string localRootPath, string remoteRootPath)
    {
        Dictionary<UniKey, LocalItem> localItems = new();
        DirectoryInfo dirInfo = new(localRootPath);
        foreach (var fileInfo in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            Debug.Assert(fileInfo.DirectoryName != null && fileInfo.DirectoryName.StartsWith(localRootPath));

            byte[] data = File.ReadAllBytes(fileInfo.FullName);
            string sha1 = BitConverter.ToString(SHA1.HashData(data)).Replace("-", "");

            // Stick to the UNIX format (which is also used by TTS) to ease comparisons.
            string folder = Path.GetFileName(remoteRootPath) + FromNativePath(fileInfo.DirectoryName[localRootPath.Length..]);

            LocalItem item = new()
            {
                Name = fileInfo.Name,
                Size = fileInfo.Length,
                Sha1 = sha1,
                Date = fileInfo.CreationTime.ToString("d'/'M'/'yyyy' 'H':'mm':'ss tt"),
                DirectoryName = fileInfo.DirectoryName,
                Folder = folder,
            };

            UniKey key = new(item.Name, sha1);

            if (localItems.TryGetValue(key, out LocalItem oldItem))
            {
                Console.WriteLine("Relocating " + item.Name + " from " + oldItem.Folder + " to " + item.Folder + " (TTS doesn't allow multiple instances of the same file.)");
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

    public static void MoveFile(LocalItem fileItem, string newFolder)
    {
        string path = Path.Combine(fileItem.DirectoryName, fileItem.Name);
        string? oldPathSuffix = GetDirectorySuffix(ToNativePath(fileItem.Folder));
        string? newPathSuffix = GetDirectorySuffix(ToNativePath(newFolder));
        string newDirectoryName = Path.Combine(Sever(fileItem.DirectoryName, oldPathSuffix), newPathSuffix ?? "");
        string newPath = Path.Combine(newDirectoryName, fileItem.Name);
        Console.Error.WriteLine($"File.Move({path}, {newPath})");
        File.Move(path, newPath);
        fileItem.DirectoryName = newDirectoryName;
        fileItem.Folder = newFolder;
    }

    public static void BackupFile(string name, byte[] data)
    {
        File.WriteAllBytes(DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss_") + name, data);
    }

    private static string FromNativePath(string path)
    {
        if (Path.DirectorySeparatorChar != '/')
        {
            return path.Replace(Char.ToString(Path.DirectorySeparatorChar), "/");
        }
        else
        {
            return path;
        }
    }

    private static string ToNativePath(string path)
    {
        if (Path.DirectorySeparatorChar != '/')
        {
            return path.Replace("/", Char.ToString(Path.DirectorySeparatorChar));
        }
        else
        {
            return path;
        }
    }

    public static string Sever(string? path, string? subPath)
    {
        if (path == null || subPath == null || subPath.Length == 0)
        {
            return path ?? "";
        }
        else if (Path.EndsInDirectorySeparator(path) == Path.EndsInDirectorySeparator(subPath))
        {
            return Sever(Path.GetDirectoryName(path), Path.GetDirectoryName(subPath));
        }
        else if (Path.EndsInDirectorySeparator(path))
        {
            return Sever(Path.GetDirectoryName(path), subPath);
        }
        else
        {
            return Sever(path, Path.GetDirectoryName(subPath));
        }
    }

    public static string? GetDirectorySuffix(string path)
    {
        int index = path.IndexOf(Path.AltDirectorySeparatorChar);
        if (index != -1)
        {
            return path[(index + 1)..];
        }
        else
        {
            return null;
        }
    }
}
