namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// How much of the calendar the events tab shows at once.
/// </summary>
internal enum EventsMode
{
    /// <summary>
    /// One day as a list.
    /// </summary>
    Day,

    /// <summary>
    /// A week as one list per day.
    /// </summary>
    Week,

    /// <summary>
    /// A month as a grid of days with how much is planned on each.
    /// </summary>
    Month,
}
