using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TypingSystem
{
    private bool skip = false;

    private readonly List<SpeechBubbleInfo> typingList = new();

    public IEnumerator Typing(TypingData action)
    {
        if (typingList.Contains(action.info))
        {
            skip = true;

            yield break;
        }
        else typingList.Add(action.info);

        action.startAction?.Invoke();

        if (!action.text.Equals(""))
        {
            WaitForSeconds sleepTime = new(action.sleepTime);

            action.info.SetActive(true);
            action.info.textMesh.text = null;

            for (int i = 0; i < action.text.Length; i++)
            {
                if (skip) break;
                else yield return sleepTime;

                action.charAction?.Invoke(i, ref action.text, ref sleepTime);

                action.info.textMesh.text += action.text[i];
            }

            if (skip)
            {
                action.info.textMesh.text = action.text;
                skip = false;
            }
        }

        typingList.Remove(action.info);

        action.endAction?.Invoke();
    }
}