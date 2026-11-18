namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class ChatViewModelTests
{
    private readonly Mock<IServerConnection> connection = new();

    private readonly Eventually eventually = new();

    private ChatMessage Message(int index, ChatRole role = ChatRole.Assistant, string text = "text")
        => new() { Index = index, Role = role, Text = text, Time = DateTimeOffset.UnixEpoch };

    private ChatViewModel Create()
        => new(connection.Object);

    [Fact]
    public async Task LoadsTheConversationAndTheWorkingState()
    {
        connection.Setup(item => item.GetHistory(null, It.IsAny<CancellationToken>())).ReturnsAsync(new ChatHistory { Messages = [Message(0, ChatRole.User, "Hi"), Message(1, text: "Hello")], Working = true });
        ChatViewModel chat = Create();

        await chat.Load();

        Assert.Equal(["Hi", "Hello"], chat.Messages.Select(message => message.Text));
        Assert.Equal([true, false], chat.Messages.Select(message => message.IsUser));
        Assert.True(chat.IsWorking);
    }

    [Fact]
    public async Task ShowsWhyTheConversationCouldNotBeLoaded()
    {
        connection.Setup(item => item.GetHistory(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The connection to the server was lost."));
        ChatViewModel chat = Create();

        await chat.Load();

        Assert.Equal("The connection to the server was lost.", chat.Error);
        Assert.Empty(chat.Messages);
    }

    [Fact]
    public void AddsMessagesPushedByTheServerInOrderWithoutDuplicates()
    {
        ChatViewModel chat = Create();

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.MessageAdded, Message = Message(1, text: "second") });
        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.MessageAdded, Message = Message(0, text: "first") });
        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.MessageAdded, Message = Message(1, text: "second") });

        Assert.Equal(["first", "second"], chat.Messages.Select(message => message.Text));
    }

    [Fact]
    public void FollowsTheWorkingStatePushedByTheServer()
    {
        ChatViewModel chat = Create();

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.WorkingChanged, Working = true });
        Assert.True(chat.IsWorking);

        connection.Raise(item => item.EventReceived += null, new RemoteEvent { Kind = RemoteEventKind.WorkingChanged, Working = false });
        Assert.False(chat.IsWorking);
    }

    [Fact]
    public void CannotSendBlankInput()
    {
        ChatViewModel chat = Create();

        Assert.False(chat.SendCommand.CanExecute(null));

        chat.Input = "  ";
        Assert.False(chat.SendCommand.CanExecute(null));

        chat.Input = "Hello";
        Assert.True(chat.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task SendsTheTrimmedInputAndClearsTheBox()
    {
        ChatViewModel chat = Create();
        chat.Input = "  Book a table  ";

        chat.SendCommand.Execute(null);
        await eventually.Be(() => connection.Invocations.Any(call => call.Method.Name == "Prompt"));

        connection.Verify(item => item.Prompt("Book a table", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(string.Empty, chat.Input);
        Assert.Null(chat.Error);
    }

    [Fact]
    public async Task KeepsTheTextAndShowsTheErrorWhenSendingFails()
    {
        connection.Setup(item => item.Prompt(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The AI could not be started."));
        ChatViewModel chat = Create();
        chat.Input = "Hello";

        chat.SendCommand.Execute(null);
        await eventually.Be(() => chat.HasError);

        Assert.Equal("Hello", chat.Input);
        Assert.Equal("The AI could not be started.", chat.Error);
    }

    [Fact]
    public async Task StopsTheAssistant()
    {
        ChatViewModel chat = Create();

        chat.StopCommand.Execute(null);
        await eventually.Be(() => connection.Invocations.Any(call => call.Method.Name == "Interrupt"));

        connection.Verify(item => item.Interrupt(It.IsAny<CancellationToken>()), Times.Once);
    }
}
