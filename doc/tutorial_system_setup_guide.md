# Tutorial System Setup Guide

Phan nay huong dan setup he thong tutorial theo code hien tai trong:

- `Assets/_MainProject/Scripts/Core/Helpers/DestinationPoint.cs`
- `Assets/_MainProject/Scripts/Tutorial/`

## 0) Cai dat helper package Tutorial (nen lam truoc)

File tool editor:
- `Assets/GameUpCore/Editor/GUHelperPackageInstallerWindow.cs`

Thao tac:
1. Mo Unity Editor.
2. Vao menu `GameUp/Project/Helper Package Installer`.
3. Tai field `Helper Module`, chon `Tutorial`.
4. Bam nut `Download & Auto Install Tutorial Helpers`.
5. Cho tool tai va import package `TutorialByDuyLV.unitypackage`.
6. Cho den khi status hien thong bao thanh cong cho module `Tutorial`.

Neu can tai thu cong:
- Bam `Open Module Package URLs` de mo link release package.

## 1) Tong quan luong chay

1. Dat cac moc trong scene bang component `DestinationPoint`.
2. Tao `SOTutorialStep` de mo ta tung buoc (focus/talk/arrow/hand drag).
3. Gom cac step vao `SOTutorialType`.
4. Dang ky cac `SOTutorialType` trong `SOTutorialController` singleton asset.
5. Goi `TutorialController.RunTutorial(...)` de chay.
6. Khi user click vao target (single focus/arrow), step duoc `MarkComplete()` va chuyen buoc tiep theo.

## 2) Setup DestinationPoint trong Scene

File lien quan: `Assets/_MainProject/Scripts/Core/Helpers/DestinationPoint.cs`

- Gan `DestinationPoint` vao object can duoc huong dan (UI hoac world object).
- Chon `pointType` phu hop (`Tutorial_1`, `Tutorial_2`, ...).
- Moi type co the co nhieu object:
  - `GetFirstDestination(type)` lay object dau tien duoc register.
  - `GetLastDestination(type)` lay object cuoi cung duoc register.
- Object bi disable/destroy se tu dong duoc remove khoi cache static.

Luu y:
- Neu 1 step dung `FocusType.Multi` hoac `useHandDrag = true`, can set du 2 destination (`destinationPoint` + `destinationPoint2`).
- Neu destination null, cac view co the khong hien thi (do script co check null truoc khi ve UI).

## 3) Tao du lieu tutorial (ScriptableObject)

### 3.1 Tao `SOTutorialStep`

File: `Assets/_MainProject/Scripts/Tutorial/Data/SOTutorialStep.cs`

Tao asset qua menu:
- `Create/Data/Tutorial/TutorialStep`

Field chinh:
- `stepName`: ten de phan biet.
- `useFocus`: bat/tat vung focus.
- `focusType`: `Single` hoac `Multi`.
- `destinationPoint`, `destinationPoint2`: map voi `DestinationType`.
- `useTalk`, `talkText`: bat text huong dan.
- `useArrow`: hien mui ten.
- `useHandDrag`: bat hand drag animation (neu bat, he thong khong spawn arrow).
- `arrowDirection`: huong mui ten (chi dung khi `useArrow = true` va `useHandDrag = false`).

Custom inspector trong `SOTutorialStepEditor` se an/hien field theo tung mode, nen setup trong Inspector se gon hon.

### 3.2 Tao `SOTutorialType`

File: `Assets/_MainProject/Scripts/Tutorial/Data/SOTutorialType.cs`

Tao asset qua menu:
- `Create/Data/Tutorial/TutorialType`

Gan:
- `tutorialType`: enum id cua tutorial flow.
- `tutorialSteps`: danh sach cac `SOTutorialStep` theo thu tu chay.

Trang thai hoan thanh duoc luu local theo key:
- `Tut_{tutorialType}`

### 3.3 Dang ky vao `SOTutorialController`

File: `Assets/_MainProject/Scripts/Tutorial/Data/SOTutorialController.cs`

Tao/gan singleton asset:
- `Create/Data/Tutorial/TutorialController`
- Them cac `SOTutorialType` vao list `tutorialTypes`.

Trong project hien tai da co asset mau:
- `Assets/_MainProject/Data/Singletons/TutorialController.asset`

## 4) Setup Prefab controller UI tutorial

Prefab lien quan:
- `Assets/_MainProject/Prefabs/UI/Helpers/Tutorial/TutorialControllder.prefab`
- `Assets/_MainProject/Prefabs/UI/Helpers/Tutorial/TutorialArrow.prefab`
- `Assets/_MainProject/Prefabs/UI/Helpers/Tutorial/TalkTutorial.prefab`

Can dam bao trong scene co instance cua `TutorialController` va map du reference:
- `talkTutorial`
- `focusItem`
- `multiFocusItem`
- `arrowContainer`
- `tutorialArrowPrefab`
- `handDragPrefab`

Neu dung world object cho focus/arrow:
- Can co `Renderer` hoac `Collider` de script tinh bounds.
- Neu canvas khong phai overlay, can camera hop le cho canvas/world.

## 5) Cach goi chay tutorial

Tu code:

```csharp
TutorialController.Instance.RunTutorial(TutorialType.TutorialTest_1);
```

Hoac truyen truc tiep `SOTutorialType`:

```csharp
TutorialController.Instance.RunTutorial(tutorialTypeAsset);
```

Trong qua trinh chay:
- He thong cho den khi khong con popup (`!UIPopup.IsPopupOn`) moi bat dau.
- Moi step se clear view cu, show lai theo config moi.
- Khi step complete, script auto clean focus/talk/arrow/handdrag truoc khi sang step tiep.
- Ket thuc flow: `tutorial.SetComplete()` va check tat ca tutorial da xong hay chua.

## 6) Rule complete step (quan trong)

- `FocusType.Single`: script them `TutorialClickHandler` vao target, click/down vao target se goi `MarkComplete()`.
- `useArrow = true` (khong hand drag): neu chua co click handler thi script se gan them de user click complete.
- `FocusType.Multi` hoac `useHandDrag`: mac dinh khong auto gan click complete cho 2 target.
  - Truong hop nay thuong can complete bang logic ngoai (goi `TutorialController.MarkComplete()` khi dieu kien game dat).

## 7) Them tutorial moi (checklist nhanh)

1. Them enum moi trong:
   - `DestinationType` (`DestinationPoint.cs`) neu can moc destination moi.
   - `TutorialType` (`SOTutorialType.cs`) neu can flow tutorial moi.
2. Dat `DestinationPoint` dung type len object trong scene.
3. Tao cac `SOTutorialStep` va cau hinh tung step.
4. Tao `SOTutorialType`, sap xep danh sach step.
5. Them `SOTutorialType` vao `TutorialController.asset`.
6. Goi `RunTutorial()` tai diem bat dau mong muon.
7. Test lai:
   - case da complete (co local key),
   - case reset chua complete,
   - case target bi an/disable.

## 8) Vi du setup 1 tutorial 3 buoc

Muc tieu vi du:
- Buoc 1: focus + talk vao nut `Play`.
- Buoc 2: arrow chi vao panel `Mission`.
- Buoc 3: hand drag tu item `A` sang slot `B`.

### 8.1 Chuan bi destination trong scene

Gan `DestinationPoint`:
- `PlayButton` -> `pointType = Tutorial_1`
- `MissionPanel` -> `pointType = Tutorial_2`
- `DragItemA` -> `pointType = Tutorial_3`
- `DragSlotB` -> `pointType = Tutorial_4`

### 8.2 Tao 3 `SOTutorialStep`

`TutorialStep_01_PlayButton`
- `stepName`: `Tap Play`
- `useFocus`: `true`
- `focusType`: `Single`
- `destinationPoint`: `Tutorial_1`
- `useTalk`: `true`
- `talkText`: `Nhan vao Play de bat dau!`
- `useArrow`: `false`

`TutorialStep_02_MissionPanel`
- `stepName`: `Open Mission`
- `useFocus`: `false`
- `useTalk`: `true`
- `talkText`: `Day la khu vuc nhiem vu.`
- `useArrow`: `true`
- `useHandDrag`: `false`
- `arrowDirection`: `Up` (doi huong neu can)
- `destinationPoint`: `Tutorial_2`

`TutorialStep_03_DragItem`
- `stepName`: `Drag Item`
- `useFocus`: `true`
- `focusType`: `Multi`
- `destinationPoint`: `Tutorial_3`
- `destinationPoint2`: `Tutorial_4`
- `useTalk`: `true`
- `talkText`: `Keo vat pham vao o trong.`
- `useArrow`: `true`
- `useHandDrag`: `true`

Luu y cho buoc 3:
- Step `Multi` + `useHandDrag` khong auto complete bang click.
- Can goi `TutorialController.Instance.MarkComplete()` khi game xac nhan da drag thanh cong.

### 8.3 Tao `SOTutorialType` va dang ky controller

Tao asset `SOTutorialType` (vi du `TutorialType_Onboarding_01`):
- `tutorialType`: chon enum mong muon (vi du `TutorialTest_1`)
- `tutorialSteps`:
  1. `TutorialStep_01_PlayButton`
  2. `TutorialStep_02_MissionPanel`
  3. `TutorialStep_03_DragItem`

Mo `Assets/_MainProject/Data/Singletons/TutorialController.asset`:
- them `TutorialType_Onboarding_01` vao list `tutorialTypes`.

### 8.4 Goi chay

```csharp
TutorialController.Instance.RunTutorial(TutorialType.TutorialTest_1);
```

Neu buoc 3 complete bang gameplay event:

```csharp
private void OnDragSuccess()
{
    TutorialController.Instance.MarkComplete();
}
```

## 9) Debug va reset khi test

- Reset 1 tutorial:
  - goi `SOTutorialController.Instance.UnComplete(tutorialType);`
- Reset tung step + trang thai tutorial:
  - goi `SOTutorialType.UnCompleteStep()` tren asset tuong ung.
- Force complete current step:
  - goi `TutorialController.Instance.MarkComplete();`

Neu tutorial khong hien:
- Kiem tra destination co dang active va da gan dung `DestinationType`.
- Kiem tra `SOTutorialController` co dang ky dung `SOTutorialType`.
- Kiem tra `TutorialController` prefab co du reference (nhat la `tutorialArrowPrefab`, `handDragPrefab`, `talkTutorial`).
