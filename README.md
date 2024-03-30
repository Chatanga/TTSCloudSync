# TTS Cloud Sync

Synchronise a local folder with the Tabletop Simulator cloud.

## Commands

Extract all the URLs for UGC (User-Generated Content) resources found in a JSON save.

```bash
extract-ugc-url [save]
```

Download the UGC URLs from a file (or the standard input) and store then in the provided output directory (or the current one).
Each resource ends up in a subdirectory named after the Steam ID of its owner.

```bash
download-ugc-resources [-o output_dir] [file]
```

Synchronise your Steam cloud for Tabletop Simulator with a local path, adding, removing or moving resource to match your local path content.
Implicitly, all resources will be shared on your cloud and the tool will output the updated mapping:

    file_name file_sha1 ugc_handle

The corresponding URL is `http://cloud-3.steamusercontent.com/ugc/<ugc_handle>/<file_sha1>/`.
A Steam cloud enforces uniqueness of (file_name, file_sha1) pairs and the tool will notify of any duplicate in your local path.
By the way, a Steam cloud allows two different files (checksum wise) with the same name in the same folder (which is somewhat a pure TTS construction),
but it cannot be exploited in a local directory.
Unless using the `--force` option, it won't push a local file with the same name and sha1 as another cloud resource ouside the provided `tts_steam_cloud_path`.

```bash
upload-to-cloud [--force] local_path [tts_steam_cloud_path] > mapping.lst
```

Patch all UGC URL in the provided save (or standard input) to use one from your cloud with the same SHA1 if one exists.
A typical worflow is to tidy up and rearrange your local copy of your resources, push them to your cloud, then update your save.
Another typical operation is to first download a save resources, copy all foreign resources in your local copy and do the same.
This way, you can easily relocate all your save resources in your cloud to avoid dead links.

```bash
update-save mapping.lst [save]
```

Find all the resources from you cloud (mapping file) not used in any of the provided saves.

```bash
find-orphans mapping.lst [save...]
```

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
