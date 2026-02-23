namespace Channels.Api.Domain;

public sealed class QueryParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string? Value { get; set; }

    public object SqlValue
    {
        get
        {
            if (Value is null)
            {
                return DBNull.Value;
            }

            return Type.ToLowerInvariant() switch
            {
                "int" or "int32" when int.TryParse(Value, out var intValue) => intValue,
                "long" or "int64" when long.TryParse(Value, out var longValue) => longValue,
                "decimal" when decimal.TryParse(Value, out var decimalValue) => decimalValue,
                "double" when double.TryParse(Value, out var doubleValue) => doubleValue,
                "bool" or "boolean" when bool.TryParse(Value, out var boolValue) => boolValue,
                "datetime" or "datetimeoffset" when DateTimeOffset.TryParse(Value, out var dateValue) => dateValue,
                _ => Value
            };
        }
    }
}

