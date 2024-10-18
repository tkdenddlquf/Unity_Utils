using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapCreator : MonoBehaviour
{
    public float roomSpecing;
    public float lineWidth;

    public int bossRoomDist;

    public int maxTreeDepth;
    public int maxTreeExtent;

    [Range(0, 100)] public int depthPercent = 30;
    [Range(0, 100)] public int extentPercent = 10;
    [Range(0, 100)] public int connectPercent = 50;

    public MapRoomInfo roomInfo;
    public Transform roomParent;
    public ObjectPool<MapRoomInfo> roomInfoPool;

    public Image roomLine;
    public Transform roomLineParent;
    public ObjectPool<Image> roomLinePool;

    public readonly Dictionary<Vector2, MapRoomInfo> mapDict = new();

    private readonly List<Image> lines = new();
    private readonly List<MapRoomInfo> mapRoomInfo = new();

    private void Start()
    {
        roomInfoPool = new(roomInfo, roomParent)
        {
            DeAction = (MapRoomInfo _info) => { _info.gameObject.SetActive(true); },
            EnAction = (MapRoomInfo _info) =>
            {
                _info.gameObject.SetActive(false);

                _info.Info.connectDir[0] = false;
                _info.Info.connectDir[1] = false;
                _info.Info.connectDir[2] = false;
                _info.Info.connectDir[3] = false;

                mapRoomInfo.Remove(_info);
            }
        };

        roomLinePool = new(roomLine, roomLineParent)
        {
            DeAction = (Image _image) => { _image.gameObject.SetActive(true); },
            EnAction = (Image _image) => { _image.gameObject.SetActive(false); }
        };
    }

    public void CreateMap()
    {
        while (mapRoomInfo.Count != 0) roomInfoPool.Enqueue(mapRoomInfo[0]);

        ClearLine();
        mapDict.Clear();

        CreateRoom(RoomType.Start, 0, 0, Vector2.zero, Vector2.zero);

        for (int i = 1; i < bossRoomDist; i++) CreateRoom(RoomType.Main, i, 0, mapRoomInfo[^1].Info.thisPos, GetRandDir(mapRoomInfo[^1].Info.thisPos));

        CreateRoom(RoomType.Boss, bossRoomDist, 0, mapRoomInfo[^1].Info.thisPos, GetRandDir(mapRoomInfo[^1].Info.thisPos));

        if (mapRoomInfo.Count != bossRoomDist + 1)
        {
            CreateMap();

            return;
        }

        for (int i = 1; i < bossRoomDist; i++) AddTreeExtent(i, 1, mapRoomInfo[i].Info.thisPos);

        for (int i = bossRoomDist + 2; i < mapRoomInfo.Count; i++) ConnectNeerRoom(mapRoomInfo[i].Info.thisPos);

        for (int i = 0; i < mapRoomInfo.Count; i++) CreateLine(mapRoomInfo[i]);
    }

    private bool CheckBetween(RoomType _type, int _thisIndex, int _nextIndex)
    {
        _nextIndex++;

        for (int i = _thisIndex; i < _nextIndex; i++)
        {
            if (mapRoomInfo[i].Info.type == _type) return false;
        }

        return true;
    }

    private void AddTreeExtent(int _mainDepth, int _nowTreeDepth, Vector2 _pos)
    {
        for (int i = 0; i < maxTreeExtent; i++)
        {
            if (RandBool(extentPercent))
            {
                if (CreateRoom(RoomType.None, _mainDepth, _nowTreeDepth, _pos, GetRandDir(_pos))) AddTreeDepth(_mainDepth, _nowTreeDepth + 1, mapRoomInfo[^1].Info.thisPos);
            }
        }
    }

    private void AddTreeDepth(int _mainDepth, int _nowTreeDepth, Vector2 _pos)
    {
        if (_nowTreeDepth > maxTreeDepth) return;

        if (RandBool(depthPercent))
        {
            if (CreateRoom(RoomType.None, _mainDepth, _nowTreeDepth, _pos, GetRandDir(_pos))) AddTreeExtent(_mainDepth, _nowTreeDepth, _pos);
        }
    }

    private bool CreateRoom(RoomType _type, int _mainDepth, int _nowTreeDepth, Vector2 _prevPos, Vector2 _dir)
    {
        if (_type != RoomType.Start && _dir == Vector2.zero) return false;

        mapRoomInfo.Add(roomInfoPool.Dequeue());

        mapRoomInfo[^1].Init(this, new() { type = _type, mainDepth = _mainDepth, thisDepth = _mainDepth + _nowTreeDepth, thisPos = _prevPos + _dir }, GetReverseDir(_dir));

        mapDict.Add(mapRoomInfo[^1].Info.thisPos, mapRoomInfo[^1]);

        mapDict[_prevPos].ConnectLine(GetDirToNum(_dir));
        mapRoomInfo[^1].ConnectLine(GetDirToNum(GetReverseDir(_dir)));

        return true;
    }

    private Vector2 GetRandDir(Vector2 _pos)
    {
        List<Vector2> _returnPosList = new() { Vector2.left, Vector2.up, Vector2.right, Vector2.down };

        for (int i = 3; i >= 0; i--)
        {
            if (mapDict.ContainsKey(_pos + _returnPosList[i])) _returnPosList.RemoveAt(i);
        }

        if (_returnPosList.Count == 0) return Vector2.zero;
        else return _returnPosList[Random.Range(0, _returnPosList.Count)];
    }

    private Vector2 GetReverseDir(Vector2 _dir)
    {
        if (_dir == Vector2.left) return Vector2.right;
        else if (_dir == Vector2.up) return Vector2.down;
        else if (_dir == Vector2.right) return Vector2.left;
        else if (_dir == Vector2.down) return Vector2.up;

        return Vector2.zero;
    }

    private int GetReverseDirNum(int _num)
    {
        return _num switch
        {
            0 => 2,
            1 => 3,
            2 => 0,
            3 => 1,
            _ => -1
        };
    }

    private Vector2 GetNumToDir(int _num)
    {
        return _num switch
        {
            0 => Vector2.left,
            1 => Vector2.up,
            2 => Vector2.right,
            3 => Vector2.down,
            _ => Vector2.zero
        };
    }

    private int GetDirToNum(Vector2 _dir)
    {
        if (_dir == Vector2.left) return 0;
        else if (_dir == Vector2.up) return 1;
        else if (_dir == Vector2.right) return 2;
        else if (_dir == Vector2.down) return 3;

        return -1;
    }

    private void ConnectNeerRoom(Vector2 _pos)
    {
        Vector2 _nextPos;

        for (int i = 0; i < 4; i++)
        {
            if (mapDict[_pos].Info.connectDir[i]) continue;

            _nextPos = _pos + GetNumToDir(i);

            if (mapDict.ContainsKey(_nextPos))
            {
                if (RandBool(connectPercent))
                {
                    if (mapDict[_nextPos].Info.type == RoomType.Start) continue;
                    if (mapDict[_nextPos].Info.type == RoomType.Boss) continue;

                    if (Mathf.Abs(mapDict[_pos].Info.thisDepth - mapDict[_nextPos].Info.thisDepth) > 1) continue;

                    if (CheckBetween(RoomType.MidBoss, mapDict[_pos].Info.mainDepth, mapDict[_nextPos].Info.mainDepth))
                    {
                        mapDict[_pos].ConnectLine(i);
                        mapDict[_nextPos].ConnectLine(GetReverseDirNum(i));
                    }
                }
            }
        }
    }

    private void CreateLine(MapRoomInfo _info)
    {
        Vector2 _dir;

        for (int i = 0; i < 4; i++)
        {
            if (!_info.Info.connectDir[i]) continue;

            _dir = GetNumToDir(i);

            if (mapDict[_info.Info.thisPos + _dir].Info.thisDepth >= _info.Info.thisDepth) continue;

            lines.Add(roomLinePool.Dequeue());
            lines[^1].rectTransform.localPosition = _info.transform.localPosition;
            lines[^1].rectTransform.sizeDelta = new Vector2(roomSpecing, lineWidth);

            if (_dir == Vector2.left) lines[^1].rectTransform.localEulerAngles = new Vector3(0, 0, 180);
            else if (_dir == Vector2.up) lines[^1].rectTransform.localEulerAngles = new Vector3(0, 0, 90);
            else if (_dir == Vector2.right) lines[^1].rectTransform.localEulerAngles = new Vector3(0, 0, 0);
            else if (_dir == Vector2.down) lines[^1].rectTransform.localEulerAngles = new Vector3(0, 0, 270);
        }
    }

    private void ClearLine()
    {
        while (lines.Count != 0)
        {
            roomLinePool.Enqueue(lines[0]);
            lines.RemoveAt(0);
        }
    }

    private bool RandBool(int _num = 50)
    {
        return Random.Range(0, 100) < _num;
    }
}

public enum MapDirection
{
    Up,
    Left,
    Down,
    Right
}