---
description: Review code C# Unity theo convention GameUp (naming, logger, Core reuse, hiệu năng)
argument-hint: [file / thư mục / để trống = diff hiện tại]
allowed-tools: Read, Grep, Glob, Bash(git diff:*), Bash(git status:*)
---

Review phạm vi: **$ARGUMENTS** (để trống → xem `git diff` và `git status` của working tree).

Kiểm theo đúng thứ tự này, chỉ báo lỗi thật, không báo ý kiến cá nhân:

**1. Luật cứng**
- `UnityEngine.Debug.*` trong code feature (phải là `GULogger`)
- Viết lại thứ Core đã có: singleton, pool, event bus, save, popup, coroutine runner
- Code game nằm trong `Packages/com.ohze.gameup.core/` hoặc namespace `GameUp.Core*`
- `using` thừa · dead code · local function lồng trong hàm · nhiều public type trong một file

**2. Naming** (`CLAUDE.md` §3)
- private `_camelCase`; `[SerializeField] private` camelCase **không** `_`
- Class = danh từ; Method bắt đầu bằng động từ
- SO có `[CreateAssetMenu]` với `menuName` phân cấp

**3. Hiệu năng**
- Alloc trong `Update`/`FixedUpdate` (LINQ, string concat, closure, `new`)
- `GetComponent`/`Find`/`Camera.main` trong vòng lặp hoặc mỗi frame
- `Instantiate`/`Destroy` lặp lại thay vì `GUPool`
- `Signal` đăng ký mà không huỷ khi disable/destroy
- Addressables handle không release

**4. An toàn Unity**
- Đổi tên/di chuyển file làm rơi reference scene/prefab
- Đổi `[SerializeField]` field không có `[FormerlySerializedAs]`
- Đổi schema save mà không tăng `dataVersion` / xử lý `Migrate`

Với mỗi phát hiện: `file:line` · vấn đề · hệ quả cụ thể · cách sửa tối thiểu. Không có lỗi thì nói thẳng là sạch.
