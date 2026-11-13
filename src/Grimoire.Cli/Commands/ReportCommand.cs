using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ReportCommand(CliEnvironment environment)
{
    private const string DefaultTag = "daily-notes";

    private static readonly JsonSerializerOptions JsonOutput = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var day = invocation.Value("date") ?? LocalToday(services);
        var window = DayWindow.Resolve(invocation.Spec, day);
        var tags = EffectiveTags(invocation);
        var project = invocation.Value("project");
        var identity = await ActorResolver.ResolveAsync(
            environment, null, null,
            services.GetRequiredService<MemoryEngineOptions>().DefaultTome,
            services.GetRequiredService<IActorClaimProvider>(),
            services.GetService<IAgentIdentitySource>(), cancellationToken);

        var records = await services.GetRequiredService<MemoryService>().ListAsync(new MemoryFilter
        {
            Tome = identity.Tome,
            CreatedAfter = window.Start,
            CreatedBefore = window.End,
            Tags = tags,
            ProjectId = project,
            Status = MemoryStatus.Active,
        }, cancellationToken);

        var groups = GroupByActor(records);
        var tome = identity.Tome;

        if (invocation.Value("json") is not null)
        {
            WriteJson(window, tags, project, tome, records.Count, groups);
            return ExitCodes.Ok;
        }

        WriteMarkdown(window, tags, project, tome, records.Count, groups);
        return ExitCodes.Ok;
    }

    private static IReadOnlyList<string> EffectiveTags(CliInvocation invocation)
    {
        var tags = CliValues.Tags(invocation.Value("tag"));
        return tags.Count > 0 ? tags : [DefaultTag];
    }

    private static string LocalToday(IServiceProvider services)
    {
        var now = services.GetRequiredService<IClock>().UtcNow.LocalDateTime;
        return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<ActorGroup> GroupByActor(IReadOnlyList<MemoryRecord> records) =>
        records
            .GroupBy(record => record.Actor)
            .OrderBy(group => group.Key.Alias, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Vendor ?? string.Empty, StringComparer.Ordinal)
            .Select(group => new ActorGroup(
                group.Key,
                [.. group.OrderByDescending(record => record.CreatedAt).ThenBy(record => record.Id)]))
            .ToList();

    private void WriteMarkdown(
        DayWindow window,
        IReadOnlyList<string> tags,
        string? project,
        string tome,
        int total,
        IReadOnlyList<ActorGroup> groups)
    {
        if (total == 0)
        {
            environment.Output.WriteLine(
                $"No daily notes in {window.Day} (window {window.Range}, tags {Join(tags)}" +
                $"{ProjectSuffix(project)}, tome {tome}).");
            return;
        }

        environment.Output.WriteLine($"Daily notes {window.Day}");
        environment.Output.WriteLine($"Window: {window.Range}");
        environment.Output.WriteLine($"Tags: {Join(tags)}");
        environment.Output.WriteLine($"Tome: {tome}");

        if (project is not null)
        {
            environment.Output.WriteLine($"Project: {project}");
        }

        environment.Output.WriteLine($"Records: {total}");

        foreach (var group in groups)
        {
            environment.Output.WriteLine();
            environment.Output.WriteLine($"## {group.Actor} ({CountWords(group.Records.Count)})");
            foreach (var record in group.Records)
            {
                environment.Output.WriteLine();
                MemoryFormatter.WriteRecord(environment.Output, record);
            }
        }
    }

    private void WriteJson(
        DayWindow window,
        IReadOnlyList<string> tags,
        string? project,
        string tome,
        int total,
        IReadOnlyList<ActorGroup> groups)
    {
        var payload = new
        {
            date = window.Day,
            window = new { @from = Iso(window.Start), to = Iso(window.End) },
            tags,
            tome,
            project,
            total,
            actors = groups.Select(group => new
            {
                actor = group.Actor.ToString(),
                alias = group.Actor.Alias,
                vendor = group.Actor.Vendor,
                count = group.Records.Count,
                records = group.Records.Select(record => new
                {
                    id = record.Id,
                    createdAt = Iso(record.CreatedAt),
                    scope = record.Scope,
                    kind = record.Kind,
                    status = record.Status,
                    tags = record.Tags,
                    projectId = record.ProjectId,
                    content = record.Content,
                }),
            }),
        };
        environment.Output.WriteLine(JsonSerializer.Serialize(payload, JsonOutput));
    }

    private static string Iso(DateTimeOffset value) => $"{value.ToUniversalTime():s}Z";

    private static string ProjectSuffix(string? project) => project is null ? string.Empty : $", project {project}";

    private static string Join(IReadOnlyList<string> values) => string.Join(",", values);

    private static string CountWords(int count) => count == 1 ? "1 record" : $"{count} records";

    private sealed record ActorGroup(MemoryActor Actor, IReadOnlyList<MemoryRecord> Records);

    private sealed record DayWindow(string Day, DateTimeOffset Start, DateTimeOffset End)
    {
        public string Range => $"{ReportCommand.Iso(Start)}..{ReportCommand.Iso(End)}";

        public static DayWindow Resolve(CommandSpec spec, string date)
        {
            CreatedAtRange range;
            try
            {
                range = CreatedAtRange.Parse(date, null);
            }
            catch (ArgumentException)
            {
                throw new UsageException(
                    $"--date must be a yyyy-MM-dd date or an ISO-8601 instant, got '{date}'.", spec);
            }

            var start = range.Since!.Value;
            var endLocalDay = start.LocalDateTime.Date.AddDays(1);
            var end = new DateTimeOffset(endLocalDay, TimeZoneInfo.Local.GetUtcOffset(endLocalDay)).ToUniversalTime();
            return new DayWindow(start.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), start, end);
        }
    }
}
