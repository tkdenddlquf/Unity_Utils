using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EllipseCollider : MonoBehaviour
{
    public List<Vector2> ranges = new();

    private Vector2 areaPos;
    private Vector2 areaSize;

    public void SetArea(Vector2 _areaPos, Vector2 _areaSize)
    {
        areaPos = _areaPos;
        areaSize = _areaSize;
    }

    public Vector3 AroundTarget(Vector3 _pos, EllipseCollider _target, float _speed)
    {
        if (transform.position == _target.transform.position) return TransAreaPos(Random.Range(0, _speed) * Vector3.up);

        Vector3 _rightTurn = TransAreaPos(Quaternion.Euler(0, 0, -90) * _pos);
        Vector3 _leftTurn = TransAreaPos(Quaternion.Euler(0, 0, 90) * _pos);

        if (OnEllipseEnter(transform.position + _rightTurn, _target, EllipseType.Unit, EllipseType.Unit) > 1) return _rightTurn;
        if (OnEllipseEnter(transform.position + _leftTurn, _target, EllipseType.Unit, EllipseType.Unit) > 1) return _leftTurn;

        return TransAreaPos(_pos + _speed * (transform.position - _target.transform.position).normalized);
    }

    public float OnEllipseEnter(Vector3 _pos, EllipseCollider _target, EllipseType _num, EllipseType _targetNum)
    {
        _pos = _target.transform.position - _pos;

        return Mathf.Pow(_pos.x / (ranges[(int)_num].x + _target.ranges[(int)_targetNum].x), 2) + Mathf.Pow(_pos.y / (ranges[(int)_num].y + _target.ranges[(int)_targetNum].y), 2);
    }

    public Vector2 TransAreaPos(Vector3 _pos)
    {
        float _dist = (areaPos.x - areaSize.x * 0.5f) - (transform.position.x + _pos.x - ranges[0].x);

        if (_dist > 0) _pos.x += _dist;

        _dist = (areaPos.x + areaSize.x * 0.5f) - (transform.position.x + _pos.x + ranges[0].x);

        if (_dist < 0) _pos.x += _dist;

        _dist = (areaPos.y - areaSize.y * 0.5f) - (transform.position.y + _pos.y - ranges[0].y);

        if (_dist > 0) _pos.y += _dist;

        _dist = (areaPos.y + areaSize.y * 0.5f) - (transform.position.y + _pos.y + ranges[0].y);

        if (_dist < 0) _pos.y += _dist;

        return _pos;
    }

#if UNITY_EDITOR

    private readonly List<Color> colors = new() { Color.green, Color.red, Color.gray };

    private void OnDrawGizmos()
    {
        for (int i = 0; i < ranges.Count; i++)
        {
            Gizmos.color = colors[i];
            DrawEllipse(ranges[i].x, ranges[i].y, 50);
        }
    }

    private void DrawEllipse(float _radiusX, float _radiusY, int _segments)
    {
        Vector2 _previousPoint = GetEllipsePoint(transform.position, 0, _radiusX, _radiusY);
        Vector2 _currentPoint;

        for (int i = 1; i <= _segments; i++)
        {
            _currentPoint = GetEllipsePoint(transform.position, i * Mathf.PI * 2 / _segments, _radiusX, _radiusY);

            Gizmos.DrawLine(_previousPoint, _currentPoint);

            _previousPoint = _currentPoint;
        }
    }

    private Vector2 GetEllipsePoint(Vector2 _pos, float _angle, float _radiusX, float _radiusY)
    {
        return _pos + new Vector2(Mathf.Cos(_angle) * _radiusX, Mathf.Sin(_angle) * _radiusY);
    }
#endif
}
