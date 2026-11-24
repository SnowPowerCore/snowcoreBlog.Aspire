---
name: unit-test-author
description: Write or repair xUnit v3 tests following snowcoreBlog conventions — AAA structure, MethodUnderTest_Scenario_ExpectedResult naming, NSubstitute mocks, FluentAssertions asserts, bUnit for components, native Microsoft.Testing.Platform.
---

# unit-test-author

Author unit tests for snowcoreBlog. Stack: **xUnit v3** running natively on
**Microsoft.Testing.Platform (MTP)**, **NSubstitute** for mocking,
**FluentAssertions** for assertions, **bUnit** for Blazor components.

## Naming and structure

- Test class: `<TypeName>Tests`.
- Test method: `MethodUnderTest_Scenario_ExpectedResult`.
- Arrange-Act-Assert with comments; omit `// Arrange` when there is nothing to set up.

```csharp
public sealed class RegisterStateTests
{
    [Fact]
    public void BeginRegistration_WhenCalled_SetsBusyFlag()
    {
        // Arrange
        var state = new RegisterState();

        // Act
        state.BeginRegistration();

        // Assert
        state.IsBusy.Should().BeTrue();
    }
}
```

## Mocking (NSubstitute)

Mock at service/repository seams only. Never mock `HttpClient` directly —
wrap it behind a typed client or inject a service seam; if the handler itself
must be faked, subclass `HttpMessageHandler`.

```csharp
var repo = Substitute.For<IArticleRepository>();
repo.GetAsync(id, Arg.Any<CancellationToken>()).Returns(article);

await sut.Run(id, CancellationToken.None);

await repo.Received(1).GetAsync(id, Arg.Any<CancellationToken>());
```

## Blazor components (bUnit)

```csharp
using Bunit;

var cut = RenderComponent<NotificationBar>(p => p.Add(x => x.Items, items));
cut.Markup.Should().Contain("expected-text");
```

Note: repo-wide ConfigureAwait.Fody weaving means awaited continuations do not
return to the Blazor dispatcher — components under test must marshal UI
updates via `InvokeAsync`.

## Running tests

Per-project, never solution-wide:

```bash
dotnet test <test-project>.csproj
```

MTP is enabled repo-wide: `global.json` sets
`"test": { "runner": "Microsoft.Testing.Platform" }` and the root
`Directory.Build.props` gives `*.Tests` projects `OutputType=Exe` +
`UseMicrosoftTestingPlatformRunner`. Consequences:

- **No VSTest args** — `--filter`, `--logger`, `--collect` no longer work.
  Use `--filter-class '*SomeNamespace*'` / `--filter-method '*MethodName*'`.
- If results look impossible, rebuild the test project explicitly — a stale
  MTP exe prints old-format ("Test run summary") output.

## Rules of thumb

- Test observable behavior, not private wiring; one logical outcome per test.
- Deterministic tests only: inject `TimeProvider`, never call `DateTime.Now`.
- Value-type trap: in conditionals targeting nullable value types write
  `null` literally — `cond ? default : x` binds `default` to `0`, never null.
- Full conventions and examples: `docs/UnitTesting.md`.
