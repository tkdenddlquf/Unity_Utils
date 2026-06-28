using System.Collections.Generic;
using UnityEngine;

namespace Yang.UIController
{
    public abstract class ScreenManagerBase<TManager, TEnum> : SingletonBase<TManager> where TManager : MonoBehaviour where TEnum : System.Enum
    {
        protected readonly Dictionary<TEnum, UIBase<TEnum>> screenDict = new();

        public bool Initialized { get; private set; }

        public TEnum CurrentScreen { get; private set; }

        protected virtual void Awake() => Init(default);

        protected void Init(TEnum type)
        {
            UIBase<TEnum>[] screens = FindObjectsByType<UIBase<TEnum>>(FindObjectsSortMode.None);

            screenDict.Clear();

            foreach (UIBase<TEnum> screen in screens)
            {
                screen.Init();

                screenDict.Add(screen.UIType, screen);
            }

            ChangeScreen(type);

            Initialized = true;
        }

        public void ChangeScreen(TEnum type)
        {
            if (screenDict.TryGetValue(type, out UIBase<TEnum> changeScreen))
            {
                if (screenDict.TryGetValue(CurrentScreen, out UIBase<TEnum> currentScreen) && currentScreen.IsActive)
                    currentScreen.SetActive(false);

                CurrentScreen = type;

                changeScreen.SetActive(true);

                UIRayUtility.SetFocus(null);
            }
        }

        public void SetData<TData>(TEnum type, TData dataMarker) where TData : struct, IDataMarker
        {
            if (screenDict.TryGetValue(type, out UIBase<TEnum> screen)) screen.SetData(dataMarker);
        }

        public void SetData<TData>(TData dataMarker) where TData : struct, IDataMarker => SetData(CurrentScreen, dataMarker);

        public bool GetData<TData>(TEnum type, string markerID, out TData result) where TData : struct, IDataMarker
        {
            if (screenDict.TryGetValue(type, out UIBase<TEnum> screen)) return screen.GetData(markerID, out result);

            result = default;

            return false;
        }

        public bool GetData<TData>(string markerID, out TData result) where TData : struct, IDataMarker => GetData(CurrentScreen, markerID, out result);
    }
}
