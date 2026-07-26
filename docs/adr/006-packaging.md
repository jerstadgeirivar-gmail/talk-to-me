# ADR 006: Packaging

Publish a self-contained `win-x64` directory. It is reproducible in Windows CI, needs no installer elevation, and can later be wrapped by a signed installer. The current output is unsigned and can trigger SmartScreen.
