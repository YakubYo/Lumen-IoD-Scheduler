# Lumen-IoD-Scheduler demo application
#### *Schedule Lumen Internet-on-Demand bandwidth changes right from your calendar*

## Synopsis:
This is a sample Windows Console app that reads events from a Microsoft 365 calendar, determines what bandwidth update actions to take, and then calls the Lumen IoD APIs to update the bandwidth at the scheduled time.

The period in which to monitor the Microsoft 365 calendar is configurable (defaults to 6 hours). To monitor a calendar around the clock, you would run a process like this via a cronjob, scheduled task, Windows service, etc. No data is written or stored locally other than output log files.
To view examples of how bandwidth actions are determined (including how conflict resolution works), please see the <a href="https://github.com/YakubYo/Lumen-IoD-Scheduler/blob/main/Lumen-IoD-Scheduler%20overview.pdf">Lumen-IoD-Scheduler overview</a>.

## Setup:
### Dependencies:
Microsoft 365 license for work/school (Personal account can be used for testing)
### Configuration:
1. Microsoft 365 calendar created and permissioned to specified user(s)
2. Registered application in <a href="https://portal.azure.com/">Azure</a> Entra ID to allow calendar access through Microsoft Graph APIs:
    1. Including client secret and Graph API permissions (Calendars.Read and User.Read.All)
    2. To test with a personal Outlook account in Visual Studio or VS Code:
        1. configure the Entra ID app to enable device code flow
        2. Add <https://localhost> as the Redirect URI
        3. Set the tenantId in appsetting.json to "common"
3. Registered application in Lumen Developer Center for IoD
    1. Including client secret

#### *Graph API required permissions:*
![GraphPermissions](https://github.com/user-attachments/assets/4ae3e4fe-d515-4d28-90a6-5915726ff842)
#### *Enabling device code flow (for personal account testing):*
![AllowPublicClientFlows](https://github.com/user-attachments/assets/862ce7b1-b28e-4c1f-9758-91c193566fcd)
#### *Redirected to localhost (for personal account testing):*
![RedirectURI](https://github.com/user-attachments/assets/033f9a2c-0118-4b4e-b53e-378dffeb92ed)

### Microsoft 365 Calendar Id:
One manner of finding your calendar Id value is by using <a href="https://developer.microsoft.com/en-us/graph/graph-explorer">Graph Explorer</a>, such as the via the <a href="https://learn.microsoft.com/en-us/graph/api/user-list-calendars">list calendars</a> call. There are other methods to get this Id that can be found with a quick web search.

### Settings:
Most settings are in the **appsettings.json** file. This includes the ability to change the time interval, set the regular expression pattern to match the calendar event subject line, the location of the output log file, as well as connection information for Graph APIs and Lumen APIs.  

*In addition*, configuration information is in **IoD-Json/bandwidthUpdate.json**, specifically in the "relatedContactInformation" section at the bottom. *You should specify an emailAddress here in order to receive the status notification mail from Lumen once the bandwidth update action has processed.*

## Contact:
For questions or feedback, please contact Jacob Johansen at <jakejoh@hotmail.com>. 

## Resources:
<a href="https://learn.microsoft.com/en-us/graph/use-the-api">Use the Microsoft Graph API - Microsoft Graph | Microsoft Learn</a>

<a href="https://learn.microsoft.com/en-us/graph/auth/">Microsoft Graph authentication and authorization overview | Microsoft Learn</a>

<a href="https://learn.microsoft.com/en-us/graph/auth-register-app-v2">Register an application with the Microsoft identity platform - Microsoft Graph | Microsoft Learn</a>

<a href="https://www.lumen.com/help/en-us/developer-center.html">Developer Center support | Lumen</a>

<a href="https://developer.lumen.com/apis/internet-on-demand#overview">Lumen Developer Center | Internet On-Demand</a>
