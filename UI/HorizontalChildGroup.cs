using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class HorizontalChildGroup : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Events")]
    [SerializeField, Space(5f)] private UnityEvent<RectTransform> beginDragEvent;
    [SerializeField, Space(5f)] private UnityEvent<float> dragEvent;
    [SerializeField, Space(5f)] private UnityEvent<List<RectTransform>> endDragEvent;

    [Header("Child Settings")]
    [SerializeField] private float spacing = 10f;
    [SerializeField] private TextAnchor childAlignment = TextAnchor.MiddleCenter;

    private bool isMove;

    private RectTransform selectItem;

    private readonly List<RectTransform> childs = new();

    private void Start()
    {
        for (int i = 0; i < transform.childCount; i++) childs.Add(transform.GetChild(i).transform as RectTransform);

        Move(false);
    }

    private void OnValidate()
    {
        if (Application.isPlaying) Move();
        else
        {
            List<RectTransform> childs = new();

            for (int i = 0; i < transform.childCount; i++) childs.Add(transform.GetChild(i).transform as RectTransform);

            SetChildPos(childs, false);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        foreach (RectTransform child in childs)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(child, eventData.pressPosition))
            {
                selectItem = child;

                Move();

                beginDragEvent?.Invoke(selectItem);

                return;
            }
        }

        selectItem = null;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (selectItem == null) return;

        Vector2 pos = eventData.position;

        pos.y = selectItem.transform.position.y;

        selectItem.transform.position = pos;

        Vector2 fixedPos = selectItem.anchoredPosition;

        float checkPosX = selectItem.rect.width * 0.5f;

        if (selectItem.anchoredPosition.x < checkPosX) fixedPos.x = checkPosX;
        else
        {
            checkPosX = (transform as RectTransform).rect.width - selectItem.rect.width * 0.5f;

            if (selectItem.anchoredPosition.x > checkPosX) fixedPos.x = checkPosX;
        }

        selectItem.anchoredPosition = fixedPos;

        childs.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        dragEvent?.Invoke(fixedPos.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        selectItem = null;

        endDragEvent?.Invoke(childs);
    }

    private void Move(bool lerp = true)
    {
        if (isMove) return;

        isMove = true;

        StartCoroutine(MoveChilds(lerp));
    }

    private IEnumerator MoveChilds(bool lerp)
    {
        while (!SetChildPos(childs, lerp) || selectItem != null) yield return null;
    }

    private bool SetChildPos(List<RectTransform> childs, bool lerp)
    {
        Rect rect = (transform as RectTransform).rect;

        float totalWidth = 0f;

        if (spacing < 0) spacing = 0;

        for (int i = 0; i < childs.Count; i++) totalWidth += childs[i].rect.width;

        totalWidth += spacing * (childs.Count - 1);

        if (totalWidth > rect.width)
        {
            float overSpacing = (totalWidth - rect.width) / (childs.Count - 1);

            spacing -= overSpacing;
            totalWidth -= overSpacing * (childs.Count - 1);
        }

        float currentY;
        float currentX = childAlignment switch
        {
            TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => (rect.width - totalWidth) * 0.5f,
            TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => rect.width - totalWidth,
            _ => 0f,
        };

        bool endMove = true;
        Rect childRect;

        for (int i = 0; i < childs.Count; i++)
        {
            childRect = childs[i].rect;

            currentX += childRect.width * 0.5f;
            currentY = childAlignment switch
            {
                TextAnchor.UpperLeft or TextAnchor.UpperCenter or TextAnchor.UpperRight => childRect.height * 0.5f,
                TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight => rect.height - childRect.height * 0.5f,
                _ => rect.height * 0.5f,
            };

            Vector2 movePos = new(currentX, -currentY);

            if (selectItem != childs[i])
            {
                if (lerp) childs[i].anchoredPosition = Vector2.Lerp(childs[i].anchoredPosition, movePos, Time.deltaTime * 10);
                else childs[i].anchoredPosition = movePos;
            }

            currentX += childRect.width * 0.5f + spacing;

            if (Vector2.Distance(childs[i].anchoredPosition, movePos) > 0.01f) endMove = false;
            else childs[i].anchoredPosition = movePos;
        }

        isMove = !endMove;

        return endMove;
    }
}
