namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// The workspace folder of the agent, inside the app data folder, which every AI session the server starts works in. The agent keeps and organizes what it knows about the user there as files.
/// </summary>
internal interface IAgentWorkspace
{
    /// <summary>
    /// Gets the path of the folder.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Gets the path of the folder of the preference files.
    /// </summary>
    string Preferences { get; }

    /// <summary>
    /// Makes the folders and the readme that tells the agent how to keep its notes, when they are not there yet.
    /// </summary>
    /// <param name="cancellation">Cancels the write.</param>
    /// <returns>A task that completes once the workspace is ready.</returns>
    Task Prepare(CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class AgentWorkspace(string appFolder) : IAgentWorkspace
{
    private readonly string readme = """
        # Preferences

        This folder is your memory about the user: what they want to see in their mail, events and news, and how they want things done. Read this file and every other file here before you start any work for them, and follow what they say.

        - Keep one file per area, for example General.md, Mail.md, Events.md and News.md, and add a file when a topic grows. Merge and split files so each stays short and easy to scan.
        - Write plain markdown bullet points. Put the user's own words first. Replace a statement that no longer holds instead of keeping contradictions, and date a preference only when the date matters.
        - Reactions.md is written by the server: it lists what the user liked and disliked, newest last. Never edit it. Fold what it shows into the other files as short general statements (kinds of topics, senders, sources, venues, times), not as lists of titles.
        - Do not put secrets, passwords or the text of private emails here.
        """;

    /// <inheritdoc />
    public string Path { get; } = System.IO.Path.Combine(appFolder, "Workspace");

    /// <inheritdoc />
    public string Preferences { get; } = System.IO.Path.Combine(appFolder, "Workspace", "Preferences");

    /// <inheritdoc />
    public async Task Prepare(CancellationToken cancellation = default)
    {
        Directory.CreateDirectory(Preferences);
        string path = System.IO.Path.Combine(Preferences, "README.md");

        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, readme, cancellation);
        }
    }
}
