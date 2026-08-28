using System.Text.Json;
using Atlastica.Domain;
using Atlastica.Infrastructure;
using Atlastica.Infrastructure.ExternalFacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAtlasticaInfrastructure(builder.Configuration);
builder.Services.AddAtlasticaFactsExtraction(builder.Configuration);
using var host = builder.Build();

var command = args.FirstOrDefault();
var defaultOutputPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "country-facts.json");
var outputPath = GetArgValue(args, "--file") ?? defaultOutputPath;

var onlyCodes = GetArgValue(args, "--only")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();

switch (command)
{
    case "extract":
        await ExtractAsync(host.Services, outputPath, onlyCodes);
        break;
    case "import":
        await ImportAsync(host.Services, outputPath);
        break;
    default:
        Console.WriteLine("""
            Usage:
              dotnet run -- extract [--file path/to/country-facts.json]
              dotnet run -- import  [--file path/to/country-facts.json]

            extract: fetches Wikidata + Wikipedia + runs local NLP extraction for every
                     country and writes the results to a JSON file (resumable - re-running
                     skips countries already present in the file).
            import:  upserts that JSON file's contents into Postgres.
            """);
        return 1;
}

return 0;

static string? GetArgValue(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static async Task ExtractAsync(IServiceProvider services, string outputPath, HashSet<string>? onlyCodes)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AtlasticaDbContext>();
    var computer = scope.ServiceProvider.GetRequiredService<CountryFactsComputer>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var countries = await db.Countries.AsNoTracking()
        .Select(c => new { c.IsoCode, c.Name })
        .OrderBy(c => c.IsoCode)
        .ToListAsync();
    if (onlyCodes is { Count: > 0 })
    {
        countries = countries.Where(c => onlyCodes.Contains(c.IsoCode)).ToList();
    }

    var results = LoadExisting(outputPath);
    logger.LogInformation("Extracting facts for {Total} countries ({AlreadyDone} already in {Path})", countries.Count, results.Count, outputPath);

    var i = 0;
    foreach (var country in countries)
    {
        i++;
        if (results.ContainsKey(country.IsoCode))
        {
            logger.LogInformation("[{I}/{Total}] {Iso} - already extracted, skipping", i, countries.Count, country.IsoCode);
            continue;
        }

        var started = DateTimeOffset.UtcNow;
        try
        {
            var facts = await computer.ComputeAsync(country.IsoCode, country.Name);
            results[country.IsoCode] = facts;
            Save(outputPath, results);
            logger.LogInformation("[{I}/{Total}] {Iso} done in {Elapsed}s", i, countries.Count, country.IsoCode, (DateTimeOffset.UtcNow - started).TotalSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{I}/{Total}] {Iso} failed - leaving unset, re-run extract to retry", i, countries.Count, country.IsoCode);
        }
    }

    logger.LogInformation("Extraction complete: {Count} countries written to {Path}", results.Count, outputPath);
}

static async Task ImportAsync(IServiceProvider services, string inputPath)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AtlasticaDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var results = LoadExisting(inputPath);
    logger.LogInformation("Importing {Count} countries' facts from {Path}", results.Count, inputPath);

    foreach (var facts in results.Values)
    {
        var existing = await db.CountryFacts.FirstOrDefaultAsync(f => f.IsoCode == facts.IsoCode);
        if (existing is null)
        {
            existing = new CountryFacts { IsoCode = facts.IsoCode, FetchedAt = facts.FetchedAtUtc };
            db.CountryFacts.Add(existing);
        }

        existing.Population = facts.Population;
        existing.Capital = facts.Capital;
        existing.GovernmentForm = facts.GovernmentForm;
        existing.OfficialLanguages = facts.OfficialLanguages;
        existing.AreaKm2 = facts.AreaKm2;
        existing.Currency = facts.Currency;
        existing.NotableGeographicFeatures = facts.NotableGeographicFeatures;
        existing.BiodiversityFacts = facts.BiodiversityFacts;
        existing.FetchedAt = facts.FetchedAtUtc;
    }

    await db.SaveChangesAsync();
    logger.LogInformation("Import complete");
}

static Dictionary<string, ComputedCountryFacts> LoadExisting(string path)
{
    if (!File.Exists(path)) return [];
    var json = File.ReadAllText(path);
    var list = JsonSerializer.Deserialize<List<ComputedCountryFacts>>(json) ?? [];
    return list.ToDictionary(f => f.IsoCode);
}

static void Save(string path, Dictionary<string, ComputedCountryFacts> results)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var json = JsonSerializer.Serialize(results.Values.OrderBy(f => f.IsoCode), new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(path, json);
}
