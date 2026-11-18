# Mail digest

`MailDigestService`, `MailDigestAi`, `DailySchedule` and the `JsonFile` the digest is kept in.

## Making a digest

The AI reads the mail itself: a session of its own with the Gmail connector, deleted again afterwards, gets the Gmail query `newer_than:7d -in:sent -in:drafts` (so archived mail and promotions count and sent mail does not), a limit of 200 messages and the background instructions. It searches, reads a message in full only when the search result is not enough, and answers with the topics (title, summary, importance and the messages, each with the identifier the Gmail tools gave, sender, subject, date, a preview and whether it is unread), or with a problem sentence when the Gmail tools are missing or fail. `Arrange` turns the topics into the digest: a message goes into the first topic that names it and a message without an identifier is dropped, a topic left with no message is dropped, and topics are ordered by importance (critical, high, normal, low, junk) and then by their newest message; the identifiers `t1`, `t2` follow that order. The client shows the first three importances in the list and collapses low and junk. A problem becomes the error of the state and the older digest stays.

A message opens through `MailReader`: a session of its own, fast and low effort, with the Gmail connector fetches the message by identifier and answers with its headers and body, which is cut at 20,000 characters and kept in memory (100 messages) so the second opening is instant.

## When

`Start` runs `DailySchedule`: a digest is made when none was saved, then a day after the last one, so a restart does not make a new one early, and an hour after an attempt that made nothing new. `Get` returns the state at once; it starts a generation only when there is no digest at all or the caller asks to refresh (the refresh of Settings), and one generation runs at a time (a second request while one runs just reports it). The state is pushed to every client as `MailDigestChanged` when a generation starts and when it ends, with the last error (a connector that is not connected, an AI that failed) while the older digest stays. The digest is saved through a temporary file, and reactions are marked on the topics when the state is built, by title.

## What is trusted

The model is asked for structure and the server keeps the guarantees the screen relies on (every message once, no empty topic, a stable order). The identifiers cannot be checked against a list the server holds, so a wrong one shows as an error when the message is opened.
