using UnityEngine;
using UnityEngine.InputSystem;
using Yang.Input;

namespace Yang.Input.Samples
{
    /// <summary>
    /// 사용 예시. 대화창이 열리면 자신을 입력 스택에 등록하고, 닫히면 등록 해제한다.
    /// 최상단일 때만 Submit/Cancel 입력을 받으며, 그 동안 아래 수신자(예: 플레이어 이동)는 입력이 차단된다.
    /// </summary>
    public sealed class DialogueInputExample : MonoBehaviour, IInputReceiver, IInputReceiverLifecycle
    {
        [SerializeField] private InputActionReference _submit;
        [SerializeField] private InputActionReference _cancel;

        /// <summary>대화창 열기: 스택 최상단으로 올라가 입력을 독점한다.</summary>
        public void Open() => InputStack.Register(this);

        /// <summary>대화창 닫기: 스택에서 빠지고 이전 수신자에게 입력이 돌아간다.</summary>
        public void Close() => InputStack.Unregister(this);

        // 최초 등록 시 호출 — 받고 싶은 입력만 바인딩한다. 여기 등록한 것 외에는 모두 차단된다.
        public void OnBind(IInputBinder binder)
        {
            if (_submit != null) binder.Bind(_submit.action, OnSubmit);
            if (_cancel != null) binder.Bind(_cancel.action, OnCancel);
        }

        public void OnActivated() => Debug.Log("[Dialogue] 입력 활성화");
        public void OnDeactivated() => Debug.Log("[Dialogue] 입력 비활성화");

        private void OnSubmit(InputAction.CallbackContext ctx) => Debug.Log("[Dialogue] Submit");

        private void OnCancel(InputAction.CallbackContext ctx)
        {
            Debug.Log("[Dialogue] Cancel");
            Close();
        }
    }
}
