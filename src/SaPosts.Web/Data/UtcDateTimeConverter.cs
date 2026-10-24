using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SaPosts.Web.Data;

/// <summary>Stores local times as UTC and marks values read from the database as UTC.</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
