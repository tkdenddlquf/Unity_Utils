using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[ExecuteAlways, DisallowMultipleComponent]
public class HorizontalChildGroup : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Events")]
    [SerializeField, Space(5f)] private UnityEvent<int> beginDragEvent;
    [SerializeField, Space(5f)] private UnityEvent<float> dragEvent;
    [SerializeField, Space(5f)] private UnityEvent<int, int> endDragEvent;

    [Header("Child Settings")]
    [SerializeField] private float spacing = 10f;
    [SerializeField] private TextAnchor childAlignment = TextAnchor.MiddleCenter;

    private bool isMove = false;

    private int childCount = 0;

    private int selectIndex = -1;
    private RectTransform selectChild;

    private RectTransform rect;

    private readonly List<RectTransform> childs = new();

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        CacheChildren();
    }

    private void OnEnable()
    {
        CacheChildren();
        RebuildLayout(false);
    }

#if UNITY_EDITOR
    private Vector2 lastSize;

    private struct ChildState
    {
        public Vector2 anchoredPos;
        public Vector2 sizeDelta;
    }

    private readonly Dictionary<RectTransform, ChildState> childStates = new();

    private void OnTransformChildrenChanged()
    {
        if (isMove) return;

        CacheChildren();
        RebuildLayout(false);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isMove || !rect) return;

        if (rect.rect.size != lastSize)
        {
            lastSize = rect.rect.size;

            RebuildLayout(false);
        }
    }

    private void LateUpdate()
    {
        if (isMove) return;

        int inactiveCount = 0;
        bool changed = false;

        foreach (Transform childTrans in transform)
        {
            if (childTrans.gameObject.activeSelf)
            {
                if (childTrans is RectTransform childRect)
                {
                    if (childStates.TryGetValue(childRect, out ChildState state))
                    {
                        if (childRect.anchoredPosition != state.anchoredPos || childRect.sizeDelta != state.sizeDelta)
                        {
                            changed = true;

                            childStates[childRect] = new ChildState
                            {
                                anchoredPos = childRect.anchoredPosition,
                                sizeDelta = childRect.sizeDelta
                            };
                        }
                    }
                }
            }
            else inactiveCount++;
        }

        if (transform.childCount - inactiveCount != childCount)
        {
            childCount = transform.childCount - inactiveCount;

            CacheChildren();

            changed = true;
        }

        if (changed) RebuildLayout(false);
    }

    private void CacheChildren()
    {
        childs.Clear();
        childStates.Clear();

        int inactiveCount = 0;

        foreach (Transform childTrans in transform)
        {
            if (childTrans.gameObject.activeSelf)
            {
                if (childTrans is RectTransform childRect)
                {
                    childStates[childRect] = new ChildState
                    {
                        anchoredPos = childRect.anchoredPosition,
                        sizeDelta = childRect.sizeDelta
                    };

                    childs.Add(childRect);
                }
                else inactiveCount++;
            }
            else inactiveCount++;
        }

        childCount = transform.childCount - inactiveCount;

        if (rect) lastSize = rect.rect.size;
    }
#else
    private void CacheChildren()
    {
        childs.Clear();

        foreach (Transform childTrans in transform)
        {
            if (childTrans.gameObject.activeSelf)
            {
                if (childTrans is RectTransform childRect) childs.Add(childRect);
                else inactiveCount++;
            }
            else inactiveCount++;
        }

        childCount = transform.childCount - inactiveCount;
    }
#endif

    public void OnBeginDrag(PointerEventData eventData)
    {
        for (int i = 0; i < childs.Count; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(childs[i], eventData.pressPosition))
            {
                selectIndex = i;
                selectChild = childs[i];

                DragChild();

                beginDragEvent?.Invoke(selectIndex);

                return;
            }
        }

        selectIndex = -1;
        selectChild = null;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (selectIndex == -1) return;

        Vector2 pos = eventData.position;

        pos.y = selectChild.position.y;

        selectChild.position = pos;

        Vector2 fixedPos = selectChild.anchoredPosition;

        float checkPosX = selectChild.rect.width * 0.5f;

        if (selectChild.anchoredPosition.x < checkPosX) fixedPos.x = checkPosX;
        else
        {
            checkPosX = rect.rect.width - selectChild.rect.width * 0.5f;

            if (selectChild.anchoredPosition.x > checkPosX) fixedPos.x = checkPosX;
        }

        selectChild.anchoredPosition = fixedPos;

        childs.Sort((a, b) => a.position.x.CompareTo(b.position.x));

        dragEvent?.Invoke(fixedPos.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (selectIndex == -1) return;

        int currentIndex = childs.IndexOf(selectChild);

        if (currentIndex < childCount - 1)
        {
            if (currentIndex == 0) selectChild.SetAsFirstSibling();
            else
            {
                if (selectIndex < currentIndex)
                {
                    int nextIndex = childs[currentIndex - 1].GetSiblingIndex();

                    selectChild.SetSiblingIndex(nextIndex);
                }
                else
                {
                    int nextIndex = childs[currentIndex + 1].GetSiblingIndex();

                    selectChild.SetSiblingIndex(nextIndex);
                }
            }
        }
        else selectChild.SetAsLastSibling();

        endDragEvent?.Invoke(selectIndex, currentIndex);

        selectIndex = -1;
        selectChild = null;
    }

    private void DragChild()
    {
        if (isMove) return;

        isMove = true;

        IEnumerator MoveWait()
        {
            while (!RebuildLayout(true) || selectChild != null) yield return null;
        }

        StartCoroutine(MoveWait());
    }

    private bool RebuildLayout(bool lerp)
    {
        int childCount = this.childCount - 1;
        float totalWidth = 0f;

        if (this.spacing < 0) this.spacing = 0;

        float spacing = this.spacing;

        for (int i = 0; i < childs.Count; i++) totalWidth += childs[i].rect.width;

        totalWidth += spacing * childCount;

        if (totalWidth > rect.rect.width)
        {
            float overSpacing = (totalWidth - rect.rect.width) / childCount;

            spacing -= overSpacing;
            totalWidth -= overSpacing * childCount;
        }

        float currentY;
        float currentX = childAlignment switch
        {
            TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => (rect.rect.width - totalWidth) * 0.5f,
            TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => rect.rect.width - totalWidth,
            _ => 0f,
        };

        bool endMove = true;
        Rect childRect;

        for (int i = 0; i < childs.Count; i++)
        {
            RectTransform childTrans = childs[i];

            childRect = childTrans.rect;

            currentX += childRect.width * 0.5f;
            currentY = childAlignment switch
            {
                TextAnchor.UpperLeft or TextAnchor.UpperCenter or TextAnchor.UpperRight => childRect.height * 0.5f,
                TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight => rect.rect.height - childRect.height * 0.5f,
                _ => rect.rect.height * 0.5f,
            };

            Vector2 movePos = new(currentX, -currentY);

            if (childs[i] != selectChild)
            {
                if (lerp) childTrans.anchoredPosition = Vector2.Lerp(childTrans.anchoredPosition, movePos, Time.deltaTime * 10);
                else childTrans.anchoredPosition = movePos;
            }

            currentX += childRect.width * 0.5f + spacing;

            if (Vector2.Distance(childTrans.anchoredPosition, movePos) > 0.01f) endMove = false;
            else childTrans.anchoredPosition = movePos;
        }

        isMove = !endMove;

        return endMove;
    }
}
