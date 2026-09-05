using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NavalCommandOnline;
using Unity.Services.CloudCode.Core;

namespace NavalCommand.CloudCode.Tests;

public sealed class FriendlyMatchTests
{
    private MemoryCloudSaveStore store = null!;
    private NavalCommandModule module = null!;
    private IExecutionContext first = null!;
    private IExecutionContext second = null!;
    private IPushClient push = null!;

    [SetUp]
    public async Task SetUp()
    {
        store = new MemoryCloudSaveStore();
        var friends = new Mock<INavalFriendshipVerifier>();
        friends.Setup(value => value.EnsureFriendAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        module = new NavalCommandModule(store, null!, NullLogger<NavalCommandModule>.Instance, friends.Object);
        first = Mock.Of<IExecutionContext>(value => value.PlayerId == "player-one");
        second = Mock.Of<IExecutionContext>(value => value.PlayerId == "player-two");
        push = Mock.Of<IPushClient>();
        await module.GetOrCreateProfile(first);
        await module.GetOrCreateProfile(second);
    }

    [Test]
    public async Task CancelledInvitation_CannotBeAccepted()
    {
        NavalMatchTicket invite = await Invite();
        await module.CancelFriendlyMatch(first, second.PlayerId!, invite.ticketId);
        Assert.That(await module.GetFriendlyInvites(second), Is.Empty);
        Assert.ThrowsAsync<InvalidOperationException>(() => module.AcceptFriendlyMatch(second, push, invite.ticketId, Fleet()));
        Assert.That((await module.PollFriendlyMatch(first, second.PlayerId!, invite.ticketId)).state, Is.EqualTo("cancelled"));
    }

    [Test]
    public async Task DeclinedInvitation_IsReportedToSender()
    {
        NavalMatchTicket invite = await Invite();
        await module.DeclineFriendlyMatch(second, invite.ticketId);
        Assert.That(await module.GetFriendlyInvites(second), Is.Empty);
        Assert.That((await module.PollFriendlyMatch(first, second.PlayerId!, invite.ticketId)).state, Is.EqualTo("declined"));
    }

    [Test]
    public async Task LostAcceptResponse_RetryAndPollingReturnSameMatch()
    {
        NavalMatchTicket invite = await Invite();
        bool failed = false;
        store.AfterWrite = entity =>
        {
            if (entity == "naval-match-" + invite.ticketId && !failed)
            {
                failed = true;
                throw new IOException("lost response after server commit");
            }
        };
        NavalMatchTicket accepted = await module.AcceptFriendlyMatch(second, push, invite.ticketId, Fleet());
        Assert.That(failed, Is.True, "The simulated lost response must actually occur.");
        store.AfterWrite = null;
        NavalMatchTicket retry = await module.AcceptFriendlyMatch(second, push, invite.ticketId, Fleet());
        Assert.That(retry.matchId, Is.EqualTo(accepted.matchId));
        Assert.That((await module.PollFriendlyMatch(first, second.PlayerId!, invite.ticketId)).matchId, Is.EqualTo(accepted.matchId));
        Assert.That((await module.CancelFriendlyMatch(first, second.PlayerId!, invite.ticketId)).matchId, Is.EqualTo(accepted.matchId));
    }

    [Test]
    public async Task FriendsPlayTurnsReconnectAndFinish_WithoutChangingRank()
    {
        NavalMatchTicket invite = await Invite();
        NavalMatchTicket match = await module.AcceptFriendlyMatch(second, push, invite.ticketId, Fleet());
        NavalPlayerMatchView view = await module.GetMatchView(first, match.matchId);
        Assert.That(view.opponentBoard.Count(cell => cell.ship), Is.Zero);
        NavalMatchAction shot = NavalMatchAction.Create(match.matchId, view.version, NavalActionType.NormalShot);
        shot.row = 9;
        shot.column = 9;
        NavalPlayerMatchView afterShot = await module.SubmitAction(first, push, shot);
        Assert.That(afterShot.currentTurnPlayerId, Is.EqualTo(second.PlayerId));
        Assert.That((await module.SubmitAction(first, push, shot)).version, Is.EqualTo(afterShot.version));
        Assert.That((await module.ReconnectMatch(second))!.version, Is.EqualTo(afterShot.version));
        NavalMatchAction surrender = NavalMatchAction.Create(match.matchId, afterShot.version, NavalActionType.Surrender);
        NavalPlayerMatchView finished = await module.SubmitAction(second, push, surrender);
        Assert.That(finished.status, Is.EqualTo(NavalMatchStatus.Finished));
        Assert.That((await module.GetOrCreateProfile(first)).lifetimeWins, Is.EqualTo(1));
        Assert.That((await module.GetOrCreateProfile(second)).lifetimeLosses, Is.EqualTo(1));
        Assert.That((await module.GetOrCreateProfile(first)).mmr, Is.EqualTo(NavalRankRules.InitialMmr));
        await module.SubmitAction(second, push, surrender);
        Assert.That((await module.GetOrCreateProfile(first)).lifetimeWins, Is.EqualTo(1));
        Assert.That(await module.GetFriendlyInvites(second), Is.Empty);
        Assert.That(await module.ReconnectMatch(first), Is.Null);
        Assert.That((await Invite()).ticketId, Is.Not.EqualTo(invite.ticketId), "A completed duel must allow an immediate rematch invitation.");
    }

    [Test]
    public async Task SimultaneousAccepts_OnlyOneMatchIsCreated()
    {
        NavalMatchTicket invite = await Invite();
        using var barrier = new Barrier(2);
        int writers = 0;
        store.BeforeWrite = entity =>
        {
            if (entity == "naval-invites-" + second.PlayerId && Interlocked.Increment(ref writers) <= 2)
                Assert.That(barrier.SignalAndWait(TimeSpan.FromSeconds(10)), Is.True);
        };
        Task<NavalMatchTicket?> Attempt() => Task.Run(async () =>
        {
            try { return await module.AcceptFriendlyMatch(second, push, invite.ticketId, Fleet()); }
            catch (InvalidOperationException) { return null; }
        });
        NavalMatchTicket?[] results = await Task.WhenAll(Attempt(), Attempt());
        store.BeforeWrite = null;
        Assert.That(writers, Is.GreaterThanOrEqualTo(2));
        Assert.That(results.Count(value => value != null), Is.EqualTo(1));
        Assert.That((await module.ReconnectMatch(first))!.matchId, Is.EqualTo(invite.ticketId));
        Assert.That((await module.ReconnectMatch(second))!.matchId, Is.EqualTo(invite.ticketId));
    }

    [Test]
    public async Task AcceptAndCancelRace_HasOneConsistentOutcome()
    {
        NavalMatchTicket invite = await Invite();
        using var barrier = new Barrier(2);
        int writers = 0;
        store.BeforeWrite = entity =>
        {
            if (entity == "naval-invites-" + second.PlayerId && Interlocked.Increment(ref writers) <= 2)
                Assert.That(barrier.SignalAndWait(TimeSpan.FromSeconds(10)), Is.True);
        };
        async Task<NavalMatchTicket?> Attempt(Func<Task<NavalMatchTicket>> operation)
        {
            try { return await operation(); }
            catch (InvalidOperationException) { return null; }
        }
        NavalMatchTicket?[] results = await Task.WhenAll(
            Task.Run(() => Attempt(() => module.AcceptFriendlyMatch(second, push, invite.ticketId, Fleet()))),
            Task.Run(() => Attempt(() => module.CancelFriendlyMatch(first, second.PlayerId!, invite.ticketId))));
        store.BeforeWrite = null;
        Assert.That(results.Count(value => value != null), Is.EqualTo(1));
        NavalMatchTicket outcome = await module.PollFriendlyMatch(first, second.PlayerId!, invite.ticketId);
        if (outcome.state == "cancelled") Assert.That(await module.ReconnectMatch(first), Is.Null);
        else Assert.That(outcome.matchId, Is.EqualTo(invite.ticketId));
    }

    [Test]
    public async Task UnrelatedPlayer_CannotReadOrCancelMatch()
    {
        NavalMatchTicket invite = await Invite();
        await module.AcceptFriendlyMatch(second, push, invite.ticketId, Fleet());
        var stranger = Mock.Of<IExecutionContext>(value => value.PlayerId == "stranger");
        Assert.ThrowsAsync<NavalRuleException>(() => module.PollFriendlyMatch(stranger, second.PlayerId!, invite.ticketId));
        Assert.ThrowsAsync<NavalRuleException>(() => module.AcceptFriendlyMatch(stranger, push, invite.ticketId, Fleet()));
    }

    private Task<NavalMatchTicket> Invite() => module.CreateFriendlyMatch(first, push, second.PlayerId!, Fleet());
    private static NavalPendingLoadout Fleet()
    {
        var fleet = new NavalPendingLoadout { commanderId = "standard-commander" };
        int[] lengths = { 5, 4, 3, 3, 2 };
        for (int row = 0; row < lengths.Length; row++)
            fleet.ships.Add(new NavalShipPlacement { row = row, column = 0, length = lengths[row], width = lengths[row], height = 1 });
        return fleet;
    }
}
