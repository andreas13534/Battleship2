using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private const string DeveloperFreeAbilitiesPreference = "naval.admin.free-abilities";
    private const string DeveloperForceJetHitPreference = "naval.admin.force-jet-hit";

    private VisualElement developerAdminPanel;
    private Toggle developerFreeAbilitiesToggle;
    private Toggle developerForceJetHitToggle;
    private bool developerFreeAbilitiesEnabled;
    private bool developerForceJetHitEnabled;

    private void CacheDeveloperAdminUi(VisualElement root)
    {
        developerAdminPanel = root.Q<VisualElement>("DeveloperAdminPanel");
        developerFreeAbilitiesToggle = root.Q<Toggle>("DeveloperFreeAbilitiesToggle");
        developerForceJetHitToggle = root.Q<Toggle>("DeveloperForceJetHitToggle");

        developerFreeAbilitiesEnabled = PlayerPrefs.GetInt(DeveloperFreeAbilitiesPreference, 0) == 1;
        developerForceJetHitEnabled = PlayerPrefs.GetInt(DeveloperForceJetHitPreference, 0) == 1;
        developerFreeAbilitiesToggle?.SetValueWithoutNotify(developerFreeAbilitiesEnabled);
        developerForceJetHitToggle?.SetValueWithoutNotify(developerForceJetHitEnabled);
        RefreshDeveloperAdminPanel();
    }

    private void BindDeveloperAdminUi()
    {
        developerFreeAbilitiesToggle?.RegisterValueChangedCallback(evt =>
        {
            developerFreeAbilitiesEnabled = evt.newValue;
            PlayerPrefs.SetInt(DeveloperFreeAbilitiesPreference, evt.newValue ? 1 : 0);
            PlayerPrefs.Save();
            UpdateAbilityButtons();
        });

        developerForceJetHitToggle?.RegisterValueChangedCallback(evt =>
        {
            developerForceJetHitEnabled = evt.newValue;
            PlayerPrefs.SetInt(DeveloperForceJetHitPreference, evt.newValue ? 1 : 0);
            PlayerPrefs.Save();
        });
    }

    private void RefreshDeveloperAdminPanel()
    {
        if (developerAdminPanel == null) return;
        bool visible = IsDeveloperAbilityAccount();
        developerAdminPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        developerAdminPanel.EnableInClassList("hidden", !visible);
    }

    private bool DeveloperFreeAbilitiesActive()
    {
        return developerFreeAbilitiesEnabled && IsDeveloperAbilityAccount();
    }

    private bool DeveloperForceJetHitActive()
    {
        return developerForceJetHitEnabled && IsDeveloperAbilityAccount();
    }
}
