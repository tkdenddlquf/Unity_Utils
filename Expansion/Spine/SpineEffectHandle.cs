using Spine.Unity;
using UnityEngine;

public class SpineEffectHandle : MonoBehaviour
{
    [SerializeField] private Transform view;
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    [SerializeField] private SpineBoneData[] bones;

    private void Awake()
    {
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].boneName == "") continue;

            bones[i].bone = skeletonAnimation.skeleton.FindBone(bones[i].boneName);
        }
    }

    public SpineBoneData GetBoneInfo(int index) => bones[index];

    /// <summary>
    /// 특정 Bone 위치에 Effect 생성
    /// </summary>
    /// <param name="index">bones 배열 index</param>
    public void SpawnEffect(int index)
    {
        if (bones[index].effectManager == null) return;

        EffectSystem effect = bones[index].effectManager.EffectPool.Get();

        effect.transform.SetParent(transform);
        effect.transform.position = bones[index].bone.GetWorldPosition(view);
    }

    /// <summary>
    /// 모든 Bone의 EffectManager에서 미리 Effect 생성
    /// </summary>
    public void CreateDefaultEffect()
    {
        foreach (SpineBoneData bone in bones)
        {
            if (bone.effectManager == null) continue;

            bone.effectManager.CreateDefault();
        }
    }
}
