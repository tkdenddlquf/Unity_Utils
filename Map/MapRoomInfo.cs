using UnityEngine;
using UnityEngine.UI;

public class MapRoomInfo : MonoBehaviour
{
    public Image image;

    public MapInfo Info { get; private set; }
    public MapCreator Creator { get; private set; }

    public void Init(MapCreator _creator, MapInfo _info, Vector2 _dir)
    {
        if (image == null) TryGetComponent(out image);

        Creator = _creator;
        Info = _info;

        if (_dir == Vector2.zero) transform.localPosition = Vector2.zero;
        else
        {
            transform.localPosition = Creator.mapDict[Info.thisPos + _dir].transform.localPosition;
            transform.localPosition -= (Vector3)_dir * Creator.roomSpecing;
        }

        switch (Info.type)
        {
            case RoomType.None:
                image.color = Color.white;
                break;

            case RoomType.Main:
                image.color = Color.gray;
                break;

            case RoomType.Start:
                image.color = Color.blue;
                break;

            case RoomType.Boss:
                image.color = Color.red;
                break;
        }
    }

    public void ConnectLine(int _num)
    {
        if (_num == -1) return;

        Info.connectDir[_num] = true;
    }
}
