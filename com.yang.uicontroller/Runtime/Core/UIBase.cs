using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yang.UIController
{
    public abstract class UIBase<TEnum> : MonoBehaviour where TEnum : Enum
    {
        public abstract TEnum UIType { get; }

        public bool IsActive { get; protected set; }

        private readonly Dictionary<string, Delegate> setHandlers = new();
        private readonly Dictionary<string, Delegate> getHandlers = new();

        public virtual void Init()
        {
            setHandlers.Clear();
            getHandlers.Clear();

            SetActive(false);
        }

        protected void Subscribe<TData>(string id, Action<TData> handler) => setHandlers[id] = handler;

        protected void Provide<TResult>(string id, Func<TResult> provider) => getHandlers[id] = provider;

        public virtual void SetActive(bool active) => IsActive = active;

        public void SetData<TData>(TData dataMarker) where TData : IDataMarker
        {
            if (setHandlers.TryGetValue(dataMarker.MarkerID, out Delegate handler) && handler is Action<TData> typed) typed(dataMarker);
        }

        public void SetData<TData>(string id, TData data)
        {
            if (setHandlers.TryGetValue(id, out Delegate handler) && handler is Action<TData> typed) typed(data);
        }

        public bool GetData<TResult>(out TResult result) where TResult : IDataMarker
        {
            result = default;

            if (getHandlers.TryGetValue(result.MarkerID, out Delegate provider) && provider is Func<TResult> typed)
            {
                result = typed();

                return true;
            }

            return false;
        }

        public bool GetData<TResult>(string id, out TResult result)
        {
            if (getHandlers.TryGetValue(id, out Delegate provider) && provider is Func<TResult> typed)
            {
                result = typed();

                return true;
            }

            result = default;

            return false;
        }
    }
}
