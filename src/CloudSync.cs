using Steamworks;

namespace TTSCloudSync;

class CloudSync
{
    public static void Main(string[] args)
    {
        CommandLineParser parser = new();
        parser.AddOption("--push");
        parser.AddOption("--pull");
        parser.AddOption("--dry-run");
        (Dictionary<string, string?> options, List<string> arguments) = parser.Parse(args);

        bool push = options.ContainsKey("--push") || !options.ContainsKey("--pull");
        bool dryRun = options.ContainsKey("--dry-run");

        string localRoot = ".";
        string remoteRoot = ".";

        switch (arguments.Count)
        {
            case 1:
                localRoot = arguments[0];
                break;
            case 2:
                localRoot = arguments[0];
                remoteRoot = arguments[1];
                break;
            default:
                Console.Error.WriteLine("Usage: sync-with-cloud [--pull] [--dry-run] LOCAL_PATH [TTS_STEAM_CLOUD_PATH] [> MAPPING]");
                Environment.Exit(1);
                break;
        }

        SteamCloud.ConnectToSteam(TabletopSimulatorCloud.TTS_APP_ID);
        try
        {
            new CloudSync(GetCanonicalFullPath(localRoot), remoteRoot, push, dryRun).Synchronize();
        }
        finally
        {
            SteamAPI.Shutdown();
        }
    }

    private static string GetCanonicalFullPath(string path)
    {
        string? canonicalFullPath = Path.GetFullPath(path);
        ArgumentNullException.ThrowIfNull(canonicalFullPath);
        canonicalFullPath = Path.TrimEndingDirectorySeparator(canonicalFullPath);
        return canonicalFullPath;
    }

    private readonly string LocalRootPath;

    private readonly string RemoteRootFolder;

    private readonly Dictionary<UniKey, LocalFileSystem.LocalItem> FileItems;

    private readonly Dictionary<UniKey, SteamCloud.RemoteItem> RemoteItems;

    private readonly Dictionary<UniKey, TabletopSimulatorCloud.CloudItem> CloudItems;

    private readonly bool Push;

    private readonly bool DryRun;

    // Shared = listed in the Steam Cloud (which is a flat list of files by the way).
    // Known = listed in TTS CloudInfo.bson file (which is a hierarchical directory layer).
    public CloudSync(string localRootPath, string remoteRootPath, bool push, bool dryRun)
    {
        LocalRootPath = localRootPath;
        RemoteRootFolder = Path.GetFileName(remoteRootPath);
        ArgumentNullException.ThrowIfNull(RemoteRootFolder);

        FileItems = LocalFileSystem.ListItems(localRootPath, remoteRootPath);
        RemoteItems = SteamCloud.ListItems();
        CloudItems = TabletopSimulatorCloud.ListItems();

        Push = push;
        DryRun = dryRun;
    }

    private void Upload(UniKey key)
    {
        FileItems.TryGetValue(key, out LocalFileSystem.LocalItem fileItem);
        byte[] data = File.ReadAllBytes(Path.Combine(fileItem.DirectoryName, fileItem.Name));

        if (TabletopSimulatorCloud.IsDistinct(CloudItems, fileItem, data))
        {
            Console.Error.Write($"Add, share and remember: {key}\n");
            if (!DryRun)
            {
                if (SteamCloud.UploadFileAndShare(fileItem.Name, data, out SteamCloud.RemoteItem? remoteItem, out UgcUrl? URL))
                {
                    CloudItems.Remove(key);
                    CloudItems.Add(key, new TabletopSimulatorCloud.CloudItem()
                    {
                        Name = fileItem.Name,
                        URL = URL.ToString() ?? "",
                        Size = fileItem.Size,
                        Date = fileItem.Date,
                        Folder = fileItem.Folder,
                    });

                    RemoteItems.Remove(key);
                    RemoteItems.Add(key, remoteItem.Value);
                }
                else
                {
                    Console.Error.WriteLine("Failed!");
                }
            }
            else
            {
                CloudItems.Remove(key);
                CloudItems.Add(key, new TabletopSimulatorCloud.CloudItem()
                {
                    Name = fileItem.Name,
                    URL = "undefined",
                    Size = fileItem.Size,
                    Date = fileItem.Date,
                    Folder = fileItem.Folder,
                });

                RemoteItems.Remove(key);
                RemoteItems.Add(key, new SteamCloud.RemoteItem()
                {
                    Name = fileItem.Name,
                    ShareName = "undefined",
                    Size = 0,
                    Sha1 = "undefined",
                });
            }
        }
        else
        {
            Console.Error.Write($"Ignore duplicated local file (same case insensitive name and content): {key}\n");
            // Fixing TTS index if needed.
            if (!RemoteItems.ContainsKey(key))
            {
                CloudItems.Remove(key);
            }
        }
    }

    private void Download(UniKey key)
    {
        if (RemoteItems.TryGetValue(key, out SteamCloud.RemoteItem remoteItem))
        {
            if (CloudItems.TryGetValue(key, out TabletopSimulatorCloud.CloudItem cloudItem))
            {
                if (cloudItem.Folder == RemoteRootFolder || cloudItem.Folder.StartsWith(RemoteRootFolder + "/"))
                {
                    string directoryName = Path.Combine(LocalRootPath, cloudItem.Folder);
                    Console.Error.Write($"Download: {key}\n");
                    if (!DryRun)
                    {
                        if (SteamCloud.DownloadFile(cloudItem.URL, directoryName, cloudItem.Name))
                        {
                            FileItems.Remove(key);
                            FileItems.Add(key, new LocalFileSystem.LocalItem()
                            {
                                Name = remoteItem.Name,
                                Size = remoteItem.Size,
                                Sha1 = remoteItem.Sha1,
                                DirectoryName = directoryName,
                                Folder = cloudItem.Folder,
                            });
                        }
                        else
                        {
                            Console.Error.WriteLine("Failed!");
                        }
                    }
                    else
                    {
                        FileItems.Remove(key);
                        FileItems.Add(key, new LocalFileSystem.LocalItem()
                        {
                            Name = remoteItem.Name,
                            Size = remoteItem.Size,
                            Sha1 = remoteItem.Sha1,
                            DirectoryName = directoryName,
                            Folder = cloudItem.Folder,
                        });
                    }
                }
            }
        }
    }

    private void Remove(UniKey key)
    {
        if (Push)
        {
            if (RemoteItems.TryGetValue(key, out SteamCloud.RemoteItem remoteItem))
            {
                if (TabletopSimulatorCloud.TTS_SPECIAL_FILE_NAMES.Contains(remoteItem.Name))
                {
                    //Console.Error.WriteLine("Skip: " + key);
                }
                else
                {
                    Console.Error.WriteLine($"Delete and forget: {key}");
                    if (!DryRun)
                    {
                        SteamCloud.DeleteFile(remoteItem.Name, remoteItem.Sha1);
                    }
                }
                RemoteItems.Remove(key);
                CloudItems.Remove(key);
            }
        }
        else
        {
            if (FileItems.TryGetValue(key, out LocalFileSystem.LocalItem fileItem))
            {
                Console.Error.WriteLine($"Delete: {key}");
                if (!DryRun)
                {
                    LocalFileSystem.DeleteFile(fileItem);
                }
                FileItems.Remove(key);
            }
        }
    }

    private void RemoveIfNotElsewhere(UniKey key)
    {
        if (CloudItems.TryGetValue(key, out TabletopSimulatorCloud.CloudItem cloudItem) && IsOutside(key, cloudItem))
        {
            //Console.Error.WriteLine("Ignore: " + key);
        }
        else
        {
            Remove(key);
        }
    }

    private void MoveIfNeeded(UniKey key)
    {
        CloudItems.TryGetValue(key, out TabletopSimulatorCloud.CloudItem cloudItem);
        FileItems.TryGetValue(key, out LocalFileSystem.LocalItem fileItem);

        if (fileItem.Folder != cloudItem.Folder)
        {
            if (IsOutside(key, cloudItem))
            {
                Console.Error.WriteLine($"Ignore local relocation: {key}");
            }
            else if (Push)
            {
                Console.Error.WriteLine($"Move: {key} ({fileItem.Folder} -> {cloudItem.Folder})");
                cloudItem.Folder = fileItem.Folder;
                CloudItems.Remove(key);
                CloudItems.Add(key, cloudItem);
            }
            else
            {
                Console.Error.WriteLine($"Move: {key} ({fileItem.Folder} <- {cloudItem.Folder})");
                if (!DryRun)
                {
                    LocalFileSystem.MoveFile(fileItem, cloudItem.Folder);
                }
                else
                {
                    fileItem.Folder = cloudItem.Folder;
                }
                FileItems.Remove(key);
                FileItems.Add(key, fileItem);
            }
        }
        else
        {
            Console.Error.WriteLine($"Keep: {key}");
        }
    }

    private bool IsOutside(UniKey key, TabletopSimulatorCloud.CloudItem cloudItem)
    {
        // TODO Questionable, to be reworked.
        return !cloudItem.Folder.StartsWith(RemoteRootFolder);
    }

    public void Synchronize()
    {
        Dictionary<(bool, bool, bool), Action<UniKey>?> actions;
        if (Push)
        {
            actions = new()
            {
                // remote   cloud   local
                // (Steam)  (TTS)   (File System)

                { (false,   false,  false), null },
                { (false,   false,  true),  Upload },

                { (false,   true,   false), Remove },
                { (false,   true,   true),  Upload },

                { (true,    false,  false), Remove },
                { (true,    false,  true),  Upload },

                { (true,    true,   false), RemoveIfNotElsewhere },
                { (true,    true,   true),  MoveIfNeeded },
            };
        }
        else
        {
            actions = new()
            {
                // remote   cloud   local
                // (Steam)  (TTS)   (File System)

                { (false,   false,  false), null },
                { (false,   false,  true),  Remove },

                { (false,   true,   false), null },
                { (false,   true,   true),  null },

                { (true,    false,  false), null },
                { (true,    false,  true),  null },

                { (true,    true,   false), Download },
                { (true,    true,   true),  MoveIfNeeded },
            };
        }

        HashSet<Action<UniKey>>[] allowedActions =
        {
            new() {Remove, RemoveIfNotElsewhere},
            new() {Upload, Download, MoveIfNeeded},
        };

        Console.Error.WriteLine($"Local File System: {FileItems.Count} / Steam Cloud: {RemoteItems.Count} / TTS index: {CloudItems.Count}");

        for (int i = 0; i < 2; ++i)
        {
            HashSet<UniKey> allKeys = new();

            foreach (var entry in FileItems)
            {
                allKeys.Add(entry.Key);
            }

            foreach (var entry in RemoteItems)
            {
                allKeys.Add(entry.Key);
            }

            foreach (var entry in CloudItems)
            {
                allKeys.Add(entry.Key);
            }

            Console.Error.WriteLine(i == 0 ? "---" : "+++");
            foreach (var key in allKeys)
            {
                var state = (
                    RemoteItems.ContainsKey(key),
                    CloudItems.ContainsKey(key),
                    FileItems.ContainsKey(key));

                if (actions.TryGetValue(state, out Action<UniKey>? action))
                {
                    //ArgumentNullException.ThrowIfNull(action);
                    if (action is not null)
                    {
                        if (allowedActions[i].Contains(action))
                        {
                            action(key);
                        }
                    }
                }
            }
        }

        if (!DryRun)
        {
            TabletopSimulatorCloud.UploadTableOfContent(CloudItems);
        }

        // Dump the mapping in stdout (other messages are sent to stderr).
        foreach (var entry in CloudItems)
        {
            if (entry.Value.URL != "undefined")
            {
                Console.WriteLine(entry.Key + ";" + entry.Value.URL);
            }
            else if (!DryRun)
            {
                Console.Error.WriteLine($"Unexpected undefined entry {entry.Key}!");
            }
        }
    }
}
