# Changelog

All notable changes to the GameUp Core Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-03-10

### Added

#### Core Systems
- `Singleton<T>` and `PersistentSingleton<T>` — generic MonoBehaviour singletons
- `EventBus<T>` — type-safe static event bus with `EventBinding` (IDisposable)
- `StateMachine` with `IState` / `BaseState` — Dictionary-based FSM
- `ObjectPool<T>` and `MonoPool` — generic and prefab-based object pools
- `JsonSaveSystem` and `BinarySaveSystem` — strategy-based save/load
- `AudioManager` — BGM cross-fade + pool-based SFX with `AudioData` ScriptableObject
- `ConfigLoader` — ScriptableObject and JSON config loading
- `SceneLoader` — async scene loading with progress callback
- `SceneTransition` — loading screen integration with minimum display time
- `CoroutineRunner` — global coroutine execution
- `GLogger` — tagged conditional logger with `LogConfig` ScriptableObject
- `TimeManager` and `Timer` — per-layer time scale and countdown/stopwatch

#### UI Framework
- `UIManager` — root canvas with layer-based sorting (Background, Screen, Popup, Overlay, Toast)
- `ScreenNavigator` — stack-based Push/Pop screen navigation
- `PopupStack` — LIFO popup management with overlay
- `FadeTransition`, `SlideTransition`, `ScaleTransition` — ScriptableObject-based transitions
- `SafeButton` — double-click prevention
- `LoadingScreen` — progress bar with percentage
- `Toast` — queue-based auto-hide notifications
- `Dialog` — confirm/alert with callbacks
- `BannerNotification` — slide-in banner from top

#### Extensions
- `TransformExtensions`, `VectorExtensions`, `ListExtensions`, `StringExtensions`, `ColorExtensions`
- `GameUtils` — weighted random, remap, distance checks, chance, snap

#### Tests
- Unit tests for Singleton, EventBus, FSM, ObjectPool, Save/Load

#### Samples
- `CoreDemo` — demonstrates EventBus, Pool, FSM, Save/Load, Timer
- `UIDemo` — demonstrates screen navigation, popups, toast, dialog, banner
