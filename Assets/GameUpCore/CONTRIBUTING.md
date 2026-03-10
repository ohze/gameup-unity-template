# Contributing to GameUp Core Framework

## Coding Conventions

### Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Namespace | PascalCase | `GameUp.Core`, `GameUp.UI` |
| Class / Struct | PascalCase | `AudioManager`, `UIScreen` |
| Interface | I + PascalCase | `IState`, `IPoolable`, `ITransition` |
| Public Method | PascalCase | `ChangeState<T>()`, `Show()` |
| Private Field | _camelCase | `_instance`, `_isRunning` |
| SerializeField | _camelCase | `[SerializeField] float _duration` |
| Property | PascalCase | `IsVisible`, `CurrentState` |
| Enum | PascalCase | `UILayer.Popup`, `LogLevel.Warning` |
| Constant | PascalCase | `GameLayer`, `UILayer` |
| Local Variable | camelCase | `elapsed`, `targetVolume` |
| Parameter | camelCase | `sceneName`, `onComplete` |

### XML Documentation

All public APIs **must** have XML docs (`///`). This is critical for UPM packages since Inspector tooltips depend on them.

```csharp
/// <summary>Load a scene asynchronously with progress callback.</summary>
/// <param name="sceneName">Name of the scene to load.</param>
/// <param name="onProgress">Called each frame with progress (0-1).</param>
public static void LoadScene(string sceneName, Action<float> onProgress = null) { }
```

### Code Style

- Use `var` when type is obvious from context
- Prefer expression-bodied members for simple one-liners
- Keep files focused: one primary class per file
- Use `sealed` on classes not designed for inheritance
- Avoid `public` fields — use `[SerializeField]` private with properties

## Branch Strategy

- `main` — stable, tagged releases only
- `develop` — integration branch
- `feature/<name>` — individual features
- `fix/<name>` — bug fixes

## Merge Request Flow

1. Create branch from `develop`: `feature/ui-toast`
2. Implement + write/update tests
3. Ensure all tests pass in Test Runner
4. Create MR to `develop`
5. Assign reviewer (at least 1)
6. After approval, squash merge

## Assembly Definitions

- New Runtime code goes in `Runtime/Core/` or `Runtime/UI/`
- Do NOT add cross-references between Core and UI (UI references Core, not vice versa)
- Editor code goes in `Editor/` with platform restriction
- Test code goes in `Tests/Runtime/` or `Tests/Editor/`

## Release Process

1. Merge `develop` into `main`
2. Update version in `package.json`
3. Update `CHANGELOG.md`
4. Create git tag: `git tag v0.1.0`
5. Push tag: `git push origin v0.1.0`
