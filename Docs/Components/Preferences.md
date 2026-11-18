# Preferences

`PreferenceService`, `PreferenceAi`, `AgentWorkspace` and the reaction buttons of the client (`ReactionViewModel`).

## The agent keeps its own notes, as files

What the assistant knows about the user is not a database: it is markdown files in the `Preferences` folder of the agent's workspace, which the agent reads and writes through the file tools of Live AI and organizes itself. `AgentWorkspace` makes the folder and a `README.md` that says how to keep it (one file per area such as General, Mail, Events and News; short bullet points; replace what no longer holds; generalize reactions instead of listing titles; no secrets). The README is only written when it is missing, so the agent may reshape it. Every AI session the server starts (the assistant, the three searches and the one that folds in reactions) has the workspace as its folder through `AiSetups`, which also applies the AI tool chosen in the settings.

## Background instructions

`Background` appends one paragraph to the instructions of every session: the workspace is its memory about the user, read the README and every file in `Preferences` before starting, and follow it. For the assistant and the folding session the paragraph also says to record lasting preferences into the right file, merge or split files to keep them organized, and never edit `Reactions.md`; for the searches it says to only read. Nothing in the instructions is stored text of preferences, so what the user or the agent edits in the files is what the next session sees, and the instructions stay short however much has been learned.

## Reactions

Likes and dislikes of mail topics, suggested events and news entries are kept by the server as the last 150 (one per entry; pressing the same button again, which sends `None`, removes it), both in `reactions.json`, which marks the buttons of entries the server sends, and in `Reactions.md` of the workspace, rewritten on every change with one line per reaction (date, liked or disliked, kind, title, feed or sender or category). The titles are written on one line, so a title cannot start a line of instructions. Because the file is always current, a like or a dislike changes the very next search that reads it.

## Folding in

Every time ten reactions have come in since the last time, a session of its own is told the new reactions and asked to fold what they show into the files of `Preferences` as the README describes, then the time of the newest one is saved, so the same reactions are not handed over twice. A failure is logged and tried again with the next reaction. Notes the user states in the chat are written into the files by the assistant itself the same way, and a `README.md` or a file the user edits by hand is respected, since the agent reads it afresh.

## Keys

A reaction names its entry by a key both sides compute the same way (`ReactionKeys`): the address of the article or event page, or the lowercased title of a mail topic. The server marks the reaction on every entry it sends, so the buttons show their state after a restart, and a mail topic that a later digest words differently starts without one.
