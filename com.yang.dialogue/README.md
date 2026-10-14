# Dialogue

Unity용 노드 기반 대화 시스템. 그래프 에디터로 대화 흐름을 작성하고, 런타임에서 `DialogueRunner`가 노드를 순회하며 `IDialogueView` 콜백으로 UI에 출력합니다. 텍스트는 Unity Localization(`com.unity.localization`)을 사용합니다.

- **Namespace**: `Yang.Dialogue` (런타임), `Yang.Dialogue.Editor` (에디터)
- **Unity**: 2023.1 이상
- **의존성**: `com.unity.localization` 1.5.3

---

## 1. 설치

수정된 로컬 배포본은 `com.yang.dialogue.tgz`로 제공합니다. Unity Package Manager의 **+ → Add package from tarball...**에서 해당 파일을 선택하세요. 기존에 동일한 `com.yang.dialogue`를 Git URL이나 다른 로컬 경로로 설치했다면 먼저 해당 패키지를 제거한 뒤 설치해야 합니다.

ZIP 배포본을 쓰려면 압축 해제한 `com.yang.dialogue` 폴더를 Unity 프로젝트의 `Packages/` 안에 넣어 embedded package로 사용합니다. Git URL 의존성을 계속 사용하면 수정된 로컬 코드가 아니라 Git 저장소의 코드가 로드될 수 있습니다.

**설치 확인:** `Tools → Dialogue` 창에서 우측 상단 `Refresh`를 누르면 Console에 `Dialogue Refresh clicked: source=...`가 나타나고 버튼 텍스트가 `Updated n / Missing n`으로 바뀝니다. `source`가 현재 설치한 `com.yang.dialogue` 경로인지 확인하세요. 이후 `Dialogue localization refresh: nodes=..., references=..., updated=..., unresolved=...`가 출력됩니다. 이 로그가 없거나 버튼 표시가 바뀌지 않으면 Unity가 이전 에디터 코드를 실행 중이거나 Console의 Log 필터가 꺼진 상태일 수 있습니다.

Localization 의존성은 `com.unity.localization` 1.5.3이며, 런타임 어셈블리 참조가 필요하면 `Dialogue.Runtime`을 추가합니다.

---

## 2. 핵심 구성요소

| 타입 | 역할 |
| --- | --- |
| `DialogueSO` | 대화 그래프를 담는 ScriptableObject 에셋 |
| `DialogueRunner` | 그래프를 실행하는 MonoBehaviour. 시작/일시정지/저장/트리거 등을 제어 |
| `IDialogueView` / `DialogueViewBase` | 노드 출력(대사·선택지 등)을 받는 UI 측 인터페이스 |
| Attribute 스키마 | Command, Event, Trigger/Condition 입력 UI와 타입 정의 |

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
| Command | 문자열 ID와 타입화된 인자를 View에 전달 | `OnCommand` |

---

## 3. 사용 흐름

### 3-1. 그래프 작성

1. 상단 메뉴 **Tools ▸ Dialogue** 로 그래프 에디터를 엽니다.
2. **Project ▸ Create ▸ Dialogue/Node** 로 `DialogueSO` 에셋을 만듭니다.
3. 에디터에서 노드를 배치하고 포트를 연결합니다.
4. Dialogue/Choice 노드의 화자·대사는 Localization String Table의 키로 지정합니다. (`DialogueSO`의 `SpeakerTable`, `TextTable`)

**에디터 편의 기능**

- **연결 표시 막대** — 포트 끝의 막대로 연결 여부를 표시합니다. *꽉 찬 막대* = 연결됨, *속이 빈 막대* = 미연결 (색은 초록/회색, 색 없이 모양만으로도 구분됩니다).
- **연결 노드로 이동** — 포트의 막대를 더블클릭하면 연결된 노드로 화면이 이동합니다. 입력 포트에 여러 개가 연결돼 있으면 더블클릭할 때마다 순환합니다.
- **ID 검색** — 우측 상단(언어 선택 아래) 검색창에 노드 ID 일부를 입력하면 자동완성 목록에서 골라 해당 노드로 이동할 수 있습니다.

### 3-2. Event와 Variable 스키마

Event는 Command와 마찬가지로 ID와 인자를 선언합니다.

```csharp
[DialogueEvent(OpenDoorEvent.ID, Menu = "Door/Open")]
public class OpenDoorEvent : IDialogueInstruction
{
    public const string ID = "door.open";
    public const int DoorIdField = 10;
    public const int AnimatedField = 20;

    [DialogueArgument(DoorIdField, "Door ID")]
    public string doorId;

    [DialogueArgument(AnimatedField, "Animated")]
    public bool animated = true;

    public void ReadFrom(RunnerCommand command)
    {
        doorId = command.GetString(DoorIdField);
        animated = command.GetBool(AnimatedField, true);
    }
}
```

Trigger, Condition, Choice가 공유하는 변수는 하나의 스키마에 선언합니다.
현재 지원 변수 타입은 `float`, `bool`입니다.
각 변수의 양수 `FieldId`는 저장 식별자와 표시 순서를 겸하며 전체 Variable 스키마에서 고유해야 합니다.

```csharp
[DialogueVariableSchema]
public class GameDialogueVariables
{
    public const int AffectionField = 10;
    public const int HasKeyField = 20;

    [DialogueVariable(AffectionField, "Affection")]
    public float affection;

    [DialogueVariable(HasKeyField, "Has Key")]
    public bool hasKey;
}
```

### 3-3. Command 스키마

Command 노드의 ID와 인자를 직접 입력하지 않으려면 게임 코드에 스키마 클래스를 선언합니다.
공개 인스턴스 필드는 에디터에서 타입에 맞는 입력 필드로 자동 표시되며, 실제 그래프에는
Command ID와 직렬화 가능한 값만 저장됩니다.

```csharp
[DialogueCommand(MoveCharacterCommand.ID, Menu = "Character/Move")]
public class MoveCharacterCommand : IDialogueInstruction
{
    public const string ID = "character.move";
    public const int TargetField = 10;
    public const int PositionXField = 20;
    public const int PositionYField = 30;
    public const int DurationField = 40;

    [DialogueArgument(TargetField, "Target")]
    [DialogueOptions(typeof(CharacterOptions), nameof(CharacterOptions.GetIDs))]
    public string target;

    [DialogueArgument(PositionXField, "Position X")]
    public float x;

    [DialogueArgument(PositionYField, "Position Y")]
    public float y;

    [DialogueArgument(DurationField, "Duration")]
    public float duration = 0.5f;

    public void ReadFrom(RunnerCommand command)
    {
        target = command.GetString(TargetField);
        x = command.GetFloat(PositionXField);
        y = command.GetFloat(PositionYField);
        duration = command.GetFloat(DurationField, 0.5f);
    }
}

public static class CharacterOptions
{
    public static IReadOnlyList<string> GetIDs()
        => new[] { "alice", "bob", "shopkeeper" };
}
```

지원 필드 타입은 `string`, `int`, `float`, `long`, `bool`, `Color32`, `Guid`, `enum`입니다.
`DialogueOptions`의 공급 메서드는 매개변수가 없는 static 메서드여야 하며
`IEnumerable<string>`을 반환해야 합니다.
스키마는 `IDialogueInstruction`을 구현하고 `ReadFrom`에서 필요한 인자를 직접 읽습니다.
이 변환 과정은 런타임 리플렉션을 사용하지 않습니다.
`DialogueArgument`의 양수 `FieldId`는 저장 식별자와 표시 순서를 겸하며 스키마 안에서 고유해야 합니다.
변수명은 자유롭게 바꿀 수 있지만 한번 사용한 `FieldId`는 변경하거나 재사용하지 않는 것을 권장합니다.
조건부 입력은 기준 필드의 ID를 사용합니다. 예: `[DialogueShowIf(AnimatedField, true)]`.

필드에 전용 Attribute가 있으면 기본 입력 필드보다 먼저 적용되는 커스텀 Drawer를 등록할 수 있습니다.
Attribute는 런타임 코드에, Drawer는 Editor 어셈블리에 선언합니다.

```csharp
using System;

[AttributeUsage(AttributeTargets.Field)]
public sealed class DialogueRangeAttribute : Attribute
{
    public float Min { get; }
    public float Max { get; }

    public DialogueRangeAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
}
```

```csharp
using System;
using System.Reflection;
using UnityEngine.UIElements;
using Yang.Dialogue;
using Yang.Dialogue.Editor;

[DialogueArgumentDrawer(typeof(DialogueRangeAttribute))]
public sealed class DialogueRangeDrawer : DialogueArgumentDrawer
{
    public override VisualElement CreateField(
        FieldInfo field,
        Attribute attribute,
        string label,
        GenericData value,
        Action<GenericData> onChanged)
    {
        DialogueRangeAttribute range = (DialogueRangeAttribute)attribute;
        Slider slider = new(label, range.Min, range.Max) { value = value.GetFloat() };

        slider.RegisterValueChangedCallback(evt => onChanged(new GenericData(evt.newValue)));

        return slider;
    }
}
```

```csharp
[DialogueRange(0f, 10f)]
public float duration = 0.5f;
```

### 3-4. View 구현

`DialogueViewBase`(MonoBehaviour)를 상속해 필요한 콜백만 override 합니다. 각 콜백은 Unity `Awaitable`을 반환하므로, 타이핑 연출·버튼 입력 대기 등을 `await` 로 처리하면 그동안 러너가 다음 노드로 진행하지 않고 기다립니다.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Yang.Dialogue;

public class SampleDialogueView : DialogueViewBase
{
    public override async Awaitable OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token)
    {
        // TableReference는 GUID, TableEntryReference는 long ID를 우선 사용
        string speakerName = await Localize(speaker);
        string body        = await Localize(text);

        // UI 출력 + 클릭 대기 등...
    }

    public override async Awaitable<int> OnChoice(RunnerText speaker, RunnerChoiceCollection texts, string message, IRunnerToken token)
    {
        // texts[i].isValid 가 false 면 조건 미충족 선택지
        // 사용자가 고른 인덱스를 반환 (선택 안 하면 -1)
        return 0;
    }

    public override async Awaitable OnCommand(IReadOnlyList<RunnerCommand> commands, IRunnerToken token)
    {
        foreach (RunnerCommand command in commands)
        {
            if (command.TryConvert(MoveCharacterCommand.ID, out MoveCharacterCommand move))
            {
                // 게임 측 캐릭터 시스템에 명령 전달
                MoveCharacter(move.target, move.x, move.y, move.duration);
            }
        }

    }

    public override async Awaitable OnMessage(string reason, IRunnerToken token) { }   // Wait 신호 등 알림

    private async Awaitable<string> Localize(RunnerText t)
    {
        return await LocalizationSettings.StringDatabase.GetLocalizedStringAsync(t.TableReference, t.TableEntryReference).Task;
    }
}
```

**로컬라이제이션 이름 변경:** Dialogue/Choice 노드는 테이블명·GUID와 엔트리 키·long ID를 함께 저장합니다. GraphView 새로고침은 GUID와 ID가 유효하면 현재 이름·키로 저장 문자열을 동기화하고, 식별자가 유효하지 않으면 저장된 이름·키로 다시 조회해 GUID·ID를 복원합니다. 둘 다 찾을 수 없을 때는 저장된 참조값을 임의로 삭제하지 않습니다. CSV의 `SpeakerTableGuid`, `TextTableGuid`, `SpeakerEntryId`, `TextEntryId`에도 동일한 우선순위를 적용하며, 과거 CSV의 이름 기반 조회도 유지합니다. 기존 View에서 문자열 `table`/`entry`를 직접 사용하는 코드는 위 예제처럼 `TableReference`/`TableEntryReference`를 사용해야 이름 변경에 안전합니다. GraphView의 **Refresh** 버튼을 누르면 Console에 검사한 노드 수(`nodes`), 참조 수(`references`), 실제 보정 횟수(`updated`), 찾을 수 없는 참조 수(`unresolved`)가 출력되며, 미해결 참조는 노드 GUID와 저장된 키/ID를 함께 경고로 표시합니다.

### 3-5. 러너 설정 & 실행

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
Awaitable StartDialogueAsync(string key, string nodeName = "", IReadOnlyList<IDialogueView> views = null);
bool IsRunning(string key);
void StopDialogue(string key);
void PauseDialogue(string key);     // 특정 흐름 일시정지
void StopAllDialogue();
void PauseAllDialogue();
void JumpNode(string key, string nodeName);   // 진행 중 흐름을 다른 노드로 점프
Awaitable JumpNodeAsync(string key, string nodeName);
void SetDialogue(DialogueSO so);    // 다른 그래프로 교체 (진행 중이면 무시)
```

- `key`별로 독립적인 흐름이 유지됩니다. 같은 `key`가 이미 진행 중이면 중복 시작은 무시됩니다.
- `views`를 넘기면 그 대화 흐름의 View 목록으로 사용합니다. 미지정 시 기존 흐름의 View를 유지하며, 새 흐름은 인스펙터의 `viewBases`와 등록된 View를 사용합니다.

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
void  SetValue(int fieldId, float value);
void  SetValue(int fieldId, bool value);
float GetFloatValue(int fieldId);
bool  GetBoolValue(int fieldId);
bool  ContainsKey(int fieldId);
bool  RemoveValue(int fieldId);
void  ClearTriggerValues();

// 값 변경 콜백
void TriggerRegisterCallback(System.Action<int> callback);             // 모든 FieldId
void TriggerRegisterCallback(int fieldId, System.Action callback);     // 특정 FieldId
void TriggerUnregisterCallback(...);   // 위 두 형태의 해제
void ClearTriggerCallbacks();
```

### Event

Event 노드가 실행될 때 호출할 콜백을 ID로 등록합니다.

```csharp
void EventRegisterCallback(string id, System.Action<RunnerCommand> callback);
void EventUnregisterCallback(string id, System.Action<RunnerCommand> callback);
void ClearEventCallbacks();
```

```csharp
runner.EventRegisterCallback(OpenDoorEvent.ID, data => door.Open(data.GetString(OpenDoorEvent.DoorIdField)));
runner.SetValue(GameDialogueVariables.AffectionField, 100f);
runner.SetValue(GameDialogueVariables.HasKeyField, true);
```

### 저장 / 불러오기

진행 중인 흐름 위치(노드), 트리거 값, 대화별 View 데이터를 직렬화 가능한 `DialogueSaveData`로 저장/복원합니다. 저장된 대화는 `Load()` 이후 `StartDialogue()`로 재개하며, 실행 중인 노드의 처음부터 다시 시작합니다.

```csharp
DialogueSaveData data = runner.Save();
string json = JsonUtility.ToJson(data);

runner.Load(JsonUtility.FromJson<DialogueSaveData>(json));
foreach (DialogueFlowData flow in data.dialogueFlows) runner.StartDialogue(flow.key);

// 같은 View에서 대화별로 저장한 데이터를 구분할 때
MyViewState state = runner.Get<MyViewState>("main", myView);
```

`Load()`는 기존 대화를 중단하고 저장된 트리거와 대화 위치를 복원하지만 자동으로 재생하지는 않습니다. 사용자 지정 View 목록을 복원하려면 해당 View를 `AddView()`로 등록한 뒤 `Load()`를 호출해야 합니다. `Get<TData>(view)`는 첫 일치 항목을 반환하는 기존 API이며, 여러 대화에서 같은 View를 사용하면 `Get<TData>(key, view)`를 사용하세요.

---

## 5. 데이터 타입 참고

- **`RunnerText`** — `TableReference`(GUID 우선), `TableEntryReference`(long ID 우선)를 통해 안정적으로 로컬라이즈합니다. 기존 `table`/`entry` 문자열은 호환용으로 유지합니다.
- **`RunnerChoiceText`** — 선택지. `portIndex`, `TableReference`, `TableEntryReference`, `isValid`, `Conditions`를 제공합니다. 문자열 키 필드는 호환용입니다.
- **`RunnerCondition`** — 선택지 조건 한 건. `fieldId`, `isValid`, `type`(Float/Bool), `checkType`, `GetFloatValue()`/`GetBoolValue()`.
- **`IRunnerToken`** — 진행 중 흐름 핸들. `State`, `Views`, `CancellationToken`, `Delay(seconds)`를 제공합니다. View 콜백에서 상태를 확인하고 흐름 중단과 연동되는 대기를 만들 때 사용합니다.

외부 UI 입력이나 이벤트를 기다릴 때는 `WaitFor`를 사용하면 Pause/Stop/End 시 자동으로 취소됩니다.

```csharp
AwaitableCompletionSource<string> scanSource = new();

RunnerWaitResult<string> waitResult = await token.WaitFor(scanSource);

switch (waitResult.Status)
{
    case RunnerWaitStatus.Completed:
        string result = waitResult.Value;
        break;

    case RunnerWaitStatus.TokenCanceled:
        // 대화가 Pause/Stop/End됨
        break;

    case RunnerWaitStatus.SourceCanceled:
        // scanSource 자체가 취소됨
        break;
}

// 외부 입력 완료 시
scanSource.TrySetResult("result");
```

---

## 6. CSV 내보내기 / 가져오기 (Localization)

`DialogueSO`의 텍스트를 번역용 CSV로 주고받을 수 있습니다 (`Yang.Dialogue.Editor`의 `DialogueCsvExporter` / `DialogueCsvImporter`).

- 컬럼: `ID, Type, Next, Message, Data` + 로케일별 `Speaker[code]` / `Text[code]`
- Choice 옵션, Condition 분기, Command/Event 명령은 소유 노드 아래 하위 행으로 표현됩니다.
- Command/Event 인자는 `fieldId:type=value` 형식으로 왕복합니다. 지원 타입은 `string`, `int`, `float`, `long`, `bool`, `color`, `guid`, `enum`이며, 색상은 `#RRGGBBAA` 형식으로 저장됩니다.
- 가져올 때 노드 ID가 **비어 있거나 중복**되면 노드마다 확인창이 떠서 *새 ID 생성 / 건너뛰기 / 모두 생성* 을 고를 수 있습니다.
- 가리키는 노드가 없거나 건너뛴 링크는 가져오기 후 경고 메시지로 모아 알려줍니다.

---

## 라이선스

`LICENSE.txt` 참고. Author: Yang Jaewan.
