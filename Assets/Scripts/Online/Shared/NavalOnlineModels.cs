using System;
using System.Collections.Generic;

public enum NavalOnlineStatus
{
    Offline,
    Initializing,
    Ready,
    SigningIn,
    SignedIn,
    Matchmaking,
    InMatch,
    Error
}

public enum NavalMatchMode
{
    Ranked,
    Friendly
}

public enum NavalMatchStatus
{
    WaitingForPlayers,
    InProgress,
    Finished,
    Cancelled
}

public enum NavalActionType
{
    NormalShot,
    Ability,
    Surrender,
    ClaimTimeout
}

[Serializable]
public sealed class NavalPlayerProfile
{
    public string playerId;
    public string displayName;
    public string friendCode;
    public string avatarId = "avatar_default";
    public string avatarImageBase64;
    public long joinedUnixMs;
    public string bannerId = "banner_default";
    public int mmr = NavalRankRules.InitialMmr;
    public int rankedWins;
    public int rankedLosses;
    public int lifetimeWins;
    public int lifetimeLosses;
    public string favoriteCommanderId = "standard-commander";
    public string seasonId;
    public string league;
    public bool placementComplete;
    public int placementMatches;

    public void RefreshDerivedFields()
    {
        league = NavalRankRules.GetLeague(mmr, placementComplete);
    }
}

[Serializable]
public sealed class NavalFriendProfile
{
    public string playerId;
    public string displayName;
    public string friendCode;
    public string avatarId;
    public string league;
    public bool online;
    public bool incomingRequest;
    public bool outgoingRequest;
    public bool blocked;
}

[Serializable]
public sealed class NavalShipPlacement
{
    public int length;
    public int width;
    public int height;
    public int row;
    public int column;
    public bool vertical;
}

[Serializable]
public sealed class NavalPendingLoadout
{
    public string commanderId;
    public List<NavalShipPlacement> ships = new List<NavalShipPlacement>();
    public string clientRulesVersion = NavalOnlineProtocol.RulesVersion;
}

[Serializable]
public sealed class NavalMatchAction
{
    public string matchId;
    public string actionId;
    public int expectedVersion;
    public NavalActionType type;
    public string abilityId;
    public int row = -1;
    public int column = -1;
    public int sourceRow = -1;
    public int sourceColumn = -1;
    public bool developerFreeAbilities;
    public List<int> targetRows = new List<int>();
    public List<int> targetColumns = new List<int>();

    public static NavalMatchAction Create(string matchId, int version, NavalActionType type)
    {
        return new NavalMatchAction
        {
            matchId = matchId,
            expectedVersion = version,
            type = type,
            actionId = Guid.NewGuid().ToString("N")
        };
    }
}

[Serializable]
public sealed class NavalCellView
{
    public int row;
    public int column;
    public bool shot;
    public bool hit;
    public bool blocked;
    public bool ship;
    public bool sunk;
    public bool scannedWater;
    public bool revealedContact;
    public bool mine;
    public bool jet;
}

[Serializable]
public sealed class NavalPlayerMatchView
{
    public string matchId;
    public NavalMatchMode mode;
    public NavalMatchStatus status;
    public int version;
    public string ownPlayerId;
    public string opponentPlayerId;
    public string opponentDisplayName;
    public string ownCommanderId;
    public string opponentCommanderId;
    public string currentTurnPlayerId;
    public long turnDeadlineUnixMs;
    public int ownAbilityPoints;
    public int opponentAbilityPoints;
    public bool ownJetLaunched;
    public bool ownJetActive;
    public bool opponentJetActive;
    public bool ownAbilitiesJammed;
    public int ownBonusShotsRemaining;
    public List<NavalShipPlacement> ownShips = new List<NavalShipPlacement>();
    public List<NavalCellView> ownBoard = new List<NavalCellView>();
    public List<NavalCellView> opponentBoard = new List<NavalCellView>();
    public string lastEvent;
    public string winnerPlayerId;
    public int ratingDelta;

    public bool IsOwnTurn => status == NavalMatchStatus.InProgress && currentTurnPlayerId == ownPlayerId;
}

[Serializable]
public sealed class NavalMatchIntro
{
    public string matchId;
    public string ownPlayerId;
    public string ownDisplayName;
    public string ownAvatarImageBase64;
    public string opponentPlayerId;
    public string opponentDisplayName;
    public string opponentAvatarImageBase64;
}

[Serializable]
public sealed class NavalMatchTicket
{
    public string ticketId;
    public string matchId;
    public string sessionId;
    public string state;
    public long createdUnixMs;
}

[Serializable]
public sealed class NavalFriendlyInvite
{
    public string inviteId;
    public string senderPlayerId;
    public string senderDisplayName;
    public long expiresUnixMs;
}

[Serializable]
public sealed class NavalLeaderboardEntry
{
    public int rank;
    public string playerId;
    public string displayName;
    public int mmr;
    public string league;
}

public enum NavalStorePlatform
{
    Apple,
    Google
}

[Serializable]
public sealed class NavalPurchaseRequest
{
    public NavalStorePlatform platform;
    public string productId;
    public string receipt;
    public string signature;
    public int localCostMinorUnits;
    public string localCurrency;
}

[Serializable]
public sealed class NavalPurchaseResult
{
    public bool verified;
    public string productId;
    public NavalEntitlements entitlements;
}

[Serializable]
public sealed class NavalEntitlements
{
    public List<string> commanderIds = new List<string>();
    public List<string> cosmeticIds = new List<string>();
    public int premiumCurrency;

    public bool OwnsCommander(string commanderId)
    {
        return commanderId == "standard-commander" ||
               commanderId == "elias-voss" ||
               commanderId == "dae-hyun-kwon" ||
               commanderIds.Contains(commanderId);
    }
}

public static class NavalRewardCodes
{
    public const string AllCommandersCode = "op_start";

    public static readonly IReadOnlyList<string> AllCommanderIds = new[]
    {
        "standard-commander",
        "elias-voss",
        "dae-hyun-kwon",
        "ronan-graves",
        "arjan-dhillon",
        "mateo-serrano",
        "imani-cross"
    };
}

public static class NavalOnlineProtocol
{
    public const string CloudModule = "NavalCommandOnline";
    public const string RulesVersion = "online-rules-v3";
    public const int BoardSize = 10;
    public const int TurnSeconds = 45;
    public const int FriendInviteMinutes = 10;
}

public static class NavalRankRules
{
    public const int InitialMmr = 1000;
    public const int PlacementGames = 5;
    public const int PlacementK = 48;
    public const int RankedK = 24;

    public static int CalculateNewMmr(int ownMmr, int opponentMmr, bool won, bool placement)
    {
        double expected = 1.0 / (1.0 + Math.Pow(10.0, (opponentMmr - ownMmr) / 400.0));
        int k = placement ? PlacementK : RankedK;
        int updated = (int)Math.Round(ownMmr + k * ((won ? 1.0 : 0.0) - expected));
        return Math.Max(0, updated);
    }

    public static int SoftReset(int previousMmr)
    {
        return InitialMmr + (previousMmr - InitialMmr) / 2;
    }

    public static string GetLeague(int mmr, bool placementComplete)
    {
        if (!placementComplete) return "PLATZIERUNG";
        if (mmr < 900) return "REKRUT";
        if (mmr < 1050) return "BRONZE";
        if (mmr < 1200) return "SILBER";
        if (mmr < 1350) return "GOLD";
        if (mmr < 1500) return "PLATIN";
        return "ADMIRAL";
    }
}

public static class NavalSeasonRules
{
    public const long SeasonOneStartUnixMs = 1785542400000L; // 2026-08-01T00:00:00Z
    public const long SeasonDurationUnixMs = 56L * 24L * 60L * 60L * 1000L;

    public static int GetSeasonNumber(long unixMs)
    {
        if (unixMs <= SeasonOneStartUnixMs) return 1;
        return 1 + (int)((unixMs - SeasonOneStartUnixMs) / SeasonDurationUnixMs);
    }

    public static string GetSeasonId(long unixMs)
    {
        return "S" + GetSeasonNumber(unixMs).ToString("00");
    }

    public static long GetSeasonEndUnixMs(long unixMs)
    {
        int number = GetSeasonNumber(unixMs);
        return SeasonOneStartUnixMs + number * SeasonDurationUnixMs;
    }

    public static string GetLeaderboardId(string seasonId)
    {
        if (string.IsNullOrWhiteSpace(seasonId) || seasonId.Length != 3 || seasonId[0] != 'S' ||
            !int.TryParse(seasonId.Substring(1), out int number) || number < 1)
            throw new ArgumentException("INVALID_SEASON_ID", nameof(seasonId));
        return "naval-ranked-season-" + number.ToString("00");
    }
}
