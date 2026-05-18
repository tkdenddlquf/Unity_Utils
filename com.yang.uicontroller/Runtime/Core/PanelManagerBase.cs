using System.Collections.Generic;
using UnityEngine;

namespace Yang.UIController
{
    public abstract class PanelManagerBase<T, U> : MonoBehaviour where T : MonoBehaviour where U : System.Enum
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

        protected readonly Dictionary<U, UIBase<U>> panelDict = new();

        public bool Initialized { get; private set; } = false;

        public U CurrentPanel { get; private set; }

        protected virtual void Awake() => Init(default);

        protected void Init(U type)
        {
            UIBase<U>[] panels = FindObjectsByType<UIBase<U>>(FindObjectsSortMode.None);

            panelDict.Clear();

            foreach (UIBase<U> panel in panels)
            {
                panel.Init();

                panelDict.Add(panel.UIType, panel);
            }

            ChangePanel(type);

            Initialized = true;
        }

        public void ChangePanel(U type)
        {
            if (panelDict.TryGetValue(type, out UIBase<U> changePanel))
            {
                UIBase<U> currentPanel = panelDict[CurrentPanel];

                if (currentPanel.IsActive) currentPanel.SetActive(false);

                CurrentPanel = type;

                changePanel.SetActive(true);

                UIRaySystem.SetFocus(null);
            }
        }

        public void SetData<V>(U type, V dataMarker) where V : struct, IDataMarker
        {
            if (panelDict.TryGetValue(type, out UIBase<U> panel)) panel.SetData(dataMarker);
        }

        public void SetData<V>(V dataMarker) where V : struct, IDataMarker => SetData(CurrentPanel, dataMarker);

        public bool GetData<V>(U type, string markerID, out V result)
        {
            if (panelDict.TryGetValue(type, out UIBase<U> panel)) return panel.GetData(markerID, out result);

            result = default;

            return false;
        }

        public bool GetData<V>(string markerID, out V result) => GetData(CurrentPanel, markerID, out result);
    }
}