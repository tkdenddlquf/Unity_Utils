using UnityEngine;

namespace Yang.Layer
{
    [System.Serializable]
    public class SingleLayer
    {
        public int layer;

        public static implicit operator int(SingleLayer singleLayer) => singleLayer.layer;

        public static implicit operator LayerMask(SingleLayer singleLayer) => 1 << singleLayer.layer;
    }
}