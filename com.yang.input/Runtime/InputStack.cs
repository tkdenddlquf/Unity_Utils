using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yang.Input
{
    /// <summary>
    /// 전역 단일 <see cref="InputStackController"/>에 대한 정적 파사드
    /// 여러 개의 독립 스택 필요시 <see cref="InputStackController"/>를 직접 생성
    /// </summary>
    public static class InputStack
    {
        private static InputStackController _default = new InputStackController();

        /// <summary>내부 기본 컨트롤러. 고급 사용 시 직접 접근.</summary>
        public static InputStackController Default => _default;

        public static IInputReceiver Target => _default.Target;
        public static int Count => _default.Count;

        public static event Action<IInputReceiver> TopChanged
        {
            add => _default.TargetChanged += value;
            remove => _default.TargetChanged -= value;
        }

        public static void Register(IInputReceiver receiver) => _default.Register(receiver);

        public static bool Unregister(IInputReceiver receiver) => _default.Unregister(receiver);

        public static void Clear() => _default.Clear();

        public static bool Contains(IInputReceiver receiver) => _default.Contains(receiver);

        public static bool IsTarget(IInputReceiver receiver) => _default.IsTarget(receiver);

        public static IReadOnlyList<IInputReceiver> GetSnapshot() => _default.GetSnapshot();

        // 도메인 리로드를 끈(Enter Play Mode Options) 환경에서도 플레이 진입 시 상태를 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            _default.Clear();
            _default = new InputStackController();
        }
    }
}