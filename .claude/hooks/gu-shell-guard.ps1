#!/usr/bin/env pwsh
# GameUp Core — PreToolUse(Bash) guard (Windows).
# Chặn lệnh phá huỷ khó hồi phục trong project Unity.
# Exit 2 = chặn, stderr được gửi lại cho Claude.

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try {
    $command = [string]($raw | ConvertFrom-Json).tool_input.command
} catch {
    $command = $raw
}
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

# Bỏ thân heredoc / here-string: nội dung sau `<<EOF` hay `@"` là DỮ LIỆU, không phải lệnh.
# Không cắt thì một commit message nhắc tên lệnh nguy hiểm cũng bị chặn.
$lines = $command -split "`n"
$scanLines = @()
foreach ($line in $lines) {
    $scanLines += $line
    if ($line -match "<<-?['`"]?[A-Za-z_]" -or $line -match '@["'']$') { break }
}
$scan = $scanLines -join "`n"

# (?m) + (^|[;|&]) → chỉ khớp khi mẫu đứng ở VỊ TRÍ LỆNH, để câu văn nhắc tên lệnh
# ("chặn rm -rf, git reset --hard") không bị chặn nhầm.
$cmdPos = '(?m)(^|[;|&])\s*'

$rules = @(
    @{ Pattern = 'git\s+push\s+(-f|--force)(\s|$)'; Name = 'git push --force'; Reason = 'Ghi đè lịch sử remote. Dùng --force-with-lease và tự chạy tay.' },
    @{ Pattern = 'git\s+reset\s+--hard';            Name = 'git reset --hard'; Reason = 'Xoá vĩnh viễn thay đổi chưa commit.' },
    @{ Pattern = 'git\s+checkout\s+--\s+\.';        Name = 'git checkout -- .'; Reason = 'Vứt toàn bộ thay đổi working tree.' },
    @{ Pattern = 'git\s+clean\s+-[a-zA-Z]*f';       Name = 'git clean -f'; Reason = 'Xoá file chưa track, kể cả asset chưa kịp add.' },
    @{ Pattern = '(/[a-z/]*)?rm\s+(-[a-zA-Z]+\s+)*-[a-zA-Z]*r[a-zA-Z]*f'; Name = 'rm -rf'; Reason = 'Xoá đệ quy không hoàn tác được.' },
    @{ Pattern = '(/[a-z/]*)?rm\s+(-[a-zA-Z]+\s+)*-[a-zA-Z]*f[a-zA-Z]*r'; Name = 'rm -fr'; Reason = 'Xoá đệ quy không hoàn tác được.' },
    @{ Pattern = 'Remove-Item[^;|&]*-Recurse[^;|&]*-Force'; Name = 'Remove-Item -Recurse -Force'; Reason = 'Xoá đệ quy không hoàn tác được.' },
    @{ Pattern = '(rm|del|Remove-Item)[^;|&]*\.meta'; Name = 'xoá file .meta'; Reason = 'Mất .meta làm rơi mọi reference trong scene/prefab.' }
)

# Xoá hàng loạt .meta: khớp ở bất kỳ đâu vì find/xargs ghép nhiều đoạn.
if ($scan -match '\*\.meta' -and $scan -match '(-delete|-exec\s+rm|xargs\s+rm)') {
    [Console]::Error.WriteLine("[GameUp guard] Lệnh bị chặn: xoá hàng loạt file .meta")
    [Console]::Error.WriteLine("Lý do: Mất .meta làm rơi mọi reference trong scene/prefab.")
    [Console]::Error.WriteLine("Nếu thật sự cần, hãy tự chạy tay trong terminal — hook này không tự nới lỏng.")
    exit 2
}

foreach ($rule in $rules) {
    if ($scan -match ($cmdPos + $rule.Pattern)) {
        [Console]::Error.WriteLine("[GameUp guard] Lệnh bị chặn: $($rule.Name)")
        [Console]::Error.WriteLine("Lý do: $($rule.Reason)")
        [Console]::Error.WriteLine("Nếu thật sự cần, hãy tự chạy tay trong terminal — hook này không tự nới lỏng.")
        exit 2
    }
}

exit 0
