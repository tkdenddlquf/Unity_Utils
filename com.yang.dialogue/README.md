# Dialogue

Unity에서 대화 흐름을 노드로 작성하고 실행하는 패키지입니다. 대사·선택지 UI는 사용자가 구현하며, 텍스트는 **Unity Localization**과 연동됩니다.

**요구 사항:** Unity 2023.1 이상, `com.unity.localization` 1.5.3

## 설치

패키지 폴더(`com.yang.dialogue`)를 Unity 프로젝트의 `Packages/`에 넣어 사용합니다. 같은 패키지가 다른 경로에서 중복 설치되지 않았는지 확인하세요. 런타임 코드를 별도 어셈블리에서 사용한다면 `Dialogue.Runtime`을 참조합니다.

## 대화 그래프 만들기

1. **Project → Create → Dialogue/Node**에서 `DialogueSO`를 생성합니다.
2. **Tools → Dialogue**를 열고 에셋을 선택합니다.
3. 노드를 추가하고 출력 포트를 다음 노드에 연결합니다. 출력 포트에서 엣지를 빈 공간으로 끌어 놓으면 검색창에서 새 노드를 생성하거나 기존 노드를 찾아 연결할 수 있습니다. 기존 노드 목록에는 타입·GUID·대표 노드 내용이 표시되며 화면 밖에 있는 노드도 표시합니다.
4. 필요하면 `DialogueSO`의 **Speaker Table / Text Table**을 지정하고, 노드에서 Localization 키를 선택합니다.
5. 게임 오브젝트에 `DialogueRunner`를 붙이고 `DialogueSO`와 View 컴포넌트를 연결합니다.

| 노드 | 역할 |
| --- | --- |
| Start | 시작점 |
| Dialogue | 화자와 대사 표시 |
| Choice | 조건에 따른 선택지와 분기 |
| Condition | 변수 값으로 분기 |
| Trigger | 변수 값 설정·증감 |
| Event | 등록된 게임 이벤트 호출 |
| Wait | 시간 또는 View 신호 대기 |
| Command | View에 명령과 인자 전달 |

그래프의 Ctrl+C/V와 우클릭 Copy/Paste는 같은 시스템 클립보드를 사용합니다. Dialogue 노드 데이터가 아닌 텍스트나 JSON은 붙여넣을 수 없습니다.

### 실행 및 저장

```csharp
using Yang.Dialogue;

// 연결된 DialogueRunner에서 실행
runner.StartDialogue("main");
runner.PauseDialogue("main");
runner.StartDialogue("main"); // 현재 위치에서 재개

DialogueSaveData saved = runner.Save();
runner.Load(saved);
runner.StartDialogue("main");

runner.StopDialogue("main");
```

저장된 흐름의 노드가 그래프에서 삭제된 경우 시작 노드로 복구하며 경고를 출력합니다. 저장 데이터에는 흐름 위치, Trigger 값, View의 저장 정보가 포함됩니다. 실제 파일 저장과 불러오기는 게임 측에서 처리해야 합니다.

### View 구현

`DialogueViewBase`를 상속해 필요한 콜백을 구현합니다. 같은 Runner에 여러 View를 연결할 수 있습니다.

- `OnDialogue`: 대사 표시
- `OnChoice`: 선택 UI를 표시하고 선택된 항목의 **인덱스** 반환
- `OnCommand`: 명령 전달
- `OnMessage`: 외부 입력 또는 신호 대기
- `OnPaused`, `OnStopped`, `OnEnded`: 흐름 종료 및 화면 정리
- `CaptureView`: 저장할 View 상태 반환

**여러 View의 선택지:** `OnChoice`는 모든 View에 동시에 요청됩니다. **가장 먼저 유효한 선택 인덱스를 반환한 View의 결과가 확정**됩니다. 나머지 선택 대기는 취소되므로, View는 `IRunnerToken.CancellationToken` 또는 `token.WaitFor(...)`를 사용해 취소 신호에 반응해야 합니다. 유효하지 않은 인덱스나 조건을 만족하지 않는 선택은 채택되지 않습니다.

```csharp
AwaitableCompletionSource<int> selection = new();
RunnerWaitResult<int> result = await token.WaitFor(selection);

if (result.IsCompleted) return result.Value;

return -1;
```

View는 UI에서 항목을 클릭했을 때 `selection.TrySetResult(선택인덱스);`를 호출하고, 취소 시 선택 UI를 정리해야 합니다.

**오류 확인:** `StartDialogue()`로 실행한 View의 비동기 콜백에서 예외가 발생하면 Unity Console에 흐름 키·노드 GUID·View ID·콜백 이름과 원래 예외가 표시됩니다. `StartDialogueAsync()`는 호출자에게 예외를 전달하므로 호출하는 쪽에서 `await`와 예외 처리를 해야 합니다. 정상적인 중지·일시정지에 따른 취소는 오류로 취급하지 않습니다. View 내부에서 시작한 다른 비동기 작업도 `await`해야 예외가 Runner로 전달됩니다.

`OnChoice`는 선택 확정 전에 하나의 View라도 예외를 발생시키면 선택 대기를 취소하고 대화 실행을 실패시킵니다. 이미 다른 View의 선택이 확정된 뒤 발생한 예외는 해당 선택 결과를 되돌리지 않고 Unity Console에 기록합니다.

### 변수 및 명령

Condition, Choice, Trigger에서 사용할 변수를 스키마로 선언합니다. 지원 타입은 `float`, `bool`이며, 필드 ID는 양수이면서 스키마 전체에서 고유해야 합니다.

```csharp
using Yang.Dialogue;

[DialogueVariableSchema]
public class GameDialogueVariables
{
    [DialogueVariable(10, "Affection")]
    public float affection;

    [DialogueVariable(20, "Has Key")]
    public bool hasKey;
}

[DialogueCommand("character.move", Menu = "Character/Move")]
public class MoveCharacterCommand
{
    public string target;
    public float speed;
}

[DialogueEvent("door.open", Menu = "Door/Open")]
public class OpenDoorEvent
{
    public string doorId;
}
```

Command / Event의 공개 필드는 Source Generator가 인자를 변환하고 런타임 팩토리를 등록합니다. ID는 다른 어셈블리를 포함하여 각 Command 또는 Event 종류 안에서 중복되면 안 됩니다. 지원 필드는 `string`, `int`, `float`, `long`, `bool`, `Color32`, `Guid`, `int` 기반 `enum`입니다.

## CSV 내보내기 / 가져오기

`DialogueSO`의 Inspector에서 **Export CSV** 또는 **Import CSV**를 사용합니다. CSV에는 노드·연결 정보와 로케일별 텍스트가 포함됩니다. Import는 **기존 노드와 링크를 교체**하고 참조 중인 String Table을 갱신할 수 있습니다.

Import를 선택하면 **Dialogue CSV Import** 검토 창이 열립니다.

1. CSV 전체를 분석한 뒤 문제 개수, 실제 파일 줄 번호, 컬럼, 원인과 수정 방법을 표시합니다. 여러 줄로 된 텍스트가 있어도 파일의 실제 줄 번호를 사용합니다.
2. 문제를 선택해 값을 수정합니다. 잘못된 노드 ID는 새 GUID를 만들 수 있고, 없는 연결 대상은 노드 검색으로 선택하거나 연결을 해제할 수 있습니다. 잘못된 타입은 선택 목록에서, 테이블 이름은 기존 String Table 목록에서 수정할 수 있습니다. 번역 충돌은 다른 행의 값으로 맞추거나 직접 수정합니다.
3. 수정 후 검증이 다시 실행됩니다. 누락된 테이블과 엔트리는 **Create missing tables / Create missing entries** 옵션으로 명시적으로 생성할 수 있습니다. 새 테이블을 만들 때는 프로젝트의 `Assets` 하위 폴더를 지정합니다. `Use asset default tables`와 `Keep existing Start if missing`도 선택할 수 있습니다.
4. 변경 요약에서 노드 수, 생성될 테이블·엔트리, 키 이름 변경과 번역 갱신 예상 건수를 확인합니다. 오류가 모두 해결된 경우에만 **Replace graph and import**가 활성화됩니다.
5. **Save corrected CSV...**로 수정된 데이터를 CSV 파일에 저장할 수 있습니다. 저장하지 않아도 검토 창의 수정 내용은 이번 Import에 적용됩니다. 원본 CSV 파일을 자동 덮어쓰지 않습니다.

검토 중에는 그래프와 Localization 에셋을 수정하지 않습니다. 적용 단계에서 다시 검증한 뒤 변경을 하나의 Undo 그룹으로 기록하고, 실패 시 변경을 되돌립니다. 잘못된 데이터나 누락된 참조를 조용히 건너뛰지 않습니다.

`SpeakerEntryId` / `TextEntryId`가 기존 엔트리와 일치하면 키 이름을 변경해도 ID와 번역 연결을 유지합니다. 다른 ID의 키 이름을 가로채거나 같은 엔트리에 충돌하는 번역을 설정하면 오류로 표시합니다. Entry ID가 없는 키의 이름 변경 여부는 추측하지 않습니다.

`Choice`의 Option, `Condition`의 Branch, `Command/Event`의 Instruction은 부모 노드 바로 아래의 CSV 행으로 표현됩니다.

## 플레이 모드 디버깅

**Tools → Dialogue**의 Runner 디버그 영역에서 실행 중인 Flow를 확인하거나 선택한 노드로 이동할 수 있습니다. 그래프를 플레이 중 수정한 경우 **Apply graph changes to Runner**로 다시 적용할 수 있습니다. 기존 실행은 일시정지되며 자동으로 재시작하지 않습니다.

## 라이선스

`LICENSE.txt`를 참고하세요.
