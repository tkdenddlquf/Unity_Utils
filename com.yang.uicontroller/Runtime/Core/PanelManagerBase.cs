using System.Collections.Generic;
using UnityEngine;

namespace Yang.UIController
{
    public abstract class PanelManagerBase<T> : MonoBehaviour where T : System.Enum
    {
        private static PanelManagerBase<T> instance;
        public static PanelManagerBase<T> Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<PanelManagerBase<T>>();

                    if (instance == null)
                    {
                        GameObject obj = new() { name = typeof(PanelManagerBase<T>).Name };

                        instance = obj.AddComponent<PanelManagerBase<T>>();
                    }
                }

                return instance;
            }
        }

        protected readonly Dictionary<T, UIBase<T>> panelDict = new();

        public T CurrentPanel { get; private set; }

        protected virtual void Start() => Init(default);

        protected void Init(T type)
        {
            UIBase<T>[] panels = FindObjectsByType<UIBase<T>>(FindObjectsSortMode.None);

            panelDict.Clear();

            foreach (UIBase<T> panel in panels)
            {
                panel.Init();

                panelDict.Add(panel.UIType, panel);
            }

            ChangePanel(type);
        }

        public void ChangePanel(T type)
        {
            if (panelDict.TryGetValue(type, out UIBase<T> changePanel))
            {
                UIBase<T> currentPanel = panelDict[CurrentPanel];

                if (currentPanel.IsActive) currentPanel.SetActive(false);

                CurrentPanel = type;

                changePanel.SetActive(true);

                UIRaySystem.SetFocus(null);
            }
        }

        public void SetData<U>(T type, U dataMarker) where U : struct, IDataMarker
        {
            if (panelDict.TryGetValue(type, out UIBase<T> panel)) panel.SetData(dataMarker);
        }

        public void SetData<U>(U dataMarker) where U : struct, IDataMarker => panelDict[CurrentPanel].SetData(dataMarker);

        public bool GetData<U>(T type, string markerID, out U result)
        {
            if (panelDict.TryGetValue(type, out UIBase<T> panel)) return panel.GetData(markerID, out result);

            result = default;

            return false;
        }

        public bool GetData<U>(string markerID, out U result) => panelDict[CurrentPanel].GetData(markerID, out result);
    }
}