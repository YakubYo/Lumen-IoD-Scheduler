using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Serilog;
using System.Data;
using System.Text.Json.Nodes;

class IoDConnection

{
    private BandwidthAction _action;
    private Settings _settings;
    private string _identifier;
    private static string? _token;
    private static readonly string _inventoryUrl = "{0}/ProductInventory/v1/inventory?serviceId={1}";
    private static readonly string _quoteUrl = "{0}/Product/v1/priceRequest";
    private static readonly string _orderUrl = "{0}/Customer/v3/Ordering/orderRequest";
    private static readonly string _authUrl = "{0}/oauth/v2/token";
    private static readonly string _quoteJsonFile = @"IoD-Json\createQuote.json";
    private static readonly string _orderJsonFile = @"IoD-Json\bandwidthUpdate.json";

    public IoDConnection(BandwidthAction action, Settings settings)
    {
        _action = action;
        _settings = settings;
        _identifier = $"[Service Id = {_action.ServiceId}, Start time: {_action.Time.ToString()}]";
    }
 
    private bool GetRequest(string path, out string responseString)
    {
        return PostRequest(path, null, out responseString);
    }

    private bool PostRequest(string path, string? body, out string responseString)
    {
        var client = new HttpClient();
        HttpRequestMessage request;

        // choose Get or Post if there is a body message
        if (String.IsNullOrEmpty(body))
        {
            request = new HttpRequestMessage(HttpMethod.Get, path);
        }
        else
        {
            request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(body, null, "application/json")
            };
        }

        request.Headers.Add("x-customer-number", _settings.CustomerNumber);
        request.Headers.Add("Authorization", $"Bearer {_token}");
        var response = client.Send(request);
        responseString = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
        return response.IsSuccessStatusCode;
    }

    private bool GetToken()
    {
        Log.Information($"{_identifier} Getting token");

        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, String.Format(_authUrl, _settings.IoDUrl));
        request.Headers.Add("Authorization", $"Basic {_settings.IoDSecret}");
        var collection = new List<KeyValuePair<string, string>> { new("grant_type", "client_credentials") };
        request.Content = new FormUrlEncodedContent(collection);
        var response = client.Send(request);
        var responseString = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();

        if (response.IsSuccessStatusCode)
        {
            _token = JsonDocument.Parse(responseString).RootElement.GetProperty("access_token").GetString();
            return true;
        }
        else
        {
            Log.Error($"{_identifier} Failed to get token! {responseString}");
            return false;
        }
    }

    /// <summary>
    /// For the bandwidth action, now execute the Lumen IoD API code to make the change at the correct time
    /// </summary>
    public void SetIoDBandwidth()
    {
        // this method will be called in separate threads, so for logging provide some unique identifying information
        Log.Information($"{_identifier} Starting");

        // determine the wait time before calling the IoD APIs
        TimeSpan waitTime = _action.Time.Subtract(DateTime.Now).Duration();
        Log.Information($"{_identifier} Waiting {waitTime.TotalMinutes.ToString()} minutes until start time");

        // everything we have been building for is finally here!
        Thread.Sleep(waitTime);

        if (!GetToken()) return;

        if (!GetInventory()) return;

        var quoteId = CreateQuote();
        if (String.IsNullOrWhiteSpace(quoteId)) return;

        if(!UpdateOrder(quoteId)) return;

        CheckBandwidthUpdate();

        Log.Information($"{_identifier} Finished");
    }

    private bool GetInventory()
    {
        Log.Information($"{_identifier} Getting service inventory details");

        bool found = GetRequest(String.Format(_inventoryUrl, _settings.IoDUrl, _action.ServiceId), out string responseString);
        if (!found)
        {
            Log.Error($"{_identifier} Failed to get service inventory details! {responseString}");
            return false;
        }

        JsonElement element = JsonDocument.Parse(responseString).RootElement.GetProperty("serviceInventory")[0];

        _action.Status = element.GetProperty("product").GetProperty("status").GetString();
        _action.AccountNumber = element.GetProperty("billingAccount").GetProperty("id").GetString();
        _action.AccountName = element.GetProperty("billingAccount").GetProperty("name").GetString();
        _action.MasterSiteId = element.GetProperty("location").GetProperty("masterSiteid").GetString();

        if (element.GetProperty("locationProfile").GetProperty("dataCenter").GetBoolean())
        {
            _action.PartnerId = element.GetProperty("locationProfile").GetProperty("relatedParty").GetProperty("id").GetString();
        }

        return true;
    }

    private string? CreateQuote()
    {
        Log.Information($"{_identifier} Attempting to create quote");

        string body;

        using (var reader = new StreamReader(_quoteJsonFile))
        {
            body = reader.ReadToEnd();
            body = body.Replace("{Customer Number}", _settings.CustomerNumber);
            body = body.Replace("{MasterSiteId}", _action.MasterSiteId);
            body = body.Replace("{Bandwidth}", _action.Bandwidth);

            // this is a real hack. In the case of a Data Center connection, we need to insert a new element to the Json for PartnerId
            if (!string.IsNullOrWhiteSpace(_action.PartnerId))
            {
                body = body.Replace("\"NaaS ExternalApi\",", $"\"NaaS ExternalApi\",\n    \"PartnerId\": \"{_action.PartnerId}\",");
            }
        }                

        if(PostRequest(String.Format(_quoteUrl, _settings.IoDUrl), body, out string responseString))
        {
            return JsonDocument.Parse(responseString).RootElement.GetProperty("id").GetString();
        }
        else
        {
            Log.Error($"{_identifier} Failed to create quote! {responseString}");
            return null;
        }
    }


    private bool UpdateOrder(string quoteId)
    {
        Log.Information($"{_identifier} Attempting to update order");

        string body;

        using (var reader = new StreamReader(_orderJsonFile))
        {
            // create unique name for ExternalId. Max 20 characters
            string externalId = string.Concat(_action.ServiceId, _action.Time.ToString("yyMMdd"), new System.Random().Next(0, 9999).ToString());

            body = reader.ReadToEnd().
                            Replace("{QuoteId}", quoteId).
                            Replace("{ServiceId}", _action.ServiceId).
                            Replace("{AccountName}", _action.AccountName).
                            Replace("{AccountNumber}", _action.AccountNumber).
                            Replace("{CalendarItemId}", _action.CalendarItemId).
                            Replace("{ExternalId}", externalId);
        }

        if(!PostRequest(String.Format(_orderUrl, _settings.IoDUrl), body, out string responseString))
        {
            Log.Error($"{_identifier} Update order call failed! {responseString}");
            return false;
        }

        return true;
    }

    private void CheckBandwidthUpdate()
    {
        Log.Information($"{_identifier} Waiting {_settings.WaitForUpdatesInMinutes} minute(s) for bandwidth update change to be applied.");
        Thread.Sleep(TimeSpan.FromMinutes(_settings.WaitForUpdatesInMinutes));
        bool found = GetRequest(String.Format(_inventoryUrl, _settings.IoDUrl, _action.ServiceId), out string responseString);
        if (!found)
        {
            Log.Error($"{_identifier} Unable to find the service after making the order update request: {responseString}");
            return;
        }

        JsonElement element = JsonDocument.Parse(responseString).RootElement.GetProperty("serviceInventory")[0];

        var status = element.GetProperty("product").GetProperty("status").GetString();
        var bandwidth = "";

        if (0 == string.Compare(status, "Active", StringComparison.InvariantCultureIgnoreCase))
        {
            foreach (var subElement in element.GetProperty("product").GetProperty("productCharacteristic").EnumerateArray())
            {
                if (0 == string.Compare(subElement.GetProperty("name").GetString(), "Bandwidth"))
                {
                    bandwidth = subElement.GetProperty("value").ToString();
                    if (0 == string.Compare(bandwidth, _action.Bandwidth, StringComparison.InvariantCultureIgnoreCase))
                    {
                        Log.Information($"{_identifier} Bandwidth successfully updated!");
                    }
                    else
                    {
                        Log.Error($"Failed to update the bandwidth. Current value is {bandwidth} when expected value is {_action.Bandwidth}");
                        return;
                    }

                    break;
                }
            }
        }
        else if (0 == string.Compare(status, "Change pending", StringComparison.InvariantCultureIgnoreCase))
        {
            Log.Information($"{_identifier} Order update is still underway after {_settings.WaitForUpdatesInMinutes} minute(s)");
        }
    }

}