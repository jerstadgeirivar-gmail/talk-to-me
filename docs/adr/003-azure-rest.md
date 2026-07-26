# ADR 003: Azure REST Client

Use a narrow `HttpClient` provider against the Microsoft-documented v1 preview transcription route. It exposes deployment, API version, language, prompt, cancellation, timeout, request ID, and typed failures without introducing a broad SDK dependency.
