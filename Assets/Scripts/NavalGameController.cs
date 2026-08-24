using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController : MonoBehaviour
{
    private const int BoardSize = 10;

    private int[] shipLengths;
    private int[] shipWidths;
    private int[] shipHeights;
    private string[] shipClasses;

    private sealed class ShipState
    {
        public int length;
        public int width;
        public int height;
        public int row;
        public int column;
        public bool vertical;
        public bool placed;
        public int hits;
    }

    private UIDocument uiDocument;
    private VisualElement menuScreen;
    private VisualElement setupScreen;
    private VisualElement battleScreen;
    private VisualElement setupGrid;
    private VisualElement shipTray;
    private VisualElement mainBattleGrid;
    private VisualElement playerMiniGrid;
    private VisualElement fleetStatus;
    private VisualElement miniBoardPanel;
    private VisualElement turnDot;
    private VisualElement sunkEnemyShips;

    private Button startGameButton;
    private Button setupBackButton;
    private Button battleBackButton;
    private Button randomizeFleetButton;
    private Button beginBattleButton;

    private Label setupProgressLabel;
    private Label pointsLabel;
    private Label turnLabel;
    private Label mainBoardCaption;
    private Label battleMessage;

    private Button[,] setupCells;
    private Button[,] mainCells;
    private Button[,] miniCells;

    private int[,] playerBoard;
    private int[,] enemyBoard;
    private bool[,] playerShots;
    private bool[,] enemyShots;
    private ShipState[] playerShips;
    private ShipState[] enemyShips;

    private bool[] shipVertical;
    private VisualElement[] shipCards;

    private int draggedShipIndex = -1;
    private int activePointerId = -1;
    private bool playerTurn;
    private bool viewingOwnBoard;
    private bool gameOver;
    private int points;

private void Start()
    {
        InitializeCinematicAudio();
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("NavalGameController benötigt ein UIDocument auf demselben GameObject.");
            return;
        }

        InitializeCommanderSystem();
        CacheUi();
        BindUi();
        ShowOnlineAppHome();
        InitializeOnlineSystem();
        StartTutorialIfNeeded();
        StartStartupSplash();
    }

private void CacheUi()
    {
        VisualElement root = uiDocument.rootVisualElement;
        CacheStartupSplashUi(root);

        menuScreen = root.Q<VisualElement>("MenuScreen");
        setupScreen = root.Q<VisualElement>("SetupScreen");
        battleScreen = root.Q<VisualElement>("BattleScreen");

        setupGrid = root.Q<VisualElement>("SetupGrid");
        shipTray = root.Q<VisualElement>("ShipTray");
        mainBattleGrid = root.Q<VisualElement>("MainBattleGrid");
        playerMiniGrid = root.Q<VisualElement>("PlayerMiniGrid");
        fleetStatus = root.Q<VisualElement>("FleetStatus");
        miniBoardPanel = root.Q<VisualElement>("MiniBoardPanel");
        turnDot = root.Q<VisualElement>("TurnDot");
        sunkEnemyShips = root.Q<VisualElement>("SunkEnemyShips");

        startGameButton = root.Q<Button>("StartGameButton");
        setupBackButton = root.Q<Button>("SetupBackButton");
        battleBackButton = root.Q<Button>("BattleBackButton");
        randomizeFleetButton = root.Q<Button>("RandomizeFleetButton");
        beginBattleButton = root.Q<Button>("BeginBattleButton");

        setupProgressLabel = root.Q<Label>("SetupProgressLabel");
        pointsLabel = root.Q<Label>("PointsLabel");
        turnLabel = root.Q<Label>("TurnLabel");
        mainBoardCaption = root.Q<Label>("MainBoardCaption");
        battleMessage = root.Q<Label>("BattleMessage");

        CacheCommanderUi(root);
        CacheShotCinematicUi(root);
        CacheOnlineUi(root);
        CacheOnlineBattleUi(root);
        CacheTutorialUi(root);
    }

private void BindUi()
    {
        startGameButton.clicked += delegate
        {
            onlineFlowMode = OnlineFlowMode.None;
            ShowCommanderSelection();
        };
        setupBackButton.clicked += ShowCommanderSelection;
        battleBackButton.clicked += HandleBattleBack;
        randomizeFleetButton.clicked += RandomizePlayerFleet;
        beginBattleButton.clicked += BeginBattle;
        miniBoardPanel.RegisterCallback<ClickEvent>(OnMiniBoardClicked);
        BindCommanderUi();
        BindOnlineUi();
        BindOnlineBattleUi();
        BindTutorialUi();
    }

private void ShowOnly(VisualElement target)
    {
        menuScreen.EnableInClassList("hidden", target != menuScreen);
        commanderScreen.EnableInClassList("hidden", target != commanderScreen);
        setupScreen.EnableInClassList("hidden", target != setupScreen);
        battleScreen.EnableInClassList("hidden", target != battleScreen);
        onlineLoginScreen.EnableInClassList("hidden", target != onlineLoginScreen);
        onlineHubScreen.EnableInClassList("hidden", target != onlineHubScreen);
        matchmakingScreen.EnableInClassList("hidden", target != matchmakingScreen);
        rankedMatchFoundScreen.EnableInClassList("hidden", target != rankedMatchFoundScreen);
    }

    private void OnDestroy()
    {
        DisposeProfilePresentationUi();
        DisposeRankedMatchFoundUi();
        if (onlineService != null)
        {
            onlineService.StateChanged -= RefreshOnlineState;
            onlineService.MatchChanged -= HandleOnlineMatchChanged;
        }
        if (iapService != null)
        {
            iapService.Changed -= RenderStore;
            iapService.Dispose();
        }
        if (rewardedAdService != null)
        {
            rewardedAdService.Changed -= RenderStore;
            rewardedAdService.RewardEarned -= HandleImaniRewardEarned;
            rewardedAdService.Dispose();
        }
    }

private void ShowMenu()
    {
        StopAllCoroutines();
        HideShotCinematicImmediate();
        ClearAbilityCinematicImmediate();
        CancelAbility();
        draggedShipIndex = -1;
        activePointerId = -1;
        ResetOnlineFlowForMenu();
        ShowOnlineAppHome();
    }

    private void ShowSetup()
    {
        StopAllCoroutines();
        ResetPlayerSetup();
        ShowOnly(setupScreen);
    }

private void ResetPlayerSetup()
    {
        playerBoard = CreateEmptyBoard();
        playerShips = CreateShipStates();
        shipVertical = new bool[shipLengths.Length];
        shipCards = new VisualElement[shipLengths.Length];

        BuildSetupGrid();
        BuildShipTray();
        DrawSetupBoard();
        UpdateSetupProgress();
    }

    private int[,] CreateEmptyBoard()
    {
        int[,] board = new int[BoardSize, BoardSize];
        for (int row = 0; row < BoardSize; row++)
        {
            for (int column = 0; column < BoardSize; column++)
            {
                board[row, column] = -1;
            }
        }
        return board;
    }

    private ShipState[] CreateShipStates()
    {
        ShipState[] ships = new ShipState[shipLengths.Length];
        for (int i = 0; i < ships.Length; i++)
        {
            ships[i] = new ShipState
            {
                length = shipLengths[i],
                width = shipWidths[i],
                height = shipHeights[i]
            };
        }
        return ships;
    }

    private void BuildSetupGrid()
    {
        setupGrid.Clear();
        setupCells = new Button[BoardSize, BoardSize];

        for (int row = 0; row < BoardSize; row++)
        {
            for (int column = 0; column < BoardSize; column++)
            {
                int capturedRow = row;
                int capturedColumn = column;
                Button cell = CreateCell(row, column);
                cell.clicked += delegate { RotateShipAt(capturedRow, capturedColumn); };
                setupCells[row, column] = cell;
                setupGrid.Add(cell);
            }
        }
    }

    private Button CreateCell(int row, int column)
    {
        Button cell = new Button();
        cell.text = string.Empty;
        cell.focusable = false;
        cell.AddToClassList("grid-cell");
        cell.style.position = Position.Absolute;
        cell.style.left = Length.Percent(column * 10f);
        cell.style.top = Length.Percent(row * 10f);
        cell.style.width = Length.Percent(10f);
        cell.style.height = Length.Percent(10f);
        return cell;
    }

private void BuildShipTray()
    {
        shipTray.Clear();

        for (int i = 0; i < shipLengths.Length; i++)
        {
            int shipIndex = i;
            VisualElement card = new VisualElement();
            card.AddToClassList("ship-card");
            if (shipLengths[i] >= 5 || shipLengths.Length <= 3)
            {
                card.AddToClassList("ship-card-wide");
            }

            VisualElement image = new VisualElement();
            image.name = "ShipImage";
            image.AddToClassList("ship-image");
            image.AddToClassList(shipClasses[i]);
            card.Add(image);

            Label lengthLabel = new Label("L" + shipLengths[i]);
            lengthLabel.style.position = Position.Absolute;
            lengthLabel.style.right = 8;
            lengthLabel.style.bottom = 5;
            lengthLabel.style.fontSize = 12;
            lengthLabel.style.color = new Color(0.42f, 1f, 0.65f, 0.58f);
            card.Add(lengthLabel);

            card.RegisterCallback<PointerDownEvent>(evt => OnShipPointerDown(evt, shipIndex));
            card.RegisterCallback<PointerMoveEvent>(evt => OnShipPointerMove(evt, shipIndex));
            card.RegisterCallback<PointerUpEvent>(evt => OnShipPointerUp(evt, shipIndex));
            card.RegisterCallback<PointerCaptureOutEvent>(evt => OnShipCaptureLost(shipIndex));

            shipCards[i] = card;
            shipTray.Add(card);
        }
    }

    private void OnShipPointerDown(PointerDownEvent evt, int shipIndex)
    {
        if (playerShips[shipIndex].placed)
        {
            ReturnShipToTray(shipIndex);
            evt.StopPropagation();
            return;
        }

        draggedShipIndex = shipIndex;
        activePointerId = evt.pointerId;
        shipCards[shipIndex].CapturePointer(evt.pointerId);
        shipCards[shipIndex].AddToClassList("ship-card-selected");
        evt.StopPropagation();
    }

    private void OnShipPointerMove(PointerMoveEvent evt, int shipIndex)
    {
        if (draggedShipIndex != shipIndex || activePointerId != evt.pointerId)
        {
            return;
        }

        PreviewPlacement(new Vector2(evt.position.x, evt.position.y), shipIndex);
        evt.StopPropagation();
    }

    private void OnShipPointerUp(PointerUpEvent evt, int shipIndex)
    {
        if (draggedShipIndex != shipIndex || activePointerId != evt.pointerId)
        {
            return;
        }

        Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
        TryDropShip(pointerPosition, shipIndex);

        if (shipCards[shipIndex].HasPointerCapture(evt.pointerId))
        {
            shipCards[shipIndex].ReleasePointer(evt.pointerId);
        }

        FinishShipDrag(shipIndex);
        evt.StopPropagation();
    }

    private void OnShipCaptureLost(int shipIndex)
    {
        if (draggedShipIndex == shipIndex)
        {
            FinishShipDrag(shipIndex);
        }
    }

    private void FinishShipDrag(int shipIndex)
    {
        ClearSetupPreview();
        shipCards[shipIndex].RemoveFromClassList("ship-card-selected");
        draggedShipIndex = -1;
        activePointerId = -1;
    }

    private bool TryDropShip(Vector2 worldPosition, int shipIndex)
    {
        int row;
        int column;
        if (!TryGetSetupCell(worldPosition, out row, out column))
        {
            return false;
        }

        bool vertical = shipVertical[shipIndex];
        if (!CanPlace(playerBoard, playerShips[shipIndex], row, column, vertical))
        {
            return false;
        }

        PlaceShip(playerBoard, playerShips, shipIndex, row, column, vertical);
        shipCards[shipIndex].AddToClassList("ship-card-placed");
        DrawSetupBoard();
        UpdateSetupProgress();
        return true;
    }

    private bool TryGetSetupCell(Vector2 worldPosition, out int row, out int column)
    {
        Vector2 local = setupGrid.WorldToLocal(worldPosition);
        float width = setupGrid.resolvedStyle.width;
        float height = setupGrid.resolvedStyle.height;

        if (width <= 0f || height <= 0f || local.x < 0f || local.y < 0f || local.x >= width || local.y >= height)
        {
            row = -1;
            column = -1;
            return false;
        }

        column = Mathf.FloorToInt(local.x / (width / BoardSize));
        row = Mathf.FloorToInt(local.y / (height / BoardSize));
        return true;
    }

    private void PreviewPlacement(Vector2 worldPosition, int shipIndex)
    {
        ClearSetupPreview();

        int row;
        int column;
        if (!TryGetSetupCell(worldPosition, out row, out column))
        {
            return;
        }

        ShipState ship = playerShips[shipIndex];
        bool vertical = shipVertical[shipIndex];
        bool valid = CanPlace(playerBoard, ship, row, column, vertical);
        string className = valid ? "cell-preview-valid" : "cell-preview-invalid";

        int rows = vertical ? ship.width : ship.height;
        int columns = vertical ? ship.height : ship.width;
        for (int rowOffset = 0; rowOffset < rows; rowOffset++)
        {
            for (int columnOffset = 0; columnOffset < columns; columnOffset++)
            {
                int targetRow = row + rowOffset;
                int targetColumn = column + columnOffset;
                if (targetRow >= 0 && targetRow < BoardSize && targetColumn >= 0 && targetColumn < BoardSize)
                    setupCells[targetRow, targetColumn].AddToClassList(className);
            }
        }
    }

    private void ClearSetupPreview()
    {
        if (setupCells == null)
        {
            return;
        }

        for (int row = 0; row < BoardSize; row++)
        {
            for (int column = 0; column < BoardSize; column++)
            {
                setupCells[row, column].RemoveFromClassList("cell-preview-valid");
                setupCells[row, column].RemoveFromClassList("cell-preview-invalid");
            }
        }
    }

    private bool CanPlace(int[,] board, ShipState ship, int row, int column, bool vertical, int ignoredShipIndex = -1)
    {
        int rows = vertical ? ship.width : ship.height;
        int columns = vertical ? ship.height : ship.width;
        int lastRow = row + rows - 1;
        int lastColumn = column + columns - 1;
        if (row < 0 || column < 0 || lastRow >= BoardSize || lastColumn >= BoardSize)
        {
            return false;
        }

        for (int rowOffset = 0; rowOffset < rows; rowOffset++)
        {
            for (int columnOffset = 0; columnOffset < columns; columnOffset++)
            {
                int occupant = board[row + rowOffset, column + columnOffset];
                if (occupant >= 0 && occupant != ignoredShipIndex)
                    return false;
            }
        }

        return true;
    }

    private void PlaceShip(int[,] board, ShipState[] ships, int shipIndex, int row, int column, bool vertical)
    {
        ShipState ship = ships[shipIndex];
        ship.row = row;
        ship.column = column;
        ship.vertical = vertical;
        ship.placed = true;
        ship.hits = 0;

        int rows = vertical ? ship.width : ship.height;
        int columns = vertical ? ship.height : ship.width;
        for (int rowOffset = 0; rowOffset < rows; rowOffset++)
        {
            for (int columnOffset = 0; columnOffset < columns; columnOffset++)
                board[row + rowOffset, column + columnOffset] = shipIndex;
        }
    }

    private void RotateShipAt(int row, int column)
    {
        if (draggedShipIndex >= 0)
        {
            return;
        }

        int shipIndex = playerBoard[row, column];
        if (shipIndex < 0)
        {
            return;
        }

        ShipState ship = playerShips[shipIndex];
        bool vertical = !ship.vertical;
        if (!CanPlace(playerBoard, ship, ship.row, ship.column, vertical, shipIndex))
        {
            return;
        }

        ClearShipFromBoard(shipIndex);
        PlaceShip(playerBoard, playerShips, shipIndex, ship.row, ship.column, vertical);
        shipVertical[shipIndex] = vertical;
        DrawSetupBoard();
    }

    private void ReturnShipToTray(int shipIndex)
    {
        ClearShipFromBoard(shipIndex);

        playerShips[shipIndex].placed = false;
        playerShips[shipIndex].hits = 0;
        shipCards[shipIndex].RemoveFromClassList("ship-card-placed");
        DrawSetupBoard();
        UpdateSetupProgress();
    }

    private void ClearShipFromBoard(int shipIndex)
    {
        for (int r = 0; r < BoardSize; r++)
        {
            for (int c = 0; c < BoardSize; c++)
            {
                if (playerBoard[r, c] == shipIndex)
                {
                    playerBoard[r, c] = -1;
                }
            }
        }
    }

    private void DrawSetupBoard()
    {
        RemoveShipOverlays(setupGrid);

        for (int row = 0; row < BoardSize; row++)
        {
            for (int column = 0; column < BoardSize; column++)
            {
                Button cell = setupCells[row, column];
                cell.RemoveFromClassList("cell-ship");
                cell.text = string.Empty;
                if (playerBoard[row, column] >= 0)
                {
                    cell.AddToClassList("cell-ship");
                }
            }
        }

        for (int i = 0; i < playerShips.Length; i++)
        {
            if (playerShips[i].placed)
            {
                AddShipOverlay(setupGrid, playerShips[i], shipClasses[i]);
            }
        }
    }

    private void AddShipOverlay(VisualElement grid, ShipState ship, string shipClass, bool sunk = false)
    {
        VisualElement overlay = new VisualElement();
        overlay.pickingMode = PickingMode.Ignore;
        overlay.AddToClassList("ship-board");
        overlay.AddToClassList(shipClass);
        if (sunk)
        {
            overlay.AddToClassList("ship-board-sunk");
        }

        float left = ship.column * 10f;
        float top = ship.row * 10f;
        if (ship.vertical)
        {
            left = (ship.column + ship.height * 0.5f - ship.width * 0.5f) * 10f;
            top = (ship.row + ship.width * 0.5f - ship.height * 0.5f) * 10f;
            overlay.AddToClassList("ship-board-vertical");
        }

        overlay.style.left = new Length(left, LengthUnit.Percent);
        overlay.style.top = new Length(top, LengthUnit.Percent);
        overlay.style.width = new Length(ship.width * 10f, LengthUnit.Percent);
        overlay.style.height = new Length(ship.height * 10f, LengthUnit.Percent);
        grid.Add(overlay);
        if (sunk)
        {
            overlay.SendToBack();
        }
    }

    private void RemoveShipOverlays(VisualElement grid)
    {
        List<VisualElement> overlays = grid.Query<VisualElement>(className: "ship-board").ToList();
        for (int i = 0; i < overlays.Count; i++)
        {
            overlays[i].RemoveFromHierarchy();
        }
    }

    private void UpdateSetupProgress()
    {
        int placed = 0;
        for (int i = 0; i < playerShips.Length; i++)
        {
            if (playerShips[i].placed)
            {
                placed++;
            }
        }

        setupProgressLabel.text = placed + " / " + playerShips.Length + " BEREIT";
        beginBattleButton.SetEnabled(placed == playerShips.Length);
    }

    private void RandomizePlayerFleet()
    {
        playerBoard = CreateEmptyBoard();
        playerShips = CreateShipStates();
        PlaceFleetRandom(playerBoard, playerShips);

        for (int i = 0; i < playerShips.Length; i++)
        {
            shipVertical[i] = playerShips[i].vertical;
            shipCards[i].AddToClassList("ship-card-placed");
        }

        DrawSetupBoard();
        UpdateSetupProgress();
        OnTutorialFleetRandomized();
    }

    private void PlaceFleetRandom(int[,] board, ShipState[] ships)
    {
        for (int shipIndex = 0; shipIndex < ships.Length; shipIndex++)
        {
            bool placed = false;
            int attempts = 0;

            while (!placed && attempts < 1000)
            {
                bool vertical = Random.value > 0.5f;
                int row = Random.Range(0, BoardSize);
                int column = Random.Range(0, BoardSize);
                if (CanPlace(board, ships[shipIndex], row, column, vertical))
                {
                    PlaceShip(board, ships, shipIndex, row, column, vertical);
                    placed = true;
                }
                attempts++;
            }
        }
    }

private void BeginBattle()
{
        for (int i = 0; i < playerShips.Length; i++)
        {
            if (!playerShips[i].placed)
            {
                return;
            }
        }

        if (IsOnlineSetup)
        {
            _ = BeginOnlineQueueAsync();
            return;
        }

        enemyBoard = CreateEmptyBoard();
        enemyShips = CreateShipStates();
        PlaceFleetRandom(enemyBoard, enemyShips);

        playerShots = new bool[BoardSize, BoardSize];
        enemyShots = new bool[BoardSize, BoardSize];
        points = 0;
        playerTurn = true;
        viewingOwnBoard = false;
        gameOver = false;

        BuildBattleGrids();
        InitializeCommanderBattleState();
        surrenderButton.EnableInClassList("hidden", true);
        RefreshBattleUi();
        ShowOnly(battleScreen);
        OnTutorialBattleStarted();
    }

private void BuildBattleGrids()
    {
        mainBattleGrid.Clear();
        playerMiniGrid.Clear();
        mainCells = new Button[BoardSize, BoardSize];
        miniCells = new Button[BoardSize, BoardSize];

        for (int row = 0; row < BoardSize; row++)
        {
            for (int column = 0; column < BoardSize; column++)
            {
                int capturedRow = row;
                int capturedColumn = column;

                Button mainCell = CreateCell(row, column);
                mainCell.clicked += delegate { OnMainCellClicked(capturedRow, capturedColumn); };
                mainCell.RegisterCallback<PointerEnterEvent>(evt => PreviewAbilityAt(capturedRow, capturedColumn));
                mainCells[row, column] = mainCell;
                mainBattleGrid.Add(mainCell);

                Button miniCell = CreateCell(row, column);
                miniCell.pickingMode = PickingMode.Ignore;
                miniCells[row, column] = miniCell;
                playerMiniGrid.Add(miniCell);
            }
        }

        mainBattleGrid.RegisterCallback<PointerLeaveEvent>(evt => ClearAbilityPreview());
    }

private void OnMainCellClicked(int row, int column)
    {
        if (gameOver || !playerTurn || shotCinematicPlaying || abilityCinematicPlaying)
        {
            return;
        }

        if (IsActiveOnlineMatch)
        {
            _ = SubmitOnlineTargetAsync(row, column);
            return;
        }

        if (TryHandleAbilityTarget(row, column))
        {
            return;
        }

        if (viewingOwnBoard)
        {
            viewingOwnBoard = false;
            RefreshBattleUi();
            return;
        }

        if (playerShots[row, column])
        {
            battleMessage.text = "ZIEL BEREITS BESCHOSSEN";
            return;
        }

        playerShots[row, column] = true;
        int shipIndex = enemyBoard[row, column];
        bool hit = shipIndex >= 0;
        bool sunk = false;

        if (hit)
        {
            enemyShips[shipIndex].hits++;
            sunk = enemyShips[shipIndex].hits >= enemyShips[shipIndex].length;
            battleMessage.text = sunk ? "SCHIFF VERSENKT" : "TREFFER";
        }
        else
        {
            if (!bonusShotsSuppressPoints)
            {
                points++;
                battleMessage.text = "DANEBEN // +1 PUNKT";
            }
            else
            {
                battleMessage.text = "FREISCHUSS // DANEBEN";
            }
        }

        OnTutorialFirstShotFired();
        StartNormalShotCinematic(hit, sunk, row, column);
    }

    private IEnumerator BotTurnRoutine()
    {
        battleMessage.text = "GEGNER BERECHNET ZIEL...";
        yield return StartCoroutine(AnnounceTutorialEnemyShot());
        yield return new WaitForSeconds(0.7f);

        int row = raptorJetRow;
        int column = raptorJetColumn;
        bool jetTargetAlreadyShot = row >= 0 && row < BoardSize &&
                                    column >= 0 && column < BoardSize &&
                                    enemyShots[row, column];
        bool forceDeveloperJetHit = ShouldForceBotJetTarget(
            onlineService?.Profile?.displayName,
            DeveloperForceJetHitActive(),
            raptorJetActive,
            row,
            column,
            jetTargetAlreadyShot);
        while (!forceDeveloperJetHit)
        {
            row = Random.Range(0, BoardSize);
            column = Random.Range(0, BoardSize);
            if (!enemyShots[row, column]) break;
        }

        bool mineTriggered = playerMines != null && playerMines[row, column];
        if (mineTriggered)
        {
            playerMines[row, column] = false;
        }

        enemyShots[row, column] = true;
        int shipIndex = playerBoard[row, column];

        bool jetWasActive = raptorJetActive;
        bool jetHit = raptorJetActive && row == raptorJetRow && column == raptorJetColumn;
        if (jetHit)
        {
            raptorJetActive = false;
            raptorJetDestroyedRow = row;
            raptorJetDestroyedColumn = column;
        }

        bool hit = shipIndex >= 0 || jetHit;
        bool blockedBySubmerge = shipIndex >= 0 && shipIndex == submergedShipIndex;
        if (blockedBySubmerge && playerBlockedShots != null)
        {
            playerBlockedShots[row, column] = true;
        }
        if (jetHit)
        {
            battleMessage.text = "RAPTOR // JET ABGESCHOSSEN";
        }
        else if (hit && !blockedBySubmerge)
        {
            playerShips[shipIndex].hits++;
            battleMessage.text = playerShips[shipIndex].hits >= playerShips[shipIndex].length ? "EIGENES SCHIFF VERLOREN" : "GEGNER TRIFFT";
        }
        else if (blockedBySubmerge)
        {
            battleMessage.text = "ABYSS // TREFFER BLOCKIERT";
        }
        else
        {
            battleMessage.text = "GEGNER VERFEHLT";
        }

        ConfigureNextShotJet(jetWasActive, jetHit);
        yield return StartCoroutine(PlayEnemyNormalShotCinematic(hit && !blockedBySubmerge, row, column));
        submergedShipIndex = -1;
        if (raptorJetActive)
        {
            TryMoveRaptorJet(false);
        }
        botAbilitiesJammed = false;
        RefreshBattleUi();

        if (AllShipsSunk(playerShips) && !raptorJetActive)
        {
            EndGame(false);
            yield break;
        }

        yield return new WaitForSeconds(0.35f);
        playerTurn = true;

        if (mineTriggered)
        {
            bonusShotsRemaining = 3;
            bonusShotsSuppressPoints = true;
            battleMessage.text = "MINE AUSGELÖST // 3 FREISCHÜSSE";
        }
        else
        {
            battleMessage.text = "ZIEL WÄHLEN";
        }

        RefreshBattleUi();
        OnTutorialPlayerTurnReady();
    }

    private static bool ShouldForceBotJetTarget(
        string displayName,
        bool developerOverrideEnabled,
        bool jetActive,
        int jetRow,
        int jetColumn,
        bool targetAlreadyShot)
    {
        return developerOverrideEnabled &&
               string.Equals(displayName, "andreas_dev", System.StringComparison.OrdinalIgnoreCase) &&
               jetActive &&
               jetRow >= 0 && jetRow < BoardSize &&
               jetColumn >= 0 && jetColumn < BoardSize &&
               !targetAlreadyShot;
    }

    private bool AllShipsSunk(ShipState[] ships)
    {
        for (int i = 0; i < ships.Length; i++)
        {
            if (ships[i].hits < ships[i].length)
            {
                return false;
            }
        }
        return true;
    }

private void EndGame(bool playerWon)
    {
        StopAllCoroutines();
        HideShotCinematicImmediate();
        ClearAbilityCinematicImmediate();
        CancelAbility();
        gameOver = true;
        playerTurn = false;
        turnLabel.text = playerWon ? "MISSION ERFOLGREICH" : "FLOTTE VERLOREN";
        turnDot.RemoveFromClassList("turn-player");
        turnDot.RemoveFromClassList("turn-enemy");
        turnDot.AddToClassList(playerWon ? "turn-player" : "turn-enemy");
        battleMessage.text = playerWon ? "GEGNERISCHE FLOTTE ZERSTÖRT" : "DEINE FLOTTE WURDE VERSENKT";
        UpdateAbilityButtons();
        OnTutorialMatchEnded(playerWon);
    }

private void OnMiniBoardClicked(ClickEvent evt)
    {
        if (gameOver || abilityCinematicPlaying)
        {
            return;
        }

        if (IsActiveOnlineMatch)
        {
            if (activeAbility != AbilityId.None)
            {
                battleMessage.text = "FÄHIGKEIT ZUERST AUSFÜHREN ODER ABBRECHEN";
                evt.StopPropagation();
                return;
            }
            viewingOwnBoard = !viewingOwnBoard;
            RefreshOnlineBattle(onlineMatchView);
            evt.StopPropagation();
            return;
        }

        if (activeAbility != AbilityId.None)
        {
            battleMessage.text = "FÄHIGKEIT ZUERST AUSFÜHREN ODER ABBRECHEN";
            evt.StopPropagation();
            return;
        }

        viewingOwnBoard = !viewingOwnBoard;
        RefreshBattleUi();
        battleMessage.text = viewingOwnBoard ? "EIGENES RASTER" : "ZIEL WÄHLEN";
        evt.StopPropagation();
    }

private void RefreshBattleUi()
{
        if (IsActiveOnlineMatch)
        {
            RefreshOnlineBattle(onlineMatchView);
            return;
        }
        pointsLabel.text = points.ToString("00");
        RefreshTurnPanel();
        DrawMainBoard();
        DrawMiniBoard();
        DrawFleetStatus();
        RefreshSunkEnemyShips();
        UpdateAbilityButtons();
    }

    private void RefreshSunkEnemyShips()
    {
        if (sunkEnemyShips == null)
        {
            return;
        }

        sunkEnemyShips.Clear();
        if (enemyShips == null)
        {
            sunkEnemyShips.style.display = DisplayStyle.None;
            return;
        }

        for (int index = 0; index < enemyShips.Length; index++)
        {
            ShipState ship = enemyShips[index];
            if (ship.hits < ship.length)
            {
                continue;
            }

            VisualElement marker = new VisualElement();
            marker.pickingMode = PickingMode.Ignore;
            marker.AddToClassList("sunk-ship-marker");
            marker.AddToClassList(shipClasses[index]);
            marker.style.width = 18f + ship.length * 7f;
            sunkEnemyShips.Add(marker);
        }

        sunkEnemyShips.style.display = sunkEnemyShips.childCount > 0
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    private void RefreshTurnPanel()
    {
        if (gameOver)
        {
            return;
        }

        turnDot.EnableInClassList("turn-player", playerTurn);
        turnDot.EnableInClassList("turn-enemy", !playerTurn);
        turnLabel.text = playerTurn ? "DU BIST AM ZUG" : "GEGNER AM ZUG";
    }

private void DrawMainBoard()
    {
        RemoveShipOverlays(mainBattleGrid);
        mainBoardCaption.text = viewingOwnBoard ? "EIGENES RASTER // VERTEIDIGUNG" : "ZIELRASTER // GEGNER";

        for (int row = 0; row < BoardSize; row++)
        {
            for (int column = 0; column < BoardSize; column++)
            {
                Button cell = mainCells[row, column];
                ResetBattleCell(cell);

                if (viewingOwnBoard)
                {
                    if (playerBoard[row, column] >= 0)
                    {
                        cell.AddToClassList("cell-ship");
                    }

                    if (enemyShots[row, column] && (playerBlockedShots == null || !playerBlockedShots[row, column]))
                    {
                        bool destroyedJet = row == raptorJetDestroyedRow && column == raptorJetDestroyedColumn;
                        if (destroyedJet)
                        {
                            cell.AddToClassList("cell-hit");
                            cell.text = "●";
                        }
                        else
                        {
                            ApplyShotState(cell, playerBoard[row, column] >= 0, playerBoard[row, column], playerShips);
                        }
                    }
                    else if (playerBlockedShots != null && playerBlockedShots[row, column])
                    {
                        cell.AddToClassList("cell-shot-blocked");
                        cell.text = "≋";
                    }
                }
                else if (playerShots[row, column])
                {
                    ApplyShotState(cell, enemyBoard[row, column] >= 0, enemyBoard[row, column], enemyShips);
                }

                DecorateCommanderMainCell(cell, row, column);
            }
        }

        if (viewingOwnBoard)
        {
            for (int i = 0; i < playerShips.Length; i++)
            {
                AddShipOverlay(mainBattleGrid, playerShips[i], shipClasses[i]);
            }
            AddRaptorJetOverlay(mainBattleGrid);
        }
        else
        {
            DrawRevealedEnemyShips();
        }
    }

private void DrawMiniBoard()
    {
        RemoveShipOverlays(playerMiniGrid);

        for (int row = 0; row < BoardSize; row++)
        {
            for (int column = 0; column < BoardSize; column++)
            {
                Button cell = miniCells[row, column];
                ResetBattleCell(cell);

                if (playerBoard[row, column] >= 0)
                {
                    cell.AddToClassList("cell-ship");
                }

                if (enemyShots[row, column] && (playerBlockedShots == null || !playerBlockedShots[row, column]))
                {
                    bool destroyedJet = row == raptorJetDestroyedRow && column == raptorJetDestroyedColumn;
                    if (destroyedJet)
                    {
                        cell.AddToClassList("cell-hit");
                        cell.text = "●";
                    }
                    else
                    {
                        ApplyShotState(cell, playerBoard[row, column] >= 0, playerBoard[row, column], playerShips);
                    }
                }
                else if (playerBlockedShots != null && playerBlockedShots[row, column])
                {
                    cell.AddToClassList("cell-shot-blocked");
                    cell.text = "≋";
                }

                DecorateCommanderMiniCell(cell, row, column);
            }
        }

        for (int i = 0; i < playerShips.Length; i++)
        {
            AddShipOverlay(playerMiniGrid, playerShips[i], shipClasses[i]);
        }
        AddRaptorJetOverlay(playerMiniGrid);
    }

private void ResetBattleCell(Button cell)
    {
        cell.text = string.Empty;
        cell.RemoveFromClassList("cell-ship");
        cell.RemoveFromClassList("cell-hit");
        cell.RemoveFromClassList("cell-miss");
        cell.RemoveFromClassList("cell-sunk");
        cell.RemoveFromClassList("cell-scanned");
        cell.RemoveFromClassList("cell-mine");
        cell.RemoveFromClassList("cell-revealed");
        cell.RemoveFromClassList("cell-ability-preview");
        cell.RemoveFromClassList("cell-rangefinder-area");
        cell.RemoveFromClassList("cell-rangefinder-edge");
        cell.RemoveFromClassList("cell-submerged");
        cell.RemoveFromClassList("cell-shot-blocked");
    }

private void ApplyShotState(Button cell, bool hit, int shipIndex, ShipState[] ships)
    {
        if (!hit)
        {
            cell.AddToClassList("cell-miss");
            cell.text = "●";
            return;
        }

        cell.AddToClassList("cell-hit");
        cell.text = "●";

        if (shipIndex >= 0 && ships[shipIndex].hits >= ships[shipIndex].length)
        {
            cell.AddToClassList("cell-sunk");
            cell.text = "●";
        }
    }

    private void DrawFleetStatus()
    {
        fleetStatus.Clear();

        for (int i = 0; i < playerShips.Length; i++)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("fleet-row");
            if (playerShips[i].hits >= playerShips[i].length)
            {
                row.AddToClassList("fleet-row-sunk");
            }

            VisualElement icon = new VisualElement();
            icon.AddToClassList("fleet-icon");
            icon.AddToClassList(shipClasses[i]);
            row.Add(icon);

            VisualElement health = new VisualElement();
            health.AddToClassList("fleet-health");
            for (int pipIndex = 0; pipIndex < playerShips[i].length; pipIndex++)
            {
                VisualElement pip = new VisualElement();
                pip.AddToClassList("health-pip");
                if (pipIndex < playerShips[i].hits)
                {
                    pip.AddToClassList("health-pip-lost");
                }
                health.Add(pip);
            }

            row.Add(health);
            fleetStatus.Add(row);
        }
    }
}
