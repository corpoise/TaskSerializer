# Concurrency

DAG(Directed Acyclic Graph) 알고리즘 기반 Task 직렬화 라이브러리.
동일한 Dependency를 공유하는 Task들이 DAG 순서에 따라 직렬로 실행되도록 보장한다.

**스택**: C# / .NET 10 / Lock-free 원자 연산 (`Interlocked`)

---

## 사용 예

```csharp
public sealed class Counter : SerializableObject
{
  private int value;

  public void Increment()
  {
    this.Post(() => this.value++);
  }

  protected override ValueTask DisposeAsyncCore()
  {
    return ValueTask.CompletedTask;
  }
}
```

여러 actor에 걸친 작업은 `PostWith`로 dependency를 한꺼번에 잡는다.

```csharp
counterA.PostWith(() => Transfer(counterA, counterB), counterB);
```

---

## 실행 모델

이 라이브러리는 **고성능 서버 task 처리** 용도로 설계되었으며, latency 예측 가능성보다 **처리량(throughput)을 우선**한다.

### Sync-when-free 시맨틱

`Post()` / `PostWith()`는 dependency의 현재 상태에 따라 두 가지로 다르게 동작한다.

| 상황 | Post 반환 시점 | action 실행 스레드 |
|------|--------------|------------------|
| dep이 free (경합 없음) | action 완료 후 | 호출자 스레드 (동기 실행) |
| dep이 다른 task에 잡힘 | 즉시 | 이전 owner의 스레드 |

경합 없는 경로에서 ThreadPool 큐잉 및 컨텍스트 스위치 오버헤드를 회피하기 위해
**호출자 스레드에서 그대로 실행**하는 것이 의도된 동작이다.

### UI 스레드에서 호출 금지

호출자가 임의 시간 동안 블록될 수 있으므로 **UI 스레드(WPF/WinForms 등)에서 직접 호출해서는 안 된다.**
이 라이브러리는 worker pool이 task를 직렬화하는 서버 시나리오를 가정한다.

### ThreadPool 분리가 필요한 경우

장시간 실행되는 작업으로 호출자를 묶고 싶지 않다면 `dispatchToThreadPool: true`로 옵트인한다.

```csharp
actor.Post(() => HeavyWork(), dispatchToThreadPool: true);
```

이 옵션은 **첫 실행만** ThreadPool로 분리한다.
이미 reserved chain에 들어간 task는 chain의 첫 실행 스레드에서 순차 실행된다.

---

## 빌드

```powershell
dotnet build C:\depot\Core\CoreLibrary.slnx
dotnet build C:\depot\Core\CoreLibrary.slnx -c Release
dotnet build C:\depot\Core\CoreLibrary.slnx -c Shipping
dotnet test  C:\depot\Core\CoreLibrary.slnx
```

---

## 예외 처리 / 로깅

Task 실행 중 발생한 예외는 `UnhandledExceptionHandler`에서 처리한다.

1. 내부적으로 NLog(`Logger.Error(e, e.Message)`)로 message + stack trace 출력
2. `UnhandledException` 이벤트를 발화하여 호출자가 telemetry 등에 연결 가능

```csharp
UnhandledExceptionHandler.UnhandledException += ex => myTelemetry.Report(ex);
```

### 체인 깊이 경고

`TryExecuteReserved` → `nextTask.TryExecute` → `ExecuteInternal` → ... 형태의 동기 재귀 체인은 호출 스택을 누적시킨다.
한 actor에 짧은 간격으로 수천 개의 task가 폭주하면 체인 깊이가 수백~수천 단계에 이르러 stack overflow가 발생할 수 있다.

라이브러리는 체인 깊이가 **500**을 넘는 즉시 NLog `Warn` 레벨로 1회 경고 로그를 발생시킨다.
경고가 보이면 호출 패턴을 점검하거나, post 시 `dispatchToThreadPool: true`로 첫 실행을 ThreadPool로 분리하는 것을 검토한다.

### NLog 설정

라이브러리 루트의 `nlog.config`가 기본 설정으로 제공되며, 빌드 시 출력 디렉토리로 자동 복사된다.
NLog 5+ 는 실행 파일과 같은 폴더의 `nlog.config`를 자동으로 로드한다.

기본 설정:
- **File 타깃**: `logs/yyyy-MM-dd_HH_mm_00.txt` — 매 분마다 새 파일이 생성된다. 파일명은 해당 분의 시작 시각(`SS`는 항상 `00`)
- **Console 타깃**: 동일한 layout으로 stdout에 출력
- Layout: `${longdate}|${level}|${logger}|${message}|${exception:format=tostring}`
- 최소 레벨: `Info`

호출 프로세스에서 동작을 바꾸려면 자신의 `nlog.config`로 덮어쓰면 된다(같은 폴더에 두면 우선 적용된다).
프로그램 코드에서 동적으로 변경하려면 `LogManager.Setup()...` API를 사용한다.

---

## 스트레스 테스트 결과

동일 dependency를 공유하는 task 100,000개를 ThreadPool에서 동시에 투입하는 극한 경합 시나리오 측정 결과.

### 빌드 구성별 특징

| 구성 | JIT 최적화 | `#if DEBUG` 블록 | PDB |
|------|-----------|----------------|-----|
| Debug | 없음 | 활성 (callDepth 추적·중복 dep 검사·EnsureThreadSafe) | 있음 |
| Release | 있음 | 비활성 | 있음 |
| Shipping | 있음 | 비활성 | 없음 |

### 측정 환경

- **CPU**: AMD Ryzen 7 9800X3D (8코어 / 16스레드)
- **RAM**: 64 GB
- **OS**: Windows 11 Pro (10.0.26200)
- **런타임**: .NET 10.0.8

### 결과 (단위: ms, 단일 측정값)

| 테스트 | 시나리오 | Debug | Release | Shipping |
|--------|---------|-------|---------|---------|
| `SingleDep_HundredThousandSyncTasks_RunSerially` | 1 dep × 100,000 tasks | 82 | 78 | 78 |
| `ThreeDeps_HundredThousandSyncTasks_RunSerially` | 3 deps × 100,000 tasks | 104 | 132 | 104 |
| `CrossActorPostWith_HundredThousandTasks_RunSerially` | A+B / B+C / A+C 교차 × 100,000 tasks | 108 | 92 | 91 |

> Debug · Release는 단일 측정값이다.
> Shipping은 10회 실행 후 최솟값 · 최댓값을 제외한 평균값이다.
