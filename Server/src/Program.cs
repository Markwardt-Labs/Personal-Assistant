namespace Markwardt.PersonalAssistant.Server;

/// <summary>
/// Process entry point for the command line server.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs the server with the given command line arguments.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        IClaudeShim shim = new ClaudeShim(Environment.ProcessPath, AppContext.BaseDirectory, Environment.GetEnvironmentVariable, RunProcess);

        if (shim.IsShim(args))
        {
            return await shim.Run(args);
        }

        Environment.SetEnvironmentVariable(shim.Variable, "1");
        using CancellationTokenSource cancellation = new();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        using PosixSignalRegistration terminated = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });

        TimeProvider time = TimeProvider.System;
        ILog log = new ConsoleLog(Console.Out, time);
        IAppInfo info = new IAppInfo.Factory().Create(typeof(Program).Assembly);
        IAssistantService? assistant = null;
        IMailDigestService? digest = null;
        IAgendaService? agenda = null;
        INewsService? news = null;
        IPreferenceService? preferences = null;
        IReleaseFeed feed = new IReleaseFeed.Factory().Create();
        IExecutableInstaller installer = new ExecutableInstaller(feed, Environment.ProcessPath, OperatingSystem.IsWindows());
        installer.Clean();
        IServerRestart restart = new ServerRestart(cancellation, Console.Out, installer, [.. args], Environment.GetEnvironmentVariable, Launch, TimeSpan.FromSeconds(1));
        IServerUpdater updater = new ServerUpdater(info, feed, installer, restart);

        IConnectionServer Create(ServerConfig config)
        {
            string folder = config.DataFolder!;
            ISettingsService settings = new SettingsService(new JsonFile<StoredSettings>(Path.Combine(folder, "settings.json")));
            // Earlier versions kept a Google refresh token here that nothing reads any more.
            File.Delete(Path.Combine(folder, "google-tokens.json"));
            IAgentWorkspace workspace = new AgentWorkspace(folder);
            IAiSetups setups = new AiSetups(workspace, shim);
            IPrompter prompter = new Prompter(Path.Combine(folder, "ai"), null, time);
            prompter.Environment = prompter.Environment with { ClaudeCommand = shim.Command };
            IEventHub events = new EventHub();
            IDailySchedule schedule = new DailySchedule(time, TimeSpan.FromDays(1), TimeSpan.FromHours(1));
            IAssistantInstructions instructions = new AssistantInstructions();
            preferences = new PreferenceService(new PreferenceAi(prompter, setups), new JsonFile<StoredReactions>(Path.Combine(folder, "reactions.json")), workspace, log, time);
            assistant = new AssistantService(new AssistantSessions(prompter, setups, new JsonFile<StoredSession>(Path.Combine(folder, "session.json")), instructions), instructions, setups, preferences, new ChatStore(Path.Combine(folder, "chat.jsonl"), time), events, log);
            digest = new MailDigestService(new MailDigestAi(prompter, setups), new JsonFile<MailDigest>(Path.Combine(folder, "mail-digest.json")), preferences, schedule, events, log, time);
            agenda = new AgendaService(new CalendarAi(prompter, setups), new JsonFile<StoredCalendar>(Path.Combine(folder, "calendar-cache.json")), new EventSuggestionsAi(prompter, setups), new JsonFile<StoredSuggestions>(Path.Combine(folder, "agenda-suggestions.json")), new JsonFile<StoredFeeds>(Path.Combine(folder, "event-feeds.json")), preferences, schedule, events, log, time, settings);
            news = new NewsService(new NewsAi(prompter, setups), new JsonFile<StoredNews>(Path.Combine(folder, "news.json")), new JsonFile<StoredNewsFeeds>(Path.Combine(folder, "news-feeds.json")), preferences, schedule, events, log, time);
            assistant.CalendarEdited += agenda.RefreshCalendar;
            digest.Start(cancellation.Token);
            agenda.Start(cancellation.Token);
            news.Start(cancellation.Token);
            return new ConnectionServer(new Handshake(info), new IMessageChannel.Factory(), new CertificateProvider(), new RequestHandler(assistant, digest, agenda, news, new MailReader(prompter, setups), preferences, new SettingsManager(settings, events), updater, log), events, log);
        }

        IApplication application = new Application(info, new ConfigLoader(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonalAssistant")), Create, Console.Out);
        int code;

        try
        {
            code = await application.Run(args, cancellation.Token);
        }
        finally
        {
            if (digest is not null)
            {
                await digest.Close();
            }

            if (agenda is not null)
            {
                await agenda.Close();
            }

            if (news is not null)
            {
                await news.Close();
            }

            if (preferences is not null)
            {
                await preferences.Close();
            }

            if (assistant is not null)
            {
                await assistant.Close();
            }
        }

        if (restart.IsRequested)
        {
            Environment.Exit(restart.Relaunch());
        }

        return code;
    }

    private static string? Launch(ProcessStartInfo info)
    {
        try
        {
            Process.Start(info)?.Dispose();
            return null;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException)
        {
            return exception.Message;
        }
    }

    private static async Task<int> RunProcess(ProcessStartInfo info, CancellationToken cancellation)
    {
        try
        {
            using Process process = Process.Start(info) ?? throw new InvalidOperationException("The process did not start.");

            try
            {
                await process.WaitForExitAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
                process.Kill(true);
                throw;
            }

            return process.ExitCode;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync($"Claude Code could not be started: {exception.Message}");
            return 127;
        }
    }
}
