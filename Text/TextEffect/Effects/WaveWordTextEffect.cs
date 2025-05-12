using TMPro;
using UnityEngine;

public class WaveWordTextEffect : ITextEffect
{
    private readonly float speed = 10;
    private readonly Vector2 power = Vector2.up * 0.5f;

    public void OnEffect(ref Vector3[] vertices, TMP_CharacterInfo charInfo)
    {
        float posX = Mathf.Sin(Time.realtimeSinceStartup * speed + charInfo.vertexIndex) * power.x;
        float posY = Mathf.Cos(Time.realtimeSinceStartup * speed + charInfo.vertexIndex) * power.y;

        for (int j = 0; j < 4; j++)
        {
            if (charInfo.character == ' ') continue;

            vertices[charInfo.vertexIndex + j] += new Vector3(posX, posY, 0) * 10f;
        }
    }
}
