# Callout

Translates Valorant voice chat from any language into English, live, in a chat window or an overlay on top of the game.
Everything runs locally on your GPU. No account, no API key, no cost.

## Download

Grab the latest build. These links always point to the newest release:

- **Windows x64** (most gaming PCs): [Callout-win-x64.zip](https://github.com/iStorry/callout/releases/latest/download/Callout-win-x64.zip)
- **Windows on ARM**: [Callout-win-arm64.zip](https://github.com/iStorry/callout/releases/latest/download/Callout-win-arm64.zip)

Unzip anywhere and run `Callout.exe`; nothing to install. All versions are on the [Releases page](https://github.com/iStorry/callout/releases). Builds are not code-signed yet, so Windows SmartScreen may warn on first launch: choose *More info → Run anyway*.

> Callout is in **early access** (0.x). Expect rough edges, and please report what you find.

## How it works

```
Valorant audio (per-app capture, or VB-Cable for voice only) ──► Callout ──► your headset (pass-through)
                                         │
                                         ├─ splits speech into lines (loudness, then Silero VAD)
                                         ├─ Whisper detects the language and translates it → English text (GPU via Vulkan)
                                         └─ shows each line in the chat window or the in-game overlay
```

**Languages:** auto-detect (the default) works out the language of each line separately, across the ~99 languages Whisper knows. A Russian, a Turk and a Pole in one lobby each get translated, and every line is tagged (`RU`, `TR`, `PL`). Lines already in English are hidden by default. Pin a language under *Teammates speak* if short callouts get misdetected.

**Display modes** (switch any time, even while listening):

- **Chat window** (default): a normal window with scrollback and timestamps. Put it on a second monitor, or pin it on top.
- **Game overlay**: the last few lines float over the game and fade after 12 s. Clicks pass through when locked.

Both are ordinary windows that never read or inject into the game, so they are safe with Vanguard.

## Resource use

- **Idle costs nothing:** the GPU only works while someone is talking. Silence never reaches the model.
- **Small per-line cost:** the encoder only processes as much audio as a line can hold (~13 s), not Whisper's usual 30 s window, and flash attention is on.
- **Stop frees everything:** the model is loaded on Start and released on Stop, so RAM and VRAM go back to the game.
- **The game comes first:** the app runs at below-normal CPU priority, and audio capture keeps its real-time thread.
- **Bounded memory:** if translation falls behind, stale lines are dropped. Chat history is capped at 300 lines. The non-concurrent GC means no background GC thread.
- **Fast start:** ReadyToRun-compiled, so startup doesn't spend CPU on JIT.

## Setup

**Zero setup:** run `Callout.exe`, keep **Voice chat source = Valorant (game audio only)**, and press **Start listening** with the game running. Callout captures only Valorant's audio through Windows' per-app capture (Windows 10 2004+). There's no install, no microphone permission, and Discord or music are never picked up.

**Best accuracy (optional, about 2 minutes):** Valorant plays voice chat and gunfire from the same process, so per-app capture hears both. To give the translator voice only:

1. Install **VB-Cable** (free): https://vb-audio.com/Cable/ and reboot.
2. In Valorant: **Settings → Audio → Voice Chat → Output Device = `CABLE Input`**.
3. In Callout, the source switches to `CABLE Output` automatically. Keep **Hear voice chat on** set to your headset so you still hear your team.

Windows lists VB-Cable as a microphone, so it asks for microphone access in this mode. Your real mic is never used.

**Overlay:** set Valorant to **Windowed Fullscreen**, choose **Game overlay**, untick **Lock overlay**, drag it into place, then lock it again.

The first start downloads the chosen model (~540 MB for Balanced) to `%LocalAppData%\Callout\models`.

## Requirements

| Package | Runs on | Translation runs on |
|---|---|---|
| `Callout-win-x64.zip` | Windows 10/11 on Intel/AMD | GPU via Vulkan (NVIDIA, AMD or Intel; just the driver), CPU fallback |
| `Callout-win-arm64.zip` | Windows 11 on ARM (Snapdragon) | CPU only: Whisper has no ARM GPU backend yet. Use the Fast quality setting. |

## Build

```bash
dotnet test
```

```bash
./scripts/publish.sh
```

This produces `artifacts/Callout-win-x64.zip` and `artifacts/Callout-win-arm64.zip`. Each is self-contained: unzip on the target PC and run `Callout.exe`.

## Releasing

Push a version tag. GitHub Actions tests, builds both architectures, and publishes the release with the zips attached:

```bash
git tag v0.2.0 && git push origin v0.2.0
```

Versions below 1.0 are titled *early access*. Every push to `main` also runs the format check, build and tests ([CI](.github/workflows/ci.yml)).

## Tuning

`%LocalAppData%\Callout\settings.json` → `Segmenter`:

- `SpeechRmsThreshold`: lower it if quiet teammates are missed, raise it if mic noise produces junk lines.
- `SilenceToEnd`: how long a pause ends a line.
- `MaxUtterance`: long speeches are cut into pieces of at most this length.

## Layout

| Project | What |
|---|---|
| `src/Callout.Core` | Platform-free logic: speech segmentation, transcript cleanup, settings |
| `src/Callout` | WPF app: audio capture (NAudio), Whisper.net translation, overlay |
| `tests/Callout.Core.Tests` | Unit tests for the core |
