# Input Stack (`com.yang.input`)

Unity **New Input System** 기반의 스택형 입력 라우팅 패키지.
입력 수신자를 스택으로 관리하고, **가장 마지막에 등록된(최상단) 수신자에게만** 입력을 전달한다.
그 동안 아래에 깔린 수신자들은 입력이 **완전히 차단**된다. UI 팝업이 게임 입력을 가로채는 상황,
모달 다이얼로그, 일시정지 메뉴 등 "특정 클래스로 입력을 제한"하고 싶을 때 쓴다.

## 요구 사항
- Unity 2021.3 이상
- `com.unity.inputsystem` 1.4.0 이상

## 설치
Package Manager에서 다음 중 하나로 설치한다.
- **git URL**: `Add package from git URL...` → 이 저장소 URL
- **로컬**: `Add package from disk...` → 이 폴더의 `package.json` 선택
- **임베드**: 프로젝트의 `Packages/` 폴더에 이 폴더를 통째로 복사

## 핵심 개념
- `IInputReceiver` — 입력을 받을 클래스가 구현. 처음 등록될 때 `OnBind`가 호출된다.
- `IInputBinder` — `OnBind`에서 받는 객체. 받고 싶은 `InputAction`에만 콜백을 연결한다.
- `InputStack` — 전역 정적 파사드. `Register` / `Unregister` 로 스택을 조작한다.
- `InputStackController` — 실제 스택 로직. 독립된 스택이 필요하면 직접 생성한다.
- `IInputReceiverLifecycle` *(선택)* — 활성/비활성 전환 시 알림(`OnActivated`/`OnDeactivated`)을 받는다.

연결한 콜백은 **그 수신자가 활성 대상(최상단)일 때만** 실제 InputAction 이벤트에 연결된다.
대상이 바뀌면 이전 대상의 콜백은 자동으로 해제되므로, 입력이 아래로 새지 않는다.
`OnBind`는 **최초 등록 시 한 번만** 호출되며, 이후 최상단으로 다시 올라올 때는 기존 바인딩이 재사용된다.

## 빠른 시작
```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using Yang.Input;

public class PauseMenu : MonoBehaviour, IInputReceiver
{
    [SerializeField] private InputActionReference _navigate;
    [SerializeField] private InputActionReference _back;

    public void Open()  => InputStack.Register(this);   // 이제부터 입력 독점
    public void Close() => InputStack.Unregister(this); // 이전 수신자에게 입력 반환

    public void OnBind(IInputBinder binder)
    {
        binder.Bind(_navigate.action, OnNavigate);
        binder.Bind(_back.action, OnBack);
        // 여기서 바인딩하지 않은 모든 입력은 이 메뉴가 최상단인 동안 차단된다.
    }

    private void OnNavigate(InputAction.CallbackContext ctx) { /* ... */ }
    private void OnBack(InputAction.CallbackContext ctx) => Close();
}
```

## API 요약
| 멤버 | 설명 |
| --- | --- |
| `InputStack.Register(receiver)` | 최상단에 등록(이미 있으면 최상단으로 이동) |
| `InputStack.Unregister(receiver)` | 특정 수신자 제거(최상단이 아니어도 가능). 제거되면 `true` 반환 |
| `InputStack.Clear()` | 전부 비움 |
| `InputStack.Target` | 현재 활성 대상(최상단). 비어 있으면 `null` |
| `InputStack.Count` | 스택에 등록된 수신자 수 |
| `InputStack.Contains(receiver)` | 해당 수신자가 스택에 있는지 |
| `InputStack.IsTarget(receiver)` | 해당 수신자가 활성 대상(최상단)인지 |
| `InputStack.GetSnapshot()` | 현재 스택 스냅샷(맨 아래 → 최상단 순). 디버그/표시용 |
| `InputStack.TopChanged` | 활성 대상 변경 이벤트 |
| `InputStack.Default` | 내부 기본 `InputStackController` 직접 접근(고급용) |

> 최상단만 제거하려면 `InputStack.Unregister(InputStack.Target)`처럼 현재 대상을 넘긴다.

### started / performed / canceled 모두 바인딩
```csharp
binder.Bind(action, OnStarted, OnPerformed, OnCanceled); // 필요 없는 단계는 null
```

### 액션 활성화를 직접 관리하고 싶다면
기본값은 최상단이 될 때 바인딩된 액션을 자동 `Enable()` 하고(직접 켠 것만) 밀려날 때 `Disable()` 한다.
이 동작을 끄려면 별도 컨트롤러를 직접 만든다.
```csharp
var controller = new InputStackController(manageActionEnabling: false);
controller.Register(receiver); // 콜백 연결/해제만, 액션 enable은 직접 관리
```

## 디버그
플레이 모드에서 **Window > Yang > Input Stack Debug** 창을 열면 현재 스택 구성과 활성 수신자를 볼 수 있다.

## 동작 메모
- 같은 액션을 외부에서 이미 `Enable()` 해 둔 경우, 이 패키지는 그 액션을 끄지 않는다(우리가 켠 것만 끈다).
- `Enter Play Mode Options`로 도메인 리로드를 꺼도 플레이 진입 시 전역 스택이 초기화된다.
