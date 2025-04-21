using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class SwipeView : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private UnityEvent beginCallback;
    [SerializeField] private UnityEvent<int> endCallback;

    private ScrollRect scrollRect;

    private Coroutine swipeCoroutine;

    private int childCount;
    private float step;

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

        beginCallback?.Invoke();
    }

    private void EndSwipe()
    {
        if (childCount == 0) return;

        swipeCoroutine = StartCoroutine(Swipe(scrollRect.vertical));
    }

    private IEnumerator Swipe(bool vert)
    {
        yield return new WaitUntil(() => Vector2.Distance(Vector2.zero, scrollRect.velocity) < 200);

        scrollRect.velocity = Vector2.zero;

        Vector2 currentPos = scrollRect.normalizedPosition;
        Vector2 targetPos;

        if (vert)
        {
            (float, int) value = Round(currentPos.y);

            targetPos = Vector2.up * value.Item1;
            SelectIndex = value.Item2;
        }
        else
        {
            (float, int) value = Round(currentPos.x);

            targetPos = Vector2.right * value.Item1;
            SelectIndex = value.Item2;
        }

        while (true)
        {
            currentPos = scrollRect.normalizedPosition;

            scrollRect.normalizedPosition = Vector2.Lerp(currentPos, targetPos, Time.unscaledDeltaTime * 5);

            if (Vector2.Distance(currentPos, targetPos) < 0.001f) break;

            yield return null;
        }

        endCallback?.Invoke(SelectIndex);
    }

    private (float, int) Round(float value)
    {
        int round = Mathf.RoundToInt(value / step);

        return (round * step, round);
    }
}
