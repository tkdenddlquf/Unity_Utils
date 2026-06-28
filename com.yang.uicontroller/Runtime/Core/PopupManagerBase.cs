using System.Collections.Generic;
using UnityEngine;

namespace Yang.UIController
{
    public abstract class PopupManagerBase<TManager, TEnum> : SingletonBase<TManager> where TManager : MonoBehaviour where TEnum : System.Enum
    {
        public bool IsActive => activePopups.Count != 0;

        public bool Initialized { get; private set; }

        protected readonly Dictionary<TEnum, UIBase<TEnum>> popupDict = new();

        protected readonly List<UIBase<TEnum>> activePopups = new();

        protected virtual void Awake() => Init();

        protected void Init()
        {
            UIBase<TEnum>[] popups = FindObjectsByType<UIBase<TEnum>>(FindObjectsSortMode.None);

            popupDict.Clear();

            foreach (UIBase<TEnum> popup in popups)
            {
                popup.Init();

                popupDict.Add(popup.UIType, popup);
            }

            Initialized = true;
        }

        public bool IsPopupActive(TEnum type)
        {
            if (popupDict.TryGetValue(type, out UIBase<TEnum> popup)) return popup.IsActive;

            return false;
        }

        public bool IsPopupFocused(TEnum type)
        {
            if (activePopups.Count != 0 && popupDict.TryGetValue(type, out UIBase<TEnum> popup)) return popup == activePopups[^1];

            return false;
        }

        public void CloseAllPopups()
        {
            while (activePopups.Count != 0) ClosePopup(activePopups[0].UIType);
        }

        public void FocusPopup(TEnum type)
        {
            if (popupDict.TryGetValue(type, out UIBase<TEnum> popup) && popup.IsActive)
            {
                activePopups.Remove(popup);

                popup.transform.SetAsLastSibling();

                activePopups.Add(popup);
            }
        }

        public void OpenPopup(TEnum type) => SetActivePopup(type, true);

        public void ClosePopup(TEnum type) => SetActivePopup(type, false);

        protected void SetActivePopup(TEnum type, bool active)
        {
            if (popupDict.TryGetValue(type, out UIBase<TEnum> popup))
            {
                if (popup.IsActive == active) return;

                popup.SetActive(active);

                if (active) FocusPopup(type);
                else activePopups.Remove(popup);

                UIRayUtility.SetFocus(null);
            }
        }

        public void SetData<TData>(TEnum type, TData dataMarker) where TData : struct, IDataMarker
        {
            if (popupDict.TryGetValue(type, out UIBase<TEnum> popup)) popup.SetData(dataMarker);
        }

        public void SetData<TData>(TData dataMarker) where TData : struct, IDataMarker
        {
            if (activePopups.Count != 0) activePopups[^1].SetData(dataMarker);
        }

        public bool GetData<TData>(TEnum type, string markerID, out TData result) where TData : struct, IDataMarker
        {
            if (popupDict.TryGetValue(type, out UIBase<TEnum> popup)) return popup.GetData(markerID, out result);

            result = default;

            return false;
        }

        public bool GetData<TData>(string markerID, out TData result) where TData : struct, IDataMarker
        {
            if (activePopups.Count != 0) return activePopups[^1].GetData(markerID, out result);

            result = default;

            return false;
        }
    }
}
