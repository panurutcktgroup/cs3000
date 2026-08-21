namespace CompanyCLI.Configuration;

public sealed class LinkServerProfile
{
    public string Name { get; set; } = string.Empty; // profile display name (unique)
    public string Host { get; set; } = string.Empty; // IP or hostname
    public int Port { get; set; } = 1433; // optional port
    // (we use the main Settings authentication when connecting)
}