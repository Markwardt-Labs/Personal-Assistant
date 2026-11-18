# API

The shared surface in `Core/` is what the server and the app must agree on: the messages of the protocol, the framing that carries them (`IMessageChannel`), the release feed that finds new versions (`IReleaseFeed`) and the product identity (`IAppInfo`). Nothing in it knows about the AI, Google or Avalonia, so neither side references the other. This document covers the design and flow of that surface, not member-level detail.

## Shape

Everything on the wire is one JSON object per WebSocket text message. The client speaks first with a `HandshakeRequest`; after an accepted `HandshakeResponse` it sends `RemoteRequest`s and receives `RemoteResponse`s. One response type carries both the answer to a request (its `Id` is the request's) and a pushed `RemoteEvent` (id zero), so a client has a single stream to read and one place that tells the two apart. A request names a `RemoteCommand` and carries only what that command needs; a response carries an `Error` or the result of the command.

The tabs map onto commands. `GetHistory`, `Prompt` and `Interrupt` are the chat. `GetMailDigest` returns a `MailDigestState` (the digest of topics, whether a new one is being made, the last error) and `ReadMail` one message in full. `GetAgenda` returns an `AgendaState` (calendar events of three months as the server last read them, the event feeds, the suggested events), and `AddEventFeed` and `RemoveEventFeed` change the feeds and answer with the new state. `GetNews`, `AddNewsFeed` and `RemoveNewsFeed` do the same for the news. `React` likes or dislikes an entry. `GetSettings` and `SetSettings` read and change what the server needs besides how to reach it (the location), and a change arrives on every client as a `SettingsChanged` event. `UpdateServer` makes the server update itself. The data commands that can start work (`GetMailDigest`, `GetAgenda`, `GetNews`) take `Refresh` to ask for a new search now, and answer at once with the state; the finished work arrives as `MailDigestChanged`, `AgendaChanged` or `NewsChanged` events carrying the same state, on every connected client.

The conversation is the unit everything shares: a `ChatMessage` has a stable `Index`, so a client that joins in the middle of a conversation can read the newest messages and then apply events without duplicating or losing any. Entries that a client reacts to have a key both sides compute the same way (`ReactionKeys`: the address for news and events, the title for a mail topic), so a reaction finds its entry again after a new search.

## Flow

1. The client opens `wss://host:port/` and sends `HandshakeRequest` with the access key within five seconds.
2. The server answers `HandshakeResponse`: `Accepted` with its `Version`, or a refusal followed by closing the connection. The client compares the version with its own and offers an update when they differ.
3. The client reads what a tab needs (`GetMailDigest`, `GetAgenda`, `GetNews`, `GetHistory`) the first time the tab is shown and then follows the events.
4. A prompt, a feed change or a refresh is answered at once; its effects arrive as events.
5. `Interrupt` stops what the assistant is doing without ending the conversation, so the next prompt can correct it.

## Framing

`IMessageChannel` owns serialization (camel case, enums as names, nulls omitted) and the size limit of a message, so the two sides cannot drift apart. Writes from several tasks are sent one at a time; a message that is too long, is not text or is not valid JSON ends the connection with `InvalidDataException`.
