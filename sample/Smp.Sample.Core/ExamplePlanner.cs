using System.Text.RegularExpressions;

namespace Smp.Sample;

// Deterministic examples replace the model ONLY in the public sample.
// The database, validation, execution and result rendering are real.
public static partial class ExamplePlanner
{
    public static QueryPlan Plan(LookupRequest request)
    {
        var question = request.Question?.Trim() ?? "";
        if (question.Length is < 1 or > 500)
            return new("Enter a question between 1 and 500 characters.", []);

        // Match the whole question, not identifiers or keywords inside arbitrary prose.
        // Otherwise an unsupported filter could silently become an unfiltered lookup.
        var reference = ReferenceQuestionPattern().Match(question);
        if (reference.Success)
            return new("Matching catalog parts. Review the notes before deciding whether a part fits.", [new("Matching parts", $"""
                SELECT [Part Number], [Interchange Name], Price, Note
                FROM dbo.Interchange WHERE [Interchange Name] = N'{reference.Groups["reference"].Value.ToUpperInvariant()}'
                ORDER BY [Part Number]
                """)]);

        var application = ApplicationQuestionPattern().Match(question);
        var followUp = FollowUpQuestionPattern().IsMatch(question);
        string[] parts = [];
        if (application.Success)
            parts = PartPattern().Matches(application.Groups["parts"].Value)
                .Select(m => m.Value.ToUpperInvariant()).Distinct().ToArray();
        else if (followUp)
        {
            var previousParts = request.PreviousParts ?? [];
            if (previousParts.Any(p => p is null || !ExactPartPattern().IsMatch(p)))
                return new("The previous part list is invalid. Start a new lookup with a sample reference.", []);
            parts = previousParts.Select(p => p.ToUpperInvariant()).Distinct().ToArray();
        }
        if (parts.Length > 8) return new("Use up to eight sample part numbers at a time.", []);
        if (parts.Length > 0)
        {
            // The regex accepts only DEMO-PART- plus exactly three digits.
            var values = string.Join(", ", parts.Select(p => $"N'{p}'"));
            return new("Vehicle application records for the requested parts. Separate rows describe different part/vehicle/year combinations.", [new("Vehicle applications", $"""
                SELECT Part, Make, Model, [Year], VIO FROM dbo.ApplicationViO
                WHERE Part IN ({values}) ORDER BY Part, Make, Model, [Year]
                """)]);
        }

        if (followUp) return new("Which part should I look up? Start with DEMO-OE-1001 or enter a DEMO-PART number.", []);
        if (CountQuestionPattern().IsMatch(question))
            return new("Each part is counted once per make, even when it has multiple model-year records.", [new("Parts by make", """
                SELECT Make, COUNT(DISTINCT Part) AS Parts FROM dbo.ApplicationViO
                GROUP BY Make ORDER BY Parts DESC, Make
                """)]);

        return new("This offline sample could not match the whole question to a supported example. " +
            "Extra filters, sorting, exclusions, and combined requests are not supported; no lookup was run. " +
            "Choose an example, enter a sample reference or part number, or use 'Show applications for DEMO-PART-101'. " +
            "It is not a live AI model.", []);
    }

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string Part = @"DEMO-PART-[0-9]{3}";

    [GeneratedRegex(@"\A(?:Which parts match\s+)?(?<reference>DEMO-OE-[0-9]{4})\??\z", Options)]
    private static partial Regex ReferenceQuestionPattern();
    [GeneratedRegex(@"\A(?:Show applications for\s+)?(?<parts>" + Part + @"(?:(?:\s*,\s*|\s+and\s+)" + Part + @")*)\??\z", Options)]
    private static partial Regex ApplicationQuestionPattern();
    [GeneratedRegex(@"\A(?:Which vehicles (?:do these (?:parts )?)?fit|What applications does it fit)\??\z", Options)]
    private static partial Regex FollowUpQuestionPattern();
    [GeneratedRegex(@"\ACount distinct parts by make\??\z", Options)]
    private static partial Regex CountQuestionPattern();
    [GeneratedRegex(Part, Options)]
    private static partial Regex PartPattern();
    [GeneratedRegex(@"\A" + Part + @"\z", Options)]
    private static partial Regex ExactPartPattern();
}
