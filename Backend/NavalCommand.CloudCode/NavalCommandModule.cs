using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.Economy.Model;
using Unity.Services.Leaderboards.Model;

namespace NavalCommandOnline;

public sealed class NavalCommandModule
{
    private const string StateKey = "state_v1";
    private readonly NavalCloudSaveStore _store;
    private readonly IGameApiClient _gameApi;
    private readonly ILogger<NavalCommandModule> _logger;

    public NavalCommandModule(NavalCloudSaveStore store, IGameApiClient gameApi, ILogger<NavalCommandModule> logger)
    {
        _store = store;
        _gameApi = gameApi;
        _logger = logger;
    }

    [CloudCodeFunction("GetOrCreateProfile")]
    public async Task<NavalPlayerProfile> GetOrCreateProfile(IExecutionContext context, long joinedUnixMs = 0)
    {
        StoredProfile stored = await GetOrCreateStoredProfile(context, RequirePlayer(context), joinedUnixMs);
        return PublicProfile(stored);
    }

    [CloudCodeFunction("UpdateProfile")]
    public async Task<NavalPlayerProfile> UpdateProfile(IExecutionContext context, string displayName)
    {
        string playerId = RequirePlayer(context);
        string normalized = ValidateDisplayName(displayName);
        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(context, ProfileEntity(playerId), StateKey);
        StoredProfile stored = record.Value ?? CreateStoredProfile(playerId);
        stored.profile.displayName = normalized;
        await _store.PutAsync(context, ProfileEntity(playerId), StateKey, stored, record.WriteLock);
        return PublicProfile(stored);
    }

    [CloudCodeFunction("UpdateAvatar")]
    public async Task<NavalPlayerProfile> UpdateAvatar(IExecutionContext context, string avatarImageBase64)
    {
        string playerId = RequirePlayer(context);
        string validated = ValidateAvatarImage(avatarImageBase64);
        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(context, ProfileEntity(playerId), StateKey);
        StoredProfile stored = record.Value ?? CreateStoredProfile(playerId);
        stored.profile.avatarImageBase64 = validated;
        await _store.PutAsync(context, ProfileEntity(playerId), StateKey, stored, record.WriteLock);
        return PublicProfile(stored);
    }

    [CloudCodeFunction("GetEntitlements")]
    public async Task<NavalEntitlements> GetEntitlements(IExecutionContext context)
    {
        StoredProfile stored = await GetOrCreateStoredProfile(context, RequirePlayer(context));
        return stored.entitlements;
    }

    [CloudCodeFunction("ClaimImaniRewardedAd")]
    public async Task<NavalEntitlements> ClaimImaniRewardedAd(IExecutionContext context)
    {
        string playerId = RequirePlayer(context);
        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(
            context, ProfileEntity(playerId), StateKey);
        StoredProfile stored = record.Value ?? CreateStoredProfile(playerId);
        if (!stored.entitlements.commanderIds.Contains("imani-cross"))
        {
            stored.entitlements.commanderIds.Add("imani-cross");
            await _store.PutAsync(context, ProfileEntity(playerId), StateKey, stored, record.WriteLock);
        }
        return stored.entitlements;
    }

    [CloudCodeFunction("RedeemRewardCode")]
    public async Task<NavalEntitlements> RedeemRewardCode(IExecutionContext context, string code)
    {
        string playerId = RequirePlayer(context);
        string normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized.Length > 64 ||
            normalized.Any(character => !char.IsLetterOrDigit(character) && character != '_' && character != '-'))
            throw new InvalidOperationException("INVALID_REWARD_CODE");
        if (normalized != NavalRewardCodes.AllCommandersCode)
            throw new InvalidOperationException("UNKNOWN_REWARD_CODE");

        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(
            context, ProfileEntity(playerId), StateKey);
        StoredProfile stored = record.Value ?? CreateStoredProfile(playerId);
        stored.redeemedRewardCodes ??= new HashSet<string>();
        if (stored.redeemedRewardCodes.Contains(normalized)) return stored.entitlements;

        foreach (string commanderId in NavalRewardCodes.AllCommanderIds)
        {
            if (!stored.entitlements.commanderIds.Contains(commanderId))
                stored.entitlements.commanderIds.Add(commanderId);
        }
        stored.redeemedRewardCodes.Add(normalized);
        await _store.PutAsync(context, ProfileEntity(playerId), StateKey, stored, record.WriteLock);
        return stored.entitlements;
    }

    [CloudCodeFunction("ResolveFriendCode")]
    public async Task<string> ResolveFriendCode(IExecutionContext context, string friendCode)
    {
        RequirePlayer(context);
        string normalized = (friendCode ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length != 10 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("INVALID_FRIEND_CODE");
        NavalStoredValue<string> record = await _store.GetAsync<string>(context, FriendCodeEntity(normalized), StateKey);
        return record.Value ?? throw new InvalidOperationException("FRIEND_CODE_NOT_FOUND");
    }

    [CloudCodeFunction("QueueRanked")]
    public async Task<NavalMatchTicket> QueueRanked(IExecutionContext context, IPushClient pushClient, NavalPendingLoadout loadout)
    {
        string playerId = RequirePlayer(context);
        NavalAuthoritativeEngine.ValidateLoadout(loadout);
        StoredProfile profile = await GetOrCreateStoredProfile(context, playerId);
        EnsureCommanderOwned(profile.entitlements, loadout.commanderId);
        if (!string.IsNullOrWhiteSpace(profile.activeMatchId))
            return Ticket(Guid.NewGuid().ToString("N"), profile.activeMatchId, "matched", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const string queueEntity = "naval-ranked-queue-global";
        for (int attempt = 0; attempt < 8; attempt++)
        {
            NavalStoredValue<RankedQueuePool> record = await _store.GetAsync<RankedQueuePool>(context, queueEntity, StateKey);
            RankedQueuePool pool = record.Value ?? new RankedQueuePool();
            pool.entries.RemoveAll(item => now - item.createdUnixMs > 300_000 || item.playerId == playerId);
            RankedQueueEntry? waiting = pool.entries
                .Where(item => Math.Abs(item.mmr - profile.profile.mmr) <= AllowedMmrDifference(now - item.createdUnixMs))
                .OrderBy(item => Math.Abs(item.mmr - profile.profile.mmr))
                .ThenBy(item => item.createdUnixMs)
                .FirstOrDefault();
            string ticketId = Guid.NewGuid().ToString("N");
            if (waiting == null)
            {
                if (pool.entries.Count >= 1000)
                    throw new InvalidOperationException("MATCHMAKING_CAPACITY_RETRY");
                pool.entries.Add(new RankedQueueEntry
                {
                    ticketId = ticketId,
                    playerId = playerId,
                    mmr = profile.profile.mmr,
                    createdUnixMs = now,
                    loadout = loadout
                });
                try
                {
                    await _store.PutAsync(context, queueEntity, StateKey, pool, record.WriteLock);
                    return Ticket(ticketId, null, "searching", now);
                }
                catch when (attempt < 7) { continue; }
            }

            pool.entries.RemoveAll(item => item.ticketId == waiting.ticketId);
            try { await _store.PutAsync(context, queueEntity, StateKey, pool, record.WriteLock); }
            catch when (attempt < 7) { continue; }

            StoredProfile opponentProfile = await GetOrCreateStoredProfile(context, waiting.playerId);
            if (!string.IsNullOrWhiteSpace(opponentProfile.activeMatchId))
                continue;
            EnsureCommanderOwned(opponentProfile.entitlements, waiting.loadout.commanderId);
            string matchId = Guid.NewGuid().ToString("N");
            NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
                matchId, NavalMatchMode.Ranked,
                waiting.playerId, opponentProfile.profile.displayName, waiting.loadout,
                playerId, profile.profile.displayName, loadout, now);
            SetRankedSnapshot(match, opponentProfile.profile, profile.profile);
            await _store.PutAsync(context, MatchEntity(matchId), StateKey, match, null);
            await SetActiveMatch(context, waiting.playerId, matchId);
            await SetActiveMatch(context, playerId, matchId);
            await TryPushMatch(context, pushClient, waiting.playerId, matchId, match.version);
            await TryPushMatch(context, pushClient, playerId, matchId, match.version);
            return Ticket(ticketId, matchId, "matched", now);
        }
        throw new InvalidOperationException("MATCHMAKING_BUSY_RETRY");
    }

    [CloudCodeFunction("CancelQueue")]
    public async Task<NavalMatchTicket> CancelQueue(IExecutionContext context, string ticketId)
    {
        string playerId = RequirePlayer(context);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        StoredProfile profile = await GetOrCreateStoredProfile(context, playerId);
        if (!string.IsNullOrWhiteSpace(profile.activeMatchId))
            return Ticket(ticketId, profile.activeMatchId, "matched", now);

        const string queueEntity = "naval-ranked-queue-global";
        NavalStoredValue<RankedQueuePool> record = await _store.GetAsync<RankedQueuePool>(context, queueEntity, StateKey);
        RankedQueuePool pool = record.Value ?? new RankedQueuePool();
        int removed = pool.entries.RemoveAll(item => item.playerId == playerId && item.ticketId == ticketId);
        if (removed > 0) await _store.PutAsync(context, queueEntity, StateKey, pool, record.WriteLock);
        return Ticket(ticketId, null, "cancelled", now);
    }

    [CloudCodeFunction("PollRanked")]
    public async Task<NavalMatchTicket> PollRanked(IExecutionContext context, string ticketId)
    {
        string playerId = RequirePlayer(context);
        if (string.IsNullOrWhiteSpace(ticketId) || ticketId.Length > 64)
            throw new InvalidOperationException("INVALID_TICKET");

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        StoredProfile profile = await GetOrCreateStoredProfile(context, playerId);
        if (!string.IsNullOrWhiteSpace(profile.activeMatchId))
            return Ticket(ticketId, profile.activeMatchId, "matched", now);

        const string queueEntity = "naval-ranked-queue-global";
        NavalStoredValue<RankedQueuePool> record = await _store.GetAsync<RankedQueuePool>(context, queueEntity, StateKey);
        RankedQueueEntry? entry = record.Value?.entries.FirstOrDefault(item =>
            item.playerId == playerId && item.ticketId == ticketId);
        if (entry == null || now - entry.createdUnixMs > 300_000)
            return Ticket(ticketId, null, "expired", now);
        return Ticket(ticketId, null, "searching", entry.createdUnixMs);
    }

    [CloudCodeFunction("CreateFriendlyMatch")]
    public async Task<NavalMatchTicket> CreateFriendlyMatch(
        IExecutionContext context, IPushClient pushClient, string friendPlayerId, NavalPendingLoadout loadout)
    {
        string playerId = RequirePlayer(context);
        if (string.IsNullOrWhiteSpace(friendPlayerId) || friendPlayerId == playerId)
            throw new InvalidOperationException("INVALID_FRIEND");
        await EnsureFriend(context, friendPlayerId);
        NavalAuthoritativeEngine.ValidateLoadout(loadout);
        StoredProfile sender = await GetOrCreateStoredProfile(context, playerId);
        EnsureCommanderOwned(sender.entitlements, loadout.commanderId);
        if (!string.IsNullOrWhiteSpace(sender.activeMatchId)) throw new InvalidOperationException("ACTIVE_MATCH_EXISTS");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        NavalStoredValue<FriendlyInbox> record = await _store.GetAsync<FriendlyInbox>(context, InviteEntity(friendPlayerId), StateKey);
        FriendlyInbox inbox = record.Value ?? new FriendlyInbox();
        inbox.invites.RemoveAll(item => item.expiresUnixMs <= now || item.senderPlayerId == playerId);
        string inviteId = Guid.NewGuid().ToString("N");
        inbox.invites.Add(new StoredFriendlyInvite
        {
            inviteId = inviteId,
            senderPlayerId = playerId,
            senderDisplayName = sender.profile.displayName,
            expiresUnixMs = now + NavalOnlineProtocol.FriendInviteMinutes * 60_000L,
            loadout = loadout
        });
        await _store.PutAsync(context, InviteEntity(friendPlayerId), StateKey, inbox, record.WriteLock);
        await TryPush(context, pushClient, friendPlayerId,
            JsonConvert.SerializeObject(new { inviteId, senderPlayerId = playerId }), "naval.friend.invite");
        return Ticket(inviteId, null, "invited", now);
    }

    [CloudCodeFunction("GetFriendlyInvites")]
    public async Task<List<NavalFriendlyInvite>> GetFriendlyInvites(IExecutionContext context)
    {
        string playerId = RequirePlayer(context);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        NavalStoredValue<FriendlyInbox> record = await _store.GetAsync<FriendlyInbox>(context, InviteEntity(playerId), StateKey);
        FriendlyInbox inbox = record.Value ?? new FriendlyInbox();
        return inbox.invites.Where(item => item.expiresUnixMs > now).Select(item => new NavalFriendlyInvite
        {
            inviteId = item.inviteId,
            senderPlayerId = item.senderPlayerId,
            senderDisplayName = item.senderDisplayName,
            expiresUnixMs = item.expiresUnixMs
        }).ToList();
    }

    [CloudCodeFunction("AcceptFriendlyMatch")]
    public async Task<NavalMatchTicket> AcceptFriendlyMatch(
        IExecutionContext context, IPushClient pushClient, string inviteId, NavalPendingLoadout loadout)
    {
        string playerId = RequirePlayer(context);
        NavalAuthoritativeEngine.ValidateLoadout(loadout);
        StoredProfile recipient = await GetOrCreateStoredProfile(context, playerId);
        EnsureCommanderOwned(recipient.entitlements, loadout.commanderId);
        if (!string.IsNullOrWhiteSpace(recipient.activeMatchId)) throw new InvalidOperationException("ACTIVE_MATCH_EXISTS");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        NavalStoredValue<FriendlyInbox> record = await _store.GetAsync<FriendlyInbox>(context, InviteEntity(playerId), StateKey);
        FriendlyInbox inbox = record.Value ?? throw new InvalidOperationException("INVITE_NOT_FOUND");
        StoredFriendlyInvite invite = inbox.invites.FirstOrDefault(item => item.inviteId == inviteId)
            ?? throw new InvalidOperationException("INVITE_NOT_FOUND");
        if (invite.expiresUnixMs <= now) throw new InvalidOperationException("INVITE_EXPIRED");
        await EnsureFriend(context, invite.senderPlayerId);
        StoredProfile sender = await GetOrCreateStoredProfile(context, invite.senderPlayerId);
        if (!string.IsNullOrWhiteSpace(sender.activeMatchId)) throw new InvalidOperationException("FRIEND_ALREADY_IN_MATCH");
        string matchId = Guid.NewGuid().ToString("N");
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            matchId, NavalMatchMode.Friendly,
            invite.senderPlayerId, sender.profile.displayName, invite.loadout,
            playerId, recipient.profile.displayName, loadout, now);
        await _store.PutAsync(context, MatchEntity(matchId), StateKey, match, null);
        await SetActiveMatch(context, invite.senderPlayerId, matchId);
        await SetActiveMatch(context, playerId, matchId);
        inbox.invites.RemoveAll(item => item.inviteId == inviteId);
        await _store.PutAsync(context, InviteEntity(playerId), StateKey, inbox, record.WriteLock);
        await TryPushMatch(context, pushClient, invite.senderPlayerId, matchId, match.version);
        await TryPushMatch(context, pushClient, playerId, matchId, match.version);
        return Ticket(inviteId, matchId, "matched", now);
    }

    [CloudCodeFunction("GetMatchView")]
    public async Task<NavalPlayerMatchView> GetMatchView(IExecutionContext context, string matchId)
    {
        string playerId = RequirePlayer(context);
        NavalStoredValue<NavalServerMatch> record = await _store.GetAsync<NavalServerMatch>(context, MatchEntity(matchId), StateKey);
        NavalServerMatch match = record.Value ?? throw new InvalidOperationException("MATCH_NOT_FOUND");
        await EnsureRewardsFinalized(context, match, record.WriteLock);
        return NavalAuthoritativeEngine.BuildView(match, playerId);
    }

    [CloudCodeFunction("GetMatchIntro")]
    public async Task<NavalMatchIntro> GetMatchIntro(IExecutionContext context, string matchId)
    {
        string playerId = RequirePlayer(context);
        if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 64)
            throw new InvalidOperationException("INVALID_MATCH_ID");
        NavalStoredValue<NavalServerMatch> record = await _store.GetAsync<NavalServerMatch>(
            context, MatchEntity(matchId), StateKey);
        NavalServerMatch match = record.Value ?? throw new InvalidOperationException("MATCH_NOT_FOUND");

        NavalServerPlayer own;
        NavalServerPlayer opponent;
        if (match.first.playerId == playerId)
        {
            own = match.first;
            opponent = match.second;
        }
        else if (match.second.playerId == playerId)
        {
            own = match.second;
            opponent = match.first;
        }
        else
        {
            throw new InvalidOperationException("NOT_A_PARTICIPANT");
        }

        StoredProfile ownProfile = await GetOrCreateStoredProfile(context, own.playerId);
        StoredProfile opponentProfile = await GetOrCreateStoredProfile(context, opponent.playerId);
        return new NavalMatchIntro
        {
            matchId = match.matchId,
            ownPlayerId = own.playerId,
            ownDisplayName = own.displayName,
            ownAvatarImageBase64 = ownProfile.profile.avatarImageBase64,
            opponentPlayerId = opponent.playerId,
            opponentDisplayName = opponent.displayName,
            opponentAvatarImageBase64 = opponentProfile.profile.avatarImageBase64
        };
    }

    [CloudCodeFunction("ReconnectMatch")]
    public async Task<NavalPlayerMatchView?> ReconnectMatch(IExecutionContext context)
    {
        string playerId = RequirePlayer(context);
        StoredProfile profile = await GetOrCreateStoredProfile(context, playerId);
        if (string.IsNullOrWhiteSpace(profile.activeMatchId)) return null;
        NavalStoredValue<NavalServerMatch> record = await _store.GetAsync<NavalServerMatch>(
            context, MatchEntity(profile.activeMatchId), StateKey);
        if (record.Value == null)
        {
            await SetActiveMatch(context, playerId, null);
            return null;
        }
        NavalPlayerMatchView view = NavalAuthoritativeEngine.BuildView(record.Value, playerId);
        await EnsureRewardsFinalized(context, record.Value, record.WriteLock);
        view = NavalAuthoritativeEngine.BuildView(record.Value, playerId);
        if (record.Value.status == NavalMatchStatus.Finished)
            await SetActiveMatch(context, playerId, null);
        return view;
    }

    [CloudCodeFunction("SubmitAction")]
    public async Task<NavalPlayerMatchView> SubmitAction(IExecutionContext context, IPushClient pushClient, NavalMatchAction action)
    {
        string playerId = RequirePlayer(context);
        if (action == null || string.IsNullOrWhiteSpace(action.matchId)) throw new InvalidOperationException("INVALID_ACTION");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            NavalStoredValue<NavalServerMatch> record = await _store.GetAsync<NavalServerMatch>(context, MatchEntity(action.matchId), StateKey);
            NavalServerMatch match = record.Value ?? throw new InvalidOperationException("MATCH_NOT_FOUND");
            NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(
                match, playerId, action, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            PrepareRankedRewards(match);
            view = NavalAuthoritativeEngine.BuildView(match, playerId);
            try
            {
                string? matchWriteLock = await _store.PutAsync(
                    context, MatchEntity(match.matchId), StateKey, match, record.WriteLock);
                if (match.status == NavalMatchStatus.Finished && !match.rewardsFinalized)
                {
                    await FinalizeMatch(context, match);
                    match.rewardsFinalized = true;
                    await _store.PutAsync(context, MatchEntity(match.matchId), StateKey, match, matchWriteLock);
                }
                await TryPushMatch(context, pushClient, match.first.playerId, match.matchId, match.version);
                await TryPushMatch(context, pushClient, match.second.playerId, match.matchId, match.version);
                return view;
            }
            catch (Exception exception) when (attempt < 2)
            {
                _logger.LogWarning(exception, "Optimistic match write failed; retry {Attempt}", attempt + 1);
            }
        }
        throw new InvalidOperationException("MATCH_BUSY_RETRY");
    }

    [CloudCodeFunction("GetLeaderboard")]
    public async Task<List<NavalLeaderboardEntry>> GetLeaderboard(IExecutionContext context, int limit = 50)
    {
        RequirePlayer(context);
        int safeLimit = Math.Max(1, Math.Min(100, limit));
        string leaderboardId = NavalSeasonRules.GetLeaderboardId(
            NavalSeasonRules.GetSeasonId(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        var response = await _gameApi.Leaderboards.GetLeaderboardScoresAsync(
            context, context.AccessToken, Guid.Parse(context.ProjectId), leaderboardId, null, 0, safeLimit);
        return response.Data.Results.Select(entry => new NavalLeaderboardEntry
        {
            rank = entry.Rank + 1,
            playerId = entry.PlayerId,
            displayName = string.IsNullOrWhiteSpace(entry.PlayerName) ? "COMMANDER" : entry.PlayerName,
            mmr = (int)Math.Round(entry.Score),
            league = NavalRankRules.GetLeague((int)Math.Round(entry.Score), true)
        }).ToList();
    }

    [CloudCodeFunction("ValidatePurchase")]
    public async Task<NavalPurchaseResult> ValidatePurchase(
        IExecutionContext context, NavalPurchaseRequest request, IGameApiClient gameApiClient)
    {
        string playerId = RequirePlayer(context);
        if (request == null || string.IsNullOrWhiteSpace(request.productId) || string.IsNullOrWhiteSpace(request.receipt))
            throw new InvalidOperationException("INVALID_PURCHASE");
        if (request.receipt.Length > 200_000 || (request.signature?.Length ?? 0) > 20_000 ||
            request.localCostMinorUnits < 0 || string.IsNullOrWhiteSpace(request.localCurrency) ||
            request.localCurrency.Length != 3 || request.localCurrency.Any(character => !char.IsLetter(character)))
            throw new InvalidOperationException("INVALID_PURCHASE_METADATA");
        if (request.platform == NavalStorePlatform.Google && string.IsNullOrWhiteSpace(request.signature))
            throw new InvalidOperationException("INVALID_GOOGLE_SIGNATURE");
        string commanderId = ProductCommander(request.productId);
        string economyPurchaseId = EconomyPurchaseId(request.productId);
        string purchaseFingerprint = PurchaseFingerprint(request);
        NavalStoredValue<StoredProfile> existingRecord = await _store.GetAsync<StoredProfile>(context, ProfileEntity(playerId), StateKey);
        StoredProfile existing = existingRecord.Value ?? CreateStoredProfile(playerId);
        if (existing.processedPurchases.Contains(purchaseFingerprint))
            return new NavalPurchaseResult { verified = true, productId = request.productId, entitlements = existing.entitlements };

        if (request.platform == NavalStorePlatform.Apple)
        {
            var response = await gameApiClient.EconomyPurchases.RedeemAppleAppStorePurchaseAsync(
                context, context.AccessToken, context.ProjectId, playerId,
                new PlayerPurchaseAppleappstoreRequest(economyPurchaseId, request.receipt,
                    request.localCostMinorUnits, request.localCurrency));
            var status = response.Data.Verification.Status;
            if (status != PlayerPurchaseAppleappstoreResponseVerification.StatusEnum.VALID &&
                status != PlayerPurchaseAppleappstoreResponseVerification.StatusEnum.VALIDNOTREDEEMED)
                throw new InvalidOperationException("PURCHASE_VERIFICATION_FAILED_" + status);
        }
        else
        {
            var response = await gameApiClient.EconomyPurchases.RedeemGooglePlayPurchaseAsync(
                context, context.AccessToken, context.ProjectId, playerId,
                new PlayerPurchaseGoogleplaystoreRequest(economyPurchaseId, request.receipt, request.signature,
                    request.localCostMinorUnits, request.localCurrency));
            var status = response.Data.Verification.Status;
            if (status != PlayerPurchaseGoogleplaystoreResponseVerification.StatusEnum.VALID &&
                status != PlayerPurchaseGoogleplaystoreResponseVerification.StatusEnum.VALIDNOTREDEEMED)
                throw new InvalidOperationException("PURCHASE_VERIFICATION_FAILED_" + status);
        }

        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(context, ProfileEntity(playerId), StateKey);
        StoredProfile stored = record.Value ?? CreateStoredProfile(playerId);
        if (!stored.entitlements.commanderIds.Contains(commanderId)) stored.entitlements.commanderIds.Add(commanderId);
        stored.processedPurchases.Add(purchaseFingerprint);
        await _store.PutAsync(context, ProfileEntity(playerId), StateKey, stored, record.WriteLock);
        return new NavalPurchaseResult { verified = true, productId = request.productId, entitlements = stored.entitlements };
    }

    [CloudCodeFunction("PrepareAccountDeletion")]
    public async Task<string> PrepareAccountDeletion(IExecutionContext context, IPushClient pushClient)
    {
        string playerId = RequirePlayer(context);
        NavalStoredValue<StoredProfile> profileRecord = await _store.GetAsync<StoredProfile>(
            context, ProfileEntity(playerId), StateKey);
        StoredProfile? stored = profileRecord.Value;

        if (!string.IsNullOrWhiteSpace(stored?.activeMatchId))
        {
            NavalStoredValue<NavalServerMatch> matchRecord = await _store.GetAsync<NavalServerMatch>(
                context, MatchEntity(stored.activeMatchId), StateKey);
            NavalServerMatch? match = matchRecord.Value;
            if (match != null && match.status == NavalMatchStatus.InProgress)
            {
                NavalMatchAction surrender = NavalMatchAction.Create(match.matchId, match.version, NavalActionType.Surrender);
                await SubmitAction(context, pushClient, surrender);
                matchRecord = await _store.GetAsync<NavalServerMatch>(context, MatchEntity(match.matchId), StateKey);
                match = matchRecord.Value;
            }

            if (match != null)
            {
                await EnsureRewardsFinalized(context, match, matchRecord.WriteLock);
                matchRecord = await _store.GetAsync<NavalServerMatch>(context, MatchEntity(match.matchId), StateKey);
                match = matchRecord.Value;
                if (match != null)
                {
                    PlayerForDeletion(match, playerId).displayName = "GELÖSCHTER COMMANDER";
                    await _store.PutAsync(context, MatchEntity(match.matchId), StateKey, match, matchRecord.WriteLock);
                }
            }
        }

        await RemovePlayerFromRankedQueue(context, playerId);
        if (!string.IsNullOrWhiteSpace(stored?.profile?.friendCode))
        {
            NavalStoredValue<string> index = await _store.GetAsync<string>(
                context, FriendCodeEntity(stored.profile.friendCode), StateKey);
            if (index.Value == playerId)
                await _store.DeleteEntityAsync(context, FriendCodeEntity(stored.profile.friendCode));
        }
        await _store.DeleteEntityAsync(context, InviteEntity(playerId));
        await _store.DeleteEntityAsync(context, ProfileEntity(playerId));
        return "prepared";
    }

    private async Task FinalizeMatch(IExecutionContext context, NavalServerMatch match)
    {
        if (string.IsNullOrWhiteSpace(match.winnerPlayerId)) return;
        if (match.mode != NavalMatchMode.Ranked)
        {
            await ApplyFriendlyResult(context, match, match.first, match.first.playerId == match.winnerPlayerId);
            await ApplyFriendlyResult(context, match, match.second, match.second.playerId == match.winnerPlayerId);
            return;
        }
        await ApplyRankedResult(context, match, match.first, match.firstRatingDelta,
            match.first.playerId == match.winnerPlayerId);
        await ApplyRankedResult(context, match, match.second, match.secondRatingDelta,
            match.second.playerId == match.winnerPlayerId);

        string leaderboardId = NavalSeasonRules.GetLeaderboardId(match.rankedSeasonId);
        await _gameApi.Leaderboards.AddLeaderboardPlayerScoreAsync(context, context.ServiceToken,
            Guid.Parse(context.ProjectId), leaderboardId, match.first.playerId,
            new AddLeaderboardScore(match.firstMmrBefore + match.firstRatingDelta));
        await _gameApi.Leaderboards.AddLeaderboardPlayerScoreAsync(context, context.ServiceToken,
            Guid.Parse(context.ProjectId), leaderboardId, match.second.playerId,
            new AddLeaderboardScore(match.secondMmrBefore + match.secondRatingDelta));
    }

    private async Task<StoredProfile> ApplyRankedResult(
        IExecutionContext context, NavalServerMatch match, NavalServerPlayer player, int ratingDelta, bool won)
    {
        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(context, ProfileEntity(player.playerId), StateKey);
        StoredProfile stored = record.Value ?? CreateStoredProfile(player.playerId);
        if (stored.finalizedMatches.Contains(match.matchId)) return stored;
        bool belongsToCurrentSeason = stored.profile.seasonId == match.rankedSeasonId;
        if (belongsToCurrentSeason)
            stored.profile.mmr = Math.Max(0, stored.profile.mmr + ratingDelta);
        if (won)
        {
            if (belongsToCurrentSeason) stored.profile.rankedWins++;
            stored.profile.lifetimeWins++;
        }
        else
        {
            if (belongsToCurrentSeason) stored.profile.rankedLosses++;
            stored.profile.lifetimeLosses++;
        }
        if (belongsToCurrentSeason) AdvancePlacement(stored.profile);
        stored.finalizedMatches.Add(match.matchId);
        TrimSet(stored.finalizedMatches, 256);
        stored.activeMatchId = null;
        await _store.PutAsync(context, ProfileEntity(player.playerId), StateKey, stored, record.WriteLock);
        return stored;
    }

    private async Task ApplyFriendlyResult(
        IExecutionContext context, NavalServerMatch match, NavalServerPlayer player, bool won)
    {
        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(
            context, ProfileEntity(player.playerId), StateKey);
        StoredProfile stored = record.Value ?? CreateStoredProfile(player.playerId);
        if (!stored.finalizedMatches.Contains(match.matchId))
        {
            if (won) stored.profile.lifetimeWins++;
            else stored.profile.lifetimeLosses++;
            stored.finalizedMatches.Add(match.matchId);
            TrimSet(stored.finalizedMatches, 256);
        }
        stored.activeMatchId = null;
        await _store.PutAsync(context, ProfileEntity(player.playerId), StateKey, stored, record.WriteLock);
    }

    private async Task EnsureRewardsFinalized(IExecutionContext context, NavalServerMatch match, string? writeLock)
    {
        if (match.status != NavalMatchStatus.Finished || match.rewardsFinalized) return;
        PrepareRankedRewards(match);
        await FinalizeMatch(context, match);
        match.rewardsFinalized = true;
        await _store.PutAsync(context, MatchEntity(match.matchId), StateKey, match, writeLock);
    }

    private static void SetRankedSnapshot(NavalServerMatch match, NavalPlayerProfile first, NavalPlayerProfile second)
    {
        match.firstMmrBefore = first.mmr;
        match.secondMmrBefore = second.mmr;
        match.firstPlacementAtStart = !first.placementComplete;
        match.secondPlacementAtStart = !second.placementComplete;
        match.rankedSeasonId = first.seasonId;
    }

    private static void PrepareRankedRewards(NavalServerMatch match)
    {
        if (match.status != NavalMatchStatus.Finished || match.mode != NavalMatchMode.Ranked ||
            match.firstRatingDelta != 0 || match.secondRatingDelta != 0) return;
        bool firstWon = match.first.playerId == match.winnerPlayerId;
        int firstAfter = NavalRankRules.CalculateNewMmr(match.firstMmrBefore, match.secondMmrBefore,
            firstWon, match.firstPlacementAtStart);
        int secondAfter = NavalRankRules.CalculateNewMmr(match.secondMmrBefore, match.firstMmrBefore,
            !firstWon, match.secondPlacementAtStart);
        match.firstRatingDelta = firstAfter - match.firstMmrBefore;
        match.secondRatingDelta = secondAfter - match.secondMmrBefore;
    }

    private async Task<StoredProfile> GetOrCreateStoredProfile(
        IExecutionContext context, string playerId, long joinedUnixMs = 0)
    {
        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(context, ProfileEntity(playerId), StateKey);
        if (record.Value != null)
        {
            bool changed = EnsureCurrentSeason(record.Value.profile, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (record.Value.profile.joinedUnixMs <= 0)
            {
                record.Value.profile.joinedUnixMs = NormalizeJoinedDate(joinedUnixMs);
                changed = true;
            }
            if (changed)
                await _store.PutAsync(context, ProfileEntity(playerId), StateKey, record.Value, record.WriteLock);
            await EnsureFriendCodeIndex(context, record.Value.profile.friendCode, playerId);
            return record.Value;
        }
        StoredProfile stored = CreateStoredProfile(playerId, joinedUnixMs);
        await _store.PutAsync(context, ProfileEntity(playerId), StateKey, stored, record.WriteLock);
        await _store.PutAsync(context, FriendCodeEntity(stored.profile.friendCode), StateKey, playerId, null);
        return stored;
    }

    private async Task EnsureFriendCodeIndex(IExecutionContext context, string code, string playerId)
    {
        NavalStoredValue<string> index = await _store.GetAsync<string>(context, FriendCodeEntity(code), StateKey);
        if (string.IsNullOrWhiteSpace(index.Value))
            await _store.PutAsync(context, FriendCodeEntity(code), StateKey, playerId, index.WriteLock);
    }

    private async Task SetActiveMatch(IExecutionContext context, string playerId, string? matchId)
    {
        NavalStoredValue<StoredProfile> record = await _store.GetAsync<StoredProfile>(context, ProfileEntity(playerId), StateKey);
        StoredProfile stored = record.Value ?? CreateStoredProfile(playerId);
        stored.activeMatchId = matchId;
        await _store.PutAsync(context, ProfileEntity(playerId), StateKey, stored, record.WriteLock);
    }

    private async Task EnsureFriend(IExecutionContext context, string otherPlayerId)
    {
        var response = await _gameApi.FriendsRelationshipsApi.GetRelationshipsAsync(
            context, context.AccessToken, 100, 0, false, false,
            new List<Unity.Services.Friends.Model.RelationshipType>
            {
                Unity.Services.Friends.Model.RelationshipType.FRIEND
            });
        bool acceptedFriend = response.Data.Any(relationship =>
            relationship.Type == Unity.Services.Friends.Model.RelationshipType.FRIEND &&
            relationship.Members.Any(member => member.Id == otherPlayerId));
        if (!acceptedFriend) throw new InvalidOperationException("FRIENDSHIP_REQUIRED");
    }

    private static StoredProfile CreateStoredProfile(string playerId, long joinedUnixMs = 0)
    {
        NavalPlayerProfile profile = new NavalPlayerProfile
        {
            playerId = playerId,
            displayName = "COMMANDER",
            friendCode = FriendCode(playerId),
            mmr = NavalRankRules.InitialMmr,
            joinedUnixMs = NormalizeJoinedDate(joinedUnixMs),
            seasonId = NavalSeasonRules.GetSeasonId(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        };
        profile.RefreshDerivedFields();
        return new StoredProfile
        {
            profile = profile,
            entitlements = new NavalEntitlements
            {
                commanderIds = new List<string> { "elias-voss", "dae-hyun-kwon" }
            }
        };
    }

    private static NavalPlayerProfile PublicProfile(StoredProfile stored)
    {
        stored.profile.RefreshDerivedFields();
        return stored.profile;
    }

    private async Task TryPushMatch(
        IExecutionContext context, IPushClient pushClient, string playerId, string matchId, int version)
        => await TryPush(context, pushClient, playerId,
            JsonConvert.SerializeObject(new { matchId, version }), "naval.match.changed");

    private async Task TryPush(
        IExecutionContext context, IPushClient pushClient, string playerId, string message, string messageType)
    {
        try
        {
            await pushClient.SendPlayerMessageAsync(context, message, messageType, playerId);
        }
        catch (Exception exception)
        {
            // The committed state remains authoritative. Clients recover by polling/reconnect.
            _logger.LogWarning(exception, "Push {MessageType} to {PlayerId} failed", messageType, playerId);
        }
    }

    private async Task RemovePlayerFromRankedQueue(IExecutionContext context, string playerId)
    {
        const string queueEntity = "naval-ranked-queue-global";
        for (int attempt = 0; attempt < 3; attempt++)
        {
            NavalStoredValue<RankedQueuePool> record = await _store.GetAsync<RankedQueuePool>(
                context, queueEntity, StateKey);
            RankedQueuePool pool = record.Value ?? new RankedQueuePool();
            if (pool.entries.RemoveAll(item => item.playerId == playerId) == 0) return;
            try
            {
                await _store.PutAsync(context, queueEntity, StateKey, pool, record.WriteLock);
                return;
            }
            catch (Exception exception) when (attempt < 2)
            {
                _logger.LogWarning(exception, "Queue cleanup conflict; retry {Attempt}", attempt + 1);
            }
        }
        throw new InvalidOperationException("QUEUE_CLEANUP_BUSY_RETRY");
    }

    private static NavalServerPlayer PlayerForDeletion(NavalServerMatch match, string playerId)
    {
        if (match.first.playerId == playerId) return match.first;
        if (match.second.playerId == playerId) return match.second;
        throw new InvalidOperationException("NOT_A_PARTICIPANT");
    }

    private static void TrimSet(HashSet<string> values, int maximum)
    {
        while (values.Count > maximum) values.Remove(values.First());
    }

    private static NavalMatchTicket Ticket(string? ticketId, string? matchId, string state, long now) => new NavalMatchTicket
    {
        ticketId = ticketId,
        matchId = matchId,
        sessionId = matchId,
        state = state,
        createdUnixMs = now
    };

    private static void AdvancePlacement(NavalPlayerProfile profile)
    {
        if (profile.placementComplete) return;
        profile.placementMatches++;
        profile.placementComplete = profile.placementMatches >= NavalRankRules.PlacementGames;
        profile.RefreshDerivedFields();
    }

    private static bool EnsureCurrentSeason(NavalPlayerProfile profile, long nowUnixMs)
    {
        string current = NavalSeasonRules.GetSeasonId(nowUnixMs);
        if (profile.seasonId == current) return false;
        if (!string.IsNullOrWhiteSpace(profile.seasonId)) profile.mmr = NavalRankRules.SoftReset(profile.mmr);
        profile.seasonId = current;
        profile.rankedWins = 0;
        profile.rankedLosses = 0;
        profile.placementMatches = 0;
        profile.placementComplete = false;
        profile.RefreshDerivedFields();
        return true;
    }

    private static void EnsureCommanderOwned(NavalEntitlements entitlements, string commanderId)
    {
        if (!entitlements.OwnsCommander(commanderId)) throw new InvalidOperationException("COMMANDER_NOT_OWNED");
    }

    private static int AllowedMmrDifference(long waitingMilliseconds)
    {
        int expansionSteps = (int)Math.Max(0, waitingMilliseconds / 15_000L);
        return Math.Min(500, 100 + expansionSteps * 50);
    }

    private static string ProductCommander(string productId) => productId switch
    {
        "commander.elias.voss" => "elias-voss",
        "commander.dae.hyun.kwon" => "dae-hyun-kwon",
        "commander.arjan.dhillon" => "arjan-dhillon",
        _ => throw new InvalidOperationException("UNKNOWN_PRODUCT")
    };

    private static string EconomyPurchaseId(string productId) => productId switch
    {
        "commander.elias.voss" => "COMMANDER_ELIAS_VOSS",
        "commander.dae.hyun.kwon" => "COMMANDER_DAE_HYUN_KWON",
        "commander.arjan.dhillon" => "COMMANDER_ARJAN_DHILLON",
        _ => throw new InvalidOperationException("UNKNOWN_PRODUCT")
    };

    private static string RequirePlayer(IExecutionContext context)
        => string.IsNullOrWhiteSpace(context.PlayerId) ? throw new InvalidOperationException("AUTH_REQUIRED") : context.PlayerId;

    private static string ValidateDisplayName(string value)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length < 3 || normalized.Length > 20) throw new InvalidOperationException("INVALID_DISPLAY_NAME");
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character != ' ' && character != '-' && character != '_'))
            throw new InvalidOperationException("INVALID_DISPLAY_NAME");
        return normalized;
    }

    private static string ValidateAvatarImage(string value)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0) return string.Empty;
        if (normalized.Length > 180000) throw new InvalidOperationException("AVATAR_TOO_LARGE");

        byte[] bytes;
        try { bytes = Convert.FromBase64String(normalized); }
        catch (FormatException) { throw new InvalidOperationException("INVALID_AVATAR_IMAGE"); }
        if (bytes.Length < 100 || bytes.Length > 135000)
            throw new InvalidOperationException("AVATAR_TOO_LARGE");

        bool jpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        bool png = bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E &&
            bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        if (!jpeg && !png) throw new InvalidOperationException("INVALID_AVATAR_IMAGE");
        return normalized;
    }

    private static long NormalizeJoinedDate(long candidate)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long earliest = new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        return candidate >= earliest && candidate <= now + 86_400_000L ? candidate : now;
    }

    private static string FriendCode(string playerId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(playerId));
        return Convert.ToHexString(hash, 0, 5);
    }

    private static string PurchaseFingerprint(NavalPurchaseRequest request)
    {
        string canonical = request.platform + "\n" + request.productId + "\n" + request.receipt + "\n" + request.signature;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ProfileEntity(string playerId) => "naval-profile-" + playerId;
    private static string MatchEntity(string matchId) => "naval-match-" + matchId;
    private static string InviteEntity(string playerId) => "naval-invites-" + playerId;
    private static string FriendCodeEntity(string code) => "naval-friend-code-" + code;

    public sealed class StoredProfile
    {
        public NavalPlayerProfile profile = new();
        public NavalEntitlements entitlements = new();
        public HashSet<string> finalizedMatches = new();
        public HashSet<string> processedPurchases = new();
        public HashSet<string> redeemedRewardCodes = new();
        public string? activeMatchId;
    }

    public sealed class RankedQueueEntry
    {
        public string ticketId = string.Empty;
        public string playerId = string.Empty;
        public int mmr;
        public long createdUnixMs;
        public NavalPendingLoadout loadout = new();
    }

    public sealed class RankedQueuePool
    {
        public List<RankedQueueEntry> entries = new();
    }

    public sealed class FriendlyInbox
    {
        public List<StoredFriendlyInvite> invites = new();
    }

    public sealed class StoredFriendlyInvite
    {
        public string inviteId = string.Empty;
        public string senderPlayerId = string.Empty;
        public string senderDisplayName = string.Empty;
        public long expiresUnixMs;
        public NavalPendingLoadout loadout = new();
    }
}
