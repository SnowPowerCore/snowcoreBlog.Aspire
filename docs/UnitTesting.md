# Unit testing

snowcoreBlog tests run on **xUnit v3** with the native
**Microsoft.Testing.Platform (MTP)** runner, **NSubstitute** for mocking,
**FluentAssertions** for assertions and **bUnit** for Blazor components.
The `unit-test-author` skill packages the same rules for AI agents.

## Platform setup (repo-wide, do not change per-project)

- `global.json`: `"test": { "runner": "Microsoft.Testing.Platform" }`.
- Root `Directory.Build.props`: every project whose name ends in `.Tests`
  gets `OutputType=Exe` + `UseMicrosoftTestingPlatformRunner=true` — test
  projects are executables, not classic libraries.

## Conventions

| Rule | Value |
|---|---|
| Test class | `<TypeName>Tests` |
| Test method | `MethodUnderTest_Scenario_ExpectedResult` |
| Structure | Arrange-Act-Assert with `// Arrange` (when needed), `// Act`, `// Assert` comments |
| Mocking | NSubstitute (`Substitute.For<T>()`) at service/repository seams |
| Assertions | FluentAssertions (`Should()`) |
| Components | bUnit (`RenderComponent<T>`) |
| Location | `<Area>.Tests` project next to the code it covers |

## Anatomy

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

    [Theory]
    [InlineData(null, false)]
    [InlineData("reader", true)]
    public void CanEnter_WithRole_ReturnsExpected(string? role, bool expected)
    {
        // Arrange
        var sut = new AccessEvaluator();

        // Act
        var actual = sut.CanEnter(role);

        // Assert
        actual.Should().Be(expected);
    }
}
```

## Mocking with NSubstitute

```csharp
var repo = Substitute.For<IArticleRepository>();
repo.GetAsync(id, Arg.Any<CancellationToken>()).Returns(article);

await sut.Run(id, CancellationToken.None);

await repo.Received(1).GetAsync(id, Arg.Any<CancellationToken>());
```

- Mock seams, not infrastructure: never `new Mock<HttpClient>()`-style mocking
  of `HttpClient` — fake the `HttpMessageHandler` or inject a service seam.
- `Received(1)` verifies calls; `.Returns()` / `.ReturnsAsync()` stub behavior.

## Determinism

- Inject `TimeProvider` instead of `DateTime.Now`/`UtcNow`; fake it in tests
  (`FakeTimeProvider`) so timing-dependent flows are stable.
- No real network, filesystem or `Task.Delay`-based synchronization; use the
  injected seams.

## Value-type conditional trap

When a conditional targets a **nullable value type**, `default` binds to the
natural (non-nullable) type — `cond ? default : status` yields `0`, never
`null`. Write `null` literally:

```csharp
StatusCode? code = allowed ? null : 403;   // NOT: allowed ? default : 403
```

## Running tests

Per-project, never solution-wide:

```bash
dotnet test snowcoreBlog.Frontend/Tests/Core.Tests/snowcoreBlog.Frontend.Core.Tests.csproj
```

MTP executables take different arguments than the legacy VSTest runner:

```bash
# class / method filters (VSTest --filter does NOT work)
dotnet test <test-project>.csproj --no-build --filter-class '*RegisterState*'
dotnet test <test-project>.csproj --no-build --filter-method '*SetsBusyFlag*'
```

Notes:

- `dotnet test` builds first by default; combine with `--no-build` after a build.
- TRX reports need the MTP logger extension package.
- If results look impossible (e.g. tests that cannot have passed), rebuild the
  test project explicitly — a stale MTP exe prints old-format summaries.
