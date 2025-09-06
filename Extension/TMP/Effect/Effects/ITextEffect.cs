using TMPro;
using UnityEngine;

public interface ITextEffect
{
    public void OnEffect(ref Vector3[] vertices, TMP_CharacterInfo charInfo);
}
