namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// One entry of the events tab: an event of the calendar or an event the assistant suggests.
/// </summary>
internal sealed class AgendaItemViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgendaItemViewModel" /> class.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="timeText">When it takes place, as text.</param>
    /// <param name="start">When it starts in the local time zone, which orders the entries of a day.</param>
    /// <param name="isAllDay">Whether it lasts whole days.</param>
    /// <param name="open">Called when the user opens the entry.</param>
    public AgendaItemViewModel(string title, string timeText, DateTime start, bool isAllDay, Action<AgendaItemViewModel> open)
    {
        Title = title;
        TimeText = timeText;
        Start = start;
        IsAllDay = isAllDay;
        OpenCommand = new RelayCommand(() => open(this));
    }

    /// <summary>
    /// Gets the command that opens the entry.
    /// </summary>
    public ICommand OpenCommand { get; }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets when it takes place, as text.
    /// </summary>
    public string TimeText { get; }

    /// <summary>
    /// Gets when it starts in the local time zone.
    /// </summary>
    public DateTime Start { get; }

    /// <summary>
    /// Gets a value indicating whether it lasts whole days.
    /// </summary>
    public bool IsAllDay { get; }

    /// <summary>
    /// Gets where it takes place, or <see langword="null" />.
    /// </summary>
    public string? Place { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Place" /> is set.
    /// </summary>
    public bool HasPlace => Place is not null;

    /// <summary>
    /// Gets what is written about it, or <see langword="null" />.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Details" /> is set.
    /// </summary>
    public bool HasDetails => Details is not null;

    /// <summary>
    /// Gets the people invited, as text, or <see langword="null" />.
    /// </summary>
    public string? Attendees { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Attendees" /> is set.
    /// </summary>
    public bool HasAttendees => Attendees is not null;

    /// <summary>
    /// Gets a value indicating whether the entry is a suggestion and not in the calendar.
    /// </summary>
    public bool IsSuggestion { get; init; }

    /// <summary>
    /// Gets the name of what the suggestion relates to, such as the feed description, or <see langword="null" />.
    /// </summary>
    public string? FeedName { get; init; }

    /// <summary>
    /// Gets the kind of event for a suggestion, or <see langword="null" />.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the address of the page of a suggestion, or <see langword="null" />.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Gets a value indicating whether the entry has a page to open.
    /// </summary>
    public bool HasUrl => Url is not null;

    /// <summary>
    /// Gets the command that opens the page of a suggestion in the browser, or <see langword="null" /> for an event of the calendar.
    /// </summary>
    public ICommand? OpenUrlCommand { get; init; }

    /// <summary>
    /// Gets the like and dislike buttons of a suggestion, or <see langword="null" /> for an event of the calendar.
    /// </summary>
    public ReactionViewModel? Reaction { get; init; }

    /// <summary>
    /// Gets a value indicating whether the entry has like and dislike buttons.
    /// </summary>
    public bool HasReaction => Reaction is not null;
}
