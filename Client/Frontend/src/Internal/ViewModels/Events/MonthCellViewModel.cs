namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// One day of the month grid with how much is planned on it.
/// </summary>
internal sealed class MonthCellViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MonthCellViewModel" /> class.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="isInMonth">Whether the day belongs to the month shown.</param>
    /// <param name="isToday">Whether it is today.</param>
    /// <param name="events">How many events of the calendar are on the day.</param>
    /// <param name="suggestions">How many suggested events are on the day.</param>
    /// <param name="select">Called when the user taps the day.</param>
    public MonthCellViewModel(DateOnly date, bool isInMonth, bool isToday, int events, int suggestions, Action<DateOnly> select)
    {
        Date = date;
        IsInMonth = isInMonth;
        IsToday = isToday;
        Events = events;
        Suggestions = suggestions;
        SelectCommand = new RelayCommand(() => select(date));
    }

    /// <summary>
    /// Gets the command that opens the day.
    /// </summary>
    public ICommand SelectCommand { get; }

    /// <summary>
    /// Gets the day.
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// Gets the number of the day in its month.
    /// </summary>
    public string Day => Date.Day.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets a value indicating whether the day belongs to the month shown.
    /// </summary>
    public bool IsInMonth { get; }

    /// <summary>
    /// Gets a value indicating whether the day is today.
    /// </summary>
    public bool IsToday { get; }

    /// <summary>
    /// Gets how many events of the calendar are on the day.
    /// </summary>
    public int Events { get; }

    /// <summary>
    /// Gets how many suggested events are on the day.
    /// </summary>
    public int Suggestions { get; }

    /// <summary>
    /// Gets the number of events as text, or an empty text for none.
    /// </summary>
    public string EventsText => Events > 0 ? Events.ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// Gets the number of suggestions as text, or an empty text for none.
    /// </summary>
    public string SuggestionsText => Suggestions > 0 ? Suggestions.ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// Gets a value indicating whether the day has events.
    /// </summary>
    public bool HasEvents => Events > 0;

    /// <summary>
    /// Gets a value indicating whether the day has suggestions.
    /// </summary>
    public bool HasSuggestions => Suggestions > 0;
}
