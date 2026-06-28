using System;
using UnityEngine.InputSystem;

namespace Yang.Input
{
    /// <summary><see cref="IInputBinder"/>의 내부 구현. 바인딩을 수신자 엔트리에 기록한다.</summary>
    internal sealed class InputBinder : IInputBinder
    {
        private readonly InputReceiverEntry _entry;

        public InputBinder(InputReceiverEntry entry)
        {
            _entry = entry;
        }

        public IInputBinder Bind(InputAction action, Action<InputAction.CallbackContext> performed) => Bind(action, null, performed, null);

        public IInputBinder Bind(InputAction action, Action<InputAction.CallbackContext> started, Action<InputAction.CallbackContext> performed, Action<InputAction.CallbackContext> canceled)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (started == null && performed == null && canceled == null) throw new ArgumentException("최소 하나의 콜백(started/performed/canceled)을 지정해야 합니다.");

            _entry.Bindings.Add(new InputBindingEntry
            {
                Action = action,
                Started = started,
                Performed = performed,
                Canceled = canceled,
            });

            return this;
        }
    }
}