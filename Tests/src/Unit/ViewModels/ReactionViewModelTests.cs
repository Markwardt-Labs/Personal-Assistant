namespace Markwardt.PersonalAssistant.Tests.Unit;

public sealed class ReactionViewModelTests
{
    private readonly Mock<IServerConnection> connection = new();

    private ReactionViewModel Create(ReactionKind initial = ReactionKind.None)
        => new(connection.Object, ReactionTarget.News, "https://example.com/a", "New chip", "Tech", initial);

    [Fact]
    public void StartsWithWhatTheUserThoughtAlready()
    {
        ReactionViewModel reaction = Create(ReactionKind.Dislike);

        Assert.True(reaction.IsDisliked);
        Assert.False(reaction.IsLiked);
    }

    [Fact]
    public async Task LikesAndTellsTheServerWhatTheEntryWas()
    {
        ReactionViewModel reaction = Create();

        reaction.LikeCommand.Execute(null);
        await new Eventually().Be(() => reaction.IsLiked);

        connection.Verify(item => item.React(ReactionTarget.News, "https://example.com/a", "New chip", "Tech", ReactionKind.Like, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PressingTheSameButtonAgainTakesTheReactionBack()
    {
        ReactionViewModel reaction = Create(ReactionKind.Like);

        reaction.LikeCommand.Execute(null);
        await new Eventually().Be(() => !reaction.IsLiked);

        connection.Verify(item => item.React(It.IsAny<ReactionTarget>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), ReactionKind.None, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PressingTheOtherButtonSwitchesTheReaction()
    {
        ReactionViewModel reaction = Create(ReactionKind.Like);

        reaction.DislikeCommand.Execute(null);
        await new Eventually().Be(() => reaction.IsDisliked);

        Assert.False(reaction.IsLiked);
    }

    [Fact]
    public async Task GoesBackAndShowsTheErrorWhenTheServerRefuses()
    {
        connection.Setup(item => item.React(It.IsAny<ReactionTarget>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ReactionKind>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RemoteException("The connection to the server was lost."));
        ReactionViewModel reaction = Create();

        reaction.LikeCommand.Execute(null);
        await new Eventually().Be(() => reaction.HasError);

        Assert.False(reaction.IsLiked);
        Assert.Equal(ReactionKind.None, reaction.Kind);
    }
}
