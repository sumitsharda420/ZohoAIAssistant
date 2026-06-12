using ZohoAIAssistant.DTOs;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Pulls the key CRM fields from a Zoho lead dictionary for AI prompts.
/// </summary>
public static class LeadContextExtractor
{
    public static LeadContextDto Extract(string leadId, ZohoLeadDto lead, string notes)
    {
        var firstName = GetField(lead, "First_Name");
        var lastName = GetField(lead, "Last_Name");
        var fullName = GetField(lead, "Full_Name");

        var leadName = !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : string.Join(' ', new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new LeadContextDto
        {
            LeadId = leadId,
            LeadName = string.IsNullOrWhiteSpace(leadName) ? "Unknown" : leadName,
            Company = GetField(lead, "Company"),
            Email = GetField(lead, "Email"),
            Phone = GetField(lead, "Phone", "Mobile"),
            Description = GetField(lead, "Description"),
            Notes = notes
        };
    }

    public static string FormatForPrompt(LeadContextDto context) =>
        $"""
        Lead ID: {context.LeadId}
        Lead Name: {context.LeadName}
        Company: {context.Company}
        Email: {context.Email}
        Phone: {context.Phone}
        Description: {context.Description}
        Notes: {context.Notes}
        """;

    private static string GetField(ZohoLeadDto lead, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (lead.TryGetValue(fieldName, out var value) && value is not null)
            {
                var text = value switch
                {
                    string s => s,
                    _ => value.ToString()
                };

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        return string.Empty;
    }
}
