using System;
using Unity.Services.LevelPlay;
using UnityEngine;

public sealed class NavalRewardedAdService : IDisposable
{
    public const string AndroidAppKey = "27be23ebd";
    public const string AndroidRewardedAdUnitId = "ha35o9hutwffhnws";

    private LevelPlayRewardedAd rewardedAd;
    private string playerId;
    private bool initializationStarted;
    private bool rewardHandledForCurrentShow;

    public event Action Changed;
    public event Action RewardEarned;

    public bool IsReady => rewardedAd != null && rewardedAd.IsAdReady();
    public bool CanRequest => rewardedAd != null;
    public string StatusMessage { get; private set; } = "WERBUNG NICHT INITIALISIERT";

    public void Initialize(string authenticatedPlayerId)
    {
        if (!string.IsNullOrWhiteSpace(authenticatedPlayerId)) playerId = authenticatedPlayerId.Trim();
        if (initializationStarted) return;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            SetStatus("FÜR WERBUNG ONLINE ANMELDEN");
            return;
        }

        initializationStarted = true;
        SetStatus("WERBUNG WIRD INITIALISIERT...");
        LevelPlay.OnInitSuccess += HandleInitSuccess;
        LevelPlay.OnInitFailed += HandleInitFailed;
        LevelPlay.Init(AndroidAppKey, playerId);
    }

    public void Show(string authenticatedPlayerId)
    {
        if (!string.IsNullOrWhiteSpace(authenticatedPlayerId)) playerId = authenticatedPlayerId.Trim();
        if (rewardedAd == null || !rewardedAd.IsAdReady())
        {
            SetStatus("WERBUNG WIRD GELADEN...");
            rewardedAd?.LoadAd();
            return;
        }

        rewardHandledForCurrentShow = false;
        LevelPlay.SetDynamicUserId(playerId);
        rewardedAd.ShowAd();
    }

    public void Dispose()
    {
        LevelPlay.OnInitSuccess -= HandleInitSuccess;
        LevelPlay.OnInitFailed -= HandleInitFailed;
        if (rewardedAd == null) return;
        rewardedAd.OnAdLoaded -= HandleAdLoaded;
        rewardedAd.OnAdLoadFailed -= HandleAdLoadFailed;
        rewardedAd.OnAdDisplayed -= HandleAdDisplayed;
        rewardedAd.OnAdDisplayFailed -= HandleAdDisplayFailed;
        rewardedAd.OnAdRewarded -= HandleAdRewarded;
        rewardedAd.OnAdClosed -= HandleAdClosed;
    }

    private void HandleInitSuccess(LevelPlayConfiguration configuration)
    {
        rewardedAd = new LevelPlayRewardedAd(AndroidRewardedAdUnitId);
        rewardedAd.OnAdLoaded += HandleAdLoaded;
        rewardedAd.OnAdLoadFailed += HandleAdLoadFailed;
        rewardedAd.OnAdDisplayed += HandleAdDisplayed;
        rewardedAd.OnAdDisplayFailed += HandleAdDisplayFailed;
        rewardedAd.OnAdRewarded += HandleAdRewarded;
        rewardedAd.OnAdClosed += HandleAdClosed;
        SetStatus("WERBUNG WIRD GELADEN...");
        rewardedAd.LoadAd();
    }

    private void HandleInitFailed(LevelPlayInitError error)
    {
        SetStatus("WERBUNG NICHT VERFÜGBAR: " + error.ErrorMessage);
    }

    private void HandleAdLoaded(LevelPlayAdInfo adInfo)
    {
        SetStatus("WERBUNG BEREIT");
    }

    private void HandleAdLoadFailed(LevelPlayAdError error)
    {
        SetStatus("WERBUNG KONNTE NICHT GELADEN WERDEN: " + error.ErrorMessage);
    }

    private void HandleAdDisplayed(LevelPlayAdInfo adInfo)
    {
        SetStatus("WERBUNG LÄUFT...");
    }

    private void HandleAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        SetStatus("WERBUNG KONNTE NICHT GESTARTET WERDEN: " + error.ErrorMessage);
        rewardedAd.LoadAd();
    }

    private void HandleAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        if (rewardHandledForCurrentShow) return;
        rewardHandledForCurrentShow = true;
        SetStatus("BELOHNUNG WIRD FREIGESCHALTET...");
        RewardEarned?.Invoke();
    }

    private void HandleAdClosed(LevelPlayAdInfo adInfo)
    {
        // LevelPlay can deliver OnAdRewarded shortly after OnAdClosed.
        // Do not report a failed view while that reward callback is still pending.
        if (!rewardHandledForCurrentShow) SetStatus("WERBUNG WIRD GEPRÜFT...");
        rewardedAd.LoadAd();
    }

    private void SetStatus(string message)
    {
        StatusMessage = message;
        Debug.Log("[LevelPlay] " + message);
        Changed?.Invoke();
    }
}
