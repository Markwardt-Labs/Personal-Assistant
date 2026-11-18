namespace Markwardt.PersonalAssistant.Client;

/// <summary>
/// Turns the address fields of a message into what a person wants to read.
/// </summary>
internal static class MailAddresses
{
    extension(string address)
    {
        /// <summary>
        /// Gets the display name of an address field such as <c>Anna &lt;anna@example.com&gt;</c>, or the address when it carries no name, or the text itself when it is not an address.
        /// </summary>
        public string DisplayName => System.Net.Mail.MailAddress.TryCreate(address, out System.Net.Mail.MailAddress? parsed) ? (parsed.DisplayName.Length > 0 ? parsed.DisplayName : parsed.Address) : address;
    }
}
