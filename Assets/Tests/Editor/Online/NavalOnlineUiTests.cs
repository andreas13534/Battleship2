using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

public sealed class NavalOnlineUiTests
{
    [TestCase("andreas_dev#99958", true)]
    [TestCase("andreas_dev", false)]
    [TestCase("andreas_dev#abc", false)]
    [TestCase("ab#12345", false)]
    public void FriendLookup_AcceptsOnlyCompleteUnityPlayerNames(string value, bool expected)
    {
        MethodInfo method = typeof(UgsNavalOnlineService).GetMethod(
            "LooksLikeUnityPlayerName", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[] { value }), Is.EqualTo(expected));
    }

    [Test]
    public void FriendInput_ExplainsTheRequiredPlayerIdFormat()
    {
        VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu/MainMenu.uxml");
        TemplateContainer root = asset.CloneTree();
        TextField field = root.Q<TextField>("FriendNameField");
        Label hint = root.Q<Label>("FriendCodeHint");

        Assert.That(field.tooltip, Does.Contain("Name#12345"));
        Assert.That(hint.text, Does.Contain("NAME#12345"));
    }

    [Test]
    public void OnlineUxml_ContainsAllPlatformScreensAndCriticalControls()
    {
        VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu/MainMenu.uxml");
        Assert.That(asset, Is.Not.Null);
        TemplateContainer root = asset.CloneTree();
        string[] requiredNames =
        {
            "OnlineLoginScreen", "WebCredentialsPanel", "WebUsernameField", "WebPasswordField", "WebSignInButton", "WebRegisterButton",
            "OnlineHubScreen", "OnlineTabViewport", "FriendsScreen", "PlayScreen",
            "ProfileScreen", "StoreScreen", "LeaderboardScreen", "MatchmakingScreen", "RankedMatchFoundScreen", "AgeConsentToggle", "RankedBattleButton",
            "FriendsList", "InvitesList", "StoreButton", "BuyEliasButton", "BuyDaeButton", "BuyArjanButton", "WatchImaniAdButton",
            "RewardCodeField", "RedeemRewardCodeButton",
            "RankedMatchOwnAvatar", "RankedMatchOwnInitials", "RankedMatchOwnName",
            "RankedMatchOpponentAvatar", "RankedMatchOpponentInitials", "RankedMatchOpponentName", "RankedMatchFoundStatus",
            "DeveloperAdminPanel", "DeveloperFreeAbilitiesToggle", "DeveloperForceJetHitToggle",
            "RestorePurchasesButton", "SurrenderButton", "DeleteAccountButton", "ProfileStatsLabel",
            "ProfileAvatarImage", "ProfileAvatarButton", "ProfileDisplayNameLabel", "ProfileIdentityCodeLabel",
            "ProfileWinsValue", "ProfileLossesValue", "ProfileRankValue", "ProfileJoinedValue",
            "ProfileAccountButton", "ProfileAccountMenu", "ProfileAccountCancelButton",
            "StoreTabButton", "FriendsTabButton", "PlayTabButton", "LeaderboardTabButton", "ProfileTabButton",
            "OnlineTabIndicator", "OnlineTabTitle", "SoloBattleButton", "PlayLoginButton", "ProfileLoginButton",
            "PlayLaunchButton", "ModeSelectorButton", "GameModeOverlay", "CloseGameModeButton",
            "ShotCinematic", "ShotEnemyAttacker", "ShotRaptorJet", "ShotTerminalMissile", "ShotImpactAnchor"
        };
        foreach (string elementName in requiredNames)
            Assert.That(root.Q(elementName), Is.Not.Null, "Missing UI element: " + elementName);
    }

    [Test]
    public void CommanderSelection_UsesVerticalScrollViewForGrowingRoster()
    {
        VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu/MainMenu.uxml");
        Assert.That(asset, Is.Not.Null);
        TemplateContainer root = asset.CloneTree();
        ScrollView commanderList = root.Q<ScrollView>(className: "commander-list");

        Assert.That(commanderList, Is.Not.Null);
        Assert.That(commanderList.mode, Is.EqualTo(ScrollViewMode.Vertical));
        Assert.That(commanderList.Query<VisualElement>(className: "commander-card").ToList().Count, Is.EqualTo(7));
    }

    [Test]
    public void MobileBuild_IsLockedToUprightPortrait()
    {
        Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
        Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.True);
        Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
        Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.False);
        Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.False);
        Assert.That(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android),
            Is.EqualTo("com.hasengschwandtner.navalcommand"));
        Assert.That(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS),
            Is.EqualTo("com.hasengschwandtner.navalcommand"));
        Assert.That(PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android),
            Is.EqualTo(ScriptingImplementation.IL2CPP));
        Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
        Assert.That(PlayerSettings.Android.forceInternetPermission, Is.True);
    }

    [Test]
    public void IncomingShotShip_IsImportedAsOneTransparentSprite()
    {
        const string path = "Assets/Art/NavalCommand/shot-animation/shot-enemy-close-destroyer.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.alphaIsTransparency, Is.True);
    }

    [Test]
    public void StoreCatalog_UsesTheServerProductIdentifiers()
    {
        Assert.That(NavalIapService.EliasProductId, Is.EqualTo("commander.elias.voss"));
        Assert.That(NavalIapService.DaeProductId, Is.EqualTo("commander.dae.hyun.kwon"));
        Assert.That(NavalIapService.ArjanProductId, Is.EqualTo("commander.arjan.dhillon"));
    }

    [Test]
    public void RewardedAd_UsesConfiguredAndroidLevelPlayIdentifiers()
    {
        Assert.That(NavalRewardedAdService.AndroidAppKey, Is.EqualTo("27be23ebd"));
        Assert.That(NavalRewardedAdService.AndroidRewardedAdUnitId, Is.EqualTo("ha35o9hutwffhnws"));
    }

    [Test]
    public void OpeningRewardCode_ContainsEveryCommanderInTheRoster()
    {
        Assert.That(NavalRewardCodes.AllCommandersCode, Is.EqualTo("op_start"));
        Assert.That(NavalRewardCodes.AllCommanderIds, Is.EquivalentTo(new[]
        {
            "standard-commander", "elias-voss", "dae-hyun-kwon", "ronan-graves",
            "arjan-dhillon", "mateo-serrano", "imani-cross"
        }));
    }

    [Test]
    public void BackgroundMusic_IsAvailableAsAStreamingClip()
    {
        AudioClip music = Resources.Load<AudioClip>(NavalBackgroundMusic.MusicResourcePath);
        Assert.That(music, Is.Not.Null);

        AudioImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(music)) as AudioImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
        Assert.That(importer.loadInBackground, Is.True);
    }

    [Test]
    public void IapFakeStore_ReplacesItsLegacyInputModule()
    {
        GameObject eventSystemObject = new GameObject("IAP Fake Store EventSystem");
        try
        {
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();

            MethodInfo replacement = typeof(NavalIapService).GetMethod(
                "ReplaceLegacyFakeStoreInputModule",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(replacement, Is.Not.Null);
            replacement.Invoke(null, null);

            Assert.That(eventSystemObject.GetComponent<StandaloneInputModule>(), Is.Null);
            Assert.That(eventSystemObject.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [Test]
    public void CommanderSelection_HasIndividuallyFilterableOwnedCards()
    {
        VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu/MainMenu.uxml");
        TemplateContainer root = asset.CloneTree();
        for (int index = 0; index < 6; index++)
        {
            Assert.That(root.Q<VisualElement>("CommanderCard" + index), Is.Not.Null);
            Assert.That(root.Q<Button>("CommanderInfoButton" + index), Is.Not.Null);
            VisualElement infoPanel = root.Q<VisualElement>("CommanderInfo" + index);
            Assert.That(infoPanel, Is.Not.Null);
            Assert.That(infoPanel.ClassListContains("hidden"), Is.True);
        }

        NavalEntitlements entitlements = new NavalEntitlements();
        entitlements.commanderIds.Add("arjan-dhillon");
        Assert.That(entitlements.OwnsCommander("standard-commander"), Is.True);
        Assert.That(entitlements.OwnsCommander("elias-voss"), Is.True);
        Assert.That(entitlements.OwnsCommander("dae-hyun-kwon"), Is.True);
        Assert.That(entitlements.OwnsCommander("arjan-dhillon"), Is.True);
        Assert.That(entitlements.OwnsCommander("ronan-graves"), Is.False);
        Assert.That(entitlements.OwnsCommander("mateo-serrano"), Is.False);
        Assert.That(entitlements.OwnsCommander("imani-cross"), Is.False);
    }

    [Test]
    public void EditorBuild_UsesTheDevelopmentUgsEnvironment()
    {
        Assert.That(NavalOnlineEnvironment.Current, Is.EqualTo(NavalOnlineEnvironment.Development));
    }
}
