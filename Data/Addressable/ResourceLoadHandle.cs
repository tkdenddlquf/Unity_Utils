using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceLoadHandle
{
    public bool IsComplete => loadCount == 0;

    private int loadCount;

    public async void LoadAsset<T>(string path, System.Action<T> action)
    {
        loadCount++;

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(path);

        await handle.Task;

        if (handle.IsDone) action?.Invoke(handle.Result);

        loadCount--;
    }
}
