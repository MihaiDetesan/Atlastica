using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace Atlastica.Nlp;

/// <summary>
/// Extracts structured fields from raw Wikipedia article text using a small
/// local model (Phi-3.5-mini-instruct, quantized, run in-process via ONNX
/// Runtime GenAI) - no external LLM API calls. The model is loaded lazily and
/// kept resident for the process lifetime since (re)loading it is expensive;
/// callers only hit this on cache misses (see CountryFactsService), so the
/// per-request cost is one CPU-bound generation, not a network round trip.
/// </summary>
public sealed class LocalPhi3ExtractionService : IFactExtractionService, IDisposable
{
    private readonly Phi3ModelOptions _options;
    private readonly ILogger<LocalPhi3ExtractionService> _logger;
    private readonly Lazy<Model> _model;
    private readonly Lazy<Tokenizer> _tokenizer;
    private readonly SemaphoreSlim _generationLock = new(1, 1);

    public LocalPhi3ExtractionService(IOptions<Phi3ModelOptions> options, ILogger<LocalPhi3ExtractionService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _model = new Lazy<Model>(() => new Model(_options.ModelPath));
        _tokenizer = new Lazy<Tokenizer>(() => new Tokenizer(_model.Value));
    }

    public async Task<CountryFactExtractionResult> ExtractCountryFactsAsync(string countryName, string wikipediaExtract, CancellationToken ct = default)
    {
        const string prompt = """
            Extract facts about {0} from the article text below. Respond with
            strict JSON only (no markdown, no commentary), matching exactly:
            {{"governmentForm": string|null, "notableGeographicFeatures": string|null, "biodiversityFacts": string|null}}

            governmentForm: a short (1 sentence) description, or null if not mentioned.

            notableGeographicFeatures: the specific NAME of a mountain, river,
            desert, or similar landmark, but only if it is genuinely famous
            internationally (e.g. "the Amazon river", "the Himalayas", "the
            Sahara desert"). Most countries do NOT have one - if nothing in the
            article is internationally famous by name, use null.
            Do NOT describe the country's borders, coastline, general location,
            or overall terrain/extent (e.g. "borders the Rhine and the
            Atlantic" is wrong) - only a specific named landmark counts.

            biodiversityFacts: only if the article mentions something distinctive
            about {0}'s wildlife or ecosystems; otherwise null.

            Never write placeholder text like "not specified", "none", "n/a", or
            "not well known" - use JSON null instead.

            Article text:
            {1}
            """;

        var json = await GenerateAsync(string.Format(prompt, countryName, wikipediaExtract), ct);
        return ParseCountryFacts(json);
    }

    public async Task<PoiDescriptionResult> ExtractPoiDescriptionAsync(string poiName, string wikipediaExtract, CancellationToken ct = default)
    {
        const string prompt = """
            Summarize {0} from the article text below in 1-2 sentences,
            suitable for a map popup. Respond with strict JSON only (no
            markdown, no commentary), matching exactly:
            {{"shortDescription": string}}

            Article text:
            {1}
            """;

        var json = await GenerateAsync(string.Format(prompt, poiName, wikipediaExtract), ct);
        return ParsePoiDescription(json, poiName);
    }

    private async Task<string> GenerateAsync(string userPrompt, CancellationToken ct)
    {
        await _generationLock.WaitAsync(ct);
        try
        {
            return await Task.Run(() => Generate(userPrompt), ct);
        }
        finally
        {
            _generationLock.Release();
        }
    }

    private string Generate(string userPrompt)
    {
        var fullPrompt =
            "<|system|>\nYou are a precise information-extraction assistant. Always respond with strict JSON only.<|end|>\n" +
            $"<|user|>\n{userPrompt}<|end|>\n<|assistant|>\n";

        var tokenizer = _tokenizer.Value;
        using var tokenizerStream = tokenizer.CreateStream();
        var sequences = tokenizer.Encode(fullPrompt);

        using var generatorParams = new GeneratorParams(_model.Value);
        generatorParams.SetSearchOption("max_length", sequences[0].Length + _options.MaxNewTokens);
        generatorParams.SetSearchOption("do_sample", false);

        using var generator = new Generator(_model.Value, generatorParams);
        generator.AppendTokenSequences(sequences);

        var sb = new StringBuilder();
        while (!generator.IsDone())
        {
            generator.GenerateNextToken();
            var lastToken = generator.GetSequence(0)[^1];
            sb.Append(tokenizerStream.Decode(lastToken));
        }

        return sb.ToString();
    }

    private CountryFactExtractionResult ParseCountryFacts(string modelOutput)
    {
        var json = ExtractJsonObject(modelOutput);
        if (json is null)
        {
            _logger.LogWarning("Model output did not contain valid JSON: {Output}", Truncate(modelOutput));
            return new CountryFactExtractionResult(null, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new CountryFactExtractionResult(
                GetNullableString(root, "governmentForm"),
                GetNullableString(root, "notableGeographicFeatures"),
                GetNullableString(root, "biodiversityFacts"));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse model JSON output: {Json}", Truncate(json));
            return new CountryFactExtractionResult(null, null, null);
        }
    }

    private PoiDescriptionResult ParsePoiDescription(string modelOutput, string poiName)
    {
        var json = ExtractJsonObject(modelOutput);
        if (json is null)
        {
            _logger.LogWarning("Model output did not contain valid JSON: {Output}", Truncate(modelOutput));
            return new PoiDescriptionResult(string.Empty);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var description = GetNullableString(doc.RootElement, "shortDescription") ?? string.Empty;
            return new PoiDescriptionResult(description);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse model JSON output for {Poi}: {Json}", poiName, Truncate(json));
            return new PoiDescriptionResult(string.Empty);
        }
    }

    // Small models don't always follow "use JSON null" instructions reliably -
    // treat common no-info placeholder strings the same as an actual null.
    private static readonly HashSet<string> PlaceholderValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "not specified", "not mentioned", "none", "n/a", "na", "unknown",
        "not applicable", "not well known", "not well-known", "no notable features",
        "no notable geographic features", "not available", "",
    };

    private static string? GetNullableString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        var str = value.GetString()?.Trim();
        return str is null || PlaceholderValues.Contains(str) ? null : str;
    }

    /// <summary>Small models often wrap JSON in markdown fences or add stray text - take the outermost {...}.</summary>
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    private static string Truncate(string s) => s.Length > 300 ? s[..300] + "..." : s;

    public void Dispose()
    {
        _generationLock.Dispose();
        if (_tokenizer.IsValueCreated) _tokenizer.Value.Dispose();
        if (_model.IsValueCreated) _model.Value.Dispose();
    }
}
