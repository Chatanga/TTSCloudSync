using Steamworks;

namespace TTSCloudSync;

class Program
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
        Console.WriteLine("Unhandled exception (" + e.ExceptionObject.GetType() + "): " + e.ExceptionObject);
    }

    static void Main(string[] args)
    {
        Synchronize("Dune Immorality", "cloud");
        //Synchronize("Temp", "new");
    }

    static void Synchronize(string remoteRootFolder, string localRootSubFolder)
    {
        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(HandleException);

        SteamCloud.ConnectToSteam(TabletopSimulatorCloud.TTS_APP_ID);
        try
        {
            string localRootFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                + "/Personnel/Productions/Code/Maison/DuneImperiumTTS/resources/"
                + localRootSubFolder;

            //Console.Error.WriteLine("Listing local file system items...");
            var fileItems = LocalFileSystem.ListItems(localRootFolder, remoteRootFolder);

            //Console.Error.WriteLine("Listing Steam cloud items...");
            var remoteItems = SteamCloud.ListItems();

            //Console.Error.WriteLine("Listing TTS cloud items...");
            var cloudItems = TabletopSimulatorCloud.ListItems();

            HashSet<string> allKeys = new ();

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
                allKeys.Add(entry.Key);
            }

            foreach (var key in allKeys)
            {
                bool ok = true;
                if (remoteItems.TryGetValue(key, out SteamCloud.RemoteItem remoteItem))
                {
                    ok = false;
                    if (remoteItem.Folder != "")
                    {
                        Console.Error.WriteLine("Delete anomality: " + key);
                        SteamCloud.DeleteFile(remoteItem.Name);
                    }
                    else
                    {
                        string[] specialFileNames = {
                            "WorkshopImageUpload.png",
                            "WorkshopUpload",
                            "CloudFolder.bson",
                            "CloudInfo.bson",
                        };
                        if (specialFileNames.Contains(remoteItem.Name))
                        {
                            //Console.Error.WriteLine("Skip: " + key);
                        }
                        else if (!cloudItems.ContainsKey(key))
                        {
                            Console.Error.WriteLine("Delete zombie: " + key);
                            SteamCloud.DeleteFile(remoteItem.Name);
                        }
                        else
                        {
                            ok = true;
                        }
                    }
                }
                else if (cloudItems.TryGetValue(key, out TabletopSimulatorCloud.CloudItem cloudItem))
                {
                    Console.Error.WriteLine("Delete ghost: " + key);
                    cloudItems.Remove(key);
                }

                if (ok)
                {
                    if (fileItems.TryGetValue(key, out LocalFileSystem.LocalItem fileItem))
                    {
                        if (cloudItems.TryGetValue(key, out TabletopSimulatorCloud.CloudItem cloudItem))
                        {
                            if (fileItem.Folder != cloudItem.Folder)
                            {
                                if (!cloudItem.Folder.StartsWith(remoteRootFolder))
                                {
                                    Console.Error.WriteLine("Ignore local relocation: " + key);
                                }
                                else
                                {
                                    Console.Error.WriteLine("Move: " + key);
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
                            Console.Error.WriteLine("Add and share: " + key);
                            byte[] data = File.ReadAllBytes(fileItem.DirectoryName + "/" + fileItem.Name);
                            if (SteamCloud.UploadFileAndShare(fileItem.Name, data, out string? URL))
                            {
                                cloudItems.Add(key, new TabletopSimulatorCloud.CloudItem () {
                                    Name = fileItem.Name,
                                    URL = URL,
                                    Size = fileItem.Size,
                                    Date = fileItem.Date,
                                    Folder = fileItem.Folder,
                                });
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
                            Console.Error.WriteLine("Delete: " + key);
                            SteamCloud.DeleteFile(cloudItem.Name);
                            cloudItems.Remove(key);
                        }
                    }
                }
            }

            TabletopSimulatorCloud.UploadTableOfContent(cloudItems);

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
