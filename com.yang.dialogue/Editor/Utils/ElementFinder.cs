using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public static class ElementFinder
    {
        public static T FindParent<T>(this IEventHandler handler) where T : VisualElement
        {
            VisualElement current = (handler as VisualElement).parent;

            while (current != null)
            {
                if (current is T target) return target;

                current = current.parent;
            }

            return null;
        }

        public static T FindParent<T>(this VisualElement element) where T : VisualElement
        {
            VisualElement current = element.parent;

            while (current != null)
            {
                if (current is T target) return target;

                current = current.parent;
            }

            return null;
        }

        public static T FindParent<T>(this IEventHandler handler, string name) where T : VisualElement
        {
            VisualElement current = (handler as VisualElement).parent;

            while (current != null)
            {
                if (current.name == name && current is T target) return target;

                current = current.parent;
            }

            return null;
        }

        public static T FindParent<T>(this VisualElement element, string name) where T : VisualElement
        {
            VisualElement current = element.parent;

            while (current != null)
            {
                if (current.name == name && current is T target) return target;

                current = current.parent;
            }

            return null;
        }

        public static T FindParentInCurrent<T>(this IEventHandler handler) where T : VisualElement
        {
            VisualElement current = handler as VisualElement;

            while (current != null)
            {
                if (current is T target) return target;

                current = current.parent;
            }

            return null;
        }

        public static T FindParentInCurrent<T>(this VisualElement element) where T : VisualElement
        {
            VisualElement current = element;

            while (current != null)
            {
                if (current is T target) return target;

                current = current.parent;
            }

            return null;
        }

        public static T FindParentInCurrent<T>(this IEventHandler handler, string name) where T : VisualElement
        {
            VisualElement current = handler as VisualElement;

            while (current != null)
            {
                if (current.name == name && current is T target) return target;

                current = current.parent;
            }

            return null;
        }

        public static T FindParentInCurrent<T>(this VisualElement element, string name) where T : VisualElement
        {
            VisualElement current = element;

            while (current != null)
            {
                if (current.name == name && current is T target) return target;

                current = current.parent;
            }

            return null;
        }
    }
}