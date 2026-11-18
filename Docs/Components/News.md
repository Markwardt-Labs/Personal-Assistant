# News

`NewsService`, `NewsAi`, `StoredNews`, `StoredNewsFeeds` and the news tab of the client (`NewsViewModel`).

## Feeds and entries

A feed has a name (1 to 40 characters, unique without regard to case) and a description of what news to search for (3 to 300 characters); at most ten exist. The server keeps them in `news-feeds.json` and the entries in `news.json`. An entry is a summary of a story, not a copy of an article: a headline and one or two sentences written by the AI, the publisher, the time it was published when the page says, and the address of the article, which opens in the browser of the phone. Entries are grouped under their feed on the screen, newest first.

## Searching

The AI searches the web in a session of its own for articles published in the last seven days, for every feed, and returns entries that each name their feed. `Arrange` keeps only entries of the feeds that were searched, with a headline, a summary and an http or https address, drops an address that appeared before (so a story shared by two feeds appears once), keeps at most eight entries per feed and numbers them in feed order. A search runs once a day, on refresh, and when a feed is added: that search covers the new feed alone and the entries of the other feeds are kept as they were, so adding a feed costs one feed of searching. Removing a feed removes its entries at once. With no feed nothing is searched, and the tab explains how to add one. Failures leave the older entries in place and show the reason.
