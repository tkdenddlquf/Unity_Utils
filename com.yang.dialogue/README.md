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

**Localization 참조 복구:** 그래프 에디터의 **↻ Refresh**는 변경된 테이블 이름·엔트리 키를 기존 GUID·ID로 동기화합니다. 자동으로 찾지 못한 참조가 있으면 **Resolve Missing (N)** 버튼이 나타납니다. 버튼을 열어 기존 테이블이나 엔트리로 연결할 수 있습니다. 같은 GUID·Entry ID를 참조하는 노드들은 Key 표시값이나 Speaker/Text 사용 위치가 달라도 하나의 복구 항목으로 묶어 한 번에 변환하며, **Keep unresolved**를 선택한 데이터는 그대로 남습니다. 테이블을 먼저 연결한 뒤 엔트리가 여전히 누락되었다면 같은 창에서 엔트리를 이어서 연결할 수 있습니다. 참조 변경은 Dialogue 에셋에만 적용되고 String Table 자체는 수정하지 않으며, Undo를 지원합니다.

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

**필드 이름 변경:** Command/Event 필드의 저장 ID는 필드 이름으로 계산합니다. 이름이 변경되거나 저장된 값의 타입이 스키마와 맞지 않으면 기존 값을 보존하고 노드에 **Resolve fields...** 버튼을 표시합니다. 매핑 창에서는 이전 ID·타입·저장값과 현재 필드·ID·값을 비교하여 연결 대상을 선택합니다. 같은 타입의 안전한 대상이 하나뿐이면 추천 선택되지만 승인 전에는 데이터를 수정하지 않습니다. 매핑하지 않는 값을 유지하거나 명시적으로 삭제할 수 있으며, 타입이 다른 기존 필드는 기본값으로 초기화할 수 있습니다. 동일 Command/Event ID의 다른 노드에도 적용하려면 해당 옵션을 켭니다. 적용 대상 노드별 변경 전후와 충돌 위치를 미리 확인하고 문제 노드로 이동할 수 있으며, 충돌한 노드만 제외하고 나머지를 적용하려면 별도 옵션을 직접 선택해야 합니다. 중복 대상 선택은 차단하고, 최종 승인 후 Undo 가능한 변경으로 기록합니다. Command/Event 종류 변경으로 인자가 초기화될 때도 사전 확인을 요구합니다.

## Excel / CSV 내보내기 및 가져오기

Excel에서 편집할 때는 `DialogueSO` Inspector의 **Export Excel (.xlsx)** → Excel 편집·저장 → **Import Excel (.xlsx)**를 사용합니다. 모든 셀을 **텍스트 형식**으로 내보내므로 긴 Entry ID, 숫자로 보이는 Key, 날짜처럼 보이는 문자열의 자동 변환을 방지합니다. Excel에서 셀을 수식이나 숫자 형식으로 변경한 경우에는 잘못된 값을 조용히 적용하지 않도록 가져오기를 중단하고 셀 위치를 알립니다. 여러 시트가 있다면 첫 시트가 Dialogue 데이터여야 합니다. Excel에서 검토 중 수정한 내용을 저장할 때도 `.xlsx` 형식을 유지합니다.

외부 파이프라인용 **Export CSV** / **Import CSV**도 그대로 사용할 수 있습니다. CSV는 Excel에서 더블클릭해 열면 값이 자동 변환될 수 있으므로, Excel 편집에는 `.xlsx`를 권장합니다. CSV에는 노드, 연결, 로케일별 번역 및 Localization 식별자가 포함됩니다. CSV는 `SpeakerTableGuid`, `TextTableGuid`, `SpeakerEntryId`, `TextEntryId` 열을 반드시 포함해야 하며, `SpeakerPresent[...]` / `TextPresent[...]` 열은 지원하지 않습니다. **빈 번역 칸은 기존 로케일 엔트리가 있으면 빈 문자열로 덮어쓰고, 엔트리가 없으면 새로 만들지 않습니다.** 새로 생성하지 않는 빈 번역은 최종 변경 내역에서도 제외합니다. `EntryId`는 모든 로케일이 공유하는 식별자이므로, ID가 있어도 특정 언어의 번역 엔트리는 없을 수 있습니다.

**가져온 파일(CSV 또는 XLSX)이 최종 기준**입니다. `SpeakerTableGuid` / `TextTableGuid`로 String Table 컬렉션을, `SpeakerEntryId` / `TextEntryId`로 엔트리를 식별합니다. 같은 GUID·Entry ID라면 기존 Key, 테이블 이름, 번역이 CSV와 달라도 오류가 아닙니다. CSV의 이름 및 번역으로 갱신하되 식별자는 유지합니다. 하나의 엔트리를 여러 노드에서 참조하면 같은 변경을 한 번 적용합니다.

**Dialogue Import** 검토 창에서는 다음처럼 처리합니다.

1. 유효한 변경은 자동 적용 대상으로 분류합니다. 기존 노드 ID에 대한 `Type`, 연결, 메시지, 데이터 변경과 기존 Localization 식별자에 대한 이름·Key·번역 변경은 별도 승인 없이 적용합니다. 같은 테이블 GUID 또는 엔트리 ID에 관한 문제는 한 번만 표시하고, Speaker와 Text 양쪽에서 참조하더라도 동일한 실체라면 관련 CSV 행을 함께 수정합니다. 여러 행이 동일한 존재하지 않는 `Next` ID를 참조하면 하나의 결정 항목으로 묶어 대상 연결이나 연결 해제를 일괄 적용합니다.
2. **사용자 선택이 필요한 경우**에만 미해결 항목이 나타납니다. 새 노드 ID는 생성하거나 아직 사용하지 않은 기존 ID로 재연결하고, 존재하지 않는 테이블 GUID·Entry ID는 새 항목을 생성하거나 기존 항목을 선택해 연결할 수 있습니다. `Next`의 대상 ID가 없으면 다른 노드를 선택하거나 비웁니다. 새 타입에 필요한 값이 없거나 사용하지 않는 필드 값이 남아 있다면 해당 값을 보완·정리합니다. Choice에 Option이 없을 때는 Option 행을 추가할 수 있습니다.
3. 명시적인 `SpeakerTableGuid` / `TextTableGuid` 또는 `SpeakerEntryId` / `TextEntryId`가 없거나 대상이 존재하지 않는 경우에는 이름·Key가 같다는 이유만으로 다른 기존 항목에 자동 연결하지 않습니다. 구형 CSV 형식은 지원하지 않습니다. 새 테이블을 생성하면 Unity가 GUID를 발급하므로 다음 CSV Export에 새 식별자가 기록됩니다. 필요한 로케일 테이블도 선택하여 생성할 수 있습니다.
4. **실제 CSV 충돌이나 형식 오류**는 별도 검증합니다. 중복 노드 ID, 없는 연결 대상, 같은 엔트리 ID에 서로 다른 Key·번역 지정, 서로 다른 엔트리의 Key 충돌, 같은 테이블 GUID에 서로 다른 이름 지정, 필수 데이터 누락, 잘못된 데이터 형식 등이 해당합니다. 이미 존재하는 Key의 정상적인 이름 변경 또는 서로 맞바뀐 Key는 충돌로 취급하지 않습니다.
5. 검토 창 왼쪽에서 **문제 제목, Localization 참조, CSV 줄·필드**를 검색하거나 알림 항목을 숨길 수 있습니다. 같은 참조를 공유하는 문제는 한 항목으로 표시하고 영향받는 행을 접어서 확인합니다. 동일 엔트리의 Key·Entry ID·번역 또는 동일 테이블의 이름이 충돌하면 모든 관련 CSV 행에서 후보를 모아 **값별 사용 건수와 기존 Localization 값**을 보여줍니다. 후보 버튼을 한 번 누르면 해당 참조의 모든 CSV 행을 즉시 통일하고 재검증합니다. 다른 값을 직접 입력할 수도 있으며, **이전/다음 문제**로 이동할 수 있습니다. 기존 노드·String Table·Localization 엔트리 선택은 검색 목록을 사용합니다. 테이블 이름이 CSV에서 변경되었더라도 GUID로 기존 테이블의 엔트리를 찾습니다.
6. 변경 요약을 확인한 뒤 **Replace graph and import**를 실행합니다. 유효하지 않은 데이터가 남아 있는 경우 **Ignore invalid data and import**로 제외 대상을 확인하고 가능한 내용만 가져올 수 있습니다. 원본 CSV는 자동으로 수정되지 않으며, **Save corrections...**로 현재 검토한 결과를 별도로 저장할 수 있습니다. 검토 중 CSV의 값을 바꾸면 저장 전까지 미저장 상태를 안내합니다.

새 테이블을 만들 때는 프로젝트 `Assets` 아래 저장 폴더를 선택합니다. 기존 Dialogue의 기본 테이블을 쓰는 **Use asset default tables**, CSV에 Start가 없을 때 유지하는 **Keep existing Start if missing** 옵션도 제공됩니다. 테이블 생성, 엔트리 생성, 로케일 테이블 생성 및 새 노드 ID 생성은 검토 창에서 명시적으로 선택합니다.

**Preview changes (before → after)...**에서 임포트 전 변경 내용을 확인할 수 있습니다. 노드 생성·삭제·타입·메시지·데이터·좌표, `Next` 연결, Localization 테이블·엔트리 생성 및 이름·Key·로케일별 번역 변경을 현재 값과 CSV 값으로 비교해 보여줍니다. 항목 검색과 노드/연결/Localization 필터를 제공하며, 실제 임포트를 누르면 **최종 변경 확인 창**에서 적용을 승인해야 합니다. 오류가 남아 있을 때의 단순 미리보기는 확정 결과가 아니며, **Ignore invalid data and import**는 제외 처리된 CSV를 기준으로 최종 변경 내역과 제외 요약을 표시합니다. CSV에 Start가 없어서 ID를 새로 생성하거나 좌표를 자동 배치하는 경우에는 생성 예정으로 안내합니다.

검토 중에는 에셋을 변경하지 않습니다. Import 적용 전에 재검증하고 변경을 Undo 그룹으로 묶으며, 실패 시 기존 데이터 및 생성 에셋의 복구를 시도합니다. Unity Localization 에셋의 이름 변경과 번역 갱신은 실제 Import 단계에서 실행합니다. **Instruction에 현재 Command/Event 스키마에서 사용하지 않는 필드 ID가 포함되어 있으면 경고하지만 그 값은 보존**하며, 이후 노드에서 직접 매핑할 수 있습니다.

`Choice`의 Option, `Condition`의 Branch, `Command/Event`의 Instruction은 부모 노드 바로 아래의 CSV 행으로 표현됩니다. Choice의 모든 Option은 부모 Choice와 같은 `TextTableGuid`를 참조해야 합니다.

## 플레이 모드 디버깅

**Tools → Dialogue**의 Runner 디버그 영역에서 실행 중인 Flow를 확인하거나 선택한 노드로 이동할 수 있습니다. 그래프를 플레이 중 수정한 경우 **Apply graph changes to Runner**로 다시 적용할 수 있습니다. 기존 실행은 일시정지되며 자동으로 재시작하지 않습니다.

## 라이선스

`LICENSE.txt`를 참고하세요.
