# Security Architecture

## Server-Side Request Forgery (SSRF) Protection
All user-configured generic API providers pass through `SsrfValidator`:
- Blocks loopback addresses: `127.0.0.1`, `::1`, `localhost`.
- Blocks private IPv4 ranges: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`.
- Blocks link-local addresses: `169.254.0.0/16` (protects cloud metadata services).
- Blocks non-HTTP/HTTPS protocols: `file://`, `ftp://`, `gopher://`.

## Tool Permission Guardrails
- **Safe**: Read files, query system telemetry, calculate math, search.
- **Controlled**: Write/create files, web browser navigation, send notifications.
- **Dangerous**: Delete files, remove directories, execute terminal shell commands.
Dangerous operations pause execution and require explicit user confirmation.

## Credential & Secret Masking
`SecretMasker` intercepts logs and telemetry to redact `Bearer` tokens, API keys, and passwords before saving or transmitting.
