# ClickFlow

사용자가 화면에서 직접 지정한 UI 위치와 동작을 **시간·순서·조건**에 따라 자동으로 실행하는 개인용 Windows 데스크톱 자동화 프로그램입니다.

- C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm)
- Windows API 기반 입력 (`SendInput`, `GetCursorPos`, 저수준 훅, GDI 화면 캡처)
- 매크로는 JSON 파일로 저장 (`%LocalAppData%\ClickFlow\Macros`)

> 이 프로그램은 본인 PC의 반복 작업을 자동화하기 위한 용도입니다. CAPTCHA 등 보안·접근통제를 우회하는 용도로 설계되지 않았으며, 그런 목적으로 사용하지 마세요.

## 주요 기능

| 영역 | 기능 |
|---|---|
| 동작 | 마우스 클릭 / 더블클릭 / 이동 / 누름·뗌 / 스크롤, 키 입력 / 누름·뗌 / 문자열 입력, 대기, 스크린샷, 조건(IF/ELSE), 반복, 종료 |
| 좌표 | 화면 절대 / 대상 창 기준 / 현재 마우스 상대 좌표. **[좌표 지정]** 오버레이로 화면을 클릭해 바로 좌표를 가져옴 |
| 녹화 | 저수준 훅으로 마우스·키보드 입력을 기록하고 동작 목록으로 변환 (더블클릭 병합, 타이밍은 `실행 전 지연`으로 보존) |
| 예약 | 즉시 / 지정 시각 1회 / 간격 반복. 지정 시각까지 **Coarse Wait + 고정밀 스핀** 2단계로 접근하고 실제 오차(ms)를 로그에 기록 |
| 조건 | 픽셀 색상, 이미지 존재(템플릿 매칭), 창 존재, 활성 창 제목, 경과 시간. 재시도 횟수·간격, 실패 정책(계속/중단/종료) |
| 안전 | 전역 핫키 (기본 F8 시작 · F9 일시정지 · F10 중지 · F12 비상 정지), 마우스를 화면 왼쪽 위 모서리로 옮기면 즉시 정지 |
| 기타 | 실행 로그(ms 단위), 화면 미리보기에 클릭 위치 표시, 프로필(폴더)별 그룹, 가져오기/내보내기, 드래그로 순서 변경, 트레이 최소화, 다크 UI |

## 프로젝트 구조

```
ClickFlow.sln
├─ src/ClickFlow.Core      모델(Macro, ActionStep, Condition), 인터페이스, JSON 직렬화, 가상 키 테이블
├─ src/ClickFlow.Engine    MacroExecutor(상태 머신), ActionExecutor, HighResolutionScheduler, ConditionEngine
├─ src/ClickFlow.Input     WindowsInputDriver(SendInput), WindowService, ScreenService(GDI/템플릿 매칭), LowLevelInputRecorder(훅)
├─ src/ClickFlow.Storage   JsonMacroRepository, JsonSettingsStore, AppPaths
├─ src/ClickFlow.App       WPF UI — Views / ViewModels / Services(DI, 핫키, 좌표 지정 오버레이)
├─ tests/ClickFlow.Tests   xUnit (엔진·스케줄러·직렬화·저장소, 가짜 드라이버 사용)
└─ samples/수강신청.json   스펙의 예시 시나리오
```

실행 흐름: `UI → ExecutionService → MacroExecutor → ActionExecutor → IInputDriver(Windows API)`.
`MacroExecutor`는 `Idle → Preparing → Waiting → Running ⇄ Paused → Completed | Stopped | Error` 상태 머신을 따릅니다.

## 빌드 / 실행

요구 사항: Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Visual Studio 2022(선택).

```powershell
dotnet restore
dotnet build ClickFlow.sln -c Release
dotnet run --project src/ClickFlow.App
```

테스트:

```powershell
dotnet test
```

단일 실행 파일로 배포하려면:

```powershell
dotnet publish src/ClickFlow.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 빠른 시작

1. **내 매크로 → 새 매크로**로 매크로를 만듭니다.
2. 편집기에서 `동작` 콤보에서 **마우스 클릭**을 고르고 **+ 추가**, 오른쪽 패널의 **[좌표 지정]**을 눌러 화면에서 클릭할 위치를 클릭합니다.
3. **대기**, **키 입력(ENTER)** 등을 추가하고 **저장**합니다.
4. 바로 확인하려면 **테스트 실행**(예약 무시, 즉시 실행), 예약하려면 `실행 방식`을 **예약 실행**으로 바꾸고 날짜/시간을 `HH:mm:ss.fff`로 입력한 뒤 **▶ 실행**을 누릅니다. 실행 페이지에 카운트다운이 표시됩니다.
5. 실행 중에는 언제든 **F10**(중지) 또는 **F12**(비상 정지)로 멈출 수 있습니다.

레코더로 시작하려면 **레코더 → ● 녹화 시작** 후 작업을 수행하고 **F10**으로 끝내면 동작 목록이 만들어집니다.

### 매크로 JSON 예시

```json
{
  "name": "수강신청",
  "schedule": { "type": "Once", "startTime": "2026-11-02T09:00:00.000" },
  "actions": [
    { "type": "Click", "x": 1240, "y": 730 },
    { "type": "Wait",  "milliseconds": 300 },
    { "type": "Click", "x": 1100, "y": 680 },
    { "type": "Key",   "key": "ENTER" }
  ]
}
```

`samples/수강신청.json`을 **내 매크로 → 가져오기**로 불러와 구조를 확인할 수 있습니다.

## 시간 정밀도에 대해

예약 시각이 가까워지면 스케줄러는 `Task.Delay`(운영체제 타이머, ~15ms 단위) 대신 스레드풀 스레드에서 `Stopwatch` 기반 스핀 대기로 전환합니다 (기본 100ms 전, 설정에서 변경). 앱 시작 시 `timeBeginPeriod(1)`로 타이머 해상도를 1ms로 올립니다.
그래도 운영체제 스케줄링·시스템 부하 때문에 **0ms 오차를 보장하지는 않습니다.** 실행 로그의 `목표 시각 도달 (오차 +X.Xms)` 항목으로 실제 지연을 확인하세요.

## 설계 메모

- **동작 모델**: `ActionStep` 추상 클래스 + `System.Text.Json` 다형성(`"type"` 판별자). 새 동작을 추가하려면 Core에 클래스를 만들고 `[JsonDerivedType]`과 `ActionExecutor`의 분기, `ActionCatalog`를 각각 한 줄씩 추가하면 됩니다.
- **녹화 타이밍**: 별도의 `Wait` 동작을 만들지 않고 각 동작의 `DelayBeforeMs`에 직전 동작과의 간격을 기록합니다. 편집기의 `지연` 열에서 바로 볼 수 있습니다.
- **창 기준 좌표**: 대상 창이 지정된 매크로에서 `좌표 기준 = Window`인 동작은 실행 시점의 창 클라이언트 영역 좌상단에 더해 계산됩니다. 좌표 지정 오버레이도 이 모드에서는 창 기준으로 변환해 저장합니다.
- **이미지 인식**: `ScreenService.FindImage`는 픽셀 단위 템플릿 매칭(코너 5점 사전 검사 → 전체 비교, 유사도 임계)으로 구현되어 있습니다. 큰 검색 영역에서는 느릴 수 있으니 `검색 영역`을 지정하는 편이 좋습니다.

## 알려진 제한 / 아직 확인하지 못한 부분

이 저장소는 Linux 환경에서 `EnableWindowsTargeting`으로 **컴파일만** 검증했습니다 (전체 솔루션 0 경고 / 0 오류, 단위 테스트 48개 통과). 실제 Windows에서 처음 실행할 때 다음을 확인해 주세요.

- 멀티 모니터(특히 음수 좌표 원점)에서 `SendInput` 절대 좌표 이동 — `SetCursorPos`를 후속 호출해 보정하고 있습니다.
- 일부 게임/앱이 `wVk`와 스캔코드를 함께 보내면 키를 두 번 처리하는 경우 — `WindowsInputDriver`에서 `wVk`를 0으로 바꾸면 됩니다.
- `SetForegroundWindow` 제한 — `AttachThreadInput` 우회를 쓰지만 Windows 버전에 따라 실패할 수 있습니다.
- 저수준 훅은 콜백이 ~300ms 이상 걸리면 Windows가 조용히 해제합니다. 훅 스레드에서 UI를 블로킹하지 않도록 `Dispatcher.BeginInvoke`로 넘기고 있습니다.
- 중첩 동작(반복/조건 안의 동작)의 세부 옵션은 편집기에서 좌표·대기·키만 바로 편집할 수 있고, 나머지는 JSON 파일에서 수정해야 합니다.
- 관리자 권한으로 실행 중인 창에는 일반 권한의 ClickFlow가 입력을 보낼 수 없습니다 (UIPI). 그 경우 ClickFlow도 관리자 권한으로 실행하세요.

## 로드맵 (스펙 27장 기준)

- [x] Phase 1 — MVP: 좌표 가져오기, 클릭, 키 입력, 대기, 동작 목록, 시작/중지, JSON 저장·불러오기
- [x] Phase 2 — Recorder: 마우스·키보드 녹화, 동작 편집/순서 변경/삭제/복제
- [x] Phase 3 — Scheduler: 예약 실행, 카운트다운, 고정밀 타이밍, 반복 실행, 실행 로그
- [x] Phase 4 — Smart Automation: 창 인식, 창 기준 좌표, 이미지/픽셀 감지, IF/ELSE, 재시도, 타임아웃
- [~] Phase 5 — UX 고도화: 다크 UI, 드래그 정렬, 프로필, 가져오기/내보내기, 핫키, 트레이 (완료) · 라이트 테마, 실행 통계 영속화, 설정 백업, 플러그인 구조 (미구현)
