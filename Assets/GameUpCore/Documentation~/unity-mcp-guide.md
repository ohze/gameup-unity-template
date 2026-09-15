# Claude Code × Unity: Plugin + MCP

> Hướng dẫn nội bộ cho team. Đã chạy thử thật trên project **Arena-Image-Fight** (Unity 6000.3.21f1, Linux, VS Code).
> Phiên bản khi viết: Claude Code 2.1.267 · plugin `unity` 0.1.2-beta · Unity CLI 1.0.0-beta.5 · `com.unity.pipeline` 0.7.0-exp.1
> ⚠️ Plugin và pipeline đều đang **beta / experimental**, tên tool có thể đổi giữa các bản.

---

## 1. TL;DR

Cài xong, Claude Code **nhìn thấy và thao tác trực tiếp trên Unity Editor đang mở**: đọc scene, sửa GameObject, bấm Play, đọc Console, chụp màn hình, chạy test, đổi setting. Nó cũng có thêm **33 skill chính thức của Unity** (IAP, LevelPlay Ads, UI, tối ưu…).

Trước đây:
> Dev copy log dán vào chat → Claude đoán → Claude sửa file `.unity`/`.prefab` bằng tay (dễ hỏng reference) → dev tự vào Editor kiểm tra.

Bây giờ:
> "Bấm Play, đợi 3 giây, đọc lỗi Console và chụp Game View" → Claude tự làm, tự xem kết quả, rồi mới sửa code.

---

## 2. Gồm những gì

Có **2 phần độc lập**, cài riêng từng phần:

**① Plugin `unity` (skills)**
- Là bộ "kiến thức chuẩn" của Unity, viết theo tài liệu chính thức.
- Cài **một lần trên mỗi máy**, dùng được ở mọi project.
- Không điều khiển Editor, chỉ giúp Claude làm đúng cách.

**② Unity MCP (điều khiển Editor)**
- Gồm Unity CLI (`unity mcp`) trên máy dev và package `com.unity.pipeline` trong project.
- Claude gọi các tool như `get_scene_hierarchy`, `editor_play`, `run_tests`… trên Editor đang mở.
- Chạy **hoàn toàn trên máy local**, dưới tài khoản của bạn, không phải remote access.

❗ Hướng dẫn cũ trên mạng nhắc tới `~/.unity/relay/` hoặc `Project Settings > AI > Unity MCP`: **không áp dụng** cho bản này, đừng mất công tìm.

---

## 3. Lợi thế: vì sao nên cài

### 3.1. Claude "nhìn thấy" project thay vì đoán
- Đọc được **hierarchy thật** của scene đang mở: tên object, component, trạng thái active.
  Ví dụ trên Arena-Image-Fight, một lệnh trả về toàn bộ `====Manager====`, `=====UI=====/NavBtn/ButtonPvP/...`, `====GamePlay====` kèm component.
- Đọc được **giá trị serialized field** trên component/prefab mà không cần mở file YAML.
- Tìm asset và object theo type, tên, label qua Unity Search.
- Bạn không phải chụp màn hình Inspector gửi cho Claude nữa.

### 3.2. Không phải sửa YAML scene/prefab bằng tay
- Không có MCP, Claude chỉ sửa được `.unity`/`.prefab` dạng text. Cách này dễ làm hỏng `fileID`/GUID, mất reference, gây conflict khi merge.
- Có MCP, Claude thao tác **qua API của Editor** (`add_component`, `set_serialized_field`, `apply_prefab_overrides`…), giống người dùng bấm trong Inspector. Unity tự lo reference và `.meta`.
- Đúng tinh thần rule của team: "Không sửa `.meta` thủ công", "Prefab is King".

### 3.3. Vòng lặp debug khép kín
- Claude **tự đọc Console** (tool `console`, lọc được log/warn/error) thay vì chờ dev copy log.
- Claude **tự bấm Play/Stop/Pause**, chỉnh timescale, giả lập phím và chạm (`simulate_key`, `simulate_pointer`).
- Claude **chụp Game View / Scene View** để tự kiểm tra UI có vỡ layout không.
- Claude **tự recompile** và biết project có lỗi compile hay không (`recompile_status`).
- Kết quả: sửa → compile → Play → đọc lỗi → sửa tiếp, dev không phải làm trung gian.

### 3.4. Test thật, không chỉ "code trông có vẻ đúng"
- `list_tests`, `run_tests`, `test_status`: chạy EditMode/PlayMode test ngay trong Editor đang mở.
- Khớp với quy trình §7 CLAUDE.md: bug fix phải có regression test, pass trước khi báo xong.

### 3.5. Kiểm tra cấu hình build / release nhanh
- Đọc Player Settings, Quality, Graphics, Physics, Input, Tags & Layers, Build Settings bằng một câu hỏi.
- Hữu ích trước khi build Android: kiểm tra package name, version, scripting backend, scene trong build, build target…
- Có cả `build`, `switch_build_target`, `list_build_profiles` (build chỉ chạy khi bạn yêu cầu rõ).

### 3.6. Skills chuẩn Unity cho mảng mobile / monetization
Đây là mảng team làm nhiều nhất:
- **`implement-in-app-purchases`**: Unity IAP gồm store connection, catalog, consumable/non-consumable/subscription, luồng pending → confirm, receipt validation, restore, extension riêng cho Google Play, migrate từ native billing / RevenueCat…
- **`levelplay-unity-integration`**: LevelPlay Mediation gồm rewarded/interstitial/banner, ad unit, lỗi gradle Android, ATT/privacy, ILRD revenue, migrate từ IronSource.Agent cũ hoặc Unity Ads.
- **`build-live-game`**: Unity Services cho live-ops: remote config, A/B test, cloud save, leaderboard, economy, battle pass.
- **Tối ưu mobile**: `optimize-audio`, `optimize-text-mesh-pro`, `manage-sprite-atlas`.
- **2D / UI**: `ui-ugui`, `2d-pixel-perfect`, `sprite-editor`, `tilemap-*`, `urp-postprocessing`.

### 3.7. Tiết kiệm thời gian ở các việc lặp lại
- Dựng hàng loạt GameObject/UI theo cấu trúc (`create_gameobjects`, `batch`).
- Tạo prefab, prefab variant, animator controller, timeline.
- Tìm mọi chỗ dùng một component, bật/tắt hàng loạt, đổi layer/tag.
- Cài/gỡ/tìm package qua Package Manager (`package_add`, `package_search`…).

---

## 4. Cài đặt

### 4.1. Yêu cầu
- Claude Code **≥ 2.1.257** (VS Code extension hoặc CLI đều được)
- Unity **6.0+**
- Git (để marketplace clone plugin)

### 4.2. Một lần trên mỗi máy dev

```bash
# 1. Kiểm tra version, thấp hơn 2.1.257 thì update
claude --version
claude update

# 2. Thêm marketplace + cài plugin (scope user = dùng được ở mọi project)
claude plugin marketplace add Unity-Technologies/unity-agent-plugin
claude plugin install unity@unity-agent-plugin --scope user
claude plugin list          # phải thấy: unity ... Status: ✔ enabled

# 3. Unity CLI: kiểm tra đã có chưa
which unity && unity --version
```

Chưa có Unity CLI thì cài:

```bash
# Linux / macOS
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
```

```powershell
# Windows (PowerShell)
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

Mở terminal mới rồi đăng ký MCP server với Claude Code:

```bash
claude mcp add --scope user --transport stdio unity-editor-mcp unity mcp
# hoặc: unity mcp configure claude-code   (chạy ra đúng lệnh trên)
```

Cuối cùng **Reload VS Code**: `Ctrl+Shift+P` → `Developer: Reload Window`.

### 4.3. Một lần cho mỗi project Unity

> Project đã commit `com.unity.pipeline` trong `Packages/manifest.json` (ví dụ Arena-Image-Fight) thì **bỏ qua bước này**, pull về là có.

```bash
cd <thư mục project Unity>
unity pipeline install      # thêm com.unity.pipeline vào manifest.json
```

1. **Click vào cửa sổ Unity Editor** để Editor import package. Unity không tự Refresh khi cửa sổ không có focus, bấm `Ctrl+R` nếu cần.
2. Đợi compile xong, Console **không có lỗi compile**.
3. Kiểm tra:

```bash
unity status        # State phải là: ready
claude mcp list     # unity-editor-mcp: unity mcp - ✔ Connected
```

4. Commit `Packages/manifest.json` và `Packages/packages-lock.json` để cả team dùng chung.

### 4.4. Cách lười: nhờ Claude cài hộ
Dán prompt ở **mục 8** vào Claude Code. Nó làm từng bước và hỏi trước mỗi thao tác quan trọng.

---

## 5. Cách sử dụng chi tiết

### 5.1. Nguyên tắc chung
- **Mở Unity Editor trước**, rồi mới nhờ Claude làm gì trên Editor.
- **Cứ nói bằng ngôn ngữ tự nhiên.** Claude tự chọn tool/skill phù hợp, không cần nhớ tên tool.
- Muốn gọi đích danh một skill: gõ `/unity:` rồi chọn trong danh sách, ví dụ `/unity:implement-in-app-purchases`.
- Skills của Unity **dùng chung** với skill team GameUp (`/gu-story`, `/gu-bug`, `/gu-test`…). Rule trong `CLAUDE.md` (dùng `GULogger` thay `Debug.Log`, dùng `GUPool`, `UIScreen`/`UIPopup`…) **vẫn ưu tiên** hơn code mẫu chung của skill Unity.

### 5.2. Xem / tìm hiểu project (chỉ đọc, an toàn)
```
Liệt kê các scene đang mở và hierarchy tóm tắt của scene active
Tìm tất cả GameObject có component LockButton, cái nào đang tắt?
Đọc giá trị serialized field của component BattleManager trên ====GamePlay====
Tìm các prefab trong Assets/_MainProject/Prefabs có dùng CurrencyHelperView
Liệt kê package đang cài và version
```

### 5.3. Debug
```
Đọc 20 lỗi/warning mới nhất trong Console và giải thích nguyên nhân
Bấm Play, đợi 5 giây, đọc lỗi Console rồi Stop
Bấm Play, chụp Game View (source=screen để thấy cả UI overlay), xem layout NavBtn có bị lệch không
Recompile, nếu có lỗi compile thì sửa rồi recompile lại tới khi sạch
Đo performance stats trong lúc Play
```
Mẹo: chụp Game View ở Edit mode có thể ra **ảnh đen** nếu nội dung chỉ spawn lúc runtime. Muốn thấy UI overlay thì chụp **trong Play mode** với `source=screen`.

### 5.4. Sửa scene / prefab
```
Thêm component SafeArea vào =====UI=====/Popup
Tạo prefab variant từ AvtUnit.prefab tên AvtUnit_Boss, đổi scale 1.5
Đổi layer của mọi object con trong UnitHolder sang "Unit"
Apply override của instance ButtonShop về prefab gốc
Lưu scene
```

### 5.5. Test
```
Liệt kê EditMode test hiện có
Chạy toàn bộ EditMode test, báo test nào fail kèm lý do
Viết regression test cho bug X, chạy để thấy fail, sửa code, chạy lại để thấy pass
```

### 5.6. Release / build Android
```
Đọc Player Settings Android: package name, version, bundle version code, scripting backend, target API
Kiểm tra scene nào đang nằm trong Build Settings
Kiểm tra Quality Settings đang dùng cho Android
```
Build (`build`) và đổi platform (`switch_build_target`) **tốn thời gian**. Chỉ yêu cầu khi thật sự cần.

### 5.7. Dùng skill cho feature cụ thể
```
/unity:implement-in-app-purchases  Thêm gói 100 gem consumable và gói remove-ads non-consumable cho Google Play
/unity:levelplay-unity-integration Thêm rewarded video để nhân đôi phần thưởng cuối trận
/unity:manage-sprite-atlas         Gom sprite UI vào atlas để giảm draw call
/unity:optimize-audio              Rà import setting audio cho mobile
/unity:ui-ugui                     Dựng popup Settings theo cấu trúc UIPopup của GameUp
```

### 5.8. Dùng Unity CLI trực tiếp (không qua chat)
```bash
unity status                          # Editor nào đang chạy, state gì
unity pipeline list                   # version pipeline, có bản mới không, Safe Mode?
unity command                         # liệt kê lệnh Editor đang cung cấp
unity command editor_play             # chạy một lệnh
unity command editor_status --format json
unity command editor_play --project-path /path/to/Project   # khi mở nhiều Editor
```

---

## 6. Danh sách tool MCP (pipeline 0.7.0-exp.1)

Lấy từ Editor thật, nhóm lại cho dễ tra:

- **Editor**: `editor_status`, `editor_play`, `editor_pause`, `editor_stop`, `editor_focus`, `set_timescale`, `set_target_framerate`, `runtime_status`, `wait_for`
- **Scene**: `list_open_scenes`, `open_scene`, `create_scene`, `save_scene`, `save_all`, `set_active_scene`, `get_scene_hierarchy`
- **GameObject**: `find_gameobjects`, `create_gameobject(s)`, `delete_gameobject`, `rename_gameobject`, `set_active`, `set_parent`, `set_transform`, `set_tag`, `set_layer`, `get_selection`, `set_selection`
- **Component**: `add_component`, `remove_component`, `get_component_properties`, `set_component_properties`, `get_serialized_fields`, `set_serialized_field`, `attach_script`
- **Prefab**: `create_prefab`, `create_prefab_variant`, `instantiate_prefab`, `apply_prefab_overrides`, `revert_prefab_overrides`, `unpack_prefab`, `save_prefab_contents`
- **Asset**: `find_assets`, `search`, `create_asset`, `create_folder`, `copy_asset`, `move_asset`, `rename_asset`, `delete_asset`, `import_asset`, `get/set_import_settings`, `create_script`, `read_text_file`, `write_text_file`
- **Console / quan sát**: `console`, `console_status`, `clear_console`, `log`, `capture_game_view`, `capture_scene_view`, `screenshot`, `get_performance_stats`
- **Input giả lập**: `simulate_key`, `simulate_pointer`
- **Code**: `recompile`, `recompile_status`, `eval`, `eval_file`, `run_script`, `reload_file*`
- **Test**: `list_tests`, `run_tests`, `test_status`, `cancel_tests`
- **Build**: `build`, `build_status`, `get/set_build_settings`, `list_build_profiles`, `list_build_targets`, `switch_build_target`, `add/remove_scene_from_build`
- **Settings**: `get/set_` + `player`, `quality`, `graphics`, `physics`, `audio`, `time`, `input`, `tags_layers`, `lighting`, `navmesh`, `runtime_pipeline` settings
- **Package**: `package_list`, `package_search`, `package_add`, `package_remove`, `package_resolve`, `package_status`
- **Animation / Timeline**: `create_animator_controller`, `add_animator_state/transition/parameter/layer`, `create_animation_clip`, `set/remove_animation_curve`, `create_timeline`, `add_timeline_track/clip`
- **Bake**: `bake_lighting`, `bake_navmesh(_surfaces)`, `bake_occlusion_culling` (+ status/cancel/clear)

Lưu ý: ở 0.6.0 có `get_console_logs`, sang **0.7.0 đổi thành `console`**.

---

## 7. Lưu ý, rủi ro, xử lý sự cố

### An toàn khi làm việc
- **Commit hoặc stash trước** khi nhờ Claude sửa scene/prefab hàng loạt, để có thể `git diff` và revert.
- Tool như `delete_asset`, `eval` (chạy C# tuỳ ý), `write_text_file`, `set_*_settings` **có thể phá project**. Đọc kỹ trước khi đồng ý quyền.
- Scene có thay đổi chưa lưu thì Claude có thể `save_scene` luôn. Muốn giữ nguyên thì nói rõ "không lưu scene".
- Skill Unity là hướng dẫn chung. Code sinh ra vẫn phải qua `/gu-review` theo convention team.

### Sự cố thường gặp
- **`unity status` trống / MCP không kết nối được**
  → Editor chưa mở, hoặc project chưa có `com.unity.pipeline`. Kiểm tra `unity pipeline list`.
- **Vừa cài/upgrade pipeline nhưng version không đổi**
  → Editor chưa Refresh. Click vào cửa sổ Unity, hoặc (nếu MCP còn kết nối) nhờ Claude gọi `package_resolve`.
- **Cột Safe Mode = true / lệnh bị timeout**
  → Project có lỗi compile, Pipeline không load được. Sửa lỗi compile rồi khởi động lại Unity.
- **"Network error" hoặc "Cannot connect … 127.0.0.1:7800" ngay sau khi recompile**
  → Editor đang domain reload. Đợi vài giây rồi gọi lại.
- **Claude gọi tool báo "Command Not Found"**
  → Danh sách tool trong session cũ hơn bản pipeline. **Reload Window** VS Code.
- **Mở nhiều Editor cùng lúc**
  → Thêm `--project-path <đường dẫn>` cho lệnh `unity`, hoặc mở VS Code đúng thư mục project.
- **Output hierarchy quá dài, tốn context**
  → Hỏi hẹp lại: "chỉ hierarchy của =====UI=====/NavBtn", hoặc dùng `find_gameobjects` thay vì lấy cả scene.

### Nâng cấp pipeline
```bash
unity pipeline list            # cột "Update Available" = true thì có bản mới
unity pipeline upgrade         # sửa manifest.json + packages-lock.json
```
Sau đó focus Unity (hoặc `package_resolve`), đợi `unity status` báo `ready`, **Reload Window** VS Code, rồi commit 2 file package.

---

## 8. Prompt để Claude tự cài hộ

Mở VS Code **đúng thư mục project Unity**, mở Unity Editor, rồi dán:

```text
Cài Unity Plugin chính thức cho Claude Code và kết nối Unity MCP để điều khiển Unity Editor.
Làm tuần tự, chạy lệnh qua terminal, báo cáo kết quả từng bước bằng tiếng Việt.
Dừng lại hỏi tôi khi gặp lỗi hoặc khi bước nào đó ghi "HỎI TÔI".

KIẾN TRÚC (đã kiểm chứng, đừng làm theo hướng dẫn cũ):
- Plugin `unity` chỉ cung cấp skills, KHÔNG có MCP server.
- MCP đến từ Unity CLI (`unity mcp`) + package `com.unity.pipeline` trong project.
- KHÔNG có ~/.unity/relay/, KHÔNG có "Project Settings > AI > Unity MCP". Đừng tìm mấy thứ này.

=== PHẦN A: PLUGIN ===
A1. `claude --version`. Nếu < 2.1.257 → HỎI TÔI trước khi chạy `claude update`.
A2. `claude plugin marketplace add Unity-Technologies/unity-agent-plugin`
A3. `claude plugin install unity@unity-agent-plugin --scope user`
A4. `claude plugin list` → xác nhận `unity` đang enabled.
    Nếu không enabled: `rm -rf ~/.claude/plugins/cache` rồi làm lại từ A2.
    Vẫn lỗi thì tổng hợp lỗi cho tôi, không thử workaround khác.

=== PHẦN B: UNITY CLI + PIPELINE ===
B1. Đọc ProjectSettings/ProjectVersion.txt (cần Unity 6.0+) và kiểm tra
    Packages/manifest.json đã có `com.unity.pipeline` chưa.
B2. `which unity && unity --version`.
    Nếu chưa có Unity CLI → HỎI TÔI, cho tôi xem lệnh cài chính thức
    (từ public-cdn.cloud.unity3d.com, channel beta) đúng với hệ điều hành của tôi.
B3. `unity pipeline list` → xem Editor có đang chạy và đã có Pipeline chưa.
    Nếu chưa mở Editor → báo tôi mở Unity Editor với project này rồi quay lại.
B4. Nếu manifest CHƯA có com.unity.pipeline → HỎI TÔI trước khi chạy
    `unity pipeline install` (lệnh này sửa manifest.json).
    Nếu ĐÃ có nhưng `unity pipeline list` báo có bản mới → HỎI TÔI có muốn `unity pipeline upgrade` không.
B5. Sau khi install/upgrade: nhắc tôi CLICK VÀO CỬA SỔ UNITY EDITOR để Editor import
    package (Unity không tự Refresh khi cửa sổ không có focus). Sau đó cứ 5-10s chạy
    `unity status` cho tới khi thấy state `ready`, tối đa 5 phút.
    Nếu Pipeline không load được → kiểm tra Safe Mode (`unity pipeline list`, cột Safe Mode);
    có lỗi compile thì báo tôi, đừng tự sửa code.

=== PHẦN C: MCP ===
C1. `claude mcp list` → đã có `unity-editor-mcp` chưa.
C2. Nếu chưa có → HỎI TÔI trước khi chạy:
    claude mcp add --scope user --transport stdio unity-editor-mcp unity mcp
C3. `claude mcp list` → xác nhận `unity-editor-mcp` báo ✔ Connected.

=== BÁO CÁO CUỐI ===
- Phiên bản: Claude Code, plugin unity, Unity CLI, com.unity.pipeline, Unity Editor.
- Trạng thái plugin, pipeline, MCP (đã kết nối hay chưa).
- File trong project đã bị thay đổi (chạy `git status`, `git diff --stat`).
- Việc tôi phải tự làm, gồm cả việc nhắc tôi Reload Window VS Code để các tool MCP hiện ra.
- Sau khi tôi reload xong, chứng minh MCP hoạt động bằng các thao tác CHỈ ĐỌC:
  trạng thái Editor, scene đang mở, hierarchy tóm tắt, lỗi trong Console.

RÀNG BUỘC
- Chỉ sửa Packages/manifest.json và packages-lock.json qua lệnh `unity pipeline`, và chỉ khi tôi đã duyệt.
  Không đụng file nào khác trong project (scene, prefab, script, ProjectSettings).
- Không git commit/push.
- Không tự sửa ~/.claude/settings.json hay ~/.claude.json bằng tay.
- Không cài package, extension hay MCP server nào ngoài những thứ nêu trên.
- Không chạy Unity ở chế độ batch/headless.
```

---

## 9. Checklist nhanh

- [ ] `claude --version` ≥ 2.1.257
- [ ] `claude plugin list` → `unity` enabled
- [ ] `unity --version` có kết quả
- [ ] `claude mcp list` → `unity-editor-mcp` ✔ Connected
- [ ] Project có `com.unity.pipeline` trong manifest
- [ ] Unity Editor đang mở, không lỗi compile
- [ ] `unity status` → `ready`
- [ ] Đã Reload Window VS Code
- [ ] Thử: "Liệt kê scene đang mở và lỗi trong Console"

---

## 10. Cursor × Unity MCP

Dùng chung Unity CLI và `com.unity.pipeline` với Claude Code — đã cài cho Claude thì Cursor chỉ cần đăng ký MCP và skill.

**Cách nhanh:** Unity Editor → `GameUp → Project → Cursor × Unity (MCP)` → **Cài đặt tất cả** (hoặc nút trong `GameUp → Settings`, mục Cursor IDE). Cửa sổ chạy tuần tự, bước đã xong thì bỏ qua:

| Bước | Kiểm tra | Nếu thiếu |
|---|---|---|
| Cursor trên máy | lệnh `cursor` hoặc thư mục `~/.cursor` | cài và mở Cursor một lần |
| Unity CLI | `unity --version` | script cài channel beta |
| MCP server `unity` | `~/.cursor/mcp.json` có entry `unity` với args `--project-path ${workspaceFolder}` | `unity mcp configure cursor --yes` (gộp vào file có sẵn, giữ server khác; backup `mcp.gameup-backup.json`) rồi sửa args |
| Skill chính thức của Unity | `~/.cursor/skills/unity-cli/SKILL.md` | chép 31 skill từ cache plugin Claude Code, hoặc `git clone --depth 1` repo `Unity-Technologies/unity-agent-plugin` |
| Rules + skills GameUp | `.cursor/rules/gameup-core-usage.mdc` + `unity-mcp.mdc` | bù file còn thiếu từ template GameUp Core |
| `com.unity.pipeline` | `Packages/manifest.json` | `unity pipeline install` |
| Editor ready | `unity status --json` | xem lỗi compile / Safe Mode |

**Cách tay:**

```bash
unity mcp configure cursor --yes        # ~/.cursor/mcp.json — KHÔNG dùng --local (đường dẫn tuyệt đối của máy bạn sẽ bị commit)
mkdir -p ~/.cursor/skills
cp -r ~/.claude/plugins/cache/unity-agent-plugin/unity/<version>/skills/. ~/.cursor/skills/
```

Rồi **sửa tay** entry `unity` trong `~/.cursor/mcp.json` cho có `--project-path ${workspaceFolder}`:

```json
"unity": {
  "command": "/home/<bạn>/.local/bin/unity",
  "args": ["mcp", "--project-path", "${workspaceFolder}"]
}
```

⚠️ **Thiếu bước này Cursor sẽ báo "không có tool Unity" dù server Connected.** Cursor chạy server MCP với thư mục làm việc là thư mục home, nên `unity mcp` không tìm ra Editor và trả **0 tool** (đo thật: chạy từ home → 0 tool, từ thư mục project hoặc có `--project-path` → 151 tool; biến môi trường `UNITY_PROJECT_PATH` không có tác dụng). Claude Code không bị vì nó chạy server từ thư mục project.
Không truyền `--project-path '${workspaceFolder}'` qua `unity mcp configure` được — CLI coi đó là đường dẫn tương đối và ghi thành `/home/<bạn>/${workspaceFolder}`. Không ghi đường dẫn tuyệt đối của một project vào file user vì sẽ ghim Cursor vào đúng project đó.

Sau đó:
1. **Thoát hẳn Cursor rồi mở lại** đúng thư mục project — Cursor chỉ đọc `~/.cursor/mcp.json` lúc khởi động; `Reload Window` không đủ.
2. **Cursor Settings → Tools & MCP** → bật server `unity` (chấm xanh + số tool = đã kết nối).
3. **Mở chat mới.** Phiên chat mở trước khi server lên đã chốt danh sách tool, Agent sẽ báo "không có tool Unity" dù server đã chạy.

Lưu ý:
- Cursor không có lệnh kiểu `claude mcp get` để kiểm "Connected" từ terminal — nhìn chấm xanh trong Settings → Tools & MCP. Muốn chắc server phía Unity chạy được: `unity mcp` phải in "MCP server started (stdio)" và trả ~150 tool khi client gọi `tools/list`.
- Chấm đỏ / không lên → Output panel → chọn **MCP Logs** để xem lỗi.
- Skill của Unity theo **Unity Companion License**: chỉ chép vào `~/.cursor/skills` trên từng máy, không commit vào repo. Plugin lên bản mới thì chạy lại bước skill.
- Luật dùng MCP cho Agent của Cursor nằm trong rule `.cursor/rules/unity-mcp.mdc`.
