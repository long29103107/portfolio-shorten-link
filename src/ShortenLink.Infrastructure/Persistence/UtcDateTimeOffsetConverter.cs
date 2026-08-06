using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShortenLink.Infrastructure.Persistence;

internal sealed class UtcDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset, DateTime>(
        value => value.UtcDateTime,
        value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));
