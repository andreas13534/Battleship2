using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private Button surrenderButton;
    private bool onlineActionPending;
    private bool timeoutClaimPending;
    private long nextTimeoutClaimUnixMs;
    private long surrenderArmedUntilUnixMs;
    private string observedOnlineMatchId;
    private int observedOnlineVersion = -1;
    private string observedOnlineTurnPlayerId;
    private bool[,] observedOnlineOwnShots;

    private bool IsActiveOnlineMatch => onlineMatchView != null &&
        (onlineMatchView.status == NavalMatchStatus.InProgress || onlineMatchView.status == NavalMatchStatus.Finished);

    private void CacheOnlineBattleUi(VisualElement root)
    {
        surrenderButton = root.Q<Button>("SurrenderButton");
    }

    private void BindOnlineBattleUi()
    {
        surrenderButton.clicked += ArmOrConfirmSurrender;
    }

    private void EnterOnlineBattle(NavalPlayerMatchView view)
    {
        if (view == null) return;
        onlineMatchView = view;
        for (int index = 0; index < commanderCatalog.Length; index++)
        {
            if (commanderCatalog[index].id != view.ownCommanderId) continue;
            currentCommander = commanderCatalog[index];
            ApplyCommanderFleet();
            break;
        }

        playerShots = new bool[BoardSize, BoardSize];
        enemyShots = new bool[BoardSize, BoardSize];
        enemyBoard = CreateEmptyBoard();
        enemyShips = CreateShipStates();
        enemyScanWater = new bool[BoardSize, BoardSize];
        enemyRangefinderArea = new bool[BoardSize, BoardSize];
        playerMines = new bool[BoardSize, BoardSize];
        enemyRevealedShips = new bool[enemyShips.Length];
        lastRangefinderDistance = -1;
        lastRangefinderRow = -1;
        lastRangefinderColumn = -1;
        lastRangefinderHit = false;
        lastBarrageShots.Clear();
        activeAbility = AbilityId.None;
        viewingOwnBoard = false;
        onlineActionPending = false;
        timeoutClaimPending = false;
        BuildBattleGrids();
        UpdateCommanderPanel();
        ShowOnly(battleScreen);
        ResetOnlineOpponentActionTracking(view);
        RefreshOnlineBattle(view);
    }

    private void RefreshOnlineBattle(NavalPlayerMatchView view)
    {
        if (view == null) return;
        if (onlineMatchView != null && onlineMatchView.matchId == view.matchId && view.version < onlineMatchView.version) return;
        ObserveOnlineOpponentAction(view);
        onlineMatchView = view;
        if (mainCells == null || mainCells.Length == 0) return;

        SyncOnlineFleet(view);
        bonusShotsRemaining = view.ownBonusShotsRemaining;
        points = view.ownAbilityPoints;
        playerTurn = view.IsOwnTurn && !onlineActionPending;
        gameOver = view.status == NavalMatchStatus.Finished;
        SyncOwnRaptorJet(view);
        UpdateOwnShipDamage(view);
        pointsLabel.text = points.ToString("00");
        RenderOnlineMainBoard();
        RenderOnlineMiniBoard();
        DrawFleetStatus();
        surrenderButton.EnableInClassList("hidden", gameOver);
        battleMessage.text = string.IsNullOrWhiteSpace(view.lastEvent) ? "SICHERE VERBINDUNG" : view.lastEvent;

        if (gameOver)
        {
            bool won = view.winnerPlayerId == view.ownPlayerId;
            playerTurn = false;
            turnDot.EnableInClassList("turn-player", won);
            turnDot.EnableInClassList("turn-enemy", !won);
            turnLabel.text = won ? "MISSION ERFOLGREICH" : "FLOTTE VERLOREN";
            battleMessage.text = won ? "ONLINE-SIEG BESTÄTIGT" : "ONLINE-NIEDERLAGE BESTÄTIGT";
        }
        else
        {
            UpdateOnlineClock();
        }
        UpdateAbilityButtons();
    }

    private void SyncOwnRaptorJet(NavalPlayerMatchView view)
    {
        raptorJetLaunched = view.ownJetLaunched;
        raptorJetActive = view.ownJetActive;
        raptorJetRow = -1;
        raptorJetColumn = -1;
        if (!raptorJetActive || view.ownBoard == null) return;
        for (int index = 0; index < view.ownBoard.Count; index++)
        {
            NavalCellView cell = view.ownBoard[index];
            if (!cell.jet) continue;
            raptorJetRow = cell.row;
            raptorJetColumn = cell.column;
            break;
        }
    }

    private void ResetOnlineOpponentActionTracking(NavalPlayerMatchView view)
    {
        observedOnlineMatchId = view?.matchId;
        observedOnlineVersion = view?.version ?? -1;
        observedOnlineTurnPlayerId = view?.currentTurnPlayerId;
        observedOnlineOwnShots = new bool[BoardSize, BoardSize];
        if (view?.ownBoard == null) return;

        for (int row = 0; row < BoardSize; row++)
        for (int column = 0; column < BoardSize; column++)
        {
            NavalCellView cell = CellAt(view.ownBoard, row, column);
            observedOnlineOwnShots[row, column] = cell != null && cell.shot;
        }
    }

    private void ObserveOnlineOpponentAction(NavalPlayerMatchView view)
    {
        if (view == null) return;
        if (observedOnlineOwnShots == null || observedOnlineMatchId != view.matchId)
        {
            ResetOnlineOpponentActionTracking(view);
            return;
        }
        if (view.version <= observedOnlineVersion) return;

        bool opponentActed = !string.IsNullOrWhiteSpace(observedOnlineTurnPlayerId) &&
                             observedOnlineTurnPlayerId != view.ownPlayerId;
        int incomingRow = -1;
        int incomingColumn = -1;
        bool incomingHit = false;
        bool[] incomingResolvedColumns = new bool[BoardSize];
        bool[] incomingHitColumns = new bool[BoardSize];

        for (int row = 0; row < BoardSize; row++)
        for (int column = 0; column < BoardSize; column++)
        {
            NavalCellView cell = CellAt(view.ownBoard, row, column);
            bool shot = cell != null && cell.shot;
            if (shot && !observedOnlineOwnShots[row, column] && incomingRow < 0)
            {
                incomingRow = row;
                incomingColumn = column;
                incomingHit = cell.hit;
            }
            if (shot && !observedOnlineOwnShots[row, column] && (incomingRow < 0 || row == incomingRow))
            {
                incomingResolvedColumns[column] = true;
                incomingHitColumns[column] = cell.hit;
            }
            observedOnlineOwnShots[row, column] = shot;
        }

        observedOnlineVersion = view.version;
        observedOnlineTurnPlayerId = view.currentTurnPlayerId;

        if (!opponentActed || shotCinematicPlaying || abilityCinematicPlaying) return;
        AbilityData opponentAbility = FindOpponentAbilityForEvent(view.opponentCommanderId, view.lastEvent);
        if (opponentAbility != null)
        {
            if (opponentAbility.id == AbilityId.AbyssTorpedo)
                SetLastTorpedoAnimationColumns(incomingResolvedColumns, incomingHitColumns);
            PrepareVectorAnimationResult(opponentAbility, view.lastEvent, BoardSize / 2, BoardSize / 2, false);
            StartCoroutine(PlayOnlineOpponentAbilityCinematic(opponentAbility, view.lastEvent));
            return;
        }
        if (incomingRow >= 0)
        {
            bool jetHit = !string.IsNullOrWhiteSpace(view.lastEvent) && view.lastEvent.Contains("JET ABGESCHOSSEN");
            ConfigureNextShotJet(raptorJetActive || jetHit, jetHit);
            StartCoroutine(PlayEnemyNormalShotCinematic(incomingHit, incomingRow, incomingColumn));
            return;
        }

    }

    private AbilityData FindOpponentAbilityForEvent(string commanderId, string lastEvent)
    {
        if (string.IsNullOrWhiteSpace(lastEvent)) return null;
        AbilityId id = AbilityId.None;
        if (lastEvent.Contains("ORACLE")) id = AbilityId.OracleScan;
        else if (lastEvent.Contains("ARES")) id = AbilityId.AresRepair;
        else if (lastEvent.Contains("CHEMIEBOMBE")) id = AbilityId.ChemicalBomb;
        else if (lastEvent.Contains("MINE GELEGT")) id = AbilityId.MineLayer;
        else if (lastEvent.Contains("TITAN")) id = AbilityId.FleetRelocation;
        else if (lastEvent.Contains("ATOMBOMBE")) id = AbilityId.NuclearStrike;
        else if (lastEvent.Contains("VECTOR")) id = AbilityId.RangefinderShot;
        else if (lastEvent.Contains("AMOKLAUF")) id = AbilityId.BerserkerBarrage;
        else if (lastEvent.Contains("TORPEDO")) id = AbilityId.AbyssTorpedo;
        else if (lastEvent.Contains("UNTERGETAUCHT")) id = AbilityId.AbyssSubmerge;
        else if (lastEvent.Contains("JET IN DER LUFT")) id = AbilityId.RaptorJetStart;
        if (id == AbilityId.None) return null;

        for (int commanderIndex = 0; commanderIndex < commanderCatalog.Length; commanderIndex++)
        {
            CommanderData commander = commanderCatalog[commanderIndex];
            if (commander.id != commanderId || commander.abilities == null) continue;
            for (int abilityIndex = 0; abilityIndex < commander.abilities.Length; abilityIndex++)
                if (commander.abilities[abilityIndex].id == id) return commander.abilities[abilityIndex];
        }
        return null;
    }

    private IEnumerator PlayOnlineOpponentAbilityCinematic(AbilityData ability, string result)
    {
        abilityCinematicPlaying = true;
        UpdateAbilityButtons();
        yield return StartCoroutine(PlayAbilityFx(ability, result ?? string.Empty, BoardSize / 2, BoardSize / 2));
        abilityCinematicPlaying = false;
        if (onlineMatchView != null) RefreshOnlineBattle(onlineMatchView);
    }

    private async Task SubmitOnlineTargetAsync(int row, int column)
    {
        if (!IsActiveOnlineMatch || onlineMatchView.status != NavalMatchStatus.InProgress || onlineActionPending) return;
        if (!onlineMatchView.IsOwnTurn)
        {
            battleMessage.text = "GEGNER AM ZUG";
            return;
        }

        AbilityData ability = activeAbility == AbilityId.None ? null : GetActiveAbilityData();
        if (ability == null && viewingOwnBoard)
        {
            viewingOwnBoard = false;
            RefreshOnlineBattle(onlineMatchView);
            return;
        }

        if (ability != null && ability.id == AbilityId.FleetRelocation && relocationShipIndex < 0)
        {
            int selected = playerBoard[row, column];
            if (selected < 0)
            {
                battleMessage.text = "HIER LIEGT KEIN SCHIFF";
                return;
            }
            if (playerShips[selected].hits > 0)
            {
                battleMessage.text = "NUR UNBESCHÄDIGTE SCHIFFE KÖNNEN VERSCHOBEN WERDEN";
                return;
            }
            relocationShipIndex = selected;
            lastRelocatedShipClass = shipClasses[selected];
            battleMessage.text = "NEUE POSITION WÄHLEN // AUSRICHTUNG BLEIBT";
            RefreshOnlineBattle(onlineMatchView);
            return;
        }

        NavalMatchAction action;
        if (ability != null)
        {
            if (ability.targetsOwnBoard != viewingOwnBoard)
            {
                viewingOwnBoard = ability.targetsOwnBoard;
                RefreshOnlineBattle(onlineMatchView);
            }
            action = NavalMatchAction.Create(onlineMatchView.matchId, onlineMatchView.version, NavalActionType.Ability);
            action.abilityId = OnlineAbilityId(ability.id);
            if (ability.id == AbilityId.FleetRelocation && relocationShipIndex >= 0)
            {
                action.sourceRow = playerShips[relocationShipIndex].row;
                action.sourceColumn = playerShips[relocationShipIndex].column;
            }
        }
        else
        {
            NavalCellView target = CellAt(onlineMatchView.opponentBoard, row, column);
            if (target != null && target.shot)
            {
                battleMessage.text = "ZIEL BEREITS BESCHOSSEN";
                return;
            }
            action = NavalMatchAction.Create(onlineMatchView.matchId, onlineMatchView.version, NavalActionType.NormalShot);
        }
        action.row = row;
        action.column = column;
        if (ability != null) action.developerFreeAbilities = DeveloperFreeAbilitiesActive();

        bool[] previousTorpedoShots = null;
        bool opponentJetVisible = ability == null && onlineMatchView.opponentJetActive;
        if (ability != null && ability.id == AbilityId.AbyssTorpedo)
        {
            previousTorpedoShots = new bool[BoardSize];
            for (int boardColumn = 0; boardColumn < BoardSize; boardColumn++)
            {
                NavalCellView previousCell = CellAt(onlineMatchView.opponentBoard, row, boardColumn);
                previousTorpedoShots[boardColumn] = previousCell != null && previousCell.shot;
            }
        }

        onlineActionPending = true;
        playerTurn = false;
        battleMessage.text = "BEFEHL WIRD BESTÄTIGT...";
        UpdateAbilityButtons();
        try
        {
            NavalPlayerMatchView result = await onlineService.SubmitActionAsync(action);
            onlineMatchView = result;
            onlineActionPending = false;
            if (ability == null)
            {
                NavalCellView resolved = CellAt(result.opponentBoard, row, column);
                bool jetHit = !string.IsNullOrWhiteSpace(result.lastEvent) && result.lastEvent.Contains("JET ABGESCHOSSEN");
                bool sunk = !string.IsNullOrWhiteSpace(result.lastEvent) && result.lastEvent.Contains("SCHIFF VERSENKT");
                ConfigureNextShotJet(opponentJetVisible || jetHit, jetHit);
                StartNormalShotCinematic(resolved != null && resolved.hit, sunk, row, column);
            }
            else
            {
                if (ability.id == AbilityId.AbyssTorpedo)
                    PrepareOnlineTorpedoAnimation(result.opponentBoard, row, previousTorpedoShots);
                if (ability.id == AbilityId.FleetRelocation && relocationShipIndex >= 0)
                {
                    ShipState movedShip = playerShips[relocationShipIndex];
                    for (int boardRow = 0; boardRow < BoardSize; boardRow++)
                    for (int boardColumn = 0; boardColumn < BoardSize; boardColumn++)
                        if (playerBoard[boardRow, boardColumn] == relocationShipIndex)
                            playerBoard[boardRow, boardColumn] = -1;
                    movedShip.row = row;
                    movedShip.column = column;
                    int rows = movedShip.vertical ? movedShip.width : movedShip.height;
                    int columns = movedShip.vertical ? movedShip.height : movedShip.width;
                    for (int rowOffset = 0; rowOffset < rows; rowOffset++)
                    for (int columnOffset = 0; columnOffset < columns; columnOffset++)
                        playerBoard[row + rowOffset, column + columnOffset] = relocationShipIndex;
                }
                activeAbility = AbilityId.None;
                relocationShipIndex = -1;
                PrepareVectorAnimationResult(ability, result.lastEvent, row, column, true);
                RefreshOnlineBattle(result);
                StartCoroutine(PlayAbilityFxAndFinishTurn(ability, result.lastEvent, row, column));
            }
        }
        catch (Exception exception)
        {
            onlineActionPending = false;
            battleMessage.text = exception.Message.ToUpperInvariant();
            try { await onlineService.GetMatchViewAsync(onlineMatchView.matchId); }
            catch { }
            RefreshOnlineBattle(onlineMatchView);
        }
    }

    private async Task SubmitOnlineAutomaticAbilityAsync(AbilityData ability)
    {
        if (!IsActiveOnlineMatch || onlineMatchView.status != NavalMatchStatus.InProgress || onlineActionPending) return;
        if (!onlineMatchView.IsOwnTurn)
        {
            battleMessage.text = "GEGNER AM ZUG";
            return;
        }

        NavalMatchAction action = NavalMatchAction.Create(onlineMatchView.matchId, onlineMatchView.version, NavalActionType.Ability);
        action.abilityId = OnlineAbilityId(ability.id);
        action.developerFreeAbilities = DeveloperFreeAbilitiesActive();
        onlineActionPending = true;
        playerTurn = false;
        battleMessage.text = "BEFEHL WIRD BESTÄTIGT...";
        UpdateAbilityButtons();
        try
        {
            NavalPlayerMatchView result = await onlineService.SubmitActionAsync(action);
            onlineMatchView = result;
            onlineActionPending = false;
            activeAbility = AbilityId.None;
            PrepareVectorAnimationResult(ability, result.lastEvent, -1, -1, true);
            RefreshOnlineBattle(result);
            if (ability.id != AbilityId.RaptorJammer)
                StartCoroutine(PlayAbilityFxAndFinishTurn(ability, result.lastEvent, -1, -1));
        }
        catch (Exception exception)
        {
            onlineActionPending = false;
            battleMessage.text = exception.Message.ToUpperInvariant();
            RefreshOnlineBattle(onlineMatchView);
        }
    }

    private void PrepareVectorAnimationResult(AbilityData ability, string result, int row, int column, bool markRange)
    {
        if (ability.id == AbilityId.RangefinderShot)
        {
            if (markRange && row >= 0 && column >= 0)
            {
                NavalCellView resolved = CellAt(onlineMatchView?.opponentBoard, row, column);
                lastRangefinderHit = resolved != null && resolved.hit;
            }
            else
            {
                lastRangefinderHit = false;
            }
            const string marker = "DISTANZ ";
            int markerIndex = (result ?? string.Empty).IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                markerIndex += marker.Length;
                int end = markerIndex;
                while (end < result.Length && char.IsDigit(result[end])) end++;
                if (int.TryParse(result.Substring(markerIndex, end - markerIndex), out int distance) && markRange)
                    SetRangefinderArea(row, column, distance);
                else if (int.TryParse(result.Substring(markerIndex, end - markerIndex), out distance))
                    lastRangefinderDistance = distance;
            }
        }
        else if (ability.id == AbilityId.BerserkerBarrage)
        {
            lastBarrageShots.Clear();
            int divider = (result ?? string.Empty).IndexOf("//", StringComparison.Ordinal);
            string pattern = divider >= 0 ? result.Substring(divider + 2).Trim() : string.Empty;
            for (int index = 0; index < pattern.Length; index++)
                if (pattern[index] == 'H' || pattern[index] == 'M' || pattern[index] == 'B')
                    lastBarrageShots.Add(new BarrageShotResult { row = -1, column = -1, hit = pattern[index] == 'H' });
        }
    }

    private void PrepareOnlineTorpedoAnimation(List<NavalCellView> board, int row, bool[] previousShots)
    {
        bool[] resolvedColumns = new bool[BoardSize];
        bool[] hitColumns = new bool[BoardSize];
        for (int column = 0; column < BoardSize; column++)
        {
            NavalCellView cell = CellAt(board, row, column);
            bool wasShot = previousShots != null && previousShots[column];
            resolvedColumns[column] = cell != null && cell.shot && !wasShot;
            hitColumns[column] = resolvedColumns[column] && cell.hit;
        }
        SetLastTorpedoAnimationColumns(resolvedColumns, hitColumns);
    }

    private void SetLastTorpedoAnimationColumns(bool[] resolvedColumns, bool[] hitColumns)
    {
        Array.Clear(lastTorpedoResolvedColumns, 0, lastTorpedoResolvedColumns.Length);
        Array.Clear(lastTorpedoHitColumns, 0, lastTorpedoHitColumns.Length);
        if (resolvedColumns == null || hitColumns == null) return;
        for (int column = 0; column < BoardSize; column++)
        {
            lastTorpedoResolvedColumns[column] = column < resolvedColumns.Length && resolvedColumns[column];
            lastTorpedoHitColumns[column] = column < hitColumns.Length && hitColumns[column];
        }
    }

    private void RenderOnlineMainBoard()
    {
        RemoveShipOverlays(mainBattleGrid);
        mainBoardCaption.text = viewingOwnBoard ? "EIGENES RASTER // VERTEIDIGUNG" : "ZIELRASTER // GEGNER";
        List<NavalCellView> cells = viewingOwnBoard ? onlineMatchView.ownBoard : onlineMatchView.opponentBoard;
        for (int row = 0; row < BoardSize; row++)
        for (int column = 0; column < BoardSize; column++)
        {
            Button button = mainCells[row, column];
            ResetBattleCell(button);
            NavalCellView cell = CellAt(cells, row, column);
            if (cell == null) continue;
            if (viewingOwnBoard && cell.ship) button.AddToClassList("cell-ship");
            ApplyOnlineCellVisual(button, cell, viewingOwnBoard);
            if (!viewingOwnBoard && enemyRangefinderArea != null && enemyRangefinderArea[row, column])
            {
                button.AddToClassList("cell-rangefinder-area");
                int minRow = Mathf.Max(0, lastRangefinderRow - lastRangefinderDistance);
                int maxRow = Mathf.Min(BoardSize - 1, lastRangefinderRow + lastRangefinderDistance);
                int minColumn = Mathf.Max(0, lastRangefinderColumn - lastRangefinderDistance);
                int maxColumn = Mathf.Min(BoardSize - 1, lastRangefinderColumn + lastRangefinderDistance);
                if (row == minRow || row == maxRow || column == minColumn || column == maxColumn)
                    button.AddToClassList("cell-rangefinder-edge");
            }
        }

        if (viewingOwnBoard)
        {
            for (int index = 0; index < playerShips.Length; index++)
                AddShipOverlay(mainBattleGrid, playerShips[index], shipClasses[index]);
            AddRaptorJetOverlay(mainBattleGrid);
        }
    }

    private void RenderOnlineMiniBoard()
    {
        RemoveShipOverlays(playerMiniGrid);
        for (int row = 0; row < BoardSize; row++)
        for (int column = 0; column < BoardSize; column++)
        {
            Button button = miniCells[row, column];
            ResetBattleCell(button);
            NavalCellView cell = CellAt(onlineMatchView.ownBoard, row, column);
            if (cell == null) continue;
            if (cell.ship) button.AddToClassList("cell-ship");
            ApplyOnlineCellVisual(button, cell, true);
        }
        for (int index = 0; index < playerShips.Length; index++)
            AddShipOverlay(playerMiniGrid, playerShips[index], shipClasses[index]);
        AddRaptorJetOverlay(playerMiniGrid);
    }

    private static void ApplyOnlineCellVisual(Button button, NavalCellView cell, bool ownBoard)
    {
        if (cell.shot)
        {
            button.AddToClassList(cell.blocked ? "cell-shot-blocked" : cell.hit ? "cell-hit" : "cell-miss");
            button.text = cell.blocked ? "≋" : "●";
            if (cell.sunk && !cell.blocked)
            {
                button.AddToClassList("cell-sunk");
                button.text = "●";
            }
        }
        if (!ownBoard && cell.scannedWater)
        {
            button.AddToClassList("cell-scanned");
            button.text = "·";
        }
        if (!ownBoard && cell.revealedContact)
            button.AddToClassList("cell-revealed");
        if (ownBoard && cell.mine && !cell.shot)
        {
            button.AddToClassList("cell-mine");
            button.text = "◆";
        }
    }

    private void UpdateOwnShipDamage(NavalPlayerMatchView view)
    {
        if (playerShips == null) return;
        for (int shipIndex = 0; shipIndex < playerShips.Length; shipIndex++)
        {
            ShipState ship = playerShips[shipIndex];
            ship.hits = 0;
            int rows = ship.vertical ? ship.width : ship.height;
            int columns = ship.vertical ? ship.height : ship.width;
            for (int rowOffset = 0; rowOffset < rows; rowOffset++)
            {
                for (int columnOffset = 0; columnOffset < columns; columnOffset++)
                {
                    NavalCellView cell = CellAt(view.ownBoard, ship.row + rowOffset, ship.column + columnOffset);
                    if (cell != null && cell.hit) ship.hits++;
                }
            }
        }
    }

    private void SyncOnlineFleet(NavalPlayerMatchView view)
    {
        if (view.ownShips == null || view.ownShips.Count == 0) return;
        if (playerShips == null || playerShips.Length != view.ownShips.Count)
            playerShips = new ShipState[view.ownShips.Count];
        playerBoard = CreateEmptyBoard();
        for (int index = 0; index < view.ownShips.Count; index++)
        {
            NavalShipPlacement placement = view.ownShips[index];
            ShipState ship = playerShips[index] ?? (playerShips[index] = new ShipState());
            ship.length = placement.length;
            ship.width = placement.width;
            ship.height = placement.height;
            ship.row = placement.row;
            ship.column = placement.column;
            ship.vertical = placement.vertical;
            ship.placed = true;
            int rows = ship.vertical ? ship.width : ship.height;
            int columns = ship.vertical ? ship.height : ship.width;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
                playerBoard[ship.row + row, ship.column + column] = index;
        }
    }

    private void Update()
    {
        UpdateOnlineMatchmaking();
        UpdateOnlineRefresh();
        if (IsActiveOnlineMatch && onlineMatchView.status == NavalMatchStatus.InProgress)
            UpdateOnlineClock();
    }

    private void UpdateOnlineClock()
    {
        if (onlineMatchView == null || onlineMatchView.status != NavalMatchStatus.InProgress) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int seconds = Mathf.Max(0, Mathf.CeilToInt((onlineMatchView.turnDeadlineUnixMs - now) / 1000f));
        bool ownTurn = onlineMatchView.IsOwnTurn;
        turnDot.EnableInClassList("turn-player", ownTurn);
        turnDot.EnableInClassList("turn-enemy", !ownTurn);
        turnLabel.text = (ownTurn ? "DU // " : "GEGNER // ") + seconds.ToString("00") + "s";
        if (seconds == 0 && !timeoutClaimPending && now >= nextTimeoutClaimUnixMs)
            _ = ClaimOnlineTimeoutAsync();
    }

    private async Task ClaimOnlineTimeoutAsync()
    {
        timeoutClaimPending = true;
        try { await onlineService.ClaimTimeoutAsync(onlineMatchView.matchId, onlineMatchView.version); }
        catch { nextTimeoutClaimUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 2000; }
        finally { timeoutClaimPending = false; }
    }

    private void ArmOrConfirmSurrender()
    {
        if (!IsActiveOnlineMatch || onlineMatchView.status != NavalMatchStatus.InProgress) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now > surrenderArmedUntilUnixMs)
        {
            surrenderArmedUntilUnixMs = now + 3000;
            surrenderButton.text = "BESTÄTIGEN";
            battleMessage.text = "NOCHMAL TIPPEN // MATCH AUFGEBEN";
            return;
        }
        surrenderButton.text = "AUFGEBEN";
        surrenderArmedUntilUnixMs = 0;
        _ = SurrenderOnlineAsync();
    }

    private async Task SurrenderOnlineAsync()
    {
        if (onlineActionPending) return;
        onlineActionPending = true;
        try { await onlineService.SurrenderAsync(onlineMatchView.matchId, onlineMatchView.version); }
        catch (Exception exception) { battleMessage.text = exception.Message.ToUpperInvariant(); }
        finally { onlineActionPending = false; }
    }

    private void HandleBattleBack()
    {
        if (!IsActiveOnlineMatch)
        {
            ShowMenu();
            return;
        }
        if (onlineMatchView.status == NavalMatchStatus.Finished)
        {
            onlineMatchView = null;
            activeMatchTicket = null;
            surrenderButton.EnableInClassList("hidden", true);
            _ = ShowOnlineHubAsync();
            return;
        }
        battleMessage.text = "ONLINE-MATCH LÄUFT // ZUERST AUFGEBEN";
    }

    private void HandleCommanderBack()
    {
        if (IsOnlineSetup) _ = ShowOnlineHubAsync();
        else ShowMenu();
    }

    private static NavalCellView CellAt(List<NavalCellView> cells, int row, int column)
    {
        if (cells == null) return null;
        int index = row * BoardSize + column;
        if (index >= 0 && index < cells.Count)
        {
            NavalCellView candidate = cells[index];
            if (candidate.row == row && candidate.column == column) return candidate;
        }
        for (int i = 0; i < cells.Count; i++)
            if (cells[i].row == row && cells[i].column == column) return cells[i];
        return null;
    }

    private static string OnlineAbilityId(AbilityId id)
    {
        switch (id)
        {
            case AbilityId.OracleScan: return NavalAuthoritativeEngine.OracleScan;
            case AbilityId.AresRepair: return NavalAuthoritativeEngine.AresRepair;
            case AbilityId.ChemicalBomb: return NavalAuthoritativeEngine.ChemicalBomb;
            case AbilityId.MineLayer: return NavalAuthoritativeEngine.MineLayer;
            case AbilityId.FleetRelocation: return NavalAuthoritativeEngine.FleetRelocation;
            case AbilityId.NuclearStrike: return NavalAuthoritativeEngine.NuclearStrike;
            case AbilityId.RangefinderShot: return NavalAuthoritativeEngine.RangefinderShot;
            case AbilityId.BerserkerBarrage: return NavalAuthoritativeEngine.BerserkerBarrage;
            case AbilityId.AbyssTorpedo: return NavalAuthoritativeEngine.AbyssTorpedo;
            case AbilityId.AbyssSubmerge: return NavalAuthoritativeEngine.AbyssSubmerge;
            case AbilityId.RaptorJetStart: return NavalAuthoritativeEngine.RaptorJetStart;
            case AbilityId.RaptorJammer: return NavalAuthoritativeEngine.RaptorJammer;
            default: throw new InvalidOperationException("ONLINE-FÄHIGKEIT NICHT VERFÜGBAR");
        }
    }
}
