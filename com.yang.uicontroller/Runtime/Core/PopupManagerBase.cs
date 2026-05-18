using System.Collections.Generic;
using UnityEngine;

namespace Yang.UIController
{
    public abstract class PopupManagerBase<T, U> : MonoBehaviour where T : MonoBehaviour where U : System.Enum
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<T>();

                    if (instance == null)
                    {
                        GameObject obj = new() { name = typeof(T).Name };

                        instance = obj.AddComponent<T>();
                    }
                }

                return instance;
            }
        }

        public bool IsActive => activeUIs.Count != 0;

        public bool Initialized { get; private set; } = false;

        protected readonly Dictionary<U, UIBase<U>> popupDict = new();

        protected readonly List<UIBase<U>> activeUIs = new();

        protected virtual void Awake() => Init();

        protected void Init()
        {
            UIBase<U>[] popups = FindObjectsByType<UIBase<U>>(FindObjectsSortMode.None);

            popupDict.Clear();

            foreach (UIBase<U> popup in popups)
            {
                popup.Init();

                popupDict.Add(popup.UIType, popup);
            }

            Initialized = true;
        }

        public bool CheckActive(U type)
        {
            if (popupDict.TryGetValue(type, out UIBase<U> popup)) return popup.IsActive;

            return false;
        }

        public bool CheckFocus(U type)
        {
            if (popupDict.TryGetValue(type, out UIBase<U> popup)) return popup == activeUIs[^1];

            return false;
        }

        public void CloseAllPopup()
        {
            while (activeUIs.Count != 0) InactivePopup(activeUIs[0].UIType);
        }

        public void FocusPopup(U type)
        {
            if (popupDict.TryGetValue(type, out UIBase<U> popup) && popup.IsActive)
            {
                activeUIs.Remove(popup);

                popup.transform.SetAsLastSibling();

                activeUIs.Add(popup);
            }
        }

        public void ActivePopup(U type) => SetActivePopup(type, true);

        public void InactivePopup(U type) => SetActivePopup(type, false);

        protected void SetActivePopup(U type, bool active)
        {
            if (popupDict.TryGetValue(type, out UIBase<U> popup))
            {
                if (popup.IsActive == active) return;

                popup.SetActive(active);

                if (active) FocusPopup(type);
                else activeUIs.Remove(popup);

                UIRaySystem.SetFocus(null);
            }
        }

        public void SetData<V>(U type, V dataMarker) where V : struct, IDataMarker
        {
            if (popupDict.TryGetValue(type, out UIBase<U> popup)) popup.SetData(dataMarker);
        }

        public void SetData<V>(V dataMarker) where V : struct, IDataMarker
        {
            if (activeUIs.Count != 0) activeUIs[^1].SetData(dataMarker);
        }

        public bool GetData<V>(U type, string markerID, out V result)
        {
            if (popupDict.TryGetValue(type, out UIBase<U> popup)) return popup.GetData(markerID, out result);

            result = default;

            return false;
        }

        public bool GetData<V>(string markerID, out V result)
        {
            if (activeUIs.Count != 0) return activeUIs[^1].GetData(markerID, out result);

            result = default;

            return false;
        }
    }
}