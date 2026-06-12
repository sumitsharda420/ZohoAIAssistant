namespace ZohoAIAssistant.Configuration;

/// <summary>
/// Splits Zoho field API names into batches that respect the CRM API limit (50 fields per request).
/// </summary>
public static class ZohoFieldQueryHelper
{
    public const int MaxFieldsPerRequest = 50;

    /// <summary>
    /// Builds one or more comma-separated field lists. Each batch includes <c>id</c> for record merging.
    /// </summary>
    public static IReadOnlyList<string> BuildFieldBatches(IEnumerable<string> fieldApiNames)
    {
        var fields = fieldApiNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!fields.Any(name => name.Equals("id", StringComparison.OrdinalIgnoreCase)))
        {
            fields.Insert(0, "id");
        }

        if (fields.Count <= MaxFieldsPerRequest)
        {
            return [string.Join(',', fields)];
        }

        var nonIdFields = fields
            .Where(name => !name.Equals("id", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var batches = new List<string>();
        var batchSize = MaxFieldsPerRequest - 1;

        for (var offset = 0; offset < nonIdFields.Count; offset += batchSize)
        {
            var batch = new List<string> { "id" };
            batch.AddRange(nonIdFields.Skip(offset).Take(batchSize));
            batches.Add(string.Join(',', batch));
        }

        return batches;
    }
}
