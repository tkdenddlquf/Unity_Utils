using System;
using System.Collections.Generic;

namespace Yang.Input
{
    /// <summary> 수신자(<see cref="IInputReceiver"/>)를 스택으로 관리하며, 활성화된 대상에게만 입력 전달 </summary>
    public sealed class InputStackController
    {
        private readonly List<InputReceiverEntry> _stack = new List<InputReceiverEntry>();
        private readonly bool _manageActionEnabling;

        /// <summary> 대상 변경 알림 콜백 </summary>
        public event Action<IInputReceiver> TargetChanged;

        /// <summary> 활성화 대상 </summary>
        public IInputReceiver Target => _stack.Count > 0 ? _stack[^1].Receiver : null;

        public int Count => _stack.Count;

        /// <param name="manageActionEnabling">
        /// true면 최상단이 될 때 바인딩된 InputAction을 자동으로 Enable하고, 밀려날 때
        /// (직접 켠 것만) Disable한다. 액션 활성화를 직접 관리한다면 false로 두면 콜백 연결/해제만 수행한다.
        /// </param>
        public InputStackController(bool manageActionEnabling = true)
        {
            _manageActionEnabling = manageActionEnabling;
        }

        public bool Contains(IInputReceiver receiver) => IndexOf(receiver) >= 0;

        public bool IsTarget(IInputReceiver receiver) => _stack.Count > 0 && ReferenceEquals(_stack[_stack.Count - 1].Receiver, receiver);

        public void Register(IInputReceiver receiver)
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));

            int existing = IndexOf(receiver);

            if (existing >= 0)
            {
                if (existing == _stack.Count - 1) return;

                InputReceiverEntry moved = _stack[existing];

                Deactivate(_stack[^1]);

                _stack.RemoveAt(existing);
                _stack.Add(moved);

                Activate(moved);

                TargetChanged?.Invoke(Target);

                return;
            }

            if (_stack.Count > 0) Deactivate(_stack[^1]);

            InputReceiverEntry entry = new InputReceiverEntry { Receiver = receiver };

            receiver.OnBind(new InputBinder(entry));

            _stack.Add(entry);

            Activate(entry);

            TargetChanged?.Invoke(Target);
        }

        public bool Unregister(IInputReceiver receiver)
        {
            if (receiver == null) return false;

            int idx = IndexOf(receiver);

            if (idx < 0) return false;

            bool wasTop = idx == _stack.Count - 1;

            if (wasTop) Deactivate(_stack[idx]);

            _stack.RemoveAt(idx);

            if (wasTop)
            {
                if (_stack.Count > 0) Activate(_stack[^1]);

                TargetChanged?.Invoke(Target);
            }

            return true;
        }

        public void Clear()
        {
            if (_stack.Count == 0) return;

            Deactivate(_stack[^1]);

            _stack.Clear();

            TargetChanged?.Invoke(null);
        }

        /// <summary> 현재 스택 스냅샷(맨 아래 → 최상단 순)을 반환한다. 디버그/표시 용도 </summary>
        public IReadOnlyList<IInputReceiver> GetSnapshot()
        {
            IInputReceiver[] arr = new IInputReceiver[_stack.Count];

            for (int i = 0; i < _stack.Count; i++) arr[i] = _stack[i].Receiver;

            return arr;
        }

        private int IndexOf(IInputReceiver receiver)
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (ReferenceEquals(_stack[i].Receiver, receiver)) return i;
            }

            return -1;
        }

        private void Activate(InputReceiverEntry entry)
        {
            if (entry.Active) return;

            foreach (var b in entry.Bindings)
            {
                if (_manageActionEnabling && !b.Action.enabled)
                {
                    b.Action.Enable();
                    b.EnabledByUs = true;
                }

                if (b.Started != null) b.Action.started += b.Started;
                if (b.Performed != null) b.Action.performed += b.Performed;
                if (b.Canceled != null) b.Action.canceled += b.Canceled;
            }

            entry.Active = true;

            (entry.Receiver as IInputReceiverLifecycle)?.OnActivated();
        }

        private void Deactivate(InputReceiverEntry entry)
        {
            if (!entry.Active) return;

            foreach (var b in entry.Bindings)
            {
                if (b.Started != null) b.Action.started -= b.Started;
                if (b.Performed != null) b.Action.performed -= b.Performed;
                if (b.Canceled != null) b.Action.canceled -= b.Canceled;

                if (b.EnabledByUs)
                {
                    b.Action.Disable();
                    b.EnabledByUs = false;
                }
            }

            entry.Active = false;

            (entry.Receiver as IInputReceiverLifecycle)?.OnDeactivated();
        }
    }
}