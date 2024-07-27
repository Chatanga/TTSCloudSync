# TTS Cloud Sync

Synchronise a local folder with the Tabletop Simulator cloud.

Note: this tool is loosely based on [tts-cloud-manager](https://github.com/leberechtreinhold/tts-cloud-manager),
another tool which may interests you if you are on Windows and prefer a graphical user interface.

## Commands

There are actually 4 command line tools to combine to update a TTS cloud.

```bash
extract-ugc-url [--help] [SAVE] [> URL_LST]
```

Extract all the URLs for UGC (User-Generated Content) resources found in a JSON save (any kind of text file actually).
Lua scripts are parsed along with the rest, but if the mod creates UGC URLs procedurally, the tool won't find them.

```bash
download-ugc-resources [--help] [--no-sha1] [-o OUTPUT_DIR] [FILE]
```

Download the UGC URLs from a file (or the standard input) and store them in the provided output directory (or the current one).
Each resource ends up in a subdirectory named after the Steam ID of its owner.
*Important: the Steam client must be running for this command to work.*

```bash
sync-with-cloud [--help] [--pull] [--dry-run] LOCAL_PATH [TTS_STEAM_CLOUD_PATH] [> MAPPING]
```

Synchronise your Steam cloud for Tabletop Simulator with a local path, adding, removing or moving resources to match your local path content.
(When simply moved around, the shared URL of a resource remains the same.)
Implicitly, all resources will be shared on your cloud and the tool will output the updated mapping:
*Important: the Steam client must be running for this command to work.*

```txt
file_name file_sha1 ugc_handle
```

The corresponding URL is `https://steamusercontent-a.akamaihd.net/ugc/<ugc_handle>/<file_sha1>/`
(formerly `http://cloud-3.steamusercontent.com/ugc/<ugc_handle>/<file_sha1>/`).

A TTS cloud enforces uniqueness of `(uppercase(file_name), file_sha1)` pairs and the tool will notify of any duplicate in your local path.
It's important to understand that the directory structure in the TTS cloud is a pure TTS construction.
From the underlying Steam cloud perspective, there are only a flat list of files indexed by their `(file_name, file_sha1)`.
As such, you can have two different files named the same in the same location of your cloud (something you can't replicate locally however),
but you can't have the exact same file (same name and sha1) in two different directories.
Doing so remove the oldest duplicate (an operation the TTS layer is not detecting, leaving a ghost entry in its index).

In addition, when a file is uploaded into a TTS cloud, it is also shared under an arbitrary UGC URL.
Deleting then uploading the exact same file will generate a different UGC URL, leading to broken links in mod saves you have no control over.
Simply moving a file using the TTS UI won't republish it hovewer, keeping its UGC URL stable.

```bash
patch-ugc-url [--help] [-i] [--no-backup] MAPPING [SAVE]
```

Patch all UGC URLs in the provided save (or standard input) to use one from your cloud with the same SHA1 (whatever the name) if one exists.

## Workflow

Here is a typical worflow is to rehost part or all of the resources of an existing mod save:

```bash
TTS_DIR="$HOME/.local/share/Tabletop Simulator"
mkdir resources

# 1. Download all the resources of a save.
extract-ugc-url "$TTS_DIR/Mods/Workshop/1234567890.json" | download-ugc-resources --no-sha1 -o resources

# 2. Tidy up and rearrange the content of the "resources" directory.
# You can very well choose to remove some resources you want to keep outside your cloud.

# 3. Push everything into your cloud (sync-with-cloud).
sync-with-cloud resources "my_mod_name" > mapping.lst

# 4. Patch the mod to use your own resources when available.
patch-ugc-url mapping.lst "$TTS_DIR/Mods/Workshop/1234567890.json" > "$TTS_DIR/Saves/TS_Save_100.json"
```

This way, you can easily relocate all the resources into your cloud to avoid any dead links in the future.

The previous example run on Linux, but the Windows version is almost the same:

```batch
set TTS_DIR=%UserProfile%\Documents\My Games\Tabletop Simulator
md resources

rem 1. Download all the resources of a save.
extract-ugc-url "%TTS_DIR%\Mods\Workshop\1234567890.json" | download-ugc-resources --no-sha1 -o resources

rem 2. Tidy up and rearrange the content of the "resources" directory.
rem You can very well choose to remove some resources you want to keep outside your cloud.

rem 3. Push everything into your cloud (sync-with-cloud).
sync-with-cloud resources "my_mod_name" > mapping.lst

rem 4. Patch the mod to use your own resources when available.
patch-ugc-url mapping.lst "%TTS_DIR%\Mods\Workshop\1234567890.json" > "%TTS_DIR%\Saves\TS_Save_100.json"
```

## How to build & install

Once you've installed the .NET 7.0 framework:

```bash
dotnet publish --os linux
dotnet publish --os win
```

Each call create a corresponding ZIP file in `dist`.
Simply unzip the file matching your OS somewhere and run the scripts from there (or add the created directory in you `PATH`).

Alternatively, you can download the latest [release from GitHub](https://github.com/Chatanga/TTSCloudSync/releases).

## API SteamWork

*UGC stands for "User-Generated Content".*

### ISteamRemoteStorage

```txt
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
```
