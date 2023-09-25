# TTS Cloud Sync

Synchronise a local folder with ther Tabletop Simulator cloud.

## API SteamWork

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
