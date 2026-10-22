# Changelog

## 1.0.1

- Shortened the capture feedback animation and reused the existing STA UI thread.
- Save screenshots through atomic, collision-safe PNG files before retrying clipboard access.
- Expanded the local benchmark to cover standard capture, varied Shift selections, Escape cancellation, PNG dimensions, and clipboard availability.
- Added baseline comparison notes and verified Release x64 build instructions.
- Embedded Paolo Rubagotti and `paolo.ruba.1913@gmail.com` in the executable's Windows version metadata.
- Renamed the Windows product and executable to `RBG - RScreenFlash` and prepared the x64 release package with a SHA-256 checksum.
