using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private const string TutorialCompletedKey = "naval-command.tutorial-completed.v1";

    private enum TutorialStage
    {
        None, Welcome, FleetPlacement, FleetReady, FirstShot, Points,
        AbilitySelect, AbilityTarget, FinalBattle, Complete
    }

    private VisualElement tutorialOverlay;
    private VisualElement tutorialScrim;
    private VisualElement tutorialCallout;
    private Label tutorialTitle;
    private Label tutorialBody;
    private Label tutorialTask;
    private Button tutorialContinueButton;
    private Button tutorialSkipButton;
    private Button tutorialMenuButton;
    private Button tutorialReplayButton;
    private VisualElement tutorialPointsPanel;
    private VisualElement tutorialAbilityButtons;
    private TutorialStage tutorialStage;
    private bool tutorialActive;
    private bool tutorialAdvancePending;

    private void CacheTutorialUi(VisualElement root)
    {
        tutorialOverlay = root.Q<VisualElement>("TutorialOverlay");
        tutorialScrim = root.Q<VisualElement>("TutorialScrim");
        tutorialCallout = root.Q<VisualElement>("TutorialCallout");
        tutorialTitle = root.Q<Label>("TutorialTitle");
        tutorialBody = root.Q<Label>("TutorialBody");
        tutorialTask = root.Q<Label>("TutorialTask");
        tutorialContinueButton = root.Q<Button>("TutorialContinueButton");
        tutorialSkipButton = root.Q<Button>("TutorialSkipButton");
        tutorialMenuButton = root.Q<Button>("TutorialButton");
        tutorialReplayButton = root.Q<Button>("TutorialReplayButton");
        tutorialPointsPanel = root.Q<VisualElement>(className: "points-panel");
        tutorialAbilityButtons = root.Q<VisualElement>(className: "ability-buttons");
        tutorialOverlay.pickingMode = PickingMode.Ignore;
        tutorialScrim.pickingMode = PickingMode.Position;
        tutorialCallout.pickingMode = PickingMode.Ignore;
        tutorialContinueButton.pickingMode = PickingMode.Position;
    }

    private void BindTutorialUi()
    {
        tutorialContinueButton.clicked += AdvanceTutorialFromCard;
        tutorialSkipButton.clicked += SkipTutorial;
        tutorialMenuButton.clicked += BeginTutorial;
        tutorialReplayButton.clicked += BeginTutorial;
    }

    private void StartTutorialIfNeeded()
    {
        if (PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 0) BeginTutorial();
    }

    private void BeginTutorial()
    {
        StopAllCoroutines();
        onlineFlowMode = OnlineFlowMode.None;
        tutorialActive = true;
        tutorialSkipButton.RemoveFromClassList("hidden");
        tutorialAdvancePending = false;
        tutorialStage = TutorialStage.Welcome;
        currentCommander = CreateTutorialCommander();
        ApplyCommanderFleet();
        ShowOnly(menuScreen);
        SetTutorialChromeVisible(false);
        ShowTutorialCard(
            "WILLKOMMEN AN BORD",
            "Willkommen an Bord – los geht's!",
            "AUSBILDUNG // 1 VON 5", "LOS GEHT'S", true);
    }

    private CommanderData CreateTutorialCommander()
    {
        return new CommanderData
        {
            id = "tutorial-commander",
            name = "AUSBILDUNGSFLOTTE",
            rank = "KONRAD WEISS // TRAINING",
            portraitClass = "portrait-standard",
            monogram = "KW",
            shipLengths = new[] { 5, 4, 3, 3, 2 },
            shipWidths = new[] { 5, 4, 3, 3, 2 },
            shipHeights = new[] { 1, 1, 1, 1, 1 },
            shipClasses = new[] { "ship-5", "ship-4b", "ship-3a", "ship-3b", "ship-2" },
            abilities = new[]
            {
                Ability(AbilityId.StandardBarrage, "SPERRFEUER", "SPERRFEUER", 3, false, "tutorial-barrage", "fx-barrage", "✦"),
                Ability(AbilityId.StandardLine, "LINIENFEUER", "LINIENFEUER", 4, false, "tutorial-line", "fx-line", "═")
            }
        };
    }

    private void AdvanceTutorialFromCard()
    {
        if (!tutorialActive) return;
        switch (tutorialStage)
        {
            case TutorialStage.Welcome:
                tutorialStage = TutorialStage.FleetPlacement;
                ShowSetup();
                SetTutorialChromeVisible(false);
                ShowTutorialCard("DEINE FLOTTE",
                    "Drück jetzt ZUFALL.",
                    "TEST 1 // FLOTTE AUFSTELLEN", null, false);
                tutorialCallout.AddToClassList("tutorial-callout-right");
                HighlightTutorialTarget(randomizeFleetButton);
                break;
            case TutorialStage.Points:
                tutorialStage = TutorialStage.AbilitySelect;
                HideTutorialCard();
                SetTutorialChromeVisible(true, false);
                HighlightTutorialTarget(abilityOneButton);
                ShowTutorialCard("FÄHIGKEIT BEREIT",
                    "Drück SPERRFEUER.",
                    "TEST 4 // FÄHIGKEIT WÄHLEN", null, false);
                break;
            case TutorialStage.FinalBattle:
                HideTutorialCard();
                ClearTutorialHighlights();
                SetTutorialChromeVisible(true, true);
                break;
            case TutorialStage.Complete:
                FinishTutorial();
                break;
        }
    }

    private void OnTutorialFleetRandomized()
    {
        if (!tutorialActive || tutorialStage != TutorialStage.FleetPlacement) return;
        tutorialStage = TutorialStage.FleetReady;
        ClearTutorialHighlights();
        HighlightTutorialTarget(beginBattleButton);
        ShowTutorialCard("FLOTTE BEREIT",
            "Starte jetzt das Gefecht.",
            "TEST 2 // EINSATZ STARTEN", null, false);
    }

    private void OnTutorialBattleStarted()
    {
        if (!tutorialActive || tutorialStage != TutorialStage.FleetReady) return;
        tutorialStage = TutorialStage.FirstShot;
        ClearTutorialHighlights();
        SetTutorialChromeVisible(false);
        HighlightTutorialTarget(mainBattleGrid);
        ShowTutorialCard("FEUER FREI",
            "Wähle ein Feld und feuere.",
            "TEST 3 // ERSTEN SCHUSS ABGEBEN", null, false);
    }

    private void OnTutorialFirstShotResolved()
    {
        if (!tutorialActive || tutorialStage != TutorialStage.FirstShot) return;
        tutorialAdvancePending = true;
        ClearTutorialHighlights();
        HideTutorialCard();
    }

    private void OnTutorialFirstShotFired()
    {
        if (tutorialActive && tutorialStage == TutorialStage.FirstShot)
        {
            HideTutorialCard();
            ClearTutorialHighlights();
        }
    }

    private void OnTutorialAbilitySelected()
    {
        if (!tutorialActive || tutorialStage != TutorialStage.AbilitySelect) return;
        tutorialStage = TutorialStage.AbilityTarget;
        SetTutorialChromeVisible(true, false);
        ClearTutorialHighlights();
        HighlightTutorialTarget(mainBattleGrid);
        ShowTutorialCard("SEKTOR MARKIEREN",
            "Wähle einen 2×2-Sektor.",
            "TEST 4 // ZIEL BESTÄTIGEN", null, false);
    }

    private void OnTutorialAbilityResolved()
    {
        if (!tutorialActive || tutorialStage != TutorialStage.AbilityTarget) return;
        tutorialAdvancePending = true;
        ClearTutorialHighlights();
        HideTutorialCard();
    }

    private void OnTutorialPlayerTurnReady()
    {
        if (!tutorialActive || !tutorialAdvancePending) return;
        tutorialAdvancePending = false;
        if (tutorialStage == TutorialStage.FirstShot)
        {
            tutorialStage = TutorialStage.Points;
            points = Mathf.Max(points, 3);
            RefreshBattleUi();
            SetTutorialChromeVisible(true, false);
            HighlightTutorialTarget(tutorialPointsPanel);
            ShowTutorialCard("PUNKTE FREIGESCHALTET",
                "Fehlschüsse geben Punkte – hier sind drei zum Üben.",
                "LEKTION 4 VON 5 // PUNKTE", "FÄHIGKEIT TESTEN", true);
        }
        else if (tutorialStage == TutorialStage.AbilityTarget)
        {
            tutorialStage = TutorialStage.FinalBattle;
            points = Mathf.Max(points, 4);
            RefreshBattleUi();
            SetTutorialChromeVisible(true, true);
            ShowTutorialCard("DER FINALE TEST",
                "Versenke jetzt die feindliche Flotte.",
                "TEST 5 // GEFECHT BEENDEN", "TEST STARTEN", true);
        }
    }

    private void OnTutorialMatchEnded(bool playerWon)
    {
        if (!tutorialActive || tutorialStage != TutorialStage.FinalBattle) return;
        tutorialStage = TutorialStage.Complete;
        PlayerPrefs.SetInt(TutorialCompletedKey, 1);
        PlayerPrefs.Save();
        ClearTutorialHighlights();
        ShowTutorialCard(
            playerWon ? "AUSBILDUNG BESTANDEN" : "AUSBILDUNG ABGESCHLOSSEN",
            playerWon
                ? "Gut gemacht – die Brücke gehört dir."
                : "Gut gekämpft – beim nächsten Mal klappt es.",
            "KONRAD WEISS // ENDE DER AUSBILDUNG", "ZUM HAUPTMENÜ", true);
    }

    private void SkipTutorial()
    {
        if (!tutorialActive) return;
        PlayerPrefs.SetInt(TutorialCompletedKey, 1);
        PlayerPrefs.Save();
        FinishTutorial();
    }

    private void FinishTutorial()
    {
        tutorialActive = false;
        tutorialSkipButton.AddToClassList("hidden");
        tutorialStage = TutorialStage.None;
        tutorialAdvancePending = false;
        HideTutorialCard();
        ClearTutorialHighlights();
        SetTutorialChromeVisible(true, true);
        currentCommander = commanderCatalog[0];
        ApplyCommanderFleet();
        ShowMenu();
    }

    private void ShowTutorialCard(string title, string body, string task, string buttonText, bool modal)
    {
        tutorialTitle.text = title;
        tutorialBody.text = body;
        tutorialTask.text = task;
        tutorialContinueButton.text = buttonText ?? string.Empty;
        tutorialContinueButton.EnableInClassList("hidden", string.IsNullOrEmpty(buttonText));
        tutorialScrim.EnableInClassList("hidden", !modal);
        tutorialCallout.EnableInClassList("tutorial-callout-modal", modal);
        tutorialCallout.RemoveFromClassList("tutorial-callout-right");
        tutorialOverlay.RemoveFromClassList("hidden");
    }

    private void HideTutorialCard() => tutorialOverlay.AddToClassList("hidden");

    private IEnumerator AnnounceTutorialEnemyShot()
    {
        if (!tutorialActive || tutorialStage != TutorialStage.FirstShot)
        {
            yield break;
        }

        ShowTutorialCard("ACHTUNG!", "Jetzt feuert der Gegner.", "ERSTER GEGENZUG", null, false);
        yield return new WaitForSecondsRealtime(1.35f);
        HideTutorialCard();
    }

    private void SetTutorialChromeVisible(bool showPoints, bool showAllAbilities = false)
    {
        if (tutorialPointsPanel != null) tutorialPointsPanel.style.display = showPoints ? DisplayStyle.Flex : DisplayStyle.None;
        if (tutorialAbilityButtons != null) tutorialAbilityButtons.style.display = showPoints ? DisplayStyle.Flex : DisplayStyle.None;
        if (abilityOneButton != null) abilityOneButton.style.display = showPoints ? DisplayStyle.Flex : DisplayStyle.None;
        if (abilityTwoButton != null) abilityTwoButton.style.display = showAllAbilities ? DisplayStyle.Flex : DisplayStyle.None;
        setupBackButton.style.display = tutorialActive ? DisplayStyle.None : DisplayStyle.Flex;
        battleBackButton.style.display = tutorialActive ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void HighlightTutorialTarget(VisualElement target)
    {
        ClearTutorialHighlights();
        target?.AddToClassList("tutorial-highlight");
    }

    private void ClearTutorialHighlights()
    {
        randomizeFleetButton?.RemoveFromClassList("tutorial-highlight");
        beginBattleButton?.RemoveFromClassList("tutorial-highlight");
        mainBattleGrid?.RemoveFromClassList("tutorial-highlight");
        abilityOneButton?.RemoveFromClassList("tutorial-highlight");
        abilityTwoButton?.RemoveFromClassList("tutorial-highlight");
        tutorialPointsPanel?.RemoveFromClassList("tutorial-highlight");
    }
}
