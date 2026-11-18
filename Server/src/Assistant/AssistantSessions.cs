namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Makes the one AI session of the assistant, and finds it again after a restart so the conversation goes on.
/// </summary>
internal interface IAssistantSessions
{
    /// <summary>
    /// Loads the stored session, or creates it when there is none or the AI tool has lost it, and gives it the assistant's instructions, which ask for the Gmail and Google Calendar tools.
    /// </summary>
    /// <param name="cancellation">Cancels the work.</param>
    /// <returns>The session, which the caller disposes.</returns>
    /// <exception cref="PromptSessionException">The AI tool is not installed or could not be started.</exception>
    Task<IPromptSession> Open(CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class AssistantSessions(IPrompter prompter, IAiSetups setups, IJsonFile<StoredSession> store, IAssistantInstructions instructions) : IAssistantSessions
{
    /// <inheritdoc />
    public async Task<IPromptSession> Open(CancellationToken cancellation = default)
    {
        IPromptSession? session = await Load(cancellation);

        if (session is null)
        {
            session = await prompter.Create(await setups.Create(cancellation), cancellation);
            await store.Save(new() { Id = session.Id }, cancellation);
        }

        session.Configuration.Capability = PromptCapability.Balanced;
        session.Configuration.Effort = PromptEffort.Medium;
        session.Configuration.IsWebAllowed = true;
        session.Configuration.Instructions = setups.WithConnectors(instructions.Text);
        return session;
    }

    private async Task<IPromptSession?> Load(CancellationToken cancellation = default)
    {
        if (await store.Load(cancellation) is not { } stored)
        {
            return null;
        }

        try
        {
            IPromptSession loaded = await prompter.Load(stored.Id, cancellation);

            if (loaded.Provider == PromptProvider.Claude)
            {
                return loaded;
            }

            await loaded.DisposeAsync();
            return null;
        }
        catch (PromptSessionNotFoundException)
        {
            return null;
        }
    }
}
