using System.Security.Cryptography;
using System.Text.Json;
using CompanyCLI.Configuration;

namespace CompanyCLI.Services;

public sealed class ServerConnectionSettingsStore
{
    private const string ApplicationFolderName = "CompanyCLI";
    private const string SettingsFileName = "connection-settings.dat";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationFolderName,
        SettingsFileName);

    public ServerConnectionSettings Load(out string? errorMessage)
    {
        errorMessage = null;

        if (!File.Exists(FilePath))
        {
            return new ServerConnectionSettings();
        }

        try
        {
            EnsureWindows();

            var protectedBytes = File.ReadAllBytes(FilePath);
            var jsonBytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            return JsonSerializer.Deserialize<ServerConnectionSettings>(jsonBytes, JsonOptions)
                ?? new ServerConnectionSettings();
        }
        catch (CryptographicException)
        {
            errorMessage = "Saved connection settings could not be decrypted for this Windows user.";
        }
        catch (JsonException)
        {
            errorMessage = "Saved connection settings are invalid and could not be loaded.";
        }
        catch (IOException)
        {
            errorMessage = "Saved connection settings could not be read.";
        }

        return new ServerConnectionSettings();
    }

    public void Save(ServerConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        EnsureWindows();

        var directory = Path.GetDirectoryName(FilePath)
            ?? throw new InvalidOperationException("Could not determine the settings directory.");

        Directory.CreateDirectory(directory);

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        var protectedBytes = ProtectedData.Protect(
            jsonBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        var temporaryFilePath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryFilePath, protectedBytes);
            File.Move(temporaryFilePath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    public void Clear()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Encrypted connection settings require Windows DPAPI.");
        }
    }
}
