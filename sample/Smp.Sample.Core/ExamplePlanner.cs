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

        var references = ReferencePattern().Matches(question).Select(m => m.Value.ToUpperInvariant()).Distinct().ToArray();
        if (references.Length > 1)
            return new("Use one reference number at a time in this sample.", []);
        if (references.Length == 1)
            return new("Matching catalog parts. Review the notes before deciding whether a part fits.", [new("Matching parts", $"""
                SELECT [Part Number], [Interchange Name], Price, Note
                FROM dbo.Interchange WHERE [Interchange Name] = N'{references[0]}'
                ORDER BY [Part Number]
                """)]);

        var parts = PartPattern().Matches(question).Select(m => m.Value.ToUpperInvariant()).Distinct().ToArray();
        var fitmentQuestion = FitmentPattern().IsMatch(question);
        if (fitmentQuestion && parts.Length == 0)
            parts = (request.PreviousParts ?? []).Where(p => p is not null && ExactPartPattern().IsMatch(p))
                .Select(p => p.ToUpperInvariant()).Distinct().Take(8).ToArray();
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

        if (fitmentQuestion) return new("Which part should I look up? Start with DEMO-OE-1001 or include a DEMO-PART number.", []);
        if (question.Equals("Count distinct parts by make", StringComparison.OrdinalIgnoreCase))
            return new("Each part is counted once per make, even when it has multiple model-year records.", [new("Parts by make", """
                SELECT Make, COUNT(DISTINCT Part) AS Parts FROM dbo.ApplicationViO
                GROUP BY Make ORDER BY Parts DESC, Make
                """)]);

        return new("This offline planner supports the example lookups shown above, DEMO-OE references, DEMO-PART numbers, and vehicle follow-ups. It is not a live AI model.", []);
    }

    [GeneratedRegex(@"\bDEMO-OE-\d{4}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();
    [GeneratedRegex(@"\bDEMO-PART-\d{3}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartPattern();
    [GeneratedRegex(@"\ADEMO-PART-[0-9]{3}\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExactPartPattern();
    [GeneratedRegex(@"\b(vehicle|vehicles|fit|fits|fitment|applications)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FitmentPattern();
}
