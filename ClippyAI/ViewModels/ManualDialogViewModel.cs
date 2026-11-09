using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ClippyAI.Services;
using ClippyAI.Views;

namespace ClippyAI.ViewModels;

/// <summary>
/// Background information about the application a manual is being written
/// for. Profiles are persisted as keys in the shared Configuration table so
/// repeated manual runs for the same app need no re-typing.
/// </summary>
public partial class ManualDialogViewModel : ViewModelBase
{
    /// <summary>Best-effort guess from the window context of the first screenshot.</summary>
    [ObservableProperty]
    private string _appName = "";

    [ObservableProperty]
    private string _appVersion = "";

    [ObservableProperty]
    private string _appNotes = "";

    [ObservableProperty]
    private string _audience = "End users";

    [ObservableProperty]
    private string _language = ConfigurationService.GetConfigurationValue("DefaultLanguage", "English");

    [ObservableProperty]
    private bool _saveProfile = true;

    [ObservableProperty]
    private bool _saveAsFile = true;

    [ObservableProperty]
    private string _errorMessage = "";

    public ObservableCollection<string> LanguageItems { get; } = ["English", "Deutsch", "Français", "Español"];

    [ObservableProperty]
    private string? _selectedProfile;

    public ObservableCollection<string> ProfileNames { get; } = [];

    public ManualDialogViewModel(string? suggestedContext)
    {
        foreach (string name in LoadProfileNames())
        {
            ProfileNames.Add(name);
        }

        if (!string.IsNullOrWhiteSpace(suggestedContext))
        {
            AppName = SuggestedNameFromContext(suggestedContext);
        }
    }

    partial void OnSelectedProfileChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        (string version, string notes, string audience) = LoadProfile(value);
        AppName = value;
        AppVersion = version;
        AppNotes = notes;
        Audience = audience;
    }

    public void PersistProfileIfRequested()
    {
        if (!SaveProfile || string.IsNullOrWhiteSpace(AppName))
        {
            return;
        }

        SaveProfileRecord(AppName.Trim(), AppVersion.Trim(), AppNotes.Trim(), Audience.Trim());
    }

    // ---------------- profile storage (Configuration KV table) ----------------

    private const string ListKey = "ManualProfileList";
    private const string Prefix = "ManualProfile::";

    private static string[] LoadProfileNames()
    {
        string raw = ConfigurationService.GetConfigurationValue(ListKey, "");
        return raw.Split(";;", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static (string Version, string Notes, string Audience) LoadProfile(string name)
    {
        string raw = ConfigurationService.GetConfigurationValue(Prefix + name, "");
        string[] parts = raw.Split('|');
        return (parts.Length > 0 ? Unescape(parts[0]) : "",
                parts.Length > 1 ? Unescape(parts[1]) : "",
                parts.Length > 2 ? Unescape(parts[2]) : "");
    }

    private static void SaveProfileRecord(string name, string version, string notes, string audience)
    {
        ConfigurationService.SetConfigurationValue(Prefix + name,
            string.Join('|', Escape(version), Escape(notes), Escape(audience)));

        string[] names = LoadProfileNames()
            .Concat([name])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ConfigurationService.SetConfigurationValue(ListKey, string.Join(";;", names));
    }

    private static string Escape(string value) => value.Replace("|", "/");

    private static string Unescape(string value) => value;

    /// <summary>
    /// "Paint — window 'Bild - Paint' — process mspaint.exe" style context to a
    /// clean app name candidate.
    /// </summary>
    private static string SuggestedNameFromContext(string context)
    {
        int dash = context.IndexOf(" — ", StringComparison.Ordinal);
        string first = dash > 0 ? context.Substring(0, dash) : context;
        return first.Trim();
    }
}
