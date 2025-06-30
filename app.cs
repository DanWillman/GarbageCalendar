using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text;
using System.Globalization;

var nextcloudUrl = Environment.GetEnvironmentVariable("NEXTCLOUD_CALDAV_URL")!;
var username = Environment.GetEnvironmentVariable("NEXTCLOUD_USERNAME")!;
var password = Environment.GetEnvironmentVariable("NEXTCLOUD_PASSWORD")!;
var addressUrl = Environment.GetEnvironmentVariable("GARBAGE_URL")!;

using var http = new HttpClient();

// Fetch and log JSON
var json = await http.GetStringAsync(addressUrl);
Console.WriteLine("Collection API Response:");
Console.WriteLine(json);
var doc = JsonNode.Parse(json)!;

// Parse Date
DateTime? ParseDate(string input)
{
    if (DateTime.TryParseExact(input, "dddd MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        return dt;
    return null;
}

// ICS Generator
string IcsEvent(string uid, DateTime start, DateTime end, string summary) => $"""
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Garbage Schedule//
BEGIN:VEVENT
UID:{uid}
DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}
SUMMARY:{summary}
DTSTART;TZID=UTC:{start:yyyyMMddTHHmmssZ}
DTEND;TZID=UTC:{end:yyyyMMddTHHmmssZ}
END:VEVENT
END:VCALENDAR
""";

async Task UploadEvent(JsonNode? node, string label)
{
    if (node is null || node["is_determined"]?.GetValue<bool>() != true) return;

    var rawDate = node["date"]?.GetValue<string>();
    var date = rawDate is null ? null : ParseDate(rawDate);
    if (date is null) return;

    var start = date.Value.AddDays(-1).Date.AddHours(21); // 9:00 PM previous day
    var end = date.Value.Date.AddHours(9);               // 9:00 AM day of

    var uid = $"{label.ToLower()}-{date:yyyyMMdd}@garbage.sync";
    var ics = IcsEvent(uid, start, end, $"{label} Pickup");

    var eventUri = $"{nextcloudUrl.TrimEnd('/')}/{uid}.ics";
    var content = new StringContent(ics, Encoding.UTF8, "text/calendar");

    var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

    var response = await http.PutAsync(eventUri, content);
    Console.WriteLine($"{label} upload: {(int)response.StatusCode} {response.ReasonPhrase}");
}

await UploadEvent(doc["garbage"], "Garbage");
await UploadEvent(doc["recycling"], "Recycling");
