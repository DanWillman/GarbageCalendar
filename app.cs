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
CALSCALE:GREGORIAN
BEGIN:VTIMEZONE
TZID:America/Chicago
X-LIC-LOCATION:America/Chicago
BEGIN:DAYLIGHT
TZOFFSETFROM:-0600
TZOFFSETTO:-0500
TZNAME:CDT
DTSTART:19700308T020000
RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=2SU
END:DAYLIGHT
BEGIN:STANDARD
TZOFFSETFROM:-0500
TZOFFSETTO:-0600
TZNAME:CST
DTSTART:19701101T020000
RRULE:FREQ=YEARLY;BYMONTH=11;BYDAY=1SU
END:STANDARD
END:VTIMEZONE
BEGIN:VEVENT
UID:{uid}
DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}
SUMMARY:{summary}
DTSTART;TZID=America/Chicago:{start:yyyyMMddTHHmmss}
DTEND;TZID=America/Chicago:{end:yyyyMMddTHHmmss}
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
