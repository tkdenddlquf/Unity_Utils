using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yang.UIController
{
    public abstract class UIBase<TEnum> : MonoBehaviour where TEnum : System.Enum
    {
        [SerializeField] protected CanvasGroup canvasGroup;

        public abstract TEnum UIType { get; }

        public bool IsActive { get; protected set; }

        private readonly Dictionary<string, Delegate> setHandlers = new();
        private readonly Dictionary<string, Delegate> getHandlers = new();

        public virtual void Init()
        {
            setHandlers.Clear();
            getHandlers.Clear();

            RegisterData();

            SetActive(false);
        }

        // 각 UI가 처리할 MarkerID를 여기서 Subscribe/Provide로 등록합니다.
        protected virtual void RegisterData() { }

        protected void Subscribe<TData>(string markerID, Action<TData> handler) where TData : struct, IDataMarker
            => setHandlers[markerID] = handler;

        protected void Provide<TData>(string markerID, Func<TData> provider) where TData : struct, IDataMarker
            => getHandlers[markerID] = provider;

        public virtual void SetActive(bool active)
        {
            if (active)
            {
                canvasGroup.alpha = 1;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                canvasGroup.alpha = 0;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            IsActive = active;
        }

        public void SetData<TData>(TData dataMarker) where TData : struct, IDataMarker
        {
            if (setHandlers.TryGetValue(dataMarker.MarkerID, out Delegate handler) && handler is Action<TData> typed)
                typed(dataMarker);
        }

        public bool GetData<TData>(string markerID, out TData result) where TData : struct, IDataMarker
        {
            if (getHandlers.TryGetValue(markerID, out Delegate provider) && provider is Func<TData> typed)
            {
                result = typed();

                return true;
            }

            result = default;

            return false;
        }
    }
}
