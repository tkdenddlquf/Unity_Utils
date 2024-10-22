using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSystem : MonoBehaviour
{
    public LerpSlider percent = new();

    private static SceneNames sceneNames;

    private AsyncOperation asyncOperation;

    private readonly LerpAction lerpAction = new();

    private void Start()
    {
        percent.action = lerpAction;
        percent.callback = Callback;

        percent.speed = 0.1f;
        percent.slider.maxValue = 0.9f;

        percent.SetData(0);

        StartCoroutine(LoadSceneProcess());
    }

    private void FixedUpdate()
    {
        lerpAction.actions?.Invoke();
    }

    public static void LoadScene(SceneNames _name)
    {
        if (sceneNames == _name) return;

        SceneManager.LoadScene((int)SceneNames.Loading);
        sceneNames = _name;
    }

    private IEnumerator LoadSceneProcess()
    {
        asyncOperation = SceneManager.LoadSceneAsync((int)sceneNames);
        asyncOperation.allowSceneActivation = false;

        while (!asyncOperation.isDone)
        {
            percent.SetData(asyncOperation.progress);

            yield return null;
        }
    }

    private void Callback(float _now, float _end)
    {
        if (_now == percent.slider.maxValue) asyncOperation.allowSceneActivation = true;
    }
}