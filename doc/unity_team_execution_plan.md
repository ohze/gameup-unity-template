# Kế hoạch triển khai Unity Template cho team Dev


## 1) Mục tiêu và nguyên tắc triển khai

- Giữ Core độc lập, UI phụ thuộc Core, Extensions tách rời để tái sử dụng.
- Dự án đã có DOTween: dùng DOTween cho animation UI (Transition, ItemMoveHelper, Notify/Toast animation), nhưng interface transition vẫn giữ abstraction để không khóa framework.
- Definition of Done bắt buộc cho mọi task:
  - Dev tự test lại chức năng + smoke test các luồng liên quan.
  - Dev tự viết/ cập nhật tài liệu sử dụng ngắn gọn (API usage + prefab setup nếu có).
  - Dev ping lead để review code + review tài liệu trước khi merge.

## 2) Cách thức tiếp cận dự án (team workflow)

- Mô hình nhánh:
  - `main`: luôn xanh (build/test pass).
  - `develop` (khuyến nghị): tích hợp theo phase.
  - `feature/<module>-<owner>`: từng module/tính năng.
- Quy trình mỗi task:
  - Nhận ticket -> làm trên nhánh feature -> self-test -> cập nhật docs -> tạo PR -> ping lead review.
  - Chỉ merge khi pass checklist DoD và không phá asmdef dependency.
- Cadence phối hợp:
  - Daily sync ngắn theo blocker.
  - Cuối mỗi phase có integration checkpoint (merge + test hồi quy).
- Quy chuẩn code:
  - Public API có XML docs.
  - Có sample usage tối thiểu cho module mới.
  - Không thêm coupling chéo trái kiến trúc (UI không bị gọi ngược bởi Core runtime).

## 3) Cấu trúc team 8 người

- Lead: kiến trúc, review cuối, quản lý dependency, quyết định tiêu chuẩn API.
- Các Dev chia vai trò:
  - Dev A: Core Foundations (Singleton, CoroutineRunner, TimeSystem)
  - Dev B: EventBus + SceneLoader
  - Dev C: SaveLoad + GameUtils
  - Dev D: ObjectPool
  - Dev E: Audio
  - Dev F: UI Manager/Navigation
  - Dev G: UI Components/Transition + ItemMoveHelper
- Quy tắc ownership:
  - Mỗi module có 1 owner chính và 1 backup reviewer chéo.
  - Task giao nhau phải chốt contract trước (interface, event payload, lifecycle).

## 4) Cấu trúc folder đề xuất triển khai

Giữ theo plan hiện tại, bổ sung rõ khu vực UI components mở rộng:

```text
Assets/
├── GameUpCore/
│   ├── Runtime/
│   │   ├── Core/
│   │   │   ├── Singleton/
│   │   │   ├── EventBus/
│   │   │   ├── TimeSystem/
│   │   │   ├── SceneLoader/
│   │   │   ├── SaveLoad/
│   │   │   ├── ObjectPool/
│   │   │   ├── CoroutineRunner/
│   │   │   ├── Audio/
│   │   │   ├── Utils/
│   │   │   └── Extensions/
│   │   └── UI/
│   │       ├── Manager/
│   │       ├── Navigation/
│   │       ├── Transition/
│   │       ├── Components/
│   │       │   ├── Common/
│   │       │   ├── NotifyBar/
│   │       │   ├── Toast/
│   │       │   ├── SelectView/
│   │       │   ├── Button/
│   │       │   ├── CustomSelectButton/
│   │       │   ├── CustomSelectViews/
│   │       │   └── Helpers/
│   │       └── MultiResolution/
│   ├── Editor/
│   │   ├── Core/
│   │   ├── UI/
│   │   └── Tools/
│   ├── Tests/
│   │   ├── Runtime/
│   │   │   ├── Core/
│   │   │   └── UI/
│   │   └── Editor/
│   ├── Samples~/
│   │   ├── CoreDemo/
│   │   └── UIDemo/
│   └── Documentation~/
├── Extensions/
│   ├── Runtime/
│   │   ├── StringUtils/
│   │   ├── GameExtension/
│   │   └── GameUtils/
│   ├── Tests/
│   └── Documentation~/
└── Project/
    ├── Scenes/
    ├── Prefabs/
    ├── Art/
    ├── Audio/
    └── Resources/
```

### 4.0) Sơ đồ tổng quan cấu trúc

```mermaid
flowchart TD
    assetsRoot[Assets]
    gameUpCore[GameUpCore]
    extensionsRoot[Extensions]
    projectRoot[Project]

    assetsRoot --> gameUpCore
    assetsRoot --> extensionsRoot
    assetsRoot --> projectRoot

    gameUpCore --> runtimeRoot[Runtime]
    gameUpCore --> editorRoot[Editor]
    gameUpCore --> testsRoot[Tests]
    gameUpCore --> samplesRoot[Samples~]
    gameUpCore --> docsRoot[Documentation~]

    runtimeRoot --> coreRuntimeFolder[Core]
    runtimeRoot --> uiRuntimeFolder[UI]

    uiRuntimeFolder --> managerFolder[Manager]
    uiRuntimeFolder --> navigationFolder[Navigation]
    uiRuntimeFolder --> transitionFolder[Transition]
    uiRuntimeFolder --> componentsFolder[Components]
    uiRuntimeFolder --> multiResolutionFolder[MultiResolution]

    componentsFolder --> notifyBarFolder[NotifyBar]
    componentsFolder --> toastFolder[Toast]
    componentsFolder --> selectViewFolder[SelectView]
    componentsFolder --> buttonFolder[Button]
    componentsFolder --> customSelectButtonFolder[CustomSelectButton]
    componentsFolder --> customSelectViewsFolder[CustomSelectViews]
    componentsFolder --> helpersFolder[Helpers]
```

### 4.1) ASMDEF cần có (bắt buộc)

- `GameUp.Core.Runtime`
  - Vị trí: `Assets/GameUpCore/Runtime/Core/`
  - References: không bắt buộc package nội bộ khác.
- `GameUp.UI.Runtime`
  - Vị trí: `Assets/GameUpCore/Runtime/UI/`
  - References: `GameUp.Core.Runtime`
- `GameUp.Core.Editor`
  - Vị trí: `Assets/GameUpCore/Editor/`
  - Include Platforms: `Editor`
  - References: `GameUp.Core.Runtime`, `GameUp.UI.Runtime`
- `GameUp.Core.Tests.Runtime`
  - Vị trí: `Assets/GameUpCore/Tests/Runtime/`
  - Optional Unity References: `TestAssemblies`
  - References: `GameUp.Core.Runtime`, `GameUp.UI.Runtime`
- `GameUp.Core.Tests.Editor`
  - Vị trí: `Assets/GameUpCore/Tests/Editor/`
  - Include Platforms: `Editor`
  - Optional Unity References: `TestAssemblies`
  - References: `GameUp.Core.Editor`
- `GameUp.Extensions`
  - Vị trí: `Assets/Extensions/`
  - References: độc lập (không phụ thuộc `GameUp.Core.Runtime`)

### 4.2) Dependency rule giữa các ASMDEF

- Core không tham chiếu UI.
- UI chỉ tham chiếu Core (1 chiều).
- Editor chỉ chạy ở Editor và có thể tham chiếu Core/UI.
- Test Runtime không tham chiếu Editor.
- Extensions tách rời để dùng lại ở cả project.

```mermaid
flowchart LR
    coreAsm[GameUp.Core.Runtime]
    uiAsm[GameUp.UI.Runtime]
    editorAsm[GameUp.Core.Editor]
    testRuntimeAsm[GameUp.Core.Tests.Runtime]
    testEditorAsm[GameUp.Core.Tests.Editor]
    extAsm[GameUp.Extensions]

    uiAsm --> coreAsm
    editorAsm --> coreAsm
    editorAsm --> uiAsm
    testRuntimeAsm --> coreAsm
    testRuntimeAsm --> uiAsm
    testEditorAsm --> editorAsm
    extAsm -. standalone .-> coreAsm
```

### 4.3) Package cần thiết

- Bắt buộc:
  - `com.unity.textmeshpro` (UI text)
  - `com.unity.ugui` (UI cơ bản)
- Cho test:
  - `com.unity.test-framework` (Runtime/Editor tests)
- Cho animation UI (đã có sẵn trong dự án):
  - `DOTween` (Demigiant) dùng cho `Transition`, `Toast`, `NotifyBar`, `ItemMoveHelper`

### 4.4) Package khuyến nghị theo nhu cầu mở rộng

- `com.unity.addressables` (nếu tải prefab/ui/audio động)
- `com.unity.localization` (nếu hỗ trợ đa ngôn ngữ)
- `com.unity.inputsystem` (nếu dự án dùng Input System mới)

```mermaid
flowchart LR
    packages[Packages]
    requiredPkgs[BắtBuộc]
    testPkgs[ChoTest]
    uiAnimPkgs[ChoUIAnimation]
    optionalPkgs[KhuyếnNghịMởRộng]

    packages --> requiredPkgs
    packages --> testPkgs
    packages --> uiAnimPkgs
    packages --> optionalPkgs

    requiredPkgs --> uguiPkg[com.unity.ugui]
    requiredPkgs --> tmpPkg[com.unity.textmeshpro]
    testPkgs --> testFrameworkPkg[com.unity.test-framework]
    uiAnimPkgs --> dotweenPkg[DOTween]
    optionalPkgs --> addressablesPkg[com.unity.addressables]
    optionalPkgs --> localizationPkg[com.unity.localization]
    optionalPkgs --> inputSystemPkg[com.unity.inputsystem]
```

## 5) Checklist triển khai theo module

### Core

- Singleton
- EventBus
- TimeSystem
- SceneLoader
- SaveLoad
- ObjectPool
- CoroutineRunner
- Audio
- GameExtension
- StringUtils
- GameUtils

### UI

- UI Manager
- Multi resolution / đa màn hình (CanvasScaler + safe area + anchor policy)
- Popup
- Navigation
- Transition (ưu tiên DOTween adapter)
- Components base
- NotifyBar
- Toast
- SelectView
- Button
- CustomSelectButton
- CustomSelectViews
- ItemMoveHelper (hiệu ứng tiền bay)

### Chất lượng & tài liệu

- Unit test/smoke test cho module trọng yếu
- Demo scene tích hợp
- README module/API usage
- Checklist review + merge gate

## 6) Tiến trình triển khai (không gắn số ngày)

```mermaid
flowchart TD
    Phase0[Phase0_Bootstrap] --> Phase1[Phase1_CoreFoundation]
    Phase1 --> Phase2[Phase2_DataAndFlow]
    Phase2 --> Phase3[Phase3_UIBase]
    Phase3 --> Phase4[Phase4_UIComponentsAndFX]
    Phase4 --> Phase5[Phase5_IntegrationAndHardening]
    Phase5 --> Phase6[Phase6_DocAndRelease]
```



- Phase0_Bootstrap:
  - Chốt coding convention, git flow, PR template, DoD, owner từng module.
  - Khóa kiến trúc asmdef/dependency.
- Phase1_CoreFoundation (làm trước):
  - Singleton, CoroutineRunner, TimeSystem, GameExtension/StringUtils/GameUtils.
  - Mục tiêu: tạo nền API dùng chung cho các module sau.
- Phase2_DataAndFlow:
  - EventBus, SaveLoad, SceneLoader, ObjectPool, Audio.
  - Mục tiêu: hoàn thành các dịch vụ runtime cốt lõi.
- Phase3_UIBase:
  - UI Manager, Navigation, Popup, Multi resolution.
  - Mục tiêu: khung UI ổn định, điều hướng chuẩn.
- Phase4_UIComponentsAndFX:
  - Transition (DOTween), NotifyBar, Toast, SelectView, Button, CustomSelectButton, CustomSelectViews, ItemMoveHelper.
  - Mục tiêu: hoàn thiện lớp reusable UI components và hiệu ứng.
- Phase5_IntegrationAndHardening:
  - Tích hợp toàn hệ thống vào sample scene.
  - Test hồi quy chéo module, xử lý race/lifecycle/null-safe.
- Phase6_DocAndRelease:
  - Hoàn thiện docs sử dụng theo module, onboarding notes.
  - Chốt release checklist, tag phiên bản nội bộ.

## 7) Cơ chế kiểm soát chất lượng bắt buộc cho từng dev

Mỗi dev khi hoàn thành task phải thực hiện đủ 3 bước trước khi yêu cầu merge:

- Tự test:
  - Test path + edge cases chính.
  - Verify không phá scene/sample hiện có.
- Tự viết docs:
  - Cập nhật hướng dẫn sử dụng module vừa làm (init, API chính, ví dụ gọi).
- Ping review:
  - Gửi PR + checklist self-test + link docs cập nhật.
  - Chỉ merge sau khi lead approve.

## 8) Ma trận triển khai song song

- Luồng song song 1 (Core nền): Dev A + Dev C
- Luồng song song 2 (Flow services): Dev B + Dev D + Dev E
- Luồng song song 3 (UI): Dev F + Dev G
- Lead:
  - Review theo thứ tự dependency: Foundation -> Services -> UI Base -> UI Components.
  - Chặn merge nếu thiếu self-test hoặc thiếu doc usage.

## 9) Deliverables cuối mỗi phase

- Code đã merge theo module owner.
- Checklist self-test của từng task.
- Tài liệu sử dụng cập nhật tương ứng.
- Demo scene chạy ổn ở mức tích hợp phase.

