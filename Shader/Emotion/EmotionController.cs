using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yang.Dialogue;

public class EmotionController : MonoBehaviour
{
    [SerializeField] private Image image;

    [SerializeField] private Color32 defaultColor;

    [SerializeField, Range(0, 100)] private int width;
    [SerializeField, Range(0, 100)] private int height;

    [Header("Sound")]
    [SerializeField] private EventReference changeSound;
    [SerializeField] private EventReference successSound;

    [SerializeField, FMODParameters("changeSound")] private string changeParameter;

    private InputAction widthAction;
    private InputAction heightAction;

    private DialogueRunner runner;

    private float phase;

    private EventInstance soundInstance;

    public EmotionMarker Data { get; set; }

    public void Init()
    {
        Material material = new(image.material);

        image.material = material;

        runner = FindAnyObjectByType<DialogueRunner>();

        widthAction = new InputAction("Width", InputActionType.Value);
        widthAction.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/d").With("Negative", "<Keyboard>/a");

        heightAction = new InputAction("Height", InputActionType.Value);
        heightAction.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/w").With("Negative", "<Keyboard>/s");
    }

    public void StartEmotion()
    {
        soundInstance = RuntimeManager.CreateInstance(changeSound);
        soundInstance.setParameterByName(changeParameter, 0);
        soundInstance.start();

        StopAllCoroutines();
        StartCoroutine(Emotion());
    }

    private IEnumerator Emotion()
    {
        float delay = 0;

        float time = Data.time;

        int targetWidth = Data.width;
        int targetHeight = Data.height;

        widthAction.Enable();
        heightAction.Enable();

        while (true)
        {
            phase += Time.deltaTime * 25;

            if (widthAction.IsPressed())
            {
                int value = width + (int)widthAction.ReadValue<float>();

                if (value < 0) width = 0;
                else if (value > 100) width = 100;
                else width = value;
            }

            if (heightAction.IsPressed())
            {
                int value = height + (int)heightAction.ReadValue<float>();

                if (value < 0) height = 0;
                else if (value > 100) height = 100;
                else height = value;
            }

            float widthDiff = Mathf.Abs(width - targetWidth);
            float heightDiff = Mathf.Abs(height - targetHeight);

            float t = 1 - Mathf.Clamp01((widthDiff + heightDiff) / 30f);

            soundInstance.setParameterByName(changeParameter, t);

            if (t > 0.8f)
            {
                delay += Time.deltaTime;

                if (delay >= 0.5f) break;
            }
            else delay = 0;

            if (time != -1)
            {
                time -= Time.deltaTime;

                if (time <= 0)
                {
                    time = 0;

                    break;
                }
            }

            Material material = image.material;

            material.SetInt("_Width", width);
            material.SetInt("_Height", height);
            material.SetFloat("_Phase", phase);
            material.SetColor("_Color", Color32.Lerp(defaultColor, Data.color, t));

            yield return null;
        }

        widthAction.Disable();
        heightAction.Disable();

        soundInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        soundInstance.release();

        if (time != 0)
        {
            runner.SetValue(Data.trigger, true);

            Data.successCallback?.Invoke();

            successSound.PlayOneShot();
        }

        PopupManager.Instance.InactivePopup(PopupType.Emotion);
    }
}