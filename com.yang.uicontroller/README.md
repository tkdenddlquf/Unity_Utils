# UIController

enum 기반으로 화면(Screen)과 팝업(Popup)을 관리하는 가벼운 Unity UI 프레임워크입니다.
복잡한 설정 없이 매니저를 상속하고 enum만 정의하면 바로 쓸 수 있습니다.

> `com.yang.uicontroller` · Unity 2022.2+ · New Input System 필요

## 기능

- **enum 기반 UI 관리** — 문자열이 아닌 enum으로 화면/팝업을 식별해 타입 안전하게 다룹니다.
- **Screen 매니저** — 한 번에 하나만 보이는 메인 화면을 `ChangeScreen`으로 전환합니다.
- **Popup 매니저** — 여러 팝업을 스택으로 쌓고 z-order 포커스·전체 닫기를 처리합니다.
- **데이터 마커** — `IDataMarker` 구조체로 박싱 없이 UI에 데이터를 주고받습니다.
- **자동 싱글톤** — `Instance` 접근만으로 씬에서 찾거나 자동 생성됩니다.
- **포인터 유틸** — New Input System 기반으로 포인터가 UI 위에 있는지 판정합니다(마우스·터치·펜).

## 요구 사항

- Unity 2022.2 이상
- `com.unity.inputsystem` 1.5.1 이상

## 설치

`Packages/manifest.json`에 추가:

```json
"com.yang.uicontroller": "https://github.com/<user>/com.yang.uicontroller.git"
```

또는 로컬 경로로:

```json
"com.yang.uicontroller": "file:../Assets/com.yang.uicontroller"
```

## 사용법

### 1. enum 정의

```csharp
public enum ScreenType { Title, Lobby, Game }
public enum PopupType  { Settings, Shop, Confirm }
```

### 2. 매니저 (상속만 하면 끝)

```csharp
using Yang.UIController;

public class ScreenManager : ScreenManagerBase<ScreenManager, ScreenType> { }
public class PopupManager  : PopupManagerBase<PopupManager, PopupType> { }
```

### 3. UI 컴포넌트

화면/팝업 오브젝트에 `CanvasGroup`을 붙이고 `UIBase`를 상속한 스크립트를 부착합니다.

```csharp
using UnityEngine;
using UnityEngine.UI;
using Yang.UIController;

public class LobbyScreen : UIBase<ScreenType>
{
    [SerializeField] private Text goldText;
    private int gold;

    public override ScreenType UIType => ScreenType.Lobby;

    // 이 UI가 처리할 MarkerID만 등록합니다. switch도 수동 타입 변환도 필요 없습니다.
    protected override void RegisterData()
    {
        Subscribe<GoldData>("gold", g =>
        {
            gold = g.Amount;
            goldText.text = g.Amount.ToString();
        });

        Provide<GoldData>("gold", () => new GoldData { Amount = gold });
    }
}
```

### 4. 호출

```csharp
// 화면 전환
ScreenManager.Instance.ChangeScreen(ScreenType.Lobby);
ScreenManager.Instance.SetData(new GoldData { Amount = 1500 });

// 팝업
PopupManager.Instance.OpenPopup(PopupType.Settings);
PopupManager.Instance.ClosePopup(PopupType.Settings);
PopupManager.Instance.CloseAllPopups();
```

## 데이터 마커

UI에 넘길 데이터를 `struct`로 정의해 박싱 없이 주고받습니다.

```csharp
public struct GoldData : IDataMarker
{
    public string MarkerID => "gold";
    public int Amount;
}
```

- 각 UI는 `RegisterData()`에서 `Subscribe`(수신)·`Provide`(응답)로 관심 있는 `MarkerID`만 등록합니다.
- `SetData`/`GetData`는 베이스가 `MarkerID`로 핸들러를 찾아 자동 dispatch합니다.
- 핸들러는 `Delegate` → `Action<T>`/`Func<T>` 캐스팅으로 호출되어 **박싱이 없습니다.**

## API

### `ScreenManagerBase<TManager, TEnum>`

| 멤버 | 설명 |
|------|------|
| `Instance` | 싱글톤 인스턴스 |
| `CurrentScreen` | 현재 활성 화면 |
| `ChangeScreen(TEnum)` | 화면 전환(이전 화면 자동 비활성화) |
| `SetData` / `GetData` | 특정/현재 화면에 데이터 전달·조회 |

### `PopupManagerBase<TManager, TEnum>`

| 멤버 | 설명 |
|------|------|
| `Instance` | 싱글톤 인스턴스 |
| `IsActive` | 활성 팝업 존재 여부 |
| `OpenPopup` / `ClosePopup` | 팝업 열기 / 닫기 |
| `CloseAllPopups()` | 모든 팝업 닫기 |
| `FocusPopup(TEnum)` | 해당 팝업을 최상단으로 포커스 |
| `IsPopupActive` / `IsPopupFocused` | 활성·포커스 상태 조회 |
| `SetData` / `GetData` | 특정/최상단 팝업에 데이터 전달·조회 |

### `UIBase<TEnum>` (추상)

| 멤버 | 설명 |
|------|------|
| `UIType` | 이 UI를 식별하는 enum 값(구현 필수) |
| `IsActive` | 현재 표시 여부 |
| `SetActive(bool)` | CanvasGroup 기반 표시/숨김 |
| `RegisterData()` | 처리할 MarkerID를 등록하는 지점(override) |
| `Subscribe<TData>(id, handler)` | `SetData` 수신 핸들러 등록 |
| `Provide<TData>(id, provider)` | `GetData` 응답 제공자 등록 |
| `SetData` / `GetData` | 등록된 핸들러로 자동 dispatch(베이스 제공) |

### `UIRayUtility` (static)

| 멤버 | 설명 |
|------|------|
| `IsPointerOverUI()` | 포인터가 임의의 UI 위에 있는지 |
| `IsPointerOverUI(Transform)` | 포인터가 특정 UI(자식 포함) 위에 있는지 |
| `SetFocus(GameObject)` | EventSystem 선택 대상 설정 |

## 참고

- 같은 `UIType`를 한 씬에 중복 배치하지 마세요. enum 값이 식별자이므로 충돌합니다.
- 표시 전환은 GameObject가 아닌 `CanvasGroup`으로 처리됩니다. 비활성 화면도 오브젝트는 살아 있으니 무거운 로직은 `IsActive`로 가드하세요.
- 시작 화면은 enum의 0번 값이 기본입니다. 다른 화면으로 시작하려면 초기화 시 `ChangeScreen`을 호출하세요.
