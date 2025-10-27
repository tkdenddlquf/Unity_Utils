using Spine.Unity;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class SpineTester : MonoBehaviour
{
    [Header("Test")]
    [SerializeField] private bool playAnim;

    [Header("Spine Settings")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField, SpineAnimation(dataField: "skeletonAnimation")] private string animationName;

    [Header("Animation Time Settings")]
    [SerializeField, Range(0, 1)] private float currentTime = 0.0f;
    [SerializeField, SpineEvent(dataField: "skeletonAnimation")] private string eventName;

    [Header("Effect Position Settings")]
    [SerializeField] private Transform view;
    [SerializeField] private GameObject effect;
    [SerializeField, SpineBone(dataField: "skeletonAnimation")] private string boneName;

    private float duration;

    private string prevBoneName;
    private string prevAnimationName;

    private Spine.TrackEntry anim;
    private Spine.Bone bone;

    private double oldTime;

    private void OnValidate()
    {
        // 에디터에서 업데이트를 사용하기 위해 설정
        if (playAnim)
        {
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;

            oldTime = EditorApplication.timeSinceStartup;
        }
        else EditorApplication.update -= EditorUpdate;

        if (skeletonAnimation == null) return;

        CheckData();

        if (anim != null)
        {
            // 특정 이벤트가 발생하는 지점으로 이동
            if (!string.IsNullOrEmpty(eventName))
            {
                float eventTime = GetTime();

                if (eventTime != -1) currentTime = eventTime / duration;

                eventName = string.Empty;
            }

            if (skeletonAnimation.state == null) return;

            float time = currentTime * duration * 0.5f;

            anim.TrackTime = time;

            skeletonAnimation.Update(time);
        }

        if (effect != null && view != null) effect.transform.position = bone == null ? view.transform.position : bone.GetWorldPosition(view);
    }

    private void EditorUpdate()
    {
        if (EditorApplication.isPlaying) return;

        if (!playAnim)
        {
            EditorApplication.update -= EditorUpdate;

            return;
        }

        CheckData();

        if (anim != null)
        {
            float time = currentTime * duration * 0.5f;

            if (anim.TrackTime % 1 < time) anim.TrackTime = time;

            float deltaTime = (float)(EditorApplication.timeSinceStartup - oldTime);

            skeletonAnimation.Update(deltaTime);
            oldTime = EditorApplication.timeSinceStartup;

            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    /// <summary>
    /// 비어있는 데이터 확인 및 적용
    /// </summary>
    private void CheckData()
    {
        if (anim == null && !string.IsNullOrEmpty(animationName))
        {
            anim = skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
            duration = anim.Animation.Duration;
        }

        if (animationName != prevAnimationName)
        {
            anim = null;

            currentTime = 0.0f;

            prevAnimationName = animationName;
        }

        if (boneName != prevBoneName)
        {
            if (!string.IsNullOrEmpty(boneName)) bone = skeletonAnimation.skeleton.FindBone(boneName);
            else bone = null;

            prevBoneName = boneName;
        }
    }

    /// <summary>
    /// 특정 이벤트 발생 시점을 반환하는 코드
    /// </summary>
    /// <returns>이벤트 발생 지점</returns>
    private float GetTime()
    {
        foreach (var timeline in anim.Animation.Timelines)
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
}