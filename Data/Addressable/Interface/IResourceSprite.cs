using UnityEngine;

public interface IResourceSprite
{
    public Sprite Sprite { get; set; }
    public string SpritePath { get; set; }
    public int SpriteIndex { get; set; }
}
