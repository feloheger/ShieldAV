using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using ShieldAV.Models;

namespace ShieldAV.Engine;

public static class HashScanner
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    // ── API Key Storage ─────────────────────────────────────────────
    // Loaded once from AppData\ShieldAV\settings.json
    private static string _virusTotalKey = "";

    public static string VirusTotalApiKey
    {
        get => _virusTotalKey;
        set { _virusTotalKey = value?.Trim() ?? ""; SaveSettings(); }
    }

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ShieldAV", "settings.json");

    public static void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = JObject.Parse(File.ReadAllText(SettingsPath));
            _virusTotalKey = json["VirusTotalApiKey"]?.ToString() ?? "";
        }
        catch { }
    }

    private static void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = new JObject { ["VirusTotalApiKey"] = _virusTotalKey };
            File.WriteAllText(SettingsPath, json.ToString());
        }
        catch { }
    }

    // ── Extensions to scan ──────────────────────────────────────────
    private static readonly HashSet<string> CheckableExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".sys", ".bat", ".cmd", ".vbs", ".ps1",
            ".wsf", ".hta", ".scr", ".pif", ".com", ".msi", ".jar"
        };

    private static readonly HashSet<string> FakableExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp",
            ".doc", ".docx", ".xls", ".xlsx", ".txt", ".mp3", ".mp4", ".zip"
        };

    // ── Hash ────────────────────────────────────────────────────────
    public static string ComputeSHA256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLower();
    }

    // ── MalwareBazaar ───────────────────────────────────────────────
    public static async Task<(bool found, string name)> CheckMalwareBazaar(string hash)
    {
        try
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("query", "get_info"),
                new KeyValuePair<string, string>("hash",  hash)
            ]);
            var resp = await _http.PostAsync("https://mb-api.abuse.ch/api/v1/", content)
                                  .ConfigureAwait(false);
            var obj = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
            var status = obj["query_status"]?.ToString();
            if (status is null or "hash_not_found" or "no_results") return (false, "");
            var name = obj["data"]?[0]?["signature"]?.ToString()
                    ?? obj["data"]?[0]?["tags"]?[0]?.ToString()
                    ?? "Unbekannte Malware";
            return (true, name);
        }
        catch { return (false, ""); }
    }

    // ── VirusTotal ──────────────────────────────────────────────────
    /// <summary>
    /// Queries VirusTotal v3 API. Returns (detected, detectionCount, engineCount, threatName).
    /// Requires a free API key from https://www.virustotal.com
    /// Free tier: 500 requests/day, 4 requests/minute.
    /// </summary>
    public static async Task<(bool detected, int detections, int total, string threatName)>
        CheckVirusTotal(string hash)
    {
        if (string.IsNullOrEmpty(_virusTotalKey))
            return (false, 0, 0, "");

        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://www.virustotal.com/api/v3/files/{hash}");
            req.Headers.Add("x-apikey", _virusTotalKey);

            var resp = await _http.SendAsync(req).ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (false, 0, 0, "");   // hash not in VT = likely clean

            if (!resp.IsSuccessStatusCode)
                return (false, 0, 0, "");   // rate-limit or error → skip

            var json = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
            var stats = json["data"]?["attributes"]?["last_analysis_stats"];

            int malicious  = stats?["malicious"]?.Value<int>()  ?? 0;
            int suspicious = stats?["suspicious"]?.Value<int>() ?? 0;
            int undetected = stats?["undetected"]?.Value<int>() ?? 0;
            int harmless   = stats?["harmless"]?.Value<int>()   ?? 0;
            int total      = malicious + suspicious + undetected + harmless;

            if (malicious == 0 && suspicious == 0)
                return (false, 0, total, "");

            // Get the most common threat name from all engines
            var results   = json["data"]?["attributes"]?["last_analysis_results"];
            var nameCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (results != null)
            {
                foreach (var engine in results.Children<JProperty>())
                {
                    var cat  = engine.Value["category"]?.ToString();
                    var name = engine.Value["result"]?.ToString();
                    if ((cat == "malicious" || cat == "suspicious")
                        && !string.IsNullOrEmpty(name))
                    {
                        nameCount.TryGetValue(name, out int c);
                        nameCount[name] = c + 1;
                    }
                }
            }

            string bestName = nameCount.Count > 0
                ? nameCount.OrderByDescending(x => x.Value).First().Key
                : "Malware";

            return (true, malicious + suspicious, total, bestName);
        }
        catch { return (false, 0, 0, ""); }
    }

    // ── Heuristics ──────────────────────────────────────────────────
    private static bool IsDoubleExtensionMasquerade(string filePath, out string reason)
    {
        reason = "";
        var fname    = Path.GetFileName(filePath);
        var outerExt = Path.GetExtension(fname);
        if (!CheckableExtensions.Contains(outerExt)) return false;
        var innerExt = Path.GetExtension(Path.GetFileNameWithoutExtension(fname));
        if (string.IsNullOrEmpty(innerExt) || !FakableExtensions.Contains(innerExt)) return false;
        reason = $"Doppelte Dateiendung '{innerExt}{outerExt}' – Tarnung als Dokument";
        return true;
    }

    // ── Main scan ───────────────────────────────────────────────────
    public static async Task<ScanResult> ScanFile(string filePath)
    {
        var result = new ScanResult { FilePath = filePath };
        try
        {
            var info = new FileInfo(filePath);
            result.FileSize = info.Length;

            if (info.Length > 200L * 1024 * 1024)
            {
                result.Level  = ThreatLevel.Clean;
                result.Reason = "Übersprungen (zu groß)";
                return result;
            }

            // ── 1. Heuristik (kein Netzwerk) ──────────────────────
            if (IsDoubleExtensionMasquerade(filePath, out var hReason))
            {
                result.Level      = ThreatLevel.Threat;
                result.ThreatName = "Masquerade: Doppelte Dateiendung";
                result.Reason     = hReason;
                result.Hash       = ComputeSHA256(filePath);
                return result;
            }

            var ext = Path.GetExtension(filePath);
            if (!CheckableExtensions.Contains(ext))
            {
                result.Level = ThreatLevel.Clean;
                return result;
            }

            result.Hash = ComputeSHA256(filePath);

            // ── 2. VirusTotal (70+ Engines, wenn API-Key vorhanden) ─
            if (!string.IsNullOrEmpty(_virusTotalKey))
            {
                var (vtDetected, vtCount, vtTotal, vtName) =
                    await CheckVirusTotal(result.Hash).ConfigureAwait(false);

                if (vtDetected)
                {
                    result.Level      = ThreatLevel.Threat;
                    result.ThreatName = vtName;
                    result.Reason     = $"VirusTotal: {vtCount}/{vtTotal} Engines positiv";
                    return result;
                }
            }

            // ── 3. MalwareBazaar (Fallback, immer aktiv) ───────────
            var (mbFound, mbName) = await CheckMalwareBazaar(result.Hash).ConfigureAwait(false);
            if (mbFound)
            {
                result.Level      = ThreatLevel.Threat;
                result.ThreatName = mbName;
                result.Reason     = "MalwareBazaar-Datenbank: bekannte Malware";
                return result;
            }

            result.Level = ThreatLevel.Clean;
        }
        catch (UnauthorizedAccessException) { result.Level = ThreatLevel.Clean; result.Reason = "Kein Zugriff"; }
        catch (IOException)                 { result.Level = ThreatLevel.Clean; result.Reason = "Datei gesperrt"; }
        catch (Exception ex)                { result.Level = ThreatLevel.Clean; result.Reason = $"Fehler: {ex.Message}"; }

        return result;
    }
}
