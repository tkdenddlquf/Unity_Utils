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
            UIBase<TEnum>[] screens = FindObjectsByType<UIBase<TEnum>>();

            screenDict.Clear();

            foreach (UIBase<TEnum> screen in screens)
            {
                screen.Init();

                screenDict[screen.UIType] = screen;
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

        #region Marker Data
        public void SetData<TData>(TEnum type, TData dataMarker) where TData : IDataMarker
        {
            if (screenDict.TryGetValue(type, out UIBase<TEnum> screen)) screen.SetData(dataMarker);
        }

        public void SetData<TData>(TData dataMarker) where TData : IDataMarker => SetData(CurrentScreen, dataMarker);

        public bool GetData<TResult>(TEnum type, out TResult result) where TResult : IDataMarker
        {
            if (screenDict.TryGetValue(type, out UIBase<TEnum> screen)) return screen.GetData(out result);

            result = default;

            return false;
        }

        public bool GetData<TResult>(out TResult result) where TResult : IDataMarker => GetData(CurrentScreen, out result);
        #endregion

        #region Normal Data
        public void SetData<TData>(TEnum type, string id, TData data)
        {
            if (screenDict.TryGetValue(type, out UIBase<TEnum> screen)) screen.SetData(id, data);
        }

        public void SetData<TData>(string id, TData data) => SetData(CurrentScreen, id, data);

        public bool GetData<TResult>(TEnum type, string id, out TResult result)
        {
            if (screenDict.TryGetValue(type, out UIBase<TEnum> screen)) return screen.GetData(id, out result);

            result = default;

            return false;
        }

        public bool GetData<TResult>(string id, out TResult result) => GetData(CurrentScreen, id, out result);
        #endregion
    }
}
