using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;


/// <summary>
/// Manages the selection of application language through a dropdown menu and updates the localization settings
/// accordingly.
/// </summary>
/// <remarks>This class interacts with the Unity Localization package to allow users to select a language from a
/// predefined list. The selected language is saved in player preferences and applied on subsequent application
/// launches</remarks>
public class LanguageSelect : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    const string PrefKey = "app_locale";

    private readonly (string display, string code)[] languages = new[]
    {
        ("English", "en"),
        ("Français", "fr"),
        ("Kinyarwanda", "rw-RW")
    };

    // Wait for Localization system to initialize before working with locales
    private IEnumerator Start()
    {
        if (dropdown == null)
        {
            Debug.LogWarning("LanguageSelect: TMP_Dropdown is not assigned in inspector.", this);
            yield break;
        }

        // Ensure Localization package finished initializing
        yield return LocalizationSettings.InitializationOperation;

        PopulateDropdown();

        // Load saved locale (fallback to English)
        var saved = PlayerPrefs.GetString(PrefKey, languages[0].code);
        var initialLocale = GetLocaleByCode(saved) ?? GetLocaleByCode(languages[0].code) ?? LocalizationSettings.AvailableLocales.Locales[0];

        LocalizationSettings.SelectedLocale = initialLocale;

        // Reflect selected locale in dropdown without triggering callback
        var index = System.Array.FindIndex(languages, l => l.code == initialLocale.Identifier.Code);
        if (index < 0) index = 0;
        dropdown.SetValueWithoutNotify(index);

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    private void PopulateDropdown()
    {
        dropdown.ClearOptions();
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var lang in languages)
            options.Add(new TMP_Dropdown.OptionData(lang.display));
        dropdown.AddOptions(options);
    }

    private void OnDropdownValueChanged(int index)
    {
        if (index < 0 || index >= languages.Length)
            return;

        var code = languages[index].code;
        var locale = GetLocaleByCode(code);
        if (locale == null)
        {
            Debug.LogWarning($"Locale with code '{code}' not found in Available Locales.", this);
            return;
        }

        LocalizationSettings.SelectedLocale = locale;

        PlayerPrefs.SetString(PrefKey, code);
        PlayerPrefs.Save();
    }

    private Locale GetLocaleByCode(string code)
    {
        foreach (var l in LocalizationSettings.AvailableLocales.Locales)
            if (l.Identifier.Code == code)
                return l;
        return null;
    }
}
