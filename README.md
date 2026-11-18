# Personal Assistant

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build](https://github.com/Markwardt-Labs/Personal-Assistant/actions/workflows/build.yml/badge.svg)](https://github.com/Markwardt-Labs/Personal-Assistant/actions/workflows/build.yml)
![Coverage](.github/badges/badge_linecoverage.svg)

Personal Assistant is an AI-driven assistant for the things that fill a day. A command line server runs on your PC and drives the `claude` (Claude Code) command through the [Live AI](https://github.com/Markwardt-Labs/Live-Ai) library, which reaches your Gmail and Google Calendar through the connectors of your Claude account, and an Avalonia Android app is the way you use it over secure WebSocket. The app has tabs for your **mail** of the last seven days summarized into topics with the unimportant ones collapsed, your **events** (the Google calendar plus suggested events in your area) in day, week and month views, your **news** by feed, and a **chat** where you give the assistant direct instructions: act on your mail and calendar, or remember what you want. Like and dislike buttons teach it what to show you. The server refreshes mail, events and news once a day, and the app updates itself and the server from GitHub releases. Built on .NET 10 and [Avalonia](https://avaloniaui.net/); see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for redistributed dependency licenses.

## Requirements

- Windows 10/11, 64-bit, or Linux, x86-64 (glibc-based distro), for the server, and an Android 7 or newer phone for the app. No separate .NET runtime install needed: the server is self-contained.
- On the server's machine, the `claude` command (Claude Code) installed and signed in to a claude.ai account.
- In that account, the Gmail and Google Calendar connectors connected (claude.ai, Settings, Connectors; see the [usage guide](Docs/Usage.md)). No Google Cloud project or API key is needed.

## Installing

Download the server executable for your platform from the latest [Release](https://github.com/Markwardt-Labs/Personal-Assistant/releases):

- **Windows** - download `PersonalAssistant-Server-win.exe` and run it.
- **Linux** - download `PersonalAssistant-Server-linux`, mark it executable (`chmod +x PersonalAssistant-Server-linux`), and run it.

The first start writes a config file with a fresh access key into the application data folder of the machine and prints the key; give the server's address, its port (52680 by default) and the key to the app on first start. Everything else (your location) is set in the app's Settings and kept by the server. The Android app (`PersonalAssistant-android.apk`) is on the same page; once installed, the app updates itself and the server from the newest release.

## Documentation

| File | Covers |
| --- | --- |
| [Docs/Api.md](Docs/Api.md) | Shape and flow of the shared `Core` surface |
| [Docs/Usage.md](Docs/Usage.md) | Runnable usage examples |
| [Docs/Architecture.md](Docs/Architecture.md) | High-level design decisions |
| [Docs/Project.md](Docs/Project.md) | Repository tooling: scripts, publishing, CI |
| [Docs/Components/](Docs/Components) | One file per complex internal component |
