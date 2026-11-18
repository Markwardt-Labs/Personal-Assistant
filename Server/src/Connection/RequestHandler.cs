namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Does what a client asks and answers it.
/// </summary>
internal interface IRequestHandler
{
    /// <summary>
    /// Handles one request; a failure is the answer's error and never an exception.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellation">Cancels the work, as when the client disconnects.</param>
    /// <returns>The answer.</returns>
    Task<RemoteResponse> Handle(RemoteRequest request, CancellationToken cancellation = default);
}

/// <inheritdoc />
internal sealed class RequestHandler(IAssistantService assistant, IMailDigestService digest, IAgendaService agenda, INewsService news, IMailReader mail, IPreferenceService preferences, ISettingsManager settings, IServerUpdater updater, ILog log) : IRequestHandler
{
    /// <inheritdoc />
    public async Task<RemoteResponse> Handle(RemoteRequest request, CancellationToken cancellation = default)
    {
        try
        {
            switch (request.Command)
            {
                case RemoteCommand.GetHistory:
                    return new() { Id = request.Id, History = await assistant.History(request.Limit, cancellation) };
                case RemoteCommand.Prompt:
                    await assistant.Prompt(request.Text, cancellation);
                    return new() { Id = request.Id };
                case RemoteCommand.Interrupt:
                    await assistant.Interrupt(cancellation);
                    return new() { Id = request.Id };
                case RemoteCommand.GetMailDigest:
                    return new() { Id = request.Id, MailDigest = await digest.Get(request.Refresh == true, cancellation) };
                case RemoteCommand.GetAgenda:
                    return new() { Id = request.Id, Agenda = await agenda.Get(request.Refresh == true, cancellation) };
                case RemoteCommand.AddEventFeed:
                    return new() { Id = request.Id, Agenda = await agenda.AddFeed(request.Name, request.Text, cancellation) };
                case RemoteCommand.RemoveEventFeed:
                    return new() { Id = request.Id, Agenda = await agenda.RemoveFeed(request.FeedId, cancellation) };
                case RemoteCommand.GetNews:
                    return new() { Id = request.Id, News = await news.Get(request.Refresh == true, cancellation) };
                case RemoteCommand.AddNewsFeed:
                    return new() { Id = request.Id, News = await news.AddFeed(request.Name, request.Text, cancellation) };
                case RemoteCommand.RemoveNewsFeed:
                    return new() { Id = request.Id, News = await news.RemoveFeed(request.FeedId, cancellation) };
                case RemoteCommand.React:
                    await preferences.React(request.Target ?? throw new RequestException("The kind of entry is missing."), request.Key, request.Title, request.Context, request.Reaction ?? throw new RequestException("The reaction is missing."), cancellation);
                    return new() { Id = request.Id };
                case RemoteCommand.GetSettings:
                    return new() { Id = request.Id, Settings = await settings.Get(cancellation) };
                case RemoteCommand.SetSettings:
                    return new() { Id = request.Id, Settings = await settings.Update(request.Settings ?? throw new RequestException("The settings are missing."), cancellation) };
                case RemoteCommand.ReadMail:
                    return new() { Id = request.Id, Mail = await mail.Read(request.MessageId, cancellation) };
                case RemoteCommand.UpdateServer:
                    await updater.Update(cancellation);
                    return new() { Id = request.Id };
                default:
                    return new() { Id = request.Id, Error = "That command is not known." };
            }
        }
        catch (RequestException exception)
        {
            return new() { Id = request.Id, Error = exception.Message };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new() { Id = request.Id, Error = exception.Message };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.Error($"The {request.Command} request failed", exception);
            return new() { Id = request.Id, Error = "The server failed to do that." };
        }
    }
}
