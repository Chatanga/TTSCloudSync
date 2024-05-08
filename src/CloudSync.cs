using Steamworks;

namespace TTSCloudSync;

class CloudSync
{
    private enum CloudCommand
    {
        Add,
        Replace,
        Move,
        Keep,
        Delete
    }

    private static void HandleException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.WriteLine($"Unhandled exception ({e.ExceptionObject.GetType()}): {e.ExceptionObject}");
    }

    public static void Main(string[] args)
    {
        //sync-with-cloud [--force] local_path [tts_steam_cloud_path] > mapping.lst

        string localRoot = ".";
        string remoteRoot = ".";

        switch (args.Length)
        {
            case 1:
                localRoot = args[0];
                break;
            case 2:
                localRoot = args[0];
                remoteRoot = args[1];
                break;
            default:
                Console.Error.WriteLine("Usage: ... [FILE]");
                Environment.Exit(1);
                break;
        }

        Synchronize(GetCanonicalFullPath(localRoot), remoteRoot, false);
    }

    static string GetCanonicalFullPath(string path)
    {
        string? canonicalFullPath = Path.GetFullPath(path);
        ArgumentNullException.ThrowIfNull(canonicalFullPath);
        canonicalFullPath = Path.TrimEndingDirectorySeparator(canonicalFullPath);
        return canonicalFullPath;
    }

    static void Synchronize(string localRootPath, string remoteRootPath, bool dryRun)
    {
        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(HandleException);

        SteamCloud.ConnectToSteam(TabletopSimulatorCloud.TTS_APP_ID);
        try
        {
            //Console.Error.WriteLine("Listing local file system items...");
            var fileItems = LocalFileSystem.ListItems(localRootPath, remoteRootPath);

            //Console.Error.WriteLine("Listing Steam cloud items...");
            var remoteItems = SteamCloud.ListItems();

            //Console.Error.WriteLine("Listing TTS cloud items...");
            var cloudItems = TabletopSimulatorCloud.ListItems();
            foreach (var entry in cloudItems)
            {
                //Console.Error.WriteLine("cloudItem -> " + entry.Key);
            }

            HashSet<UniKey> allKeys = new();

            foreach (var entry in fileItems)
            {
                allKeys.Add(entry.Key);
            }

            foreach (var entry in remoteItems)
            {
                allKeys.Add(entry.Key);
            }

            foreach (var entry in cloudItems)
            {
                //Console.Error.WriteLine(entry.Value.Name + " // " + entry.Value.Folder);
                allKeys.Add(entry.Key);
            }

            // Shared = listed in the Steam Cloud (which a flat list of files by the way).
            // Known = listed in TTS CloudInfo.bson file (which is hierarchical directory layer).

            HashSet<UniKey> allValidKeys = new();
            foreach (var key in allKeys)
            {
                if (remoteItems.TryGetValue(key, out SteamCloud.RemoteItem remoteItem))
                {
                    string[] ttsSpecialFileNames = {
                        "WorkshopImageUpload.png",
                        "WorkshopUpload",
                        "CloudFolder.bson",
                        "CloudInfo.bson",
                    };
                    if (ttsSpecialFileNames.Contains(remoteItem.Name))
                    {
                        //Console.Error.WriteLine("Skip: " + key);
                    }
                    else if (!cloudItems.ContainsKey(key))
                    {
                        Console.Error.WriteLine($"Delete unknown shared file: {key}");
                        if (!dryRun)
                        {
                            SteamCloud.DeleteFile(remoteItem.Name, remoteItem.Sha1);
                        }
                        // Updating remoteItems is not needed but we did nevertheless.
                        remoteItems.Remove(key);
                    }
                    else
                    {
                        allValidKeys.Add(key);
                    }
                }
                else if (cloudItems.TryGetValue(key, out TabletopSimulatorCloud.CloudItem cloudItem))
                {
                    Console.Error.WriteLine($"Forget unshared known file: {key}");
                    cloudItems.Remove(key);
                }
                else
                {
                    allValidKeys.Add(key);
                }
            }

            string? remoteRootFolder = Path.GetFileName(remoteRootPath);
            ArgumentNullException.ThrowIfNull(remoteRootFolder);

            foreach (var key in allValidKeys)
            {
                if (fileItems.TryGetValue(key, out LocalFileSystem.LocalItem fileItem))
                {
                    if (cloudItems.TryGetValue(key, out TabletopSimulatorCloud.CloudItem cloudItem))
                    {
                        if (fileItem.Folder != cloudItem.Folder)
                        {
                            if (!cloudItem.Folder.StartsWith(remoteRootFolder))
                            {
                                Console.Error.WriteLine($"Ignore local relocation: {key}");
                            }
                            else
                            {
                                Console.Error.WriteLine($"Move: {key} ({fileItem.Folder} != {cloudItem.Folder})");
                                cloudItem.Folder = fileItem.Folder;
                                cloudItems.Remove(key);
                                cloudItems.Add(key, cloudItem);
                            }
                        }
                        else
                        {
                            //Console.Error.WriteLine("Keep: " + key);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Add, share and remember: {key}");
                        if (!dryRun)
                        {
                            byte[] data = File.ReadAllBytes(fileItem.DirectoryName + "/" + fileItem.Name);
                            if (SteamCloud.UploadFileAndShare(fileItem.Name, data, out string? URL))
                            {
                                cloudItems.Add(key, new TabletopSimulatorCloud.CloudItem()
                                {
                                    Name = fileItem.Name,
                                    URL = URL,
                                    Size = fileItem.Size,
                                    Date = fileItem.Date,
                                    Folder = fileItem.Folder,
                                });
                            }
                        }
                    }
                }
                else if (cloudItems.TryGetValue(key, out TabletopSimulatorCloud.CloudItem cloudItem))
                {
                    if (!cloudItem.Folder.StartsWith(remoteRootFolder))
                    {
                        //Console.Error.WriteLine("Ignore: " + key);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Delete and forget: {key}");
                        if (!dryRun)
                        {
                            SteamCloud.DeleteFile(cloudItem.Name, null);
                            cloudItems.Remove(key);
                        }
                    }
                }
            }

            if (!dryRun)
            {
                TabletopSimulatorCloud.UploadTableOfContent(cloudItems);
            }

            // Dump the mapping in stdout (other messages are sent to stderr).
            foreach (var entry in cloudItems)
            {
                Console.WriteLine(entry.Key + ";" + entry.Value.URL);
            }
        }
        finally
        {
            SteamAPI.Shutdown();
        }
    }

}
