using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceConvertHandle
{
    public bool IsComplete => count == 0;

    private int count = 0;

    public IEnumerator LoadSpriteResource<T>(T[] datas, Action<Dictionary<string, T>> callback) where T : IResourceId, IResourceSprite
    {
        count++;

        for (int i = 0; i < datas.Length; i++)
        {
            if (datas[i].SpritePath == "") continue;
            if (!datas[i].SpritePath.Contains("/")) continue;

            AsyncOperationHandle<Sprite[]> handle = Addressables.LoadAssetAsync<Sprite[]>(datas[i].SpritePath);

            yield return new WaitUntil(() => handle.IsDone);

            List<Sprite> sprites = new(handle.Result);
            sprites.Sort((x, y) => int.Parse(x.name.Split('_')[^1]).CompareTo(int.Parse(y.name.Split('_')[^1])));

            datas[i].Sprite = sprites[datas[i].SpriteIndex];
        }

        callback?.Invoke(DictionaryBuilder.Create(datas));

        count--;
    }
}
