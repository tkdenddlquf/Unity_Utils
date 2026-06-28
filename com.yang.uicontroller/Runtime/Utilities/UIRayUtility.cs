using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Yang.UIController
{
    public static class UIRayUtility
    {
        private static EventSystem eventSystem;
        private static PointerEventData pointerData;

        private static readonly List<RaycastResult> results = new();

        static UIRayUtility() => Init();

        public static void Init()
        {
            eventSystem = EventSystem.current;

            pointerData = new(eventSystem);
        }

        public static bool IsPointerOverUI()
        {
            if (eventSystem != EventSystem.current) Init();
            if (eventSystem == null) return false;

            Pointer pointer = Pointer.current;
            if (pointer == null) return false;

            pointerData.position = pointer.position.ReadValue();

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
