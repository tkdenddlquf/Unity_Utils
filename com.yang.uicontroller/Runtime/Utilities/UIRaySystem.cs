using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Yang.UIController
{
    public static class UIRaySystem
    {
        private static EventSystem eventSystem;
        private static PointerEventData pointerData;

        private static readonly List<RaycastResult> results = new();

        static UIRaySystem() => Init();

        public static void Init()
        {
            eventSystem = EventSystem.current;

            pointerData = new(eventSystem);
        }

        public static bool IsPointerOverUI()
        {
            if (eventSystem != EventSystem.current) Init();
            if (eventSystem == null) return false;

            pointerData.position = Mouse.current.position.ReadValue();

            eventSystem.RaycastAll(pointerData, results);

            return results.Count > 0;
        }

        public static bool IsPointerOverUI(Transform target)
        {
            if (IsPointerOverUI())
            {
                foreach (RaycastResult result in results)
                {
                    bool isChild = result.gameObject.transform.IsChildOf(target);

                    if (isChild) return true;
                }
            }

            return false;
        }

        public static void SetFocus(GameObject target)
        {
            if (eventSystem == null) return;
            if (eventSystem != EventSystem.current) Init();

            eventSystem.SetSelectedGameObject(target);
        }
    }
}