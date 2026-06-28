using System.Collections.Generic;

namespace Yang.Input
{
    /// <summary> 수신자와 바인딩 목록을 담는 내부 레코드 </summary>
    internal sealed class InputReceiverEntry
    {
        public IInputReceiver Receiver;

        /// <summary> 활성화 여부 </summary>
        public bool Active;

        public readonly List<InputBindingEntry> Bindings = new List<InputBindingEntry>();
    }
}