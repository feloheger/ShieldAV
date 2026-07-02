using ShieldAV.Models;

namespace ShieldAV.Engine;

public class ScanEngine
{
    private CancellationTokenSource _cts = new();
    public bool IsScanning { get; private set; }

    public event Action<ScanResult>? FileScanned;
    public event Action<int, int>?   ProgressChanged;
    public event Action<ScanStats>?  ScanCompleted;
    public event Action<string>?     StatusChanged;

    public static List<string> GetFilesInFolder(string folder)
    {
        var files = new List<string>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(folder, "*",
                new EnumerationOptions
                {
                    IgnoreInaccessible    = true,
                    RecurseSubdirectories = true,
                    MaxRecursionDepth     = 12
                }))
                files.Add(f);
        }
        catch { }
        return files;
    }

    public async Task StartScan(List<string> folders, ScanStats stats)
    {
        if (IsScanning) return;
        IsScanning = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            StatusChanged?.Invoke("Dateien werden aufgelistet…");

            var allFiles = await Task.Run(() =>
            {
                var list = new List<string>();
                foreach (var f in folders)
                    list.AddRange(GetFilesInFolder(f));
                return list;
            }, token);

            stats.TotalFiles = allFiles.Count;
            StatusChanged?.Invoke($"{allFiles.Count:N0} Dateien gefunden – Scan läuft…");

            // Only 2 concurrent API calls — prevents rate-limit AND UI flooding
            using var sem = new SemaphoreSlim(2, 2);

            // Progress throttle: only fire UI update every 50 files
            int updateEvery = Math.Max(1, allFiles.Count / 200);

            var tasks = allFiles.Select(async file =>
            {
                if (token.IsCancellationRequested) return;
                await sem.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (token.IsCancellationRequested) return;

                    var result = await HashScanner.ScanFile(file).ConfigureAwait(false);

                    Interlocked.Increment(ref stats.ScannedFilesRaw);
                    if (result.Level == ThreatLevel.Threat)
                        Interlocked.Increment(ref stats.ThreatsFoundRaw);
                    if (result.Level == ThreatLevel.Suspicious)
                        Interlocked.Increment(ref stats.SuspiciousFilesRaw);

                    // Only report threats + throttled progress (no per-file status spam)
                    if (result.Level != ThreatLevel.Clean)
                        FileScanned?.Invoke(result);

                    if (stats.ScannedFilesRaw % updateEvery == 0)
                        ProgressChanged?.Invoke(stats.ScannedFiles, stats.TotalFiles);
                }
                catch (OperationCanceledException) { }
                catch { /* per-file errors must never kill the scan */ }
                finally { sem.Release(); }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Fehler: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            stats.EndTime = DateTime.Now;
            // Only fire completed if NOT cancelled
            if (!_cts.IsCancellationRequested)
                ScanCompleted?.Invoke(stats);
        }
    }

    public void Cancel() => _cts.Cancel();
}
