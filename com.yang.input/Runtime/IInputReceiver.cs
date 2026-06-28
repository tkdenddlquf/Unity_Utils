namespace Yang.Input
{
    /// <summary> 입력 대상 인터페이스 </summary>
    public interface IInputReceiver
    {
        /// <summary>
        /// <see cref="InputStackController.Register(IInputReceiver)"/>될 때 호출되며,
        /// 전달된 <see cref="IInputBinder"/>로 InputAction과 콜백을 연결
        /// </summary>
        public void OnBind(IInputBinder binder);
    }
}