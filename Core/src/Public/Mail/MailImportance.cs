namespace Markwardt.PersonalAssistant;

/// <summary>
/// How much a topic of mail matters to the user, which orders the mail tab: the first three are shown, the last two are collapsed.
/// </summary>
public enum MailImportance
{
    /// <summary>
    /// Needs the user soon: a deadline, a security or money matter, something someone is waiting on.
    /// </summary>
    Critical,

    /// <summary>
    /// Worth reading today: messages from real people, work, appointments, deliveries.
    /// </summary>
    High,

    /// <summary>
    /// Relevant but not pressing.
    /// </summary>
    Normal,

    /// <summary>
    /// Automated or bulk mail that is not spam: newsletters, notifications, receipts, social updates.
    /// </summary>
    Low,

    /// <summary>
    /// Spam, advertising and promotions.
    /// </summary>
    Junk,
}
