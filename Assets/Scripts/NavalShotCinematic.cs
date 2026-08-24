using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private static readonly string[] ShotCinematicStateClasses =
    {
        "shot-visible",
        "shot-cut",
        "shot-incoming",
        "shot-terminal",
        "shot-impact-hit",
        "shot-impact-miss",
        "shot-jet-active",
        "shot-jet-targeted",
        "shot-jet-struck",
        "shot-finish",
        "shot-shake-a",
        "shot-shake-b",
        "shot-shake-c"
    };

    private VisualElement shotCinematic;
    private VisualElement shotImpactAnchor;
    private VisualElement shotTerminalMissile;
    private VisualElement shotRaptorJet;
    private Label shotResultLabel;
    private Label shotCoordinateLabel;
    private bool shotCinematicPlaying;
    private bool nextShotShowsJet;
    private bool nextShotHitsJet;

    private void CacheShotCinematicUi(VisualElement root)
    {
        shotCinematic = root.Q<VisualElement>("ShotCinematic");
        shotImpactAnchor = root.Q<VisualElement>("ShotImpactAnchor");
        shotTerminalMissile = root.Q<VisualElement>("ShotTerminalMissile");
        shotRaptorJet = root.Q<VisualElement>("ShotRaptorJet");
        shotResultLabel = root.Q<Label>("ShotResultLabel");
        shotCoordinateLabel = root.Q<Label>("ShotCoordinateLabel");
        HideShotCinematicImmediate();
    }

    private void StartNormalShotCinematic(bool hit, bool sunk, int row, int column)
    {
        if (shotCinematicPlaying)
        {
            return;
        }

        shotCinematicPlaying = true;
        StartCoroutine(PlayNormalShotAndContinue(hit, sunk, row, column));
    }

    private IEnumerator PlayNormalShotAndContinue(bool hit, bool sunk, int row, int column)
    {
        yield return StartCoroutine(PlayNormalShotCinematic(hit, sunk, row, column));
        shotCinematicPlaying = false;

        if (IsActiveOnlineMatch)
        {
            RefreshOnlineBattle(onlineMatchView);
            yield break;
        }

        RefreshBattleUi();
        OnTutorialFirstShotResolved();

        if (AllShipsSunk(enemyShips))
        {
            EndGame(true);
            yield break;
        }

        if (ConsumeBonusShotIfActive())
        {
            yield break;
        }

        EndPlayerTurn();
    }

    private IEnumerator PlayNormalShotCinematic(bool hit, bool sunk, int row, int column)
    {
        ResetShotCinematicClasses();
        ApplyShotJetState();
        PrepareShotJetFlight();
        ConfigureShotImpact(hit, sunk, row, column);

        shotCinematic.RemoveFromClassList("hidden");
        yield return null;

        shotCinematic.AddToClassList("shot-visible");
        shotCinematic.AddToClassList("shot-cut");
        yield return null;
        if (!nextShotHitsJet) StartShotJetFlight();
        yield return new WaitForSecondsRealtime(0.28f);

        shotCinematic.AddToClassList("shot-terminal");
        yield return StartCoroutine(PlayShotJetTerminalApproach());

        if (nextShotHitsJet) ResolveShotJetImpact();
        if (hit) PlayExplosionSfx();
        shotCinematic.AddToClassList(hit ? "shot-impact-hit" : "shot-impact-miss");
        if (hit)
        {
            yield return StartCoroutine(PlayImpactShake());
        }
        else
        {
            SetShotShakeClass("shot-shake-c");
            yield return new WaitForSecondsRealtime(0.08f);
            SetShotShakeClass(null);
        }

        yield return new WaitForSecondsRealtime(0.7f);

        shotCinematic.AddToClassList("shot-finish");
        yield return new WaitForSecondsRealtime(0.24f);
        HideShotCinematicImmediate();
    }

    private IEnumerator PlayEnemyNormalShotCinematic(bool hit, int row, int column)
    {
        shotCinematicPlaying = true;
        ResetShotCinematicClasses();
        ApplyShotJetState();
        PrepareShotJetFlight();
        ConfigureIncomingShotImpact(hit, row, column);

        shotCinematic.RemoveFromClassList("hidden");
        yield return null;

        shotCinematic.AddToClassList("shot-visible");
        shotCinematic.AddToClassList("shot-incoming");
        shotCinematic.AddToClassList("shot-cut");
        yield return null;
        if (!nextShotHitsJet) StartShotJetFlight();
        yield return new WaitForSecondsRealtime(0.42f);

        shotCinematic.AddToClassList("shot-terminal");
        yield return StartCoroutine(PlayShotJetTerminalApproach());

        if (nextShotHitsJet) ResolveShotJetImpact();
        if (hit) PlayExplosionSfx();
        shotCinematic.AddToClassList(hit ? "shot-impact-hit" : "shot-impact-miss");
        if (hit)
        {
            yield return StartCoroutine(PlayImpactShake());
        }
        else
        {
            SetShotShakeClass("shot-shake-c");
            yield return new WaitForSecondsRealtime(0.08f);
            SetShotShakeClass(null);
        }

        yield return new WaitForSecondsRealtime(0.7f);
        shotCinematic.AddToClassList("shot-finish");
        yield return new WaitForSecondsRealtime(0.24f);
        HideShotCinematicImmediate();
    }

    private IEnumerator PlayStandardBarrageCinematic(string result)
    {
        return PlayStandardPatternCinematic(lastStandardBarrageShots, result, null);
    }

    private IEnumerator PlayStandardLineCinematic(string result)
    {
        return PlayStandardPatternCinematic(
            lastStandardLineShots,
            result,
            lineVertical ? "standard-line-vertical" : "standard-line-horizontal");
    }

    private IEnumerator PlayStandardPatternCinematic(List<BarrageShotResult> shots, string result, string layoutClass)
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-vector-barrage");
        if (!string.IsNullOrEmpty(layoutClass)) cinematic.AddToClassList(layoutClass);
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);
        panel.Add(CreateAbilityElement("vector-target-ship"));

        for (int index = 0; index < shots.Count; index++)
        {
            BarrageShotResult shot = shots[index];
            VisualElement shotElement = CreateAbilityElement("vector-barrage-shot");
            shotElement.AddToClassList("vector-barrage-shot-" + index);
            shotElement.AddToClassList(shot.hit ? "vector-barrage-will-hit" : "vector-barrage-will-miss");
            shotElement.Add(CreateAbilityElement("vector-barrage-missile"));
            shotElement.Add(CreateAbilityElement("vector-barrage-explosion"));
            shotElement.Add(CreateAbilityElement("vector-barrage-splash"));
            panel.Add(shotElement);
        }
        panel.Add(CreateAbilityLabel(result, "ability-cinematic-result"));

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.25f);
        for (int index = 0; index < shots.Count; index++)
        {
            cinematic.AddToClassList("vector-barrage-fire-" + index);
            yield return new WaitForSecondsRealtime(0.37f);
            if (shots[index].hit) PlayExplosionSfx();
            cinematic.AddToClassList("vector-barrage-impact-" + index);
            yield return new WaitForSecondsRealtime(0.13f);
        }
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(1.1f);
        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private void ConfigureShotImpact(bool hit, bool sunk, int row, int column)
    {
        float impactX;
        if (nextShotHitsJet)
        {
            impactX = 52f;
        }
        else if (hit)
        {
            impactX = 48f + column * 0.45f;
        }
        else if (column < BoardSize / 2)
        {
            impactX = 17f + column * 2.6f;
        }
        else
        {
            impactX = 83f - (BoardSize - 1 - column) * 2.6f;
        }

        float impactY = nextShotHitsJet ? 30f : hit ? 48.5f : 50f;
        shotImpactAnchor.style.left = new Length(impactX, LengthUnit.Percent);
        shotImpactAnchor.style.top = new Length(impactY, LengthUnit.Percent);
        shotTerminalMissile.style.left = new Length(impactX, LengthUnit.Percent);

        shotResultLabel.text = nextShotHitsJet ? "JET ABGESCHOSSEN" : sunk ? "SCHIFF ZERSTÖRT" : hit ? "TREFFER BESTÄTIGT" : "KEIN KONTAKT";
        shotCoordinateLabel.text = nextShotHitsJet
            ? "RAPTOR-KONTAKT // TREFFER BESTÄTIGT"
            : "RASTER " + GridCoordinate(row, column) + (hit ? " // RUMPFTREFFER" : " // WASSEREINSCHLAG");
    }

    private void ConfigureIncomingShotImpact(bool hit, int row, int column)
    {
        int clampedColumn = Mathf.Clamp(column, 0, BoardSize - 1);
        float impactX;
        if (nextShotHitsJet)
        {
            impactX = 52f;
        }
        else if (hit)
        {
            impactX = 35f + clampedColumn * (30f / (BoardSize - 1));
        }
        else if (clampedColumn < BoardSize / 2)
        {
            impactX = 5f + clampedColumn * 1.2f;
        }
        else
        {
            impactX = 95f - (BoardSize - 1 - clampedColumn) * 1.2f;
        }
        float impactY = nextShotHitsJet ? 30f : 50f;
        shotImpactAnchor.style.left = new Length(impactX, LengthUnit.Percent);
        shotImpactAnchor.style.top = new Length(impactY, LengthUnit.Percent);
        shotTerminalMissile.style.left = new Length(impactX, LengthUnit.Percent);

        shotResultLabel.text = nextShotHitsJet ? "EIGENER JET GETROFFEN" : hit ? "GEGNERISCHER TREFFER" : "GEGNER VERFEHLT";
        shotCoordinateLabel.text = nextShotHitsJet
            ? "RAPTOR-KONTAKT // JET VERLOREN"
            : "EIGENES RASTER " + GridCoordinate(row, column) +
              (hit ? " // RUMPFTREFFER" : " // WASSEREINSCHLAG");
    }

    private void ConfigureNextShotJet(bool visible, bool hit)
    {
        nextShotShowsJet = visible || hit;
        nextShotHitsJet = hit;
    }

    private void ApplyShotJetState()
    {
        if (nextShotShowsJet) shotCinematic.AddToClassList("shot-jet-active");
        if (nextShotHitsJet) shotCinematic.AddToClassList("shot-jet-targeted");
    }

    private void PrepareShotJetFlight()
    {
        if (shotRaptorJet == null)
        {
            return;
        }

        shotRaptorJet.style.left = new Length(-34f, LengthUnit.Percent);
        shotRaptorJet.style.opacity = nextShotShowsJet ? 1f : 0f;
    }

    private void StartShotJetFlight()
    {
        if (!nextShotShowsJet || shotRaptorJet == null)
        {
            return;
        }

        PlayJetFlybySfx();
        shotRaptorJet.style.left = new Length(112f, LengthUnit.Percent);
    }

    private IEnumerator PlayShotJetTerminalApproach()
    {
        if (!nextShotHitsJet)
        {
            yield return new WaitForSecondsRealtime(0.88f);
            yield break;
        }

        yield return new WaitForSecondsRealtime(0.625f);
        StartShotJetFlight();
        yield return new WaitForSecondsRealtime(0.255f);
    }

    private void ResolveShotJetImpact()
    {
        shotCinematic.AddToClassList("shot-jet-struck");
        if (shotRaptorJet == null)
        {
            return;
        }

        shotRaptorJet.style.left = new Length(125f, LengthUnit.Percent);
        shotRaptorJet.style.top = new Length(58f, LengthUnit.Percent);
        shotRaptorJet.style.opacity = StyleKeyword.Null;
        if (shotImpactAnchor != null)
        {
            shotImpactAnchor.style.left = new Length(141f, LengthUnit.Percent);
            shotImpactAnchor.style.top = new Length(65f, LengthUnit.Percent);
        }
    }

    private IEnumerator PlayImpactShake()
    {
        string[] sequence =
        {
            "shot-shake-a",
            "shot-shake-b",
            "shot-shake-c",
            "shot-shake-b",
            "shot-shake-a",
            null
        };

        for (int i = 0; i < sequence.Length; i++)
        {
            SetShotShakeClass(sequence[i]);
            yield return new WaitForSecondsRealtime(0.045f);
        }
    }

    private void SetShotShakeClass(string className)
    {
        shotCinematic.RemoveFromClassList("shot-shake-a");
        shotCinematic.RemoveFromClassList("shot-shake-b");
        shotCinematic.RemoveFromClassList("shot-shake-c");

        if (!string.IsNullOrEmpty(className))
        {
            shotCinematic.AddToClassList(className);
        }
    }

    private string GridCoordinate(int row, int column)
    {
        char columnLetter = (char)('A' + Mathf.Clamp(column, 0, BoardSize - 1));
        return columnLetter + (Mathf.Clamp(row, 0, BoardSize - 1) + 1).ToString("00");
    }

    private void ResetShotCinematicClasses()
    {
        if (shotCinematic == null)
        {
            return;
        }

        for (int i = 0; i < ShotCinematicStateClasses.Length; i++)
        {
            shotCinematic.RemoveFromClassList(ShotCinematicStateClasses[i]);
        }
    }

    private void HideShotCinematicImmediate()
    {
        shotCinematicPlaying = false;

        if (shotCinematic == null)
        {
            return;
        }

        ResetShotCinematicClasses();
        shotCinematic.AddToClassList("hidden");
        if (shotRaptorJet != null)
        {
            shotRaptorJet.style.left = StyleKeyword.Null;
            shotRaptorJet.style.top = StyleKeyword.Null;
            shotRaptorJet.style.opacity = StyleKeyword.Null;
        }
        nextShotShowsJet = false;
        nextShotHitsJet = false;
    }
}
