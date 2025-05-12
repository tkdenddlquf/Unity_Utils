using TMPro;
using UnityEngine;

public class ShakeTextEffect : ITextEffect
{
    private readonly float power = 10f;

    public void OnEffect(ref Vector3[] vertices, TMP_CharacterInfo charInfo)
    {
        Vector3 offset = new Vector2(Random.Range(-power, power), Random.Range(-power, power));

        for (int j = 0; j < 4; j++)
        {
            if (charInfo.character == ' ') continue;

            Vector3 vertPos = vertices[charInfo.vertexIndex + j];

            vertices[charInfo.vertexIndex + j] = Vector3.Lerp(vertPos, vertPos + offset, Time.deltaTime * 20f);
        }
    }
}
