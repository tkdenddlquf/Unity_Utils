using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>Provides a drag-to-reorder grip for node rows, committing reorders via adjacent swaps.</summary>
    internal static class RowDrag
    {
        /// <summary>Creates a drag handle that reorders the given row within its container on release.</summary>
        public static Label CreateHandle(VisualElement row, int lockedCount, Action<int, int> swapAdjacent, Action onComplete = null)
        {
            Label grip = new("≡") { name = "drag-handle", tooltip = "Drag to reorder" };

            grip.AddToClassList("dlg-grip");

            bool dragging = false;

            VisualElement placeholder = null;

            int originalIndex = 0;
            int currentTarget = 0;
            float startTop = 0f;
            float grabPointerY = 0f;

            /// <summary>Moves the placeholder to the slot under the pointer to preview the drop position.</summary>
            void Track(float pointerY)
            {
                VisualElement container = row.parent;

                if (container == null) return;

                placeholder.RemoveFromHierarchy();

                currentTarget = TargetIndex(container, row, pointerY, lockedCount);

                int insert = Mathf.Clamp(currentTarget, lockedCount, container.childCount);

                container.Insert(insert, placeholder);
            }

            /// <summary>Moves the floating row to follow the pointer by its drag delta.</summary>
            void FollowPointer(float pointerY)
            {
                row.style.top = startTop + (pointerY - grabPointerY);
            }

            /// <summary>Restores the row to normal layout flow and tears down the placeholder and drag styling.</summary>
            void EndDrag()
            {
                dragging = false;

                grip.RemoveFromClassList("dlg-grip--active");
                row.RemoveFromClassList("dlg-row--drag");

                row.style.position = StyleKeyword.Null;
                row.style.left = StyleKeyword.Null;
                row.style.top = StyleKeyword.Null;
                row.style.width = StyleKeyword.Null;

                placeholder?.RemoveFromHierarchy();
                placeholder = null;
            }

            /// <summary>Settles the drag by returning the row to its slot and walking it to the target via adjacent swaps.</summary>
            void Commit()
            {
                int to = currentTarget;
                VisualElement container = row.parent;

                EndDrag();

                if (container != null)
                {
                    container.Insert(originalIndex, row);

                    int from = originalIndex;

                    while (from < to) { swapAdjacent(from, from + 1); from++; }
                    while (from > to) { swapAdjacent(from - 1, from); from--; }
                }

                onComplete?.Invoke();
            }

            grip.RegisterCallback<PointerDownEvent>(evt =>
            {
                VisualElement container = row.parent;

                if (container == null) return;

                dragging = true;

                originalIndex = container.IndexOf(row);
                currentTarget = originalIndex;
                grabPointerY = evt.position.y;

                Rect bound = row.layout;
                startTop = bound.y;

                placeholder = new VisualElement();
                placeholder.AddToClassList("dlg-row-placeholder");
                placeholder.style.height = bound.height;
                placeholder.style.width = bound.width;
                placeholder.style.marginTop = row.resolvedStyle.marginTop;
                placeholder.style.marginBottom = row.resolvedStyle.marginBottom;
                placeholder.style.flexShrink = 0f;

                row.style.position = Position.Absolute;
                row.style.left = bound.x;
                row.style.width = bound.width;
                row.style.top = startTop;
                row.BringToFront();

                container.Insert(originalIndex, placeholder);

                grip.CapturePointer(evt.pointerId);
                grip.AddToClassList("dlg-grip--active");
                row.AddToClassList("dlg-row--drag");

                evt.StopPropagation();
            });

            grip.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging) return;

                Track(evt.position.y);
                FollowPointer(evt.position.y);

                evt.StopPropagation();
            });

            grip.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging) return;

                Commit();

                grip.ReleasePointer(evt.pointerId);

                evt.StopPropagation();
            });

            grip.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (!dragging) return;

                Commit();
            });

            return grip;
        }

        /// <summary>Computes the target landing index from how many reorderable siblings sit above the pointer.</summary>
        private static int TargetIndex(VisualElement container, VisualElement row, float pointerY, int locked)
        {
            int count = container.childCount;
            int above = 0;

            for (int i = locked; i < count; i++)
            {
                VisualElement child = container[i];

                if (child == row) continue;

                if (child.worldBound.center.y < pointerY) above++;
            }

            return locked + above;
        }
    }
}
