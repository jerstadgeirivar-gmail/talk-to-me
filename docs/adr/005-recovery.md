# ADR 005: Recovery Policy

Write recordings incrementally under the current user's local application data. Delete after successful transcription or cancellation. Delete after transcription failure by default; when the user explicitly enables failed-audio retention, expose retry and delete actions. Crash metadata discovery remains available, while bounded age-based cleanup remains incomplete.
