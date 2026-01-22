using System.Collections.Generic;
using UnityEngine;

namespace Yang.UIController
{
    public abstract class PopupManagerBase<T> : MonoBehaviour where T : System.Enum
    {
        private static PopupManagerBase<T> instance;
        public static PopupManagerBase<T> Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<PopupManagerBase<T>>();

                    if (instance == null)
                    {
                        GameObject obj = new() { name = typeof(PopupManagerBase<T>).Name };

                        instance = obj.AddComponent<PopupManagerBase<T>>();
                    }
                }

                return instance;
            }
        }

        public bool IsActive => activeUIs.Count != 0;

        protected readonly Dictionary<T, UIBase<T>> popupDict = new();

        protected readonly List<UIBase<T>> activeUIs = new();

        protected virtual void Awake() => Init();

        protected void Init()
        {
            UIBase<T>[] popups = FindObjectsByType<UIBase<T>>(FindObjectsSortMode.None);

            popupDict.Clear();

            foreach (UIBase<T> popup in popups)
            {
                popup.Init();

                popupDict.Add(popup.UIType, popup);
            }
        }

        public bool CheckActive(T type) => popupDict[type].IsActive;

        public void CloseAllPopup()
        {
            while (activeUIs.Count != 0) InactivePopup(activeUIs[0].UIType);
        }

        public void ActivePopup(T type) => SetActivePopup(type, true);

        public void InactivePopup(T type) => SetActivePopup(type, false);

        protected void SetActivePopup(T type, bool active)
        {
            if (popupDict.TryGetValue(type, out UIBase<T> popup))
            {
                if (popup.IsActive == active) return;

                popup.SetActive(active);

                if (active)
                {
                    activeUIs.Add(popup);

                    popup.transform.SetAsLastSibling();
                }
                else activeUIs.Remove(popup);

                UIRaySystem.SetFocus(null);
            }
        }

        public void SetData<U>(T type, U dataMarker) where U : struct, IDataMarker
        {
            if (popupDict.TryGetValue(type, out UIBase<T> popup)) popup.SetData(dataMarker);
        }

        public void SetData<U>(U dataMarker) where U : struct, IDataMarker => activeUIs[^1].SetData(dataMarker);

        public bool GetData<U>(T type, string markerID, out U result)
        {
            if (popupDict.TryGetValue(type, out UIBase<T> popup)) return popup.GetData(markerID, out result);

            result = default;

            return false;
        }

        public bool GetData<U>(string markerID, out U result) => activeUIs[^1].GetData(markerID, out result);
    }
}