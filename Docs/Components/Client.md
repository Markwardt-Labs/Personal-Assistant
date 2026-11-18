# Client

`MainViewModel`, `HomeViewModel`, the view models of the tabs, `ServerConnection`, `SettingsStore` and the views in `Client/Frontend/`, and the Android head in `Client/Mobile/`.

## Screens and navigation

`MainViewModel` owns the one screen that shows: the connect screen, the version mismatch screen, the home screen, or the updating screen over any of them while an update runs. `HomeViewModel` has the tabs (mail, events, news, chat in that order) and puts the settings over them; selecting a tab reads what the server has (the news once, the mail again only while it has nothing to show, the events each time), and the tabs keep their state while another one shows. The home screen owns the one bar on top: the tab title (the day, week and month switch for events), the feeds icon of events and news, the gear, and a thin progress line while the server works for the selected tab (or the assistant answers). The bar hides while a tab shows a page of its own (a topic, a message, an event, the feeds) or the settings, which have a bar with a back arrow of their own, so there is never more than one bar. The back button of the phone steps back through what is open (the settings, a mail message or topic, the details of an event, a list of feeds) and leaves the app only from the top level; `MainView` forwards `BackRequested` to `MainViewModel.GoBack`. Every screen is a view model with a template, so the headless tests drive and render them without a phone.

## Connection

`ServerConnection` numbers requests, completes each call when its response arrives and posts pushed events to the synchronization context it was opened on, which is the UI thread, so view models never marshal. When the connection ends, calls waiting on it fail and `Lost` is raised, unless it was disposed on purpose; `MainViewModel` then reconnects at once with the settings that last worked and shows the connect screen with the error when that fails. Settings are only how to reach the server, saved in a file only the user can read.

## Tabs

Each tab view model has the same shape: `Load` reads the state once and a pushed event replaces the state, which rebuilds its collections. Pages inside a tab (a topic, a message) are view models in a `Page` property, and the nested back steps are part of `GoBack`. Entries with like and dislike buttons hold a `ReactionViewModel`, which updates at once and tells the server, and goes back with the error when the server refuses. Links (articles, event pages) open through `ILinkLauncher`, which uses the launcher of the top level of the window.

## Settings

`SettingsViewModel` is a page over the tabs, and the only place that asks for a refresh (the tabs refresh by themselves as the server pushes): the three refresh buttons ask the server and the tabs show the result when it is pushed. It reads the settings of the server when it opens: the location is edited locally and sent with Save, and a `SettingsChanged` event pushed by the server (a save from another phone) replaces it. The screen also explains that the Gmail and Google Calendar connectors have to be connected in the Claude account of the server's computer.
