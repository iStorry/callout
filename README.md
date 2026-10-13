# Callout

Translates in-game voice chat from any language into English, live, for any game: Valorant, CS2, Apex, Fortnite, or anything else that plays voice through Windows. Lines appear in a chat window or an overlay on top of the game.
Everything runs locally on your GPU. No account, no API key, no cost.

## Download

These links always point to the newest release:

| | Installer (recommended) | Portable |
|---|---|---|
| **Windows x64** (most gaming PCs) | [Callout-Setup-x64.exe](https://github.com/iStorry/callout/releases/latest/download/Callout-Setup-x64.exe) | [Callout-win-x64.zip](https://github.com/iStorry/callout/releases/latest/download/Callout-win-x64.zip) |
| **Windows on ARM** | [Callout-Setup-arm64.exe](https://github.com/iStorry/callout/releases/latest/download/Callout-Setup-arm64.exe) | [Callout-win-arm64.zip](https://github.com/iStorry/callout/releases/latest/download/Callout-win-arm64.zip) |

- **Installer:** installs for your user only (no administrator prompt), adds a Start menu entry, and can be removed from *Settings → Apps*. Uninstalling also deletes the downloaded models.
- **Portable:** unzip anywhere and run `Callout.exe`.

All versions are on the [Releases page](https://github.com/iStorry/callout/releases). Builds are not code-signed yet, so Windows SmartScreen may warn on first launch: choose *More info → Run anyway*.

> Callout is in **early access** (0.x). Expect rough edges, and please report what you find.

## How it works

```
Game audio (one app, a whole device, or VB-Cable for voice only) ──► Callout ──► your headset (pass-through for VB-Cable)
                                         │
                                         ├─ splits speech into lines (loudness, then Silero VAD)
                                         ├─ Whisper detects the language and translates it → English text (GPU via Vulkan)
                                         └─ shows each line in the chat window or the in-game overlay
```

**Languages:** auto-detect (the default) works out the language of each line separately, across the ~99 languages Whisper knows. A Russian, a Turk and a Pole in one lobby each get translated, and every line is tagged (`RU`, `TR`, `PL`). Lines already in English are hidden by default. Pin a language under *Teammates speak* if short callouts get misdetected.

**Display modes** (switch any time, even while listening):

- **Chat window** (default): a normal window with scrollback and timestamps. Put it on a second monitor, or pin it on top.
- **Game overlay**: the last few lines float over the game and fade after 12 s. Clicks pass through when locked.

Both are ordinary windows. Callout never reads, injects into or hooks the game, so it doesn't interact with anti-cheat.

## Setup

**Pick what to listen to** under *Voice chat source*:

| Source | Install? | Hears | Notes |
|---|---|---|---|
| **App: *your game*** | Nothing | Only that game | Start the game first, then press refresh (⟳) so it appears. Discord, music and browsers are left out. Windows 10 2004+. |
| **App: Discord** (or another voice app) | Nothing | Only that app | Use this if your team talks over Discord rather than in-game chat. |
| **Everything playing on *speakers*** | Nothing | All PC audio | The default. Simplest, but music and videos get translated too. |
| **CABLE Output** | VB-Cable | Voice chat only | Best accuracy; see below. |

None of the options except VB-Cable ask for microphone access.

**Best accuracy (optional, about 2 minutes):** a game plays voice chat and gunfire from the same process, so per-app capture hears both. Most games let you send voice chat to a separate output device, which gives the translator voice only:

1. Install **VB-Cable** (free): https://vb-audio.com/Cable/ and reboot.
2. In your game's audio settings, set the **voice chat output device** to `CABLE Input`. In Valorant: *Settings → Audio → Voice Chat → Output Device*.
3. In Callout, the source switches to `CABLE Output` automatically. Keep **Hear voice chat on** set to your headset so you still hear your team.

Windows lists VB-Cable as a microphone, so it asks for microphone access in this mode. Your real mic is never used.

**Overlay:** run the game in **windowed** or **borderless fullscreen** (exclusive fullscreen hides every overlay), choose **Game overlay**, untick **Lock overlay**, drag it into place, then lock it again.

The first start downloads the chosen model (~540 MB for Balanced) to `%LocalAppData%\Callout\models`.

## Resource use

- **Idle costs nothing:** the GPU only works while someone is talking. Silence never reaches the model.
- **Small per-line cost:** the encoder only processes as much audio as a line can hold (~13 s), not Whisper's usual 30 s window, and flash attention is on.
- **Stop frees everything:** the model is loaded on Start and released on Stop, so RAM and VRAM go back to the game.
- **The game comes first:** the app runs at below-normal CPU priority, and audio capture keeps its real-time thread.
- **Bounded memory:** if translation falls behind, stale lines are dropped. Chat history is capped at 300 lines. The non-concurrent GC means no background GC thread.
- **Fast start:** ReadyToRun-compiled, so startup doesn't spend CPU on JIT.

## Requirements

| Build | Runs on | Translation runs on |
|---|---|---|
| x64 | Windows 10/11 on Intel/AMD | GPU via Vulkan (NVIDIA, AMD or Intel; just the driver), CPU fallback |
| ARM64 | Windows 11 on ARM (Snapdragon) | CPU only: Whisper has no ARM GPU backend yet. Use the Fast quality setting. |

## Build

```bash
dotnet test
```

```bash
./scripts/publish.sh
```

This produces the portable `artifacts/Callout-win-x64.zip` and `artifacts/Callout-win-arm64.zip`. The installers are built in CI with [Inno Setup](https://jrsoftware.org/isinfo.php) from [installer/Callout.iss](installer/Callout.iss), since that step needs Windows.

## Releasing

Push a version tag. GitHub Actions tests, builds both architectures, builds both installers, and publishes the release with all four files attached:

```bash
git tag v0.2.0 && git push origin v0.2.0
```

Versions below 1.0 are titled *early access*. To check a release build without publishing, run *Actions → Release → Run workflow*: the installers and zips are attached to that run as artifacts. Every push to `main` also runs the format check, build and tests ([CI](.github/workflows/ci.yml)).

## Tuning

`%LocalAppData%\Callout\settings.json` → `Segmenter`:

- `SpeechRmsThreshold`: lower it if quiet teammates are missed, raise it if mic noise produces junk lines.
- `SilenceToEnd`: how long a pause ends a line.
- `MaxUtterance`: long speeches are cut into pieces of at most this length.

## Layout

| Path | What |
|---|---|
| `src/Callout.Core` | Platform-free logic: speech segmentation, transcript cleanup, audio sources, settings |
| `src/Callout` | WPF app: audio capture (NAudio), Whisper.net translation, chat window and overlay |
| `tests/Callout.Core.Tests` | Unit tests for the core |
| `installer/Callout.iss` | Inno Setup installer script |
| `scripts/publish.sh` | Builds the portable zips |
