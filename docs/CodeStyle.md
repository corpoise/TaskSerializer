---
name: CodeStyle
description: C# 코드 스타일 규칙. 네임스페이스/using 선언 순서, 들여쓰기, 변수명, 중괄호, var 사용, 타입 참조, 미사용 코드 정리, 조건식 표현, expression body 금지, record/class 선택, init 프로퍼티 규칙을 포함한다.
---

# CodeStyle

## C# 코드 작성 지침

답변에 C# 코드를 적어야 할 시 다음의 규칙에 맞춰서 작성해 주세요.

### 1. Namespace / Using 선언 순서

file-scoped namespace(`namespace Foo;`)를 사용하며, `using` 문은 namespace 선언 아래에 작성한다.

### 2. 들여쓰기

들여쓰기는 whitespace 2칸으로 사용한다. 탭(tab) 사용을 금지하며, 항상 스페이스(space)를 사용한다.

### 3. 변수명 규칙

변수명은 camelCase 로 작성한다.

### 4. camelCase 적용 범위

지역 변수, 매개변수, private 필드는 camelCase로 작성한다. (underscore prefix 없이)

```csharp
// ✅
private int myField;
void Foo(int myParam) { var myValue = 1; }

// ❌
private int _myField;
void Foo(int MyParam) { var MyValue = 1; }
```

### 5. 중괄호 규칙

if/else/else if 문 다음엔 항상 `{}` 추가해서 작성하며 한 줄짜리 body 일 시에도 적용한다.

여는 중괄호, body, 닫는 중괄호는 각각 별도 줄에 작성한다.

```csharp
// ✅
if (flag is false)
{
  return;
}

// ❌
if (flag is false) { return; }
```

### 6. var 우선 사용

명확한 이유가 없다면 변수타입은 항상 `var` 로 사용한다. 필요하다면 설명과 함께 사용자에게 확인 후 진행한다.

### 7. namespace.타입 형식 금지

타입 사용 시 코드 내에서 `namespace.타입` 형식을 절대 사용하지 않는다. 반드시 `using` 으로 namespace를 선언한 후 타입명만 사용한다.

```csharp
// ✅
using System.IO;
File.ReadAllText("path");

// ❌
System.IO.File.ReadAllText("path");
```

### 8. 미사용 코드 정리

코드 작성 후 결과물을 검토하여, 이번 작업으로 인해 발생한 미사용 `using`, 변수, 필드, 메서드, 타입은 삭제한다. 단, 이번 작업과 무관한 기존의 미사용 코드는 건드리지 않는다.

### 9. 조건식 표현 규칙

if 문 조건식에서 `!` 부정 연산자를 사용하지 않는다. 대신 `is false`로 표현한다.

```csharp
// ✅
if (IsValid() is false) { }
if (flag is false) { }

// ❌
if (!IsValid()) { }
if (!flag) { }
```

if 문 조건식에서 리터럴 값(문자열, 정수, null, 열거형 멤버 등 모든 상수)과 비교할 때는 `==` 대신 `is`, `!=` 대신 `is not` 패턴을 사용한다.

```csharp
// ✅
if (format is "int32" || format is "int64") { }
if (count is 0) { }
if (state is DataRowState.Deleted) { }
if (state is not DataRowState.Deleted) { }

// ❌
if (format == "int32" || format == "int64") { }
if (count == 0) { }
if (state == DataRowState.Deleted) { }
if (state != DataRowState.Deleted) { }
```

문자열이 비었는지 검사할 때는 직접 비교하지 않고 `string.IsNullOrEmpty`를 사용한다. 단, 공백 문자열까지 비어 있는 값으로 처리해야 하는 경우에는 `string.IsNullOrWhiteSpace`를 사용한다.

```csharp
// ✅
if (string.IsNullOrEmpty(name)) { }
if (string.IsNullOrWhiteSpace(name)) { }

// ❌
if (name == "") { }
if (name is "") { }
if (name.Length == 0) { }
```

### 10. Expression Body 규칙

메서드는 한 줄이어도 expression body(`=>`)를 사용하지 않는다. 항상 `{}` 블록으로 작성한다.

메서드 본문이 비어 있는 경우에도 여는 중괄호, body, 닫는 중괄호를 각각 별도 줄에 작성한다. 이는 생성자, 클래스, 인터페이스, 구조체 선언에도 동일하게 적용한다.

단, getter만 존재하는 프로퍼티는 `=>` 형식을 허용한다.

```csharp
// ✅ 메서드 — 블록 사용
bool IsFoo()
{
  return true;
}

// ✅ 빈 메서드 — 중괄호 별도 줄
public void AfterBind()
{
}

// ✅ getter-only 프로퍼티 — expression body 허용
public string Name => this.column.Name;

// ❌ 메서드에 expression body
bool IsFoo() => true;

// ❌ 빈 메서드를 한 줄로 작성
public void AfterBind() { }
```

### 16. 닫는 중괄호 뒤 빈 줄

닫는 중괄호(`}`) 다음에 같은 블록 내의 다른 코드가 이어지는 경우, 반드시 한 줄을 비운다. 단, 이어지는 것이 `}`, `else`, `else if`, `catch`, `finally`인 경우에는 빈 줄을 넣지 않는다.

```csharp
// ✅
if (condition)
{
  DoA();
}

DoB();

// ✅ else는 빈 줄 없이
if (condition)
{
  DoA();
}
else
{
  DoB();
}

// ❌
if (condition)
{
  DoA();
}
DoB();
```

### 17. 멤버 간 빈 줄

메서드, 생성자, 프로퍼티(expression body 포함) 선언 사이에는 반드시 한 줄을 비운다.

```csharp
// ✅
public int Foo { get; }

public void Bar()
{
}

public void Baz()
{
}

// ❌
public int Foo { get; }
public void Bar()
{
}
public void Baz()
{
}
```

### 18. using alias 사용 금지

`using Alias = Namespace.Type;` 형식의 using alias는 사용하지 않는다. 타입명이 충돌하는 경우 한쪽의 사용 범위를 좁히거나 호출 지점을 메서드로 분리하여 해소한다.

```csharp
// ✅
using System.IO;
var content = File.ReadAllText("path");

// ❌
using IO = System.IO;
var content = IO.File.ReadAllText("path");
```

### 11. record 대신 class 사용 시 사유 주석 작성

생성자에서 값을 주입하고 모든 프로퍼티가 `init` 또는 get-only 형태라서 record로 표현 가능한 타입을 class로 작성해야 하는 경우, class 선언부 바로 위에 왜 record가 아닌 class를 사용하는지 주석으로 명시한다.

특히 다음과 같은 이유가 있을 때 class 사용 사유를 남긴다.

- 내부 상태가 mutable 컬렉션이나 mutable 객체를 가진다.
- UI 편집 모델처럼 객체 동일성 또는 변경 가능한 상태가 중요하다.
- 값 동등성(record equality)이 의도와 맞지 않는다.
- 기존 프레임워크 바인딩이나 직렬화 흐름 때문에 class가 필요하다.

```csharp
// SerializedTask owns mutable execution state (reserved task chain, dependency
// counters) updated via Interlocked, so class is used instead of record.
internal sealed class SerializedTask : DependencyTaskBase
{
}
```

### 12. readonly 필드

생성자 또는 필드 선언에서 초기화되고 이후 변경되지 않는 private 필드에는 `readonly`를 항상 붙인다.

```csharp
// ✅
private readonly Dependency[] dependencies;
private readonly Action action;

// ❌
private Dependency[] dependencies;
private Action action;
```

단, 생성자가 아닌 메서드에서 할당되는 필드는 C# 언어 규칙상 `readonly` 적용이 불가능하므로 제외한다.

### 14. this. 한정자 사용

필드 및 프로퍼티에 접근할 때 항상 `this.`를 명시한다.

```csharp
// ✅
public SerializedTask(Dependency[] dependencies, Action action)
  : base(dependencies)
{
  this.action = action;
}

// ❌
public SerializedTask(Dependency[] dependencies, Action action)
  : base(dependencies)
{
  action = action;
}
```

### 15. 불필요한 타입 변환 금지

이미 올바른 타입인 값에 대해 불필요한 캐스팅을 하지 않는다. 연산 결과 타입이 이미 원하는 타입과 일치하거나 암시적으로 변환 가능한 경우 명시적 캐스트를 사용하지 않는다.

```csharp
// ✅
int count = this.dependencies.Length; // Length는 이미 int 반환
return count > 0;

// ❌
var count = this.dependencies.LongLength; // long 반환
return (int)count > 0; // Length를 쓰면 캐스트 자체가 불필요
```

반환 타입이 맞지 않아 변환이 반드시 필요한 경우에는 해당 값을 생산하는 메서드의 반환 타입을 먼저 수정하는 것을 검토한다.

### 19. 인스턴스 생성 후 메서드 호출 인라인 체이닝 금지

`new X(...).Method()` 형태로 생성과 호출을 한 줄에 체이닝하지 않는다. 항상 인스턴스를 변수에 먼저 할당한 뒤 메서드를 호출한다.

```csharp
// ✅
var task = new SerializedTask(dependencies, action);
task.TryAcquire(dispatchToThreadPool);

// ❌
new SerializedTask(dependencies, action).TryAcquire(dispatchToThreadPool);
```

### 13. readonly 프로퍼티의 init 명시

생성 이후 값이 변경되지 않는 public 프로퍼티는 get-only 프로퍼티 대신 `init` setter를 명시한다.

```csharp
// ✅
public string Name { get; init; }

// ❌
public string Name { get; }
```

단, 내부 mutable 객체를 생성자에서 직접 소유하고 외부 초기화를 허용하지 않아야 하는 경우에는 get-only 프로퍼티를 사용할 수 있다. 이 경우 record 대신 class를 사용하는 규칙과 동일하게 사유가 코드에서 드러나야 한다.

### 20. sealed 클래스

상속을 목적으로 설계된 클래스(`abstract` 클래스, 또는 명시적으로 상속을 허용하는 기반 클래스)가 아닌 모든 `class`에는 `sealed`를 붙인다.

```csharp
// ✅
public sealed class Counter : SerializableObject { }

// ❌
public class Counter : SerializableObject { }
```