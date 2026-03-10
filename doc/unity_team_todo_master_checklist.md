# Unity Team Master TODO Checklist

Checklist tổng hợp toàn bộ việc cần làm cho dự án `gameup-unity-template`, dùng để theo dõi tiến độ team 8 người (1 lead + 7 dev).

---

## 1) Setup dự án và quy trình

- [ ] Chốt git workflow: `main`, `develop`, `feature/<module>-<owner>`
- [ ] Chốt Definition of Done (self-test, docs, ping lead review)
- [ ] Tạo template PR + checklist review
- [ ] Chốt coding conventions (naming, folder, namespace, XML docs)
- [ ] Chốt rule dependency asmdef (Core -> UI một chiều, Extensions độc lập)
- [ ] Chốt format báo cáo daily và cách cập nhật tiến độ

---

## 2) Cấu trúc folder, package, asmdef

### 2.1 Folder
- [ ] Hoàn thiện cấu trúc folder theo `doc/unity_team_execution_plan.md`
- [ ] Tách rõ `Runtime/Core`, `Runtime/UI`, `Editor`, `Tests`, `Samples~`, `Documentation~`
- [ ] Chuẩn hóa `Assets/Extensions` theo hướng độc lập dùng lại

### 2.2 Package
- [ ] Khai báo `Assets/GameUpCore/package.json` đúng thông tin package
- [ ] Kiểm tra package cài đặt đủ: `com.unity.ugui`, `com.unity.textmeshpro`, `com.unity.test-framework`
- [ ] Xác nhận DOTween dùng ổn trong dự án (cho Transition/FX UI)
- [ ] Xác nhận package khuyến nghị (Addressables/Localization/InputSystem) theo scope thực tế

### 2.3 ASMDEF
- [ ] `Assets/GameUpCore/Runtime/Core/GameUp.Core.Runtime.asmdef`
- [ ] `Assets/GameUpCore/Runtime/UI/GameUp.UI.Runtime.asmdef`
- [ ] `Assets/GameUpCore/Editor/GameUp.Core.Editor.asmdef`
- [ ] `Assets/GameUpCore/Tests/Runtime/GameUp.Core.Tests.Runtime.asmdef`
- [ ] `Assets/GameUpCore/Tests/Editor/GameUp.Core.Tests.Editor.asmdef`
- [ ] `Assets/Extensions/GameUp.Extensions.asmdef`
- [ ] Verify references đúng theo kiến trúc đã thống nhất

---

## 3) Core modules cần triển khai

### 3.1 Foundation
- [ ] Singleton
- [ ] CoroutineRunner
- [ ] TimeSystem
- [ ] GameExtension
- [ ] StringUtils
- [ ] GameUtils

### 3.2 Runtime services
- [ ] EventBus
- [ ] SaveLoad
- [ ] SceneLoader
- [ ] ObjectPool
- [ ] Audio

### 3.3 Chất lượng Core
- [ ] Unit test/smoke test cho từng module Core
- [ ] Demo usage ngắn cho mỗi module Core
- [ ] XML docs cho public API Core

---

## 4) UI framework cần triển khai

### 4.1 UI base
- [ ] UI Manager
- [ ] Multi resolution / đa màn hình (CanvasScaler + safe area + anchor policy)
- [ ] Popup
- [ ] Navigation

### 4.2 UI transitions + components
- [ ] Transition (DOTween adapter ưu tiên)
- [ ] Components base
- [ ] NotifyBar
- [ ] Toast
- [ ] SelectView
- [ ] Button
- [ ] CustomSelectButton
- [ ] CustomSelectViews
- [ ] ItemMoveHelper (hiệu ứng tiền bay)

### 4.3 Chất lượng UI
- [ ] Test luồng UI chính (mở/đóng screen, popup stack, transition)
- [ ] Test đa tỉ lệ màn hình và safe area
- [ ] Demo scene UI đầy đủ

---

## 5) Timeline thực thi theo phase (không gắn ngày cụ thể)

### Phase 0 - Bootstrap
- [ ] Chốt conventions, workflow, ownership
- [ ] Chốt kiến trúc folder/package/asmdef

### Phase 1 - Core Foundation
- [ ] Hoàn thành nhóm Foundation Core
- [ ] Verify chạy độc lập từng module

### Phase 2 - Core Data/Flow Services
- [ ] Hoàn thành EventBus, SaveLoad, SceneLoader, ObjectPool, Audio
- [ ] Verify tích hợp chéo services

### Phase 3 - UI Base
- [ ] Hoàn thành Manager, Navigation, Popup, MultiResolution
- [ ] Verify luồng điều hướng chuẩn

### Phase 4 - UI Components and FX
- [ ] Hoàn thành Transition + toàn bộ UI components yêu cầu
- [ ] Verify animation/UX consistency

### Phase 5 - Integration and Hardening
- [ ] Tích hợp full Core + UI vào sample scenes
- [ ] Chạy smoke/regression tests
- [ ] Fix lỗi vòng đời, race condition, null safety

### Phase 6 - Docs and Release Readiness
- [ ] Hoàn thiện docs sử dụng toàn bộ module
- [ ] Hoàn thiện checklist bàn giao
- [ ] Chốt release nội bộ

---

## 6) Checklist bắt buộc cho mỗi task của dev

- [ ] Self-test trước khi tạo PR (happy path + edge cases chính)
- [ ] Không làm hỏng scene/sample hiện có
- [ ] Cập nhật doc usage cho phần vừa làm
- [ ] Tạo PR có checklist rõ ràng
- [ ] Ping lead review code + review docs
- [ ] Chỉ merge khi được approve

---

## 7) Checklist cho lead

- [ ] Review kiến trúc và dependency trước khi merge
- [ ] Review contract API module giao nhau
- [ ] Review kết quả self-test và bằng chứng test
- [ ] Review tài liệu sử dụng tương ứng task
- [ ] Chặn merge nếu thiếu test/doc/chuẩn code
- [ ] Tổng hợp risk và blocker cuối mỗi phase

---

## 8) Deliverables cuối mỗi phase

- [ ] Code đã merge theo module owner
- [ ] Checklist self-test được cập nhật
- [ ] Tài liệu sử dụng cập nhật đầy đủ
- [ ] Demo scene hoạt động ổn ở mức tích hợp phase

