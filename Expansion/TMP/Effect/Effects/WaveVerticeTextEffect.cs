using TMPro;
using UnityEngine;

public class WaveVerticeTextEffect : ITextEffect
{
    private readonly float speed = 10;
    private readonly Vector2 power = Vector2.up * 0.5f;

    public void OnEffect(ref Vector3[] vertices, TMP_CharacterInfo charInfo)
    {
        for (int j = 0; j < 4; j++)
        {
            if (charInfo.character == ' ') continue;

            int vertexIndex = charInfo.vertexIndex + j;

            float posX = Mathf.Sin(Time.realtimeSinceStartup * speed + vertexIndex) * power.x;
            float posY = Mathf.Cos(Time.realtimeSinceStartup * speed + vertexIndex) * power.y;

            vertices[vertexIndex] += new Vector3(posX, posY, 0) * 10f;
        }
    }
}
