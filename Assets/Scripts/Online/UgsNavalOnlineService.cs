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
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using Unity.Services.Authentication.PlayerAccounts;

public sealed class UgsNavalOnlineService : INavalOnlineService, IDisposable
{
    [Serializable]
    private sealed class MatchPushEnvelope
    {
        public string matchId;
        public int version;
    }

    private ISubscriptionEvents subscription;
    private bool friendsInitialized;
    private Task initializationTask;
    private Task friendsInitializationTask;
    private bool signInPending;
    private bool disposed;
    private int sessionVersion;
    private NavalPlayerMatchView latestMatch;
    private IAuthenticationService observedAuthentication;

    public NavalOnlineStatus Status { get; private set; } = NavalOnlineStatus.Offline;
    public string LastError { get; private set; } = string.Empty;
    public bool IsSignedIn => UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance.IsSignedIn;
    public NavalPlayerProfile Profile { get; private set; }
    public NavalEntitlements Entitlements { get; private set; } = new NavalEntitlements();

    public event Action StateChanged;
    public event Action<NavalPlayerMatchView> MatchChanged;

    public Task InitializeAsync(string environmentName)
    {
        if (initializationTask != null && !initializationTask.IsCompleted) return initializationTask;
        initializationTask = InitializeCoreAsync(environmentName);
        return initializationTask;
    }

    private async Task InitializeCoreAsync(string environmentName)
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
            if (observedAuthentication == null)
            {
                observedAuthentication = authentication;
                authentication.Expired += HandleSessionExpired;
                authentication.SignedOut += HandleSessionSignedOut;
            }
            if (!authentication.IsSignedIn && authentication.SessionTokenExists)
            {
                SetStatus(NavalOnlineStatus.SigningIn);
                try
                {
                    await authentication.SignInAnonymouslyAsync(new SignInOptions { CreateAccount = false });
                }
                catch (AuthenticationException)
                {
                    // An expired/revoked saved session must still allow interactive login.
                    authentication.SignOut(true);
                }
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
#if UNITY_WEBGL && !UNITY_EDITOR
        SetError("DIE ANMELDUNG BENÖTIGT DIE ANDROID-, IOS- ODER WINDOWS-APP.");
        await Task.CompletedTask;
        return;
#else
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
                    Task timeout = Task.Delay(TimeSpan.FromMinutes(3));
                    Task launch = playerAccounts.StartSignInAsync();
                    Task first = await Task.WhenAny(launch, signInCompleted.Task, timeout);
                    if (first == timeout)
                        throw new TimeoutException("ANMELDUNG WURDE NICHT RECHTZEITIG ABGESCHLOSSEN");
                    if (first == launch) await launch;
                    Task finished = await Task.WhenAny(signInCompleted.Task, timeout);
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
#endif
    }

    public Task SignInWithUsernamePasswordAsync(string username, string password)
    {
        return RunSignInAsync(() => AuthenticationService.Instance.SignInWithUsernamePasswordAsync(
            (username ?? string.Empty).Trim(), password ?? string.Empty));
    }

    public Task SignUpWithUsernamePasswordAsync(string username, string password)
    {
        return RunSignInAsync(() => AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(
            (username ?? string.Empty).Trim(), password ?? string.Empty));
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
        sessionVersion++;
        latestMatch = null;
        await UnsubscribeAsync();
        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            PlayerAccountService.Instance.SignOut();
#endif
            AuthenticationService.Instance.SignOut(true);
        }

        friendsInitialized = false;
        friendsInitializationTask = null;
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
        int session = sessionVersion;
        string normalized = ValidateDisplayName(displayName);
        await AuthenticationService.Instance.UpdatePlayerNameAsync(normalized);
        if (disposed || session != sessionVersion || !IsSignedIn)
            throw new OperationCanceledException("SITZUNG BEENDET");

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
        int session = sessionVersion;
        await EnsureFriendsInitializedAsync();
        await FriendsService.Instance.ForceRelationshipsRefreshAsync();
        if (disposed || session != sessionVersion || !IsSignedIn)
            throw new OperationCanceledException("SITZUNG BEENDET");
        return FriendsService.Instance.Relationships.Select(ToFriendProfile).ToList();
    }

    public async Task SendFriendRequestAsync(string playerName)
    {
        EnsureSignedIn();
        await EnsureFriendsInitializedAsync();
        if (string.IsNullOrWhiteSpace(playerName)) throw new ArgumentException("SPIELERNAME FEHLT");
        string normalized = playerName.Trim();
        try
        {
            if (LooksLikeFriendCode(normalized))
            {
                string playerId = await CallAsync<string>("ResolveFriendCode", new Dictionary<string, object>
                {
                    { "friendCode", normalized.ToUpperInvariant() }
                });
                if (string.Equals(playerId, AuthenticationService.Instance.PlayerId, StringComparison.Ordinal))
                    throw new InvalidOperationException("DU KANNST DICH NICHT SELBST HINZUFÜGEN");
                await FriendsService.Instance.AddFriendAsync(playerId);
            }
            else
            {
                if (!LooksLikeUnityPlayerName(normalized))
                    throw new ArgumentException("VOLLSTÄNDIGE SPIELER-ID MIT #NUMMER ODER FREUNDESCODE EINGEBEN");
                if (string.Equals(normalized, AuthenticationService.Instance.PlayerName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("DU KANNST DICH NICHT SELBST HINZUFÜGEN");
                await FriendsService.Instance.AddFriendByNameAsync(normalized);
            }
        }
        catch (FriendsServiceException exception)
        {
            throw new InvalidOperationException(ToFriendRequestMessage(exception), exception);
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

    public Task<NavalMatchTicket> PollFriendlyMatchAsync(string friendPlayerId, string inviteId)
    {
        return CallAsync<NavalMatchTicket>("PollFriendlyMatch", new Dictionary<string, object>
        {
            { "friendPlayerId", friendPlayerId }, { "inviteId", inviteId }
        });
    }

    public Task<NavalMatchTicket> CancelFriendlyMatchAsync(string friendPlayerId, string inviteId)
    {
        return CallAsync<NavalMatchTicket>("CancelFriendlyMatch", new Dictionary<string, object>
        {
            { "friendPlayerId", friendPlayerId }, { "inviteId", inviteId }
        });
    }

    public async Task DeclineFriendlyMatchAsync(string inviteId)
    {
        await CallAsync<string>("DeclineFriendlyMatch", new Dictionary<string, object> { { "inviteId", inviteId } });
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
        return NotifyMatch(view);
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
        return view == null ? null : NotifyMatch(view);
    }

    public async Task<NavalPlayerMatchView> SubmitActionAsync(NavalMatchAction action)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.matchId) || string.IsNullOrWhiteSpace(action.actionId))
            throw new ArgumentException("UNGÜLTIGER ONLINE-BEFEHL");

        NavalPlayerMatchView view = await CallAsync<NavalPlayerMatchView>("SubmitAction",
            new Dictionary<string, object> { { "action", action } });
        return NotifyMatch(view);
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
#if !UNITY_WEBGL || UNITY_EDITOR
        PlayerAccountService.Instance.SignOut();
#endif
        friendsInitialized = false;
        Profile = null;
        Entitlements = new NavalEntitlements();
        SetStatus(NavalOnlineStatus.Ready);
    }

    private async Task RunSignInAsync(Func<Task> signIn)
    {
        if (signInPending || disposed) return;
        signInPending = true;
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await InitializeAsync(NavalOnlineEnvironment.Current);
                if (UnityServices.State != ServicesInitializationState.Initialized) return;
            }
            SetStatus(NavalOnlineStatus.SigningIn);
            if (!IsSignedIn) await signIn();
            await InitializeSignedInServicesAsync();
            if (Status != NavalOnlineStatus.InMatch)
                SetStatus(NavalOnlineStatus.SignedIn);
        }
        catch (Exception exception)
        {
            SetError(ToUserMessage(exception));
        }
        finally { signInPending = false; }
    }

    private async Task InitializeSignedInServicesAsync()
    {
        await RefreshProfileAsync();
        await RefreshEntitlementsAsync();
        await AuthenticationService.Instance.GetPlayerNameAsync();
        // Live subscriptions enhance the polling path; a push outage must not block login.
        try { await EnsureFriendsInitializedAsync(); }
        catch (Exception) { friendsInitialized = false; }
        try { await SubscribeToMatchMessagesAsync(); }
        catch (Exception) { subscription = null; }
        // An interrupted match must not make an otherwise valid account login fail.
        // The UI retries the match lookup on resume and through its polling loop.
        try { await ReconnectMatchAsync(); }
        catch (Exception) { latestMatch = null; }
    }

    private Task EnsureFriendsInitializedAsync()
    {
        if (friendsInitialized) return Task.CompletedTask;
        if (friendsInitializationTask != null && !friendsInitializationTask.IsCompleted) return friendsInitializationTask;
        friendsInitializationTask = InitializeFriendsCoreAsync();
        return friendsInitializationTask;
    }

    private async Task InitializeFriendsCoreAsync()
    {
        int session = sessionVersion;
        await FriendsService.Instance.InitializeAsync();
        if (session != sessionVersion || disposed || !IsSignedIn) return;
        await FriendsService.Instance.SetPresenceAvailabilityAsync(Availability.Online);
        friendsInitialized = true;
    }

    private async Task SubscribeToMatchMessagesAsync()
    {
        if (subscription != null) return;
        int session = sessionVersion;
        SubscriptionEventCallbacks callbacks = new SubscriptionEventCallbacks();
        callbacks.MessageReceived += HandlePushMessage;
        // Polling keeps the game running while the SDK reconnects its live channel.
        callbacks.Error += error => RaiseStateChanged();
        ISubscriptionEvents created = await CloudCodeService.Instance.SubscribeToPlayerMessagesAsync(callbacks);
        if (disposed || session != sessionVersion || !IsSignedIn)
        {
            await created.UnsubscribeAsync();
            return;
        }
        subscription = created;
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
        if (message == null || disposed || !IsSignedIn) return;
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
        catch (Exception)
        {
            // The next poll retries without turning an otherwise valid account into an error state.
        }
    }

    private static NavalFriendProfile ToFriendProfile(Relationship relationship)
    {
        bool incoming = relationship.Type == RelationshipType.FriendRequest && relationship.Member.Role == MemberRole.Source;
        return new NavalFriendProfile
        {
            playerId = relationship.Member.Id,
            displayName = string.IsNullOrWhiteSpace(relationship.Member.Profile?.Name)
                ? "COMMANDER"
                : relationship.Member.Profile.Name,
            online = relationship.Member.Presence != null &&
                (relationship.Member.Presence.Availability == Availability.Online ||
                 relationship.Member.Presence.Availability == Availability.Away ||
                 relationship.Member.Presence.Availability == Availability.Busy),
            incomingRequest = incoming,
            outgoingRequest = relationship.Type == RelationshipType.FriendRequest && !incoming,
            blocked = relationship.Type == RelationshipType.Block
        };
    }

    private async Task<T> CallAsync<T>(string function, Dictionary<string, object> arguments)
    {
        EnsureSignedIn();
        int session = sessionVersion;
        string playerId = AuthenticationService.Instance.PlayerId;
        T result;
        try
        {
            result = await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
                NavalOnlineProtocol.CloudModule, function, arguments ?? new Dictionary<string, object>());
        }
        catch (Exception exception) { throw new InvalidOperationException(ToUserMessage(exception), exception); }
        if (disposed || session != sessionVersion || !IsSignedIn || AuthenticationService.Instance.PlayerId != playerId)
            throw new OperationCanceledException("SITZUNG BEENDET");
        return result;
    }

    private NavalPlayerMatchView NotifyMatch(NavalPlayerMatchView view)
    {
        if (view == null || disposed) return view;
        if (latestMatch != null && latestMatch.matchId == view.matchId && view.version < latestMatch.version) return latestMatch;
        latestMatch = view;
        NavalOnlineStatus status = view.status == NavalMatchStatus.Finished ? NavalOnlineStatus.SignedIn : NavalOnlineStatus.InMatch;
        if (Status != status) SetStatus(status);
        MatchChanged?.Invoke(view);
        return view;
    }

    private void EnsureSignedIn()
    {
        if (disposed || !IsSignedIn)
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
        if (!disposed) StateChanged?.Invoke();
    }

    private void HandleSessionExpired()
    {
        AuthenticationService.Instance.SignOut();
        SetError("SITZUNG ABGELAUFEN. BITTE ERNEUT ANMELDEN.");
    }

    private void HandleSessionSignedOut()
    {
        sessionVersion++;
        Profile = null;
        Entitlements = new NavalEntitlements();
        latestMatch = null;
        friendsInitialized = false;
        friendsInitializationTask = null;
        _ = UnsubscribeAsync();
        SetStatus(NavalOnlineStatus.Ready);
    }

    public void Dispose()
    {
        disposed = true;
        sessionVersion++;
        if (observedAuthentication != null)
        {
            observedAuthentication.Expired -= HandleSessionExpired;
            observedAuthentication.SignedOut -= HandleSessionSignedOut;
            observedAuthentication = null;
        }
        _ = UnsubscribeAsync();
        StateChanged = null;
        MatchChanged = null;
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

    private static bool LooksLikeUnityPlayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        int separator = value.LastIndexOf('#');
        if (separator < 3 || separator >= value.Length - 1) return false;
        for (int index = separator + 1; index < value.Length; index++)
            if (!char.IsDigit(value[index])) return false;
        return true;
    }

    private static string ToFriendRequestMessage(FriendsServiceException exception)
    {
        switch (exception.ErrorCode)
        {
            case FriendsErrorCode.RelationshipAlreadyExists:
            case FriendsErrorCode.FriendshipAlreadyExists:
                return "ANFRAGE BEREITS GESENDET ODER SPIELER BEREITS BEFREUNDET";
            case FriendsErrorCode.UserTargetingSelf:
            case FriendsErrorCode.InvalidCreateTarget:
                return "DU KANNST DICH NICHT SELBST HINZUFÜGEN";
            case FriendsErrorCode.ActionUnauthorizedWhenBlocked:
                return "FREUNDESANFRAGE NICHT MÖGLICH, SPIELER IST BLOCKIERT";
            case FriendsErrorCode.FriendLimitReached:
            case FriendsErrorCode.FriendRequestLimitReached:
                return "DEIN FREUNDES- ODER ANFRAGENLIMIT IST ERREICHT";
            case FriendsErrorCode.TargetsFriendLimitReached:
                return "DIESER SPIELER KANN KEINE WEITEREN FREUNDE ANNEHMEN";
            case FriendsErrorCode.ProjectNotEnabled:
                return "UNITY FRIENDS IST FÜR DIESES ENVIRONMENT NICHT AKTIVIERT";
            case FriendsErrorCode.NetworkError:
                return "KEINE VERBINDUNG ZUM FREUNDEDIENST. BITTE ERNEUT VERSUCHEN";
        }

        if ((int)exception.StatusCode == 404)
            return "SPIELER NICHT GEFUNDEN. SPIELER-ID UND #NUMMER PRÜFEN";
        return "FREUNDESANFRAGE KONNTE NICHT GESENDET WERDEN. BITTE ERNEUT VERSUCHEN";
    }

    private static string ToUserMessage(Exception exception)
    {
        string message = exception?.Message ?? "UNBEKANNTER FEHLER";
        var errors = new Dictionary<string, string>
        {
            { "INVITE_NOT_FOUND", "DIE EINLADUNG IST NICHT MEHR VERFÜGBAR" },
            { "INVITE_EXPIRED", "DIE EINLADUNG IST ABGELAUFEN" },
            { "FRIEND_CODE_NOT_FOUND", "FREUNDESCODE NICHT GEFUNDEN" },
            { "INVALID_FRIEND_CODE", "FREUNDESCODE PRÜFEN" },
            { "FRIENDSHIP_REQUIRED", "BITTE ZUERST DIE FREUNDESANFRAGE ANNEHMEN" },
            { "FRIEND_ALREADY_IN_MATCH", "DEIN FREUND SPIELT GERADE" },
            { "ACTIVE_MATCH_EXISTS", "DU HAST BEREITS EIN LAUFENDES SPIEL" },
            { "INVITE_LIMIT_REACHED", "DEIN FREUND HAT ZU VIELE OFFENE EINLADUNGEN" },
            { "MATCH_BUSY_RETRY", "DUELL WIRD VORBEREITET. BITTE ERNEUT VERSUCHEN." },
            { "STALE_MATCH_VERSION", "SPIELSTAND WIRD AKTUALISIERT" },
            { "NOT_YOUR_TURN", "DEIN GEGNER IST AM ZUG" }
        };
        foreach (var error in errors)
            if (message.IndexOf(error.Key, StringComparison.OrdinalIgnoreCase) >= 0) return error.Value;
        if (exception is TimeoutException) return "ANMELDUNG ABGELAUFEN. BITTE ERNEUT VERSUCHEN.";
        if (message.IndexOf("Singleton is not initialized", StringComparison.OrdinalIgnoreCase) >= 0)
            return "UNITY PLAYER ACCOUNTS IST IM PROJEKT NOCH NICHT KONFIGURIERT";
        if (message.IndexOf("USERNAME_ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase) >= 0)
            return "DIESER NUTZERNAME IST BEREITS VERGEBEN";
        if (message.IndexOf("INVALID_USERNAME_PASSWORD", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("INVALID_CREDENTIALS", StringComparison.OrdinalIgnoreCase) >= 0)
            return "NUTZERNAME ODER PASSWORT IST FALSCH";
        if (message.IndexOf("ID_PROVIDER_NOT_FOUND", StringComparison.OrdinalIgnoreCase) >= 0)
            return "BROWSER-ANMELDUNG IST IM UNITY DASHBOARD NOCH NICHT AKTIVIERT";
        if (message.IndexOf("INVALID_PARAMETERS", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("correct format", StringComparison.OrdinalIgnoreCase) >= 0)
            return "NUTZERNAME ODER PASSWORT ERFÜLLT DIE ANFORDERUNGEN NICHT";
        if (message.IndexOf("client", StringComparison.OrdinalIgnoreCase) >= 0 &&
            message.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
            return "PLAYER-ACCOUNTS CLIENT-ID FEHLT // UNITY DASHBOARD KONFIGURIEREN";
        if (message.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 &&
            message.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
            return "UNITY-CLOUD-PROJEKT NOCH NICHT VERKNÜPFT";
        return message.ToUpperInvariant();
    }
}
