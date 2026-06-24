# Dialogue

Unity용 노드 기반 대화 시스템. 그래프 에디터로 대화 흐름을 작성하고, 런타임에서 `DialogueRunner`가 노드를 순회하며 `IDialogueView` 콜백으로 UI에 출력합니다. 텍스트는 Unity Localization(`com.unity.localization`)을 사용합니다.

- **Namespace**: `Yang.Dialogue` (런타임), `Yang.Dialogue.Editor` (에디터)
- **Unity**: 2021.3 이상
- **의존성**: `com.unity.localization` 1.5.3

---

## 1. 설치

Package Manager의 **Add package from git URL** 로 설치합니다.

1. Unity 상단 메뉴 **Window ▸ Package Manager** 를 엽니다.
2. 좌측 상단 **+** 버튼 ▸ **Add package from git URL...** 를 선택합니다.
3. 아래 URL을 입력하고 **Add** 를 누릅니다.

```
https://github.com/tkdenddlquf/Unity_Utils.git?path=/com.yang.dialogue
```

또는 `Packages/manifest.json` 의 `dependencies` 에 직접 추가해도 됩니다.

```json
"dependencies": {
  "com.yang.dialogue": "https://github.com/tkdenddlquf/Unity_Utils.git?path=/com.yang.dialogue"
}
```

의존성인 Localization(`com.unity.localization`)은 보통 자동으로 함께 설치되지만, 없다면 Package Manager에서 먼저 설치하세요.

런타임 코드에서 어셈블리 참조가 필요하면 `Dialogue.asmdef`(런타임) 를 참조에 추가합니다.

---

## 2. 핵심 구성요소

| 타입 | 역할 |
| --- | --- |
| `DialogueSO` | 대화 그래프를 담는 ScriptableObject 에셋 |
| `DialogueRunner` | 그래프를 실행하는 MonoBehaviour. 시작/일시정지/저장/트리거 등을 제어 |
| `IDialogueView` / `DialogueViewBase` | 노드 출력(대사·선택지 등)을 받는 UI 측 인터페이스 |
| `IEventMarker` / `IConditionMarker` | 에디터에서 이벤트 ID·조건 키 목록을 제공하는 마커 클래스 |

### 노드 타입 (`NodeType`)

| 노드 | 동작 | View 콜백 |
| --- | --- | --- |
| Start | 그래프 시작점 | — |
| Dialogue | 화자/대사 출력 | `OnDialogue` |
| Choice | 선택지 출력, 선택한 포트로 분기 | `OnChoice` |
| Condition | 트리거 값을 검사해 분기 | — |
| Trigger | 트리거 값을 설정/증감 | — |
| Event | 등록된 이벤트 콜백 실행 | — |
| Wait | 초 단위 대기 또는 외부 신호 대기 | (신호 대기 시) `OnMessage` |
| Object | UnityEngine.Object 참조 전달 | `OnObject` |

---

## 3. 사용 흐름

### 3-1. 그래프 작성

1. 상단 메뉴 **Tools ▸ Dialogue** 로 그래프 에디터를 엽니다.
2. **Project ▸ Create ▸ Dialogue/Node** 로 `DialogueSO` 에셋을 만듭니다.
3. 에디터에서 노드를 배치하고 포트를 연결합니다.
4. Dialogue/Choice 노드의 화자·대사는 Localization String Table의 키로 지정합니다. (`DialogueSO`의 `SpeakerTable`, `TextTable`)

### 3-2. 마커 클래스 (선택)

Event 노드의 이벤트 ID와 Condition/Choice의 조건 키를 에디터 드롭다운에 노출하려면 마커 인터페이스를 구현한 클래스를 만들고, 상수(`const string`)로 값을 정의한 뒤 `DialogueSO`에 지정합니다.

```csharp
using Yang.Dialogue;

public class Events_Chapter_01 : IEventMarker
{
    public const string OpenDoor = "OpenDoor";
    public const string PlaySfx  = "PlaySfx";
}

public class Conditions_Chapter_01 : IConditionMarker
{
    public const string Gold     = "Gold";
    public const string HasKey   = "HasKey";
}
```

### 3-3. View 구현

`DialogueViewBase`(MonoBehaviour)를 상속해 필요한 콜백만 override 합니다. 각 콜백은 `Task`를 반환하므로, 타이핑 연출·버튼 입력 대기 등을 `await` 로 처리하면 그동안 러너가 다음 노드로 진행하지 않고 기다립니다.

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using Yang.Dialogue;

public class SampleDialogueView : DialogueViewBase
{
    public override async Task OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token)
    {
        // RunnerText.table / RunnerText.entry 로 Localization 키를 받음
        string speakerName = await Localize(speaker);
        string body        = await Localize(text);

        // UI 출력 + 클릭 대기 등...
    }

    public override Task<int> OnChoice(RunnerText speaker, IReadOnlyList<RunnerChoiceText> texts, string message, IRunnerToken token)
    {
        // texts[i].isValid 가 false 면 조건 미충족 선택지
        // 사용자가 고른 인덱스를 반환 (선택 안 하면 -1)
        return Task.FromResult(0);
    }

    public override Task OnObject(IReadOnlyList<Object> target, IRunnerToken token) => Task.CompletedTask;   // Object 노드가 넘긴 에셋 처리

    public override Task OnMessage(string reason, IRunnerToken token) => Task.CompletedTask;   // 대화 종료/Wait 신호 등 알림

    private async Task<string> Localize(RunnerText t)
    {
        var table = await LocalizationSettings.StringDatabase.GetTableAsync(t.table).Task;

        return table.GetEntry(t.entry)?.GetLocalizedString() ?? t.entry;
    }
}
```

### 3-4. 러너 설정 & 실행

1. 씬의 GameObject에 `DialogueRunner` 컴포넌트를 추가합니다.
2. 인스펙터에서 `so`(DialogueSO)와 `viewBases`(위에서 만든 View들)를 할당합니다.
3. 코드에서 `StartDialogue`로 대화를 시작합니다.

```csharp
[SerializeField] private DialogueRunner runner;

void Begin()
{
    // key 는 동시에 흐를 수 있는 대화 흐름의 식별자
    runner.StartDialogue("main");

    // 특정 노드 이름에서 시작하려면:
    // runner.StartDialogue("main", "SomeNodeGuid");
}
```

---

## 4. DialogueRunner API

### 흐름 제어

```csharp
void StartDialogue(string key, string nodeName = "", IReadOnlyList<IDialogueView> views = null);
bool IsStarted(string key);
void PauseDialogue(string key);     // 특정 흐름 일시정지
void StopAllDialogue();             // 모든 흐름 정지 + 초기화
void JumpNode(string key, string nodeName);   // 진행 중 흐름을 다른 노드로 점프
void SetDialogue(DialogueSO so);    // 다른 그래프로 교체 (진행 중이면 무시)
```

- `key`별로 독립적인 흐름이 유지됩니다. 같은 `key`가 이미 진행 중이면 중복 시작은 무시됩니다.
- `views`를 넘기면 해당 호출에서만 그 View들로 출력합니다(미지정 시 인스펙터의 `viewBases` 사용).

### View 관리

```csharp
bool AddView(IDialogueView view);
bool RemoveView(IDialogueView view);
void ClearViews();
IReadOnlyList<IDialogueView> Views { get; }
```

### Trigger (변수)

조건 분기와 선택지 조건에 쓰이는 float/bool 값입니다.

```csharp
void  SetValue(string key, float value);
void  SetValue(string key, bool value);
float GetFloatValue(string key);
bool  GetBoolValue(string key);
bool  ContainsKey(string key);
bool  RemoveValue(string key);
void  ClearTriggerValues();

// 값 변경 콜백
void TriggerRegisterCallback(System.Action<string> callback);          // 모든 키
void TriggerRegisterCallback(string key, System.Action callback);      // 특정 키
void TriggerUnregisterCallback(...);   // 위 두 형태의 해제
void ClearTriggerCallbacks();
```

### Event

Event 노드가 실행될 때 호출할 콜백을 ID로 등록합니다.

```csharp
void EventRegisterCallback(string id, System.Action callback);
void EventUnregisterCallback(string id, System.Action callback);
void ClearEventCallbacks();
```

```csharp
runner.EventRegisterCallback(Events_Chapter_01.OpenDoor, () => door.Open());
runner.SetValue(Conditions_Chapter_01.Gold, 100f);
runner.SetValue(Conditions_Chapter_01.HasKey, true);
```

### 저장 / 불러오기

진행 중인 흐름 위치(노드)와 모든 트리거 값을 직렬화 가능한 `DialogueWrapper`로 저장/복원합니다.

```csharp
// 저장
DialogueWrapper data = runner.Save();      // JsonUtility 등으로 직렬화 가능

// 복원 (한 번에) — 기존 흐름 정리 + 트리거 복원 + 저장된 노드부터 재개
runner.LoadAndStart(data);

// 복원 (수동 제어) — 트리거만 즉시 복원하고, 흐름은 직접 재개
runner.StopAllDialogue();
foreach (var flow in runner.Load(data))
    runner.StartDialogue(flow.Key, flow.Value);
```

---

## 5. 데이터 타입 참고

- **`RunnerText`** — `table`, `entry` (Localization String Table 키). 화자/대사 텍스트.
- **`RunnerChoiceText`** — 선택지. `portIndex`(분기 포트), `table`/`entry`, `isValid`(조건 충족 여부), `Conditions`.
- **`RunnerCondition`** — 선택지 조건 한 건. `key`, `isValid`, `type`(Float/Bool), `checkType`, `GetFloatValue()`/`GetBoolValue()`.
- **`IRunnerToken`** — 진행 중 흐름 핸들. `IsStarted`, `Pause()`, `Delay(seconds)`, `OnStopCallback`. View 콜백에서 흐름 상태 확인·제어에 사용.

---

## 6. CSV 내보내기 / 가져오기 (Localization)

`DialogueSO`의 텍스트를 번역용 CSV로 주고받을 수 있습니다 (`Yang.Dialogue.Editor`의 `DialogueCsvExporter` / `DialogueCsvImporter`).

- 컬럼: `ID, Type, Next, Message, Data` + 로케일별 `Speaker[code]` / `Text[code]`
- Choice 옵션, Condition 분기, Object 참조는 소유 노드 아래 하위 행으로 표현됩니다.

---

## 라이선스

`LICENSE.txt` 참고. Author: Yang Jaewan.
