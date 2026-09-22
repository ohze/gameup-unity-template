#!/usr/bin/env pwsh
# GameUp Core — PostToolUse(Write|Edit) lint cho C# (Windows).
#
# Cố ý chỉ thi hành MỘT luật: không dùng UnityEngine.Debug trong code game/feature.
# Các quy ước cần đọc ngữ cảnh (namespace, FindObjectOfType, naming…) nằm ở
# CLAUDE.md và lệnh /gu-review — hook chạy sau mỗi lần ghi file nên phải ít nhiễu.
#
# Exit 2 = trả lỗi cho Claude để nó tự sửa ngay trong lượt.

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try {
    $file = [string]($raw | ConvertFrom-Json).tool_input.file_path
} catch {
    exit 0
}
if ([string]::IsNullOrWhiteSpace($file) -or -not (Test-Path -LiteralPath $file -PathType Leaf)) { exit 0 }
if (-not $file.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase)) { exit 0 }

$normalized = $file -replace '\\', '/'
if (-not ($normalized -match '(^|/)Assets/(_MainProject|GameUpCore)/')) { exit 0 }
if ($normalized -match '(GULogger\.cs$|/FullSerializerJson/|/ThirdParty/|/Plugins/)') { exit 0 }

$lines = Get-Content -LiteralPath $file
if ($lines -match 'gu-lint:allow-debug') { exit 0 }

# Bỏ nội dung chuỗi và comment cuối dòng trước khi soi. Số dòng vẫn khớp file gốc.
$pattern = '(^|[^A-Za-z0-9_.])Debug\.(Log|LogWarning|LogError|LogException|LogFormat|LogWarningFormat|LogErrorFormat|LogAssertion)'
$hits = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    $clean = ($lines[$i] -replace '"[^"]*"', '""') -replace '//.*', ''
    if ($clean -match $pattern) { $hits += "$($i + 1):$($clean.Trim())" }
    if ($hits.Count -ge 10) { break }
}

if ($hits.Count -eq 0) { exit 0 }

[Console]::Error.WriteLine("[GameUp lint] $file dùng UnityEngine.Debug — code game/feature phải dùng GameUp.Core.GULogger (CLAUDE.md §2.1):")
foreach ($h in $hits) { [Console]::Error.WriteLine($h) }
[Console]::Error.WriteLine("Đổi sang GULogger.Log/Warning/Error(tag, message) rồi báo lại.")
[Console]::Error.WriteLine("Nếu file này thật sự được phép wrap Debug, thêm comment: // gu-lint:allow-debug")
exit 2
