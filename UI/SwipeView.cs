using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class SwipeView : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Settings")]
    [SerializeField] private float waitVelocity = 200f;

    [SerializeField, Tooltip("스크롤 속도")] private float scrollPower = 5f;
    [SerializeField, Tooltip("스크롤 정지 거리")] private float setDistance = 0.01f;

    [Header("Events")]
    [SerializeField, Space(5f)] private UnityEvent onBeginDrag;
    [SerializeField, Space(5f)] private UnityEvent<int> onEndDrag;

    private ScrollRect scrollRect;

    private Coroutine swipeCoroutine;

    private int childCount;
    private float step;

    public int CurrentIndex => PositionToIndex(scrollRect.vertical ? scrollRect.normalizedPosition.y : scrollRect.normalizedPosition.x);

    public int SelectIndex { get; private set; } = 0;

    public void UpdateContents()
    {
        if (scrollRect == null) TryGetComponent(out scrollRect);

        childCount = scrollRect.content.childCount;

        step = 1f / (childCount - 1);

        BeginSwipe();
        EndSwipe();
    }

    public void OnBeginDrag(PointerEventData data) => BeginSwipe();

    public void OnEndDrag(PointerEventData data) => EndSwipe();

    private void BeginSwipe()
    {
        if (childCount == 0) return;
        if (swipeCoroutine != null) StopCoroutine(swipeCoroutine);

        swipeCoroutine = null;

        onBeginDrag?.Invoke();
    }

    private void EndSwipe()
    {
        if (childCount == 0) return;

        swipeCoroutine = StartCoroutine(Swipe(scrollRect.vertical));
    }

    private IEnumerator Swipe(bool vertSwipe)
    {
        yield return new WaitUntil(() => Vector2.Distance(Vector2.zero, scrollRect.velocity) < waitVelocity);

        scrollRect.velocity = Vector2.zero;

        Vector2 currentPos = scrollRect.normalizedPosition;
        Vector2 targetPos = PositionToIndex(vertSwipe ? currentPos.y : currentPos.x) * step * Vector2.up;

        while (true)
        {
            currentPos = scrollRect.normalizedPosition;

            scrollRect.normalizedPosition = Vector2.Lerp(currentPos, targetPos, Time.unscaledDeltaTime * scrollPower);

            if (Vector2.Distance(currentPos, targetPos) < setDistance) break;

            yield return null;
        }

        SelectIndex = PositionToIndex(vertSwipe ? currentPos.y : currentPos.x);

        onEndDrag?.Invoke(SelectIndex);
    }

    private int PositionToIndex(float value) => childCount == 0 ? -1 : Mathf.RoundToInt(value / step);
}
