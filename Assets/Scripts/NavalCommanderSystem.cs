using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    public enum AbilityId
    {
        None,
        StandardBarrage,
        StandardLine,
        OracleScan,
        AresRepair,
        ChemicalBomb,
        MineLayer,
        FleetRelocation,
        NuclearStrike,
        RangefinderShot,
        BerserkerBarrage,
        AbyssTorpedo,
        AbyssSubmerge,
        RaptorJetStart,
        RaptorJammer
    }

    public sealed class AbilityData
    {
        public AbilityId id;
        public string name;
        public string shortLabel;
        public int cost;
        public bool targetsOwnBoard;
        public string animationId;
        public string fxClass;
        public string fxGlyph;
    }

    public sealed class CommanderData
    {
        public string id;
        public string name;
        public string rank;
        public string portraitClass;
        public string monogram;
        public int[] shipLengths;
        public int[] shipWidths;
        public int[] shipHeights;
        public string[] shipClasses;
        public AbilityData[] abilities;
    }

    public static CommanderData[] CreateCommanderCatalog()
    {
        return new[]
        {
            new CommanderData
            {
                id = "standard-commander",
                name = "STANDARD-KOMMANDANT",
                rank = "FLOTTENKOMMANDO",
                portraitClass = "portrait-standard",
                monogram = "FC",
                shipLengths = new[] { 5, 4, 3, 3, 2 },
                shipWidths = new[] { 5, 4, 3, 3, 2 },
                shipHeights = new[] { 1, 1, 1, 1, 1 },
                shipClasses = new[] { "ship-5", "ship-4b", "ship-3a", "ship-3b", "ship-2" },
                abilities = new AbilityData[0]
            },
            new CommanderData
            {
                id = "elias-voss",
                name = "KAPITÄN ELIAS VOSS",
                rank = "BERGUNGSKOMMANDO // ORACLE",
                portraitClass = "portrait-elias",
                monogram = string.Empty,
                shipLengths = new[] { 2, 3, 5 },
                shipWidths = new[] { 2, 3, 5 },
                shipHeights = new[] { 1, 1, 1 },
                shipClasses = new[] { "ship-2", "ship-3a", "ship-5" },
                abilities = new[]
                {
                    Ability(AbilityId.OracleScan, "ORACLE-SEKTORSCANNER", "SEKTORSCAN", 8, false, "oracle-sector-sweep", "fx-scan", "◉"),
                    Ability(AbilityId.AresRepair, "ARES-FELDREPARATUR", "REPARATUR", 3, true, "ares-repair-beam", "fx-repair", "✚")
                }
            },
            new CommanderData
            {
                id = "dae-hyun-kwon",
                name = "KAPITÄN DAE-HYUN KWON",
                rank = "SPEZIALWAFFEN // VIPER",
                portraitClass = "portrait-dae",
                monogram = string.Empty,
                shipLengths = new[] { 4, 4, 2, 2 },
                shipWidths = new[] { 4, 4, 2, 2 },
                shipHeights = new[] { 1, 1, 1, 1 },
                shipClasses = new[] { "ship-4b", "ship-4", "ship-2", "ship-2b" },
                abilities = new[]
                {
                    Ability(AbilityId.ChemicalBomb, "CHEMIEBOMBE", "CHEMIEBOMBE", 4, false, "viper-chemical-cloud", "fx-chemical", "☣"),
                    Ability(AbilityId.MineLayer, "MINENLEGER", "MINENLEGER", 7, true, "viper-mine-arm", "fx-mine", "◆")
                }
            },
            new CommanderData
            {
                id = "ronan-graves",
                name = "KAPITÄN RONAN GRAVES",
                rank = "BELAGERUNGSKOMMANDO // TITAN",
                portraitClass = "portrait-ronan",
                monogram = string.Empty,
                shipLengths = new[] { 14, 2 },
                shipWidths = new[] { 7, 2 },
                shipHeights = new[] { 2, 1 },
                shipClasses = new[] { "ship-14", "ship-2" },
                abilities = new[]
                {
                    Ability(AbilityId.FleetRelocation, "VERSCHIEBUNG", "VERSCHIEBUNG", 5, true, "titan-fleet-relocation", "fx-relocation", "↟"),
                    Ability(AbilityId.NuclearStrike, "ATOMBOMBE", "ATOMBOMBE", 7, false, "titan-nuclear-strike", "fx-nuclear", "☢")
                }
            },
            new CommanderData
            {
                id = "arjan-dhillon",
                name = "KAPITÄN ARJAN DHILLON",
                rank = "FEUERLEITKOMMANDO // VECTOR",
                portraitClass = "portrait-arjan",
                monogram = string.Empty,
                shipLengths = new[] { 6, 4, 3 },
                shipWidths = new[] { 3, 4, 3 },
                shipHeights = new[] { 2, 1, 1 },
                shipClasses = new[] { "ship-6", "ship-4", "ship-3a" },
                abilities = new[]
                {
                    Ability(AbilityId.RangefinderShot, "ENTFERNUNGSMESSER", "ENTFERNUNG", 5, false, "vector-rangefinder", "fx-rangefinder", "◎"),
                    Ability(AbilityId.BerserkerBarrage, "AMOKLAUF", "AMOKLAUF ×6", 8, false, "berserker-random-barrage", "fx-berserker", "✦")
                }
            },
            new CommanderData
            {
                id = "mateo-serrano",
                name = "KAPITÄN MATEO SERRANO",
                rank = "UNTERWASSERKOMMANDO // ABYSS",
                portraitClass = "portrait-mateo",
                monogram = string.Empty,
                shipLengths = new[] { 5, 3, 3 },
                shipWidths = new[] { 5, 3, 3 },
                shipHeights = new[] { 1, 1, 1 },
                shipClasses = new[] { "ship-5", "ship-3b", "ship-3a" },
                abilities = new[]
                {
                    Ability(AbilityId.AbyssTorpedo, "TORPEDO", "TORPEDO", 14, false, "abyss-row-torpedo", "fx-abyss-torpedo", "➤"),
                    Ability(AbilityId.AbyssSubmerge, "UNTERTAUCHEN", "UNTERTAUCHEN", 5, true, "abyss-submerge", "fx-abyss-submerge", "≋")
                }
            },
            new CommanderData
            {
                id = "imani-cross",
                name = "KAPITÄN IMANI CROSS",
                rank = "TRÄGERLUFTGRUPPE & ELEKTRONISCHE KAMPFFÜHRUNG // RAPTOR",
                portraitClass = "portrait-imani",
                monogram = string.Empty,
                shipLengths = new[] { 5, 6, 2 },
                shipWidths = new[] { 5, 3, 2 },
                shipHeights = new[] { 1, 2, 1 },
                shipClasses = new[] { "ship-5", "ship-6", "ship-2" },
                abilities = new[]
                {
                    Ability(AbilityId.RaptorJetStart, "JET START", "JET START", 8, false, "raptor-jet-start", "fx-raptor-jet", "✈"),
                    Ability(AbilityId.RaptorJammer, "STÖRSENDER", "STÖRSENDER", 3, false, "raptor-jammer", "fx-raptor-jammer", "⌁")
                }
            }
        };
    }

    private static AbilityData Ability(AbilityId id, string name, string shortLabel, int cost, bool own, string animationId, string fxClass, string glyph)
    {
        return new AbilityData
        {
            id = id,
            name = name,
            shortLabel = shortLabel,
            cost = cost,
            targetsOwnBoard = own,
            animationId = animationId,
            fxClass = fxClass,
            fxGlyph = glyph
        };
    }

    private CommanderData[] commanderCatalog;
    private CommanderData currentCommander;
    private VisualElement commanderScreen;
    private VisualElement battleCommanderPortrait;
    private Button commanderBackButton;
    private Button abilityOneButton;
    private Button abilityTwoButton;
    private Label abilityOneCostLabel;
    private Label abilityOneNameLabel;
    private Label abilityTwoCostLabel;
    private Label abilityTwoNameLabel;
    private Toggle debugModeToggle;
    private Button[] commanderSelectButtons;
    private Button[] commanderInfoButtons;
    private VisualElement[] commanderCards;
    private VisualElement[] commanderInfoPanels;
    private Label battleCommanderName;
    private Label battleCommanderRank;
    private Label battleCommanderMonogram;

    private bool[,] enemyScanWater;
    private bool[,] enemyRangefinderArea;
    private bool[,] playerMines;
    private bool[,] playerBlockedShots;
    private bool[] enemyRevealedShips;
    private int bonusShotsRemaining;
    private bool bonusShotsSuppressPoints;
    private AbilityId activeAbility = AbilityId.None;
    private bool lineVertical;
    private bool debugMode;
    private bool abilityCinematicPlaying;
    private int relocationShipIndex = -1;
    private string lastRelocatedShipClass = "ship-14";
    private int lastRangefinderDistance = -1;
    private int lastRangefinderRow = -1;
    private int lastRangefinderColumn = -1;
    private bool lastRangefinderHit;
    private int submergedShipIndex = -1;
    private bool raptorJetLaunched;
    private bool raptorJetActive;
    private int raptorJetRow = -1;
    private int raptorJetColumn = -1;
    private int raptorJetDestroyedRow = -1;
    private int raptorJetDestroyedColumn = -1;
    private bool botAbilitiesJammed;
    private readonly bool[] lastTorpedoResolvedColumns = new bool[BoardSize];
    private readonly bool[] lastTorpedoHitColumns = new bool[BoardSize];

    private sealed class BarrageShotResult
    {
        public int row;
        public int column;
        public bool hit;
    }

    private readonly List<BarrageShotResult> lastBarrageShots = new List<BarrageShotResult>();
    private readonly List<BarrageShotResult> lastStandardBarrageShots = new List<BarrageShotResult>();
    private readonly List<BarrageShotResult> lastStandardLineShots = new List<BarrageShotResult>();

    private void InitializeCommanderSystem()
    {
        commanderCatalog = CreateCommanderCatalog();
        currentCommander = commanderCatalog[0];
        ApplyCommanderFleet();
    }

    private void CacheCommanderUi(VisualElement root)
    {
        commanderScreen = root.Q<VisualElement>("CommanderScreen");
        battleCommanderPortrait = root.Q<VisualElement>("BattleCommanderPortrait");
        commanderBackButton = root.Q<Button>("CommanderBackButton");
        abilityOneButton = root.Q<Button>("AbilityOneButton");
        abilityTwoButton = root.Q<Button>("AbilityTwoButton");
        abilityOneCostLabel = root.Q<Label>("AbilityOneCostLabel");
        abilityOneNameLabel = root.Q<Label>("AbilityOneNameLabel");
        abilityTwoCostLabel = root.Q<Label>("AbilityTwoCostLabel");
        abilityTwoNameLabel = root.Q<Label>("AbilityTwoNameLabel");
        debugModeToggle = root.Q<Toggle>("DebugModeToggle");
        commanderSelectButtons = new[]
        {
            root.Q<Button>("SelectCommander0"),
            root.Q<Button>("SelectCommander1"),
            root.Q<Button>("SelectCommander2"),
            root.Q<Button>("SelectCommander3"),
            root.Q<Button>("SelectCommander4"),
            root.Q<Button>("SelectCommander5"),
            root.Q<Button>("SelectCommander6")
        };
        commanderInfoButtons = new[]
        {
            root.Q<Button>("CommanderInfoButton0"),
            root.Q<Button>("CommanderInfoButton1"),
            root.Q<Button>("CommanderInfoButton2"),
            root.Q<Button>("CommanderInfoButton3"),
            root.Q<Button>("CommanderInfoButton4"),
            root.Q<Button>("CommanderInfoButton5"),
            root.Q<Button>("CommanderInfoButton6")
        };
        commanderCards = new[]
        {
            root.Q<VisualElement>("CommanderCard0"),
            root.Q<VisualElement>("CommanderCard1"),
            root.Q<VisualElement>("CommanderCard2"),
            root.Q<VisualElement>("CommanderCard3"),
            root.Q<VisualElement>("CommanderCard4"),
            root.Q<VisualElement>("CommanderCard5"),
            root.Q<VisualElement>("CommanderCard6")
        };
        commanderInfoPanels = new[]
        {
            root.Q<VisualElement>("CommanderInfo0"),
            root.Q<VisualElement>("CommanderInfo1"),
            root.Q<VisualElement>("CommanderInfo2"),
            root.Q<VisualElement>("CommanderInfo3"),
            root.Q<VisualElement>("CommanderInfo4"),
            root.Q<VisualElement>("CommanderInfo5"),
            root.Q<VisualElement>("CommanderInfo6")
        };
        battleCommanderName = root.Q<Label>("BattleCommanderName");
        battleCommanderRank = root.Q<Label>("BattleCommanderRank");
        battleCommanderMonogram = root.Q<Label>("BattleCommanderMonogram");
    }

    private void BindCommanderUi()
    {
        commanderBackButton.clicked += HandleCommanderBack;
        abilityOneButton.clicked += delegate { ActivateAbility(0); };
        abilityTwoButton.clicked += delegate { ActivateAbility(1); };
        debugModeToggle.RegisterValueChangedCallback(evt => SetDebugMode(evt.newValue));

        for (int i = 0; i < commanderSelectButtons.Length; i++)
        {
            int commanderIndex = i;
            commanderSelectButtons[i].clicked += delegate { SelectCommander(commanderIndex); };
            commanderInfoButtons[i].clicked += delegate { ToggleCommanderInfo(commanderIndex); };
        }
    }

    private void ShowCommanderSelection()
    {
        StopAllCoroutines();
        CancelAbility();
        UpdateCommanderAvailability();
        CollapseCommanderInfo();
        ShowOnly(commanderScreen);
    }

    private void ToggleCommanderInfo(int index)
    {
        if (index < 0 || index >= commanderInfoPanels.Length) return;
        bool show = commanderInfoPanels[index].ClassListContains("hidden");
        CollapseCommanderInfo();
        commanderInfoPanels[index].EnableInClassList("hidden", !show);
        commanderInfoButtons[index].EnableInClassList("info-open", show);
    }

    private void CollapseCommanderInfo()
    {
        for (int index = 0; index < commanderInfoPanels.Length; index++)
        {
            commanderInfoPanels[index].AddToClassList("hidden");
            commanderInfoButtons[index].RemoveFromClassList("info-open");
        }
    }

    private void UpdateCommanderAvailability()
    {
        bool developerAccount = IsDeveloperAbilityAccount();
        for (int index = 0; index < commanderSelectButtons.Length && index < commanderCatalog.Length; index++)
        {
            bool owned = developerAccount ||
                         (onlineService?.Entitlements ?? new NavalEntitlements()).OwnsCommander(commanderCatalog[index].id);
            commanderCards[index].style.display = owned ? DisplayStyle.Flex : DisplayStyle.None;
            commanderSelectButtons[index].text = "AUSWÄHLEN";
            commanderSelectButtons[index].SetEnabled(owned);
        }
    }

    private void SelectCommander(int index)
    {
        if (index < 0 || index >= commanderCatalog.Length)
        {
            return;
        }
        if (!IsDeveloperAbilityAccount() &&
            !(onlineService?.Entitlements ?? new NavalEntitlements()).OwnsCommander(commanderCatalog[index].id))
        {
            return;
        }

        currentCommander = commanderCatalog[index];
        ApplyCommanderFleet();
        ShowSetup();
    }

    private void ApplyCommanderFleet()
    {
        shipLengths = (int[])currentCommander.shipLengths.Clone();
        shipWidths = (int[])currentCommander.shipWidths.Clone();
        shipHeights = (int[])currentCommander.shipHeights.Clone();
        shipClasses = (string[])currentCommander.shipClasses.Clone();
    }

    private void InitializeCommanderBattleState()
    {
        ClearAbilityCinematicImmediate();
        enemyScanWater = new bool[BoardSize, BoardSize];
        enemyRangefinderArea = new bool[BoardSize, BoardSize];
        playerMines = new bool[BoardSize, BoardSize];
        playerBlockedShots = new bool[BoardSize, BoardSize];
        enemyRevealedShips = new bool[enemyShips.Length];
        bonusShotsRemaining = 0;
        bonusShotsSuppressPoints = false;
        activeAbility = AbilityId.None;
        relocationShipIndex = -1;
        lastRangefinderDistance = -1;
        lastRangefinderRow = -1;
        lastRangefinderColumn = -1;
        lastRangefinderHit = false;
        submergedShipIndex = -1;
        raptorJetLaunched = false;
        raptorJetActive = false;
        raptorJetRow = -1;
        raptorJetColumn = -1;
        raptorJetDestroyedRow = -1;
        raptorJetDestroyedColumn = -1;
        botAbilitiesJammed = false;
        System.Array.Clear(lastTorpedoResolvedColumns, 0, lastTorpedoResolvedColumns.Length);
        System.Array.Clear(lastTorpedoHitColumns, 0, lastTorpedoHitColumns.Length);
        lastBarrageShots.Clear();
        lastStandardBarrageShots.Clear();
        lastStandardLineShots.Clear();
        lineVertical = false;
        UpdateCommanderPanel();
    }

    private void UpdateCommanderPanel()
    {
        string[] portraits = { "portrait-standard", "portrait-elias", "portrait-dae", "portrait-ronan", "portrait-arjan", "portrait-mateo", "portrait-imani" };
        for (int i = 0; i < portraits.Length; i++)
        {
            battleCommanderPortrait.RemoveFromClassList(portraits[i]);
        }
        battleCommanderPortrait.AddToClassList(currentCommander.portraitClass);

        battleCommanderName.text = currentCommander.name;
        battleCommanderRank.text = currentCommander.rank;
        battleCommanderMonogram.text = currentCommander.monogram;
        battleCommanderMonogram.style.display = string.IsNullOrEmpty(currentCommander.monogram) ? DisplayStyle.None : DisplayStyle.Flex;
        UpdateAbilityButtons();
    }

    private void SetDebugMode(bool enabled)
    {
        debugMode = enabled;
        UpdateAbilityButtons();
    }

    private int GetEffectiveAbilityCost(AbilityData ability)
    {
        return DeveloperFreeAbilitiesActive() || (debugMode && !IsActiveOnlineMatch) ? 0 : ability.cost;
    }

    private bool IsDeveloperAbilityAccount()
    {
        return string.Equals(onlineService?.Profile?.displayName, "andreas_dev", System.StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateAbilityButtons()
    {
        if (abilityOneButton == null || currentCommander == null)
        {
            return;
        }

        int abilityCount = currentCommander.abilities == null ? 0 : currentCommander.abilities.Length;
        abilityOneButton.style.display = abilityCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        abilityTwoButton.style.display = abilityCount > 1 ? DisplayStyle.Flex : DisplayStyle.None;

        if (abilityCount == 0)
        {
            abilityOneButton.RemoveFromClassList("ability-active");
            abilityTwoButton.RemoveFromClassList("ability-active");
            return;
        }

        AbilityData first = currentCommander.abilities[0];

        int firstCost = GetEffectiveAbilityCost(first);
        abilityOneCostLabel.text = firstCost.ToString();
        abilityOneNameLabel.text = first.shortLabel;
        bool abilitiesJammed = IsOwnAbilitiesJammed();
        bool firstAvailable = first.id != AbilityId.RaptorJetStart || !raptorJetLaunched;
        abilityOneButton.SetEnabled(playerTurn && !gameOver && !abilityCinematicPlaying && !abilitiesJammed && firstAvailable && points >= firstCost);
        abilityOneButton.EnableInClassList("ability-active", activeAbility == first.id);

        if (abilityCount == 1)
        {
            abilityTwoButton.RemoveFromClassList("ability-active");
            return;
        }

        AbilityData second = currentCommander.abilities[1];
        int secondCost = GetEffectiveAbilityCost(second);
        string secondLabel = second.shortLabel;
        if (activeAbility == AbilityId.StandardLine)
        {
            secondLabel = lineVertical ? "LINIE VERTIKAL" : "LINIE HORIZONTAL";
        }
        abilityTwoCostLabel.text = secondCost.ToString();
        abilityTwoNameLabel.text = secondLabel;

        abilityTwoButton.SetEnabled(playerTurn && !gameOver && !abilityCinematicPlaying && !abilitiesJammed && points >= secondCost);
        abilityTwoButton.EnableInClassList("ability-active", activeAbility == second.id);
    }

    private void ActivateAbility(int slot)
    {
        if (!playerTurn || gameOver || abilityCinematicPlaying || slot < 0 ||
            currentCommander == null || currentCommander.abilities == null || slot >= currentCommander.abilities.Length)
        {
            return;
        }

        AbilityData ability = currentCommander.abilities[slot];
        int effectiveCost = GetEffectiveAbilityCost(ability);
        if (points < effectiveCost)
        {
            battleMessage.text = "NICHT GENUG PUNKTE";
            return;
        }

        if (ability.id == AbilityId.BerserkerBarrage || ability.id == AbilityId.RaptorJetStart || ability.id == AbilityId.RaptorJammer)
        {
            if (IsActiveOnlineMatch)
            {
                _ = SubmitOnlineAutomaticAbilityAsync(ability);
            }
            else if (ability.id == AbilityId.RaptorJetStart || ability.id == AbilityId.RaptorJammer)
            {
                ExecuteRaptorAutomaticAbility(ability, effectiveCost);
            }
            else
            {
                ExecuteBerserkerBarrage(ability, effectiveCost);
            }
            return;
        }

        if (activeAbility == ability.id)
        {
            if (ability.id == AbilityId.StandardLine)
            {
                lineVertical = !lineVertical;
                battleMessage.text = lineVertical ? "VERTIKALE DREIERLINIE WÄHLEN" : "HORIZONTALE DREIERLINIE WÄHLEN";
                UpdateAbilityButtons();
                return;
            }

            CancelAbility();
            battleMessage.text = "FÄHIGKEIT ABGEBROCHEN";
            return;
        }

        activeAbility = ability.id;
        viewingOwnBoard = ability.targetsOwnBoard;
        ClearAbilityPreview();
        RefreshBattleUi();
        OnTutorialAbilitySelected();

        switch (ability.id)
        {
            case AbilityId.StandardBarrage:
                battleMessage.text = "2×2-ZIELBEREICH WÄHLEN";
                break;
            case AbilityId.StandardLine:
                battleMessage.text = lineVertical ? "VERTIKALE DREIERLINIE WÄHLEN" : "HORIZONTALE DREIERLINIE WÄHLEN";
                break;
            case AbilityId.OracleScan:
                battleMessage.text = "3×3-SCANSEKTOR WÄHLEN";
                break;
            case AbilityId.AresRepair:
                battleMessage.text = "GETROFFENES EIGENES FELD WÄHLEN";
                break;
            case AbilityId.ChemicalBomb:
                battleMessage.text = "CHEMISCHES ZIEL WÄHLEN";
                break;
            case AbilityId.MineLayer:
                battleMessage.text = "UNBESCHOSSES EIGENES FELD WÄHLEN";
                break;
            case AbilityId.FleetRelocation:
                relocationShipIndex = -1;
                battleMessage.text = "UNBESCHÄDIGTES SCHIFF WÄHLEN";
                break;
            case AbilityId.NuclearStrike:
                battleMessage.text = "ZIEL FÜR ATOMBOMBE WÄHLEN";
                break;
            case AbilityId.RangefinderShot:
                battleMessage.text = "ZIEL FÜR ENTFERNUNGSMESSER WÄHLEN";
                break;
            case AbilityId.AbyssTorpedo:
                battleMessage.text = "HORIZONTALE REIHE FÜR TORPEDO WÄHLEN";
                break;
            case AbilityId.AbyssSubmerge:
                battleMessage.text = "AKTIVES EIGENES SCHIFF ZUM UNTERTAUCHEN WÄHLEN";
                break;
        }
    }

    private bool IsOwnAbilitiesJammed()
    {
        return IsActiveOnlineMatch && onlineMatchView != null && onlineMatchView.ownAbilitiesJammed;
    }

    private void CancelAbility()
    {
        activeAbility = AbilityId.None;
        relocationShipIndex = -1;
        ClearAbilityPreview();
        if (abilityOneButton != null)
        {
            abilityOneButton.RemoveFromClassList("ability-active");
            abilityTwoButton.RemoveFromClassList("ability-active");
        }
    }

    private void PreviewAbilityAt(int row, int column)
    {
        if (activeAbility == AbilityId.None)
        {
            return;
        }

        ClearAbilityPreview();
        if (activeAbility == AbilityId.FleetRelocation && relocationShipIndex >= 0)
        {
            ShipState ship = playerShips[relocationShipIndex];
            int rows = ship.vertical ? ship.width : ship.height;
            int columns = ship.vertical ? ship.height : ship.width;
            for (int rowOffset = 0; rowOffset < rows; rowOffset++)
            for (int columnOffset = 0; columnOffset < columns; columnOffset++)
            {
                int targetRow = row + rowOffset;
                int targetColumn = column + columnOffset;
                if (targetRow >= 0 && targetRow < BoardSize && targetColumn >= 0 && targetColumn < BoardSize)
                    mainCells[targetRow, targetColumn].AddToClassList("cell-ability-preview");
            }
            return;
        }
        List<Vector2Int> cells = GetAbilityPattern(row, column, activeAbility);
        for (int i = 0; i < cells.Count; i++)
        {
            mainCells[cells[i].x, cells[i].y].AddToClassList("cell-ability-preview");
        }
    }

    private void ClearAbilityPreview()
    {
        if (mainCells == null)
        {
            return;
        }

        for (int row = 0; row < BoardSize; row++)
        {
            for (int column = 0; column < BoardSize; column++)
            {
                mainCells[row, column].RemoveFromClassList("cell-ability-preview");
            }
        }
    }

    private List<Vector2Int> GetAbilityPattern(int row, int column, AbilityId ability)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        if (ability == AbilityId.StandardBarrage)
        {
            int startRow = Mathf.Clamp(row, 0, BoardSize - 2);
            int startColumn = Mathf.Clamp(column, 0, BoardSize - 2);
            for (int r = startRow; r < startRow + 2; r++)
            {
                for (int c = startColumn; c < startColumn + 2; c++)
                {
                    cells.Add(new Vector2Int(r, c));
                }
            }
        }
        else if (ability == AbilityId.StandardLine)
        {
            if (lineVertical)
            {
                int startRow = Mathf.Clamp(row - 1, 0, BoardSize - 3);
                for (int r = startRow; r < startRow + 3; r++)
                {
                    cells.Add(new Vector2Int(r, column));
                }
            }
            else
            {
                int startColumn = Mathf.Clamp(column - 1, 0, BoardSize - 3);
                for (int c = startColumn; c < startColumn + 3; c++)
                {
                    cells.Add(new Vector2Int(row, c));
                }
            }
        }
        else if (ability == AbilityId.OracleScan)
        {
            int startRow = Mathf.Clamp(row - 1, 0, BoardSize - 3);
            int startColumn = Mathf.Clamp(column - 1, 0, BoardSize - 3);
            for (int r = startRow; r < startRow + 3; r++)
            {
                for (int c = startColumn; c < startColumn + 3; c++)
                {
                    cells.Add(new Vector2Int(r, c));
                }
            }
        }
        else if (ability == AbilityId.AbyssTorpedo)
        {
            for (int targetColumn = 0; targetColumn < BoardSize; targetColumn++)
            {
                cells.Add(new Vector2Int(row, targetColumn));
            }
        }
        else
        {
            cells.Add(new Vector2Int(row, column));
        }

        return cells;
    }

    private bool TryHandleAbilityTarget(int row, int column)
    {
        if (activeAbility == AbilityId.None)
        {
            return false;
        }

        AbilityData ability = GetActiveAbilityData();
        int effectiveCost = ability == null ? int.MaxValue : GetEffectiveAbilityCost(ability);
        if (ability == null || points < effectiveCost)
        {
            battleMessage.text = "FÄHIGKEIT NICHT VERFÜGBAR";
            return true;
        }

        if (activeAbility == AbilityId.FleetRelocation && relocationShipIndex < 0)
        {
            int selected = playerBoard[row, column];
            if (selected < 0)
            {
                battleMessage.text = "HIER LIEGT KEIN SCHIFF";
                return true;
            }
            if (playerShips[selected].hits > 0)
            {
                battleMessage.text = "NUR UNBESCHÄDIGTE SCHIFFE KÖNNEN VERSCHOBEN WERDEN";
                return true;
            }
            relocationShipIndex = selected;
            lastRelocatedShipClass = shipClasses[selected];
            battleMessage.text = "NEUE POSITION WÄHLEN // AUSRICHTUNG BLEIBT";
            RefreshBattleUi();
            return true;
        }

        bool success;
        string result;

        switch (activeAbility)
        {
            case AbilityId.StandardBarrage:
            case AbilityId.StandardLine:
                success = ResolveAttackPattern(GetAbilityPattern(row, column, activeAbility), out result);
                break;
            case AbilityId.OracleScan:
                success = ResolveOracleScan(GetAbilityPattern(row, column, activeAbility), out result);
                break;
            case AbilityId.AresRepair:
                success = ResolveRepair(row, column, out result);
                break;
            case AbilityId.ChemicalBomb:
                success = ResolveChemicalBomb(row, column, out result);
                break;
            case AbilityId.MineLayer:
                success = ResolveMineLayer(row, column, out result);
                break;
            case AbilityId.FleetRelocation:
                success = ResolveFleetRelocation(row, column, out result);
                break;
            case AbilityId.NuclearStrike:
                success = ResolveNuclearStrike(row, column, out result);
                break;
            case AbilityId.RangefinderShot:
                success = ResolveRangefinderShot(row, column, out result);
                break;
            case AbilityId.AbyssTorpedo:
                success = ResolveAbyssTorpedo(row, out result);
                break;
            case AbilityId.AbyssSubmerge:
                success = ResolveAbyssSubmerge(row, column, out result);
                break;
            default:
                success = false;
                result = "UNGÜLTIGE FÄHIGKEIT";
                break;
        }

        if (!success)
        {
            battleMessage.text = result;
            return true;
        }

        points -= effectiveCost;
        activeAbility = AbilityId.None;
        relocationShipIndex = -1;
        battleMessage.text = result;
        RefreshBattleUi();
        OnTutorialAbilityResolved();
        StartCoroutine(PlayAbilityFxAndFinishTurn(ability, result, row, column));

        return true;
    }

    private AbilityData GetActiveAbilityData()
    {
        for (int i = 0; i < currentCommander.abilities.Length; i++)
        {
            if (currentCommander.abilities[i].id == activeAbility)
            {
                return currentCommander.abilities[i];
            }
        }
        return null;
    }

    private bool ResolveAttackPattern(List<Vector2Int> cells, out string message)
    {
        int resolved = 0;
        int hits = 0;
        List<BarrageShotResult> cinematicShots = null;
        if (activeAbility == AbilityId.StandardBarrage)
        {
            lastStandardBarrageShots.Clear();
            cinematicShots = lastStandardBarrageShots;
        }
        else if (activeAbility == AbilityId.StandardLine)
        {
            lastStandardLineShots.Clear();
            cinematicShots = lastStandardLineShots;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            int row = cells[i].x;
            int column = cells[i].y;
            int shipIndex = enemyBoard[row, column];
            if (cinematicShots != null)
            {
                cinematicShots.Add(new BarrageShotResult
                {
                    row = row,
                    column = column,
                    hit = shipIndex >= 0
                });
            }
            if (playerShots[row, column])
            {
                continue;
            }

            playerShots[row, column] = true;
            resolved++;
            if (shipIndex >= 0)
            {
                enemyShips[shipIndex].hits++;
                hits++;
            }
        }

        if (resolved == 0)
        {
            message = "ALLE ZIELE BEREITS BESCHOSSEN";
            return false;
        }

        message = hits > 0 ? "SPEZIALANGRIFF // " + hits + " TREFFER" : "SPEZIALANGRIFF // KEIN TREFFER";
        return true;
    }

    private bool ResolveOracleScan(List<Vector2Int> cells, out string message)
    {
        bool contact = false;
        bool newInformation = false;

        for (int i = 0; i < cells.Count; i++)
        {
            int row = cells[i].x;
            int column = cells[i].y;
            if (enemyBoard[row, column] >= 0)
            {
                contact = true;
            }
            else if (!enemyScanWater[row, column] && !playerShots[row, column])
            {
                newInformation = true;
            }
        }

        if (contact)
        {
            message = "ORACLE // KONTAKT IM SEKTOR";
            return true;
        }

        if (!newInformation)
        {
            message = "SEKTOR BEREITS VOLLSTÄNDIG ERFASST";
            return false;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            int row = cells[i].x;
            int column = cells[i].y;
            if (enemyBoard[row, column] < 0 && !playerShots[row, column])
            {
                enemyScanWater[row, column] = true;
            }
        }

        message = "ORACLE // SEKTOR IST LEER";
        return true;
    }

    private bool ResolveRepair(int row, int column, out string message)
    {
        int shipIndex = playerBoard[row, column];
        if (shipIndex < 0 || !enemyShots[row, column])
        {
            message = "HIER GIBT ES KEINEN SCHADEN";
            return false;
        }

        if (playerShips[shipIndex].hits >= playerShips[shipIndex].length)
        {
            message = "VERSENKTE SCHIFFE SIND NICHT REPARIERBAR";
            return false;
        }

        enemyShots[row, column] = false;
        playerShips[shipIndex].hits = Mathf.Max(0, playerShips[shipIndex].hits - 1);
        message = "ARES // SCHIFFSTEIL REPARIERT";
        return true;
    }

    private bool ResolveChemicalBomb(int row, int column, out string message)
    {
        if (playerShots[row, column])
        {
            message = "ZIEL BEREITS BESCHOSSEN";
            return false;
        }

        playerShots[row, column] = true;
        int shipIndex = enemyBoard[row, column];
        if (shipIndex < 0)
        {
            message = "CHEMIEBOMBE // KEIN KONTAKT";
            return true;
        }

        enemyShips[shipIndex].hits++;
        enemyRevealedShips[shipIndex] = true;
        message = "CHEMIEBOMBE // SCHIFF AUFGEDECKT";
        return true;
    }

    private bool ResolveMineLayer(int row, int column, out string message)
    {
        if (enemyShots[row, column])
        {
            message = "FELD WURDE BEREITS BESCHOSSEN";
            return false;
        }

        if (playerMines[row, column])
        {
            message = "HIER LIEGT BEREITS EINE MINE";
            return false;
        }

        playerMines[row, column] = true;
        message = "MINE SCHARF // POSITION GESICHERT";
        return true;
    }

    private bool ResolveFleetRelocation(int row, int column, out string message)
    {
        if (relocationShipIndex < 0 || relocationShipIndex >= playerShips.Length)
        {
            message = "ZUERST EIN SCHIFF WÄHLEN";
            return false;
        }

        ShipState ship = playerShips[relocationShipIndex];
        if (ship.hits > 0)
        {
            message = "BESCHÄDIGTES SCHIFF KANN NICHT VERSCHOBEN WERDEN";
            return false;
        }
        if (ship.row == row && ship.column == column)
        {
            message = "NEUE POSITION MUSS SICH UNTERSCHEIDEN";
            return false;
        }
        if (!CanPlace(playerBoard, ship, row, column, ship.vertical, relocationShipIndex))
        {
            message = "POSITION BLOCKIERT";
            return false;
        }

        int rows = ship.vertical ? ship.width : ship.height;
        int columns = ship.vertical ? ship.height : ship.width;
        for (int rowOffset = 0; rowOffset < rows; rowOffset++)
        for (int columnOffset = 0; columnOffset < columns; columnOffset++)
            if (enemyShots[row + rowOffset, column + columnOffset])
            {
                message = "BEREITS BESCHOSSENE FELDER SIND GESPERRT";
                return false;
            }

        for (int boardRow = 0; boardRow < BoardSize; boardRow++)
        for (int boardColumn = 0; boardColumn < BoardSize; boardColumn++)
            if (playerBoard[boardRow, boardColumn] == relocationShipIndex)
                playerBoard[boardRow, boardColumn] = -1;

        ship.row = row;
        ship.column = column;
        for (int rowOffset = 0; rowOffset < rows; rowOffset++)
        for (int columnOffset = 0; columnOffset < columns; columnOffset++)
            playerBoard[row + rowOffset, column + columnOffset] = relocationShipIndex;

        message = "TITAN // SCHIFF VERSCHOBEN";
        return true;
    }

    private bool ResolveNuclearStrike(int row, int column, out string message)
    {
        if (playerShots[row, column])
        {
            message = "ZIEL BEREITS BESCHOSSEN";
            return false;
        }

        playerShots[row, column] = true;
        int shipIndex = enemyBoard[row, column];
        if (shipIndex < 0)
        {
            message = "ATOMBOMBE // KEIN KONTAKT";
            return true;
        }

        for (int boardRow = 0; boardRow < BoardSize; boardRow++)
        for (int boardColumn = 0; boardColumn < BoardSize; boardColumn++)
            if (enemyBoard[boardRow, boardColumn] == shipIndex)
                playerShots[boardRow, boardColumn] = true;
        enemyShips[shipIndex].hits = enemyShips[shipIndex].length;
        message = "ATOMBOMBE // ZIEL AUSGELÖSCHT";
        return true;
    }

    private bool ResolveRangefinderShot(int row, int column, out string message)
    {
        if (playerShots[row, column])
        {
            message = "ZIEL BEREITS BESCHOSSEN";
            return false;
        }

        playerShots[row, column] = true;
        int shipIndex = enemyBoard[row, column];
        lastRangefinderHit = shipIndex >= 0;
        if (shipIndex >= 0)
        {
            enemyShips[shipIndex].hits++;
        }

        int distance = FindNearestActiveEnemyShipDistance(row, column);
        SetRangefinderArea(row, column, distance);
        message = distance >= 0 ? "VECTOR // DISTANZ " + distance + " FELDER" : "VECTOR // KEIN AKTIVES ZIEL";
        return true;
    }

    private bool ResolveAbyssTorpedo(int row, out string message)
    {
        int resolved = 0;
        int hits = 0;
        System.Array.Clear(lastTorpedoResolvedColumns, 0, lastTorpedoResolvedColumns.Length);
        System.Array.Clear(lastTorpedoHitColumns, 0, lastTorpedoHitColumns.Length);
        for (int column = 0; column < BoardSize; column++)
        {
            if (playerShots[row, column]) continue;
            playerShots[row, column] = true;
            lastTorpedoResolvedColumns[column] = true;
            resolved++;
            int shipIndex = enemyBoard[row, column];
            if (shipIndex < 0) continue;
            enemyShips[shipIndex].hits++;
            lastTorpedoHitColumns[column] = true;
            hits++;
        }

        if (resolved == 0)
        {
            message = "DIESE REIHE WURDE BEREITS VOLLSTÄNDIG BESCHOSSEN";
            return false;
        }

        message = hits > 0 ? "ABYSS // TORPEDO " + hits + " TREFFER" : "ABYSS // TORPEDO OHNE KONTAKT";
        return true;
    }

    private bool ResolveAbyssSubmerge(int row, int column, out string message)
    {
        int shipIndex = playerBoard[row, column];
        if (shipIndex < 0)
        {
            message = "HIER LIEGT KEIN EIGENES SCHIFF";
            return false;
        }
        if (playerShips[shipIndex].hits >= playerShips[shipIndex].length)
        {
            message = "VERSENKTE SCHIFFE KÖNNEN NICHT UNTERTAUCHEN";
            return false;
        }

        submergedShipIndex = shipIndex;
        message = "ABYSS // SCHIFF UNTERGETAUCHT";
        return true;
    }

    private int FindNearestActiveEnemyShipDistance(int row, int column)
    {
        int nearest = int.MaxValue;
        for (int boardRow = 0; boardRow < BoardSize; boardRow++)
        for (int boardColumn = 0; boardColumn < BoardSize; boardColumn++)
        {
            int shipIndex = enemyBoard[boardRow, boardColumn];
            if (shipIndex < 0 || enemyShips[shipIndex].hits >= enemyShips[shipIndex].length)
            {
                continue;
            }

            nearest = Mathf.Min(nearest, Mathf.Abs(boardRow - row) + Mathf.Abs(boardColumn - column));
        }
        return nearest == int.MaxValue ? -1 : nearest;
    }

    private void SetRangefinderArea(int row, int column, int distance)
    {
        if (enemyRangefinderArea == null)
        {
            enemyRangefinderArea = new bool[BoardSize, BoardSize];
        }
        System.Array.Clear(enemyRangefinderArea, 0, enemyRangefinderArea.Length);
        lastRangefinderRow = row;
        lastRangefinderColumn = column;
        lastRangefinderDistance = distance;
        if (distance < 0)
        {
            return;
        }

        for (int boardRow = Mathf.Max(0, row - distance); boardRow <= Mathf.Min(BoardSize - 1, row + distance); boardRow++)
        for (int boardColumn = Mathf.Max(0, column - distance); boardColumn <= Mathf.Min(BoardSize - 1, column + distance); boardColumn++)
        {
            enemyRangefinderArea[boardRow, boardColumn] = true;
        }
    }

    private void ExecuteBerserkerBarrage(AbilityData ability, int effectiveCost)
    {
        List<Vector2Int> available = new List<Vector2Int>();
        for (int row = 0; row < BoardSize; row++)
        for (int column = 0; column < BoardSize; column++)
            if (!playerShots[row, column])
                available.Add(new Vector2Int(row, column));

        if (available.Count == 0)
        {
            battleMessage.text = "KEINE UNBESCHOSSENEN ZIELE";
            return;
        }

        lastBarrageShots.Clear();
        int shotCount = Mathf.Min(6, available.Count);
        int hits = 0;
        for (int shot = 0; shot < shotCount; shot++)
        {
            int choice = Random.Range(0, available.Count);
            Vector2Int target = available[choice];
            available.RemoveAt(choice);
            playerShots[target.x, target.y] = true;
            int shipIndex = enemyBoard[target.x, target.y];
            bool hit = shipIndex >= 0;
            if (hit)
            {
                enemyShips[shipIndex].hits++;
                hits++;
            }
            lastBarrageShots.Add(new BarrageShotResult { row = target.x, column = target.y, hit = hit });
        }

        points -= effectiveCost;
        activeAbility = AbilityId.None;
        battleMessage.text = hits > 0 ? "AMOKLAUF // " + hits + " TREFFER" : "AMOKLAUF // KEIN TREFFER";
        RefreshBattleUi();
        StartCoroutine(PlayAbilityFxAndFinishTurn(ability, battleMessage.text, -1, -1));
    }

    private void ExecuteRaptorAutomaticAbility(AbilityData ability, int effectiveCost)
    {
        if (ability.id == AbilityId.RaptorJetStart)
        {
            if (raptorJetLaunched)
            {
                battleMessage.text = "JET WURDE BEREITS GESTARTET";
                return;
            }
            if (!HasActiveAircraftCarrier())
            {
                battleMessage.text = "KEIN AKTIVER FLUGZEUGTRÄGER";
                return;
            }
            if (!TryMoveRaptorJet(true))
            {
                battleMessage.text = "KEIN FREIES WASSERFELD FÜR JET";
                return;
            }

            raptorJetLaunched = true;
            raptorJetActive = true;
            points -= effectiveCost;
            activeAbility = AbilityId.None;
            battleMessage.text = "RAPTOR // JET IN DER LUFT";
            RefreshBattleUi();
            StartCoroutine(PlayAbilityFxAndFinishTurn(ability, battleMessage.text, -1, -1));
            return;
        }
        else if (ability.id == AbilityId.RaptorJammer)
        {
            if (botAbilitiesJammed)
            {
                battleMessage.text = "STÖRSENDER BEREITS AKTIV";
                return;
            }

            botAbilitiesJammed = true;
            points -= effectiveCost;
            activeAbility = AbilityId.None;
            battleMessage.text = "RAPTOR // GEGNERISCHE FÄHIGKEITEN GESTÖRT";
        }
        else
        {
            return;
        }

        RefreshBattleUi();
        EndPlayerTurn();
    }

    private bool HasActiveAircraftCarrier()
    {
        if (playerShips == null || shipClasses == null) return false;
        for (int index = 0; index < playerShips.Length && index < shipClasses.Length; index++)
            if (shipClasses[index] == "ship-5" && playerShips[index].hits < playerShips[index].length)
                return true;
        return false;
    }

    private bool TryMoveRaptorJet(bool allowCurrentCell)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int row = 0; row < BoardSize; row++)
        for (int column = 0; column < BoardSize; column++)
        {
            if (playerBoard[row, column] >= 0 || enemyShots[row, column]) continue;
            if (playerMines != null && playerMines[row, column]) continue;
            if (!allowCurrentCell && row == raptorJetRow && column == raptorJetColumn) continue;
            candidates.Add(new Vector2Int(row, column));
        }
        if (candidates.Count == 0) return false;

        Vector2Int next = candidates[Random.Range(0, candidates.Count)];
        raptorJetRow = next.x;
        raptorJetColumn = next.y;
        return true;
    }

    private void AddRaptorJetOverlay(VisualElement grid)
    {
        if (!raptorJetActive || grid == null || raptorJetRow < 0 || raptorJetColumn < 0) return;
        VisualElement overlay = new VisualElement();
        overlay.pickingMode = PickingMode.Ignore;
        overlay.AddToClassList("ship-board");
        overlay.AddToClassList("raptor-jet-board");
        overlay.style.left = new Length(raptorJetColumn * 10f, LengthUnit.Percent);
        overlay.style.top = new Length(raptorJetRow * 10f, LengthUnit.Percent);
        overlay.style.width = new Length(10f, LengthUnit.Percent);
        overlay.style.height = new Length(10f, LengthUnit.Percent);
        grid.Add(overlay);
    }

    private void EndPlayerTurn()
    {
        CancelAbility();
        playerTurn = false;
        RefreshBattleUi();
        StartCoroutine(BotTurnRoutine());
    }

    private void PlayLegacyAbilityFx(AbilityData ability)
    {
        VisualElement fx = new VisualElement();
        fx.pickingMode = PickingMode.Ignore;
        fx.AddToClassList("ability-fx");
        fx.AddToClassList(ability.fxClass);

        Label label = new Label(ability.fxGlyph + "  " + ability.name);
        label.AddToClassList("ability-fx-label");
        fx.Add(label);
        battleScreen.Add(fx);

        fx.schedule.Execute(new System.Action(delegate { fx.AddToClassList("ability-fx-active"); })).StartingIn(20);
        fx.schedule.Execute(new System.Action(delegate
        {
            fx.RemoveFromClassList("ability-fx-active");
            fx.schedule.Execute(new System.Action(delegate { fx.RemoveFromHierarchy(); })).StartingIn(220);
        })).StartingIn(720);
    }

    private void DecorateCommanderMainCell(Button cell, int row, int column)
    {
        if (viewingOwnBoard)
        {
            if (submergedShipIndex >= 0 && playerBoard[row, column] == submergedShipIndex)
            {
                cell.AddToClassList("cell-submerged");
                cell.text = "≋";
            }
            if (!enemyShots[row, column] && playerMines[row, column])
            {
                cell.AddToClassList("cell-mine");
                cell.text = "◆";
            }
        }
        else
        {
            if (enemyRangefinderArea != null && enemyRangefinderArea[row, column])
            {
                cell.AddToClassList("cell-rangefinder-area");
                int minRow = Mathf.Max(0, lastRangefinderRow - lastRangefinderDistance);
                int maxRow = Mathf.Min(BoardSize - 1, lastRangefinderRow + lastRangefinderDistance);
                int minColumn = Mathf.Max(0, lastRangefinderColumn - lastRangefinderDistance);
                int maxColumn = Mathf.Min(BoardSize - 1, lastRangefinderColumn + lastRangefinderDistance);
                if (row == minRow || row == maxRow || column == minColumn || column == maxColumn)
                {
                    cell.AddToClassList("cell-rangefinder-edge");
                }
            }

            if (!playerShots[row, column] && enemyScanWater[row, column])
            {
                cell.AddToClassList("cell-scanned");
                cell.text = "·";
            }

            int shipIndex = enemyBoard[row, column];
            if (shipIndex >= 0 && enemyRevealedShips[shipIndex])
            {
                cell.AddToClassList("cell-revealed");
            }
        }
    }

    private void DecorateCommanderMiniCell(Button cell, int row, int column)
    {
        if (submergedShipIndex >= 0 && playerBoard[row, column] == submergedShipIndex)
        {
            cell.AddToClassList("cell-submerged");
            cell.text = "≋";
        }
        if (!enemyShots[row, column] && playerMines[row, column])
        {
            cell.AddToClassList("cell-mine");
            cell.text = "◆";
        }
    }

private void DrawRevealedEnemyShips()
    {
        if (viewingOwnBoard)
        {
            return;
        }

        for (int i = 0; i < enemyShips.Length; i++)
        {
            bool sunk = enemyShips[i].hits >= enemyShips[i].length;
            if (enemyRevealedShips[i] || sunk)
            {
                AddShipOverlay(mainBattleGrid, enemyShips[i], shipClasses[i], sunk);
            }
        }
    }

    private bool ConsumeBonusShotIfActive()
    {
        if (bonusShotsRemaining <= 0)
        {
            return false;
        }

        bonusShotsRemaining--;
        if (bonusShotsRemaining > 0)
        {
            playerTurn = true;
            battleMessage.text = "MINE // NOCH " + bonusShotsRemaining + " FREISCHÜSSE";
            RefreshBattleUi();
            return true;
        }

        bonusShotsSuppressPoints = false;
        return false;
    }
}
