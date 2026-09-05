using System;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private const int ProfileAvatarSize = 256;
    private const int MaximumAvatarBase64Characters = 180000;

    private VisualElement profileAvatarImage;
    private VisualElement profileAccountMenu;
    private ScrollView profileMinimalScroll;
    private Label profileAvatarInitials;
    private Label profileDisplayNameLabel;
    private Label profileIdentityCodeLabel;
    private Label profileWinsValue;
    private Label profileLossesValue;
    private Label profileRankValue;
    private Label profileJoinedValue;
    private Button profileAvatarButton;
    private Button profileAccountButton;
    private Button profileAccountCancelButton;
    private Texture2D profileAvatarTexture;
    private string renderedAvatarBase64;
    private bool avatarRendered;
    private Button copyFriendCodeButton;

    private void CacheProfilePresentationUi(VisualElement root)
    {
        profileAvatarImage = root.Q<VisualElement>("ProfileAvatarImage");
        profileAccountMenu = root.Q<VisualElement>("ProfileAccountMenu");
        profileMinimalScroll = root.Q<ScrollView>(className: "profile-minimal-scroll");
        profileMinimalScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        profileAvatarInitials = root.Q<Label>("ProfileAvatarInitials");
        profileDisplayNameLabel = root.Q<Label>("ProfileDisplayNameLabel");
        profileIdentityCodeLabel = root.Q<Label>("ProfileIdentityCodeLabel");
        profileWinsValue = root.Q<Label>("ProfileWinsValue");
        profileLossesValue = root.Q<Label>("ProfileLossesValue");
        profileRankValue = root.Q<Label>("ProfileRankValue");
        profileJoinedValue = root.Q<Label>("ProfileJoinedValue");
        profileAvatarButton = root.Q<Button>("ProfileAvatarButton");
        profileAccountButton = root.Q<Button>("ProfileAccountButton");
        profileAccountCancelButton = root.Q<Button>("ProfileAccountCancelButton");
        copyFriendCodeButton = root.Q<Button>("CopyFriendCodeButton");
    }

    private void BindProfilePresentationUi()
    {
        profileAvatarButton.clicked += RequestProfileImage;
        profileAccountButton.clicked += OpenProfileAccountMenu;
        profileAccountCancelButton.clicked += CloseProfileAccountMenu;
        copyFriendCodeButton.clicked += () =>
        {
            string code = onlineService?.Profile?.friendCode;
            if (string.IsNullOrWhiteSpace(code)) return;
            GUIUtility.systemCopyBuffer = code;
            copyFriendCodeButton.text = "KOPIERT";
        };
        profileAccountMenu.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == profileAccountMenu)
                CloseProfileAccountMenu();
        });
    }

    private void RenderProfilePresentation(NavalPlayerProfile profile, bool signedIn)
    {
        if (profileDisplayNameLabel == null) return;
        string displayName = profile?.displayName ?? "COMMANDER";
        profileDisplayNameLabel.text = displayName;
        string playerName = UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsSignedIn
                ? AuthenticationService.Instance.PlayerName
                : string.Empty;
        profileIdentityCodeLabel.text = string.IsNullOrWhiteSpace(playerName)
            ? "CODE " + (profile?.friendCode ?? "--------")
            : playerName + "\nCODE " + (profile?.friendCode ?? "--------");
        copyFriendCodeButton.SetEnabled(signedIn && !string.IsNullOrWhiteSpace(profile?.friendCode));
        copyFriendCodeButton.text = "FREUNDESCODE KOPIEREN";
        profileWinsValue.text = (profile?.lifetimeWins ?? 0).ToString();
        profileLossesValue.text = (profile?.lifetimeLosses ?? 0).ToString();
        profileRankValue.text = (profile?.league ?? "PLATZIERUNG") + "\n" +
            (profile?.mmr ?? NavalRankRules.InitialMmr) + " RP";
        profileJoinedValue.text = FormatJoinedDate(profile?.joinedUnixMs ?? 0);
        profileAvatarButton.text = string.IsNullOrWhiteSpace(profile?.avatarImageBase64)
            ? "PROFILBILD HINZUFÜGEN"
            : "PROFILBILD ÄNDERN";
        profileAvatarButton.SetEnabled(signedIn);
        profileAccountButton.EnableInClassList("hidden", !signedIn);
        RenderProfileAvatar(profile?.avatarImageBase64, displayName);
    }

    private static string FormatJoinedDate(long unixMs)
    {
        if (unixMs <= 0) return "--.--.----";
        try { return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime().ToString("dd.MM.yyyy"); }
        catch (ArgumentOutOfRangeException) { return "--.--.----"; }
    }

    private void RenderProfileAvatar(string imageBase64, string displayName)
    {
        profileAvatarInitials.text = GetInitials(displayName);
        if (avatarRendered && renderedAvatarBase64 == imageBase64) return;
        avatarRendered = true;
        renderedAvatarBase64 = imageBase64;
        DisposeProfileAvatarTexture();
        profileAvatarImage.style.backgroundImage = StyleKeyword.None;
        bool loaded = false;
        if (!string.IsNullOrWhiteSpace(imageBase64))
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(imageBase64);
                profileAvatarTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                loaded = profileAvatarTexture.LoadImage(bytes, true);
                if (loaded)
                    profileAvatarImage.style.backgroundImage = new StyleBackground(profileAvatarTexture);
            }
            catch (FormatException) { }
        }

        if (!loaded) DisposeProfileAvatarTexture();
        profileAvatarInitials.text = GetInitials(displayName);
        profileAvatarInitials.EnableInClassList("hidden", loaded);
    }

    private static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "NC";
        string[] parts = displayName.Trim().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
            return char.ToUpperInvariant(parts[0][0]).ToString() + char.ToUpperInvariant(parts[parts.Length - 1][0]);
        string compact = parts.Length == 0 ? "NC" : parts[0];
        return compact.Substring(0, Math.Min(2, compact.Length)).ToUpperInvariant();
    }

    private void OpenProfileAccountMenu()
    {
        deleteAccountArmedUntilUnixMs = 0;
        deleteAccountButton.text = "KONTO LÖSCHEN";
        profileNameField.value = onlineService?.Profile?.displayName ?? string.Empty;
        profileMessageLabel.text = string.Empty;
        profileAccountMenu.RemoveFromClassList("hidden");
    }

    private void CloseProfileAccountMenu()
    {
        profileAccountMenu?.AddToClassList("hidden");
        deleteAccountArmedUntilUnixMs = 0;
        if (deleteAccountButton != null) deleteAccountButton.text = "KONTO LÖSCHEN";
    }

    private void RequestProfileImage()
    {
        if (onlineService?.IsSignedIn != true) return;
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Profilbild auswählen", string.Empty, "png,jpg,jpeg");
        if (!string.IsNullOrWhiteSpace(path)) ProcessProfileImage(File.ReadAllBytes(path));
#elif UNITY_ANDROID
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaClass picker = new AndroidJavaClass("com.navalcommand.profile.NavalImagePicker"))
            picker.CallStatic("open", activity, gameObject.name);
#else
        profileMessageLabel.text = "BILDAUSWAHL AUF DIESER PLATTFORM NICHT VERFÜGBAR";
#endif
    }

    public void OnProfileImagePicked(string imageBase64)
    {
        try { ProcessProfileImage(Convert.FromBase64String(imageBase64)); }
        catch (Exception exception) { OnProfileImagePickerError(exception.Message); }
    }

    public void OnProfileImagePickerError(string message)
    {
        if (profileMessageLabel != null)
            profileMessageLabel.text = "PROFILBILD KONNTE NICHT GELADEN WERDEN";
    }

    private void ProcessProfileImage(byte[] sourceBytes)
    {
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(sourceBytes, false))
        {
            Destroy(source);
            OnProfileImagePickerError(string.Empty);
            return;
        }

        Texture2D square = CreateSquareAvatar(source);
        Destroy(source);
        byte[] encoded = square.EncodeToJPG(78);
        string base64 = Convert.ToBase64String(encoded);
        if (base64.Length > MaximumAvatarBase64Characters)
        {
            encoded = square.EncodeToJPG(62);
            base64 = Convert.ToBase64String(encoded);
        }
        Destroy(square);
        if (base64.Length > MaximumAvatarBase64Characters)
        {
            profileMessageLabel.text = "PROFILBILD IST ZU GROSS";
            return;
        }
        _ = SaveProfileAvatarAsync(base64);
    }

    private static Texture2D CreateSquareAvatar(Texture2D source)
    {
        RenderTexture target = RenderTexture.GetTemporary(ProfileAvatarSize, ProfileAvatarSize, 0, RenderTextureFormat.ARGB32);
        float sourceAspect = source.width / (float)source.height;
        Vector2 scale = sourceAspect > 1f ? new Vector2(1f / sourceAspect, 1f) : new Vector2(1f, sourceAspect);
        Vector2 offset = (Vector2.one - scale) * 0.5f;
        Graphics.Blit(source, target, scale, offset);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D result = new Texture2D(ProfileAvatarSize, ProfileAvatarSize, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, ProfileAvatarSize, ProfileAvatarSize), 0, 0);
        result.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        return result;
    }

    private async Task SaveProfileAvatarAsync(string base64)
    {
        profileAvatarButton.SetEnabled(false);
        profileMessageLabel.text = "PROFILBILD WIRD GESPEICHERT...";
        try
        {
            NavalPlayerProfile profile = await onlineService.UpdateAvatarAsync(base64);
            profileMessageLabel.text = "PROFILBILD AKTUALISIERT";
            RenderProfilePresentation(profile, true);
        }
        catch (Exception exception)
        {
            profileMessageLabel.text = exception.Message.ToUpperInvariant();
        }
        finally
        {
            profileAvatarButton.SetEnabled(onlineService?.IsSignedIn == true);
        }
    }

    private void DisposeProfilePresentationUi() => DisposeProfileAvatarTexture();

    private void DisposeProfileAvatarTexture()
    {
        if (profileAvatarTexture == null) return;
        Destroy(profileAvatarTexture);
        profileAvatarTexture = null;
    }
}
