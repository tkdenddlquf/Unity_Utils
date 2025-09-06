using Spine.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class SpineAnimationEvent
{
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField, SpineAnimation(dataField: "skeletonAnimation")] private string animationName;

    [SerializeField] private SpineEventData[] events;

    private Spine.Animation playAnimation;

    private readonly Dictionary<string, UnityEvent> eventHandles = new();

    public string Name => animationName;

    public void Init()
    {
        playAnimation = skeletonAnimation.skeleton.Data.FindAnimation(Name);

        for (int i = 0; i < events.Length; i++)
        {
            if (events[i].eventName == "") continue;

            eventHandles.Add(events[i].eventName, events[i].unityEvent);
        }

        skeletonAnimation.AnimationState.Event -= AnimationEvent;
        skeletonAnimation.AnimationState.Event += AnimationEvent;
    }

    public float GetAnimationTime() => skeletonAnimation.AnimationState.GetCurrent(0).AnimationTime;

    public float GetAnimationEndTime() => skeletonAnimation.AnimationState.GetCurrent(0).AnimationEnd;

    public float GetEventTime(string eventName)
    {
        foreach (var timeline in skeletonAnimation.AnimationState.GetCurrent(0).Animation.Timelines)
        {
            if (timeline is Spine.EventTimeline)
            {
                Spine.Event[] spineEvents = (timeline as Spine.EventTimeline).Events;

                foreach (var spineEvent in spineEvents)
                {
                    if (spineEvent.Data.Name == eventName) return spineEvent.Time;
                }

                break;
            }
        }

        return -1f;
    }

    public Spine.Bone FindBone(string boneName) => skeletonAnimation.skeleton.FindBone(boneName);

    public void SetAnimation(float timeScale, bool loop = false) => SetAnimation(null, timeScale, loop);

    public void SetAnimation(Spine.AnimationState.TrackEntryDelegate callback, float timeScale, bool loop)
    {
        if (playAnimation == null)
        {
            callback?.Invoke(null);

            return;
        }

        if (callback == null) skeletonAnimation.AnimationState.SetAnimation(0, playAnimation, loop);
        else skeletonAnimation.AnimationState.SetAnimation(0, playAnimation, loop).Complete += callback;

        skeletonAnimation.timeScale = timeScale;
    }

    private void AnimationEvent(Spine.TrackEntry trackEntry, Spine.Event e)
    {
        if (eventHandles.ContainsKey(e.Data.Name)) eventHandles[e.Data.Name].Invoke();
    }
}
