#!/usr/bin/env pwsh

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) {
  '{"permission":"allow"}'
  exit 0
}

try {
  $payload = $raw | ConvertFrom-Json
  $command = [string]$payload.command
} catch {
  '{"permission":"allow"}'
  exit 0
}

$dangerPatterns = @(
  'git\s+push\s+--force',
  'git\s+reset\s+--hard',
  'rm\s+-rf',
  'Remove-Item\s+.+-Recurse\s+-Force'
)

$isDangerous = $false
foreach ($pattern in $dangerPatterns) {
  if ($command -match $pattern) {
    $isDangerous = $true
    break
  }
}

if ($isDangerous) {
  $response = @{
    permission = "deny"
    user_message = "Blocked risky shell command. Use a safer alternative, or run manually if you absolutely need it."
    agent_message = "Hook denied potentially destructive shell command: $command"
  }
  $response | ConvertTo-Json -Compress
  exit 0
}

'{"permission":"allow"}'
exit 0
