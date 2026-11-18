namespace Markwardt.PersonalAssistant.Tests.Integration;

public sealed class ChatStoreTests : IDisposable
{
    private readonly TempFolder folder = new();

    private readonly TimeProvider time = TimeProvider.System;

    public void Dispose()
        => folder.Dispose();

    [Fact]
    public async Task NumbersMessagesFromZeroInOrder()
    {
        ChatStore store = new(folder.Combine("chat.jsonl"), time);

        ChatMessage first = await store.Add(ChatRole.User, "Hi", CancellationToken.None);
        ChatMessage second = await store.Add(ChatRole.Assistant, "Hello", CancellationToken.None);

        Assert.Equal([0, 1], [first.Index, second.Index]);
        Assert.Equal(["Hi", "Hello"], (await store.Read(10, CancellationToken.None)).Select(message => message.Text));
    }

    [Fact]
    public async Task ReadsOnlyTheNewestMessagesUpToTheLimit()
    {
        ChatStore store = new(folder.Combine("chat.jsonl"), time);

        for (int number = 0; number < 5; number++)
        {
            await store.Add(ChatRole.User, $"m{number}", CancellationToken.None);
        }

        Assert.Equal(["m3", "m4"], (await store.Read(2, CancellationToken.None)).Select(message => message.Text));
    }

    [Fact]
    public async Task KeepsTheConversationAcrossRestarts()
    {
        await new ChatStore(folder.Combine("chat.jsonl"), time).Add(ChatRole.User, "Before", CancellationToken.None);
        ChatStore restarted = new(folder.Combine("chat.jsonl"), time);

        ChatMessage next = await restarted.Add(ChatRole.Assistant, "After", CancellationToken.None);

        Assert.Equal(1, next.Index);
        Assert.Equal([ChatRole.User, ChatRole.Assistant], (await restarted.Read(10, CancellationToken.None)).Select(message => message.Role));
    }

    [Fact]
    public async Task StartsEmptyWithoutAFile()
    {
        Assert.Empty(await new ChatStore(folder.Combine("none.jsonl"), time).Read(10, CancellationToken.None));
    }

    [Fact]
    public async Task NumbersMessagesAddedAtTheSameTimeWithoutGaps()
    {
        ChatStore store = new(folder.Combine("chat.jsonl"), time);

        ChatMessage[] added = await Task.WhenAll(Enumerable.Range(0, 20).Select(number => store.Add(ChatRole.User, $"m{number}", CancellationToken.None)));

        Assert.Equal(Enumerable.Range(0, 20), added.Select(message => message.Index).Order());
    }
}
