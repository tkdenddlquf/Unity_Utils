using Spine.Unity;
using System;

[Serializable]
public struct SpineBoneData
{
    [SpineBone(dataField: "skeletonAnimation")] public string boneName;

    public Spine.Bone bone;
    public EffectManager effectManager;
}
