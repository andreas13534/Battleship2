using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal sealed class FakeNavalOnlineService : INavalOnlineService
{
    public NavalOnlineStatus Status { get; set; } = NavalOnlineStatus.Ready;
    public string LastError { get; set; } = string.Empty;
    public bool IsSignedIn { get; set; } = true;
    public NavalPlayerProfile Profile { get; set; } = new NavalPlayerProfile();
    public NavalEntitlements Entitlements { get; set; } = new NavalEntitlements();

    public event Action StateChanged;
    public event Action<NavalPlayerMatchView> MatchChanged;

    public Func<Task<IReadOnlyList<NavalFriendProfile>>> GetFriendsHandler { get; set; }
    public Func<Task<IReadOnlyList<NavalFriendlyInvite>>> GetFriendlyInvitesHandler { get; set; }
    public Func<NavalPendingLoadout, Task<NavalMatchTicket>> QueueRankedHandler { get; set; }
    public Func<string, Task<NavalMatchTicket>> PollRankedHandler { get; set; }
    public Func<string, string, Task<NavalMatchTicket>> PollFriendlyMatchHandler { get; set; }
    public Func<string, string, Task<NavalMatchTicket>> CancelFriendlyMatchHandler { get; set; }
    public Func<string, Task> DeclineFriendlyMatchHandler { get; set; }
    public Func<string, Task<NavalPlayerMatchView>> GetMatchViewHandler { get; set; }
    public Func<Task<NavalPlayerMatchView>> ReconnectMatchHandler { get; set; }
    public Func<string, Task> CancelQueueHandler { get; set; }
    public Func<Task> SignInWithPlayerAccountHandler { get; set; }
    public Func<Task> SignOutHandler { get; set; }

    public int GetFriendsCallCount { get; private set; }
    public int GetFriendlyInvitesCallCount { get; private set; }
    public int QueueRankedCallCount { get; private set; }
    public int PollRankedCallCount { get; private set; }
    public int PollFriendlyMatchCallCount { get; private set; }
    public int CancelFriendlyMatchCallCount { get; private set; }
    public int DeclineFriendlyMatchCallCount { get; private set; }
    public int GetMatchViewCallCount { get; private set; }
    public int ReconnectMatchCallCount { get; private set; }
    public int CancelQueueCallCount { get; private set; }
    public int SignInWithPlayerAccountCallCount { get; private set; }
    public int SignOutCallCount { get; private set; }

    public void RaiseStateChanged() => StateChanged?.Invoke();
    public void RaiseMatchChanged(NavalPlayerMatchView view) => MatchChanged?.Invoke(view);

    public Task InitializeAsync(string environmentName) => Task.CompletedTask;
    public Task SignInWithUsernamePasswordAsync(string username, string password) => Task.CompletedTask;
    public Task SignUpWithUsernamePasswordAsync(string username, string password) => Task.CompletedTask;
    public async Task SignInWithPlayerAccountAsync()
    {
        SignInWithPlayerAccountCallCount++;
        if (SignInWithPlayerAccountHandler != null)
            await SignInWithPlayerAccountHandler();
        IsSignedIn = true;
    }
    public Task SignInWithAppleAsync(string idToken) => Task.CompletedTask;
    public Task SignInWithGoogleAsync(string idToken) => Task.CompletedTask;
    public async Task SignOutAsync()
    {
        SignOutCallCount++;
        if (SignOutHandler != null)
            await SignOutHandler();
        IsSignedIn = false;
        Profile = null;
    }
    public Task<NavalPlayerProfile> RefreshProfileAsync() => Task.FromResult(Profile);
    public Task<NavalPlayerProfile> UpdateDisplayNameAsync(string displayName) => Task.FromResult(Profile);
    public Task<NavalPlayerProfile> UpdateAvatarAsync(string avatarImageBase64) => Task.FromResult(Profile);

    public Task<IReadOnlyList<NavalFriendProfile>> GetFriendsAsync()
    {
        GetFriendsCallCount++;
        return GetFriendsHandler == null
            ? Task.FromResult<IReadOnlyList<NavalFriendProfile>>(new List<NavalFriendProfile>())
            : GetFriendsHandler();
    }

    public Task SendFriendRequestAsync(string playerName) => Task.CompletedTask;
    public Task AcceptFriendRequestAsync(string playerId) => Task.CompletedTask;
    public Task RemoveFriendAsync(string playerId) => Task.CompletedTask;
    public Task BlockPlayerAsync(string playerId) => Task.CompletedTask;
    public Task UnblockPlayerAsync(string playerId) => Task.CompletedTask;

    public Task<NavalMatchTicket> QueueRankedAsync(NavalPendingLoadout loadout)
    {
        QueueRankedCallCount++;
        return QueueRankedHandler == null
            ? Task.FromResult(new NavalMatchTicket())
            : QueueRankedHandler(loadout);
    }

    public Task<NavalMatchTicket> PollRankedAsync(string ticketId)
    {
        PollRankedCallCount++;
        return PollRankedHandler == null
            ? Task.FromResult(new NavalMatchTicket())
            : PollRankedHandler(ticketId);
    }

    public Task CancelQueueAsync(string ticketId)
    {
        CancelQueueCallCount++;
        return CancelQueueHandler == null ? Task.CompletedTask : CancelQueueHandler(ticketId);
    }

    public Task<NavalMatchTicket> CreateFriendlyMatchAsync(string friendPlayerId, NavalPendingLoadout loadout) =>
        Task.FromResult(new NavalMatchTicket());

    public Task<IReadOnlyList<NavalFriendlyInvite>> GetFriendlyInvitesAsync()
    {
        GetFriendlyInvitesCallCount++;
        return GetFriendlyInvitesHandler == null
            ? Task.FromResult<IReadOnlyList<NavalFriendlyInvite>>(new List<NavalFriendlyInvite>())
            : GetFriendlyInvitesHandler();
    }

    public Task<NavalMatchTicket> AcceptFriendlyMatchAsync(string inviteId, NavalPendingLoadout loadout) =>
        Task.FromResult(new NavalMatchTicket());

    public Task<NavalMatchTicket> PollFriendlyMatchAsync(string friendPlayerId, string inviteId)
    {
        PollFriendlyMatchCallCount++;
        return PollFriendlyMatchHandler == null
            ? Task.FromResult(new NavalMatchTicket())
            : PollFriendlyMatchHandler(friendPlayerId, inviteId);
    }

    public Task<NavalMatchTicket> CancelFriendlyMatchAsync(string friendPlayerId, string inviteId)
    {
        CancelFriendlyMatchCallCount++;
        return CancelFriendlyMatchHandler == null
            ? Task.FromResult(new NavalMatchTicket())
            : CancelFriendlyMatchHandler(friendPlayerId, inviteId);
    }

    public Task DeclineFriendlyMatchAsync(string inviteId)
    {
        DeclineFriendlyMatchCallCount++;
        return DeclineFriendlyMatchHandler == null
            ? Task.CompletedTask
            : DeclineFriendlyMatchHandler(inviteId);
    }

    public Task<NavalPlayerMatchView> GetMatchViewAsync(string matchId)
    {
        GetMatchViewCallCount++;
        return GetMatchViewHandler == null
            ? Task.FromResult<NavalPlayerMatchView>(null)
            : GetMatchViewHandler(matchId);
    }
    public Task<NavalMatchIntro> GetMatchIntroAsync(string matchId) => Task.FromResult<NavalMatchIntro>(null);

    public Task<NavalPlayerMatchView> ReconnectMatchAsync()
    {
        ReconnectMatchCallCount++;
        return ReconnectMatchHandler == null
            ? Task.FromResult<NavalPlayerMatchView>(null)
            : ReconnectMatchHandler();
    }

    public Task<NavalPlayerMatchView> SubmitActionAsync(NavalMatchAction action) =>
        Task.FromResult<NavalPlayerMatchView>(null);
    public Task<NavalPlayerMatchView> ClaimTimeoutAsync(string matchId, int expectedVersion) =>
        Task.FromResult<NavalPlayerMatchView>(null);
    public Task<NavalPlayerMatchView> SurrenderAsync(string matchId, int expectedVersion) =>
        Task.FromResult<NavalPlayerMatchView>(null);
    public Task<NavalEntitlements> RefreshEntitlementsAsync() => Task.FromResult(Entitlements);
    public Task<NavalEntitlements> ClaimImaniRewardedAdAsync() => Task.FromResult(Entitlements);
    public Task<NavalEntitlements> RedeemRewardCodeAsync(string code) => Task.FromResult(Entitlements);
    public Task<IReadOnlyList<NavalLeaderboardEntry>> GetLeaderboardAsync(int limit = 50) =>
        Task.FromResult<IReadOnlyList<NavalLeaderboardEntry>>(new List<NavalLeaderboardEntry>());
    public Task<NavalPurchaseResult> ValidatePurchaseAsync(NavalPurchaseRequest request) =>
        Task.FromResult(new NavalPurchaseResult());
    public Task DeleteAccountAsync() => Task.CompletedTask;
}
