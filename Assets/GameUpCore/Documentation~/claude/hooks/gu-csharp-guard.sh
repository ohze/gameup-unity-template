#!/bin/sh
# GameUp Core — PostToolUse(Write|Edit) lint cho C# do Claude vừa ghi.
#
# Cố ý chỉ thi hành MỘT luật: không dùng UnityEngine.Debug trong code game/feature.
# Đây là luật cứng, kiểm được chính xác, và gần như không có ngoại lệ hợp lệ.
# Các quy ước cần đọc ngữ cảnh (namespace, FindObjectOfType, naming…) nằm ở
# CLAUDE.md và lệnh /gu-review — hook chạy sau mỗi lần ghi file nên phải ít nhiễu,
# nếu không người dùng sẽ tắt nó.
#
# Exit 2 = trả lỗi cho Claude để nó tự sửa ngay trong lượt.

payload=$(cat)

file=$(printf '%s' "$payload" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n 1)
[ -n "$file" ] || exit 0
[ -f "$file" ] || exit 0

case "$file" in
  *.cs) ;;
  *) exit 0 ;;
esac

# Chỉ soi code thuộc quyền sở hữu của project.
case "$file" in
  */Assets/_MainProject/*|Assets/_MainProject/*) ;;
  */Assets/GameUpCore/*|Assets/GameUpCore/*) ;;
  *) exit 0 ;;
esac

# Ngoại lệ: file wrap Debug, thư viện nhúng, và file tự đánh dấu bỏ qua.
case "$file" in
  *GULogger.cs) exit 0 ;;
  */FullSerializerJson/*) exit 0 ;;
  */ThirdParty/*|*/Plugins/*) exit 0 ;;
esac
grep -q 'gu-lint:allow-debug' "$file" && exit 0

# Bỏ nội dung chuỗi và comment cuối dòng trước khi soi, để "…Debug.Log…" trong string
# hoặc trong ghi chú không bị báo nhầm. Số dòng vẫn khớp file gốc.
hits=$(sed -e 's|"[^"]*"|""|g' -e "s|//.*||" "$file" \
  | grep -nE '(^|[^A-Za-z0-9_.])Debug\.(Log|LogWarning|LogError|LogException|LogFormat|LogWarningFormat|LogErrorFormat|LogAssertion)' \
  | head -n 10)

[ -n "$hits" ] || exit 0

printf '%s\n' "[GameUp lint] $file dùng UnityEngine.Debug — code game/feature phải dùng GameUp.Core.GULogger (CLAUDE.md §2.1):" >&2
printf '%s\n' "$hits" >&2
printf '%s\n' "Đổi sang GULogger.Log/Warning/Error(tag, message) rồi báo lại." >&2
printf '%s\n' "Nếu file này thật sự được phép wrap Debug, thêm comment: // gu-lint:allow-debug" >&2
exit 2
