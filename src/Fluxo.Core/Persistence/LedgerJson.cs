using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fluxo.Core.Persistence;

/// <summary>
/// Serialização gerada em tempo de compilação. No Blazor WebAssembly a publicação corta código
/// não usado (trimming), e serialização por reflexão quebra em silêncio nesse cenário.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(LedgerState))]
internal sealed partial class LedgerJsonContext : JsonSerializerContext;

public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetInt64());
    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Cents);
}

public static class LedgerJson
{
    private static readonly LedgerJsonContext Context = new(new JsonSerializerOptions
    {
        Converters = { new MoneyJsonConverter() },
    });

    public static string Serialize(LedgerState state) => JsonSerializer.Serialize(state, Context.LedgerState);

    /// <summary>Texto ilegível ou vazio volta como estado novo, nunca derruba o app.</summary>
    public static LedgerState Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new LedgerState();
        try
        {
            return JsonSerializer.Deserialize(json, Context.LedgerState) ?? new LedgerState();
        }
        catch (JsonException)
        {
            return new LedgerState();
        }
    }
}
