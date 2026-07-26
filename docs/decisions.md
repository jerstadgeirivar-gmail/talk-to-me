# Decisions

## 2026-07-26: Initial solution shape

Use the three-project product structure shared by all specifications plus one executable UI driver under `tools/`. Target .NET 10 because SDK and Windows Desktop runtime 10.0.101 are installed. No Git repository is currently initialized, so checkpoints are preserved as working files rather than commits.

## 2026-07-26: Azure v1 REST contract and live-test blocker

Use the current Microsoft-documented v1 preview transcription route (`/openai/v1/audio/transcriptions?api-version=preview`) through a narrow `HttpClient` provider. The deployment remains configuration and is sent as the multipart `model` field. The provider is contract-tested for URL, API-key header, multipart fields, JSON text, line endings, and rate-limit classification.

The live smoke test is blocked externally: `_init/az-foundry-api-key.md` contains one unlabeled credential value only, with no endpoint or deployment; the required environment variables are absent; no matching Windows credential metadata or prior indexed session exists; and Azure CLI resource discovery is blocked by an expired Conditional Access refresh token requiring interactive sign-in. No credential value was printed or copied.

## 2026-07-26: Safe external insertion target

Windows 11 Notepad reused the user's already-running packaged Notepad process and did not expose a separately owned process/window. The unattended harness therefore uses `TalkToMe.TestTarget`, a separate normal WPF Edit control, to avoid touching unrelated user content. This verifies standard UI insertion mechanics and exact-PID cleanup; Notepad remains a named smoke scenario once a separately owned instance can be guaranteed.
