using System.Security.Cryptography;
using System.Text.Json;
using CompanyCLI.Configuration;

namespace CompanyCLI.Services;

public sealed class LinkServerProfileStore
{
    private const string ApplicationFolderName = "CompanyCLI";
    private const string SettingsFileName = "link-profiles.dat";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationFolderName,
        SettingsFileName);

    public List<LinkServerProfile> LoadAll()
    {
        if (!File.Exists(FilePath))
            return new List<LinkServerProfile>();

        try
        {
            EnsureWindows();
            var protectedBytes = File.ReadAllBytes(FilePath);
            var jsonBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            var profiles = JsonSerializer.Deserialize<List<LinkServerProfile>>(jsonBytes, JsonOptions);
            return profiles ?? new List<LinkServerProfile>();
        }
        catch
        {
            // on any error return empty list (caller can show warning)
            return new List<LinkServerProfile>();
        }
    }

    public void SaveAll(List<LinkServerProfile> profiles)
    {
        EnsureWindows();
        var directory = Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("Could not determine the settings directory.");
        Directory.CreateDirectory(directory);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(profiles, JsonOptions);
        var protectedBytes = ProtectedData.Protect(jsonBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        var temporaryFilePath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryFilePath, protectedBytes);
            File.Move(temporaryFilePath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFilePath)) File.Delete(temporaryFilePath);
        }
    }

    public void AddOrUpdate(LinkServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var list = LoadAll();
        var existing = list.FindIndex(x => string.Equals(x.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) list[existing] = profile; else list.Add(profile);
        SaveAll(list);
    }

    public void Remove(string name)
    {
        var list = LoadAll();
        var removed = list.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) SaveAll(list);
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Encrypted profile storage requires Windows DPAPI.");
        }
    }
}