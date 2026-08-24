using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface INavalOnlineService
{
    NavalOnlineStatus Status { get; }
    string LastError { get; }
    bool IsSignedIn { get; }
    NavalPlayerProfile Profile { get; }
    NavalEntitlements Entitlements { get; }

    event Action StateChanged;
    event Action<NavalPlayerMatchView> MatchChanged;

    Task InitializeAsync(string environmentName);
    Task SignInWithPlayerAccountAsync();
    Task SignInWithAppleAsync(string idToken);
    Task SignInWithGoogleAsync(string idToken);
    Task SignOutAsync();
    Task<NavalPlayerProfile> RefreshProfileAsync();
    Task<NavalPlayerProfile> UpdateDisplayNameAsync(string displayName);
    Task<NavalPlayerProfile> UpdateAvatarAsync(string avatarImageBase64);
    Task<IReadOnlyList<NavalFriendProfile>> GetFriendsAsync();
    Task SendFriendRequestAsync(string playerName);
    Task AcceptFriendRequestAsync(string playerId);
    Task RemoveFriendAsync(string playerId);
    Task BlockPlayerAsync(string playerId);
    Task UnblockPlayerAsync(string playerId);
    Task<NavalMatchTicket> QueueRankedAsync(NavalPendingLoadout loadout);
    Task<NavalMatchTicket> PollRankedAsync(string ticketId);
    Task CancelQueueAsync(string ticketId);
    Task<NavalMatchTicket> CreateFriendlyMatchAsync(string friendPlayerId, NavalPendingLoadout loadout);
    Task<IReadOnlyList<NavalFriendlyInvite>> GetFriendlyInvitesAsync();
    Task<NavalMatchTicket> AcceptFriendlyMatchAsync(string inviteId, NavalPendingLoadout loadout);
    Task<NavalPlayerMatchView> GetMatchViewAsync(string matchId);
    Task<NavalMatchIntro> GetMatchIntroAsync(string matchId);
    Task<NavalPlayerMatchView> ReconnectMatchAsync();
    Task<NavalPlayerMatchView> SubmitActionAsync(NavalMatchAction action);
    Task<NavalPlayerMatchView> ClaimTimeoutAsync(string matchId, int expectedVersion);
    Task<NavalPlayerMatchView> SurrenderAsync(string matchId, int expectedVersion);
    Task<NavalEntitlements> RefreshEntitlementsAsync();
    Task<NavalEntitlements> ClaimImaniRewardedAdAsync();
    Task<NavalEntitlements> RedeemRewardCodeAsync(string code);
    Task<IReadOnlyList<NavalLeaderboardEntry>> GetLeaderboardAsync(int limit = 50);
    Task<NavalPurchaseResult> ValidatePurchaseAsync(NavalPurchaseRequest request);
    Task DeleteAccountAsync();
}
