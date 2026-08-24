using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class NavalAuthoritativeEngineTests
{
    private const long Start = 1_000_000;

    [Test]
    public void ValidFleet_IsAccepted_AndOverlapIsRejected()
    {
        NavalPendingLoadout loadout = StandardLoadout();
        Assert.DoesNotThrow(() => NavalAuthoritativeEngine.ValidateLoadout(loadout));
        loadout.ships[1].row = 0;
        loadout.ships[1].column = 0;
        Assert.That(() => NavalAuthoritativeEngine.ValidateLoadout(loadout),
            Throws.TypeOf<NavalRuleException>().With.Property("Code").EqualTo("OVERLAPPING_SHIPS"));
    }

    [Test]
    public void OpponentView_DoesNotLeakUnhitShipsOrMines()
    {
        NavalServerMatch match = Match();
        match.second.mines[9, 9] = true;
        NavalPlayerMatchView view = NavalAuthoritativeEngine.BuildView(match, "p1");
        Assert.That(view.opponentBoard.Count(cell => cell.ship), Is.Zero);
        Assert.That(view.opponentBoard.Count(cell => cell.mine), Is.Zero);
        Assert.That(view.ownBoard.Count(cell => cell.ship), Is.EqualTo(17));
    }

    [Test]
    public void NormalMiss_AwardsPoint_ChangesTurn_AndAdvancesVersion()
    {
        NavalServerMatch match = Match();
        NavalMatchAction action = Action(match, NavalActionType.NormalShot, 9, 9);
        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", action, Start + 100);
        Assert.That(view.ownAbilityPoints, Is.EqualTo(1));
        Assert.That(view.currentTurnPlayerId, Is.EqualTo("p2"));
        Assert.That(view.version, Is.EqualTo(2));
        Assert.That(view.lastEvent, Does.Contain("+1"));
    }

    [Test]
    public void DuplicateAction_IsIdempotent()
    {
        NavalServerMatch match = Match();
        NavalMatchAction action = Action(match, NavalActionType.NormalShot, 9, 9);
        NavalAuthoritativeEngine.SubmitAction(match, "p1", action, Start + 100);
        NavalPlayerMatchView duplicate = NavalAuthoritativeEngine.SubmitAction(match, "p1", action, Start + 200);
        Assert.That(duplicate.version, Is.EqualTo(2));
        Assert.That(match.first.abilityPoints, Is.EqualTo(1));
    }

    [Test]
    public void StaleVersion_IsRejected()
    {
        NavalServerMatch match = Match();
        NavalMatchAction action = Action(match, NavalActionType.NormalShot, 9, 9);
        action.expectedVersion = 0;
        Assert.That(() => NavalAuthoritativeEngine.SubmitAction(match, "p1", action, Start + 100),
            Throws.TypeOf<NavalRuleException>().With.Property("Code").EqualTo("STALE_MATCH_VERSION"));
    }

    [Test]
    public void Timeout_CannotBeClaimedEarly_AndAwardsOpponentAfterDeadline()
    {
        NavalServerMatch match = Match();
        NavalMatchAction claim = Action(match, NavalActionType.ClaimTimeout, -1, -1);
        Assert.That(() => NavalAuthoritativeEngine.SubmitAction(match, "p2", claim, match.turnDeadlineUnixMs - 1),
            Throws.TypeOf<NavalRuleException>().With.Property("Code").EqualTo("TURN_NOT_EXPIRED"));
        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p2", claim, match.turnDeadlineUnixMs);
        Assert.That(view.status, Is.EqualTo(NavalMatchStatus.Finished));
        Assert.That(view.winnerPlayerId, Is.EqualTo("p2"));
    }

    [Test]
    public void ChemicalBomb_RevealsOnlyTheHitShip()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", DaeLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 4;
        NavalMatchAction action = Action(match, NavalActionType.Ability, 0, 0);
        action.abilityId = NavalAuthoritativeEngine.ChemicalBomb;
        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", action, Start + 1);
        Assert.That(view.ownAbilityPoints, Is.Zero);
        Assert.That(view.opponentBoard.Count(cell => cell.revealedContact), Is.EqualTo(5));
        Assert.That(view.opponentBoard.Count(cell => cell.ship), Is.EqualTo(5));
    }

    [Test]
    public void RankedRules_AreSymmetricAndSoftResetTowardsOneThousand()
    {
        int winner = NavalRankRules.CalculateNewMmr(1000, 1000, true, false);
        int loser = NavalRankRules.CalculateNewMmr(1000, 1000, false, false);
        Assert.That(winner, Is.EqualTo(1012));
        Assert.That(loser, Is.EqualTo(988));
        Assert.That(NavalRankRules.SoftReset(1400), Is.EqualTo(1200));
        Assert.That(NavalRankRules.SoftReset(800), Is.EqualTo(900));
    }

    [Test]
    public void Seasons_AreEightWeeksAndHaveStableIds()
    {
        Assert.That(NavalSeasonRules.GetSeasonId(NavalSeasonRules.SeasonOneStartUnixMs), Is.EqualTo("S01"));
        Assert.That(NavalSeasonRules.GetSeasonId(
            NavalSeasonRules.SeasonOneStartUnixMs + NavalSeasonRules.SeasonDurationUnixMs), Is.EqualTo("S02"));
        Assert.That(NavalSeasonRules.GetSeasonEndUnixMs(NavalSeasonRules.SeasonOneStartUnixMs),
            Is.EqualTo(NavalSeasonRules.SeasonOneStartUnixMs + NavalSeasonRules.SeasonDurationUnixMs));
        Assert.That(NavalSeasonRules.GetLeaderboardId("S02"), Is.EqualTo("naval-ranked-season-02"));
    }

    [Test]
    public void FinishedRankedView_OnlyReturnsTheViewersRatingDelta()
    {
        NavalServerMatch match = Match();
        match.status = NavalMatchStatus.Finished;
        match.winnerPlayerId = "p1";
        match.firstRatingDelta = 24;
        match.secondRatingDelta = -24;
        Assert.That(NavalAuthoritativeEngine.BuildView(match, "p1").ratingDelta, Is.EqualTo(24));
        Assert.That(NavalAuthoritativeEngine.BuildView(match, "p2").ratingDelta, Is.EqualTo(-24));
    }

    [Test]
    public void Surrender_IsAllowedOutsideThePlayersTurn()
    {
        NavalServerMatch match = Match();
        NavalMatchAction surrender = Action(match, NavalActionType.Surrender, -1, -1);
        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p2", surrender, Start + 10);
        Assert.That(view.status, Is.EqualTo(NavalMatchStatus.Finished));
        Assert.That(view.winnerPlayerId, Is.EqualTo("p1"));
    }

    [Test]
    public void Repair_RemovesExactlyOneHitFromAnActiveOwnShip()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", EliasLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.shotsReceived[0, 0] = true;
        match.first.ships[0].hits = 1;
        match.first.abilityPoints = 3;
        NavalMatchAction repair = Action(match, NavalActionType.Ability, 0, 0);
        repair.abilityId = NavalAuthoritativeEngine.AresRepair;

        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", repair, Start + 20);

        Assert.That(view.ownAbilityPoints, Is.Zero);
        Assert.That(view.ownBoard.Single(cell => cell.row == 0 && cell.column == 0).hit, Is.False);
        Assert.That(match.first.ships[0].hits, Is.Zero);
    }

    [Test]
    public void TriggeredMine_GrantsExactlyThreeNormalShotsWithoutMissPoints()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", DaeLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.mines[9, 9] = true;
        match.currentTurnPlayerId = "p2";
        NavalAuthoritativeEngine.SubmitAction(match, "p2", Action(match, NavalActionType.NormalShot, 9, 9), Start + 1);

        Assert.That(match.currentTurnPlayerId, Is.EqualTo("p1"));
        for (int shot = 0; shot < 3; shot++)
        {
            NavalMatchAction freeShot = Action(match, NavalActionType.NormalShot, 9, shot);
            NavalAuthoritativeEngine.SubmitAction(match, "p1", freeShot, Start + 2 + shot);
        }

        Assert.That(match.first.abilityPoints, Is.Zero);
        Assert.That(match.currentTurnPlayerId, Is.EqualTo("p2"));
        Assert.That(match.first.bonusShotsRemaining, Is.Zero);
    }

    [Test]
    public void InvalidCoordinates_AreRejectedWithoutChangingTheVersion()
    {
        NavalServerMatch match = Match();
        NavalMatchAction action = Action(match, NavalActionType.NormalShot, -1, 4);
        Assert.That(() => NavalAuthoritativeEngine.SubmitAction(match, "p1", action, Start + 1),
            Throws.TypeOf<NavalRuleException>().With.Property("Code").EqualTo("CELL_OUT_OF_BOUNDS"));
        Assert.That(match.version, Is.EqualTo(1));
    }

    [Test]
    public void RonanFortress_IsValidatedAsSevenByTwoAndRelocatesWithoutRotation()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", RonanLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 5;
        NavalMatchAction move = Action(match, NavalActionType.Ability, 3, 0);
        move.abilityId = NavalAuthoritativeEngine.FleetRelocation;
        move.sourceRow = 0;
        move.sourceColumn = 0;

        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", move, Start + 1);

        Assert.That(view.ownAbilityPoints, Is.Zero);
        Assert.That(match.first.board[0, 0], Is.EqualTo(-1));
        Assert.That(match.first.board[3, 0], Is.EqualTo(0));
        Assert.That(match.first.board[4, 6], Is.EqualTo(0));
        Assert.That(match.first.ships[0].vertical, Is.False);
    }

    [Test]
    public void NuclearStrike_SinksTheWholeHitShip()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", RonanLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 7;
        NavalMatchAction nuke = Action(match, NavalActionType.Ability, 0, 0);
        nuke.abilityId = NavalAuthoritativeEngine.NuclearStrike;

        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", nuke, Start + 1);

        Assert.That(match.second.ships[0].IsSunk, Is.True);
        Assert.That(view.opponentBoard.Count(cell => cell.sunk && cell.hit), Is.EqualTo(5));
        Assert.That(view.lastEvent, Does.Contain("AUSGELÖSCHT"));
    }

    [Test]
    public void AndreasDev_UsesAllAbilitiesForZeroPoints()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "andreas_dev", RonanLoadout(), "p2", "TWO", StandardLoadout(), Start);
        NavalMatchAction nuke = Action(match, NavalActionType.Ability, 9, 9);
        nuke.abilityId = NavalAuthoritativeEngine.NuclearStrike;
        nuke.developerFreeAbilities = true;

        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", nuke, Start + 1);

        Assert.That(view.ownAbilityPoints, Is.Zero);
        Assert.That(view.lastEvent, Does.Contain("KEIN KONTAKT"));
    }

    [Test]
    public void AndreasDev_PaysNormalAbilityCostWhenAdminOverrideIsDisabled()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "andreas_dev", RonanLoadout(), "p2", "TWO", StandardLoadout(), Start);
        NavalMatchAction nuke = Action(match, NavalActionType.Ability, 9, 9);
        nuke.abilityId = NavalAuthoritativeEngine.NuclearStrike;

        Assert.Throws<NavalRuleException>(() =>
            NavalAuthoritativeEngine.SubmitAction(match, "p1", nuke, Start + 1));
    }

    [Test]
    public void VectorRangefinder_FiresOneShotAndReportsOnlyNearestDistance()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", ArjanLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 5;
        NavalMatchAction action = Action(match, NavalActionType.Ability, 9, 9);
        action.abilityId = NavalAuthoritativeEngine.RangefinderShot;

        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", action, Start + 1);

        Assert.That(view.ownAbilityPoints, Is.Zero);
        Assert.That(view.opponentBoard.Count(cell => cell.shot), Is.EqualTo(1));
        Assert.That(view.lastEvent, Is.EqualTo("VECTOR // DISTANZ 13 FELDER"));
    }

    [Test]
    public void Berserker_FiresSixUniqueUntargetedCellsWithOneCost()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", ArjanLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 8;
        NavalMatchAction action = Action(match, NavalActionType.Ability, -1, -1);
        action.abilityId = NavalAuthoritativeEngine.BerserkerBarrage;

        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", action, Start + 1);

        Assert.That(view.ownAbilityPoints, Is.Zero);
        Assert.That(view.opponentBoard.Count(cell => cell.shot), Is.EqualTo(6));
        string pattern = view.lastEvent.Substring(view.lastEvent.IndexOf("//") + 2).Trim();
        Assert.That(pattern.Length, Is.EqualTo(6));
        Assert.That(pattern.All(character => character == 'H' || character == 'M'), Is.True);
    }

    [Test]
    public void AbyssTorpedo_FiresEveryUntargetedCellInSelectedRowWithOneCost()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", MateoLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 14;
        NavalMatchAction torpedo = Action(match, NavalActionType.Ability, 0, 4);
        torpedo.abilityId = NavalAuthoritativeEngine.AbyssTorpedo;

        NavalPlayerMatchView view = NavalAuthoritativeEngine.SubmitAction(match, "p1", torpedo, Start + 1);

        Assert.That(view.ownAbilityPoints, Is.Zero);
        Assert.That(view.opponentBoard.Count(cell => cell.row == 0 && cell.shot), Is.EqualTo(10));
        Assert.That(view.opponentBoard.Count(cell => cell.row == 0 && cell.hit), Is.EqualTo(5));
        Assert.That(match.second.ships[0].IsSunk, Is.True);
        Assert.That(view.lastEvent, Does.Contain("5 TREFFER"));
        Assert.That(view.currentTurnPlayerId, Is.EqualTo("p2"));
    }

    [Test]
    public void AbyssTorpedo_RejectsAFullyTargetedRowWithoutSpendingPoints()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", MateoLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 14;
        for (int column = 0; column < NavalOnlineProtocol.BoardSize; column++)
            match.second.shotsReceived[9, column] = true;
        NavalMatchAction torpedo = Action(match, NavalActionType.Ability, 9, 0);
        torpedo.abilityId = NavalAuthoritativeEngine.AbyssTorpedo;

        Assert.That(() => NavalAuthoritativeEngine.SubmitAction(match, "p1", torpedo, Start + 1),
            Throws.TypeOf<NavalRuleException>().With.Property("Code").EqualTo("ROW_ALREADY_TARGETED"));
        Assert.That(match.first.abilityPoints, Is.EqualTo(14));
        Assert.That(match.version, Is.EqualTo(1));
    }

    [Test]
    public void AbyssSubmerge_BlocksOneEnemyTurnWithoutLeakingOrDamagingTheShip()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", MateoLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 5;
        NavalMatchAction submerge = Action(match, NavalActionType.Ability, 0, 0);
        submerge.abilityId = NavalAuthoritativeEngine.AbyssSubmerge;
        NavalAuthoritativeEngine.SubmitAction(match, "p1", submerge, Start + 1);

        NavalPlayerMatchView blockedView = NavalAuthoritativeEngine.SubmitAction(
            match, "p2", Action(match, NavalActionType.NormalShot, 0, 0), Start + 2);
        NavalCellView blockedCell = blockedView.opponentBoard.Single(cell => cell.row == 0 && cell.column == 0);
        Assert.That(blockedCell.shot, Is.True);
        Assert.That(blockedCell.blocked, Is.True);
        Assert.That(blockedCell.hit, Is.False);
        Assert.That(blockedCell.ship, Is.False);
        Assert.That(match.first.ships[0].hits, Is.Zero);
        Assert.That(match.first.submergedShipIndex, Is.EqualTo(-1));

        NavalAuthoritativeEngine.SubmitAction(match, "p1", Action(match, NavalActionType.NormalShot, 9, 9), Start + 3);
        NavalAuthoritativeEngine.SubmitAction(match, "p2", Action(match, NavalActionType.NormalShot, 0, 1), Start + 4);
        Assert.That(match.first.ships[0].hits, Is.EqualTo(1));
    }

    [Test]
    public void RaptorJetStart_IsOneTimeHiddenMovesAfterMissAndMustBeDestroyed()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", ImaniLoadout(), "p2", "TWO", StandardLoadout(), Start);
        match.first.abilityPoints = 16;
        NavalMatchAction launch = Action(match, NavalActionType.Ability, -1, -1);
        launch.abilityId = NavalAuthoritativeEngine.RaptorJetStart;

        NavalPlayerMatchView ownerView = NavalAuthoritativeEngine.SubmitAction(match, "p1", launch, Start + 1);
        Assert.That(ownerView.ownAbilityPoints, Is.EqualTo(8));
        Assert.That(ownerView.ownJetLaunched, Is.True);
        Assert.That(ownerView.ownJetActive, Is.True);
        Assert.That(ownerView.ownBoard.Count(cell => cell.jet), Is.EqualTo(1));
        NavalPlayerMatchView enemyView = NavalAuthoritativeEngine.BuildView(match, "p2");
        Assert.That(enemyView.opponentBoard.Any(cell => cell.jet), Is.False);

        int oldRow = match.first.jetRow;
        int oldColumn = match.first.jetColumn;
        NavalCellView miss = enemyView.opponentBoard.First(cell => !cell.ship && !cell.shot &&
            (cell.row != oldRow || cell.column != oldColumn));
        NavalAuthoritativeEngine.SubmitAction(
            match, "p2", Action(match, NavalActionType.NormalShot, miss.row, miss.column), Start + 2);
        Assert.That(match.first.jetActive, Is.True);
        Assert.That(match.first.jetRow != oldRow || match.first.jetColumn != oldColumn, Is.True);

        NavalAuthoritativeEngine.SubmitAction(
            match, "p1", Action(match, NavalActionType.NormalShot, 9, 9), Start + 3);
        foreach (NavalServerShip ship in match.first.ships) ship.hits = ship.length;
        int jetRow = match.first.jetRow;
        int jetColumn = match.first.jetColumn;
        NavalPlayerMatchView destroyed = NavalAuthoritativeEngine.SubmitAction(
            match, "p2", Action(match, NavalActionType.NormalShot, jetRow, jetColumn), Start + 4);
        Assert.That(match.first.jetActive, Is.False);
        Assert.That(destroyed.status, Is.EqualTo(NavalMatchStatus.Finished));
        Assert.That(destroyed.lastEvent, Does.Contain("FLOTTE ZERSTÖRT"));
    }

    [Test]
    public void RaptorJammer_BlocksAbilityWithoutSpendingThenNormalShotConsumesIt()
    {
        NavalServerMatch match = NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Friendly, "p1", "ONE", ImaniLoadout(), "p2", "TWO", EliasLoadout(), Start);
        match.first.abilityPoints = 3;
        match.second.abilityPoints = 8;
        NavalMatchAction jammer = Action(match, NavalActionType.Ability, -1, -1);
        jammer.abilityId = NavalAuthoritativeEngine.RaptorJammer;
        NavalAuthoritativeEngine.SubmitAction(match, "p1", jammer, Start + 1);

        NavalMatchAction blockedAbility = Action(match, NavalActionType.Ability, 5, 5);
        blockedAbility.abilityId = NavalAuthoritativeEngine.OracleScan;
        Assert.That(() => NavalAuthoritativeEngine.SubmitAction(match, "p2", blockedAbility, Start + 2),
            Throws.TypeOf<NavalRuleException>().With.Property("Code").EqualTo("ABILITIES_JAMMED"));
        Assert.That(match.second.abilityPoints, Is.EqualTo(8));
        Assert.That(match.second.abilitiesJammed, Is.True);

        NavalAuthoritativeEngine.SubmitAction(
            match, "p2", Action(match, NavalActionType.NormalShot, 9, 9), Start + 3);
        Assert.That(match.second.abilitiesJammed, Is.False);
    }

    private static NavalServerMatch Match()
    {
        return NavalAuthoritativeEngine.CreateMatch(
            "m1", NavalMatchMode.Ranked, "p1", "ONE", StandardLoadout(), "p2", "TWO", StandardLoadout(), Start);
    }

    private static NavalMatchAction Action(NavalServerMatch match, NavalActionType type, int row, int column)
    {
        NavalMatchAction action = NavalMatchAction.Create(match.matchId, match.version, type);
        action.row = row;
        action.column = column;
        return action;
    }

    private static NavalPendingLoadout StandardLoadout()
    {
        return Loadout("standard-commander", new[] { 5, 4, 3, 3, 2 });
    }

    private static NavalPendingLoadout DaeLoadout()
    {
        return Loadout("dae-hyun-kwon", new[] { 4, 4, 2, 2 });
    }

    private static NavalPendingLoadout EliasLoadout()
    {
        return Loadout("elias-voss", new[] { 5, 3, 2 });
    }

    private static NavalPendingLoadout RonanLoadout()
    {
        NavalPendingLoadout loadout = new NavalPendingLoadout { commanderId = "ronan-graves" };
        loadout.ships.Add(new NavalShipPlacement
        {
            length = 14, width = 7, height = 2, row = 0, column = 0, vertical = false
        });
        loadout.ships.Add(new NavalShipPlacement
        {
            length = 2, width = 2, height = 1, row = 2, column = 0, vertical = false
        });
        return loadout;
    }

    private static NavalPendingLoadout ArjanLoadout()
    {
        NavalPendingLoadout loadout = new NavalPendingLoadout { commanderId = "arjan-dhillon" };
        loadout.ships.Add(new NavalShipPlacement
        {
            length = 6, width = 3, height = 2, row = 0, column = 0, vertical = false
        });
        loadout.ships.Add(new NavalShipPlacement
        {
            length = 4, width = 4, height = 1, row = 2, column = 0, vertical = false
        });
        loadout.ships.Add(new NavalShipPlacement
        {
            length = 3, width = 3, height = 1, row = 3, column = 0, vertical = false
        });
        return loadout;
    }

    private static NavalPendingLoadout MateoLoadout()
    {
        return Loadout("mateo-serrano", new[] { 5, 3, 3 });
    }

    private static NavalPendingLoadout ImaniLoadout()
    {
        NavalPendingLoadout loadout = new NavalPendingLoadout { commanderId = "imani-cross" };
        loadout.ships.Add(new NavalShipPlacement
        {
            length = 5, width = 5, height = 1, row = 0, column = 0, vertical = false
        });
        loadout.ships.Add(new NavalShipPlacement
        {
            length = 6, width = 3, height = 2, row = 2, column = 0, vertical = false
        });
        loadout.ships.Add(new NavalShipPlacement
        {
            length = 2, width = 2, height = 1, row = 4, column = 0, vertical = false
        });
        return loadout;
    }

    private static NavalPendingLoadout Loadout(string commanderId, int[] lengths)
    {
        NavalPendingLoadout loadout = new NavalPendingLoadout { commanderId = commanderId };
        for (int index = 0; index < lengths.Length; index++)
        {
            loadout.ships.Add(new NavalShipPlacement
            {
                length = lengths[index],
                row = index,
                column = 0,
                vertical = false
            });
        }
        return loadout;
    }
}
