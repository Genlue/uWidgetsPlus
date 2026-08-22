using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Services;
using uWidgets.Views;

namespace uWidgets.Services;

public class UpdateService
{
    private readonly IAppSettingsProvider appSettingsProvider;

    public UpdateService(IAppSettingsProvider appSettingsProvider)
    {
        this.appSettingsProvider = appSettingsProvider;
        TimerService.Timer1Day.Subscribe(CheckForUpdates);
    }

    public void CheckForUpdates()
    {
        _ = CheckForUpdatesAsync();
    }
    
    public async Task CheckForUpdatesAsync()
    {
        var version = await GetUpdateVersionAsync();
        if (version == null) return;

        new UpdatePopup(version, this).Show();
    }
    
    public async Task<Version?> GetUpdateVersionAsync()
    {
        // Custom update source: null/empty disables update checks entirely
        // (the personal fork must not report upstream releases as updates).
        var updateUrl = appSettingsProvider.Get().UpdateUrl;
        if (string.IsNullOrWhiteSpace(updateUrl)) return null;

        try
        {
            using var httpClient = ProxySettings.CreateHttpClient();
            var response = await httpClient.GetAsync(updateUrl);

            if (!response.IsSuccessStatusCode) return null;

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var latestVersionText = response.RequestMessage?.RequestUri?.ToString().Split("/").Last().Replace("v", "") ?? "";

            if (!Version.TryParse(latestVersionText, out var latestVersion)) return null;

            var ignoreVersionText = appSettingsProvider.Get().IgnoreUpdate;

            if (ignoreVersionText != null && Version.TryParse(ignoreVersionText, out var ignoreVersion) &&
                latestVersion <= ignoreVersion)
                return null;

            if (latestVersion <= currentVersion) 
                return null;

            return latestVersion;
        }
        catch (Exception)
        {
            // Network/proxy failures must never crash the app — update check is best-effort.
            return null;
        }
    }

    public void SkipVersion(Version version)
    {
        var settings = appSettingsProvider.Get() with { IgnoreUpdate = version.ToString() };
        appSettingsProvider.Save(settings);
    }
}
