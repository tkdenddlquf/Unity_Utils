using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TypingSystem
{
    private bool skip = false;

    private readonly List<SpeechBubbleInfo> typingList = new();

    public IEnumerator Typing(TypingData _action)
    {
        if (typingList.Contains(_action.info))
        {
            skip = true;

            yield break;
        }
        else typingList.Add(_action.info);

        _action.startAction?.Invoke();

        if (!_action.text.Equals(""))
        {
            WaitForSeconds sleepTime = new(_action.sleepTime);

            _action.info.SetActive(true);
            _action.info.textMesh.text = null;

            for (int i = 0; i < _action.text.Length; i++)
            {
                if (skip) break;
                else yield return sleepTime;

                _action.charAction?.Invoke(i, ref _action.text, ref sleepTime);

                _action.info.textMesh.text += _action.text[i];
            }

            if (skip)
            {
                _action.info.textMesh.text = _action.text;
                skip = false;
            }
        }

        typingList.Remove(_action.info);

        _action.endAction?.Invoke();
    }
}

public struct TypingData
{
    public SpeechBubbleInfo info;

    public string text;
    public float sleepTime;

    public Action startAction;
    public Action endAction;
    public CharAction charAction;

    public delegate void CharAction(int _index, ref string _text, ref WaitForSeconds _sleepTime);
}

public enum TypingActionType
{
    ChangeSleepTime,
    TypingEmoji
}
