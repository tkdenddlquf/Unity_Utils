using System;
using UnityEngine.InputSystem;

namespace Yang.Input
{
    /// <summary> 단일 InputAction과 그 콜백들을 담는 내부 레코드 </summary>
    internal sealed class InputBindingEntry
    {
        public InputAction Action;

        public Action<InputAction.CallbackContext> Started;
        public Action<InputAction.CallbackContext> Performed;
        public Action<InputAction.CallbackContext> Canceled;

        /// <summary> 컨트롤러에 의해 Enable() 된 액션인지에 대한 여부 </summary>
        public bool EnabledByUs;
    }
}