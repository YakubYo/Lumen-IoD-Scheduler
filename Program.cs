using Microsoft.Kiota.Abstractions.Extensions;
using Serilog;

using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
// using Microsoft.Identity.Client;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Primitives;

// using Microsoft.AspNetCore.Components.WebAssembly.Authentication;



// immediately grab current time as the beginning of our window
// use a little trick to round down to eliminate seconds, milliseconds, nanoseconds
// as we will be using this value to compare against calendar events later (which
// are no more precise than minutes)
var startWindow = DateTime.Now;
startWindow = startWindow.AddTicks(-(startWindow.Ticks % (60 * 10_000_000)));

// initialize settings and logger
var settings = Settings.LoadSettings();
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(settings.LogLocation ?? "", "Lumen-IoD-Scheduler_.log"),
        rollingInterval: RollingInterval.Day,
        flushToDiskInterval: TimeSpan.FromMinutes(5),
        rollOnFileSizeLimit: true,
       outputTemplate: "{Timestamp:yyyy-MM-ddTHH:mm:ss zzz} {Message:lj}{NewLine}{Exception}"
        )
    .CreateLogger();

try
{
    // TEST: Override the start date to test with a known calendar window
    // startWindow = DateTime.Parse("2024-09-12T14:00:00Z").AddHours(7);
    var endWindow = startWindow.AddMinutes(settings.TimeWindowInMinutes);

    Log.Information($"Starting Lumen-IoD-Scheduler with a monitoring window between {startWindow.ToString()} and {endWindow.ToString()}");

    Log.Information($"Connecting to Graph APIs and retrieving calendar events");
    GraphHelper.InitializeGraph(settings);

    // get events from the calendar
    var eventList = await GraphHelper.GetCalendarEventsAsync(startWindow, endWindow);

    // transform events to an intermediary class (abstract away from Graph API objects for the remainder of our logic)
    var calendarItems = GraphHelper.TransformEventCollection(eventList);

    // In our logic, Location reflects the IoD ServiceId (the unique IoD connection).
    // We want to separate by ServiceId here, as each ServiceId can have it's own unique schedule.
    // As an example, we can have two calendar events at the same time that reflect bandwidth
    // changes on different IoD services. We do not want to treat those as conflicting events.
    // One we process each set of calendar events grouped by service, we will then reassemble into a final
    // list of bandwidth updates we need to make.
    var calendarItemGroups = calendarItems.GroupBy(eventItem => eventItem.Location);
    EventConverter converter = new(settings.SubjectRegEx);
    List<BandwidthAction> allBandwidthChanges = [];

    Log.Information("Building list of bandwidth change actions for each calendar event");
    foreach (var calendarItemGroup in calendarItemGroups)
    {
        allBandwidthChanges.AddRange(
            converter.BuildBandwidthActionsFromEvents
                                        (
                                            calendarItemGroup.AsList(),
                                            startWindow,
                                            endWindow
                                        )
                                    );
    }

    Log.Information($"Queueing up {allBandwidthChanges.Count.ToString()} bandwidth update(s) for this time window");
    List<Thread> threads = [];
    foreach (var action in allBandwidthChanges)
    {
        IoDConnection connection = new IoDConnection(action, settings);
        Thread t = new(new ThreadStart(connection.SetIoDBandwidth));
        threads.Add(t);
        t.Start();

        // add a slight delay before next iteration of loop as otherwise the thread
        // was picking up the next iteration of the action variable when initializing
        Thread.Sleep(1500);
    }

    // wait for each thread to finish
    foreach(var thread in threads)
    {
        thread.Join();
    }

}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception when attempting to update IoD bandwidth settings from the calendar");
}

Log.CloseAndFlush();


// More todo:
// auth via clientkey


// BandwidthAction action = new BandwidthAction()
//     {
//     BandwidthValue = "1000 Mbps",
//     ServiceId = "7786867426",
//     // ServiceId = "7768192647",
//     EventSubject = "test",
//     CalEventId = "eventId val",
//     Time = DateTime.Now.AddMinutes(3),
//     Priority = 1
//     };

// settings.CustomerNumber = "2-BYXZXQ";
// IoDConnection conn = new IoDConnection(action, settings);

// conn.SetIoDBandwidth();
// Console.WriteLine("Pause!!");


// var tenantId = settings.TenantId;

// // The client ID of the app registered in Microsoft Entra ID
// var clientId = settings.ClientId;

// // *Never* include client secrets in source code!
// // var clientSecret = await GetClientSecretFromKeyVault(); // Or some other secure place.

// // var clientSecret = await GetClientSecretFromKeyVault(); // Or some other secure place.
// var clientSecret = "B-_8Q~9.uXIeI0agYPLkZIlZuK8bwPm~mMFwfcqq";

// // Create an instance of a TokenCredential. Since we're using the Client Credentials
// // flow with a client secret, we use ClientSecretCredential.
// var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);

// // The app registration should be configured to require access to permissions
// // sufficient for the Microsoft Graph API calls the app will be making, and
// // those permissions should be granted by a tenant administrator.
// // var scopes = new[] { "https://graph.microsoft.com/.default" };
// var scopes = settings.GraphUserScopes;

// // Build the Microsoft Graph client
// GraphServiceClient graphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);

// // Make a Microsoft Graph API request
// var users = await graphServiceClient.Users.GetAsync();
