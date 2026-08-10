#!/bin/sh
# GameUp Core — PreToolUse(Bash) guard.
# Chặn lệnh phá huỷ khó hồi phục trong project Unity.
# Exit 2 = chặn, stderr được gửi lại cho Claude để nó chọn cách khác.

payload=$(cat)

# Lấy đúng chuỗi lệnh thay vì soi cả payload JSON. Nếu không tách được thì
# soi payload (chặt hơn, có thể báo dư — thà dư còn hơn sót ở hook an toàn).
if command -v python3 >/dev/null 2>&1; then
  command=$(printf '%s' "$payload" | python3 -c \
    'import sys,json
try: print(json.load(sys.stdin).get("tool_input",{}).get("command",""))
except Exception: pass' 2>/dev/null)
fi
[ -n "$command" ] || command=$payload

deny() {
  printf '%s\n' "[GameUp guard] Lệnh bị chặn: $1" >&2
  printf '%s\n' "Lý do: $2" >&2
  printf '%s\n' "Nếu thật sự cần, hãy tự chạy tay trong terminal — hook này không tự nới lỏng." >&2
  exit 2
}

# Bỏ thân heredoc: nội dung sau `<<EOF` là DỮ LIỆU, không phải lệnh. Không cắt thì
# một commit message nhắc tên lệnh nguy hiểm cũng bị chặn — lỗi này làm hook vô dụng.
scan=$(printf '%s\n' "$command" | sed -n "/<<-\{0,1\}['\"]\{0,1\}[A-Za-z_]/{p;q;};p")

# Chỉ khớp khi mẫu đứng ở VỊ TRÍ LỆNH: đầu dòng, hoặc sau ; | &.
# Nhờ vậy "chặn rm -rf, git reset --hard" trong câu văn không bị chặn nhầm,
# mà "foo && rm -rf bar" hay lệnh trên dòng riêng thì vẫn bắt được.
CMD_POS='(^|[;|&])[[:space:]]*'

match() {
  printf '%s' "$scan" | grep -Eq "$1"
}

at_command_position() {
  printf '%s\n' "$scan" | grep -Eq "$CMD_POS$1"
}

# --- Git: mất commit / mất thay đổi chưa lưu ------------------------------
at_command_position 'git[[:space:]]+push[[:space:]]+(-f|--force)([[:space:]]|$)' \
  && deny "git push --force" "Ghi đè lịch sử remote. Dùng --force-with-lease và tự chạy tay."
at_command_position 'git[[:space:]]+reset[[:space:]]+--hard' \
  && deny "git reset --hard" "Xoá vĩnh viễn thay đổi chưa commit."
at_command_position 'git[[:space:]]+checkout[[:space:]]+--[[:space:]]+\.' \
  && deny "git checkout -- ." "Vứt toàn bộ thay đổi working tree."
at_command_position 'git[[:space:]]+clean[[:space:]]+-[a-zA-Z]*f' \
  && deny "git clean -f" "Xoá file chưa track, kể cả asset chưa kịp add."

# --- Xoá đệ quy ------------------------------------------------------------
at_command_position '(/[a-z/]*)?rm[[:space:]]+(-[a-zA-Z]+[[:space:]]+)*-[a-zA-Z]*r[a-zA-Z]*f' \
  && deny "rm -rf" "Xoá đệ quy không hoàn tác được."
at_command_position '(/[a-z/]*)?rm[[:space:]]+(-[a-zA-Z]+[[:space:]]+)*-[a-zA-Z]*f[a-zA-Z]*r' \
  && deny "rm -fr" "Xoá đệ quy không hoàn tác được."
at_command_position 'Remove-Item[^;|&]*-Recurse[^;|&]*-Force' \
  && deny "Remove-Item -Recurse -Force" "Xoá đệ quy không hoàn tác được."

# --- Đặc thù Unity: mất .meta = mất toàn bộ reference ----------------------
at_command_position '(rm|del|Remove-Item)[^;|&]*\.meta' \
  && deny "xoá file .meta" "Mất .meta làm rơi mọi reference trong scene/prefab của project."
match '\*\.meta' && match '(-delete|-exec[[:space:]]+rm|xargs[[:space:]]+rm)' \
  && deny "xoá hàng loạt file .meta" "Mất .meta làm rơi mọi reference trong scene/prefab."
at_command_position 'rm[[:space:]]+(-[a-zA-Z]+[[:space:]]+)*(\./)?(Assets|ProjectSettings|Packages)(/|[[:space:]]|$)' \
  && deny "xoá Assets/ProjectSettings/Packages" "Xoá asset phải làm trong Unity Editor để .meta được xử lý đúng."

exit 0
