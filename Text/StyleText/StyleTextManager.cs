using TMPro;
using UnityEngine;

public class StyleTextManager : MonoBehaviour
{
    public float size = 5;
    public TMP_Text text;

    public StyleTextType type;

    private float nowValue;
    private float lerpValue;

    private TMP_TextInfo textInfo;

    private TMP_MeshInfo meshInfo;
    private TMP_CharacterInfo charInfo;

    private Vector3[] varts;

    public void Start()
    {
        IncreaseText(type);
    }

    public void IncreaseText(StyleTextType type)
    {
        text.ForceMeshUpdate();

        textInfo = text.textInfo;

        nowValue = 0;
        lerpValue = size / textInfo.characterCount;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible)
            {
                nowValue += lerpValue * 2;

                continue;
            }

            varts = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;

            switch (type)
            {
                case StyleTextType.Up:
                    varts[charInfo.vertexIndex + 1] += nowValue * Vector3.up;

                    nowValue += lerpValue;

                    varts[charInfo.vertexIndex + 2] += nowValue * Vector3.up;

                    nowValue += lerpValue;
                    break;

                case StyleTextType.Both:
                    varts[charInfo.vertexIndex] += nowValue * Vector3.down;
                    varts[charInfo.vertexIndex + 1] += nowValue * Vector3.up;

                    nowValue += lerpValue;

                    varts[charInfo.vertexIndex + 2] += nowValue * Vector3.up;
                    varts[charInfo.vertexIndex + 3] += nowValue * Vector3.down;

                    nowValue += lerpValue;
                    break;

                case StyleTextType.Down:
                    varts[charInfo.vertexIndex] += nowValue * Vector3.down;

                    nowValue += lerpValue;

                    varts[charInfo.vertexIndex + 3] += nowValue * Vector3.down;

                    nowValue += lerpValue;
                    break;
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;

            text.UpdateGeometry(meshInfo.mesh, i);
        }
    }
}
