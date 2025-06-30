// app.cs
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Globalization;

// Config
var nextcloudUrl = Environment.GetEnvironmentVariable("NEXTCLOUD_CALDAV_URL")!;
var username = Environment.GetEnvironmentVariable("NEXTCLOUD_USERNAME")!;
var password = Environment.GetEnvironmentVariable("NEXTCLOUD_PASSWORD")!;
var calendarName = "garbage";
var addressUrl = Environment.GetEnvironmentVariable("GARBAGE_URL");

// Fetch JSON
using var http = new HttpClient();
var json = await http.GetStringAsync(addressUrl);
var doc = JsonNode.Parse(json)!;

string? ParseDate(string input) =>
    DateTime.TryParseExact(input, "dddd MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
        ? dt.ToString("yyyyMMdd")
        : null;

string IcsEvent(string uid, string date, string summary) => $"""
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Garbage Schedule//
BEGIN:VEVENT
UID:{uid}
DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}
SUMMARY:{summary}
DTSTART;VALUE=DATE:{date}
DTEND;VALUE=DATE:{date}
END:VEVENT
END:VCALENDAR
""";

// Garbage
var garbage = doc["garbage"];
var recycling = doc["recycling"];

async Task UploadEvent(JsonNode? node, string label)
{
    if (node is null || node["is_determined"]?.GetValue<bool>() != true) return;

    var rawDate = node["date"]?.GetValue<string>();
    var parsedDate = rawDate is null ? null : ParseDate(rawDate);
    if (parsedDate is null) return;

    var uid = $"{label.ToLower()}-{parsedDate}@garbage.sync";
    var ics = IcsEvent(uid, parsedDate, $"{label} Pickup");

    var eventUri = $"{nextcloudUrl.TrimEnd('/')}/{uid}.ics";
    var content = new StringContent(ics, Encoding.UTF8, "text/calendar");
    var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

    var response = await http.PutAsync(eventUri, content);
    Console.WriteLine($"{label} upload: {(int)response.StatusCode} {response.ReasonPhrase}");
}

await UploadEvent(garbage, "Garbage");
await UploadEvent(recycling, "Recycling");
