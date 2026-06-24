using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Drag-to-reorder grip for node rows, replacing the old ▲▼ buttons. Grab the handle and drag a
    /// row up/down within its sibling container; reordering is performed as a sequence of adjacent
    /// swaps so each node only has to implement one primitive.
    ///
    /// While dragging, the row is lifted out of layout flow (<c>position: absolute</c>) and tracks the
    /// cursor by its <c>top</c> offset, so it glides smoothly with no per-frame layout reads (the source
    /// of the old jitter). A placeholder holds the gap and slides between siblings to preview where the
    /// row will land. The data/element order is left untouched during the drag and committed once on
    /// release, so <paramref name="swapAdjacent"/> never sees an inconsistent index.
    ///
    /// <paramref name="row"/> is the element that moves (an item container, or an output Port).
    /// <paramref name="lockedCount"/> leading siblings can never move (e.g. a Condition's default port).
    /// <paramref name="swapAdjacent"/> swaps data + elements for the adjacent pair (lower, lower + 1).
    /// <paramref name="onComplete"/> runs once after a drag settles (e.g. RefreshPorts).
    /// </summary>
    internal static class RowDrag
    {
        public static Label CreateHandle(VisualElement row, int lockedCount, Action<int, int> swapAdjacent, Action onComplete = null)
        {
            Label grip = new("≡") { name = "drag-handle", tooltip = "Drag to reorder" };

            grip.AddToClassList("dlg-grip");

            bool dragging = false;

            VisualElement placeholder = null;

            int originalIndex = 0;       // row's index/data slot at grab time — never touched mid-drag
            int currentTarget = 0;       // index the row will land on, tracked from the placeholder
            float startTop = 0f;         // row's layout top at grab time
            float grabPointerY = 0f;     // pointer Y at grab time, to drive the absolute follow by delta

            // Slides the placeholder to the slot under the pointer so the siblings make room there. The
            // dragged row is skipped while measuring (it floats), and the placeholder is pulled out first
            // so the raw insert index is unambiguous.
            void Track(float pointerY)
            {
                VisualElement container = row.parent;

                if (container == null) return;

                placeholder.RemoveFromHierarchy();

                currentTarget = TargetIndex(container, row, pointerY, lockedCount);

                int insert = Mathf.Clamp(currentTarget, lockedCount, container.childCount);

                container.Insert(insert, placeholder);
            }

            // Move the floating row by the pointer delta only — no layout reads, so it never lags or snaps.
            void FollowPointer(float pointerY)
            {
                row.style.top = startTop + (pointerY - grabPointerY);
            }

            // Restore the row to normal flow and tear down the placeholder / drag styling.
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

            // Settle the drag: put the row element back at its data slot, then walk it to the target with
            // the adjacent-swap primitive so data and elements move together.
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

                // Placeholder takes over the row's vertical footprint (height + margins) so nothing shifts
                // when the row pops out of flow.
                placeholder = new VisualElement();
                placeholder.AddToClassList("dlg-row-placeholder");
                placeholder.style.height = bound.height;
                placeholder.style.width = bound.width;
                placeholder.style.marginTop = row.resolvedStyle.marginTop;
                placeholder.style.marginBottom = row.resolvedStyle.marginBottom;
                placeholder.style.flexShrink = 0f;

                // Lift the row out of flow, pin its size, and raise it above its siblings. BringToFront
                // only moves the element (not the data) and is undone in Commit before any swap.
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

            // Settle the drag if pointer capture is lost without a normal release (e.g. focus change).
            grip.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (!dragging) return;

                Commit();
            });

            return grip;
        }

        /// <summary>Final index the dragged row should land on: locked offset plus the number of
        /// reorderable siblings whose vertical center sits above the pointer.</summary>
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
