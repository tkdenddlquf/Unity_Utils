using System;
using UnityEngine.InputSystem;

namespace Yang.Input
{
    /// <summary> <see cref="IInputReceiver.OnBind"/>에 전달되어 InputAction과 콜백을 연결하는 데 사용 </summary>
    public interface IInputBinder
    {
        /// <summary> <paramref name="action"/>의 performed 단계에 <paramref name="performed"/> 콜백을 바인딩한다. </summary>
        public IInputBinder Bind(InputAction action, Action<InputAction.CallbackContext> performed);

        /// <summary>
        /// <paramref name="action"/>의 started/performed/canceled 단계에 각각 콜백을 바인딩
        /// 불필요 시 null 전달 (단, 모두 null이어선 안 됨)
        /// </summary>
        public IInputBinder Bind(InputAction action, Action<InputAction.CallbackContext> started, Action<InputAction.CallbackContext> performed, Action<InputAction.CallbackContext> canceled);
    }
}