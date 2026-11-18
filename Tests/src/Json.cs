namespace Markwardt.PersonalAssistant.Tests;

internal static class Json
{
    extension(string json)
    {
        public JsonElement ToElement()
            => JsonDocument.Parse(json).RootElement.Clone();
    }
}
