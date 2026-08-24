using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private VisualElement rankedMatchFoundScreen;
    private VisualElement rankedMatchOwnAvatar;
    private VisualElement rankedMatchOpponentAvatar;
    private Label rankedMatchOwnInitials;
    private Label rankedMatchOpponentInitials;
    private Label rankedMatchOwnName;
    private Label rankedMatchOpponentName;
    private Label rankedMatchFoundStatus;
    private Texture2D rankedMatchOwnAvatarTexture;
    private Texture2D rankedMatchOpponentAvatarTexture;
    private string rankedMatchFoundMatchId;
    private int rankedMatchFoundGeneration;

    private void CacheRankedMatchFoundUi(VisualElement root)
    {
        rankedMatchFoundScreen = root.Q<VisualElement>("RankedMatchFoundScreen");
        rankedMatchOwnAvatar = root.Q<VisualElement>("RankedMatchOwnAvatar");
        rankedMatchOpponentAvatar = root.Q<VisualElement>("RankedMatchOpponentAvatar");
        rankedMatchOwnInitials = root.Q<Label>("RankedMatchOwnInitials");
        rankedMatchOpponentInitials = root.Q<Label>("RankedMatchOpponentInitials");
        rankedMatchOwnName = root.Q<Label>("RankedMatchOwnName");
        rankedMatchOpponentName = root.Q<Label>("RankedMatchOpponentName");
        rankedMatchFoundStatus = root.Q<Label>("RankedMatchFoundStatus");
    }

    private bool ShouldPlayRankedMatchFound(NavalPlayerMatchView view)
    {
        return view != null && view.mode == NavalMatchMode.Ranked &&
               onlineFlowMode == OnlineFlowMode.Ranked && matchmakingScreen != null &&
               !matchmakingScreen.ClassListContains("hidden");
    }

    private bool IsRankedMatchFoundSequence(string matchId)
    {
        return !string.IsNullOrWhiteSpace(matchId) && rankedMatchFoundMatchId == matchId;
    }

    private void BeginRankedMatchFoundSequence(NavalPlayerMatchView view)
    {
        if (view == null || IsRankedMatchFoundSequence(view.matchId)) return;
        rankedMatchFoundMatchId = view.matchId;
        int generation = ++rankedMatchFoundGeneration;
        ApplyRankedMatchFoundProfiles(view, null);
        StartCoroutine(PlayRankedMatchFound(generation, view.matchId));
        _ = LoadRankedMatchFoundProfilesAsync(view, generation);
    }

    private async Task LoadRankedMatchFoundProfilesAsync(NavalPlayerMatchView view, int generation)
    {
        NavalMatchIntro intro = null;
        try
        {
            intro = await onlineService.GetMatchIntroAsync(view.matchId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Ranked match intro profile data could not be loaded: " + exception.Message);
        }

        if (generation != rankedMatchFoundGeneration || rankedMatchFoundMatchId != view.matchId ||
            onlineMatchView == null || onlineMatchView.matchId != view.matchId) return;

        ApplyRankedMatchFoundProfiles(view, intro);
    }

    private void ApplyRankedMatchFoundProfiles(NavalPlayerMatchView view, NavalMatchIntro intro)
    {
        string ownName = string.IsNullOrWhiteSpace(intro?.ownDisplayName)
            ? onlineService?.Profile?.displayName ?? "COMMANDER"
            : intro.ownDisplayName;
        string opponentName = string.IsNullOrWhiteSpace(intro?.opponentDisplayName)
            ? view.opponentDisplayName ?? "GEGNER"
            : intro.opponentDisplayName;
        rankedMatchOwnName.text = ownName.ToUpperInvariant();
        rankedMatchOpponentName.text = opponentName.ToUpperInvariant();
        RenderRankedMatchAvatar(rankedMatchOwnAvatar, rankedMatchOwnInitials,
            intro?.ownAvatarImageBase64 ?? onlineService?.Profile?.avatarImageBase64,
            ownName, ref rankedMatchOwnAvatarTexture);
        RenderRankedMatchAvatar(rankedMatchOpponentAvatar, rankedMatchOpponentInitials,
            intro?.opponentAvatarImageBase64, opponentName, ref rankedMatchOpponentAvatarTexture);
    }

    private IEnumerator PlayRankedMatchFound(int generation, string matchId)
    {
        rankedMatchFoundScreen.RemoveFromClassList("ranked-match-found-visible");
        rankedMatchFoundScreen.RemoveFromClassList("ranked-match-found-cards-in");
        rankedMatchFoundScreen.RemoveFromClassList("ranked-match-found-vs-impact");
        rankedMatchFoundScreen.RemoveFromClassList("ranked-match-found-out");
        rankedMatchFoundStatus.text = "GEGNER GEFUNDEN";
        ShowOnly(rankedMatchFoundScreen);

        yield return null;
        rankedMatchFoundScreen.AddToClassList("ranked-match-found-visible");
        yield return new WaitForSecondsRealtime(0.12f);
        rankedMatchFoundScreen.AddToClassList("ranked-match-found-cards-in");
        yield return new WaitForSecondsRealtime(0.52f);
        rankedMatchFoundScreen.AddToClassList("ranked-match-found-vs-impact");
        yield return new WaitForSecondsRealtime(0.92f);
        rankedMatchFoundStatus.text = "GEFECHT WIRD GELADEN";
        yield return new WaitForSecondsRealtime(0.55f);
        rankedMatchFoundScreen.AddToClassList("ranked-match-found-out");
        yield return new WaitForSecondsRealtime(0.28f);

        if (generation != rankedMatchFoundGeneration || rankedMatchFoundMatchId != matchId) yield break;
        rankedMatchFoundMatchId = null;
        NavalPlayerMatchView latest = onlineMatchView;
        ClearRankedMatchFoundPresentation();
        if (latest != null && latest.matchId == matchId)
            EnterOnlineBattle(latest);
    }

    private void RenderRankedMatchAvatar(
        VisualElement avatar, Label initials, string imageBase64, string displayName, ref Texture2D texture)
    {
        DisposeRankedMatchTexture(ref texture);
        avatar.style.backgroundImage = StyleKeyword.None;
        bool loaded = false;
        if (!string.IsNullOrWhiteSpace(imageBase64))
        {
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                loaded = texture.LoadImage(Convert.FromBase64String(imageBase64), true);
                if (loaded) avatar.style.backgroundImage = new StyleBackground(texture);
            }
            catch (FormatException) { }
        }
        if (!loaded) DisposeRankedMatchTexture(ref texture);
        initials.text = GetInitials(displayName);
        initials.EnableInClassList("hidden", loaded);
    }

    private void ResetRankedMatchFoundSequence()
    {
        rankedMatchFoundGeneration++;
        rankedMatchFoundMatchId = null;
        ClearRankedMatchFoundPresentation();
    }

    private void ClearRankedMatchFoundPresentation()
    {
        if (rankedMatchFoundScreen != null)
        {
            rankedMatchFoundScreen.AddToClassList("hidden");
            rankedMatchFoundScreen.RemoveFromClassList("ranked-match-found-visible");
            rankedMatchFoundScreen.RemoveFromClassList("ranked-match-found-cards-in");
            rankedMatchFoundScreen.RemoveFromClassList("ranked-match-found-vs-impact");
            rankedMatchFoundScreen.RemoveFromClassList("ranked-match-found-out");
        }
        if (rankedMatchOwnAvatar != null) rankedMatchOwnAvatar.style.backgroundImage = StyleKeyword.None;
        if (rankedMatchOpponentAvatar != null) rankedMatchOpponentAvatar.style.backgroundImage = StyleKeyword.None;
        DisposeRankedMatchTexture(ref rankedMatchOwnAvatarTexture);
        DisposeRankedMatchTexture(ref rankedMatchOpponentAvatarTexture);
    }

    private void DisposeRankedMatchFoundUi()
    {
        rankedMatchFoundGeneration++;
        DisposeRankedMatchTexture(ref rankedMatchOwnAvatarTexture);
        DisposeRankedMatchTexture(ref rankedMatchOpponentAvatarTexture);
    }

    private void DisposeRankedMatchTexture(ref Texture2D texture)
    {
        if (texture == null) return;
        Destroy(texture);
        texture = null;
    }
}
