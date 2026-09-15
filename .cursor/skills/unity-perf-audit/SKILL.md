---
name: unity-perf-audit
description: Audit hiệu năng Unity theo ngân sách FPS/GC/draw call/memory/load time và đề xuất sửa theo thứ tự hiệu quả. Dùng khi game giật, tụt FPS, build nặng, load lâu, hoặc trước release.
---

# Unity Performance Audit

Nguyên tắc: **đo trước, sửa sau**. Không có bằng chứng thì phải ghi rõ "giả thuyết".

## Ngân sách mặc định (mobile)

| Chỉ số | Mục tiêu |
|---|---|
| FPS | 60 ổn định (30 trên low-end) |
| GC alloc / frame trong gameplay | ~0 B |
| Thời gian tới màn đầu | < 3s |
| Managed heap | ổn định, không tăng đơn điệu |
| Crash rate | < 0.1% |

## Quy trình

1. Triệu chứng: giật lúc nào · scene nào · thiết bị nào · tái hiện được không.
2. Phân loại: CPU script · GPU/render · GC spike · IO/load · memory leak.
3. Thu bằng chứng: Profiler marker, Frame Debugger, Memory Profiler, log thời gian bootstrap.
4. Sửa theo thứ tự **hiệu quả/chi phí**, mỗi lần một thay đổi, đo lại.

## Chỗ hay thủng (kiểm theo thứ tự này)

1. `Instantiate`/`Destroy` lặp → **`GUPool`/`GUPoolers`**, `Prewarm` cho burst, object nhận `IPoolable`.
2. Alloc mỗi frame: LINQ, `ToList/ToArray`, closure, string concat, `new WaitForSeconds` trong coroutine.
3. `GetComponent`/`Find`/`Camera.main` trong `Update` hoặc vòng lặp → cache ở `Awake`.
4. Hàng trăm `Update` rời rạc → gom về một manager tick.
5. UI: Canvas rebuild toàn phần → tách Canvas theo tần suất đổi; tắt `Raycast Target` thừa; giảm overdraw panel chồng nhau.
6. Transparent/particle overdraw — thủ phạm số 1 trên mobile.
7. Texture/audio import setting: max size, ASTC, tắt Read/Write, tắt mipmap cho UI, streaming cho nhạc nền.
8. Addressables: handle không release → leak; `Resources/` kéo **toàn bộ** thư mục vào build.
9. `Signal` đăng ký mà không huỷ khi view tắt → xử lý thừa + leak.
10. Bootstrap: step nào chậm thì tách sang lazy/async (`GUBootstrap` có timeout theo step).

## Output

```markdown
## Performance Audit
- Triệu chứng / scene / thiết bị:
- Bằng chứng đo được:
- Bottleneck:

## Đề xuất (ưu tiên giảm dần)
1. [thay đổi] — chi phí: thấp|trung|cao — kỳ vọng: [số]

## Kết quả
- Trước → Sau:
- Đánh đổi:
- Chưa làm & lý do:
```

## Guardrails

- Không micro-optimize code chạy 1 lần lúc load.
- Không đổi `ProjectSettings/` (Quality/Graphics/Player) mà không báo trước.
- Không đánh đổi tính đúng đắn lấy FPS mà không nói rõ.
