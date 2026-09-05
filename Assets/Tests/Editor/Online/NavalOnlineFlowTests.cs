using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class NavalOnlineFlowTests
{
    private GameObject host;
    private NavalGameController controller;
    private VisualElement root;
    private FakeNavalOnlineService service;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("OnlineFlowRegression");
        UIDocument document = host.AddComponent<UIDocument>();
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu/MainMenu.uxml");
        root = document.rootVisualElement;
        controller = host.AddComponent<NavalGameController>();
        Set("uiDocument", document);
        Call("InitializeCommanderSystem");
        Call("CacheUi");
        service = new FakeNavalOnlineService();
        Set("onlineService", service);
        Call("BindOnlineUi");
        Call("ShowOnlineAppHome");
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

    [TestCase(4)]
    [TestCase(1)]
    public void SignedOutProtectedTab_OpensLoginImmediately(int tab)
    {
        service.IsSignedIn = false;
        Call("SwitchOnlineTab", tab, true);
        Assert.That(root.Q("OnlineLoginScreen").ClassListContains("hidden"), Is.False);
        Assert.That(root.Q("OnlineHubScreen").ClassListContains("hidden"), Is.True);
        Assert.That(Get<int>("loginReturnTab"), Is.EqualTo(tab));
    }

    [Test]
    public async Task ProfileLogin_ReturnsToProfileAfterSuccess()
    {
        service.IsSignedIn = false;
        Call("SwitchOnlineTab", 4, true);
        root.Q<Toggle>("AgeConsentToggle").value = true;
        await Run("SignInWithPlayerAccountAsync");
        Assert.That(Get<int>("activeOnlineTab"), Is.EqualTo(4));
        Assert.That(root.Q("OnlineLoginScreen").ClassListContains("hidden"), Is.True);
        Assert.That(root.Q("ProfileScreen").ClassListContains("hidden"), Is.False);
    }

    [Test]
    public async Task LoginInFlight_ConsentChangesDoNotEnableDuplicateSignIn()
    {
        var pending = new TaskCompletionSource<bool>();
        service.IsSignedIn = false;
        service.SignInWithPlayerAccountHandler = () => pending.Task;
        root.Q<Toggle>("AgeConsentToggle").value = true;
        Task first = Run("SignInWithPlayerAccountAsync");
        root.Q<Toggle>("AgeConsentToggle").value = false;
        root.Q<Toggle>("AgeConsentToggle").value = true;
        Assert.That(root.Q<Button>("PlayerAccountLoginButton").enabledSelf, Is.False);
        await Run("SignInWithPlayerAccountAsync");
        Assert.That(service.SignInWithPlayerAccountCallCount, Is.EqualTo(1));
        pending.SetResult(true);
        await first;
    }

    [Test]
    public void FriendWithUnavailablePresence_CanStillBeInvited()
    {
        VisualElement row = (VisualElement)Call("CreateFriendRow", new NavalFriendProfile { playerId = "friend", online = false });
        Button duel = row.Query<Button>().ToList().Find(button => button.text == "DUELL");
        Assert.That(duel, Is.Not.Null);
        Assert.That(duel.enabledSelf, Is.True);
    }

    [Test]
    public async Task FriendlyCancellation_UsesInvitationEndpoint()
    {
        SetFlow("Friendly");
        Set("friendlyOpponentId", "friend");
        Set("activeMatchTicket", new NavalMatchTicket { ticketId = "invite" });
        await Run("CancelMatchmakingAsync");
        Assert.That(service.CancelFriendlyMatchCallCount, Is.EqualTo(1));
        Assert.That(service.CancelQueueCallCount, Is.Zero);
        Assert.That(Get<NavalMatchTicket>("activeMatchTicket"), Is.Null);
    }

    [Test]
    public async Task FailedCancellation_KeepsTicketForRetry()
    {
        SetFlow("Ranked");
        var ticket = new NavalMatchTicket { ticketId = "queue" };
        Set("activeMatchTicket", ticket);
        service.CancelQueueHandler = _ => Task.FromException(new Exception("network unavailable"));
        await Run("CancelMatchmakingAsync");
        Assert.That(Get<NavalMatchTicket>("activeMatchTicket"), Is.SameAs(ticket));
        Assert.That(root.Q<Label>("MatchmakingStatusLabel").text, Does.Contain("ERNEUT"));
    }

    [Test]
    public async Task LatePollAfterCancel_DoesNotStartMatch()
    {
        SetFlow("Ranked");
        Set("activeMatchTicket", new NavalMatchTicket { ticketId = "queue" });
        var pending = new TaskCompletionSource<NavalMatchTicket>();
        service.PollRankedHandler = _ => pending.Task;
        Task poll = Run("PollRankedAsync");
        await Run("CancelMatchmakingAsync");
        pending.SetResult(new NavalMatchTicket { ticketId = "queue", matchId = "late" });
        await poll;
        Assert.That(service.GetMatchViewCallCount, Is.Zero);
        Assert.That(Get<NavalMatchTicket>("activeMatchTicket"), Is.Null);
    }

    [Test]
    public async Task FriendlyWait_PollsWithoutPush()
    {
        SetFlow("Friendly");
        Set("friendlyOpponentId", "friend");
        Set("activeMatchTicket", new NavalMatchTicket { ticketId = "invite" });
        await Run("PollRankedAsync");
        Assert.That(service.PollFriendlyMatchCallCount, Is.EqualTo(1));
        Assert.That(service.PollRankedCallCount, Is.Zero);
    }

    [Test]
    public void StaleMatchPush_DoesNotRollBackCurrentView()
    {
        var current = new NavalPlayerMatchView { matchId = "match", version = 9, status = NavalMatchStatus.InProgress };
        Set("onlineMatchView", current);
        Call("HandleOnlineMatchChanged", new NavalPlayerMatchView { matchId = "match", version = 8, status = NavalMatchStatus.InProgress });
        Assert.That(Get<NavalPlayerMatchView>("onlineMatchView"), Is.SameAs(current));
    }

    [Test]
    public void ServiceStaleResponse_ReturnsNewestKnownView()
    {
        var online = new UgsNavalOnlineService();
        MethodInfo notify = typeof(UgsNavalOnlineService).GetMethod("NotifyMatch", Private);
        var current = new NavalPlayerMatchView { matchId = "match", version = 9 };
        notify.Invoke(online, new object[] { current });
        object result = notify.Invoke(online, new object[] { new NavalPlayerMatchView { matchId = "match", version = 8 } });
        Assert.That(result, Is.SameAs(current));
        online.Dispose();
    }

    private void SetFlow(string mode)
    {
        FieldInfo field = typeof(NavalGameController).GetField("onlineFlowMode", Private);
        field.SetValue(controller, Enum.Parse(field.FieldType, mode));
    }

    [Test]
    public void ReconnectedFleet_RestoresServerPositionsAndOrientation()
    {
        var view = new NavalPlayerMatchView();
        view.ownShips.Add(new NavalShipPlacement { length = 3, width = 3, height = 1, row = 4, column = 6, vertical = true });
        Call("SyncOnlineFleet", view);
        int[,] board = Get<int[,]>("playerBoard");
        Assert.That(board[4, 6], Is.Zero);
        Assert.That(board[5, 6], Is.Zero);
        Assert.That(board[6, 6], Is.Zero);
        Assert.That(board[4, 7], Is.EqualTo(-1));
        view.ownShips[0].row = 0;
        Call("SyncOnlineFleet", view);
        Assert.That(Get<int[,]>("playerBoard")[4, 6], Is.EqualTo(-1));
        Assert.That(Get<int[,]>("playerBoard")[0, 6], Is.Zero);
    }
    private void Set(string name, object value) => typeof(NavalGameController).GetField(name, Private).SetValue(controller, value);
    private T Get<T>(string name) => (T)typeof(NavalGameController).GetField(name, Private).GetValue(controller);
    private object Call(string name, params object[] args) => typeof(NavalGameController).GetMethod(name, Private).Invoke(controller, args);
    private Task Run(string name) => (Task)Call(name);
}
