# Architecture

This document explains the high-level design decisions behind Personal Assistant: why it is built the way it is, not the class-by-class mechanics.

## Core, Server and one Avalonia client

`Core` holds what the server and the app must agree on (the protocol messages and framing, the release feed), so neither references the other. Having the app depend on the server project was rejected because it would drag server-only code into the Android build. The trade-off is that shared code must stay platform neutral. The client is split into `Client/Frontend/`, a plain `net10.0` Avalonia library with all of its logic and screens, and `Client/Mobile/`, a thin Android head; the split exists because the headless Avalonia tests cannot load an Android assembly.

## The server owns the assistant, the app is a window onto it

The AI session, the conversation, the calendar, the mail digest, the events, the news and everything learned about the user live on the server, which runs on a PC that is on and signed in to Claude Code. The app keeps only how to reach the server, so a phone that loses signal loses nothing and a reinstalled app is back at once. The alternative, running the AI on the phone, was rejected because the AI tools, the user's accounts and the data they act on belong on one machine.

## The tabs are filled in the background, once a day

Mail, events and news are searches that take a minute of AI work, so the server makes them ahead of time: once a day on its own schedule, after a restart only when the last result is a day old, and an hour after an attempt that failed. The app opens on a result that is already there and follows what the server pushes; asking for a new one is a rare action that lives in the settings, not on the tabs. The alternative, searching when a tab opens, was rejected because a phone user should not wait for an AI. Each search runs in a session of its own that is deleted again, so the assistant's own conversation is never touched. The mail and the calendar are read by the AI through the user's connectors, which is slow (minutes), so the calendar too is kept as last read and refreshed once a day, on request and when the assistant changed it, instead of on every look.

## Topics, not emails

The mail tab shows what the mail is about, not a list of messages. The AI groups the messages of the last seven days into topics with a title, a summary and an importance, and the server checks the answer: a message appears once and a topic without messages is dropped. Several messages about one thing, such as three advertisements from one store, are one topic. Topics are ordered by importance and then by the newest message; the unimportant ones (newsletters, notifications, spam, advertising) are collapsed on the screen.

## Feeds say what to look for, and nothing else is searched

Suggested events and news are searched only for feeds the user wrote: a name and a description of what to look for, under which the entries are shown. With no feed nothing is searched and the tab says how to add one. Every entry the AI returns must name the feed it belongs to and have an address of the page it was found on; entries that do not are dropped, which is also the main defense against an AI that invents events or articles. A new feed is searched alone, for events as for news, so adding one neither costs a search of all feeds nor loses the entries of the others.

## The agent keeps its own notes as files

Everything the server keeps lives in the application data folder of the machine, and inside it a workspace folder is where every AI session works. What the assistant knows about the user is markdown files in the workspace's `Preferences` folder, which the agent organizes itself with the file tools, instead of a store of the server's own. Mail topics, suggested events and news entries have like and dislike buttons; the server writes them to a file in the same folder and has the agent fold every ten new ones into its notes, and what the user dictates in the chat goes into the notes the same way. Every session is told to read the folder first, so a like, a dislike or a sentence in the chat changes the next search as well as the next conversation, and the user can read and correct the notes in a text editor. The alternatives, a model trained on reactions or a profile the server stores and pastes into every prompt, were rejected as opaque and as ever-growing instructions; files the agent reads when it needs them stay organized and cost nothing when they are not needed.

## Only how to reach the server is configured on the server

The server's config file has the port, the access key and an optional certificate and nothing else. Where the user lives is a setting of the app, kept by the server and read at the time of each use, so a phone user never edits a file on the PC and a change needs no restart.

## Claude only, with mail and calendar through the connectors of the Claude account

The assistant is a Live AI session on Claude Code, and it reaches the user's mail and calendar through the Gmail and Google Calendar connectors of the claude.ai account that Claude Code is signed in with. The alternatives were rejected: Google's REST APIs need an OAuth client from a Cloud project of the user's own (setup that a phone user cannot do, and a token that expires weekly in testing status), and a Codex provider has no such connectors, so supporting both would leave one of them with a mail and calendar that cannot work. The price is speed (the AI does every read, so a digest or a calendar read takes minutes and opening a message ten seconds) and the loss of structured access: the server cannot call a calendar API itself, so it asks the AI for typed answers (topics, events, a message) and keeps them, which is why the calendar is read once a day and not on every look.

Live AI starts Claude Code with `--strict-mcp-config`, which leaves out every account connector, so the server hands the library its own executable as the `claude` command (`ClaudeShim`). The library starts that, the executable sees from its first argument and a marker environment variable that it is the stand-in, and starts the real `claude` with the same streams and the arguments changed for the sessions that ask for connectors: no strict config, and the two connectors added to the allowed tools. A session asks by carrying a marker sentence in its instructions, so the event, news and preference sessions, which read web pages written by strangers, keep the library's arguments and have no way to reach the mail or the calendar. Claude Code runs with its permission mode `dontAsk` and no built-in tools, so nothing but the allowed tools can run. The shim exists only until the library offers connectors itself.

The assistant acts on the user's requests without asking for approval first, since a chat that stops to confirm every email defeats the point of an assistant; what limits the damage of a mistake is that only the user's own chat messages count as tasks: email and calendar text is written by other people, so the instructions of every session that reads it say it is data and never orders. The AI gets no shell and no access outside its own workspace folder.

## One conversation, recorded by the server

The conversation shown in the chat is the server's own record (`chat.jsonl`), not the AI tool's stored conversation, which holds things the user has no use for such as tool calls and compaction summaries. The AI session is found again after a restart from the identifier the server keeps, and replaced silently when the AI tool no longer has it, so a lost session never leaves the assistant unusable; the visible conversation continues either way.

## The server pushes what changes

Replies, the working state and the results of the background searches reach the app as events on the same connection instead of being polled. Events go to every connected client and carry the message index or the whole state, which is what lets a client join mid-stream. A prompt sent while the assistant is working is given to the work in progress instead of waiting for it, so the user can correct it.

## Client and server must run the same version, and update together

The protocol is not versioned beyond the release, so the server tells its version in the handshake and an app that differs shows a screen with the two versions that offers an update or another server instead of risking calls the other side does not understand. One action updates both: the app asks the server to update first, which restarts it, then replaces itself through the installer of the phone. The server and the Android app ship as GitHub release assets and replace themselves from them rather than through a store because they are single files; the checksum GitHub states for each asset is checked before anything is replaced.

## Access key handshake before any traffic

Clients authenticate with a single shared access key sent as the first message, compared in constant time, before the server accepts anything else. A per-user account system was rejected as unneeded for a single-owner server.

## Always `wss`, on one port

Every connection is encrypted, so the key never travels in plaintext and there is no insecure mode to misconfigure. One transport means one port to forward and a connection that passes through anything that proxies WebSockets. The server creates a self-signed certificate on first start (or uses a configured PFX), since a single-owner server usually has no certificate authority behind it. Without one the app cannot validate it, so it accepts any certificate, trading protection against a man in the middle for needing no setup; the access key is the only authentication.

## The server is a self-contained single-file executable

The server (Windows and Linux, x64) is distributed as one executable per platform with the runtime bundled, so users install nothing else. The trade-off is a larger download than a framework-dependent build.
