using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class NavalCommanderTests
{
    [Test]
    public void GridCell_TenthColumnUsesExactAbsoluteTenPercentSlot()
    {
        GameObject host = new GameObject("GridLayoutTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        try
        {
            MethodInfo createCell = typeof(NavalGameController).GetMethod(
                "CreateCell", BindingFlags.Instance | BindingFlags.NonPublic);
            Button cell = (Button)createCell.Invoke(controller, new object[] { 4, 9 });

            Assert.AreEqual(Position.Absolute, cell.style.position.value);
            Assert.AreEqual(90f, cell.style.left.value.value);
            Assert.AreEqual(10f, cell.style.width.value.value);
            Assert.AreEqual(40f, cell.style.top.value.value);
            Assert.AreEqual(10f, cell.style.height.value.value);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void StandardCommander_HasNoAbilities()
    {
        NavalGameController.CommanderData standard = NavalGameController.CreateCommanderCatalog()
            .Single(commander => commander.id == "standard-commander");

        Assert.NotNull(standard.abilities);
        Assert.IsEmpty(standard.abilities);
    }

    [Test]
    public void EliasVoss_KeepsSpecifiedFleetCostsAndAnimationIds()
    {
        NavalGameController.CommanderData elias = NavalGameController.CreateCommanderCatalog()
            .Single(commander => commander.id == "elias-voss");

        CollectionAssert.AreEqual(new[] { 2, 3, 5 }, elias.shipLengths);
        Assert.AreEqual(8, elias.abilities[0].cost);
        Assert.AreEqual(3, elias.abilities[1].cost);
        Assert.AreEqual("oracle-sector-sweep", elias.abilities[0].animationId);
        Assert.AreEqual("ares-repair-beam", elias.abilities[1].animationId);
    }

    [Test]
    public void DebugMode_ChangesEffectiveCostWithoutChangingStoredCost()
    {
        GameObject host = new GameObject("CommanderCostTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        NavalGameController.AbilityData ability = NavalGameController.CreateCommanderCatalog()
            .Single(commander => commander.id == "elias-voss").abilities[0];

        FieldInfo debugField = typeof(NavalGameController).GetField("debugMode", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo effectiveCost = typeof(NavalGameController).GetMethod("GetEffectiveAbilityCost", BindingFlags.Instance | BindingFlags.NonPublic);

        try
        {
            debugField.SetValue(controller, false);
            Assert.AreEqual(8, effectiveCost.Invoke(controller, new object[] { ability }));

            debugField.SetValue(controller, true);
            Assert.AreEqual(0, effectiveCost.Invoke(controller, new object[] { ability }));
            Assert.AreEqual(8, ability.cost);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void EliasAbilities_AreRegisteredForSpecialCinematics()
    {
        MethodInfo registration = typeof(NavalGameController).GetMethod(
            "HasSpecialAbilityCinematic",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.OracleScan }));
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.AresRepair }));
        Assert.IsFalse((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.StandardBarrage }));
    }

    [Test]
    public void DaeHyunKwon_KeepsFleetCostsAndViperAnimationIds()
    {
        NavalGameController.CommanderData dae = NavalGameController.CreateCommanderCatalog()
            .Single(commander => commander.id == "dae-hyun-kwon");

        CollectionAssert.AreEqual(new[] { 4, 4, 2, 2 }, dae.shipLengths);
        Assert.AreEqual(4, dae.abilities[0].cost);
        Assert.AreEqual(7, dae.abilities[1].cost);
        Assert.AreEqual("viper-chemical-cloud", dae.abilities[0].animationId);
        Assert.AreEqual("viper-mine-arm", dae.abilities[1].animationId);
    }

    [Test]
    public void DaeHyunAbilities_AreRegisteredForSpecialCinematics()
    {
        MethodInfo registration = typeof(NavalGameController).GetMethod(
            "HasSpecialAbilityCinematic",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.ChemicalBomb }));
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.MineLayer }));
    }

    [Test]
    public void RonanGraves_HasTitanFleetCostsAndCinematics()
    {
        NavalGameController.CommanderData ronan = NavalGameController.CreateCommanderCatalog()
            .Single(commander => commander.id == "ronan-graves");
        CollectionAssert.AreEqual(new[] { 14, 2 }, ronan.shipLengths);
        CollectionAssert.AreEqual(new[] { 7, 2 }, ronan.shipWidths);
        CollectionAssert.AreEqual(new[] { 2, 1 }, ronan.shipHeights);
        Assert.AreEqual(5, ronan.abilities[0].cost);
        Assert.AreEqual(7, ronan.abilities[1].cost);
        Assert.AreEqual("titan-fleet-relocation", ronan.abilities[0].animationId);
        Assert.AreEqual("titan-nuclear-strike", ronan.abilities[1].animationId);

        MethodInfo registration = typeof(NavalGameController).GetMethod(
            "HasSpecialAbilityCinematic", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.FleetRelocation }));
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.NuclearStrike }));
    }

    [Test]
    public void ArjanDhillon_HasVectorFleetCostsAndCinematics()
    {
        NavalGameController.CommanderData arjan = NavalGameController.CreateCommanderCatalog()
            .Single(commander => commander.id == "arjan-dhillon");
        CollectionAssert.AreEqual(new[] { 6, 4, 3 }, arjan.shipLengths);
        CollectionAssert.AreEqual(new[] { 3, 4, 3 }, arjan.shipWidths);
        CollectionAssert.AreEqual(new[] { 2, 1, 1 }, arjan.shipHeights);
        Assert.AreEqual(5, arjan.abilities[0].cost);
        Assert.AreEqual(8, arjan.abilities[1].cost);
        Assert.AreEqual("vector-rangefinder", arjan.abilities[0].animationId);
        Assert.AreEqual("berserker-random-barrage", arjan.abilities[1].animationId);

        MethodInfo registration = typeof(NavalGameController).GetMethod(
            "HasSpecialAbilityCinematic", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.RangefinderShot }));
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.BerserkerBarrage }));
    }

    [Test]
    public void MateoSerrano_HasAbyssFleetCostsAndCinematics()
    {
        NavalGameController.CommanderData mateo = NavalGameController.CreateCommanderCatalog()
            .Single(commander => commander.id == "mateo-serrano");
        CollectionAssert.AreEqual(new[] { 5, 3, 3 }, mateo.shipLengths);
        Assert.AreEqual(14, mateo.abilities[0].cost);
        Assert.AreEqual(5, mateo.abilities[1].cost);
        Assert.IsFalse(mateo.abilities[0].targetsOwnBoard);
        Assert.IsTrue(mateo.abilities[1].targetsOwnBoard);
        Assert.AreEqual("abyss-row-torpedo", mateo.abilities[0].animationId);
        Assert.AreEqual("abyss-submerge", mateo.abilities[1].animationId);

        MethodInfo registration = typeof(NavalGameController).GetMethod(
            "HasSpecialAbilityCinematic", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.AbyssTorpedo }));
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.AbyssSubmerge }));
    }

    [Test]
    public void ImaniCross_HasRaptorFleetCostsAndJetStartCinematic()
    {
        NavalGameController.CommanderData imani = NavalGameController.CreateCommanderCatalog()
            .Single(commander => commander.id == "imani-cross");
        CollectionAssert.AreEqual(new[] { 5, 6, 2 }, imani.shipLengths);
        CollectionAssert.AreEqual(new[] { 5, 3, 2 }, imani.shipWidths);
        CollectionAssert.AreEqual(new[] { 1, 2, 1 }, imani.shipHeights);
        CollectionAssert.AreEqual(new[] { "ship-5", "ship-6", "ship-2" }, imani.shipClasses);
        Assert.AreEqual(8, imani.abilities[0].cost);
        Assert.AreEqual(3, imani.abilities[1].cost);
        Assert.AreEqual(NavalGameController.AbilityId.RaptorJetStart, imani.abilities[0].id);
        Assert.AreEqual(NavalGameController.AbilityId.RaptorJammer, imani.abilities[1].id);

        MethodInfo registration = typeof(NavalGameController).GetMethod(
            "HasSpecialAbilityCinematic", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsTrue((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.RaptorJetStart }));
        Assert.IsFalse((bool)registration.Invoke(null, new object[] { NavalGameController.AbilityId.RaptorJammer }));
    }

    [Test]
    public void RaptorJetStartCinematic_LayersJetBehindTransparentCockpitInterior()
    {
        GameObject host = new GameObject("RaptorCinematicTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        VisualElement battleScreen = new VisualElement();
        typeof(NavalGameController).GetField("battleScreen", flags).SetValue(controller, battleScreen);
        try
        {
            IEnumerator cinematic = (IEnumerator)typeof(NavalGameController)
                .GetMethod("PlayRaptorJetStartCinematic", flags)
                .Invoke(controller, new object[] { "RAPTOR // JET IN DER LUFT" });
            Assert.IsTrue(cinematic.MoveNext());
            Assert.NotNull(battleScreen[0].Q(className: "raptor-command-deck-backdrop"));
            VisualElement panel = battleScreen[0].Q(className: "ability-cinematic-panel");
            VisualElement jet = panel.Q(className: "raptor-window-jet");
            VisualElement interior = panel.Q(className: "raptor-command-deck-interior");
            Assert.NotNull(interior);
            Assert.AreEqual(1, battleScreen[0].Query<VisualElement>(className: "raptor-window-jet").ToList().Count);
            Assert.Less(panel.IndexOf(jet), panel.IndexOf(interior));
            Assert.IsTrue(cinematic.MoveNext());
            Assert.IsTrue(battleScreen[0].ClassListContains("ability-cinematic-show"));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void AndreasDev_BotForcesActiveRaptorJetTargetOnly()
    {
        MethodInfo shouldForceTarget = typeof(NavalGameController).GetMethod(
            "ShouldForceBotJetTarget",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(shouldForceTarget);
        Assert.IsTrue((bool)shouldForceTarget.Invoke(null, new object[] { "andreas_dev", true, true, 4, 7, false }));
        Assert.IsTrue((bool)shouldForceTarget.Invoke(null, new object[] { "ANDREAS_DEV", true, true, 4, 7, false }));
        Assert.IsFalse((bool)shouldForceTarget.Invoke(null, new object[] { "andreas_dev", false, true, 4, 7, false }));
        Assert.IsFalse((bool)shouldForceTarget.Invoke(null, new object[] { "other_player", true, true, 4, 7, false }));
        Assert.IsFalse((bool)shouldForceTarget.Invoke(null, new object[] { "andreas_dev", true, false, 4, 7, false }));
        Assert.IsFalse((bool)shouldForceTarget.Invoke(null, new object[] { "andreas_dev", true, true, -1, 7, false }));
        Assert.IsFalse((bool)shouldForceTarget.Invoke(null, new object[] { "andreas_dev", true, true, 4, 7, true }));
    }

    [Test]
    public void RaptorJetHit_ContinuesRightAndDownAfterTimedApproach()
    {
        GameObject host = new GameObject("RaptorShotFlightTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        VisualElement cinematic = new VisualElement();
        VisualElement jet = new VisualElement();
        VisualElement impactAnchor = new VisualElement();
        VisualElement missile = new VisualElement();
        typeof(NavalGameController).GetField("shotCinematic", flags).SetValue(controller, cinematic);
        typeof(NavalGameController).GetField("shotRaptorJet", flags).SetValue(controller, jet);
        typeof(NavalGameController).GetField("shotImpactAnchor", flags).SetValue(controller, impactAnchor);
        typeof(NavalGameController).GetField("shotTerminalMissile", flags).SetValue(controller, missile);
        typeof(NavalGameController).GetField("shotResultLabel", flags).SetValue(controller, new Label());
        typeof(NavalGameController).GetField("shotCoordinateLabel", flags).SetValue(controller, new Label());
        typeof(NavalGameController).GetField("nextShotShowsJet", flags).SetValue(controller, true);
        typeof(NavalGameController).GetField("nextShotHitsJet", flags).SetValue(controller, true);

        try
        {
            typeof(NavalGameController).GetMethod("ConfigureIncomingShotImpact", flags)
                .Invoke(controller, new object[] { true, 4, 7 });
            Assert.AreEqual(52f, impactAnchor.style.left.value.value);
            Assert.AreEqual(30f, impactAnchor.style.top.value.value);
            Assert.AreEqual(52f, missile.style.left.value.value);

            typeof(NavalGameController).GetMethod("PrepareShotJetFlight", flags).Invoke(controller, null);
            Assert.AreEqual(-34f, jet.style.left.value.value);

            typeof(NavalGameController).GetMethod("StartShotJetFlight", flags).Invoke(controller, null);
            Assert.AreEqual(112f, jet.style.left.value.value);

            typeof(NavalGameController).GetMethod("ResolveShotJetImpact", flags).Invoke(controller, null);
            Assert.AreEqual(125f, jet.style.left.value.value);
            Assert.AreEqual(58f, jet.style.top.value.value);
            Assert.AreEqual(141f, impactAnchor.style.left.value.value);
            Assert.AreEqual(65f, impactAnchor.style.top.value.value);
            Assert.IsTrue(cinematic.ClassListContains("shot-jet-struck"));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void AbyssCinematics_UseExactRowImpactsAndReducedDestroyerShimmer()
    {
        GameObject host = new GameObject("AbyssCinematicTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        VisualElement battleScreen = new VisualElement();
        typeof(NavalGameController).GetField("battleScreen", flags).SetValue(controller, battleScreen);
        try
        {
            bool[] resolvedColumns = (bool[])typeof(NavalGameController)
                .GetField("lastTorpedoResolvedColumns", flags).GetValue(controller);
            bool[] hitColumns = (bool[])typeof(NavalGameController)
                .GetField("lastTorpedoHitColumns", flags).GetValue(controller);
            for (int column = 0; column < resolvedColumns.Length; column++) resolvedColumns[column] = true;
            hitColumns[2] = true;
            hitColumns[7] = true;

            IEnumerator torpedo = (IEnumerator)typeof(NavalGameController)
                .GetMethod("PlayAbyssTorpedoCinematic", flags)
                .Invoke(controller, new object[] { "ABYSS // TORPEDO 2 TREFFER" });
            Assert.IsTrue(torpedo.MoveNext());
            Assert.NotNull(battleScreen[0].Q(className: "ability-ocean-backdrop"));
            Assert.Null(battleScreen[0].Q(className: "abyss-submarine"));
            Assert.Null(battleScreen[0].Q(className: "abyss-waterline"));
            Assert.NotNull(battleScreen[0].Q(className: "abyss-torpedo"));
            Assert.AreEqual(10, battleScreen[0].Query<VisualElement>(className: "abyss-row-impact").ToList().Count);
            Assert.AreEqual(2, battleScreen[0].Query<VisualElement>(className: "abyss-impact-hit").ToList().Count);
            Assert.AreEqual(8, battleScreen[0].Query<VisualElement>(className: "abyss-impact-splash").ToList().Count);
            Assert.AreEqual(0, battleScreen[0].Query<VisualElement>(className: "abyss-bubble").ToList().Count);
            Assert.IsTrue(torpedo.MoveNext());
            Assert.IsTrue(battleScreen[0].ClassListContains("ability-cinematic-show"));

            battleScreen.Clear();
            IEnumerator submerge = (IEnumerator)typeof(NavalGameController)
                .GetMethod("PlayAbyssSubmergeCinematic", flags)
                .Invoke(controller, new object[] { "ABYSS // SCHIFF UNTERGETAUCHT" });
            Assert.IsTrue(submerge.MoveNext());
            Assert.NotNull(battleScreen[0].Q(className: "abyss-submerge-ship"));
            Assert.Null(battleScreen[0].Q(className: "abyss-submerge-ship-shadow"));
            Assert.Null(battleScreen[0].Q(className: "abyss-waterline"));
            Assert.Null(battleScreen[0].Q(className: "abyss-sonar-ring"));
            Assert.AreEqual(0, battleScreen[0].Query<VisualElement>(className: "abyss-submerge-bubble").ToList().Count);
            Assert.IsTrue(submerge.MoveNext());
            Assert.IsTrue(battleScreen[0].ClassListContains("ability-cinematic-show"));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void VectorCinematics_ContainBlueScanAndSixSeparateShots()
    {
        GameObject host = new GameObject("VectorCinematicTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        VisualElement battleScreen = new VisualElement();
        typeof(NavalGameController).GetField("battleScreen", flags).SetValue(controller, battleScreen);
        typeof(NavalGameController).GetField("lastRangefinderDistance", flags).SetValue(controller, 4);
        try
        {
            IEnumerator rangefinder = (IEnumerator)typeof(NavalGameController)
                .GetMethod("PlayVectorRangefinderCinematic", flags)
                .Invoke(controller, new object[] { "VECTOR // DISTANZ 4 FELDER" });
            Assert.IsTrue(rangefinder.MoveNext());
            Assert.AreEqual(2, battleScreen[0].Query<VisualElement>(className: "vector-scan-ring").ToList().Count);
            Assert.Null(battleScreen[0].Q(className: "vector-search-square"));
            Assert.AreEqual("4 FELDER", battleScreen[0].Q<Label>(className: "vector-distance-result").text);

            battleScreen.Clear();
            object shots = typeof(NavalGameController).GetField("lastBarrageShots", flags).GetValue(controller);
            System.Collections.IList shotList = (System.Collections.IList)shots;
            System.Type shotType = shots.GetType().GetGenericArguments()[0];
            for (int index = 0; index < 6; index++)
            {
                object shot = System.Activator.CreateInstance(shotType);
                shotType.GetField("hit").SetValue(shot, index % 2 == 0);
                shotList.Add(shot);
            }
            IEnumerator barrage = (IEnumerator)typeof(NavalGameController)
                .GetMethod("PlayVectorBarrageCinematic", flags)
                .Invoke(controller, new object[] { "AMOKLAUF // MMMMMM" });
            Assert.IsTrue(barrage.MoveNext());
            Assert.AreEqual(6, battleScreen[0].Query<VisualElement>(className: "vector-barrage-shot").ToList().Count);
            Assert.AreEqual(6, battleScreen[0].Query<VisualElement>(className: "vector-barrage-missile").ToList().Count);
            Assert.AreEqual(3, battleScreen[0].Query<VisualElement>(className: "vector-barrage-will-hit").ToList().Count);
            Assert.AreEqual(3, battleScreen[0].Query<VisualElement>(className: "vector-barrage-will-miss").ToList().Count);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void TitanNuclearCinematic_UsesMissileSplashAndMushroomAssetElement()
    {
        GameObject host = new GameObject("TitanCinematicTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        VisualElement battleScreen = new VisualElement();
        typeof(NavalGameController).GetField("battleScreen", flags).SetValue(controller, battleScreen);
        try
        {
            IEnumerator cinematic = (IEnumerator)typeof(NavalGameController)
                .GetMethod("PlayTitanNuclearCinematic", flags)
                .Invoke(controller, new object[] { "ATOMBOMBE // ZIEL AUSGELÖSCHT" });
            Assert.IsTrue(cinematic.MoveNext());
            Assert.NotNull(battleScreen[0].Q(className: "ability-ocean-backdrop"));
            Assert.NotNull(battleScreen[0].Q(className: "titan-nuclear-missile-sprite"));
            Assert.NotNull(battleScreen[0].Q(className: "titan-nuclear-splash"));
            Assert.NotNull(battleScreen[0].Q(className: "titan-nuclear-mushroom"));
            Assert.NotNull(battleScreen[0].Q(className: "titan-nuclear-firestorm"));
            Assert.AreEqual(18, battleScreen[0].Query<VisualElement>(className: "titan-fire-ember").ToList().Count);

            battleScreen.Clear();
            IEnumerator miss = (IEnumerator)typeof(NavalGameController)
                .GetMethod("PlayTitanNuclearCinematic", flags)
                .Invoke(controller, new object[] { "ATOMBOMBE // KEIN KONTAKT" });
            Assert.IsTrue(miss.MoveNext());
            Assert.IsTrue(battleScreen[0].ClassListContains("titan-nuclear-will-miss"));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void EliasCinematics_CreateDistinctOracleAndAresScenes()
    {
        GameObject host = new GameObject("CommanderCinematicTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo battleScreenField = typeof(NavalGameController).GetField("battleScreen", instanceFlags);
        VisualElement battleScreen = new VisualElement();
        battleScreenField.SetValue(controller, battleScreen);

        try
        {
            MethodInfo oracleMethod = typeof(NavalGameController).GetMethod("PlayOracleCinematic", instanceFlags);
            IEnumerator oracle = (IEnumerator)oracleMethod.Invoke(controller, new object[] { "ORACLE // KONTAKT IM SEKTOR" });
            Assert.IsTrue(oracle.MoveNext());
            Assert.IsTrue(battleScreen[0].ClassListContains("ability-cinematic-oracle"));
            Assert.NotNull(battleScreen[0].Q(className: "ability-ocean-backdrop"));
            Assert.NotNull(battleScreen[0].Q(className: "oracle-distant-ship"));
            Assert.NotNull(battleScreen[0].Q(className: "oracle-sky-beam"));
            Assert.Null(battleScreen[0].Q(className: "oracle-scan-grid"));
            Assert.Null(battleScreen[0].Q(className: "oracle-water-scan"));
            battleScreen.Clear();

            MethodInfo aresMethod = typeof(NavalGameController).GetMethod("PlayAresCinematic", instanceFlags);
            IEnumerator ares = (IEnumerator)aresMethod.Invoke(controller, new object[] { "ARES // SCHIFFSTEIL REPARIERT", 0, 0 });
            Assert.IsTrue(ares.MoveNext());
            Assert.IsTrue(battleScreen[0].ClassListContains("ability-cinematic-ares"));
            Assert.NotNull(battleScreen[0].Q(className: "ability-ocean-backdrop"));
            Assert.NotNull(battleScreen[0].Q(className: "ares-repair-ship"));
            Assert.NotNull(battleScreen[0].Q(className: "ares-blue-glow"));
            Assert.Null(battleScreen[0].Q(className: "ares-repair-beam"));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void DaeHyunCinematics_CreateParticleGasAndMineSilhouette()
    {
        GameObject host = new GameObject("ViperCinematicTest");
        NavalGameController controller = host.AddComponent<NavalGameController>();
        BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        VisualElement battleScreen = new VisualElement();
        typeof(NavalGameController).GetField("battleScreen", instanceFlags).SetValue(controller, battleScreen);

        try
        {
            MethodInfo chemicalMethod = typeof(NavalGameController).GetMethod("PlayViperChemicalCinematic", instanceFlags);
            IEnumerator chemical = (IEnumerator)chemicalMethod.Invoke(controller, new object[] { "CHEMIEBOMBE // SCHIFF AUFGEDECKT" });
            Assert.IsTrue(chemical.MoveNext());
            Assert.NotNull(battleScreen[0].Q(className: "viper-chemical-bomb"));
            Assert.NotNull(battleScreen[0].Q(className: "viper-particle-layer"));
            object particleFx = typeof(NavalGameController)
                .GetField("activeViperParticleFx", instanceFlags)
                .GetValue(controller);
            Assert.NotNull(particleFx);
            Assert.NotNull(particleFx.GetType().GetField("particles").GetValue(particleFx));

            typeof(NavalGameController).GetMethod("ClearAbilityCinematicImmediate", instanceFlags).Invoke(controller, null);

            MethodInfo mineMethod = typeof(NavalGameController).GetMethod("PlayViperMineCinematic", instanceFlags);
            IEnumerator mine = (IEnumerator)mineMethod.Invoke(controller, null);
            Assert.IsTrue(mine.MoveNext());
            Assert.NotNull(battleScreen[0].Q(className: "viper-mine-silhouette"));
            Assert.AreEqual("MINE GELEGT", battleScreen[0].Q<Label>(className: "ability-cinematic-result").text);
        }
        finally
        {
            typeof(NavalGameController).GetMethod("ClearAbilityCinematicImmediate", instanceFlags).Invoke(controller, null);
            Object.DestroyImmediate(host);
        }
    }
}
