using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Authentication.PlayerAccounts;

public sealed class UgsNavalOnlineService : INavalOnlineService
{
    [Serializable]
    private sealed class MatchPushEnvelope
    {
        public string matchId;
        public int version;
    }

    private ISubscriptionEvents subscription;
    private bool friendsInitialized;

    public NavalOnlineStatus Status { get; private set; } = NavalOnlineStatus.Offline;
    public string LastError { get; private set; } = string.Empty;
    public bool IsSignedIn => UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance.IsSignedIn;
    public NavalPlayerProfile Profile { get; private set; }
    public NavalEntitlements Entitlements { get; private set; } = new NavalEntitlements();

    public event Action StateChanged;
    public event Action<NavalPlayerMatchView> MatchChanged;

    public async Task InitializeAsync(string environmentName)
    {
        SetStatus(NavalOnlineStatus.Initializing);
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                InitializationOptions options = new InitializationOptions();
                options.SetEnvironmentName(string.IsNullOrWhiteSpace(environmentName) ? "development" : environmentName);
                string localProfile = GetLocalProfileOverride();
                if (!string.IsNullOrWhiteSpace(localProfile))
                    options.SetProfile(localProfile);
                await UnityServices.InitializeAsync(options);
            }

            IAuthenticationService authentication = AuthenticationService.Instance;
            if (!authentication.IsSignedIn && authentication.SessionTokenExists)
            {
                SetStatus(NavalOnlineStatus.SigningIn);
                await authentication.SignInAnonymouslyAsync(new SignInOptions
                {
                    // A missing token must never create an unrelated anonymous account.
                    CreateAccount = false
                });
            }

            SetStatus(authentication.IsSignedIn
                ? NavalOnlineStatus.SignedIn
                : NavalOnlineStatus.Ready);

            if (authentication.IsSignedIn)
            {
                await InitializeSignedInServicesAsync();
            }
        }
        catch (Exception exception)
        {
            SetError(ToUserMessage(exception));
        }
    }

    private static string GetLocalProfileOverride()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length; index++)
        {
            const string prefix = "-naval-profile=";
            if (arguments[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return arguments[index].Substring(prefix.Length).Trim();
            if (string.Equals(arguments[index], "-naval-profile", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < arguments.Length)
                return arguments[index + 1].Trim();
        }
        return string.Empty;
    }

    public async Task SignInWithPlayerAccountAsync()
    {
        await RunSignInAsync(async () =>
        {
            IPlayerAccountService playerAccounts = PlayerAccountService.Instance;
            if (!playerAccounts.IsSignedIn)
            {
                TaskCompletionSource<bool> signInCompleted = new TaskCompletionSource<bool>();
                Action signedIn = () => signInCompleted.TrySetResult(true);
                Action<RequestFailedException> signInFailed = error => signInCompleted.TrySetException(error);

                playerAccounts.SignedIn += signedIn;
                playerAccounts.SignInFailed += signInFailed;
                try
                {
                    // StartSignInAsync only launches the browser. The access token becomes
                    // available later through SignedIn after the browser callback completes.
                    await playerAccounts.StartSignInAsync();
                    Task finished = await Task.WhenAny(signInCompleted.Task, Task.Delay(TimeSpan.FromMinutes(3)));
                    if (finished != signInCompleted.Task)
                        throw new TimeoutException("ANMELDUNG WURDE NICHT RECHTZEITIG ABGESCHLOSSEN");
                    await signInCompleted.Task;
                }
                finally
                {
                    playerAccounts.SignedIn -= signedIn;
                    playerAccounts.SignInFailed -= signInFailed;
                }
            }

            if (string.IsNullOrWhiteSpace(playerAccounts.AccessToken))
                throw new InvalidOperationException("UNITY PLAYER ACCOUNTS HAT KEIN ZUGRIFFSTOKEN GELIEFERT");

            await AuthenticationService.Instance.SignInWithUnityAsync(playerAccounts.AccessToken);
        });
    }

    public async Task SignInWithAppleAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            SetError("APPLE-ANMELDUNG BENÖTIGT EIN GÜLTIGES ID-TOKEN");
            return;
        }

        await RunSignInAsync(() => AuthenticationService.Instance.SignInWithAppleAsync(idToken));
    }

    public async Task SignInWithGoogleAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            SetError("GOOGLE-ANMELDUNG BENÖTIGT EIN GÜLTIGES ID-TOKEN");
            return;
        }

        await RunSignInAsync(() => AuthenticationService.Instance.SignInWithGoogleAsync(idToken));
    }

    public async Task SignOutAsync()
    {
        await UnsubscribeAsync();

        PlayerAccountService.Instance.SignOut();
        if (AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut(true);
        }

        friendsInitialized = false;
        Profile = null;
        Entitlements = new NavalEntitlements();
        SetStatus(NavalOnlineStatus.Ready);
    }

    public async Task<NavalPlayerProfile> RefreshProfileAsync()
    {
        EnsureSignedIn();
        long joinedUnixMs = 0;
        DateTime? createdAt = AuthenticationService.Instance.PlayerInfo?.CreatedAt;
        if (createdAt.HasValue)
            joinedUnixMs = new DateTimeOffset(createdAt.Value.ToUniversalTime()).ToUnixTimeMilliseconds();
        Profile = await CallAsync<NavalPlayerProfile>("GetOrCreateProfile", new Dictionary<string, object>
        {
            { "joinedUnixMs", joinedUnixMs }
        });

        Profile.RefreshDerivedFields();
        RaiseStateChanged();
        return Profile;
    }

    public async Task<NavalPlayerProfile> UpdateAvatarAsync(string avatarImageBase64)
    {
        EnsureSignedIn();
        Profile = await CallAsync<NavalPlayerProfile>("UpdateAvatar", new Dictionary<string, object>
        {
            { "avatarImageBase64", avatarImageBase64 ?? string.Empty }
        });
        Profile.RefreshDerivedFields();
        RaiseStateChanged();
        return Profile;
    }

    public async Task<NavalPlayerProfile> UpdateDisplayNameAsync(string displayName)
    {
        EnsureSignedIn();
        string normalized = ValidateDisplayName(displayName);
        await AuthenticationService.Instance.UpdatePlayerNameAsync(normalized);

        Profile = await CallAsync<NavalPlayerProfile>("UpdateProfile", new Dictionary<string, object>
        {
            { "displayName", normalized }
        });

        Profile.RefreshDerivedFields();
        RaiseStateChanged();
        return Profile;
    }

    public async Task<IReadOnlyList<NavalFriendProfile>> GetFriendsAsync()
    {
        EnsureSignedIn();
        await EnsureFriendsInitializedAsync();
        await FriendsService.Instance.ForceRelationshipsRefreshAsync();
        return FriendsService.Instance.Relationships.Select(ToFriendProfile).ToList();
    }

    public async Task SendFriendRequestAsync(string playerName)
    {
        EnsureSignedIn();
        await EnsureFriendsInitializedAsync();
        if (string.IsNullOrWhiteSpace(playerName)) throw new ArgumentException("SPIELERNAME FEHLT");
        string normalized = playerName.Trim();
        if (LooksLikeFriendCode(normalized))
        {
            string playerId = await CallAsync<string>("ResolveFriendCode", new Dictionary<string, object>
            {
                { "friendCode", normalized.ToUpperInvariant() }
            });
            await FriendsService.Instance.AddFriendAsync(playerId);
        }
        else
        {
            await FriendsService.Instance.AddFriendByNameAsync(normalized);
        }
        RaiseStateChanged();
    }

    public async Task AcceptFriendRequestAsync(string playerId)
    {
        EnsureSignedIn();
        await EnsureFriendsInitializedAsync();
        await FriendsService.Instance.AddFriendAsync(playerId);
        RaiseStateChanged();
    }

    public async Task RemoveFriendAsync(string playerId)
    {
        EnsureSignedIn();
        await EnsureFriendsInitializedAsync();
        Relationship relationship = FriendsService.Instance.Relationships.FirstOrDefault(item => item.Member.Id == playerId);
        if (relationship == null) return;
        if (relationship.Type == RelationshipType.Friend)
            await FriendsService.Instance.DeleteFriendAsync(playerId);
        else if (relationship.Type == RelationshipType.FriendRequest && relationship.Member.Role == MemberRole.Source)
            await FriendsService.Instance.DeleteIncomingFriendRequestAsync(playerId);
        else if (relationship.Type == RelationshipType.FriendRequest)
            await FriendsService.Instance.DeleteOutgoingFriendRequestAsync(playerId);
        RaiseStateChanged();
    }

    public async Task BlockPlayerAsync(string playerId)
    {
        EnsureSignedIn();
        await EnsureFriendsInitializedAsync();
        await FriendsService.Instance.AddBlockAsync(playerId);
        RaiseStateChanged();
    }

    public async Task UnblockPlayerAsync(string playerId)
    {
        EnsureSignedIn();
        await EnsureFriendsInitializedAsync();
        await FriendsService.Instance.DeleteBlockAsync(playerId);
        RaiseStateChanged();
    }

    public async Task<NavalMatchTicket> QueueRankedAsync(NavalPendingLoadout loadout)
    {
        ValidateLoadout(loadout);
        SetStatus(NavalOnlineStatus.Matchmaking);
        try
        {
            NavalMatchTicket ticket = await CallAsync<NavalMatchTicket>("QueueRanked", new Dictionary<string, object>
            {
                { "loadout", loadout }
            });
            return ticket;
        }
        catch (Exception exception)
        {
            SetError(ToUserMessage(exception));
            throw;
        }
    }

    public async Task CancelQueueAsync(string ticketId)
    {
        NavalMatchTicket ticket = await CallAsync<NavalMatchTicket>("CancelQueue",
            new Dictionary<string, object> { { "ticketId", ticketId } });
        if (!string.IsNullOrWhiteSpace(ticket?.matchId))
        {
            await GetMatchViewAsync(ticket.matchId);
            return;
        }
        SetStatus(NavalOnlineStatus.SignedIn);
    }

    public Task<NavalMatchTicket> PollRankedAsync(string ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId)) throw new ArgumentException("MATCHMAKING-TICKET FEHLT");
        return CallAsync<NavalMatchTicket>("PollRanked", new Dictionary<string, object>
        {
            { "ticketId", ticketId }
        });
    }

    public async Task<NavalMatchTicket> CreateFriendlyMatchAsync(string friendPlayerId, NavalPendingLoadout loadout)
    {
        ValidateLoadout(loadout);
        return await CallAsync<NavalMatchTicket>("CreateFriendlyMatch", new Dictionary<string, object>
        {
            { "friendPlayerId", friendPlayerId },
            { "loadout", loadout }
        });
    }

    public async Task<IReadOnlyList<NavalFriendlyInvite>> GetFriendlyInvitesAsync()
    {
        List<NavalFriendlyInvite> invites = await CallAsync<List<NavalFriendlyInvite>>("GetFriendlyInvites", null);
        return invites ?? new List<NavalFriendlyInvite>();
    }

    public async Task<NavalMatchTicket> AcceptFriendlyMatchAsync(string inviteId, NavalPendingLoadout loadout)
    {
        ValidateLoadout(loadout);
        NavalMatchTicket ticket = await CallAsync<NavalMatchTicket>("AcceptFriendlyMatch", new Dictionary<string, object>
        {
            { "inviteId", inviteId },
            { "loadout", loadout }
        });
        return ticket;
    }

    public async Task<NavalPlayerMatchView> GetMatchViewAsync(string matchId)
    {
        NavalPlayerMatchView view = await CallAsync<NavalPlayerMatchView>("GetMatchView",
            new Dictionary<string, object> { { "matchId", matchId } });
        NotifyMatch(view);
        return view;
    }

    public Task<NavalMatchIntro> GetMatchIntroAsync(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("MATCH-ID FEHLT", nameof(matchId));
        return CallAsync<NavalMatchIntro>("GetMatchIntro",
            new Dictionary<string, object> { { "matchId", matchId } });
    }

    public async Task<NavalPlayerMatchView> ReconnectMatchAsync()
    {
        NavalPlayerMatchView view = await CallAsync<NavalPlayerMatchView>("ReconnectMatch", null);
        if (view != null) NotifyMatch(view);
        return view;
    }

    public async Task<NavalPlayerMatchView> SubmitActionAsync(NavalMatchAction action)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.matchId) || string.IsNullOrWhiteSpace(action.actionId))
            throw new ArgumentException("UNGÜLTIGER ONLINE-BEFEHL");

        NavalPlayerMatchView view = await CallAsync<NavalPlayerMatchView>("SubmitAction",
            new Dictionary<string, object> { { "action", action } });
        NotifyMatch(view);
        return view;
    }

    public Task<NavalPlayerMatchView> ClaimTimeoutAsync(string matchId, int expectedVersion)
    {
        NavalMatchAction action = NavalMatchAction.Create(matchId, expectedVersion, NavalActionType.ClaimTimeout);
        return SubmitActionAsync(action);
    }

    public Task<NavalPlayerMatchView> SurrenderAsync(string matchId, int expectedVersion)
    {
        NavalMatchAction action = NavalMatchAction.Create(matchId, expectedVersion, NavalActionType.Surrender);
        return SubmitActionAsync(action);
    }

    public async Task<NavalEntitlements> RefreshEntitlementsAsync()
    {
        EnsureSignedIn();
        Entitlements = await CallAsync<NavalEntitlements>("GetEntitlements", null);
        RaiseStateChanged();
        return Entitlements;
    }

    public async Task<NavalEntitlements> ClaimImaniRewardedAdAsync()
    {
        EnsureSignedIn();
        Entitlements = await CallAsync<NavalEntitlements>("ClaimImaniRewardedAd", null);
        RaiseStateChanged();
        return Entitlements;
    }

    public async Task<NavalEntitlements> RedeemRewardCodeAsync(string code)
    {
        EnsureSignedIn();
        string normalized = (code ?? string.Empty).Trim();
        if (normalized.Length == 0) throw new ArgumentException("REWARD_CODE_REQUIRED", nameof(code));
        Entitlements = await CallAsync<NavalEntitlements>("RedeemRewardCode",
            new Dictionary<string, object> { { "code", normalized } });
        RaiseStateChanged();
        return Entitlements;
    }

    public async Task<IReadOnlyList<NavalLeaderboardEntry>> GetLeaderboardAsync(int limit = 50)
    {
        List<NavalLeaderboardEntry> entries = await CallAsync<List<NavalLeaderboardEntry>>("GetLeaderboard",
            new Dictionary<string, object> { { "limit", Math.Max(1, Math.Min(100, limit)) } });
        return entries ?? new List<NavalLeaderboardEntry>();
    }

    public async Task<NavalPurchaseResult> ValidatePurchaseAsync(NavalPurchaseRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        NavalPurchaseResult result = await CallAsync<NavalPurchaseResult>("ValidatePurchase",
            new Dictionary<string, object> { { "request", request } });
        if (result?.verified == true && result.entitlements != null) Entitlements = result.entitlements;
        RaiseStateChanged();
        return result;
    }

    public async Task DeleteAccountAsync()
    {
        EnsureSignedIn();
        await CallAsync<string>("PrepareAccountDeletion", null);
        await UnsubscribeAsync();
        await AuthenticationService.Instance.DeleteAccountAsync();
        PlayerAccountService.Instance.SignOut();
        friendsInitialized = false;
        Profile = null;
        Entitlements = new NavalEntitlements();
        SetStatus(NavalOnlineStatus.Ready);
    }

    private async Task RunSignInAsync(Func<Task> signIn)
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await InitializeAsync(NavalOnlineEnvironment.Current);
            if (UnityServices.State != ServicesInitializationState.Initialized) return;
        }

        SetStatus(NavalOnlineStatus.SigningIn);
        try
        {
            await signIn();
            await InitializeSignedInServicesAsync();
            if (Status != NavalOnlineStatus.InMatch)
                SetStatus(NavalOnlineStatus.SignedIn);
        }
        catch (Exception exception)
        {
            SetError(ToUserMessage(exception));
        }
    }

    private async Task InitializeSignedInServicesAsync()
    {
        await EnsureFriendsInitializedAsync();
        await RefreshProfileAsync();
        await RefreshEntitlementsAsync();
        await SubscribeToMatchMessagesAsync();
        await ReconnectMatchAsync();
    }

    private async Task EnsureFriendsInitializedAsync()
    {
        if (friendsInitialized) return;
        await FriendsService.Instance.InitializeAsync();
        friendsInitialized = true;
    }

    private async Task SubscribeToMatchMessagesAsync()
    {
        if (subscription != null) return;
        SubscriptionEventCallbacks callbacks = new SubscriptionEventCallbacks();
        callbacks.MessageReceived += HandlePushMessage;
        callbacks.Error += error => SetError("LIVE-VERBINDUNG: " + error);
        subscription = await CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
    }

    private async Task UnsubscribeAsync()
    {
        if (subscription == null) return;
        try { await subscription.UnsubscribeAsync(); }
        catch { }
        subscription = null;
    }

    private async void HandlePushMessage(IMessageReceivedEvent message)
    {
        if (message == null) return;
        if (message.MessageType == "naval.friend.invite")
        {
            RaiseStateChanged();
            return;
        }
        if (message.MessageType != "naval.match.changed") return;
        try
        {
            MatchPushEnvelope envelope = JsonConvert.DeserializeObject<MatchPushEnvelope>(message.Message);
            if (envelope != null && !string.IsNullOrWhiteSpace(envelope.matchId))
            {
                await GetMatchViewAsync(envelope.matchId);
            }
        }
        catch (Exception exception)
        {
            SetError("MATCH-UPDATE FEHLGESCHLAGEN: " + exception.Message);
        }
    }

    private static NavalFriendProfile ToFriendProfile(Relationship relationship)
    {
        bool incoming = relationship.Type == RelationshipType.FriendRequest && relationship.Member.Role == MemberRole.Source;
        return new NavalFriendProfile
        {
            playerId = relationship.Member.Id,
            displayName = StripPlayerNameSuffix(relationship.Member.Profile?.Name),
            online = relationship.Member.Presence != null && relationship.Member.Presence.Availability != Availability.Offline,
            incomingRequest = incoming,
            outgoingRequest = relationship.Type == RelationshipType.FriendRequest && !incoming,
            blocked = relationship.Type == RelationshipType.Block
        };
    }

    private async Task<T> CallAsync<T>(string function, Dictionary<string, object> arguments)
    {
        EnsureSignedIn();
        return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
            NavalOnlineProtocol.CloudModule,
            function,
            arguments ?? new Dictionary<string, object>());
    }

    private void NotifyMatch(NavalPlayerMatchView view)
    {
        if (view == null) return;
        SetStatus(view.status == NavalMatchStatus.Finished ? NavalOnlineStatus.SignedIn : NavalOnlineStatus.InMatch);
        MatchChanged?.Invoke(view);
    }

    private void EnsureSignedIn()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            throw new InvalidOperationException("ONLINE-ANMELDUNG ERFORDERLICH");
    }

    private void SetStatus(NavalOnlineStatus status)
    {
        Status = status;
        if (status != NavalOnlineStatus.Error) LastError = string.Empty;
        RaiseStateChanged();
    }

    private void SetError(string error)
    {
        LastError = string.IsNullOrWhiteSpace(error) ? "UNBEKANNTER ONLINE-FEHLER" : error;
        Status = NavalOnlineStatus.Error;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke();
    }

    private static string ValidateDisplayName(string value)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length < 3 || normalized.Length > 20)
            throw new ArgumentException("NAME MUSS 3 BIS 20 ZEICHEN HABEN");

        for (int i = 0; i < normalized.Length; i++)
        {
            char character = normalized[i];
            if (!char.IsLetterOrDigit(character) && character != ' ' && character != '-' && character != '_')
                throw new ArgumentException("NAME ENTHÄLT UNGÜLTIGE ZEICHEN");
        }
        return normalized;
    }

    private static void ValidateLoadout(NavalPendingLoadout loadout)
    {
        if (loadout == null || string.IsNullOrWhiteSpace(loadout.commanderId) || loadout.ships == null || loadout.ships.Count == 0)
            throw new ArgumentException("ONLINE-FLOTTE IST NICHT BEREIT");
    }

    private static string StripPlayerNameSuffix(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return "COMMANDER";
        int suffix = playerName.LastIndexOf('#');
        return suffix > 0 ? playerName.Substring(0, suffix) : playerName;
    }

    private static string CreateLocalFriendCode(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return "--------";
        return playerId.Substring(0, Math.Min(8, playerId.Length)).ToUpperInvariant();
    }

    private static bool LooksLikeFriendCode(string value)
    {
        if (value == null || value.Length != 10) return false;
        for (int index = 0; index < value.Length; index++)
            if (!Uri.IsHexDigit(value[index])) return false;
        return true;
    }

    private static string ToUserMessage(Exception exception)
    {
        string message = exception?.Message ?? "UNBEKANNTER FEHLER";
        if (message.IndexOf("Singleton is not initialized", StringComparison.OrdinalIgnoreCase) >= 0)
            return "UNITY PLAYER ACCOUNTS IST IM PROJEKT NOCH NICHT KONFIGURIERT";
        if (message.IndexOf("client", StringComparison.OrdinalIgnoreCase) >= 0 &&
            message.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
            return "PLAYER-ACCOUNTS CLIENT-ID FEHLT // UNITY DASHBOARD KONFIGURIEREN";
        if (message.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 &&
            message.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
            return "UNITY-CLOUD-PROJEKT NOCH NICHT VERKNÜPFT";
        return message.ToUpperInvariant();
    }
}
