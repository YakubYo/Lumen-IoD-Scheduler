// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

class GraphHelper
{
    // Settings object
    private static Settings? _settings;
    // User auth token credential
    private static DeviceCodeCredential? _deviceCodeCredential;
    // Client configured with user authentication
    private static GraphServiceClient? _userClient;

    public static void InitializeGraph(Settings settings)
    {
        _settings = settings;

        if(settings.UserAuth)
        {
            InitializeGraphForUserAuth((info, cancel) =>
                {
                    // Display the device code message to the user
                    // This tells them where to go to sign in and provides the code to use
                    Console.WriteLine(info.Message);
                    return Task.FromResult(0);
                });
        }
        else
        {
            InitializeGraphWithClientSecret();
        }
    }

    /// <summary>
    /// Authenticate with a device code / user prompt. Works for Personal accounts and is good for testing
    /// </summary>
    /// <param name="deviceCodePrompt">Device code callback</param>
    private static void InitializeGraphForUserAuth(Func<DeviceCodeInfo, CancellationToken, Task> deviceCodePrompt)
    {
        _ = _settings ??
            throw new ArgumentNullException("Settings value cannot be null");

        var options = new DeviceCodeCredentialOptions
        {
            ClientId = _settings.ClientId,
            TenantId = _settings.TenantId,
            DeviceCodeCallback = deviceCodePrompt,
        };

        _deviceCodeCredential = new DeviceCodeCredential(options);

        _userClient = new GraphServiceClient(_deviceCodeCredential, _settings.GraphUserScopes);
    }

    /// <summary>
    /// Authenticate with a client secret. Works for work/school account for users with a valid license and cofigured Entra Id app
    /// </summary>
    /// <exception cref="ArgumentNullException">Settings cannot be null</exception>
    private static void InitializeGraphWithClientSecret()
    {
        _ = _settings ??
            throw new ArgumentNullException("Settings value cannot be null");

        var options = new TokenCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var clientSecretCredential = new ClientSecretCredential(_settings.TenantId, _settings.ClientId, _settings.EntraIdSecret, options);

        // The app registration should be configured to require access to permissions
        // sufficient for the Microsoft Graph API calls the app will be making, and
        // those permissions should be granted by a tenant administrator.
        var scopes = new[] { "https://graph.microsoft.com/.default" };

        _userClient = new GraphServiceClient(clientSecretCredential, scopes);
    }




/// <summary>
/// Retrieve the list of Graph API events from a specific calendar between a specific time range
/// </summary>
/// <param name="startWindow">Start time</param>
/// <param name="endWindow">End time</param>
/// <returns>Collection of events</returns>
/// <exception cref="System.NullReferenceException">Graph API must be initialized</exception>
/// <exception cref="ArgumentNullException">Calendar value must be set</exception>
    public static Task<EventCollectionResponse?> GetCalendarEventsAsync(DateTime startWindow, DateTime endWindow)
    {
        _ = _userClient ??
            throw new System.NullReferenceException("Graph has not been initialized for user auth");

        _ = _settings ??
            throw new ArgumentNullException("Settings value cannot be null");

        return _userClient.Users[_settings.UserAccount].Calendars[_settings.CalendarId].CalendarView.GetAsync((config) =>
            {
                config.QueryParameters.Select = new[] { "subject", "start", "end", "location", "categories", "icaluid" };
                config.QueryParameters.StartDateTime = startWindow.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
                config.QueryParameters.EndDateTime = endWindow.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
                config.Headers.Add("Prefer", $"outlook.timezone=\"{TimeZoneInfo.Local.StandardName}\"");
            });
    }

    /// <summary>
    /// Transform Graph API event to intermediary class
    /// </summary>
    /// <param name="e">Graph API event</param>
    /// <returns></returns>
    public static CalendarItem Transform(Event e)
    {
    #pragma warning disable CS8602 // Dereference of a possibly null reference.
        return new CalendarItem() {
            Id = e.ICalUId ?? "No subject",
            Start = e.Start.DateTime ?? "No start time",
            End = e.End.DateTime ?? "No end time",
            Location = e.Location.DisplayName ?? "No location",
            Subject = e.Subject ?? "No subject",
            Count = e.Categories.Count
        };
    #pragma warning restore CS8602 // Dereference of a possibly null reference.
    }

    /// <summary>
    /// Transform Graph API events to intermediary class
    /// </summary>
    /// <param name="events">A collection of Graph API events</param>
    /// <returns>List of calendar items</returns>
    public static List<CalendarItem> TransformEventCollection(EventCollectionResponse? events)
    {
        List<CalendarItem> calItems = [];
        if (null == events || null == events.Value)
        {
            return calItems;
        }

        events.Value.ForEach(delegate(Event e)
        {
            calItems.Add(Transform(e));
        });

        // must sort the list by start time in order for our logic to work later on in BuildBandwidthActionsFromEvents
        return calItems.OrderBy(item => DateTime.Parse(item.Start)).ToList();
    }

}
