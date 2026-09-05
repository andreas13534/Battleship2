using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private enum OnlineFlowMode
    {
        None,
        Ranked,
        Friendly,
        FriendlyAccept
    }

    private INavalOnlineService onlineService;
    private NavalIapService iapService;
    private NavalRewardedAdService rewardedAdService;
    private OnlineFlowMode onlineFlowMode;
    private string friendlyOpponentId;
    private string pendingFriendlyInviteId;
    private NavalMatchTicket activeMatchTicket;
    private NavalPlayerMatchView onlineMatchView;

    private VisualElement onlineLoginScreen;
    private VisualElement onlineHubScreen;
    private VisualElement profileScreen;
    private VisualElement matchmakingScreen;
    private VisualElement leaderboardScreen;
    private VisualElement storeScreen;
    private VisualElement friendsList;
    private VisualElement invitesPanel;
    private VisualElement invitesList;
    private VisualElement leaderboardList;
    private VisualElement onlineTabViewport;
    private VisualElement friendsScreen;
    private VisualElement playScreen;
    private VisualElement onlineTabIndicator;
    private VisualElement gameModeOverlay;
    private VisualElement[] onlineTabPages;
    private Button[] onlineTabButtons;
    private Button onlineButton;
    private Button accountButton;
    private Button onlineLoginBackButton;
    private Button onlineHubBackButton;
    private Button playerAccountLoginButton;
    private Button webSignInButton;
    private Button webRegisterButton;
    private Button appleLoginButton;
    private Button googleLoginButton;
    private Button signOutButton;
    private Button saveProfileButton;
    private Button deleteAccountButton;
    private Button rankedBattleButton;
    private Button soloBattleButton;
    private Button playLoginButton;
    private Button profileLoginButton;
    private Button playLaunchButton;
    private Button modeSelectorButton;
    private Button closeGameModeButton;
    private Button leaderboardButton;
    private Button refreshFriendsButton;
    private Button addFriendButton;
    private Button cancelMatchmakingButton;
    private Button storeButton;
    private Button buyEliasButton;
    private Button buyDaeButton;
    private Button buyArjanButton;
    private Button watchImaniAdButton;
    private Button redeemRewardCodeButton;
    private Button restorePurchasesButton;
    private Label onlineStatusLabel;
    private Label loginStatusLabel;
    private Label onlineHubStatusLabel;
    private Label onlineHubMessageLabel;
    private Label profileNameLabel;
    private Label profileCodeLabel;
    private Label profileRankLabel;
    private Label profileStatsLabel;
    private Label profileMessageLabel;
    private Label friendCountLabel;
    private Label matchmakingStatusLabel;
    private Label leaderboardMessageLabel;
    private Label seasonLabel;
    private Label storeMessageLabel;
    private Label onlineTabTitle;
    private Label gameModeMessageLabel;
    private TextField friendNameField;
    private TextField webUsernameField;
    private TextField webPasswordField;
    private TextField profileNameField;
    private TextField rewardCodeField;
    private Toggle ageConsentToggle;
    private VisualElement webCredentialsPanel;
    private long deleteAccountArmedUntilUnixMs;
    private long nextMatchmakingPollUnixMs;
    private long lastResumeRefreshUnixMs;
    private bool matchmakingPollPending;
    private bool resumeRefreshPending;
    private bool rewardCodeRedemptionPending;
    private int activeOnlineTab = -1;
    private bool selectedRankedMode;
    private bool loginPending;
    private bool queueRequestPending;
    private bool cancelQueuePending;
    private bool socialRefreshPending;
    private bool friendRequestPending;
    private long nextSocialRefreshUnixMs;
    private long nextBattleRefreshUnixMs;
    private int loginReturnTab = 2;
    private int tabTransitionVersion;

    private static readonly string[] OnlineTabTitles =
    {
        "ARSENAL", "FREUNDE", "SPIELEN", "RANGLISTE", "PROFIL"
    };

    private void CacheOnlineUi(VisualElement root)
    {
        onlineLoginScreen = root.Q<VisualElement>("OnlineLoginScreen");
        onlineHubScreen = root.Q<VisualElement>("OnlineHubScreen");
        profileScreen = root.Q<VisualElement>("ProfileScreen");
        matchmakingScreen = root.Q<VisualElement>("MatchmakingScreen");
        leaderboardScreen = root.Q<VisualElement>("LeaderboardScreen");
        storeScreen = root.Q<VisualElement>("StoreScreen");
        friendsList = root.Q<VisualElement>("FriendsList");
        invitesPanel = root.Q<VisualElement>("InvitesPanel");
        invitesList = root.Q<VisualElement>("InvitesList");
        leaderboardList = root.Q<VisualElement>("LeaderboardList");
        onlineTabViewport = root.Q<VisualElement>("OnlineTabViewport");
        friendsScreen = root.Q<VisualElement>("FriendsScreen");
        playScreen = root.Q<VisualElement>("PlayScreen");
        onlineTabIndicator = root.Q<VisualElement>("OnlineTabIndicator");
        gameModeOverlay = root.Q<VisualElement>("GameModeOverlay");
        onlineTabPages = new[] { storeScreen, friendsScreen, playScreen, leaderboardScreen, profileScreen };
        onlineButton = root.Q<Button>("OnlineButton");
        accountButton = root.Q<Button>("AccountButton");
        onlineLoginBackButton = root.Q<Button>("OnlineLoginBackButton");
        onlineHubBackButton = root.Q<Button>("OnlineHubBackButton");
        playerAccountLoginButton = root.Q<Button>("PlayerAccountLoginButton");
        webSignInButton = root.Q<Button>("WebSignInButton");
        webRegisterButton = root.Q<Button>("WebRegisterButton");
        appleLoginButton = root.Q<Button>("AppleLoginButton");
        googleLoginButton = root.Q<Button>("GoogleLoginButton");
        signOutButton = root.Q<Button>("SignOutButton");
        saveProfileButton = root.Q<Button>("SaveProfileButton");
        deleteAccountButton = root.Q<Button>("DeleteAccountButton");
        rankedBattleButton = root.Q<Button>("RankedBattleButton");
        soloBattleButton = root.Q<Button>("SoloBattleButton");
        playLoginButton = root.Q<Button>("PlayLoginButton");
        profileLoginButton = root.Q<Button>("ProfileLoginButton");
        playLaunchButton = root.Q<Button>("PlayLaunchButton");
        modeSelectorButton = root.Q<Button>("ModeSelectorButton");
        closeGameModeButton = root.Q<Button>("CloseGameModeButton");
        leaderboardButton = root.Q<Button>("LeaderboardButton");
        refreshFriendsButton = root.Q<Button>("RefreshFriendsButton");
        addFriendButton = root.Q<Button>("AddFriendButton");
        cancelMatchmakingButton = root.Q<Button>("CancelMatchmakingButton");
        storeButton = root.Q<Button>("StoreButton");
        buyEliasButton = root.Q<Button>("BuyEliasButton");
        buyDaeButton = root.Q<Button>("BuyDaeButton");
        buyArjanButton = root.Q<Button>("BuyArjanButton");
        watchImaniAdButton = root.Q<Button>("WatchImaniAdButton");
        redeemRewardCodeButton = root.Q<Button>("RedeemRewardCodeButton");
        restorePurchasesButton = root.Q<Button>("RestorePurchasesButton");
        onlineTabButtons = new[]
        {
            root.Q<Button>("StoreTabButton"),
            root.Q<Button>("FriendsTabButton"),
            root.Q<Button>("PlayTabButton"),
            root.Q<Button>("LeaderboardTabButton"),
            root.Q<Button>("ProfileTabButton")
        };
        onlineStatusLabel = root.Q<Label>("OnlineStatusLabel");
        loginStatusLabel = root.Q<Label>("LoginStatusLabel");
        onlineHubStatusLabel = root.Q<Label>("OnlineHubStatusLabel");
        onlineHubMessageLabel = root.Q<Label>("OnlineHubMessageLabel");
        profileNameLabel = root.Q<Label>("ProfileNameLabel");
        profileCodeLabel = root.Q<Label>("ProfileCodeLabel");
        profileRankLabel = root.Q<Label>("ProfileRankLabel");
        profileStatsLabel = root.Q<Label>("ProfileStatsLabel");
        profileMessageLabel = root.Q<Label>("ProfileMessageLabel");
        friendCountLabel = root.Q<Label>("FriendCountLabel");
        matchmakingStatusLabel = root.Q<Label>("MatchmakingStatusLabel");
        leaderboardMessageLabel = root.Q<Label>("LeaderboardMessageLabel");
        seasonLabel = root.Q<Label>("SeasonLabel");
        storeMessageLabel = root.Q<Label>("StoreMessageLabel");
        onlineTabTitle = root.Q<Label>("OnlineTabTitle");
        gameModeMessageLabel = root.Q<Label>("GameModeMessageLabel");
        friendNameField = root.Q<TextField>("FriendNameField");
        webUsernameField = root.Q<TextField>("WebUsernameField");
        webPasswordField = root.Q<TextField>("WebPasswordField");
        profileNameField = root.Q<TextField>("ProfileNameField");
        rewardCodeField = root.Q<TextField>("RewardCodeField");
        ageConsentToggle = root.Q<Toggle>("AgeConsentToggle");
        webCredentialsPanel = root.Q<VisualElement>("WebCredentialsPanel");
#if UNITY_WEBGL && !UNITY_EDITOR
        webCredentialsPanel.RemoveFromClassList("hidden");
        playerAccountLoginButton.AddToClassList("hidden");
#else
        webCredentialsPanel.AddToClassList("hidden");
        playerAccountLoginButton.RemoveFromClassList("hidden");
#endif
        CacheProfilePresentationUi(root);
        CacheRankedMatchFoundUi(root);
        CacheDeveloperAdminUi(root);
    }

    private void BindOnlineUi()
    {
        onlineButton.clicked += OpenOnline;
        accountButton.clicked += () => SwitchOnlineTab(4);
        onlineLoginBackButton.clicked += ShowOnlineAppHome;
        onlineHubBackButton.clicked += ShowOnlineAppHome;
        playerAccountLoginButton.clicked += () => _ = SignInWithPlayerAccountAsync();
        webSignInButton.clicked += () => _ = SignInWithWebCredentialsAsync(false);
        webRegisterButton.clicked += () => _ = SignInWithWebCredentialsAsync(true);
        appleLoginButton.clicked += () => _ = SignInWithPlayerAccountAsync();
        googleLoginButton.clicked += () => _ = SignInWithPlayerAccountAsync();
        signOutButton.clicked += () =>
        {
            CloseProfileAccountMenu();
            _ = SignOutOnlineAsync();
        };
        saveProfileButton.clicked += () => _ = SaveProfileAsync();
        deleteAccountButton.clicked += ArmOrDeleteAccount;
        rankedBattleButton.clicked += () => SelectGameMode(true);
        soloBattleButton.clicked += () => SelectGameMode(false);
        playLaunchButton.clicked += StartSelectedGameMode;
        modeSelectorButton.clicked += OpenGameModeMenu;
        closeGameModeButton.clicked += CloseGameModeMenu;
        playLoginButton.clicked += OpenOnline;
        profileLoginButton.clicked += () => OpenOnlineLogin(4);
        leaderboardButton.clicked += () => SwitchOnlineTab(3);
        storeButton.clicked += () => SwitchOnlineTab(0);
        buyEliasButton.clicked += () => BuyProduct(NavalIapService.EliasProductId);
        buyDaeButton.clicked += () => BuyProduct(NavalIapService.DaeProductId);
        buyArjanButton.clicked += () => BuyProduct(NavalIapService.ArjanProductId);
        watchImaniAdButton.clicked += ShowImaniRewardedAd;
        redeemRewardCodeButton.clicked += () => _ = RedeemRewardCodeAsync();
        rewardCodeField.RegisterValueChangedCallback(_ => UpdateRewardCodeControls());
        restorePurchasesButton.clicked += RestorePurchases;
        refreshFriendsButton.clicked += () => _ = RefreshSocialAsync();
        addFriendButton.clicked += () => _ = AddFriendAsync();
        cancelMatchmakingButton.clicked += () => _ = CancelMatchmakingAsync();
        for (int index = 0; index < onlineTabButtons.Length; index++)
        {
            int tabIndex = index;
            onlineTabButtons[index].clicked += () => SwitchOnlineTab(tabIndex);
        }
        ageConsentToggle.RegisterValueChangedCallback(evt => SetOnlineButtonsEnabled(evt.newValue));
        SetOnlineButtonsEnabled(ageConsentToggle.value);
        BindProfilePresentationUi();
        BindDeveloperAdminUi();
    }

    private async void InitializeOnlineSystem()
    {
        onlineService = new UgsNavalOnlineService();
        onlineService.StateChanged += RefreshOnlineState;
        onlineService.MatchChanged += HandleOnlineMatchChanged;
        await onlineService.InitializeAsync(NavalOnlineEnvironment.Current);
        RefreshOnlineState();
    }

    private void OpenOnline()
    {
        onlineFlowMode = OnlineFlowMode.None;
        if (onlineService != null && onlineService.IsSignedIn && onlineService.Status != NavalOnlineStatus.Error)
        {
            _ = ShowOnlineHubAsync();
            return;
        }

        OpenOnlineLogin(2);
    }

    private void OpenOnlineLogin(int returnTab)
    {
        loginReturnTab = returnTab;
        CloseGameModeMenu();
        CloseProfileAccountMenu();
        ShowOnly(onlineLoginScreen);
        RefreshOnlineState();
    }

    private void BeginSoloFlow()
    {
        onlineFlowMode = OnlineFlowMode.None;
        ShowCommanderSelection();
    }

    private void HandleRankedBattlePressed()
    {
        if (onlineService?.IsSignedIn == true)
            BeginRankedFlow();
        else
            OpenOnline();
    }

    private void StartSelectedGameMode()
    {
        if (selectedRankedMode)
            HandleRankedBattlePressed();
        else
            BeginSoloFlow();
    }

    private void OpenGameModeMenu()
    {
        UpdateGameModeUi();
        gameModeOverlay.RemoveFromClassList("hidden");
    }

    private void CloseGameModeMenu()
    {
        gameModeOverlay.AddToClassList("hidden");
    }

    private void SelectGameMode(bool ranked)
    {
        selectedRankedMode = ranked;
        UpdateGameModeUi();
        CloseGameModeMenu();
        CloseProfileAccountMenu();
    }

    private void UpdateGameModeUi()
    {
        modeSelectorButton.text = selectedRankedMode
            ? "RANKED // ONLINE  ›"
            : "SOLO // GEGEN BOT  ›";
        soloBattleButton.EnableInClassList("game-mode-option-active", !selectedRankedMode);
        rankedBattleButton.EnableInClassList("game-mode-option-active", selectedRankedMode);
        gameModeMessageLabel.text = selectedRankedMode
            ? onlineService?.IsSignedIn == true ? "RANKED IST AUSGEWÄHLT" : "RANKED BENÖTIGT EINE ANMELDUNG"
            : "SOLO IST AUSGEWÄHLT";
    }

    private async Task SignInWithPlayerAccountAsync()
    {
        await RunLoginUiAsync(
            () => onlineService.SignInWithPlayerAccountAsync(),
            "ANMELDUNG IM BROWSER ABSCHLIESSEN…");
    }

    private async Task SignInWithWebCredentialsAsync(bool createAccount)
    {
        string username = (webUsernameField.value ?? string.Empty).Trim();
        string password = webPasswordField.value ?? string.Empty;
        if (username.Length < 3 || username.Length > 20)
        {
            ShowLoginMessage("NUTZERNAME MUSS 3 BIS 20 ZEICHEN HABEN");
            return;
        }
        if (password.Length < 8 || password.Length > 30)
        {
            ShowLoginMessage("PASSWORT MUSS 8 BIS 30 ZEICHEN HABEN");
            return;
        }

        await RunLoginUiAsync(
            () => createAccount
                ? onlineService.SignUpWithUsernamePasswordAsync(username, password)
                : onlineService.SignInWithUsernamePasswordAsync(username, password),
            createAccount ? "KONTO WIRD ERSTELLT…" : "ANMELDUNG LÄUFT…");
        if (this != null) webPasswordField.value = string.Empty;
    }

    private async Task RunLoginUiAsync(Func<Task> signIn, string pendingMessage)
    {
        if (loginPending || onlineService == null) return;
        if (!ageConsentToggle.value)
        {
            ShowLoginMessage("ONLINE-SPIEL IST ERST AB 16 JAHREN VERFÜGBAR");
            return;
        }
        loginPending = true;
        SetOnlineButtonsEnabled(false);
        try
        {
            ShowLoginMessage(pendingMessage);
            if (onlineService.IsSignedIn)
                await onlineService.InitializeAsync(NavalOnlineEnvironment.Current);
            else
                await signIn();

            if (this == null) return;
            if (onlineService.IsSignedIn && onlineService.Profile != null && onlineService.Status != NavalOnlineStatus.Error)
            {
                if (!IsActiveOnlineMatch && !onlineLoginScreen.ClassListContains("hidden"))
                {
                    ShowOnly(onlineHubScreen);
                    SwitchOnlineTab(loginReturnTab, true);
                }
            }
            else ShowLoginMessage(onlineService.LastError);
        }
        catch (Exception exception)
        {
            if (this != null) ShowLoginMessage(exception.Message);
        }
        finally
        {
            loginPending = false;
            if (this != null) SetOnlineButtonsEnabled(ageConsentToggle.value);
        }
    }

    private async Task SignOutOnlineAsync()
    {
        await onlineService.SignOutAsync();
        ResetOnlineFlowForMenu();
        invitesList.Clear();
        invitesPanel.AddToClassList("hidden");
        friendsList.Clear();
        ShowOnlineAppHome();
        RefreshOnlineState();
    }

    private void ArmOrDeleteAccount()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now > deleteAccountArmedUntilUnixMs)
        {
            deleteAccountArmedUntilUnixMs = now + 5000;
            deleteAccountButton.text = "ENDGÜLTIG LÖSCHEN";
            profileMessageLabel.text = "NOCHMAL TIPPEN. MATCHES WERDEN AUFGEGEBEN UND DAS KONTO GELÖSCHT";
            return;
        }

        deleteAccountArmedUntilUnixMs = 0;
        deleteAccountButton.text = "KONTO LÖSCHEN";
        _ = DeleteAccountAsync();
    }

    private async Task DeleteAccountAsync()
    {
        deleteAccountButton.SetEnabled(false);
        profileMessageLabel.text = "KONTO WIRD SICHER GELÖSCHT...";
        try
        {
            await onlineService.DeleteAccountAsync();
            ResetOnlineFlowForMenu();
            ShowOnly(onlineLoginScreen);
            ShowLoginMessage("KONTO GELÖSCHT");
        }
        catch (Exception exception)
        {
            profileMessageLabel.text = "LÖSCHEN FEHLGESCHLAGEN: " + exception.Message.ToUpperInvariant();
        }
        finally
        {
            deleteAccountButton.SetEnabled(true);
        }
    }

    private async Task ShowOnlineHubAsync()
    {
        ShowOnlineAppHome();
        await Task.CompletedTask;
    }

    private void ShowOnlineAppHome()
    {
        ShowOnly(onlineHubScreen);
        RenderProfile();
        SwitchOnlineTab(2, true);
    }

    private void SwitchOnlineTab(int index, bool immediate = false)
    {
        if (onlineTabPages == null || index < 0 || index >= onlineTabPages.Length)
            return;

        if ((index == 1 || index == 4) && onlineService?.IsSignedIn != true)
        {
            OpenOnlineLogin(index);
            return;
        }

        CloseGameModeMenu();
        CloseProfileAccountMenu();
        int transition = ++tabTransitionVersion;
        // A delayed callback from an older swipe must not hide a newly selected page.
        foreach (VisualElement page in onlineTabPages)
        {
            if (page == null) continue;
            page.RemoveFromClassList("online-tab-enter-left");
            page.RemoveFromClassList("online-tab-enter-right");
            page.RemoveFromClassList("online-tab-exit-left");
            page.RemoveFromClassList("online-tab-exit-right");
            page.EnableInClassList("hidden", page != onlineTabPages[index] && page != (activeOnlineTab < 0 ? null : onlineTabPages[activeOnlineTab]));
        }

        int previousIndex = activeOnlineTab;
        VisualElement nextPage = onlineTabPages[index];
        if (nextPage == null)
            return;

        if (onlineTabTitle != null)
            onlineTabTitle.text = OnlineTabTitles[index];

        if (onlineTabIndicator != null)
            onlineTabIndicator.style.left = Length.Percent(index * 20f);

        for (int buttonIndex = 0; buttonIndex < onlineTabButtons.Length; buttonIndex++)
            onlineTabButtons[buttonIndex]?.EnableInClassList("online-tab-active", buttonIndex == index);

        if (immediate || previousIndex < 0 || previousIndex == index)
        {
            for (int pageIndex = 0; pageIndex < onlineTabPages.Length; pageIndex++)
            {
                VisualElement page = onlineTabPages[pageIndex];
                if (page == null) continue;
                page.RemoveFromClassList("online-tab-enter-left");
                page.RemoveFromClassList("online-tab-enter-right");
                page.RemoveFromClassList("online-tab-exit-left");
                page.RemoveFromClassList("online-tab-exit-right");
                page.EnableInClassList("hidden", pageIndex != index);
            }
        }
        else
        {
            bool movingRight = index > previousIndex;
            VisualElement previousPage = onlineTabPages[previousIndex];
            string enterClass = movingRight ? "online-tab-enter-right" : "online-tab-enter-left";
            string exitClass = movingRight ? "online-tab-exit-left" : "online-tab-exit-right";

            nextPage.RemoveFromClassList("hidden");
            nextPage.AddToClassList(enterClass);
            nextPage.schedule.Execute(() => nextPage.RemoveFromClassList(enterClass)).ExecuteLater(16);

            if (previousPage != null)
            {
                previousPage.AddToClassList(exitClass);
                previousPage.schedule.Execute(() =>
                {
                    if (transition != tabTransitionVersion) return;
                    previousPage.AddToClassList("hidden");
                    previousPage.RemoveFromClassList(exitClass);
                }).ExecuteLater(190);
            }
        }

        activeOnlineTab = index;
        _ = LoadOnlineTabAsync(index);
    }

    private async Task LoadOnlineTabAsync(int index)
    {
        bool signedIn = onlineService?.IsSignedIn == true;
        if (!signedIn)
        {
            RenderSignedOutTab(index);
            return;
        }

        switch (index)
        {
            case 0:
                await ShowStoreAsync();
                break;
            case 1:
                await RefreshSocialAsync();
                break;
            case 3:
                await ShowLeaderboardAsync();
                break;
            case 4:
                OpenProfileEditor();
                break;
        }
    }

    private void RenderSignedOutTab(int index)
    {
        switch (index)
        {
            case 0:
                buyEliasButton.SetEnabled(false);
                buyDaeButton.SetEnabled(false);
                buyArjanButton.SetEnabled(false);
                watchImaniAdButton.SetEnabled(false);
                rewardCodeField.SetEnabled(false);
                redeemRewardCodeButton.SetEnabled(false);
                restorePurchasesButton.SetEnabled(false);
                storeMessageLabel.text = "FÜR KÄUFE ONLINE ANMELDEN";
                break;
            case 1:
                friendsList.Clear();
                friendsList.Add(CreateOnlineInfoLabel("FÜR FREUNDE UND DUELLE ONLINE ANMELDEN"));
                friendCountLabel.text = "OFFLINE";
                break;
            case 3:
                leaderboardList.Clear();
                leaderboardList.Add(CreateOnlineInfoLabel("FÜR DIE RANGLISTE ONLINE ANMELDEN"));
                leaderboardMessageLabel.text = string.Empty;
                break;
            case 4:
                OpenProfileEditor();
                break;
        }
    }

    private async Task ShowStoreAsync()
    {
        storeMessageLabel.text = "STORE WIRD VERBUNDEN...";
        try
        {
            await onlineService.RefreshEntitlementsAsync();
            EnsureRewardedAdService();
            if (iapService == null)
            {
                iapService = new NavalIapService(onlineService);
                iapService.Changed += RenderStore;
            }
            await iapService.InitializeAsync();
        }
        catch (Exception exception)
        {
            storeMessageLabel.text = exception.Message.ToUpperInvariant();
        }
        RenderStore();
    }

    private void RenderStore()
    {
        if (storeMessageLabel == null || buyEliasButton == null || buyDaeButton == null ||
            buyArjanButton == null || watchImaniAdButton == null) return;
        NavalEntitlements entitlements = onlineService?.Entitlements ?? new NavalEntitlements();
        ConfigureStoreButton(buyEliasButton, NavalIapService.EliasProductId, entitlements.OwnsCommander("elias-voss"));
        ConfigureStoreButton(buyDaeButton, NavalIapService.DaeProductId, entitlements.OwnsCommander("dae-hyun-kwon"));
        ConfigureStoreButton(buyArjanButton, NavalIapService.ArjanProductId, entitlements.OwnsCommander("arjan-dhillon"));
        ConfigureImaniRewardedButton(entitlements.OwnsCommander("imani-cross"));
        UpdateRewardCodeControls();
        restorePurchasesButton.SetEnabled(iapService != null && onlineService?.IsSignedIn == true);
        string storeStatus = iapService?.StatusMessage ?? "STORE NICHT VERBUNDEN";
        string adStatus = rewardedAdService?.StatusMessage;
        storeMessageLabel.text = string.IsNullOrWhiteSpace(adStatus) ? storeStatus : storeStatus + " // " + adStatus;
    }

    private void EnsureRewardedAdService()
    {
        if (rewardedAdService == null)
        {
            rewardedAdService = new NavalRewardedAdService();
            rewardedAdService.Changed += RenderStore;
            rewardedAdService.RewardEarned += HandleImaniRewardEarned;
        }
        rewardedAdService.Initialize(onlineService?.Profile?.playerId);
    }

    private void ConfigureImaniRewardedButton(bool owned)
    {
        if (owned)
        {
            watchImaniAdButton.text = "FREIGESCHALTET";
            watchImaniAdButton.SetEnabled(false);
            return;
        }

        bool signedIn = onlineService?.IsSignedIn == true;
        bool ready = rewardedAdService?.IsReady == true;
        bool canRequest = rewardedAdService?.CanRequest == true;
        watchImaniAdButton.text = ready ? "WERBUNG ANSEHEN" :
            canRequest ? "WERBUNG LADEN" : "WERBUNG WIRD GELADEN...";
        watchImaniAdButton.SetEnabled(signedIn && (ready || canRequest));
    }

    private void ShowImaniRewardedAd()
    {
        if (onlineService?.IsSignedIn != true)
        {
            storeMessageLabel.text = "FÜR DIE FREISCHALTUNG ONLINE ANMELDEN";
            return;
        }
        rewardedAdService?.Show(onlineService.Profile?.playerId);
    }

    private void HandleImaniRewardEarned()
    {
        _ = ClaimImaniRewardAsync();
    }

    private async Task ClaimImaniRewardAsync()
    {
        watchImaniAdButton.SetEnabled(false);
        watchImaniAdButton.text = "WIRD FREIGESCHALTET...";
        try
        {
            await onlineService.ClaimImaniRewardedAdAsync();
            storeMessageLabel.text = "IMANI CROSS DAUERHAFT FREIGESCHALTET";
        }
        catch (Exception exception)
        {
            storeMessageLabel.text = exception.Message.ToUpperInvariant();
        }
        RenderStore();
    }

    private void ConfigureStoreButton(Button button, string productId, bool owned)
    {
        if (owned)
        {
            button.text = "FREIGESCHALTET";
            button.SetEnabled(false);
            return;
        }

        bool available = iapService?.HasProduct(productId) == true;
        button.text = available ? iapService.GetLocalizedPrice(productId) : "NICHT VERFÜGBAR";
        button.SetEnabled(available);
    }

    private void UpdateRewardCodeControls()
    {
        if (rewardCodeField == null || redeemRewardCodeButton == null) return;
        bool signedIn = onlineService?.IsSignedIn == true;
        rewardCodeField.SetEnabled(signedIn && !rewardCodeRedemptionPending);
        redeemRewardCodeButton.SetEnabled(signedIn && !rewardCodeRedemptionPending &&
            !string.IsNullOrWhiteSpace(rewardCodeField.value));
    }

    private async Task RedeemRewardCodeAsync()
    {
        string code = rewardCodeField?.value?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            storeMessageLabel.text = "BELOHNUNGSCODE EINGEBEN";
            return;
        }

        rewardCodeRedemptionPending = true;
        UpdateRewardCodeControls();
        storeMessageLabel.text = "BELOHNUNGSCODE WIRD GEPRÜFT...";
        try
        {
            await onlineService.RedeemRewardCodeAsync(code);
            rewardCodeField.value = string.Empty;
            RenderStore();
            storeMessageLabel.text = "ALLE KOMMANDANTEN FREIGESCHALTET";
        }
        catch (Exception exception)
        {
            storeMessageLabel.text = exception.Message.ToUpperInvariant().Contains("REWARD_CODE")
                ? "BELOHNUNGSCODE UNGÜLTIG"
                : exception.Message.ToUpperInvariant();
        }
        finally
        {
            rewardCodeRedemptionPending = false;
            UpdateRewardCodeControls();
        }
    }

    private void BuyProduct(string productId)
    {
        try
        {
            iapService?.Purchase(productId);
        }
        catch (Exception exception)
        {
            storeMessageLabel.text = exception.Message.ToUpperInvariant();
        }
    }

    private void RestorePurchases()
    {
        try
        {
            iapService?.RestorePurchases();
        }
        catch (Exception exception)
        {
            storeMessageLabel.text = exception.Message.ToUpperInvariant();
        }
    }

    private void OpenProfileEditor()
    {
        if (onlineService?.IsSignedIn != true)
        {
            OpenOnlineLogin(4);
            return;
        }
        NavalPlayerProfile profile = onlineService?.Profile;
        profileNameField.value = profile?.displayName ?? string.Empty;
        profileMessageLabel.text = string.Empty;
        bool signedIn = onlineService?.IsSignedIn == true;
        profileNameField.SetEnabled(signedIn);
        saveProfileButton.SetEnabled(signedIn);
        signOutButton.EnableInClassList("hidden", !signedIn);
        deleteAccountButton.EnableInClassList("hidden", !signedIn);
        profileLoginButton.EnableInClassList("hidden", signedIn);
        RenderProfilePresentation(profile, signedIn);
    }

    private async Task SaveProfileAsync()
    {
        saveProfileButton.SetEnabled(false);
        profileMessageLabel.text = "PROFIL WIRD GESPEICHERT...";
        try
        {
            await onlineService.UpdateDisplayNameAsync(profileNameField.value);
            profileMessageLabel.text = "PROFIL AKTUALISIERT";
            RenderProfile();
            CloseProfileAccountMenu();
        }
        catch (Exception exception)
        {
            profileMessageLabel.text = exception.Message.ToUpperInvariant();
        }
        finally
        {
            saveProfileButton.SetEnabled(true);
        }
    }

    private async Task RefreshFriendsAsync(bool quiet = false)
    {
        if (onlineService == null || !onlineService.IsSignedIn) return;
        if (!quiet)
        {
            friendsList.Clear();
            friendsList.Add(CreateOnlineInfoLabel("FREUNDE LADEN…"));
        }
        try
        {
            IReadOnlyList<NavalFriendProfile> friends = await onlineService.GetFriendsAsync();
            if (this == null || !onlineService.IsSignedIn) return;
            RenderFriends(friends);
        }
        catch (Exception exception)
        {
            if (this == null || !onlineService.IsSignedIn) return;
            friendsList.Clear();
            friendsList.Add(CreateOnlineInfoLabel("FREUNDE NICHT VERFÜGBAR"));
            ShowHubMessage(exception.Message);
        }
    }

    private async Task AddFriendAsync()
    {
        if (onlineService?.IsSignedIn != true) { OpenOnlineLogin(1); return; }
        if (friendRequestPending) return;
        string playerName = friendNameField.value;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            ShowHubMessage("SPIELERNAME ODER FREUNDESCODE EINGEBEN");
            return;
        }

        friendRequestPending = true;
        addFriendButton.SetEnabled(false);
        try
        {
            await onlineService.SendFriendRequestAsync(playerName);
            friendNameField.value = string.Empty;
            ShowHubMessage("FREUNDESANFRAGE GESENDET");
            await RefreshFriendsAsync();
        }
        catch (Exception exception)
        {
            ShowHubMessage(exception.Message);
        }
        finally
        {
            friendRequestPending = false;
            addFriendButton.SetEnabled(onlineService?.IsSignedIn == true);
        }
    }

    private void RenderFriends(IReadOnlyList<NavalFriendProfile> friends)
    {
        friendsList.Clear();
        int accepted = 0;
        for (int i = 0; i < friends.Count; i++)
        {
            NavalFriendProfile friend = friends[i];
            if (!friend.incomingRequest && !friend.outgoingRequest && !friend.blocked) accepted++;
            friendsList.Add(CreateFriendRow(friend));
        }

        friendCountLabel.text = accepted + " FREUNDE";
        if (friends.Count == 0)
        {
            friendsList.Add(CreateOnlineInfoLabel("NOCH KEINE FREUNDE"));
        }
    }

    private async Task RefreshInvitesAsync()
    {
        if (onlineService == null || !onlineService.IsSignedIn) return;
        try
        {
            IReadOnlyList<NavalFriendlyInvite> invites = await onlineService.GetFriendlyInvitesAsync();
            if (this == null || !onlineService.IsSignedIn) return;
            invitesList.Clear();
            invitesPanel.EnableInClassList("hidden", invites.Count == 0);
            for (int index = 0; index < invites.Count; index++)
            {
                NavalFriendlyInvite invite = invites[index];
                VisualElement row = new VisualElement();
                row.AddToClassList("friend-row");
                Label label = new Label(string.IsNullOrWhiteSpace(invite.senderDisplayName) ? "COMMANDER" : invite.senderDisplayName);
                label.AddToClassList("friend-name");
                row.Add(label);
                Button accept = CreateFriendButton("DUELL ANNEHMEN");
                accept.clicked += () => BeginAcceptFriendlyFlow(invite.inviteId);
                row.Add(accept);
                Button decline = CreateFriendButton("ABLEHNEN");
                decline.clicked += () => _ = DeclineInviteAsync(invite.inviteId);
                row.Add(decline);
                invitesList.Add(row);
            }
        }
        catch (Exception exception)
        {
            if (this == null || !onlineService.IsSignedIn) return;
            invitesPanel.EnableInClassList("hidden", true);
            ShowHubMessage(exception.Message);
        }
    }

    private async Task DeclineInviteAsync(string inviteId)
    {
        try
        {
            await onlineService.DeclineFriendlyMatchAsync(inviteId);
            await RefreshInvitesAsync();
        }
        catch (Exception exception) { ShowHubMessage(exception.Message); }
    }

    private async Task RefreshSocialAsync()
    {
        if (socialRefreshPending || onlineService?.IsSignedIn != true) return;
        socialRefreshPending = true;
        try
        {
            await Task.WhenAll(RefreshFriendsAsync(true), RefreshInvitesAsync());
        }
        finally
        {
            socialRefreshPending = false;
            nextSocialRefreshUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 6000;
        }
    }

    private async Task ShowLeaderboardAsync()
    {
        leaderboardList.Clear();
        leaderboardMessageLabel.text = "RANGLISTE WIRD GELADEN...";
        try
        {
            IReadOnlyList<NavalLeaderboardEntry> entries = await onlineService.GetLeaderboardAsync();
            for (int index = 0; index < entries.Count; index++)
            {
                NavalLeaderboardEntry entry = entries[index];
                VisualElement row = new VisualElement();
                row.AddToClassList("friend-row");
                Label rank = new Label("#" + entry.rank);
                rank.AddToClassList("friend-state");
                row.Add(rank);
                Label name = new Label(string.IsNullOrWhiteSpace(entry.displayName) ? "COMMANDER" : entry.displayName);
                name.AddToClassList("friend-name");
                row.Add(name);
                Label score = new Label(entry.league + " // " + entry.mmr + " RP");
                score.AddToClassList("friend-state");
                row.Add(score);
                leaderboardList.Add(row);
            }
            leaderboardMessageLabel.text = entries.Count == 0 ? "NOCH KEINE RANGLISTENSPIELE" : string.Empty;
        }
        catch (Exception exception)
        {
            leaderboardMessageLabel.text = exception.Message.ToUpperInvariant();
        }
    }

    private VisualElement CreateFriendRow(NavalFriendProfile friend)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("friend-row");
        if (!friend.online) row.AddToClassList("friend-row-offline");
        if (friend.incomingRequest || friend.outgoingRequest) row.AddToClassList("friend-row-request");

        Label name = new Label(string.IsNullOrWhiteSpace(friend.displayName) ? "COMMANDER" : friend.displayName);
        name.AddToClassList("friend-name");
        row.Add(name);

        string state = friend.blocked ? "BLOCKIERT" : friend.incomingRequest ? "ANFRAGE" :
            friend.outgoingRequest ? "AUSSTEHEND" : friend.online ? "ONLINE" : "OFFLINE";
        Label stateLabel = new Label(state);
        stateLabel.AddToClassList("friend-state");
        row.Add(stateLabel);

        if (friend.blocked)
        {
            Button unblock = CreateFriendButton("FREIGEBEN");
            unblock.clicked += () => _ = UnblockFriendAsync(friend.playerId);
            row.Add(unblock);
        }
        else if (friend.incomingRequest)
        {
            Button accept = CreateFriendButton("ANNEHMEN");
            accept.clicked += () => _ = AcceptFriendAsync(friend.playerId);
            row.Add(accept);
            Button block = CreateFriendButton("BLOCK");
            block.clicked += () => _ = BlockFriendAsync(friend.playerId);
            row.Add(block);
        }
        else if (!friend.outgoingRequest && !friend.blocked)
        {
            Button duel = CreateFriendButton("DUELL");
            // Presence can be delayed; an accepted friend can always receive an invitation.
            duel.SetEnabled(onlineService?.IsSignedIn == true);
            duel.clicked += () => BeginFriendlyFlow(friend.playerId);
            row.Add(duel);
            Button remove = CreateFriendButton("ENTF.");
            remove.clicked += () => _ = RemoveFriendAsync(friend.playerId);
            row.Add(remove);
        }
        else if (friend.outgoingRequest)
        {
            Button remove = CreateFriendButton("ZURÜCK");
            remove.clicked += () => _ = RemoveFriendAsync(friend.playerId);
            row.Add(remove);
        }

        return row;
    }

    private async Task AcceptFriendAsync(string playerId)
    {
        try
        {
            await onlineService.AcceptFriendRequestAsync(playerId);
            await RefreshFriendsAsync();
        }
        catch (Exception exception)
        {
            ShowHubMessage(exception.Message);
        }
    }

    private async Task RemoveFriendAsync(string playerId)
    {
        try
        {
            await onlineService.RemoveFriendAsync(playerId);
            await RefreshFriendsAsync();
        }
        catch (Exception exception) { ShowHubMessage(exception.Message); }
    }

    private async Task BlockFriendAsync(string playerId)
    {
        try
        {
            await onlineService.BlockPlayerAsync(playerId);
            await RefreshFriendsAsync();
        }
        catch (Exception exception) { ShowHubMessage(exception.Message); }
    }

    private void BeginRankedFlow()
    {
        onlineFlowMode = OnlineFlowMode.Ranked;
        friendlyOpponentId = null;
        ShowCommanderSelection();
    }

    private void BeginFriendlyFlow(string opponentId)
    {
        onlineFlowMode = OnlineFlowMode.Friendly;
        friendlyOpponentId = opponentId;
        pendingFriendlyInviteId = null;
        ShowCommanderSelection();
    }

    private void BeginAcceptFriendlyFlow(string inviteId)
    {
        onlineFlowMode = OnlineFlowMode.FriendlyAccept;
        pendingFriendlyInviteId = inviteId;
        friendlyOpponentId = null;
        ShowCommanderSelection();
    }

    private bool IsOnlineSetup => onlineFlowMode != OnlineFlowMode.None;

    private async Task BeginOnlineQueueAsync()
    {
        if (queueRequestPending || cancelQueuePending) return;
        if (onlineService?.IsSignedIn != true) { OpenOnlineLogin(1); return; }
        queueRequestPending = true;
        beginBattleButton.SetEnabled(false);
        cancelMatchmakingButton.SetEnabled(false);
        matchmakingStatusLabel.text = onlineFlowMode == OnlineFlowMode.Ranked
            ? "GEGNER SUCHEN…"
            : "DUELL VORBEREITEN…";
        ShowOnly(matchmakingScreen);

        try
        {
            NavalPendingLoadout loadout = CreateOnlineLoadout();
            if (onlineFlowMode == OnlineFlowMode.Ranked)
                activeMatchTicket = await onlineService.QueueRankedAsync(loadout);
            else if (onlineFlowMode == OnlineFlowMode.FriendlyAccept)
                activeMatchTicket = await onlineService.AcceptFriendlyMatchAsync(pendingFriendlyInviteId, loadout);
            else
                activeMatchTicket = await onlineService.CreateFriendlyMatchAsync(friendlyOpponentId, loadout);
            if (activeMatchTicket == null) throw new InvalidOperationException("BITTE ERNEUT VERSUCHEN");
            matchmakingStatusLabel.text = onlineFlowMode == OnlineFlowMode.Ranked
                ? "GEGNER WIRD GESUCHT…"
                : string.IsNullOrWhiteSpace(activeMatchTicket.matchId)
                    ? "WARTE AUF DEINEN FREUND…"
                    : "DUELL STARTET…";
            if (!string.IsNullOrWhiteSpace(activeMatchTicket.matchId))
                await onlineService.GetMatchViewAsync(activeMatchTicket.matchId);
            else
                nextMatchmakingPollUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1500;
        }
        catch (Exception exception)
        {
            ShowOnly(setupScreen);
            setupProgressLabel.text = "ONLINE-FEHLER // " + exception.Message.ToUpperInvariant();
            beginBattleButton.SetEnabled(true);
        }
        finally
        {
            queueRequestPending = false;
            cancelMatchmakingButton.SetEnabled(true);
        }
    }

    private async Task UnblockFriendAsync(string playerId)
    {
        try
        {
            await onlineService.UnblockPlayerAsync(playerId);
            await RefreshFriendsAsync();
        }
        catch (Exception exception) { ShowHubMessage(exception.Message); }
    }

    private async Task CancelMatchmakingAsync()
    {
        if (queueRequestPending || cancelQueuePending) return;
        cancelQueuePending = true;
        cancelMatchmakingButton.SetEnabled(false);
        try
        {
            if (activeMatchTicket != null && !string.IsNullOrWhiteSpace(activeMatchTicket.ticketId))
            {
                if (onlineFlowMode == OnlineFlowMode.Friendly)
                {
                    NavalMatchTicket ticket = await onlineService.CancelFriendlyMatchAsync(friendlyOpponentId, activeMatchTicket.ticketId);
                    if (!string.IsNullOrWhiteSpace(ticket?.matchId))
                        await onlineService.GetMatchViewAsync(ticket.matchId);
                }
                else await onlineService.CancelQueueAsync(activeMatchTicket.ticketId);
            }
            if (IsActiveOnlineMatch) return;
            activeMatchTicket = null;
            onlineFlowMode = OnlineFlowMode.None;
            await ShowOnlineHubAsync();
        }
        catch (Exception)
        {
            matchmakingStatusLabel.text = "ABBRUCH NICHT BESTÄTIGT. ERNEUT VERSUCHEN.";
        }
        finally
        {
            cancelQueuePending = false;
            cancelMatchmakingButton.SetEnabled(true);
        }
    }

    private void UpdateOnlineMatchmaking()
    {
        if ((onlineFlowMode != OnlineFlowMode.Ranked && onlineFlowMode != OnlineFlowMode.Friendly) || activeMatchTicket == null ||
            !string.IsNullOrWhiteSpace(activeMatchTicket.matchId) || matchmakingPollPending || cancelQueuePending || IsActiveOnlineMatch) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now < nextMatchmakingPollUnixMs) return;
        matchmakingStatusLabel.text = onlineFlowMode == OnlineFlowMode.Friendly
            ? "WARTE AUF DEINEN FREUND…" : "GEGNER WIRD GESUCHT…";
        nextMatchmakingPollUnixMs = now + 2000;
        _ = PollRankedAsync();
    }

    private async Task PollRankedAsync()
    {
        NavalMatchTicket requestedTicket = activeMatchTicket;
        if (requestedTicket == null) return;
        matchmakingPollPending = true;
        try
        {
            NavalMatchTicket ticket = onlineFlowMode == OnlineFlowMode.Friendly
                ? await onlineService.PollFriendlyMatchAsync(friendlyOpponentId, requestedTicket.ticketId)
                : await onlineService.PollRankedAsync(requestedTicket.ticketId);
            if (this == null || activeMatchTicket != requestedTicket || cancelQueuePending) return;
            if (ticket == null) return;
            if (!string.IsNullOrWhiteSpace(ticket.matchId))
            {
                activeMatchTicket = ticket;
                await onlineService.GetMatchViewAsync(ticket.matchId);
            }
            else if (ticket.state == "expired" || ticket.state == "cancelled" || ticket.state == "declined")
            {
                activeMatchTicket = null;
                onlineFlowMode = OnlineFlowMode.None;
                ShowHubMessage(ticket.state == "declined" ? "EINLADUNG ABGELEHNT" : "SUCHE BEENDET. DU KANNST ERNEUT STARTEN.");
                await ShowOnlineHubAsync();
            }
        }
        catch (Exception)
        {
            if (activeMatchTicket != requestedTicket) return;
            matchmakingStatusLabel.text = "VERBINDUNG WIRD WIEDERHERGESTELLT…";
            nextMatchmakingPollUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3000;
        }
        finally
        {
            matchmakingPollPending = false;
        }
    }

    private NavalPendingLoadout CreateOnlineLoadout()
    {
        NavalPendingLoadout loadout = new NavalPendingLoadout { commanderId = currentCommander.id };
        for (int i = 0; i < playerShips.Length; i++)
        {
            loadout.ships.Add(new NavalShipPlacement
            {
                length = playerShips[i].length,
                width = playerShips[i].width,
                height = playerShips[i].height,
                row = playerShips[i].row,
                column = playerShips[i].column,
                vertical = playerShips[i].vertical
            });
        }
        return loadout;
    }

    private void HandleOnlineMatchChanged(NavalPlayerMatchView view)
    {
        if (view == null) return;
        if (onlineMatchView != null && onlineMatchView.matchId == view.matchId && view.version < onlineMatchView.version) return;
        bool refreshExistingBattle = IsActiveOnlineMatch &&
                                     onlineMatchView != null &&
                                     onlineMatchView.matchId == view.matchId &&
                                     !battleScreen.ClassListContains("hidden");
        onlineMatchView = view;
        if (view.status == NavalMatchStatus.InProgress)
        {
            nextMatchmakingPollUnixMs = 0;
            matchmakingStatusLabel.text = "GEGNER GEFUNDEN";
            if (IsRankedMatchFoundSequence(view.matchId))
            {
                return;
            }
            if (!refreshExistingBattle && ShouldPlayRankedMatchFound(view))
            {
                BeginRankedMatchFoundSequence(view);
            }
            else if (refreshExistingBattle)
            {
                RefreshOnlineBattle(view);
            }
            else
            {
                EnterOnlineBattle(view);
            }
        }
        else if (view.status == NavalMatchStatus.Finished)
        {
            if (!refreshExistingBattle) EnterOnlineBattle(view);
            else RefreshOnlineBattle(view);
        }
    }

    private void RefreshOnlineState()
    {
        if (onlineService == null) return;
        onlineStatusLabel.text = onlineService.Status == NavalOnlineStatus.Error
            ? "NC // CLOUD NICHT VERFÜGBAR"
            : onlineService.IsSignedIn
                ? "NC // ONLINE GESICHERT"
                : "NC // OFFLINE BEREIT";

        string message = onlineService.Status == NavalOnlineStatus.Error ? onlineService.LastError :
            onlineService.Status == NavalOnlineStatus.Initializing ? "VERBINDEN…" :
            onlineService.Status == NavalOnlineStatus.SigningIn ? "ANMELDUNG LÄUFT…" : string.Empty;
        loginStatusLabel.text = message;
        onlineHubStatusLabel.text = onlineService.Status == NavalOnlineStatus.Error ? "VERBINDUNGSFEHLER" : "SICHERE VERBINDUNG";
        RenderProfile();
        SetOnlineButtonsEnabled(ageConsentToggle.value);
        friendNameField.SetEnabled(onlineService.IsSignedIn);
        addFriendButton.SetEnabled(onlineService.IsSignedIn && !friendRequestPending);
        refreshFriendsButton.SetEnabled(onlineService.IsSignedIn);
        if (!onlineService.IsSignedIn && onlineMatchView?.status == NavalMatchStatus.InProgress)
        {
            loginReturnTab = 2;
            ShowOnly(onlineLoginScreen);
        }
    }

    private void RenderProfile()
    {
        if (profileNameLabel == null) return;
        NavalPlayerProfile profile = onlineService?.Profile;
        profileNameLabel.text = profile?.displayName ?? "COMMANDER";
        string playerName = Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized &&
            Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn
                ? Unity.Services.Authentication.AuthenticationService.Instance.PlayerName
                : string.Empty;
        profileCodeLabel.text = string.IsNullOrWhiteSpace(playerName)
            ? "CODE " + (profile?.friendCode ?? "--------")
            : "ID " + playerName + "   CODE " + (profile?.friendCode ?? "--------");
        profileRankLabel.text = (profile?.league ?? "PLATZIERUNG") + " // " + (profile?.mmr ?? NavalRankRules.InitialMmr) + " RP";
        profileStatsLabel.text = (profile?.lifetimeWins ?? 0) + " SIEGE // " +
            (profile?.lifetimeLosses ?? 0) + " NIEDERLAGEN";
        string season = profile?.seasonId;
        if (string.IsNullOrWhiteSpace(season))
            season = NavalSeasonRules.GetSeasonId(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        seasonLabel.text = "RANKED // SAISON " + season.TrimStart('S');
        bool signedIn = onlineService?.IsSignedIn == true;
        playLoginButton?.EnableInClassList("hidden", signedIn);
        RenderProfilePresentation(profile, signedIn);
        RefreshDeveloperAdminPanel();
        UpdateGameModeUi();
    }

    private void ResetOnlineFlowForMenu()
    {
        onlineFlowMode = OnlineFlowMode.None;
        friendlyOpponentId = null;
        pendingFriendlyInviteId = null;
        activeMatchTicket = null;
        onlineMatchView = null;
        matchmakingPollPending = false;
        nextMatchmakingPollUnixMs = 0;
        ResetRankedMatchFoundSequence();
        if (surrenderButton != null) surrenderButton.EnableInClassList("hidden", true);
    }

    private void ShowLoginMessage(string message)
    {
        loginStatusLabel.text = string.IsNullOrWhiteSpace(message) ? "ANMELDUNG NICHT VERFÜGBAR" : message.ToUpperInvariant();
    }

    private void ShowHubMessage(string message)
    {
        if (onlineHubMessageLabel != null)
            onlineHubMessageLabel.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.ToUpperInvariant();
    }

    private void SetOnlineButtonsEnabled(bool enabled)
    {
        enabled = enabled && !loginPending && onlineService?.Status != NavalOnlineStatus.Initializing &&
            onlineService?.Status != NavalOnlineStatus.SigningIn;
        playerAccountLoginButton.SetEnabled(enabled);
        webSignInButton.SetEnabled(enabled);
        webRegisterButton.SetEnabled(enabled);
        appleLoginButton.SetEnabled(enabled);
        googleLoginButton.SetEnabled(enabled);
    }

    private static Label CreateOnlineInfoLabel(string text)
    {
        Label label = new Label(text);
        label.AddToClassList("online-message");
        return label;
    }

    private static Button CreateFriendButton(string text)
    {
        Button button = new Button { text = text };
        button.AddToClassList("secondary-button");
        button.AddToClassList("friend-action");
        return button;
    }

    private static string ShortId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "--------";
        return value.Substring(0, Mathf.Min(8, value.Length)).ToUpperInvariant();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) QueueOnlineResumeRefresh();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) QueueOnlineResumeRefresh();
    }

    private void QueueOnlineResumeRefresh()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (resumeRefreshPending || now - lastResumeRefreshUnixMs < 1000) return;
        lastResumeRefreshUnixMs = now;
        _ = ResumeOnlineSessionAsync();
    }

    private async Task ResumeOnlineSessionAsync()
    {
        if (onlineService == null || !onlineService.IsSignedIn) return;
        resumeRefreshPending = true;
        try
        {
            if (onlineMatchView != null && onlineMatchView.status == NavalMatchStatus.InProgress)
                await onlineService.GetMatchViewAsync(onlineMatchView.matchId);
            else
                await onlineService.ReconnectMatchAsync();
        }
        catch (Exception exception)
        {
            ShowHubMessage("WIEDERVERBINDUNG // " + exception.Message);
        }
        finally
        {
            resumeRefreshPending = false;
        }
    }

    private void UpdateOnlineRefresh()
    {
        if (onlineService?.IsSignedIn != true) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!onlineHubScreen.ClassListContains("hidden") && activeOnlineTab == 1 && now >= nextSocialRefreshUnixMs)
            _ = RefreshSocialAsync();
        if (onlineMatchView?.status == NavalMatchStatus.InProgress && !resumeRefreshPending && now >= nextBattleRefreshUnixMs)
        {
            nextBattleRefreshUnixMs = now + 2000;
            _ = ResumeOnlineSessionAsync();
        }
    }
}
