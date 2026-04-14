# CoinFly CurrencyHelperView Guide

File chinh: `Assets/_MainProject/Scripts/UI/CoinFly/CurrencyHelperView.cs`

## 1) Cai helper package cho CoinFly

1. Mo Unity Editor.
2. Vao menu `GameUp/Project/Helper Package Installer`.
3. Chon `Helper Module = CoinFly`.
4. Bam `Download & Auto Install CoinFly Helpers`.
5. Cho tool tai va import lan luot 2 package:
   - `CoinFlyText.unitypackage`
   - `UIParticleImage.unitypackage`

Neu can mo link thu cong, bam `Open Module Package URLs`.

## 2) Setup prefab/UI

Gan component `CurrencyHelperView` vao UI holder cua currency (vi du top bar).

Can map cac field sau trong Inspector:

- `currencyText`: TMP hien so tien hien tai.
- `currencyIcon`: icon currency muc tieu.
- `particleImage`: component `ParticleImage` de bay coin.
- `currencyDeltaTextPrefab`: TMP prefab de hien `+x` hoac `-x`.
- `deltaTextParent`: parent cho delta text (co the de trong de dung parent cua `currencyText`).

Nen giu `autoInitOnAwake = true` de effect tu khoi tao.

## 3) API su dung nhanh

### Khoi tao so du

```csharp
currencyHelperView.SetCurrency(1000f);
```

### Cong currency + coin fly

```csharp
currencyHelperView.AddCurrency(
    amount: 250f,
    fromTransform: rewardChestTransform,
    onStepUpdated: current => { /* update state neu can */ },
    onCompleted: current => { /* save/tiep theo flow */ });
```

- `fromTransform`: diem phat coin bay.
- `onStepUpdated`: callback moi dot coin ve icon.
- `onCompleted`: callback khi ket thuc hieu ung.

### Tru currency

```csharp
currencyHelperView.SubtractCurrency(120f);
```

### Doi icon currency runtime

```csharp
currencyHelperView.SetCurrencyIcon(newCurrencySprite);
```

## 4) Luu y

- `AddCurrency` bo qua khi `amount <= 0`.
- `SubtractCurrency` bo qua khi `amount <= 0`.
- `SetEffectCount(int count)` se tu dong clamp toi thieu `1`.
- Component dung `GUPoolers` de spawn/despawn delta text, can dam bao `GUPoolers` da co trong scene.
