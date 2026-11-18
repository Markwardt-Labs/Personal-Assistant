# Protocol

`HandshakeRequest`/`HandshakeResponse`, `RemoteRequest`/`RemoteResponse`/`RemoteEvent`, `IMessageChannel` on the client side (`ServerConnection`) and the server side (`ConnectionServer`, `Handshake`, `EventHub`).

## Transport

The server is a Kestrel host on one port with TLS. A request that is not a WebSocket upgrade is answered with a short text page, which lets a browser be pointed at the port to accept a self-signed certificate. Both ends send WebSocket pings (the server every 25 seconds, the client every 20) and drop a connection whose pings go unanswered for 20 seconds, so a connection that died silently, such as a phone that left the network, fails promptly instead of hanging.

## Handshake

The first message is read with a 1 KB limit and a five second timeout; a line that is not valid JSON, has no key or has a wrong key is refused. The key is compared by hashing both sides and comparing the hashes in constant time, so neither the content nor the length of the key leaks through timing. A malformed first message is dropped without an answer. After the handshake a message may be up to 1 MB from a client and 40 MB from the server.

## Requests and events on one connection

`ConnectionServer` reads requests one at a time per connection and answers each before reading the next, while a second task pushes events from the connection's `EventSubscription`. A subscription has a queue of its own, so a slow client delays only itself, and a client only receives events published after it subscribed, which is why it reads `GetHistory` after connecting and ignores messages whose index it already has.

`ServerConnection` numbers requests, keeps a table of waiting calls and completes each when the response with its id arrives. When the read loop ends, every waiting call fails with a `RemoteException` and `Lost` is raised, unless the connection was disposed on purpose. Events are posted to the synchronization context the connection was opened on, which for the Avalonia clients is the UI thread, so view models never need to marshal.

## Shutdown

Stopping the server first cancels a token shared by every connection, so each client gets a normal close and sees `Lost` immediately, then stops the host with a short timeout instead of waiting out the default.

## Background work and its events

The commands that read the mail digest, the agenda and the news answer at once with the state and, when a search is needed or asked for with `Refresh`, start it in the background; a second request while one runs only reports that it runs. The server publishes the state as `MailDigestChanged`, `AgendaChanged` or `NewsChanged` when a search starts and when it ends, to every connection through the same subscription that carries chat events, so a client that asked, a client that did not and a client that joins later all converge on the same screen. A client only applies what it receives and reads the state again when it opens a tab, because events published before it subscribed are not replayed.
