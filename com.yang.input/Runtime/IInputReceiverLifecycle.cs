namespace Yang.Input
{
    /// <summary> 최상단 활성/비활성 전환 알림이 필요한 수신자가 추가로 구현하는 선택적 인터페이스 </summary>
    public interface IInputReceiverLifecycle
    {
        /// <summary> 이 수신자가 활성화 되었을 때 호출 </summary>
        public void OnActivated();

        /// <summary> 이 수신자가 비활성화 되었을 때 호출 </summary>
        public void OnDeactivated();
    }
}