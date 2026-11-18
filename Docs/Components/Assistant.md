# Assistant

`AssistantService`, `AssistantSessions`, `AssistantInstructions`, `ChatStore` and `ClaudeShim`, which gives sessions the Gmail and Google Calendar connectors.

## Session lifecycle

`AssistantSessions` creates the one Live AI session on Claude Code in the agent's workspace, saves its identifier in `session.json` and, on every later start, loads it again so the AI keeps its context. When Claude Code no longer has the session, or the saved one belongs to another tool (an earlier version could run Codex), a new session replaces it. Settings that must not drift (balanced capability, medium effort, web allowed, no shell, the assistant's instructions with the connector marker) are applied on every open. The session is created lazily on the first prompt, so a server starts and serves history even when the AI tool is missing; the prompt then fails with an error the client shows.

## From AI events to the conversation

`AssistantService` subscribes to the session once. The session calls the observer on its own thread, so the events go into a channel and a single consumer task turns them into stored messages and published events, one at a time, in order. A message the AI writes becomes an assistant message; a failure becomes an assistant message that says what failed; state changes become `WorkingChanged`. Recording a message stores it and publishes it under one lock, so the order of events always matches the order of indices.

Before a prompt the service sets the instructions of the session to the base instructions plus what the server learned about the user plus the connector marker, which the session takes with that prompt. A prompt is recorded first, then given to the AI: when the session is waiting it is requested in the background, and otherwise (or when the session turns out to be busy by the time the request starts) it is sent into the work in progress. A failure of a request that the session also reported as a failure event is shown once.

## Connectors

The assistant has no tools of its own: it uses the `claude_ai_Gmail` and `claude_ai_Google_Calendar` connectors of the Claude account. Live AI never loads account connectors, so `ClaudeShim` stands in for the `claude` command. It is the server executable itself: the library starts it with the arguments of Claude Code, it recognizes them (the first argument is `-p` and the server has set `PERSONAL_ASSISTANT_CLAUDE_SHIM` in its own environment, which its children inherit) before anything else in `Main` runs, and it starts the real `claude` with inherited streams, so the library talks to Claude Code directly and killing the process tree ends both. Only a start whose `--append-system-prompt` contains the marker sentence is rewritten: `--strict-mcp-config` is dropped and `mcp__claude_ai_Gmail` and `mcp__claude_ai_Google_Calendar` are added to `--allowedTools`; every other start (the model list, the usage and the sessions without the marker) passes unchanged. `IAiSetups.WithConnectors` appends the marker. When run by the `dotnet` command the shim finds the executable beside the application files.

After a prompt in which the assistant created, updated, deleted or answered a calendar event, `AssistantService` raises `CalendarEdited` when the session is waiting again, and the agenda reads the calendar again, so the events tab follows the chat.

`ChatStore` appends one JSON line per message and loads the file on first use.
