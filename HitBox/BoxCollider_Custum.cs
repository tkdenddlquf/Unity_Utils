using UnityEngine;
using System.Collections.Generic;
using System;

public class BoxCollider_Custum : MonoBehaviour
{
    public List<BoxCollider_CustumData> data = new();
    public Action<BoxCollider_CustomInfo> callback;

    private int length;
    private readonly Collider[] colliders = new Collider[5];

    public void OnTriggerEnter_Callback(int _index, LayerMask _mask) // 충돌 콜백
    {
        length = Physics.OverlapBoxNonAlloc(transform.position + transform.TransformDirection(data[_index].pos), data[_index].scale, colliders, Quaternion.identity, _mask);

        for (int i = 0; i < length; i++)
        {
            if (data[_index].callback != null) data[_index].callback(new(colliders[i].gameObject, _index, data[_index].maxCount));
            else callback(new(colliders[i].gameObject, _index, data[_index].maxCount));
        }
    }

    public bool OnTriggerEnter_Check(int _index, LayerMask _mask) // 충돌 확인
    {
        return Physics.CheckBox(transform.position + transform.TransformDirection(data[_index].pos), data[_index].scale, Quaternion.identity, _mask);
    }

#if UNITY_EDITOR
    private int select = 0;

    public int Select
    {
        get
        {
            if (select < 0) select = 0;
            if (select > data.Count - 1) select = data.Count - 1;

            return select;
        }
        set
        {
            if (value < 0) select = 0;
            if (value > data.Count - 1) select = data.Count - 1;

            select = value;
        }
    }

    public Vector3 this[GUIMode _state]
    {
        get
        {
            return _state switch
            {
                GUIMode.Pos => data[Select].pos,
                GUIMode.Scale => data[Select].scale,
                _ => Vector3.zero
            };
        }
        set
        {
            switch (_state)
            {
                case GUIMode.Pos:
                    data[Select].pos = value;
                    break;

                case GUIMode.Scale:
                    data[Select].scale = value;
                    break;
            }
        }
    }
#endif
}

[Serializable]
public class BoxCollider_CustumData
{
    public int maxCount;

    public Vector3 pos;
    public Vector3 scale;

    public Action<BoxCollider_CustomInfo> callback;

    public BoxCollider_CustumData()
    {
        maxCount = 1;

        pos = new(0, 0, 0);
        scale = new(1, 1, 1);
    }
}

public struct BoxCollider_CustomInfo
{
    public GameObject target;

    public int index;
    public int maxCount;

    public BoxCollider_CustomInfo(GameObject _target, int _index, int _maxCount)
    {
        target = _target;
        index = _index;
        maxCount = _maxCount;
    }
}