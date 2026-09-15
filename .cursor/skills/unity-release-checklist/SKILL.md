---
name: unity-release-checklist
description: Chạy checklist sẵn sàng phát hành cho build Unity và trả kết luận Go/No-Go. Dùng trước khi merge feature lớn, cắt nhánh release, hoặc publish build.
---

# Unity Release Checklist

## Cổng chặn (fail bất kỳ mục nào → No-Go)

- [ ] Build thành công cho mọi platform đích
- [ ] Bug blocker/critical đã fix hoặc được chấp nhận có văn bản
- [ ] Smoke test pass **trên build thật**, không chỉ trong Editor
- [ ] Save/load và **đường nâng cấp từ bản trước** đã kiểm (cài đè, không xoá app)
- [ ] Ngân sách hiệu năng đạt trên scene chính, thiết bị thấp nhất trong danh sách hỗ trợ
- [ ] Log đã tắt cho release (`GameUp → Logger → Disable Logs`)
- [ ] IAP / Ads / Analytics kiểm trên build release (không phải debug key)
- [ ] Icon, splash, bundle id, version code/name, quyền (permission) đúng
- [ ] Không còn asset debug/test lọt vào build
- [ ] CHANGELOG + known issues đã soạn
- [ ] Có kế hoạch rollback / hotfix

## Output

```markdown
## Release Readiness
- Version:
- Status: Go / No-Go
- Blocking issues:
- Risks accepted (ai chấp nhận, vì sao):
- Post-release follow-up:
- Next action:
```

## Guardrails

- Tách **must-fix** khỏi **post-release follow-up**, đừng gộp.
- Nêu rủi ro theo platform (Android fragment, iOS review, store policy) nếu có.
- Không tự bấm build/publish; trả checklist và để người dùng quyết định.
