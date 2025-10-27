using Steamworks;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Network.Steam
{
    public class SteamCloud
    {
        private readonly CallResult<RemoteStorageFileReadAsyncComplete_t> readAsyncComplete = new();
        private readonly CallResult<RemoteStorageFileWriteAsyncComplete_t> writeAsyncComplete = new();
        private readonly CallResult<RemoteStorageFileShareResult_t> shareComplete = new();

        public void Dispose()
        {
            readAsyncComplete.Dispose();
            writeAsyncComplete.Dispose();
            shareComplete.Dispose();
        }

        public async Task<T> JsonRead<T>(string fileName)
        {
            byte[] data = await FileRead(fileName);

            if (data == null) return default;
            else
            {
                string json = Encoding.UTF8.GetString(data);

                return JsonUtility.FromJson<T>(json);
            }
        }

        public async Task<bool> JsonWrite<T>(string fileName, T data)
        {
            string json = JsonUtility.ToJson(data);
            byte[] convertData = Encoding.UTF8.GetBytes(json);

            return await FileWrite(fileName, convertData);
        }

        public async Task<byte[]> FileRead(string fileName)
        {
            TaskCompletionSource<byte[]> tcs = new();

            int fileSize = SteamRemoteStorage.GetFileSize(fileName);

            if (fileSize <= 0) return null;

            SteamAPICall_t call = SteamRemoteStorage.FileReadAsync(fileName, 0, (uint)fileSize);

            void Complete(RemoteStorageFileReadAsyncComplete_t result, bool failure)
            {
                if (failure || result.m_eResult != EResult.k_EResultOK) tcs.SetResult(null);
                else
                {
                    byte[] buffer = new byte[result.m_cubRead];
                    bool complete = SteamRemoteStorage.FileReadAsyncComplete(result.m_hFileReadAsync, buffer, result.m_cubRead);

                    tcs.SetResult(complete ? buffer : null);
                }
            }

            readAsyncComplete.Set(call, Complete);

            return await tcs.Task;
        }

        public async Task<bool> FileWrite(string fileName, byte[] data)
        {
            TaskCompletionSource<bool> tcs = new();

            SteamAPICall_t call = SteamRemoteStorage.FileWriteAsync(fileName, data, (uint)data.Length);

            void Complete(RemoteStorageFileWriteAsyncComplete_t result, bool failure)
            {
                if (failure || result.m_eResult != EResult.k_EResultOK) tcs.SetResult(false);
                else tcs.SetResult(true);
            }

            writeAsyncComplete.Set(call, Complete);

            if (await tcs.Task)
            {
                tcs = new();

                call = SteamRemoteStorage.FileShare(fileName);

                void Share(RemoteStorageFileShareResult_t result, bool failure)
                {
                    if (failure || result.m_eResult != EResult.k_EResultOK) tcs.SetResult(false);
                    else tcs.SetResult(true);
                }

                shareComplete.Set(call, Share);

                return await tcs.Task;
            }

            return false;
        }

        public bool FileDelete(string fileName) => SteamRemoteStorage.FileDelete(fileName);
    }
}