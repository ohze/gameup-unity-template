# Dev Tracker (Google Sheets) — Ý nghĩa từng cột

Tài liệu này mô tả ý nghĩa và cách dùng cho các cột trong sheet `Trang tính1` (Dev Tracker).

## Quy ước chung

- **1 dòng = 1 task** (Feature/Bug/Chore/TechDebt).
- **Done thực tế** ưu tiên dựa trên `Ngày QA pass` (cột `P`). Nếu team bạn chốt “bàn giao = Done” thì có thể đổi logic, nhưng hiện template đang dùng `P` để tính các KPI.
- **Cột dropdown** giúp thống kê chuẩn (không sai chính tả).
- **Cột công thức (AC–AH)** tự tính KPI theo task.

## Nhóm thông tin theo dõi (A–AB)

### A — ID
- **Ý nghĩa**: Mã định danh task (vd: DEV-001, BUG-023).
- **Cách điền**: Lead dev quy định format.

### B — Sprint/Week
- **Ý nghĩa**: Sprint/tuần chứa task (vd: S12 / W17-2026).
- **Cách điền**: Theo lịch plan.

### C — Ngày tạo
- **Ý nghĩa**: Ngày task được tạo/ghi nhận.
- **Cách điền**: Ngày tạo ticket hoặc ngày đưa vào tracker.

### D — Người tạo
- **Ý nghĩa**: Ai tạo yêu cầu (PM/Lead/Dev/QA).

### E — Hạng mục (Type) (dropdown)
- **Ý nghĩa**: Loại công việc.
- **Giá trị**: Feature / Bug / Chore / TechDebt

### F — Module
- **Ý nghĩa**: Khu vực hệ thống (Auth/Gameplay/UI/Backend/Build…).

### G — Task
- **Ý nghĩa**: Tiêu đề ngắn gọn, action-oriented.
- **Cách điền**: 1 câu, rõ phạm vi.

### H — Mô tả/AC
- **Ý nghĩa**: Mô tả + Acceptance Criteria (điều kiện đạt).
- **Cách điền**: Liệt kê checklist/expected behavior, edge cases quan trọng.

### I — Priority (dropdown)
- **Ý nghĩa**: Mức ưu tiên.
- **Giá trị**: P0 (khẩn) → P3 (thấp)

### J — Estimate (h)
- **Ý nghĩa**: Ước lượng effort (giờ) cho “đi đúng cách lần đầu”.
- **Cách điền**: Số giờ (vd: 3.5, 16).

### K — Assignee
- **Ý nghĩa**: Người thực hiện chính.

### L — Reviewer
- **Ý nghĩa**: Người review PR/kiểm tra kỹ thuật (nếu có).

### M — Ngày bắt đầu
- **Ý nghĩa**: Ngày bắt đầu thực sự (khi chuyển sang In progress).
- **Lưu ý**: Đây là mốc cho nhiều KPI (lead time, planned end…).

### N — Deadline
- **Ý nghĩa**: Hạn cam kết theo plan.

### O — Ngày bàn giao
- **Ý nghĩa**: Ngày dev “handoff” (PR merged / build bàn giao / gửi QA).
- **Gợi ý**: Dùng để theo dõi luồng bàn giao, nhưng KPI mặc định dùng `P`.

### P — Ngày QA pass
- **Ý nghĩa**: Ngày Done thực tế (QA pass / nghiệm thu).
- **Lưu ý**: Đây là mốc “kết thúc” cho KPI (lead time, delay, on-time…).

### Q — Trạng thái (dropdown)
- **Ý nghĩa**: Tình trạng hiện tại.
- **Giá trị**: Not started / In progress / Blocked / In review / Done / Cancelled

### R — % Tiến độ
- **Ý nghĩa**: Ước lượng % hoàn thành hiện tại.
- **Cách điền**: 0–100% (hoặc 0–1 tuỳ format; template đang format %).

### S — Blocker? (dropdown)
- **Ý nghĩa**: Có bị chặn không.
- **Giá trị**: Yes / No
- **Rule**: Nếu Yes mà `Ghi chú` trống → highlight đỏ (bắt buộc nêu blocker).

### T — Lý do chính chậm (dropdown)
- **Ý nghĩa**: Nếu trễ deadline, chọn 1 lý do chính.
- **Rule**: Nếu quá deadline mà chưa `QA pass` và T trống → highlight đỏ.

### U — Ghi chú
- **Ý nghĩa**: Bổ sung thông tin: blocker detail, hướng xử lý, ai đang chờ…

### V — Link ticket/PR
- **Ý nghĩa**: Link issue/ticket hoặc PR merge.

### W — Link build/demo
- **Ý nghĩa**: Link build, video demo, package, testflight… tuỳ dự án.

### X — Lead dev đánh giá
- **Ý nghĩa**: Nhận xét ngắn: OK / Need improve / note kỹ thuật.

### Y — Độ phức tạp (dropdown)
- **Ý nghĩa**: Complexity 1–5 (độ khó/độ rủi ro).

### Z — Quality (dropdown)
- **Ý nghĩa**: Chất lượng đầu ra 1–5 (ít bug/rework, code sạch, pass nhanh…).
- **Lưu ý**: Đây là input cho `Performance points`.

### AA — Rework (h)
- **Ý nghĩa**: Giờ làm lại do bug/rework/sai yêu cầu (ước lượng).
- **Dùng để**: Tính `Estimate accuracy` và trừ điểm `Performance points`.

### AB — OT (h)
- **Ý nghĩa**: Giờ OT (nếu track).
- **Lưu ý**: Không dùng để “đánh giá”, chỉ để nhìn sức tải.

## Nhóm KPI tự tính (AC–AH)

### AC — Planned end (Ngày kết thúc dự kiến)
- **Ý nghĩa**: Ngày dự kiến xong theo estimate, tính từ `Ngày bắt đầu`.
- **Công thức**: \(M + J / HoursPerDay\)
- **Nguồn**: `HoursPerDay` lấy từ `LOOKUPS` (vd: 8h/ngày).

### AD — Lead time (days)
- **Ý nghĩa**: Số ngày thực hiện từ bắt đầu đến QA pass.
- **Công thức**: \(P - M\)

### AE — Delay (days)
- **Ý nghĩa**: Số ngày trễ so với deadline (không âm).
- **Công thức**: \(\max(0, P - N)\)

### AF — On-time?
- **Ý nghĩa**: Phân loại đúng hạn hay trễ (sau khi đã QA pass).
- **Logic**: AE = 0 → On-time; AE > 0 → Late.

### AG — Estimate accuracy
- **Ý nghĩa**: Mức “chuẩn” của estimate theo góc nhìn rework.
- **Công thức**: \((J - AA) / J\)
- **Diễn giải**: rework càng nhiều → accuracy càng thấp.

### AH — Performance points
- **Ý nghĩa**: Điểm hiệu suất theo task (thước đo tương đối để tổng hợp theo người/sprint).
- **Công thức tham khảo**: \(J \times Quality \times k - Rework\) với \(k=1\) nếu On-time, \(k=0.7\) nếu Late.
- **Lưu ý**: Đây là heuristic để lead dev nhìn xu hướng, không phải đánh giá tuyệt đối.

## Màu sắc / cảnh báo tự động (Conditional formatting)

- **Done**: tô xanh nhạt (dòng).
- **Blocked**: tô cam nhạt (dòng).
- **Cancelled**: tô xám (dòng).
- **Late**: tô đỏ nhạt nếu quá `Deadline` mà chưa có `QA pass`.
- **Due soon**: tô vàng nhạt nếu còn <= `WarnDays` ngày.
- **Thiếu lý do trễ**: cột `T` đỏ nếu Late mà chưa chọn lý do.
- **Thiếu ghi chú blocker**: cột `U` đỏ nếu `Blocked? = Yes` mà trống.

