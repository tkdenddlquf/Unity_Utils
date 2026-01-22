using UnityEngine;

namespace Yang.UIController
{
    public abstract class UIBase<T> : MonoBehaviour where T : System.Enum
    {
        [SerializeField] protected CanvasGroup canvasGroup;

        public abstract T UIType { get; }

        public bool IsActive { get; protected set; }

        public virtual void Init() => SetActive(false);

        public virtual void SetActive(bool active)
        {
            if (active)
            {
                canvasGroup.alpha = 1;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                canvasGroup.alpha = 0;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            IsActive = active;
        }

        public abstract void SetData<U>(U dataMarker) where U : struct, IDataMarker;

        public abstract bool GetData<U>(string markerID, out U result);
    }
}