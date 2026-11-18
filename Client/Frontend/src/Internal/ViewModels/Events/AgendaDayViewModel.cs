namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// One day of the day or week view with what is planned on it.
/// </summary>
/// <param name="date">The day.</param>
/// <param name="label">The day as text.</param>
/// <param name="isToday">Whether it is today.</param>
/// <param name="showLabel">Whether the label shows.</param>
/// <param name="items">What is planned, all-day entries first and then by start.</param>
internal sealed class AgendaDayViewModel(DateOnly date, string label, bool isToday, bool showLabel, IReadOnlyList<AgendaItemViewModel> items)
{
    /// <summary>
    /// Gets the day.
    /// </summary>
    public DateOnly Date { get; } = date;

    /// <summary>
    /// Gets the day as text.
    /// </summary>
    public string Label { get; } = label;

    /// <summary>
    /// Gets a value indicating whether the day is today.
    /// </summary>
    public bool IsToday { get; } = isToday;

    /// <summary>
    /// Gets a value indicating whether the label shows: the week view needs it to tell its days apart, and the day view already has the date as its title.
    /// </summary>
    public bool ShowLabel { get; } = showLabel;

    /// <summary>
    /// Gets what is planned on the day.
    /// </summary>
    public IReadOnlyList<AgendaItemViewModel> Items { get; } = items;

    /// <summary>
    /// Gets a value indicating whether anything is planned.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Gets a value indicating whether nothing is planned.
    /// </summary>
    public bool IsEmpty => Items.Count == 0;
}
