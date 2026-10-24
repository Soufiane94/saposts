using System.ComponentModel.DataAnnotations;

namespace SaPosts.Web.Data;

/// <summary>Paths are relative to the content root unless absolute (in Docker: /data and /media).</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Holds the SQLite database and the Data Protection keys.</summary>
    [Required]
    public string DataPath { get; set; } = "App_Data";

    /// <summary>Uploaded media: one folder per item, plus a staging folder for uploads in progress.</summary>
    [Required]
    public string MediaPath { get; set; } = "App_Data/media";

    public string DatabaseFile => Path.Combine(DataPath, "saposts.db");

    public string KeysDirectory => Path.Combine(DataPath, "keys");
}
