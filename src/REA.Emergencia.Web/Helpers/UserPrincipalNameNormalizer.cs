namespace REA.Emergencia.Web.Helpers;

public static class UserPrincipalNameNormalizer
{
    public const string GuestSuffix = "#EXT#@entrajuda.onmicrosoft.com";

    public static string Normalize(string? userPrincipalName)
    {
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            return string.Empty;
        }

        var value = userPrincipalName.Trim();
        if (value.EndsWith(GuestSuffix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^GuestSuffix.Length];
        }

        return value;
    }

    public static IReadOnlyList<string> BuildCandidates(string? userPrincipalName)
    {
        var normalized = Normalize(userPrincipalName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            normalized,
            normalized + GuestSuffix
        };

        // Guest users often appear as local_part_domain.tld#EXT#@tenant,
        // i.e. the original '@' is replaced by '_'.
        if (normalized.Contains('@'))
        {
            var guestBase = normalized.Replace("@", "_");
            candidates.Add(guestBase);
            candidates.Add(guestBase + GuestSuffix);
        }

        return candidates.ToList();
    }
}
