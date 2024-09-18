// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;

public class Settings
{
    public required string ClientId { get; set; }
    public required string TenantId { get; set; }
    
    /// <summary>
    /// Entra Id app secret works with work/school account for users with valid M365 licenses
    /// </summary>
    public string? EntraIdSecret { get; set; }
    /// <summary>
    /// If true, use user auth / console sign-in (for testing and Personal accounts)
    /// </summary>
    public required bool UserAuth { get; set; }
    /// <summary>
    /// Graph scope values used in user / console sign-in 
    /// </summary>
    public string[]? GraphUserScopes { get; set; }

    public required string UserAccount { get; set; }
    public required string CalendarId { get; set; }
    public required double TimeWindowInMinutes { get; set; }
    public required string SubjectRegEx { get; set; }
    public required string IoDUrl { get; set; }
    public required string IoDSecret { get; set; }
    public required string CustomerNumber { get; set; }
    public required string LogLocation { get; set; }
    public required int WaitForUpdatesInMinutes { get; set; }


    public static Settings LoadSettings()
    {
        // Load settings
        IConfiguration config = new ConfigurationBuilder()
            // appsettings.json is required
            .AddJsonFile("appsettings.json", optional: false)
            // appsettings.Development.json" is optional, values override appsettings.json
            .AddJsonFile($"appsettings.Development.json", optional: true)
            // User secrets are optional, values override both JSON files
            .AddUserSecrets<Program>()
            .Build();

        return config.GetRequiredSection("Settings").Get<Settings>() ??
            throw new Exception("Could not load app settings");
    }
}
