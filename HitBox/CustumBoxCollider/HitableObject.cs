using System;
using UnityEngine;

public class HitableObject : MonoBehaviour
{
    public Func<IndividualBase, bool> check;

    private IndividualBase hitBase;

    private void Start()
    {
        TryGetComponent(out hitBase);
    }

    public void Hit(IndividualBase _hitBase, int _type, Action<IndividualBase, int> _callback)
    {
        if (check(_hitBase)) _callback(hitBase, _type);
    }
}
