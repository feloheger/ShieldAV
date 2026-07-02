namespace ShieldAV.Models;

public enum ThreatLevel { Clean, Suspicious, Threat }

public class ScanResult
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public string Hash { get; set; } = "";
    public ThreatLevel Level { get; set; } = ThreatLevel.Clean;
    public string ThreatName { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.Now;
    public string Reason { get; set; } = "";
}

public class QuarantineEntry
{
    public string OriginalPath { get; set; } = "";
    public string QuarantinePath { get; set; } = "";
    public string ThreatName { get; set; } = "";
    public DateTime QuarantinedAt { get; set; } = DateTime.Now;
    public string Hash { get; set; } = "";
}

public class ScanStats
{
    public int TotalFiles { get; set; }
    // Backing fields for Interlocked operations
    public int ScannedFilesRaw;
    public int ThreatsFoundRaw;
    public int SuspiciousFilesRaw;
    public int ScannedFiles => ScannedFilesRaw;
    public int ThreatsFound => ThreatsFoundRaw;
    public int SuspiciousFiles => SuspiciousFilesRaw;
    public int Quarantined { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
}
