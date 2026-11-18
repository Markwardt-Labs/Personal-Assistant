namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// The like and dislike buttons of an entry: a press sets the reaction, a second press of the same button takes it back, and the server learns from it.
/// </summary>
internal sealed class ReactionViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReactionViewModel" /> class.
    /// </summary>
    /// <param name="connection">The connection that tells the server.</param>
    /// <param name="target">What kind of entry it is.</param>
    /// <param name="key">What identifies the entry.</param>
    /// <param name="title">The title of the entry.</param>
    /// <param name="context">What else describes the entry, or <see langword="null" />.</param>
    /// <param name="initial">What the user thought already.</param>
    public ReactionViewModel(IServerConnection connection, ReactionTarget target, string key, string title, string? context, ReactionKind initial)
    {
        this.connection = connection;
        this.target = target;
        this.key = key;
        this.title = title;
        this.context = context;
        kind = initial;
        LikeCommand = new AsyncCommand(() => Press(ReactionKind.Like));
        DislikeCommand = new AsyncCommand(() => Press(ReactionKind.Dislike));
    }

    private readonly IServerConnection connection;

    private readonly ReactionTarget target;

    private readonly string key;

    private readonly string title;

    private readonly string? context;

    private ReactionKind kind;

    /// <summary>
    /// Gets the command of the like button.
    /// </summary>
    public AsyncCommand LikeCommand { get; }

    /// <summary>
    /// Gets the command of the dislike button.
    /// </summary>
    public AsyncCommand DislikeCommand { get; }

    /// <summary>
    /// Gets what the user thinks of the entry.
    /// </summary>
    public ReactionKind Kind
    {
        get => kind;
        private set
        {
            if (Set(ref kind, value))
            {
                Raise(nameof(IsLiked));
                Raise(nameof(IsDisliked));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the user likes the entry.
    /// </summary>
    public bool IsLiked => kind == ReactionKind.Like;

    /// <summary>
    /// Gets a value indicating whether the user dislikes the entry.
    /// </summary>
    public bool IsDisliked => kind == ReactionKind.Dislike;

    private async Task Press(ReactionKind pressed)
    {
        ReactionKind before = kind;
        Kind = before == pressed ? ReactionKind.None : pressed;

        if (!await Attempt(() => connection.React(target, key, title, context, kind)))
        {
            Kind = before;
        }
    }
}
