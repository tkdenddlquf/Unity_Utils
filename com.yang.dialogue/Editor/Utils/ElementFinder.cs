using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Extension helpers for walking up the UI Toolkit visual tree to find ancestors of a given type/name.
    /// </summary>
    public static class ElementFinder
    {
        /// <summary>Returns the nearest ancestor of type <typeparamref name="T"/>, or null.</summary>
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

        /// <summary>Returns the nearest ancestor of type <typeparamref name="T"/>, or null.</summary>
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

        /// <summary>Returns the nearest ancestor of type <typeparamref name="T"/> with the given name, or null.</summary>
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

        /// <summary>Returns the nearest ancestor of type <typeparamref name="T"/> with the given name, or null.</summary>
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

        /// <summary>Returns the element itself or its nearest ancestor of type <typeparamref name="T"/>, or null.</summary>
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

        /// <summary>Returns the element itself or its nearest ancestor of type <typeparamref name="T"/>, or null.</summary>
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

        /// <summary>Returns the element itself or its nearest ancestor of type <typeparamref name="T"/> with the given name, or null.</summary>
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

        /// <summary>Returns the element itself or its nearest ancestor of type <typeparamref name="T"/> with the given name, or null.</summary>
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