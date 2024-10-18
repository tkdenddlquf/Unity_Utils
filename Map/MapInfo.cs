using UnityEngine;

[System.Serializable]
public class MapInfo
{
    public int mainDepth;
    public int thisDepth;

    public Vector2 thisPos;

    public bool[] connectDir = new bool[4];

    public RoomType type;
}

public enum RoomType
{
    None,
    Main,
    Start,
    Key,
    MidBoss,
    Boss
}
