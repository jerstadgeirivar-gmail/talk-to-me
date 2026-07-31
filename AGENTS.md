# Repository validation requirements

Live application validation is a mandatory part of the definition of done.

- For every change that can affect runtime behavior, UI behavior, installation, packaging, updates, operating-system integration, audio handling, transcription, clipboard handling, or persisted settings, run the affected journey against the real built application.
- A successful build, static analysis, mocks, and unit tests do not substitute for live application validation.
- Exercise both the normal path and relevant failure/recovery paths. Re-run existing end-to-end scenarios that could reasonably regress because of the change.
- Inspect the observable result and generated evidence. Do not infer success merely from process exit or compilation.
- Report which live journeys were run, their results, and evidence locations. Explicitly identify anything that was not tested.
- If the environment prevents a required live test, state that the work is not fully validated; never describe it as complete or passing without that qualification.
- Documentation-only changes require link/content validation but do not require launching the application unless they describe runtime behavior that also changed.
