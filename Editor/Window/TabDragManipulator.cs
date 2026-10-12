using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MM.SpriteAnimation
{
    internal sealed class TabDragManipulator : PointerManipulator
    {
        private const float DragThreshold = 6f;
        private const string DraggingClass = "sab-tab--dragging";
        private const string DropBeforeClass = "sab-tab--drop-before";
        private const string DropAfterClass = "sab-tab--drop-after";

        private readonly IReadOnlyList<VisualElement> siblings;
        private readonly Action<int, int> moved;

        private Vector2 startPosition;
        private bool pressed;
        private bool dragging;
        private int dropIndex = -1;

        internal TabDragManipulator(IReadOnlyList<VisualElement> siblings, Action<int, int> moved)
        {
            this.siblings = siblings;
            this.moved = moved;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt)) return;
            pressed = true;
            dragging = false;
            startPosition = evt.position;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!pressed) return;
            if (!dragging)
            {
                if (((Vector2)evt.position - startPosition).sqrMagnitude < DragThreshold * DragThreshold) return;
                dragging = true;
                target.AddToClassList(DraggingClass);
                target.CapturePointer(evt.pointerId);
            }

            UpdateDropIndex(evt.position.x);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!pressed) return;

            bool wasDragging = dragging;
            int from = IndexOf(target);
            int to = dropIndex;
            if (target.HasPointerCapture(evt.pointerId)) target.ReleasePointer(evt.pointerId);
            ResetState();

            if (!wasDragging) return;
            evt.StopImmediatePropagation();
            if (from >= 0 && to >= 0 && to != from) moved(from, to);
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if (dragging) ResetState();
        }

        private void UpdateDropIndex(float pointerX)
        {
            ClearIndicators();

            int slot = 0;
            while (slot < siblings.Count && siblings[slot].worldBound.center.x < pointerX) slot++;

            int from = IndexOf(target);
            dropIndex = slot > from ? slot - 1 : slot;
            if (dropIndex == from) return;

            if (slot < siblings.Count) siblings[slot].AddToClassList(DropBeforeClass);
            else siblings[siblings.Count - 1].AddToClassList(DropAfterClass);
        }

        private void ResetState()
        {
            pressed = false;
            dragging = false;
            dropIndex = -1;
            target.RemoveFromClassList(DraggingClass);
            ClearIndicators();
        }

        private void ClearIndicators()
        {
            foreach (VisualElement sibling in siblings)
            {
                sibling.RemoveFromClassList(DropBeforeClass);
                sibling.RemoveFromClassList(DropAfterClass);
            }
        }

        private int IndexOf(VisualElement element)
        {
            for (int i = 0; i < siblings.Count; i++)
            {
                if (siblings[i] == element) return i;
            }
            return -1;
        }
    }
}
