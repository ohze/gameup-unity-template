# GameUp Core Framework

A modular Unity framework providing essential systems for game development: Singleton, EventBus, FSM, Object Pool, UI Manager, Audio, Save/Load, and more.

**Unity:** 2022.3+  
**Package:** `com.gameup.core` v0.1.0

---

## Installation

### Via Git URL (recommended)

In Unity Package Manager, add package from Git URL:

```
https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpCore#main
```

Or add directly to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.gameup.core": "https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpCore#main"
  }
}
```

### Via Local Path (development)

Clone the repo and open as a Unity project. The package at `Assets/GameUpCore/` is available immediately.

---

## Modules

### Core Systems

| Module | Namespace | Description |
|--------|-----------|-------------|
| **Singleton** | `GameUp.Core` | `Singleton<T>` and `PersistentSingleton<T>` (DontDestroyOnLoad) |
| **EventBus** | `GameUp.Core` | Type-safe static event bus with `EventBinding` (IDisposable) |
| **FSM** | `GameUp.Core` | Dictionary-based state machine with `IState` / `BaseState` |
| **ObjectPool** | `GameUp.Core` | Generic `ObjectPool<T>` and prefab-based `MonoPool` |
| **Save/Load** | `GameUp.Core` | `ISaveSystem` with `JsonSaveSystem` and `BinarySaveSystem` |
| **AudioManager** | `GameUp.Core` | BGM cross-fade + pool-based SFX via `AudioData` ScriptableObject |
| **ConfigLoader** | `GameUp.Core` | Load ScriptableObject or JSON configs from Resources |
| **SceneLoader** | `GameUp.Core` | Async scene loading with progress callback |
| **CoroutineRunner** | `GameUp.Core` | Global coroutine execution without scene MonoBehaviour |
| **Logger** | `GameUp.Core` | Tagged logger with conditional compilation (stripped in release) |
| **TimeSystem** | `GameUp.Core` | Per-layer custom time scale and Timer utility |

### UI Framework

| Module | Namespace | Description |
|--------|-----------|-------------|
| **UIManager** | `GameUp.UI` | Root canvas with layer-based sorting |
| **ScreenNavigator** | `GameUp.UI` | Stack-based Push/Pop screen navigation |
| **PopupStack** | `GameUp.UI` | LIFO popup management with overlay |
| **Transitions** | `GameUp.UI` | Fade, Slide, Scale transitions (ScriptableObject-based) |
| **SafeButton** | `GameUp.UI` | Button with double-click prevention |
| **LoadingScreen** | `GameUp.UI` | Progress bar with percentage text |
| **Toast** | `GameUp.UI` | Queue-based auto-hide notifications |
| **Dialog** | `GameUp.UI` | Confirm/Alert dialog with callbacks |
| **BannerNotification** | `GameUp.UI` | Slide-in banner from top |

### Extensions

| Module | Namespace | Description |
|--------|-----------|-------------|
| **TransformExtensions** | `GameUp.Extensions` | SetPositionX/Y/Z, ResetLocal, DestroyChildren |
| **VectorExtensions** | `GameUp.Extensions` | WithX/Y/Z, ToVector2/3, FlatDistance |
| **ListExtensions** | `GameUp.Extensions` | GetRandom, Shuffle, PopLast, AddUnique |
| **StringExtensions** | `GameUp.Extensions` | Truncate, ToMD5, ToTitleCase |
| **ColorExtensions** | `GameUp.Extensions` | WithAlpha, HexToColor, ToHex |
| **GameUtils** | `GameUp.Extensions` | WeightedRandom, Remap, IsInRange, Chance, Snap |

---

## Quick Start

### EventBus

```csharp
// Define event
struct PlayerDied : IEvent { public int Score; }

// Subscribe
var binding = EventBus<PlayerDied>.Subscribe(e => Debug.Log($"Score: {e.Score}"));

// Publish
EventBus<PlayerDied>.Publish(new PlayerDied { Score = 100 });

// Unsubscribe
binding.Dispose();
```

### FSM

```csharp
var fsm = new StateMachine();
fsm.AddState(new IdleState());
fsm.AddState(new RunState());
fsm.ChangeState<IdleState>();
// In Update: fsm.Update();
```

### Object Pool

```csharp
var pool = new ObjectPool<Bullet>(preWarm: 20);
var bullet = pool.Get();
// ... use bullet ...
pool.Release(bullet);
```

### UI Navigation

```csharp
// Push screen
UIManager.Instance.ScreenNavigator.Push(myScreen);

// Show popup
UIManager.Instance.PopupStack.Show(myPopup);

// Toast
Toast.Show("Hello World!");

// Dialog
Dialog.ShowConfirm("Title", "Message", result => { });
```

### Scene Loading

```csharp
SceneLoader.LoadScene("GameScene",
    onProgress: p => loadingBar.value = p,
    onComplete: () => Debug.Log("Loaded!"));
```

---

## Assembly Definitions

- `GameUp.Core.Runtime` — Core systems (no external dependencies)
- `GameUp.UI.Runtime` — UI framework (references Core)
- `GameUp.Core.Editor` — Editor tools (Editor platform only)
- `GameUp.Extensions` — Standalone extensions (no dependencies)

---

## License

Internal use — GameUp.
