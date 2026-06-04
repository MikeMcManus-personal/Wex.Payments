using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wex.Payments.Api.Serialization;

/// <summary>
/// Serializes monetary <see cref="decimal"/> values with a fixed two-decimal scale
/// (e.g. 100 -> 100.00) so money fields render consistently in responses. Apply only
/// to money amounts, never to exchange rates, which carry variable precision.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.ToString("F2", CultureInfo.InvariantCulture));
}
