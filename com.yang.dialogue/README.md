# Dialogue

Unity에서 대화 흐름을 노드로 작성하고 실행하는 패키지입니다. 대사·선택지 UI는 사용자가 구현하며, 텍스트는 **Unity Localization**과 연동됩니다.

**요구 사항:** Unity 2023.1 이상, `com.unity.localization` 1.5.3

## 설치

패키지 폴더(`com.yang.dialogue`)를 Unity 프로젝트의 `Packages/`에 넣어 사용합니다. 같은 패키지가 다른 경로에서 중복 설치되지 않았는지 확인하세요. 런타임 코드를 별도 어셈블리에서 사용한다면 `Dialogue.Runtime`을 참조합니다.

## 대화 그래프 만들기

1. **Project → Create → Dialogue/Node**에서 `DialogueSO`를 생성합니다.
2. **Tools → Dialogue**를 열고 에셋을 선택합니다.
3. 노드를 추가하고 출력 포트를 다음 노드에 연결합니다.
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

`DialogueSO`의 Inspector에서 **Export CSV** 또는 **Import CSV**를 사용합니다. CSV에는 노드·연결 정보와 로케일별 텍스트가 포함됩니다. 가져오기는 **기존 노드와 링크를 교체**하며, 연결된 String Table의 엔트리도 갱신할 수 있으므로 필요한 에셋을 먼저 백업하세요.

- 가져오기 전 노드·링크·텍스트 수를 미리 볼 수 있습니다.
- 누락된 **노드 ID, Localization 테이블, 엔트리**는 생성을 묻습니다. 거절하면 해당 노드 또는 Localization 데이터만 건너뜁니다.
- **잘못된 타입·ID·연결·조건식·명령 인자** 등이 있으면 CSV 행 번호와 노드 정보를 표시하고 정상 값을 입력받습니다. 해당 행이나 연결을 건너뛰거나 전체 가져오기를 취소할 수도 있습니다.
- 가져오기 작업은 입력 검증과 생성 여부 선택을 마친 뒤 적용됩니다. 누락된 Localization 참조를 건너뛰면 그 참조는 빈 값으로 유지됩니다.
- `Choice`의 Option, `Condition`의 Branch, `Command/Event`의 Instruction은 부모 노드 바로 아래의 행으로 표현됩니다.

## 플레이 모드 디버깅

**Tools → Dialogue**의 Runner 디버그 영역에서 실행 중인 Flow를 확인하거나 선택한 노드로 이동할 수 있습니다. 그래프를 플레이 중 수정한 경우 **Apply graph changes to Runner**로 다시 적용할 수 있습니다. 기존 실행은 일시정지되며 자동으로 재시작하지 않습니다.

## 라이선스

`LICENSE.txt`를 참고하세요.
