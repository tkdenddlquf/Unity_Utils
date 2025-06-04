using TMPro;
using UnityEngine;

public class TextEffectHandle : MonoBehaviour
{
    private TMP_Text text;

    private void Start() => TryGetComponent(out text);

    private void Update()
    {
        text.ForceMeshUpdate();

        TMP_TextInfo textInfo = text.textInfo;

        if (textInfo.linkCount == 0) return;

        Mesh mesh = text.mesh;
        Vector3[] vertices = mesh.vertices;

        for (int i = 0; i < textInfo.linkCount; i++)
        {
            TMP_LinkInfo info = textInfo.linkInfo[i];
            ITextEffect textEffect = TextEffectHub.GetEffect(info.GetLinkID());

            if (textEffect == null) continue;

            for (int j = info.linkTextfirstCharacterIndex; j < info.linkTextfirstCharacterIndex + info.linkTextLength; j++)
            {
                textEffect?.OnEffect(ref vertices, textInfo.characterInfo[j]);
            }
        }

        mesh.vertices = vertices;

        text.canvasRenderer.SetMesh(mesh);
    }
}
