# TTS Cloud Sync

Synchronise a local folder with the Tabletop Simulator cloud.

## Commands

There are actually 4 command line tools to combine to update a TTS cloud.

```bash
extract-ugc-url [save]
```

Extract all the URLs for UGC (User-Generated Content) resources found in a JSON save.

```bash
download-ugc-resources [-o output_dir] [file]
```

Download the UGC URLs from a file (or the standard input) and store them in the provided output directory (or the current one).
Each resource ends up in a subdirectory named after the Steam ID of its owner.

```bash
sync-with-cloud [--force] local_path [tts_steam_cloud_path] > mapping.lst
```

Synchronise your Steam cloud for Tabletop Simulator with a local path, adding, removing or moving resource to match your local path content.
Implicitly, all resources will be shared on your cloud and the tool will output the updated mapping:

    file_name file_sha1 ugc_handle

The corresponding URL is `http://cloud-3.steamusercontent.com/ugc/<ugc_handle>/<file_sha1>/`.
A Steam cloud enforces uniqueness of (uppercase(file_name), file_sha1) pairs and the tool will notify of any duplicate in your local path.
It's important to understand that the directory structure in the TTS cloud is a pure TTS construction.
From the underlying Steam cloud perspective, there are only a flat list of files indexed by their file_name + file_sha1.
As such, you can have two different files named the same in the same location of your cloud (something you can't replicate locally however),
but you can't have the exact same file (same name and sha1) in two different directories.
Doing so remove the oldest duplicate (an operation TTS layer is not detecting, leaving a ghost entry in its index).
In addition, when a file is uploaded into a TTS cloud, it is also shared under an arbitrary UGC URL.
Deleting then uploading the exact same file will lead to a different UGC URL, leading to broken links in mod saves you have no control over.
Simply moving a file using TTS UI won't republish it hovewer, keeping its UGC URL stable.
Unless using the `--force` option, it won't push a local file with the same name and sha1 as another cloud resource ouside the provided `tts_steam_cloud_path`.

```bash
patch-ugc-url mapping.lst [save]
```

Patch all UGC URLs in the provided save (or standard input) to use one from your cloud with the same SHA1 if one exists.
A typical worflow is to tidy up and rearrange your local copy of your resources, push them to your cloud, then update your save.
Another typical operation is to first download a save resources, copy all foreign resources in your local copy and do the same.
This way, you can easily relocate all your save resources in your cloud to avoid dead links.

## API SteamWork

*UGC stands for "User-Generated Content".*

### ISteamRemoteStorage

    BeginFileWriteBatch
    EndFileWriteBatch

    FileDelete
    FileExists
    FileForget
    FilePersisted
    FileRead
    FileReadAsync
    FileReadAsyncComplete
    FileShare
    FileWrite
    FileWriteAsync
    FileWriteStreamCancel
    FileWriteStreamClose
    FileWriteStreamOpen
    FileWriteStreamWriteChunk

    GetCachedUGCCount
    GetFileCount
    GetFileNameAndSize
    GetFileSize
    GetFileTimestamp
    GetLocalFileChangeCount
    GetLocalFileChange
    GetQuota
    GetSyncPlatforms
    GetUGCDetails
    GetUGCDownloadProgress

    IsCloudEnabledForAccount
    SetCloudEnabledForApp
    SetSyncPlatforms
    SetUserPublishedFileAction
    SubscribePublishedFile

    UGCDownload
    UGCDownloadToLocation
    UGCRead
