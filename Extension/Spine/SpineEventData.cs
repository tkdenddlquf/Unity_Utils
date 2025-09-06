using Spine.Unity;
using System;
using UnityEngine.Events;

[Serializable]
public struct SpineEventData
{
    [SpineEvent(dataField: "skeletonAnimation")] public string eventName;

    public UnityEvent unityEvent;
}
