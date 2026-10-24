using System.ComponentModel.DataAnnotations;

namespace SaPosts.Web.Data;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Holds the SQLite database and the Data Protection keys. Relative to the content root unless absolute.</summary>
    [Required]
    public string DataPath { get; set; } = "App_Data";

    public string DatabaseFile => Path.Combine(DataPath, "saposts.db");

    public string KeysDirectory => Path.Combine(DataPath, "keys");
}
