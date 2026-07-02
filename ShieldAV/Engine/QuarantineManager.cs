using System.Text.Json;
using ShieldAV.Models;

namespace ShieldAV.Engine;

public class QuarantineManager
{
    private readonly string _quarantineDir;
    private readonly string _dbPath;
    private List<QuarantineEntry> _entries = [];

    public QuarantineManager()
    {
        _quarantineDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShieldAV", "Quarantine");
        _dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShieldAV", "quarantine.json");
        Directory.CreateDirectory(_quarantineDir);
        Load();
    }

    public IReadOnlyList<QuarantineEntry> Entries => _entries.AsReadOnly();

    public bool Quarantine(ScanResult result)
    {
        try
        {
            var qName = $"{Guid.NewGuid():N}.qav";
            var qPath = Path.Combine(_quarantineDir, qName);

            // XOR-obfuscate so the file can't be executed accidentally
            var bytes = File.ReadAllBytes(result.FilePath);
            for (int i = 0; i < bytes.Length; i++) bytes[i] ^= 0xAA;
            File.WriteAllBytes(qPath, bytes);
            File.Delete(result.FilePath);

            var entry = new QuarantineEntry
            {
                OriginalPath = result.FilePath,
                QuarantinePath = qPath,
                ThreatName = result.ThreatName,
                Hash = result.Hash,
                QuarantinedAt = DateTime.Now
            };
            _entries.Add(entry);
            Save();
            return true;
        }
        catch { return false; }
    }

    public bool Restore(QuarantineEntry entry)
    {
        try
        {
            var bytes = File.ReadAllBytes(entry.QuarantinePath);
            for (int i = 0; i < bytes.Length; i++) bytes[i] ^= 0xAA;
            Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
            File.WriteAllBytes(entry.OriginalPath, bytes);
            File.Delete(entry.QuarantinePath);
            _entries.Remove(entry);
            Save();
            return true;
        }
        catch { return false; }
    }

    public bool Delete(QuarantineEntry entry)
    {
        try
        {
            File.Delete(entry.QuarantinePath);
            _entries.Remove(entry);
            Save();
            return true;
        }
        catch { return false; }
    }

    private void Save()
    {
        File.WriteAllText(_dbPath, JsonSerializer.Serialize(_entries));
    }

    private void Load()
    {
        if (!File.Exists(_dbPath)) return;
        try { _entries = JsonSerializer.Deserialize<List<QuarantineEntry>>(File.ReadAllText(_dbPath)) ?? []; }
        catch { _entries = []; }
    }
}
