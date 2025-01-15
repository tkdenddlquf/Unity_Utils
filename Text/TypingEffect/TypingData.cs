using System;

public struct TypingData
{
    public SpeechBubbleInfo info;

    public string text;
    public float sleepTime;

    public Action startAction;
    public Action endAction;
    public CharAction charAction;

    public delegate void CharAction(int index, ref string text, ref WaitForSeconds sleepTime);
}