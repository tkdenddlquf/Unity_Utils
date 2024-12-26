using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceDownloadHandle
{
    public bool IsComplete => count == 0;

    public long TotalSize { get; private set; }

    private int count;

    private readonly List<string> names = new();
    private readonly List<AsyncOperationHandle> handles = new();

    public void CheckDownloadSize(params string[] names)
    {
        TotalSize = 0;
        count = names.Length;
        this.names.Clear();

        for (int i = 0; i < count; i++) CheckDownloadSize(names[i]);
    }

    private async void CheckDownloadSize(string name)
    {
        AsyncOperationHandle<long> handle = Addressables.GetDownloadSizeAsync(name);

        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            if (handle.Result != 0)
            {
                TotalSize += handle.Result;
                names.Add(name);
            }

            Addressables.Release(handle);
        }

        count--;
    }

    public void Download()
    {
        count = names.Count;
        handles.Clear();

        for (int i = 0; i < count; i++) Download(names[i]);
    }

    private async void Download(string name)
    {
        AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(name, true);

        handles.Add(handle);

        await handle.Task;

        count--;
    }

    public float GetDownloadPercent()
    {
        float percent = 0;

        for (int i = 0; i < handles.Count; i++)
        {
            if (handles[i].IsValid()) percent += handles[i].GetDownloadStatus().Percent;
            else percent += 1;
        }

        return percent / handles.Count;
    }
}
