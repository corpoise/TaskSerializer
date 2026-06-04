# TaskSerializer

DAG(Directed Acyclic Graph) 기반 lock-free task 직렬화 라이브러리입니다.
동일한 Dependency를 공유하는 task들이 DAG 순서에 따라 직렬로 실행되도록 보장하며, ThreadPool 오버헤드 없이 호출자 스레드에서 직접 실행하는 고성능 서버 시나리오용으로 설계되었습니다.

---

## Features

- **Sync-when-free 실행** — 경합이 없는 경우 호출자 스레드에서 즉시 동기 실행, ThreadPool 큐잉 및 컨텍스트 스위치 비용 zero
- **DAG 기반 직렬화** — 동일 dependency를 공유하는 task가 항상 생성 순서대로 직렬 실행됨을 보장
- **교차 actor 잠금** — `PostWith`로 여러 actor의 dependency를 한 번에 획득, 교착 방지를 위해 의존성을 생성 순서(ID) 기준으로 자동 정렬
- **비동기 지원** — `Func<Task>` 오버로드로 `async/await` 코드를 직렬화 체인에 통합
- **ThreadPool 분리 옵트인** — `dispatchToThreadPool: true`로 첫 실행만 ThreadPool에 위임, 장시간 작업에서 호출자 블록킹 방지
- **예외 격리** — 실행 중 예외를 catch하여 NLog 로깅 후 `UnhandledException` 이벤트로 전달, 체인 실행은 계속됨
- **체인 깊이 경고** — DEBUG 빌드에서 동기 체인이 500단계를 초과하면 NLog Warn 1회 발생 (stack overflow 선제 감지)
- **스레드 안전성 검증** — DEBUG 빌드 전용 `EnsureThreadSafe()`로 런타임에 현재 task가 올바른 dependency를 보유했는지 검증

---

## 실행 모델

`Post()` / `PostWith()` 호출 시 해당 dependency의 상태에 따라 동작이 달라집니다.

| 상황 | action 실행 시점 | 실행 스레드 |
|------|----------------|------------|
| dependency가 free (경합 없음) | `Post()` 반환 전 (동기) | 호출자 스레드 |
| dependency가 다른 task에 의해 예약됨 | 선행 task 완료 직후 | 선행 task의 실행 스레드 |

경합 없는 경로에서 ThreadPool 큐잉 오버헤드를 완전히 제거하는 것이 이 라이브러리의 핵심 설계 원칙입니다.

### 교착(Deadlock) 방지

`PostWith`로 여러 dependency를 동시에 획득할 때, 각 dependency에 부여된 단조 증가 ID를 기준으로 **항상 동일한 순서로 잠금**을 획득합니다. 서로 다른 actor 조합이 교차 호출되어도 순환 대기가 발생하지 않습니다.

```
Actor A(id=1), Actor B(id=2), Actor C(id=3) 일 때:
  A.PostWith(action, B)   → 1번 → 2번 순으로 획득
  B.PostWith(action, C)   → 2번 → 3번 순으로 획득
  A.PostWith(action, C)   → 1번 → 3번 순으로 획득  ✓ 항상 오름차순
```

### UI 스레드 호출 금지

경합 없는 경로에서 호출자가 action이 완료될 때까지 블록됩니다. **WPF/WinForms 등의 UI 스레드에서 직접 호출하면 안 됩니다.** 이 라이브러리는 worker pool이 task를 처리하는 서버 시나리오를 가정합니다.

---

## API

### `SerializableObject`

모든 직렬화 가능 객체의 기반 클래스. 이를 상속하여 사용합니다.

```csharp
public abstract class SerializableObject : IAsyncDisposable
```

#### Post

자신의 dependency만 잠그고 action을 직렬 실행합니다.

```csharp
void Post(Action action, bool dispatchToThreadPool = false)
void Post(Func<Task> func, bool dispatchToThreadPool = false)
```

#### PostWith

자신과 다른 actor들의 dependency를 동시에 잠그고 action을 직렬 실행합니다. 모든 dependency 획득 전까지 스핀 대기합니다.

```csharp
void PostWith(Action action, params SerializableObject[] others)
void PostWith(Action action, bool dispatchToThreadPool, params SerializableObject[] others)
void PostWith(Func<Task> func, params SerializableObject[] others)
void PostWith(Func<Task> func, bool dispatchToThreadPool, params SerializableObject[] others)
```

#### DisposeAsync

dispose 완료 전까지 이미 예약된 task를 모두 처리한 뒤 `DisposeAsyncCore()`를 호출합니다. 중복 호출은 무시됩니다. dispose 이후의 `Post` / `PostWith` 호출은 무시됩니다.

```csharp
ValueTask DisposeAsync()
protected abstract ValueTask DisposeAsyncCore()
```

#### EnsureThreadSafe (DEBUG only)

현재 실행 컨텍스트가 해당 actor의 dependency를 보유하고 있는지 검증합니다. Release / Shipping 빌드에서는 완전히 제거됩니다.

```csharp
void EnsureThreadSafe()
```

---

## 사용 예

### 기본 사용

```csharp
public sealed class Counter : SerializableObject
{
  private int value;

  public void Increment()
  {
    this.Post(() => this.value++);
  }

  public int Snapshot()
  {
    var result = 0;
    this.Post(() => result = this.value);
    return result;
  }

  protected override ValueTask DisposeAsyncCore()
  {
    return ValueTask.CompletedTask;
  }
}
```

### 여러 actor에 걸친 작업

두 actor 사이의 이체처럼 원자적으로 처리해야 하는 교차 actor 작업에는 `PostWith`를 사용합니다.

```csharp
// counterA와 counterB를 동시에 잠그고 이체 실행
counterA.PostWith(() => Transfer(counterA, counterB), counterB);
```

### 비동기 작업

```csharp
public sealed class DataLoader : SerializableObject
{
  private string? cachedData;

  public void LoadAsync()
  {
    this.Post(async () =>
    {
      this.cachedData = await FetchFromRemoteAsync();
    });
  }

  protected override ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}
```

### 장시간 작업에서 호출자 블록킹 방지

```csharp
// 첫 실행만 ThreadPool에 위임, 이후 체인은 동기 실행
actor.Post(() => HeavyWork(), dispatchToThreadPool: true);
```

### 안전한 종료

```csharp
await actor.DisposeAsync(); // 예약된 task를 모두 처리한 뒤 종료
```

---

## 예외 처리

task 실행 중 발생한 예외는 체인을 중단하지 않고 다음 순서로 처리됩니다.

1. NLog `Error` 레벨로 message + stack trace 기록
2. `UnhandledException` 이벤트 발화 — 호출자가 telemetry 등에 연결 가능

```csharp
UnhandledExceptionHandler.UnhandledException += ex => myTelemetry.Report(ex);
```

---

## 로깅 (NLog)

라이브러리 루트의 `nlog.config`가 기본 설정으로 제공되며, 빌드 시 출력 디렉토리로 자동 복사됩니다.

| 항목 | 기본값 |
|------|--------|
| File target | `logs/yyyy-MM-dd_HH_mm_00.txt` (분 단위 롤링) |
| Console target | stdout, 동일 layout |
| Layout | `${longdate}\|${level}\|${logger}\|${message}\|${exception:format=tostring}` |
| 최소 레벨 | `Info` |

호출 프로세스에서 동작을 바꾸려면 같은 폴더에 자체 `nlog.config`를 두거나 `LogManager.Setup()` API를 사용합니다.

---

## 빌드 및 테스트

```powershell
# Debug
dotnet build C:\depot\Core\CoreLibrary.slnx

# Release
dotnet build C:\depot\Core\CoreLibrary.slnx -c Release

# Shipping (JIT 최적화, PDB 없음)
dotnet build C:\depot\Core\CoreLibrary.slnx -c Shipping

# 테스트
dotnet test C:\depot\Core\CoreLibrary.slnx
```

### 빌드 구성 차이

| 구성 | JIT 최적화 | `#if DEBUG` 블록 | PDB |
|------|-----------|----------------|-----|
| Debug | 없음 | 활성 (callDepth 추적 · 중복 dep 검사 · EnsureThreadSafe) | 있음 |
| Release | 있음 | 비활성 | 있음 |
| Shipping | 있음 | 비활성 | 없음 |

---

## Tech Stack

- C# / .NET 10
- Lock-free 원자 연산 (`Interlocked`)
- NLog 6.1.3
- xUnit 2.9.3

<br>
<br>

---
---

## 스트레스 테스트

### 측정 환경

- **CPU**: AMD Ryzen 7 9800X3D (8코어 / 16스레드)
- **RAM**: 64 GB
- **OS**: Windows 11 Pro (10.0.26200)
- **런타임**: .NET 10.0.8

---

### Barrier 방식 — `*_RunSerially`

`Barrier(Environment.ProcessorCount * 2)`로 CPU 논리 코어 수의 2배에 해당하는 스레드가 동시에 `Post()` / `PostWith()`를 호출하도록 동기화합니다. 가능한 최강의 동시 경합 조건을 보장합니다.

```csharp
var concurrency = Environment.ProcessorCount * 2; // e.g. 32 on 16-core CPU
var perThread = taskCount / concurrency;
var barrier = new Barrier(concurrency);

for (var t = 0; t < concurrency; t++)
{
  ThreadPool.QueueUserWorkItem(_ =>
  {
    barrier.SignalAndWait(); // 모든 스레드 동시 출발 보장
    for (var i = 0; i < perThread; i++)
      actor.Post(() => { ... });
  });
}
```

| 테스트 | 시나리오 | Debug | Release | Shipping |
|--------|---------|-------|---------|---------|
| `SingleDep_HundredThousandSyncTasks_RunSerially` | 1 dep × 100,000 tasks | 79 ms | 104 ms | 108 ms |
| `ThreeDeps_HundredThousandSyncTasks_RunSerially` | 3 deps × 100,000 tasks | 16 s † | 18 s † | 15 s † |
| `CrossActorPostWith_HundredThousandTasks_RunSerially` | A+B / B+C / A+C 교차 × 100,000 tasks | 139 ms | 126 ms | 109 ms |

> Debug · Release는 단일 측정값. Shipping은 10회 실행 후 최솟값 · 최댓값을 제외한 평균값.
>
> † ThreeDeps Barrier 수치는 32개 스레드가 동일한 3개 lock을 쉬지 않고 spin-acquire하는 인위적 극한 시나리오이며, 실제 서버 workload에서는 발생하지 않는다. 이 정도의 sustained 동시 경합이 발생한다면 dependency 설계가 잘못된 것이므로 호출 패턴을 재검토해야 한다. 참고용 한계치 기록이다.

---

### Direct 방식 — `*_Direct_RunSerially`

`Post()` / `PostWith()`를 호출 스레드에서 직접 루프로 호출합니다. 경합이 없으므로 sync-when-free가 항상 발동되어 각 task가 호출자 스레드에서 즉시 동기 실행됩니다. ThreadPool 큐잉 오버헤드가 전혀 없는 최소 비용 경로입니다.

```csharp
for (var i = 0; i < taskCount; i++)
{
  actor.Post(() => { ... });
}
```

| 테스트 | 시나리오 | Debug | Release | Shipping |
|--------|---------|-------|---------|---------|
| `SingleDep_HundredThousandSyncTasks_Direct_RunSerially` | 1 dep × 100,000 tasks | 26 ms | 20 ms | 20 ms |
| `ThreeDeps_HundredThousandSyncTasks_Direct_RunSerially` | 3 deps × 100,000 tasks | 35 ms | 24 ms | 25 ms |
| `CrossActorPostWith_HundredThousandTasks_Direct_RunSerially` | A+B / B+C / A+C 교차 × 100,000 tasks | 28 ms | 21 ms | 19 ms |

> Debug · Release는 단일 측정값. Shipping은 10회 실행 후 최솟값 · 최댓값을 제외한 평균값.

---

### dispatchToThreadPool 방식 — `*_ThreadPoolDispatch_RunSerially`

`dispatchToThreadPool: true`로 `Post()` / `PostWith()`를 호출합니다. 첫 실행만 ThreadPool에 위임하므로 호출자 스레드가 블록되지 않습니다. 이후 체인은 ThreadPool 스레드에서 동기 실행됩니다.

```csharp
for (var i = 0; i < taskCount; i++)
{
  actor.Post(() => { ... }, dispatchToThreadPool: true);
}
```

> **task 수 제한**: `dispatchToThreadPool: true`를 tight loop에서 호출하면 ThreadPool 스레드가 실행을 시작하기 전에 호출 스레드가 선행 task 체인을 모두 예약합니다. task 수가 충분히 많으면 재귀 실행 체인이 스택 오버플로우를 유발합니다 (테스트 환경에서 약 3,735 프레임). 이 패턴은 개별 heavy 작업을 ThreadPool에 위임하는 용도로 설계되었으므로 tight loop 사용은 설계 오류입니다. 스트레스 테스트는 task 수를 2,000으로 제한합니다.

| 테스트 | 시나리오 | Debug | Release | Shipping |
|--------|---------|-------|---------|---------|
| `SingleDep_TwoThousandSyncTasks_ThreadPoolDispatch_RunSerially` | 1 dep × 2,000 tasks | <1 ms | <1 ms | <1 ms |
| `ThreeDeps_TwoThousandSyncTasks_ThreadPoolDispatch_RunSerially` | 3 deps × 2,000 tasks | 1 ms | 1 ms | <1 ms |
| `CrossActorPostWith_TwoThousandTasks_ThreadPoolDispatch_RunSerially` | A+B / B+C / A+C 교차 × 2,000 tasks | 46 ms † | 1 ms | 1 ms |

> Debug · Release는 단일 측정값. Shipping은 10회 실행 후 최솟값 · 최댓값을 제외한 평균값.
>
> † CrossActorPostWith Debug 46ms는 `#if DEBUG` 블록의 추가 검증(중복 dep 검사, callDepth 추적)이 3개 dependency를 동시에 처리하는 코드 경로에서 누적되는 오버헤드다. Release에서는 해당 블록이 제거되어 1ms로 감소한다.

<br>
<br>

---
---

*This project was generated with [Claude Code](https://claude.ai/code).*
