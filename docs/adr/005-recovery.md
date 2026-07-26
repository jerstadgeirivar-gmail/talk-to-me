# ADR 005: Recovery Policy

Write recordings incrementally under the current user's local application data. Delete after successful insertion or cancellation. Retain after transcription/insertion failure and expose retry/delete actions. Crash metadata discovery and bounded age-based cleanup remain incomplete.
