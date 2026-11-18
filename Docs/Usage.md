# Usage

Runnable examples of running the server and talking to it.

## Starting the server

```sh
./PersonalAssistant-Server-linux
```

The server keeps everything in its app folder, the application data folder of the machine (`~/.local/share/PersonalAssistant` on Linux, `%LOCALAPPDATA%\PersonalAssistant` on Windows). The first start writes `config.json` there with a fresh access key and prints the key. `--data-folder <path>` uses another folder, which is for running a second server on one machine.

## The config file

```json
{
  "port": 52680,
  "key": "a-long-random-access-key",
  "certificateFile": "assistant.pfx",
  "certificatePassword": "optional"
}
```

The config only says how to reach the server: the `port`, the access `key` and, optionally, a PFX `certificateFile` that replaces the self-signed certificate the server makes in its app folder. Everything else is set from the app (see Settings below) and kept by the server.

## What is in the app folder

Next to `config.json`: `settings.json` (what you set in the app), the conversation, the mail digest, the calendar as last read, the feeds and their results, the reactions, the AI sessions and the self-signed certificate. `Workspace/` is the folder the AI works in: its `Preferences/` folder holds what the assistant knows about you as markdown files that the assistant organizes itself (a `README.md` tells it how, `Reactions.md` is your likes and dislikes, written by the server). You can read and edit those files too; the next AI session reads them.

## Settings

Open Settings in the app (the gear in the top bar). The location is saved on the server. Refresh now asks the server to sort the mail, read the calendar and look for events, or search the news, again at once: the server does all of that by itself once a day, so the tabs have no refresh button and show a thin progress line under the top bar while the server works. Settings also shows the versions of the app and the server, checks for updates and changes the server.

- **Location** - such as `Austin, Texas`; suggested events are searched near it.

The assistant runs on Claude Code, which has to be installed and signed in on the server's computer.

## Connecting Gmail and Google Calendar

The server reads and changes your mail and calendar through the Gmail and Google Calendar connectors of the Claude account that `claude` on the server's computer is signed in with, so there is nothing to set up in the server or in Google Cloud. Once, at [claude.ai](https://claude.ai) under Settings, Connectors: connect Gmail and Google Calendar and allow access to your Google account. Check with `claude mcp list` on the server's computer: `claude.ai Gmail` and `claude.ai Google Calendar` have to show Connected. When a connector is missing the tabs show that as their error and the chat says so.

Only the sessions that need them are given the two connectors: the assistant of the chat, the mail digest, the calendar read and the reading of a single message. The sessions that search the web (events, news) and the one that keeps the preference files have no access to your mail or calendar. The connectors can send mail and change events without asking, since the assistant is meant to act on its own.

Reading takes a while because the AI does it: the first mail digest takes a few minutes, the calendar a few minutes more, and opening a message takes about ten seconds the first time. The calendar is read once a day, on Refresh now in Settings, and again after the chat changed it.

Things to try in the chat tab:

- "Summarize my unread emails from today."
- "Reply to Anna's last email saying I can do Thursday." (it sends the reply without asking first)
- "Move my 3 pm tomorrow to 4 pm and tell me who is invited."

## Using the app

Install the Android app (`PersonalAssistant-android.apk`), enter the server's host, the port and the access key once, and it connects with the saved settings on every start. A connection that is lost (a phone that left the network, a restarted server) is reopened at once, and the connect screen stays with the error when that fails. The emulator reaches the PC at `10.0.2.2`. The tabs along the bottom:

- **Mail** - the mail of the last seven days as summary entries about topics, most important first; a topic of several emails (three advertisements of one store) is one entry. Spam, advertising and unimportant mail is in the collapsed section at the bottom. A dot marks what is unread. Tap an entry to open it: a topic with several emails lists them and each opens in full.
- **Events** - your Google calendars and the events suggested for you, as a day, a week (the seven days from the one shown, today at first) or a month; tap a day of the month to open it, tap an event for its details. Suggested events are marked with a star and have a link to their page. The feeds icon in the top bar opens the event feeds: each has a name and a description of what to look for (`Jazz`: `live jazz nights`). Events are only suggested for feeds, so add at least one, and set your location in Settings. A new feed is searched on its own.
- **News** - the news feeds you added, each with a name and a description of what type of news to search for (`Tech`: `new AI models and chips`), and under each feed the summary entries the server found; tap an entry to read the article. The feeds icon opens the list to add and remove feeds.
- **Chat** - the last tab, for direct instructions: do something with your mail or calendar ("move my 3 pm to 4 pm and tell Sam"), or remember something ("never show me sports news", "I like morning events"). Enter starts a new line and the arrow sends.

Mail topics, suggested events and news entries have a like and a dislike button (press again to take it back). The server writes them to the workspace and the assistant folds them, and what you tell the chat, into its preference files; the next search and conversation read those files. Refresh now in Settings shows the effect at once.

## Updating

The app looks for a newer release on GitHub when it connects and every hour. When there is one, a bar offers the update and Settings has Check for Updates. Updating asks the server to update first (it downloads its new executable and restarts; under a service manager such as systemd it needs a restart on failure policy), waits until the server answers again, and then downloads the new APK and hands it to the installer of the phone, which asks for confirmation; the first time, Android asks to allow the app to install apps. The APK must be signed with the key of the installed app, so a debug build cannot update itself this way. When the app and the server run different versions, the app shows both versions and offers the update or another server instead of the tabs.

## Giving the assistant a new capability

The assistant can do what its connectors offer. To add one, connect it at claude.ai under Settings, Connectors, add its tool prefix (`mcp__claude_ai_<Name>`, as `claude mcp list` and the tool names show it) to the allowed connectors of `ClaudeShim` in `Server/src/Claude/ClaudeShim.cs`, and say in `AssistantInstructions` what it is for. Only sessions whose instructions carry the marker of the shim get connectors, so a new one is available to those and to no other session.
