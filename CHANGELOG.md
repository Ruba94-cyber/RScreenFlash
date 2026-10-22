# Changelog

## 1.0.1-rc.1

- Shortened the capture feedback animation and reused the existing STA UI thread.
- Save screenshots through atomic, collision-safe PNG files before retrying clipboard access.
- Expanded the local benchmark to cover standard capture, varied Shift selections, Escape cancellation, PNG dimensions, and clipboard availability.
- Added baseline comparison notes and verified Release x64 build instructions.
- Embedded Paolo Rubagotti and `paolo.ruba.1913@gmail.com` in the executable's Windows version metadata.
- Added a repeatable x64 release-candidate package with SHA-256 checksum and CI artifact.
