# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), phiên bản theo [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Trình cài một nút Cursor × Unity MCP (`GameUp/Project/Cursor × Unity (MCP)`).** Cùng giao diện và cách chạy với trình cài của Claude Code, 7 bước kiểm tra → cài nếu thiếu → kiểm tra lại: Cursor trên máy, Unity CLI, server `unity` trong `~/.cursor/mcp.json` qua `unity mcp configure cursor --yes` (đã thử thật: gộp vào file có sẵn, giữ server khác; vẫn backup `mcp.gameup-backup.json`), 31 skill chính thức của Unity vào `~/.cursor/skills` (lấy từ cache plugin Claude Code, không có thì `git clone --depth 1` repo plugin), rules/skills GameUp + rule mới `unity-mcp.mdc`, `com.unity.pipeline`, Editor ready. `GameUp → Settings` có dòng trạng thái ngay dưới mục Cursor IDE.
  - Tách lõi chung `GUMcpInstallRunner` (vòng chạy, lệnh ngoài, log, tự chạy tiếp sau domain reload, bước Unity CLI / pipeline / Editor ready) và `GUMcpInstallerWindowBase` (giao diện); trình cài Claude Code và Cursor chỉ khai báo bước riêng. `GUClaudeUnityMcpStep` đổi tên thành `GUMcpInstallStep` (giữ `.meta`).
  - Guide `unity-mcp-guide.md` thêm mục 10 "Cursor × Unity MCP".
  - **Sửa "Cursor không thấy tool Unity dù server Connected":** Cursor chạy server MCP với cwd = thư mục home nên `unity mcp` không tìm ra Editor và trả 0 tool (đo thật: cwd=home → 0, cwd=project hoặc `--project-path` → 151; `UNITY_PROJECT_PATH` không tác dụng). Trình cài đặt args `["mcp", "--project-path", "${workspaceFolder}"]` — Cursor 3.20 thay biến bằng thư mục đang mở, một cấu hình user dùng cho mọi project. Không truyền được biến qua CLI (nó ghép thành `/home/<user>/${workspaceFolder}`) nên sửa thẳng mảng args của entry `unity`; bước kiểm tra chỉ báo XONG khi có `--project-path ${workspaceFolder}`. Hướng dẫn sau khi cài nhắc thoát hẳn Cursor, bật server, mở chat mới.

### Changed
- **Cursor dùng toàn bộ skill của bộ Claude.** Installer Cursor copy thẳng `Documentation~/claude/skills` sang `.cursor/skills` (12 skill: `gameup-core-api`, `gameup-sdk-api`, `gameup-iap-api`, `gameup-sdk-installer-flow`, `unity-*`) thay vì template `cursor-skills` riêng chỉ có 1 skill và đã lệch bản Claude — gỡ template đó, một nguồn cho cả hai IDE. Index `.claude/gameup-*/API_INDEX.md` nay tự sync cả khi project chỉ dùng Cursor (skill tra API cần nó); cài Cursor rules cũng sync luôn.
- **`GameUp → Settings`: nút "Mở thư mục .claude" / "Mở CLAUDE.md" chuyển về ngay dưới dòng Claude Code.** Trước đây chúng nằm ở hàng chung bên dưới mục Cursor nên nhìn như Cursor mở Claude; mục Cursor có nút riêng "Mở thư mục .cursor" / "Mở .cursorrules", hàng chung chỉ còn "Chọn lại".
- **BREAKING — 0.7.0 yêu cầu Unity 6 (`"unity": "6000.0"`).** Template chuyển sang Unity 6000.3.21f1; `package.json` khai báo đúng phụ thuộc Unity 6 đã resolve thật: `com.unity.addressables` 1.22.3 → **2.9.1** (Addressables 2.x không chạy dưới Unity 6), `com.unity.textmeshpro` 3.0.6 → **`com.unity.ugui` 2.0.0** (Unity 6 gộp TextMeshPro vào uGUI; assembly `Unity.TextMeshPro` giữ nguyên tên nên asmdef không đổi). Project còn ở 2022.3 hãy ghim Git URL về commit trước bản này.

### Added
- **Trình cài một nút Claude Code × Unity: Plugin + MCP (`GameUp/Project/Claude Code × Unity (Plugin + MCP)`).** Làm theo `Documentation~/unity-mcp-guide.md` (bản đã chạy thật), thay cho việc từng dev gõ tay ~8 lệnh CLI và dễ làm theo hướng dẫn cũ trên mạng.
  - 7 bước kiểm tra → cài nếu thiếu → kiểm tra lại, bước đã xong thì bỏ qua: Claude Code ≥ 2.1.257 (`claude update` khi cũ), marketplace `Unity-Technologies/unity-agent-plugin`, plugin `unity@unity-agent-plugin` (scope user, bật lại nếu đang tắt), Unity CLI channel beta, MCP server `unity-editor-mcp` (scope user, trỏ đường dẫn tuyệt đối của CLI), `com.unity.pipeline` qua `unity pipeline install`, và chờ Editor ready + MCP Connected (tối đa 5 phút).
  - Trạng thái đọc từ CLI thật (`claude plugin list --json`, `claude mcp get`, `unity status --json`), không dựa cờ "đã bấm". Lệnh chạy bất đồng bộ qua `GUExternalCommand` (không treo Editor, có timeout, stdin đóng để lệnh không treo chờ nhập), tự dò `~/.local/bin` vì Editor mở từ Hub thường thiếu PATH của shell.
  - Cửa sổ có thanh tiến độ, hoạt động hiện tại, badge từng bước, log lệnh + output (copy được), nút Dừng / Kiểm tra lại / Mở hướng dẫn, và hộp thoại liệt kê mọi lệnh trước khi chạy. Cài pipeline gây domain reload → trạng thái giữ trong `SessionState` và tự chạy tiếp.
  - Project Unity < 6: vẫn cài phần của máy dev, bỏ qua bước pipeline kèm cảnh báo. `GameUp → Settings` có dòng trạng thái + nút mở trình cài; template `CLAUDE.md` thêm luật dùng Unity MCP (sửa scene/prefab qua tool thay vì YAML, hỏi trước tool khó hoàn tác, tự recompile/đọc Console/chạy test).
- **AI đọc được Core, SDK và IAP khi cài qua Git UPM (`GameUp/Project/Sync GameUp source for AI`).** Git UPM giải nén package vào `Library/PackageCache/<tên>@<hash>` — thư mục bị chặn đọc trong `.claude/settings.json` và CLAUDE.md, tên còn đổi theo commit — nên Claude ở project consumer không thấy `UIScreen`/`UIPopup`, `AdsManager`, `GameUpAnalytics`, `MyIAPManager`, class nền cần kế thừa, và tự chế lại, gọi thẳng SDK bên thứ ba hoặc đoán sai chữ ký.
  - `GUCoreSourceMirror` xử lý cả `com.ohze.gameup.core`, `com.ohze.gameup.sdk`, `com.ohze.gameup.iap` (SDK/IAP đều reference Core nên tool nằm ở Core): chép source text (`.cs`, `.md`, `.asmdef`, `.json`; bỏ `.meta`, prefab, `Documentation~`, `Plugins`) sang `.claude/gameup-<core|sdk|iap>/src/`. Package embedded (`Assets/GameUp*`, thư mục thật trong `Packages/`) đọc được tại chỗ nên không chép; package bị gỡ thì xoá thư mục tương ứng (kiểm lại bằng AssetDatabase trước khi xoá).
  - `GUApiIndexBuilder` sinh `API_INDEX.md` cho từng package bằng reflection từ assembly runtime đang nạp: asmdef → namespace, bảng **class nền để kế thừa** (member abstract/virtual, gồm cả member kế thừa như `UIScreen`/`UIPopup`), prefab có sẵn, và từng type theo thư mục kèm summary `///`, link file nguồn, chữ ký `public`/`protected`/`[SerializeField]`. Editor tooling và Tests không đưa vào index.
  - Tự chạy khi Editor nạp (chỉ với project đã cài bộ công cụ Claude) — cài/gỡ/cập nhật SDK hay IAP đều gây domain reload nên AI được cập nhật ngay; chép lại khi version hoặc commit đổi; index chỉ ghi khi nội dung khác, stamp không chứa đường dẫn tuyệt đối nên không làm bẩn git diff giữa các máy. Cài/Cập nhật Claude toolkit cũng sync luôn. `GameUp → Settings` có dòng trạng thái từng package + nút *Đồng bộ*.
  - Template `CLAUDE.md` (§1, §4 "Tra API GameUp ở đâu" + mục mô tả GameUp SDK và GameUp IAP kèm luật, §6, §9, §10), skill mới `gameup-sdk-api` + `gameup-iap-api`, lệnh mới `/gu-sdk` + `/gu-iap`, cập nhật skill `gameup-core-api`, lệnh `/gu-core` `/gu-review`, agent `unity-game-developer` `gameup-core-architect` — trỏ AI tới index và bản chép, dặn Grep với `path` tường minh (`.claude/` là thư mục ẩn). `settings.template.json` chặn `Edit` vào `.claude/gameup-core|sdk|iap/**`.
- **`ScreenshotCapture` chuyển từ GameUp SDK sang Core** (`GameUp.Core`, `Runtime/Core/Utils/`) — công cụ chụp ảnh màn hình không dính gì tới ads/analytics nên thuộc về Core; kèm `ScreenshotCaptureEditor` (`GameUp.Core.Editor`) với preset kích thước ảnh store (App Store 6.7"/6.5"/5.5", Google Play phone/tablet) và nút chụp ngay trong Inspector. File giữ nguyên `.meta` khi di chuyển nên prefab/scene đang gắn component không mất reference.
  - Chạy được với **cả hai hệ input**: nhánh `#if ENABLE_INPUT_SYSTEM` dùng `Keyboard.current`, không có thì rơi về `Input.GetKeyDown` (`#if ENABLE_LEGACY_INPUT_MANAGER`). Trước đây bản dùng Input System sẽ không compile trên project chưa cài package, còn bản dùng `UnityEngine.Input` thì ném `InvalidOperationException` khi `activeInputHandler` là *Input System (New)*.
  - Phím tắt vẫn khai báo bằng `KeyCode` (giữ nguyên giá trị đã set trong Inspector/prefab), map sang `Key` của Input System theo tên enum và **cache** kết quả nên `Update` không parse enum mỗi frame.
  - `GameUp.Core.Runtime.asmdef` thêm tham chiếu `Unity.InputSystem` (chưa cài package thì Unity chỉ log warning, nhánh `#if` tự tắt).
- **Signal thông báo mở/đóng UI.** `UIPopup.OnPopupOpened` / `OnPopupClosed` (`Signal<UIPopup>`), `UIPopup.OnAllPopupClosed` (`Signal`) và `UIScreen.OnScreenOpened` / `OnScreenClosed` (`Signal<UIScreen>`) — code ngoài (audio, analytics, khoá input, pause gameplay) nghe được vòng đời UI mà không phải sửa từng popup/screen hay tự dựng event bus riêng. Popup báo đóng sau khi đã ẩn và gỡ khỏi danh sách đang mở (dựa trên kết quả `ActivePopups.Remove` nên gọi `ActionClose` lần hai không bắn trùng); screen báo đóng sau khi animation reverse chạy xong. Các Signal được tạo mới trong `ResetStaticState()` vì `Signal` giữ listener qua phiên Play khi tắt Domain Reload. `UIScreen.OnCurrentScreenChanged` giữ nguyên, không breaking.
- **`GameUp → Settings` — cửa sổ tổng cho project mới.** Trước đây người mới phải tự mò 6 menu rời rạc và không biết thứ tự; nay một cửa sổ hiện toàn bộ trạng thái (DOTween define → cấu trúc `_MainProject` → prefab Core trên scene → bộ công cụ AI), có thanh tiến độ, badge trạng thái và nút hành động cho từng bước, kèm khu mở nhanh mọi tool lẻ và bật/tắt log. Trạng thái đọc từ file thật trong project nên clone repo về máy khác vẫn đúng.
  - Có thêm mục **Project Settings → GameUp** cho ai quen tìm cài đặt ở đó.
  - **Tự chạy lần đầu**: khi mở một project vừa thêm GameUpCore, Editor mở cửa sổ Settings đúng một lần để dev chọn công cụ AI, sau đó mới bù các file còn thiếu (không bao giờ ghi đè file đã có). Tắt được bằng hai toggle trong mục "Tự động"; có nút cho hiện lại.

### Changed
- **Claude Code và Cursor thành hai lựa chọn độc lập.** Trước đây `.cursor/` được sinh vô điều kiện mỗi lần Editor nạp (`GUCursorRulesInstaller`), nên dev chỉ dùng Claude — hoặc không dùng AI — vẫn bị rác file trong repo. Nay `GameUp → Settings → Bộ công cụ AI` cho bật/tắt từng cái: chỉ Claude, chỉ Cursor, cả hai, hoặc không dùng; và Core **không sinh file nào** trước khi dev chốt lựa chọn.
  - Project clone về đã sẵn `.claude/` hoặc `.cursor/` thì lựa chọn được **suy ra từ file thật** — dev mới không bị hỏi lại, và công cụ team không dùng cũng không bị sinh thêm.
  - `GUCursorRulesInstaller` tách phần copy thành `InstallAll(overwrite, log, addIdePackage)` dùng chung cho cả menu lẫn luồng tự bù; chế độ không ghi đè nay áp dụng đúng cho `.cursorrules`, skills và hooks (trước đó `File.Copy` với `overwrite: false` sẽ ném `IOException` nếu file đã tồn tại).
- **Bộ công cụ Claude Code cho Unity (`GameUp/Project/Install Claude Code toolkit`)** — cài `CLAUDE.md` và `.claude/{agents,skills,commands,hooks,settings.json}` vào gốc project, tương đương phần `.cursor/` đã có nhưng cho Claude Code, và bám sát API thật của GameUp Core.
  - **`CLAUDE.md`**: quy ước naming/style C#, luật cứng (không `Debug.*` mà dùng `GULogger`, không tự chế lại thứ Core đã có, không nhét code game vào `Packages/`), bản đồ toàn bộ API Core, quy ước Scene/Prefab, ngân sách hiệu năng mobile, và danh sách việc AI **không** được tự làm.
  - **4 agent**: `unity-game-developer`, `gameup-core-architect`, `unity-performance-optimizer`, `unity-qa-engineer`.
  - **10 skill**: `gameup-core-api`, `unity-feature-kickoff`, `unity-design-to-tasks`, `unity-implement-story`, `unity-refactor-safely`, `unity-test-plan`, `unity-bug-triage`, `unity-perf-audit`, `unity-release-checklist`, `gameup-sdk-installer-flow`.
  - **11 lệnh** `/gu-kickoff` `/gu-tasks` `/gu-story` `/gu-refactor` `/gu-review` `/gu-test` `/gu-bug` `/gu-perf` `/gu-release` `/gu-core` `/gu-installer`.
  - **2 hook thi hành tự động** thay vì chỉ "nhắc": `gu-shell-guard` chặn `rm -rf`, `git reset --hard`, `git push --force`, `git clean -f` và mọi lệnh xoá `.meta` (mất `.meta` là rơi toàn bộ reference scene/prefab); `gu-csharp-guard` chạy sau mỗi lần AI ghi file `.cs` trong `_MainProject`/`GameUpCore`, bắt `UnityEngine.Debug.*` còn sót và trả lỗi để AI tự đổi sang `GULogger` ngay trong lượt. Hook chỉ gác luật cứng kiểm được chính xác — quy ước cần đọc ngữ cảnh để cho `/gu-review`, vì hook chạy sau *mỗi* lần ghi file nên nhiễu là bị tắt. Chuỗi và comment được lọc trước khi soi; bỏ sẵn `GULogger.cs`, `FullSerializerJson/`, `ThirdParty/`, `Plugins/`; bỏ qua một file bằng comment `// gu-lint:allow-debug`.
  - Installer chọn script `.sh` hay `.ps1` theo hệ điều hành lúc cài, sinh `settings.json` trỏ đúng đường dẫn và `chmod +x` trên macOS/Linux. `.claude/settings.local.json` (quyền cá nhân) không bao giờ bị đụng; `settings.json` cũ không do GameUp sinh sẽ được backup trước khi ghi đè.
  - Mẫu nằm trong `Documentation~/claude/` — sửa mẫu rồi bấm **Cập nhật** để phát cho cả team.
- **Data Save Viewer (`GameUp/Data/Data Save Viewer`)** — cửa sổ xem và sửa dữ liệu đã lưu ngay trong Editor, thay cho việc phải thêm log hoặc code tạm để đọc save. Dữ liệu nằm trong PlayerPrefs và đã mã hoá nên không có công cụ sẵn nào của Unity xem được.
  - Tự tìm mọi class kế thừa `BaseDataSave<T>` và **mọi bản save của từng class**: class có `Key` phụ thuộc dữ liệu (`Key => $"hero_{HeroId}"`) sinh nhiều key thì hiện đủ, gom dưới một thanh bấm để xổ.
  - Sửa theo **field** (hỗ trợ kiểu cơ bản, enum, `List`/mảng, `Dictionary` — thêm/xoá/sửa cả key — và class lồng nhau) hoặc theo **JSON thô**; đổi tab không mất chỉnh sửa dở. Ghi lại đi đúng đường `Save()` của data class nên format khớp tuyệt đối với runtime.
  - Sửa được `dataVersion` của bản save: hạ số rồi Lưu là cách nhanh nhất để ép `Migrate()` chạy lại mà thử; tab JSON ghi nguyên trạng nên dán được save của bản cũ vào để test nâng cấp schema.
  - **Tạo lại mặc định** (chạy `InitDefault()`) và **Xoá key** cho từng bản save; với class nhiều key, thao tác giữ đúng key đang chọn thay vì ghi về key mặc định.
  - Nhóm **Giá trị đơn (SettingVar)**: đọc/sửa các key lưu `BooleanVar` / `IntVar` / `FloatVar` / `LongVar` và cờ ghi thẳng bằng `LocalStorageUtils`, ghi lại qua đúng setter theo kiểu.
- **`GUInstallerUI`** — bộ widget dùng chung cho các cửa sổ setup (card, badge trạng thái, dòng trạng thái có nút, thanh tiến độ), cùng ngôn ngữ thiết kế với cửa sổ Setup Dependencies của GameUpSDK.
- **GameUpCore Installer trở thành cửa sổ duy nhất cần mở**: 3 bước có đánh số (DOTween → Folder Setup → Core setup) kèm badge trạng thái, thanh tiến độ tổng, phần Tùy chọn gom GameUpSDK / GameUpIAP (hiện version đã cài, nút copy Git URL), Helper packages, Logger và Audio setup.
- **Helper Package Installer nhận biết gói đã cài**: chọn nhiều gói bằng checkbox, cài theo hàng đợi có tiến độ "n/m", nút cài riêng từng gói. Trạng thái dựa trên marker path đã biết + so ảnh chụp thư mục `Assets` trước/sau khi import (ghi nhận lại để lần sau vẫn đúng, và tự trở về "chưa cài" nếu thư mục bị xoá).
- **Folder Setup**: thanh tiến độ, bộ lọc "chỉ hiện mục còn thiếu", nút **Tạo** cho từng thư mục thiếu và nút **Tạo** cho từng ScriptableObject thiếu, nút **Mở** để ping asset.
- **Audio Setup**: hiển thị rõ 2 giai đoạn với badge, số AudioClip trong Audio Folder, số AudioIdentity đã sinh, trạng thái `AudioID.cs`; nút Scan bị khoá kèm lý do khi chưa đủ điều kiện.
- `GUCoreProjectSetup.HasCoreObjectsInScene()` để installer biết scene đã có root Manager + UI hay chưa.

### Fixed
- **Trạng thái GameUpSDK / GameUpIAP luôn báo "chưa cài" khi cài qua Git UPM.** Package Git nằm trong `Library/PackageCache` chứ không phải `Packages/<tên>`, mà code chỉ kiểm tra thư mục vật lý. Nay hỏi AssetDatabase theo path ảo `Packages/<tên>` (Unity map mọi package vào đó) và đọc thêm version qua `PackageInfo`.
- **Folder Setup bị coi là "chưa xong" dù project đã đủ thư mục**, vì điều kiện phụ thuộc cờ `EditorPrefs` (mất khi clone project / đổi máy / đổi user) — kéo theo Core setup, Logger, Audio bị khoá menu vô lý. Nay trạng thái tính từ file thật trong project, cờ chỉ được đồng bộ lại theo đó.
- **Core setup chạy lần 2 tạo trùng `====Manager====` / `=====UI=====` trong scene**: prefab instance bị unpack ngay sau khi tạo nên không còn liên kết prefab để nhận diện; nay đối chiếu thêm theo tên root.
- Các cửa sổ setup tự vẽ lại khi project thay đổi hoặc khi lấy lại focus, thay vì giữ trạng thái cũ đến khi người dùng rê chuột vào.
- Thông báo cài DOTween / GameUpSDK / GameUpIAP không còn biến mất sau khi Unity reload domain (lưu qua `SessionState`).
- Trạng thái define `DOTween__DEPENDENCIES_INSTALLED` liệt kê đúng platform còn thiếu thay vì chỉ báo có/không.

## [0.2.0] - 2026-08-05

### Added
- `GUBootstrap`: chạy các bước khởi tạo theo thứ tự, có tiến độ và timeout cho từng bước.
- `GUSceneLoader`: load scene async có tiến độ, `minDuration` và kiểm soát thời điểm kích hoạt.
- `BaseDataSave`: đánh version dữ liệu (`dataVersion`) và hook `Migrate(fromVersion)` để nâng cấp save cũ; `Save()` chuyển thành public.
- `IPoolable` (`OnSpawn`/`OnDespawn`) và `GUPool.Prewarm(prefab, count)`.
- `AddressableLoad.WhenReady`: gom mẫu load Addressables dùng chung, có callback `onFailed`.
- Audio: `AudioCategory` (Sfx/Ui/Music), `AudioSetting.MusicVolume` / `SoundVolume`, crossfade nhạc nền, `PauseMusic`/`ResumeMusic`/`StopAllSfx`, và cập nhật volume ngay khi đổi cài đặt.
- Audio: dừng được âm thanh cụ thể — `AudioHandle` do `PlayAudio` trả về (`Stop(fade)`, `IsPlaying`) để dừng đúng một lần phát, và `AudioManager.StopAudio(identity | tên)` / `IsPlaying(...)` để dừng theo ID. Thêm `PlayAudio(string identityName)`.
- Test EditMode và PlayMode cho local storage, signal, save versioning và object pool.
- `README.md`, `CHANGELOG.md`.

### Changed
- Object pool dùng stack các object rảnh: Spawn/DeSpawn còn O(1) thay vì quét toàn bộ danh sách clone mỗi lần.
- Mọi API Open/Preload của screen và popup đi chung một đường `ResolveScreenAsync` / `ResolvePopupAsync`.
- `package.json`: khai báo thiếu `com.unity.textmeshpro`, chuyển hướng dẫn cài đặt sang README.
- `LocalStorageUtils`: lỗi khi ĐỌC dữ liệu (giải mã/deserialize hỏng) hạ từ `Error` xuống `Warning` — đây là tình huống lường trước và đã có giá trị mặc định để lui. Lỗi khi GHI vẫn là `Error` vì có nguy cơ mất dữ liệu.

### Fixed
- `MonoSingleton` chết vĩnh viễn sau khi đổi scene do cờ "đang thoát app" bị bật trong `OnDestroy`.
- Cache static của `UIScreen`/`UIPopup` giữ lại instance đã bị hủy theo scene, gây `NullReferenceException` khi mở lại màn.
- `UIBaseAnimation.OnStart` gọi nhầm callback đóng thay vì callback mở.
- `UIBaseView.OnClose` set callback sau khi chạy reverse, khiến view không bao giờ ẩn khi không có DOTween.
- `LocalStorageUtils` đọc/ghi số theo culture của máy và không bắt exception khi dữ liệu hỏng.
- `GULogger` strip cả `Error`/`Exception` khỏi build release, làm mất log lỗi trên máy thật.
- `OnValidate` của `UIScreen`/`UIPopup`/`UIShowMoveItemAnimation` che mất bản của lớp cha; phần thêm/xoá component chuyển sang `delayCall`.
- `AudioManager` giữ `AudioSource` ở trạng thái busy vĩnh viễn khi clip load lỗi.
- `AddressableDataHolder` chờ vô hạn nếu một handle không hợp lệ.
- Pool rò rỉ dictionary theo dõi clone qua mỗi lần đổi scene.
- `CoroutineRunner` không chạy gì nếu instance chưa được tạo trước đó.

## [0.1.11] và trước đó

Chưa ghi changelog.
