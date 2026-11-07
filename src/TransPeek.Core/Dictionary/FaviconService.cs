// Fetches and caches website favicons for dictionary links (SPEC §3.2).
// Mirror of the mac app's FaviconService: lookup order per host is
// <host>/favicon.ico, Google's s2 favicon service, then DuckDuckGo's icon
// service. Results are cached in memory and on disk under
// %LOCALAPPDATA%\TransPeek\Favicons\<host>.png. Failures are swallowed and
// reported as null — a missing favicon must never break the panel.

using System.Text.Json;

namespace TransPeek.Core.Dictionary;

public sealed class FaviconService
{
    public static FaviconService Shared { get; } = new();

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly Dictionary<string, byte[]> _memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private FaviconService() { }

    /// <summary>Disk cache directory. Public so Settings can show / clear it.</summary>
    public static string FaviconsDirectory =>
        Path.Combine(AppPaths.LocalDataDirectory, "Favicons");

    /// <summary>Returns the cached favicon bytes for a host, if loaded.
    /// Never triggers a fetch — use <see cref="GetImageAsync"/> for that.</summary>
    public byte[]? GetCached(string host)
    {
        lock (_gate)
            return _memoryCache.TryGetValue(host, out var bytes) ? bytes : null;
    }

    /// <summary>Returns the favicon bytes for a link's host, fetching it
    /// (disk, then network) on first use. Never throws; returns null when
    /// no favicon is available.</summary>
    public async Task<byte[]?> GetImageAsync(DictionaryLink link, CancellationToken cancellationToken = default)
    {
        var host = link.Host;
        if (host is null)
            return null;
        lock (_gate)
        {
            if (_memoryCache.TryGetValue(host, out var cached))
                return cached;
            if (!_inFlight.Add(host))
                return null;
        }
        try
        {
            var data = await FetchFaviconDataAsync(host, cancellationToken).ConfigureAwait(false);
            if (data is { Length: > 0 })
            {
                lock (_gate) _memoryCache[host] = data;
                return data;
            }
            return null;
        }
        finally
        {
            lock (_gate) _inFlight.Remove(host);
        }
    }

    /// <summary>Candidate favicon URLs for a host, in lookup order.
    /// Kept public for tests.</summary>
    public static IReadOnlyList<Uri> CandidateUrls(string host) =>
    [
        new Uri($"https://{host}/favicon.ico"),
        new Uri($"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(host)}&sz=32"),
        new Uri($"https://icons.duckduckgo.com/ip3/{host}.ico"),
    ];

    private async Task<byte[]?> FetchFaviconDataAsync(string host, CancellationToken cancellationToken)
    {
        if (LoadFromDisk(host) is { Length: > 0 } diskData)
            return diskData;

        foreach (var url in CandidateUrls(host))
        {
            byte[] data;
            try
            {
                using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    continue;
                data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }
            if (data.Length == 0 || !LooksLikeImage(data))
                continue;
            SaveToDisk(data, host);
            return data;
        }
        return null;
    }

    /// <summary>Cheap magic-byte check (PNG / ICO / GIF / JPEG / BMP / WebP)
    /// so error pages served with a 200 are not cached as icons.</summary>
    internal static bool LooksLikeImage(byte[] data)
    {
        if (data.Length < 4) return false;
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return true; // PNG
        if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00) return true; // ICO
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return true; // GIF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return true; // JPEG
        if (data[0] == 0x42 && data[1] == 0x4D) return true; // BMP
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50) return true; // WebP
        return false;
    }

    private static string DiskPath(string host)
    {
        var safe = new string(host.ToLowerInvariant().Select(c =>
            char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_').ToArray());
        return Path.Combine(FaviconsDirectory, safe + ".png");
    }

    private static byte[]? LoadFromDisk(string host)
    {
        try { return File.Exists(DiskPath(host)) ? File.ReadAllBytes(DiskPath(host)) : null; }
        catch { return null; }
    }

    private static void SaveToDisk(byte[] data, string host)
    {
        try
        {
            Directory.CreateDirectory(FaviconsDirectory);
            File.WriteAllBytes(DiskPath(host), data);
        }
        catch
        {
            // Cache failures are non-fatal.
        }
    }
}

/// <summary>Well-known local data locations. %LOCALAPPDATA%\TransPeek is
/// the Windows equivalent of macOS ~/Library/Application Support/TransPeek.</summary>
public static class AppPaths
{
    public static string LocalDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TransPeek");

    public static string SettingsFilePath => Path.Combine(LocalDataDirectory, "settings.json");

    public static string TessdataDirectory => Path.Combine(LocalDataDirectory, "tessdata");

    /// <summary>Stable per-machine id for telemetry-free diagnostics only.</summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(LocalDataDirectory);
    }
}
