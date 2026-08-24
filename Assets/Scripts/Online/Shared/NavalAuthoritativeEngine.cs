using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Deterministic, transport-independent rules used by the Cloud Code module.
/// The client may render returned views, but never owns this state in online play.
/// </summary>
public static class NavalAuthoritativeEngine
{
    public const string OracleScan = "oracle-scan";
    public const string AresRepair = "ares-repair";
    public const string ChemicalBomb = "chemical-bomb";
    public const string MineLayer = "mine-layer";
    public const string FleetRelocation = "fleet-relocation";
    public const string NuclearStrike = "nuclear-strike";
    public const string RangefinderShot = "rangefinder-shot";
    public const string BerserkerBarrage = "berserker-barrage";
    public const string AbyssTorpedo = "abyss-torpedo";
    public const string AbyssSubmerge = "abyss-submerge";
    public const string RaptorJetStart = "raptor-jet-start";
    public const string RaptorJammer = "raptor-jammer";

    private static readonly Dictionary<string, int[]> FleetByCommander = new Dictionary<string, int[]>
    {
        { "standard-commander", new[] { 5, 4, 3, 3, 2 } },
        { "elias-voss", new[] { 5, 3, 2 } },
        { "dae-hyun-kwon", new[] { 4, 4, 2, 2 } },
        { "ronan-graves", new[] { 14, 2 } },
        { "arjan-dhillon", new[] { 6, 4, 3 } },
        { "mateo-serrano", new[] { 5, 3, 3 } },
        { "imani-cross", new[] { 5, 6, 2 } }
    };

    public static NavalServerMatch CreateMatch(
        string matchId,
        NavalMatchMode mode,
        string firstPlayerId,
        string firstDisplayName,
        NavalPendingLoadout firstLoadout,
        string secondPlayerId,
        string secondDisplayName,
        NavalPendingLoadout secondLoadout,
        long nowUnixMs)
    {
        if (string.IsNullOrWhiteSpace(matchId)) throw new NavalRuleException("MATCH_ID_REQUIRED");
        if (firstPlayerId == secondPlayerId) throw new NavalRuleException("DISTINCT_PLAYERS_REQUIRED");

        NavalServerPlayer first = CreatePlayer(firstPlayerId, firstDisplayName, firstLoadout);
        NavalServerPlayer second = CreatePlayer(secondPlayerId, secondDisplayName, secondLoadout);
        return new NavalServerMatch
        {
            matchId = matchId,
            mode = mode,
            status = NavalMatchStatus.InProgress,
            version = 1,
            first = first,
            second = second,
            currentTurnPlayerId = firstPlayerId,
            turnDeadlineUnixMs = nowUnixMs + NavalOnlineProtocol.TurnSeconds * 1000L,
            lastEvent = "MATCH GESTARTET"
        };
    }

    public static void ValidateLoadout(NavalPendingLoadout loadout)
    {
        if (loadout == null || string.IsNullOrWhiteSpace(loadout.commanderId))
            throw new NavalRuleException("LOADOUT_REQUIRED");
        if (loadout.clientRulesVersion != NavalOnlineProtocol.RulesVersion)
            throw new NavalRuleException("RULES_VERSION_MISMATCH");
        if (!FleetByCommander.TryGetValue(loadout.commanderId, out int[] required))
            throw new NavalRuleException("UNKNOWN_COMMANDER");
        if (loadout.ships == null || loadout.ships.Count != required.Length)
            throw new NavalRuleException("WRONG_FLEET_SIZE");

        int[] actual = loadout.ships.Select(ship => ship.length).OrderBy(length => length).ToArray();
        int[] expected = required.OrderBy(length => length).ToArray();
        if (!actual.SequenceEqual(expected)) throw new NavalRuleException("WRONG_FLEET_COMPOSITION");

        bool[,] occupied = new bool[NavalOnlineProtocol.BoardSize, NavalOnlineProtocol.BoardSize];
        foreach (NavalShipPlacement ship in loadout.ships)
        {
            if (ship.length < 2) throw new NavalRuleException("INVALID_SHIP_LENGTH");
            int expectedWidth = ship.length == 14 ? 7 : ship.length == 6 ? 3 : ship.length;
            int expectedHeight = ship.length == 14 || ship.length == 6 ? 2 : 1;
            int width = ship.width > 0 ? ship.width : expectedWidth;
            int height = ship.height > 0 ? ship.height : expectedHeight;
            if (width != expectedWidth || height != expectedHeight || width * height != ship.length)
                throw new NavalRuleException("INVALID_SHIP_SHAPE");
            int rows = ship.vertical ? width : height;
            int columns = ship.vertical ? height : width;
            for (int rowOffset = 0; rowOffset < rows; rowOffset++)
            {
                for (int columnOffset = 0; columnOffset < columns; columnOffset++)
                {
                    int row = ship.row + rowOffset;
                    int column = ship.column + columnOffset;
                    EnsureCell(row, column);
                    if (occupied[row, column]) throw new NavalRuleException("OVERLAPPING_SHIPS");
                    occupied[row, column] = true;
                }
            }
        }
    }

    public static NavalPlayerMatchView SubmitAction(
        NavalServerMatch match,
        string actorPlayerId,
        NavalMatchAction action,
        long nowUnixMs)
    {
        EnsureParticipant(match, actorPlayerId);
        if (action == null || action.matchId != match.matchId) throw new NavalRuleException("INVALID_ACTION");
        if (string.IsNullOrWhiteSpace(action.actionId)) throw new NavalRuleException("ACTION_ID_REQUIRED");
        if (match.processedActionIds.Contains(action.actionId)) return BuildView(match, actorPlayerId);
        if (match.status != NavalMatchStatus.InProgress) throw new NavalRuleException("MATCH_FINISHED");
        if (action.expectedVersion != match.version) throw new NavalRuleException("STALE_MATCH_VERSION");

        if (action.type == NavalActionType.ClaimTimeout)
        {
            if (nowUnixMs < match.turnDeadlineUnixMs) throw new NavalRuleException("TURN_NOT_EXPIRED");
            Finish(match, OpponentOf(match, match.currentTurnPlayerId).playerId, "ZEITÜBERSCHREITUNG");
        }
        else if (action.type == NavalActionType.Surrender)
        {
            Finish(match, OpponentOf(match, actorPlayerId).playerId, "AUFGEGEBEN");
        }
        else
        {
            if (actorPlayerId != match.currentTurnPlayerId) throw new NavalRuleException("NOT_YOUR_TURN");
            if (nowUnixMs >= match.turnDeadlineUnixMs)
                throw new NavalRuleException("TURN_EXPIRED");

            NavalServerPlayer actor = PlayerOf(match, actorPlayerId);
            NavalServerPlayer defender = OpponentOf(match, actorPlayerId);
            if (action.type == NavalActionType.NormalShot)
            {
                ResolveNormalShot(match, actor, defender, action.row, action.column);
                actor.abilitiesJammed = false;
            }
            else if (action.type == NavalActionType.Ability)
            {
                if (actor.bonusShotsRemaining > 0) throw new NavalRuleException("BONUS_SHOT_NORMAL_ONLY");
                if (actor.abilitiesJammed) throw new NavalRuleException("ABILITIES_JAMMED");
                ResolveAbility(match, actor, defender, action);
            }
            else
                throw new NavalRuleException("UNSUPPORTED_ACTION");

            if (AllSunk(defender)) Finish(match, actor.playerId, "FLOTTE ZERSTÖRT");
            else AdvanceTurn(match, actor, defender, nowUnixMs);
        }

        match.processedActionIds.Add(action.actionId);
        match.version++;
        return BuildView(match, actorPlayerId);
    }

    public static NavalPlayerMatchView BuildView(NavalServerMatch match, string viewerPlayerId)
    {
        EnsureParticipant(match, viewerPlayerId);
        NavalServerPlayer viewer = PlayerOf(match, viewerPlayerId);
        NavalServerPlayer opponent = OpponentOf(match, viewerPlayerId);
        NavalPlayerMatchView view = new NavalPlayerMatchView
        {
            matchId = match.matchId,
            mode = match.mode,
            status = match.status,
            version = match.version,
            ownPlayerId = viewer.playerId,
            opponentPlayerId = opponent.playerId,
            opponentDisplayName = opponent.displayName,
            ownCommanderId = viewer.commanderId,
            opponentCommanderId = opponent.commanderId,
            currentTurnPlayerId = match.currentTurnPlayerId,
            turnDeadlineUnixMs = match.turnDeadlineUnixMs,
            ownAbilityPoints = viewer.abilityPoints,
            opponentAbilityPoints = opponent.abilityPoints,
            ownJetLaunched = viewer.jetLaunched,
            ownJetActive = viewer.jetActive,
            opponentJetActive = opponent.jetActive,
            ownAbilitiesJammed = viewer.abilitiesJammed,
            lastEvent = match.lastEvent,
            winnerPlayerId = match.winnerPlayerId,
            ratingDelta = viewer.playerId == match.first.playerId ? match.firstRatingDelta : match.secondRatingDelta
        };

        for (int row = 0; row < NavalOnlineProtocol.BoardSize; row++)
        {
            for (int column = 0; column < NavalOnlineProtocol.BoardSize; column++)
            {
                int ownShipIndex = viewer.board[row, column];
                bool ownShot = viewer.shotsReceived[row, column];
                view.ownBoard.Add(new NavalCellView
                {
                    row = row,
                    column = column,
                    ship = ownShipIndex >= 0,
                    shot = ownShot,
                    hit = ownShot &&
                          ((ownShipIndex >= 0 && !viewer.blockedShots[row, column]) ||
                           (viewer.destroyedJetRow == row && viewer.destroyedJetColumn == column)),
                    blocked = viewer.blockedShots[row, column],
                    sunk = ownShipIndex >= 0 && viewer.ships[ownShipIndex].IsSunk,
                    mine = viewer.mines[row, column],
                    jet = viewer.jetActive && viewer.jetRow == row && viewer.jetColumn == column
                });

                int opponentShipIndex = opponent.board[row, column];
                bool opponentShot = opponent.shotsReceived[row, column];
                bool chemicallyRevealed = opponentShipIndex >= 0 && opponent.revealedShips[opponentShipIndex];
                view.opponentBoard.Add(new NavalCellView
                {
                    row = row,
                    column = column,
                    shot = opponentShot,
                    hit = opponentShot &&
                          ((opponentShipIndex >= 0 && !opponent.blockedShots[row, column]) ||
                           (opponent.destroyedJetRow == row && opponent.destroyedJetColumn == column)),
                    blocked = opponent.blockedShots[row, column],
                    ship = (opponentShot && opponentShipIndex >= 0 && !opponent.blockedShots[row, column]) || chemicallyRevealed,
                    sunk = opponentShipIndex >= 0 && opponent.ships[opponentShipIndex].IsSunk,
                    scannedWater = viewer.scannedWater[row, column],
                    revealedContact = chemicallyRevealed,
                    mine = false
                });
            }
        }
        return view;
    }

    private static NavalServerPlayer CreatePlayer(string id, string displayName, NavalPendingLoadout loadout)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new NavalRuleException("PLAYER_ID_REQUIRED");
        ValidateLoadout(loadout);
        NavalServerPlayer player = new NavalServerPlayer
        {
            playerId = id,
            displayName = string.IsNullOrWhiteSpace(displayName) ? "COMMANDER" : displayName,
            commanderId = loadout.commanderId
        };
        Fill(player.board, -1);
        for (int shipIndex = 0; shipIndex < loadout.ships.Count; shipIndex++)
        {
            NavalShipPlacement placement = loadout.ships[shipIndex];
            int width = placement.width > 0 ? placement.width : (placement.length == 14 ? 7 : placement.length == 6 ? 3 : placement.length);
            int height = placement.height > 0 ? placement.height : (placement.length == 14 || placement.length == 6 ? 2 : 1);
            NavalServerShip ship = new NavalServerShip
            {
                length = placement.length,
                width = width,
                height = height,
                row = placement.row,
                column = placement.column,
                vertical = placement.vertical
            };
            player.ships.Add(ship);
            int rows = placement.vertical ? width : height;
            int columns = placement.vertical ? height : width;
            for (int rowOffset = 0; rowOffset < rows; rowOffset++)
            {
                for (int columnOffset = 0; columnOffset < columns; columnOffset++)
                    player.board[placement.row + rowOffset, placement.column + columnOffset] = shipIndex;
            }
        }
        player.revealedShips = new bool[player.ships.Count];
        return player;
    }

    private static void ResolveNormalShot(NavalServerMatch match, NavalServerPlayer actor, NavalServerPlayer defender, int row, int column)
    {
        EnsureCell(row, column);
        if (defender.shotsReceived[row, column]) throw new NavalRuleException("CELL_ALREADY_TARGETED");
        defender.shotsReceived[row, column] = true;
        if (defender.jetActive && defender.jetRow == row && defender.jetColumn == column)
        {
            defender.jetActive = false;
            defender.destroyedJetRow = row;
            defender.destroyedJetColumn = column;
            match.lastEvent = "RAPTOR // JET ABGESCHOSSEN";
            return;
        }
        int shipIndex = defender.board[row, column];
        if (shipIndex >= 0)
        {
            if (TryDamageShip(defender, row, column))
                match.lastEvent = defender.ships[shipIndex].IsSunk ? "SCHIFF VERSENKT" : "TREFFER";
            else
                match.lastEvent = "ABYSS // TREFFER BLOCKIERT";
        }
        else
        {
            if (!actor.suppressBonusMissPoints) actor.abilityPoints++;
            match.lastEvent = actor.suppressBonusMissPoints ? "FREISCHUSS // DANEBEN" : "DANEBEN // +1 PUNKT";
        }

        if (defender.jetActive)
            MoveRaptorJet(defender, row * NavalOnlineProtocol.BoardSize + column + match.version + 17);

        if (defender.mines[row, column])
        {
            defender.mines[row, column] = false;
            defender.bonusShotsRemaining = 3;
            defender.suppressBonusMissPoints = true;
        }
    }

    private static void ResolveAbility(NavalServerMatch match, NavalServerPlayer actor, NavalServerPlayer defender, NavalMatchAction action)
    {
        int baseCost = AbilityCost(actor.commanderId, action.abilityId);
        bool developerOverride = action.developerFreeAbilities &&
                                 string.Equals(actor.displayName, "andreas_dev", StringComparison.OrdinalIgnoreCase);
        int cost = developerOverride ? 0 : baseCost;
        if (actor.abilityPoints < cost) throw new NavalRuleException("NOT_ENOUGH_ABILITY_POINTS");
        switch (action.abilityId)
        {
            case OracleScan:
                ResolveScan(actor, defender, action.row, action.column);
                match.lastEvent = "ORACLE // SEKTOR GESCANNT";
                break;
            case AresRepair:
                ResolveRepair(actor, action.row, action.column);
                match.lastEvent = "ARES // SCHIFFSTEIL REPARIERT";
                break;
            case ChemicalBomb:
                ResolveChemical(defender, action.row, action.column, match);
                break;
            case MineLayer:
                ResolveMine(actor, action.row, action.column);
                match.lastEvent = "MINE GELEGT";
                break;
            case FleetRelocation:
                ResolveRelocation(actor, action.sourceRow, action.sourceColumn, action.row, action.column);
                match.lastEvent = "TITAN // SCHIFF VERSCHOBEN";
                break;
            case NuclearStrike:
                ResolveNuclear(defender, action.row, action.column, match);
                break;
            case RangefinderShot:
                ResolveRangefinder(defender, action.row, action.column, match);
                break;
            case BerserkerBarrage:
                ResolveBerserker(defender, action, match);
                break;
            case AbyssTorpedo:
                ResolveAbyssTorpedo(defender, action.row, match);
                break;
            case AbyssSubmerge:
                ResolveAbyssSubmerge(actor, action.row, action.column);
                match.lastEvent = "ABYSS // SCHIFF UNTERGETAUCHT";
                break;
            case RaptorJetStart:
                ResolveRaptorJetStart(actor, match.version);
                match.lastEvent = "RAPTOR // JET IN DER LUFT";
                break;
            case RaptorJammer:
                ResolveRaptorJammer(defender);
                match.lastEvent = "RAPTOR // FÄHIGKEITEN GESTÖRT";
                break;
            default:
                throw new NavalRuleException("ABILITY_NOT_AVAILABLE");
        }
        actor.abilityPoints -= cost;
    }

    private static int AbilityCost(string commanderId, string abilityId)
    {
        if (commanderId == "elias-voss" && abilityId == OracleScan) return 8;
        if (commanderId == "elias-voss" && abilityId == AresRepair) return 3;
        if (commanderId == "dae-hyun-kwon" && abilityId == ChemicalBomb) return 4;
        if (commanderId == "dae-hyun-kwon" && abilityId == MineLayer) return 7;
        if (commanderId == "ronan-graves" && abilityId == FleetRelocation) return 5;
        if (commanderId == "ronan-graves" && abilityId == NuclearStrike) return 7;
        if (commanderId == "arjan-dhillon" && abilityId == RangefinderShot) return 5;
        if (commanderId == "arjan-dhillon" && abilityId == BerserkerBarrage) return 8;
        if (commanderId == "mateo-serrano" && abilityId == AbyssTorpedo) return 14;
        if (commanderId == "mateo-serrano" && abilityId == AbyssSubmerge) return 5;
        if (commanderId == "imani-cross" && abilityId == RaptorJetStart) return 8;
        if (commanderId == "imani-cross" && abilityId == RaptorJammer) return 3;
        throw new NavalRuleException("ABILITY_NOT_AVAILABLE");
    }

    private static void ResolveScan(NavalServerPlayer actor, NavalServerPlayer defender, int centerRow, int centerColumn)
    {
        EnsureCell(centerRow, centerColumn);
        int startRow = Math.Max(0, Math.Min(NavalOnlineProtocol.BoardSize - 3, centerRow - 1));
        int startColumn = Math.Max(0, Math.Min(NavalOnlineProtocol.BoardSize - 3, centerColumn - 1));
        bool hasNewInformation = false;
        bool contact = false;
        for (int row = startRow; row < startRow + 3; row++)
        for (int column = startColumn; column < startColumn + 3; column++)
        {
            if (defender.board[row, column] >= 0) contact = true;
            else if (!defender.shotsReceived[row, column] && !actor.scannedWater[row, column])
                hasNewInformation = true;
        }
        if (!hasNewInformation && !contact) throw new NavalRuleException("SECTOR_ALREADY_SCANNED");
        for (int row = startRow; row < startRow + 3; row++)
        for (int column = startColumn; column < startColumn + 3; column++)
            if (defender.board[row, column] < 0 && !defender.shotsReceived[row, column]) actor.scannedWater[row, column] = true;
    }

    private static void ResolveRepair(NavalServerPlayer actor, int row, int column)
    {
        EnsureCell(row, column);
        int shipIndex = actor.board[row, column];
        if (shipIndex < 0 || !actor.shotsReceived[row, column]) throw new NavalRuleException("NO_DAMAGE_HERE");
        if (actor.ships[shipIndex].IsSunk) throw new NavalRuleException("SUNK_SHIP_CANNOT_BE_REPAIRED");
        actor.shotsReceived[row, column] = false;
        actor.ships[shipIndex].hits = Math.Max(0, actor.ships[shipIndex].hits - 1);
    }

    private static void ResolveChemical(NavalServerPlayer defender, int row, int column, NavalServerMatch match)
    {
        EnsureCell(row, column);
        if (defender.shotsReceived[row, column]) throw new NavalRuleException("CELL_ALREADY_TARGETED");
        defender.shotsReceived[row, column] = true;
        int shipIndex = defender.board[row, column];
        if (shipIndex < 0)
        {
            match.lastEvent = "CHEMIEBOMBE // KEIN KONTAKT";
            return;
        }
        if (!TryDamageShip(defender, row, column))
        {
            match.lastEvent = "ABYSS // TREFFER BLOCKIERT";
            return;
        }
        defender.revealedShips[shipIndex] = true;
        match.lastEvent = "CHEMIEBOMBE // SCHIFF AUFGEDECKT";
    }

    private static void ResolveMine(NavalServerPlayer actor, int row, int column)
    {
        EnsureCell(row, column);
        if (actor.shotsReceived[row, column]) throw new NavalRuleException("CELL_ALREADY_TARGETED");
        if (actor.mines[row, column]) throw new NavalRuleException("MINE_ALREADY_PRESENT");
        actor.mines[row, column] = true;
    }

    private static void ResolveRelocation(NavalServerPlayer actor, int sourceRow, int sourceColumn, int targetRow, int targetColumn)
    {
        EnsureCell(sourceRow, sourceColumn);
        EnsureCell(targetRow, targetColumn);
        int shipIndex = actor.board[sourceRow, sourceColumn];
        if (shipIndex < 0) throw new NavalRuleException("NO_SHIP_HERE");
        NavalServerShip ship = actor.ships[shipIndex];
        if (ship.hits > 0) throw new NavalRuleException("DAMAGED_SHIP_CANNOT_RELOCATE");
        if (ship.row == targetRow && ship.column == targetColumn) throw new NavalRuleException("RELOCATION_MUST_MOVE");

        int rows = ship.vertical ? ship.width : ship.height;
        int columns = ship.vertical ? ship.height : ship.width;
        for (int rowOffset = 0; rowOffset < rows; rowOffset++)
        for (int columnOffset = 0; columnOffset < columns; columnOffset++)
        {
            int row = targetRow + rowOffset;
            int column = targetColumn + columnOffset;
            EnsureCell(row, column);
            int occupant = actor.board[row, column];
            if (occupant >= 0 && occupant != shipIndex) throw new NavalRuleException("RELOCATION_BLOCKED");
            if (actor.shotsReceived[row, column]) throw new NavalRuleException("RELOCATION_TARGET_ALREADY_FIRED_ON");
        }

        for (int row = 0; row < NavalOnlineProtocol.BoardSize; row++)
        for (int column = 0; column < NavalOnlineProtocol.BoardSize; column++)
            if (actor.board[row, column] == shipIndex) actor.board[row, column] = -1;
        ship.row = targetRow;
        ship.column = targetColumn;
        for (int rowOffset = 0; rowOffset < rows; rowOffset++)
        for (int columnOffset = 0; columnOffset < columns; columnOffset++)
            actor.board[targetRow + rowOffset, targetColumn + columnOffset] = shipIndex;
    }

    private static void ResolveNuclear(NavalServerPlayer defender, int row, int column, NavalServerMatch match)
    {
        EnsureCell(row, column);
        if (defender.shotsReceived[row, column]) throw new NavalRuleException("CELL_ALREADY_TARGETED");
        defender.shotsReceived[row, column] = true;
        int shipIndex = defender.board[row, column];
        if (shipIndex < 0)
        {
            match.lastEvent = "ATOMBOMBE // KEIN KONTAKT";
            return;
        }

        if (shipIndex == defender.submergedShipIndex)
        {
            defender.blockedShots[row, column] = true;
            match.lastEvent = "ABYSS // TREFFER BLOCKIERT";
            return;
        }
        for (int boardRow = 0; boardRow < NavalOnlineProtocol.BoardSize; boardRow++)
        for (int boardColumn = 0; boardColumn < NavalOnlineProtocol.BoardSize; boardColumn++)
            if (defender.board[boardRow, boardColumn] == shipIndex)
                defender.shotsReceived[boardRow, boardColumn] = true;
        defender.ships[shipIndex].hits = defender.ships[shipIndex].length;
        match.lastEvent = "ATOMBOMBE // ZIEL AUSGELÖSCHT";
    }

    private static void ResolveRangefinder(NavalServerPlayer defender, int row, int column, NavalServerMatch match)
    {
        EnsureCell(row, column);
        if (defender.shotsReceived[row, column]) throw new NavalRuleException("CELL_ALREADY_TARGETED");
        defender.shotsReceived[row, column] = true;
        int hitShip = defender.board[row, column];
        if (hitShip >= 0) TryDamageShip(defender, row, column);

        int nearest = int.MaxValue;
        for (int boardRow = 0; boardRow < NavalOnlineProtocol.BoardSize; boardRow++)
        for (int boardColumn = 0; boardColumn < NavalOnlineProtocol.BoardSize; boardColumn++)
        {
            int shipIndex = defender.board[boardRow, boardColumn];
            if (shipIndex < 0 || defender.ships[shipIndex].IsSunk) continue;
            nearest = Math.Min(nearest, Math.Abs(boardRow - row) + Math.Abs(boardColumn - column));
        }
        match.lastEvent = nearest == int.MaxValue
            ? "VECTOR // KEIN AKTIVES ZIEL"
            : "VECTOR // DISTANZ " + nearest + " FELDER";
    }

    private static void ResolveBerserker(NavalServerPlayer defender, NavalMatchAction action, NavalServerMatch match)
    {
        List<int> available = new List<int>();
        for (int row = 0; row < NavalOnlineProtocol.BoardSize; row++)
        for (int column = 0; column < NavalOnlineProtocol.BoardSize; column++)
            if (!defender.shotsReceived[row, column]) available.Add(row * NavalOnlineProtocol.BoardSize + column);
        if (available.Count == 0) throw new NavalRuleException("NO_AVAILABLE_TARGETS");

        int seed = 17;
        string seedText = (action.actionId ?? string.Empty) + ":" + match.version;
        unchecked
        {
            for (int index = 0; index < seedText.Length; index++) seed = seed * 31 + seedText[index];
        }
        Random random = new Random(seed);
        int shotCount = Math.Min(6, available.Count);
        char[] pattern = new char[shotCount];
        for (int shot = 0; shot < shotCount; shot++)
        {
            int choice = random.Next(available.Count);
            int encoded = available[choice];
            available.RemoveAt(choice);
            int row = encoded / NavalOnlineProtocol.BoardSize;
            int column = encoded % NavalOnlineProtocol.BoardSize;
            defender.shotsReceived[row, column] = true;
            int shipIndex = defender.board[row, column];
            bool hit = shipIndex >= 0 && TryDamageShip(defender, row, column);
            pattern[shot] = hit ? 'H' : defender.blockedShots[row, column] ? 'B' : 'M';
        }
        match.lastEvent = "AMOKLAUF // " + new string(pattern);
    }

    private static void ResolveAbyssTorpedo(NavalServerPlayer defender, int row, NavalServerMatch match)
    {
        EnsureCell(row, 0);
        int resolved = 0;
        int hits = 0;
        int blocked = 0;
        for (int column = 0; column < NavalOnlineProtocol.BoardSize; column++)
        {
            if (defender.shotsReceived[row, column]) continue;
            defender.shotsReceived[row, column] = true;
            resolved++;
            int shipIndex = defender.board[row, column];
            if (shipIndex < 0) continue;
            if (TryDamageShip(defender, row, column)) hits++;
            else blocked++;
        }
        if (resolved == 0) throw new NavalRuleException("ROW_ALREADY_TARGETED");
        match.lastEvent = blocked > 0
            ? "ABYSS // TORPEDO " + hits + " TREFFER // " + blocked + " BLOCKIERT"
            : hits > 0 ? "ABYSS // TORPEDO " + hits + " TREFFER" : "ABYSS // TORPEDO OHNE KONTAKT";
    }

    private static void ResolveAbyssSubmerge(NavalServerPlayer actor, int row, int column)
    {
        EnsureCell(row, column);
        int shipIndex = actor.board[row, column];
        if (shipIndex < 0) throw new NavalRuleException("NO_SHIP_HERE");
        if (actor.ships[shipIndex].IsSunk) throw new NavalRuleException("SUNK_SHIP_CANNOT_SUBMERGE");
        actor.submergedShipIndex = shipIndex;
    }

    private static void ResolveRaptorJetStart(NavalServerPlayer actor, int seed)
    {
        if (actor.jetLaunched) throw new NavalRuleException("JET_ALREADY_LAUNCHED");
        bool activeCarrier = actor.ships.Any(ship => ship.length == 5 && !ship.IsSunk);
        if (!activeCarrier) throw new NavalRuleException("NO_ACTIVE_AIRCRAFT_CARRIER");
        if (!MoveRaptorJet(actor, seed)) throw new NavalRuleException("NO_FREE_WATER_CELL");
        actor.jetLaunched = true;
        actor.jetActive = true;
    }

    private static void ResolveRaptorJammer(NavalServerPlayer defender)
    {
        if (defender.abilitiesJammed) throw new NavalRuleException("JAMMER_ALREADY_ACTIVE");
        defender.abilitiesJammed = true;
    }

    private static bool MoveRaptorJet(NavalServerPlayer player, int seed)
    {
        int start = Math.Abs(seed % (NavalOnlineProtocol.BoardSize * NavalOnlineProtocol.BoardSize));
        for (int offset = 0; offset < NavalOnlineProtocol.BoardSize * NavalOnlineProtocol.BoardSize; offset++)
        {
            int encoded = (start + offset) % (NavalOnlineProtocol.BoardSize * NavalOnlineProtocol.BoardSize);
            int row = encoded / NavalOnlineProtocol.BoardSize;
            int column = encoded % NavalOnlineProtocol.BoardSize;
            if (player.board[row, column] >= 0 || player.shotsReceived[row, column] || player.mines[row, column]) continue;
            if (player.jetActive && player.jetRow == row && player.jetColumn == column) continue;
            player.jetRow = row;
            player.jetColumn = column;
            return true;
        }
        return false;
    }

    private static bool TryDamageShip(NavalServerPlayer defender, int row, int column)
    {
        int shipIndex = defender.board[row, column];
        if (shipIndex < 0) return false;
        if (shipIndex == defender.submergedShipIndex)
        {
            defender.blockedShots[row, column] = true;
            return false;
        }
        defender.ships[shipIndex].hits++;
        return true;
    }

    private static void AdvanceTurn(NavalServerMatch match, NavalServerPlayer actor, NavalServerPlayer defender, long nowUnixMs)
    {
        if (actor.bonusShotsRemaining > 0)
        {
            actor.bonusShotsRemaining--;
            actor.suppressBonusMissPoints = actor.bonusShotsRemaining > 0;
            match.currentTurnPlayerId = actor.bonusShotsRemaining > 0 ? actor.playerId : defender.playerId;
        }
        else
        {
            actor.suppressBonusMissPoints = false;
            match.currentTurnPlayerId = defender.playerId;
        }
        if (match.currentTurnPlayerId != actor.playerId)
            defender.submergedShipIndex = -1;
        match.turnDeadlineUnixMs = nowUnixMs + NavalOnlineProtocol.TurnSeconds * 1000L;
    }

    private static void Finish(NavalServerMatch match, string winnerPlayerId, string reason)
    {
        match.status = NavalMatchStatus.Finished;
        match.winnerPlayerId = winnerPlayerId;
        match.currentTurnPlayerId = null;
        match.turnDeadlineUnixMs = 0;
        match.lastEvent = reason;
    }

    private static bool AllSunk(NavalServerPlayer player) => player.ships.All(ship => ship.IsSunk) && !player.jetActive;
    private static NavalServerPlayer PlayerOf(NavalServerMatch match, string id) => match.first.playerId == id ? match.first : match.second;
    private static NavalServerPlayer OpponentOf(NavalServerMatch match, string id) => match.first.playerId == id ? match.second : match.first;

    private static void EnsureParticipant(NavalServerMatch match, string playerId)
    {
        if (match == null) throw new NavalRuleException("MATCH_REQUIRED");
        if (match.first.playerId != playerId && match.second.playerId != playerId) throw new NavalRuleException("NOT_A_PARTICIPANT");
    }

    private static void EnsureCell(int row, int column)
    {
        if (row < 0 || column < 0 || row >= NavalOnlineProtocol.BoardSize || column >= NavalOnlineProtocol.BoardSize)
            throw new NavalRuleException("CELL_OUT_OF_BOUNDS");
    }

    private static void Fill(int[,] board, int value)
    {
        for (int row = 0; row < board.GetLength(0); row++)
        for (int column = 0; column < board.GetLength(1); column++) board[row, column] = value;
    }
}

[Serializable]
public sealed class NavalServerMatch
{
    public string matchId;
    public NavalMatchMode mode;
    public NavalMatchStatus status;
    public int version;
    public NavalServerPlayer first;
    public NavalServerPlayer second;
    public string currentTurnPlayerId;
    public long turnDeadlineUnixMs;
    public string winnerPlayerId;
    public string lastEvent;
    public int firstMmrBefore;
    public int secondMmrBefore;
    public bool firstPlacementAtStart;
    public bool secondPlacementAtStart;
    public int firstRatingDelta;
    public int secondRatingDelta;
    public bool rewardsFinalized;
    public string rankedSeasonId;
    public HashSet<string> processedActionIds = new HashSet<string>();
}

[Serializable]
public sealed class NavalServerPlayer
{
    public string playerId;
    public string displayName;
    public string commanderId;
    public int abilityPoints;
    public int bonusShotsRemaining;
    public bool suppressBonusMissPoints;
    public int[,] board = new int[NavalOnlineProtocol.BoardSize, NavalOnlineProtocol.BoardSize];
    public bool[,] shotsReceived = new bool[NavalOnlineProtocol.BoardSize, NavalOnlineProtocol.BoardSize];
    public bool[,] scannedWater = new bool[NavalOnlineProtocol.BoardSize, NavalOnlineProtocol.BoardSize];
    public bool[,] mines = new bool[NavalOnlineProtocol.BoardSize, NavalOnlineProtocol.BoardSize];
    public bool[,] blockedShots = new bool[NavalOnlineProtocol.BoardSize, NavalOnlineProtocol.BoardSize];
    public int submergedShipIndex = -1;
    public bool jetLaunched;
    public bool jetActive;
    public int jetRow = -1;
    public int jetColumn = -1;
    public int destroyedJetRow = -1;
    public int destroyedJetColumn = -1;
    public bool abilitiesJammed;
    public bool[] revealedShips;
    public List<NavalServerShip> ships = new List<NavalServerShip>();
}

[Serializable]
public sealed class NavalServerShip
{
    public int length;
    public int width;
    public int height;
    public int row;
    public int column;
    public bool vertical;
    public int hits;
    public bool IsSunk => hits >= length;
}

public sealed class NavalRuleException : InvalidOperationException
{
    public string Code { get; }
    public NavalRuleException(string code) : base(code) { Code = code; }
}
