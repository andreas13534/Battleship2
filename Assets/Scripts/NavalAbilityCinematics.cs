using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private sealed class ViperParticleFx
    {
        public GameObject root;
        public RenderTexture renderTexture;
        public Material material;
        public Texture2D particleTexture;
        public ParticleSystem particles;
    }

    private ViperParticleFx activeViperParticleFx;

    private IEnumerator PlayAbilityFxAndFinishTurn(AbilityData ability, string result, int row, int column)
    {
        abilityCinematicPlaying = true;
        UpdateAbilityButtons();

        yield return StartCoroutine(PlayAbilityFx(ability, result, row, column));

        abilityCinematicPlaying = false;
        if (IsActiveOnlineMatch)
        {
            RefreshOnlineBattle(onlineMatchView);
            yield break;
        }
        if (AllShipsSunk(enemyShips))
        {
            EndGame(true);
        }
        else
        {
            EndPlayerTurn();
        }
    }

    private IEnumerator PlayAbilityFx(AbilityData ability, string result, int row, int column)
    {
        if (ability.id == AbilityId.StandardBarrage)
        {
            yield return StartCoroutine(PlayStandardBarrageCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.StandardLine)
        {
            yield return StartCoroutine(PlayStandardLineCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.OracleScan)
        {
            yield return StartCoroutine(PlayOracleCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.AresRepair)
        {
            yield return StartCoroutine(PlayAresCinematic(result, row, column));
            yield break;
        }

        if (ability.id == AbilityId.ChemicalBomb)
        {
            yield return StartCoroutine(PlayViperChemicalCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.MineLayer)
        {
            yield return StartCoroutine(PlayViperMineCinematic());
            yield break;
        }

        if (ability.id == AbilityId.FleetRelocation)
        {
            yield return StartCoroutine(PlayTitanRelocationCinematic());
            yield break;
        }

        if (ability.id == AbilityId.NuclearStrike)
        {
            yield return StartCoroutine(PlayTitanNuclearCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.RangefinderShot)
        {
            yield return StartCoroutine(PlayVectorRangefinderCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.BerserkerBarrage)
        {
            yield return StartCoroutine(PlayVectorBarrageCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.AbyssTorpedo)
        {
            yield return StartCoroutine(PlayAbyssTorpedoCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.AbyssSubmerge)
        {
            yield return StartCoroutine(PlayAbyssSubmergeCinematic(result));
            yield break;
        }

        if (ability.id == AbilityId.RaptorJetStart)
        {
            yield return StartCoroutine(PlayRaptorJetStartCinematic(result));
            yield break;
        }

        PlayLegacyAbilityFx(ability);
        yield return new WaitForSecondsRealtime(0.95f);
    }

    private IEnumerator PlayAbyssTorpedoCinematic(string result)
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-abyss-torpedo");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);

        VisualElement torpedo = CreateAbilityElement("abyss-torpedo");
        panel.Add(torpedo);

        for (int impactIndex = 0; impactIndex < BoardSize; impactIndex++)
        {
            if (!lastTorpedoResolvedColumns[impactIndex]) continue;
            VisualElement impact = CreateAbilityElement("abyss-row-impact");
            impact.AddToClassList("abyss-row-impact-" + impactIndex);
            impact.AddToClassList(lastTorpedoHitColumns[impactIndex] ? "abyss-impact-hit" : "abyss-impact-splash");
            panel.Add(impact);
        }

        Label resultLabel = CreateAbilityLabel(result, "ability-cinematic-result");
        panel.Add(resultLabel);
        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        cinematic.AddToClassList("abyss-torpedo-armed");
        yield return new WaitForSecondsRealtime(0.32f);
        cinematic.AddToClassList("abyss-torpedo-running");
        yield return new WaitForSecondsRealtime(1.05f);
        if (System.Array.IndexOf(lastTorpedoHitColumns, true) >= 0) PlayExplosionSfx();
        cinematic.AddToClassList("abyss-torpedo-impact");
        yield return new WaitForSecondsRealtime(1.62f);
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(1.05f);
        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayAbyssSubmergeCinematic(string result)
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-abyss-submerge");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);
        panel.Add(CreateAbilityElement("abyss-submerge-ship"));

        Label resultLabel = CreateAbilityLabel(result, "ability-cinematic-result");
        panel.Add(resultLabel);
        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        cinematic.AddToClassList("abyss-submerge-active");
        yield return new WaitForSecondsRealtime(0.36f);
        cinematic.RemoveFromClassList("abyss-submerge-active");
        yield return new WaitForSecondsRealtime(0.2f);
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(0.9f);
        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayRaptorJetStartCinematic(string result)
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-raptor-jet-start");
        cinematic.Add(CreateAbilityElement("raptor-command-deck-backdrop"));
        cinematic.Add(CreateAbilityElement("raptor-command-deck-grade"));

        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);
        panel.Add(CreateAbilityElement("raptor-window-jet"));
        panel.Add(CreateAbilityElement("raptor-command-deck-interior"));
        panel.Add(CreateAbilityLabel("RAPTOR // DECKSTART", "raptor-jet-telemetry"));

        panel.Add(CreateAbilityLabel(result, "ability-cinematic-result"));
        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.22f);
        PlayJetFlybySfx();
        cinematic.AddToClassList("raptor-pass-active");
        yield return new WaitForSecondsRealtime(0.575f);
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(0.86f);
        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayVectorRangefinderCinematic(string result)
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-vector-rangefinder");
        cinematic.AddToClassList(lastRangefinderHit ? "vector-will-hit" : "vector-will-miss");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);
        panel.Add(CreateAbilityElement("vector-target-ship"));

        VisualElement missile = CreateAbilityElement("vector-missile");
        missile.Add(CreateAbilityElement("vector-missile-trail"));
        missile.Add(CreateAbilityElement("vector-missile-flame"));
        missile.Add(CreateAbilityElement("vector-missile-sprite"));
        panel.Add(missile);
        VisualElement impact = CreateAbilityElement("vector-impact");
        impact.Add(CreateAbilityElement("vector-hit-explosion"));
        impact.Add(CreateAbilityElement("vector-miss-splash"));
        VisualElement firstRing = CreateAbilityElement("vector-scan-ring");
        firstRing.AddToClassList("vector-scan-ring-one");
        impact.Add(firstRing);
        VisualElement secondRing = CreateAbilityElement("vector-scan-ring");
        secondRing.AddToClassList("vector-scan-ring-two");
        impact.Add(secondRing);
        panel.Add(impact);
        string distanceText = lastRangefinderDistance >= 0 ? lastRangefinderDistance + " FELDER" : "KEIN AKTIVES ZIEL";
        panel.Add(CreateAbilityLabel(distanceText, "vector-distance-result"));
        panel.Add(CreateAbilityLabel(result, "ability-cinematic-result"));

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.22f);
        cinematic.AddToClassList("vector-shot-incoming");
        yield return new WaitForSecondsRealtime(0.82f);
        if (lastRangefinderHit) PlayExplosionSfx();
        cinematic.AddToClassList(lastRangefinderHit ? "vector-impact-hit" : "vector-impact-miss");
        yield return new WaitForSecondsRealtime(lastRangefinderHit ? 0.38f : 0.62f);
        cinematic.AddToClassList("vector-scan-complete");
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(1.45f);
        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayVectorBarrageCinematic(string result)
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-vector-barrage");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);
        panel.Add(CreateAbilityElement("vector-target-ship"));

        for (int index = 0; index < lastBarrageShots.Count; index++)
        {
            VisualElement shot = CreateAbilityElement("vector-barrage-shot");
            shot.AddToClassList("vector-barrage-shot-" + index);
            shot.AddToClassList(lastBarrageShots[index].hit ? "vector-barrage-will-hit" : "vector-barrage-will-miss");
            shot.Add(CreateAbilityElement("vector-barrage-missile"));
            shot.Add(CreateAbilityElement("vector-barrage-explosion"));
            shot.Add(CreateAbilityElement("vector-barrage-splash"));
            panel.Add(shot);
        }
        panel.Add(CreateAbilityLabel(result, "ability-cinematic-result"));

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.25f);
        for (int index = 0; index < lastBarrageShots.Count; index++)
        {
            cinematic.AddToClassList("vector-barrage-fire-" + index);
            yield return new WaitForSecondsRealtime(0.37f);
            if (lastBarrageShots[index].hit) PlayExplosionSfx();
            cinematic.AddToClassList("vector-barrage-impact-" + index);
            yield return new WaitForSecondsRealtime(0.13f);
        }
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(1.1f);
        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayTitanRelocationCinematic()
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-titan-relocation");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);

        VisualElement ship = CreateAbilityElement("titan-relocation-ship");
        panel.Add(ship);
        panel.Add(CreateAbilityElement("titan-teleport-ring"));
        Label resultLabel = CreateAbilityLabel("TITAN // SCHIFF VERSCHOBEN", "ability-cinematic-result");
        panel.Add(resultLabel);

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.55f);
        cinematic.AddToClassList("titan-relocation-vanish");
        yield return new WaitForSecondsRealtime(0.24f);
        cinematic.AddToClassList("titan-relocation-complete");
        yield return new WaitForSecondsRealtime(0.16f);
        cinematic.RemoveFromClassList("titan-relocation-vanish");
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(1.15f);
        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayTitanNuclearCinematic(string result)
    {
        bool hit = result.Contains("AUSGELÖSCHT");
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-titan-nuclear");
        cinematic.AddToClassList(hit ? "titan-nuclear-will-hit" : "titan-nuclear-will-miss");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);
        panel.Add(CreateAbilityElement("titan-target-ship"));

        VisualElement missile = CreateAbilityElement("titan-nuclear-missile");
        missile.Add(CreateAbilityElement("titan-nuclear-trail"));
        missile.Add(CreateAbilityElement("titan-nuclear-flame"));
        for (int particleIndex = 0; particleIndex < 5; particleIndex++)
        {
            VisualElement particle = CreateAbilityElement("titan-nuclear-particle");
            particle.AddToClassList("titan-nuclear-particle-" + particleIndex);
            missile.Add(particle);
        }
        missile.Add(CreateAbilityElement("titan-nuclear-missile-sprite"));
        panel.Add(missile);
        VisualElement impact = CreateAbilityElement("titan-nuclear-impact");
        impact.Add(CreateAbilityElement("titan-nuclear-splash"));
        panel.Add(impact);
        panel.Add(CreateAbilityElement("titan-nuclear-flash"));
        panel.Add(CreateAbilityElement("titan-nuclear-mushroom"));
        VisualElement firestorm = CreateAbilityElement("titan-nuclear-firestorm");
        for (int emberIndex = 0; emberIndex < 18; emberIndex++)
        {
            VisualElement ember = CreateAbilityElement("titan-fire-ember");
            ember.AddToClassList("titan-fire-ember-" + emberIndex);
            firestorm.Add(ember);
        }
        panel.Add(firestorm);
        Label resultLabel = CreateAbilityLabel(hit ? "ATOMBOMBE // ZIEL AUSGELÖSCHT" : "ATOMBOMBE // KEIN KONTAKT", "ability-cinematic-result");
        panel.Add(resultLabel);

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.3f);
        cinematic.AddToClassList("titan-nuclear-incoming");
        yield return new WaitForSecondsRealtime(0.92f);
        if (hit) PlayExplosionSfx();
        cinematic.AddToClassList(hit ? "titan-nuclear-hit" : "titan-nuclear-miss");
        if (hit)
        {
            yield return new WaitForSecondsRealtime(0.14f);
            cinematic.AddToClassList("titan-nuclear-aftermath");
            yield return new WaitForSecondsRealtime(1.21f);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.75f);
        }
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(hit ? 1.55f : 0.9f);
        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayOracleCinematic(string result)
    {
        bool contact = result.Contains("KONTAKT");
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-oracle");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);

        VisualElement distantShip = CreateAbilityElement("oracle-distant-ship");
        panel.Add(distantShip);
        VisualElement skyBeam = CreateAbilityElement("oracle-sky-beam");
        skyBeam.Add(CreateAbilityElement("oracle-sky-beam-core"));
        panel.Add(skyBeam);

        Label resultLabel = CreateAbilityLabel(
            contact ? "KONTAKT IM SEKTOR" : "SEKTOR LEER // WASSER MARKIERT",
            "ability-cinematic-result");
        panel.Add(resultLabel);

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.12f);

        cinematic.AddToClassList("oracle-scan-active");
        yield return new WaitForSecondsRealtime(0.08f);

        // Three pendulum legs: across, back, and across again.
        cinematic.AddToClassList("oracle-pendulum-right");
        yield return new WaitForSecondsRealtime(0.92f);
        cinematic.RemoveFromClassList("oracle-pendulum-right");
        yield return new WaitForSecondsRealtime(0.92f);
        cinematic.AddToClassList("oracle-pendulum-right");
        yield return new WaitForSecondsRealtime(0.92f);

        cinematic.AddToClassList(contact ? "oracle-contact" : "oracle-clear");
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(0.5f);

        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayAresCinematic(string result, int row, int column)
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-ares");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("ability-ocean-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);

        VisualElement stage = CreateAbilityElement("ares-ocean-stage");
        stage.Add(CreateAbilityElement("ares-ship-shadow"));
        stage.Add(CreateAbilityElement("ares-blue-glow"));
        VisualElement ship = CreateAbilityElement("ares-repair-ship");
        stage.Add(ship);
        panel.Add(stage);

        Label resultLabel = CreateAbilityLabel("ARES // EIGENES SCHIFF REPARIERT", "ability-cinematic-result");
        panel.Add(resultLabel);

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.18f);

        cinematic.AddToClassList("ares-repair-active");
        yield return new WaitForSecondsRealtime(0.58f);
        cinematic.AddToClassList("ares-repair-complete");
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(0.74f);

        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private IEnumerator PlayViperChemicalCinematic(string result)
    {
        bool hit = result.Contains("AUFGEDECKT");
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-viper-chemical");
        if (hit)
        {
            cinematic.AddToClassList("viper-chemical-hit");
        }

        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("viper-toxic-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);

        panel.Add(CreateAbilityElement("viper-chemical-target-ship"));
        panel.Add(CreateAbilityElement("viper-chemical-impact"));

        VisualElement bomb = CreateAbilityElement("viper-chemical-bomb");
        panel.Add(bomb);

        activeViperParticleFx = CreateViperGasParticleFx(hit);
        VisualElement particleLayer = CreateAbilityElement("viper-particle-layer");
        particleLayer.style.backgroundImage = new StyleBackground(
            Background.FromRenderTexture(activeViperParticleFx.renderTexture));
        panel.Add(particleLayer);
        panel.Add(CreateAbilityElement("viper-gas-bloom"));

        Label resultLabel = CreateAbilityLabel(
            hit ? "CHEMIEBOMBE // SCHIFF AUFGEDECKT" : "CHEMIEBOMBE // KEIN KONTAKT",
            "ability-cinematic-result");
        panel.Add(resultLabel);

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.14f);

        cinematic.AddToClassList("viper-bomb-dropping");
        yield return StartCoroutine(DropViperChemicalBomb(hit));

        cinematic.AddToClassList("viper-chemical-impact-active");
        ParticleSystem.EmissionModule impactEmission = activeViperParticleFx.particles.emission;
        impactEmission.rateOverTime = 27f;
        activeViperParticleFx.particles.Emit(62);
        yield return new WaitForSecondsRealtime(0.16f);

        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(1.58f);

        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
        DisposeViperParticleFx();
    }

    private IEnumerator DropViperChemicalBomb(bool hit)
    {
        const float dropDuration = 0.78f;
        const float startY = 4.7f;
        float targetY = hit ? 0.25f : -0.65f;
        Transform gasSource = activeViperParticleFx.particles.transform;
        gasSource.localPosition = new Vector3(0f, startY, 0f);

        ParticleSystem.EmissionModule trailEmission = activeViperParticleFx.particles.emission;
        trailEmission.rateOverTime = 5f;
        activeViperParticleFx.particles.Play();
        activeViperParticleFx.particles.Emit(3);

        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);
            float eased = t * t;
            gasSource.localPosition = new Vector3(0f, Mathf.Lerp(startY, targetY, eased), 0f);
            yield return null;
        }

        gasSource.localPosition = new Vector3(0f, targetY, 0f);
    }

    private IEnumerator PlayViperMineCinematic()
    {
        VisualElement cinematic = CreateAbilityCinematic("ability-cinematic-viper-mine");
        cinematic.Add(CreateAbilityElement("ability-ocean-backdrop"));
        cinematic.Add(CreateAbilityElement("viper-mine-grade"));
        VisualElement panel = CreateAbilityElement("ability-cinematic-panel");
        cinematic.Add(panel);

        VisualElement mine = CreateAbilityElement("viper-mine-silhouette");
        mine.Add(CreateAbilityElement("viper-mine-core"));
        for (int i = 0; i < 8; i++)
        {
            VisualElement spike = CreateAbilityElement("viper-mine-spike");
            spike.AddToClassList("viper-mine-spike-" + (i + 1));
            mine.Add(spike);
        }
        panel.Add(mine);

        Label resultLabel = CreateAbilityLabel("MINE GELEGT", "ability-cinematic-result");
        panel.Add(resultLabel);

        battleScreen.Add(cinematic);
        yield return null;
        cinematic.AddToClassList("ability-cinematic-show");
        yield return new WaitForSecondsRealtime(0.14f);

        cinematic.AddToClassList("viper-mine-visible");
        yield return new WaitForSecondsRealtime(0.72f);
        cinematic.AddToClassList("ability-cinematic-result-visible");
        yield return new WaitForSecondsRealtime(0.68f);
        cinematic.AddToClassList("viper-mine-submerged");
        yield return new WaitForSecondsRealtime(0.42f);

        yield return StartCoroutine(FinishAbilityCinematic(cinematic));
    }

    private ViperParticleFx CreateViperGasParticleFx(bool hit)
    {
        DisposeViperParticleFx();

        const int particleLayer = 30;
        ViperParticleFx fx = new ViperParticleFx();
        fx.root = new GameObject("VIPER Chemical Gas Particle System");
        fx.root.hideFlags = HideFlags.HideAndDontSave;
        fx.root.layer = particleLayer;

        fx.renderTexture = new RenderTexture(540, 960, 0, RenderTextureFormat.ARGB32)
        {
            name = "VIPER Chemical Gas",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        fx.renderTexture.Create();

        GameObject cameraObject = new GameObject("VIPER Particle Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.layer = particleLayer;
        cameraObject.transform.SetParent(fx.root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
        Camera particleCamera = cameraObject.AddComponent<Camera>();
        particleCamera.clearFlags = CameraClearFlags.SolidColor;
        particleCamera.backgroundColor = Color.clear;
        particleCamera.orthographic = true;
        particleCamera.orthographicSize = 5f;
        particleCamera.aspect = 9f / 16f;
        particleCamera.cullingMask = 1 << particleLayer;
        particleCamera.targetTexture = fx.renderTexture;
        particleCamera.allowHDR = false;
        particleCamera.allowMSAA = false;

        GameObject particleObject = new GameObject("Toxic Gas Cloud");
        particleObject.hideFlags = HideFlags.HideAndDontSave;
        particleObject.layer = particleLayer;
        particleObject.transform.SetParent(fx.root.transform, false);
        particleObject.transform.localPosition = new Vector3(0f, 4.7f, 0f);

        fx.particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = fx.particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 2.7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.38f, 0.94f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.24f, 1f, 0.18f, 0.72f),
            new Color(0.68f, 1f, 0.12f, 0.42f));
        main.maxParticles = 180;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = fx.particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 5f;

        ParticleSystem.ShapeModule shape = fx.particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.58f;
        shape.radiusThickness = 1f;

        ParticleSystem.VelocityOverLifetimeModule velocity = fx.particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.22f, 0.66f);

        ParticleSystem.NoiseModule noise = fx.particles.noise;
        noise.enabled = true;
        noise.strength = 0.34f;
        noise.frequency = 0.38f;
        noise.scrollSpeed = 0.24f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.High;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = fx.particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gasFade = new Gradient();
        gasFade.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.55f, 1f, 0.12f), 0f),
                new GradientColorKey(new Color(0.12f, 0.72f, 0.18f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.16f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gasFade;

        fx.particleTexture = CreateSoftParticleTexture();
        Shader shader = Shader.Find("Sprites/Default");
        fx.material = new Material(shader)
        {
            name = "VIPER Gas Particle Material",
            mainTexture = fx.particleTexture,
            hideFlags = HideFlags.HideAndDontSave
        };
        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = fx.material;

        return fx;
    }

    private static Texture2D CreateSoftParticleTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "VIPER Soft Gas Particle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void DisposeViperParticleFx()
    {
        if (activeViperParticleFx == null)
        {
            return;
        }

        DestroyViperObject(activeViperParticleFx.root);
        if (activeViperParticleFx.renderTexture != null)
        {
            activeViperParticleFx.renderTexture.Release();
            DestroyViperObject(activeViperParticleFx.renderTexture);
        }
        DestroyViperObject(activeViperParticleFx.material);
        DestroyViperObject(activeViperParticleFx.particleTexture);
        activeViperParticleFx = null;
    }

    private static void DestroyViperObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private IEnumerator FinishAbilityCinematic(VisualElement cinematic)
    {
        cinematic.AddToClassList("ability-cinematic-finish");
        yield return new WaitForSecondsRealtime(0.24f);
        cinematic.RemoveFromHierarchy();
    }

    private void ClearAbilityCinematicImmediate()
    {
        abilityCinematicPlaying = false;
        DisposeViperParticleFx();
        if (battleScreen == null)
        {
            return;
        }

        VisualElement cinematic = battleScreen.Q<VisualElement>(className: "commander-ability-cinematic");
        if (cinematic != null)
        {
            cinematic.RemoveFromHierarchy();
        }
    }

    private VisualElement CreateAbilityCinematic(string sceneClass)
    {
        VisualElement cinematic = CreateAbilityElement("commander-ability-cinematic");
        cinematic.AddToClassList(sceneClass);
        cinematic.pickingMode = PickingMode.Position;
        return cinematic;
    }

    private static VisualElement CreateAbilityElement(string className)
    {
        VisualElement element = new VisualElement();
        element.AddToClassList(className);
        return element;
    }

    private static Label CreateAbilityLabel(string text, string className)
    {
        Label label = new Label(text);
        label.AddToClassList(className);
        return label;
    }

    private static bool HasSpecialAbilityCinematic(AbilityId id)
    {
        return id == AbilityId.OracleScan || id == AbilityId.AresRepair ||
               id == AbilityId.ChemicalBomb || id == AbilityId.MineLayer ||
               id == AbilityId.FleetRelocation || id == AbilityId.NuclearStrike ||
               id == AbilityId.RangefinderShot || id == AbilityId.BerserkerBarrage ||
               id == AbilityId.AbyssTorpedo || id == AbilityId.AbyssSubmerge ||
               id == AbilityId.RaptorJetStart;
    }
}
