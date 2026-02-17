using Microsoft.AspNetCore.Mvc.Rendering;

namespace REA.Emergencia.Web.Models;

public static class AppThemeCatalog
{
    private const string DefaultThemeKey = "bootstrap-local";

    private static readonly IReadOnlyDictionary<string, string> ThemeUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["bootstrap-local"] = "~/lib/bootstrap/dist/css/bootstrap.min.css",
        ["bootswatch-cerulean"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/cerulean/bootstrap.min.css",
        ["bootswatch-cosmo"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/cosmo/bootstrap.min.css",
        ["bootswatch-flatly"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/flatly/bootstrap.min.css",
        ["bootswatch-journal"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/journal/bootstrap.min.css",
        ["bootswatch-litera"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/litera/bootstrap.min.css",
        ["bootswatch-lumen"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/lumen/bootstrap.min.css",
        ["bootswatch-lux"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/lux/bootstrap.min.css",
        ["bootswatch-minty"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/minty/bootstrap.min.css",
        ["bootswatch-morph"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/morph/bootstrap.min.css",
        ["bootswatch-pulse"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/pulse/bootstrap.min.css",
        ["bootswatch-sandstone"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/sandstone/bootstrap.min.css",
        ["bootswatch-simplex"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/simplex/bootstrap.min.css",
        ["bootswatch-sketchy"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/sketchy/bootstrap.min.css",
        ["bootswatch-slate"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/slate/bootstrap.min.css",
        ["bootswatch-vapor"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/vapor/bootstrap.min.css",
        ["bootswatch-yeti"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/yeti/bootstrap.min.css",
        ["bootswatch-zephyr"] = "https://cdn.jsdelivr.net/npm/bootswatch@5.3.8/dist/zephyr/bootstrap.min.css"
    };

    public static string ResolveThemeUrl(string? themeKey)
    {
        var normalized = string.IsNullOrWhiteSpace(themeKey) ? DefaultThemeKey : themeKey.Trim();
        if (ThemeUrls.TryGetValue(normalized, out var url))
        {
            return url;
        }

        return ThemeUrls[DefaultThemeKey];
    }

    public static IReadOnlyList<SelectListItem> GetThemeOptions(string? selectedThemeKey)
    {
        var selected = string.IsNullOrWhiteSpace(selectedThemeKey) ? DefaultThemeKey : selectedThemeKey.Trim();
        return new List<SelectListItem>
        {
            new() { Value = "bootstrap-local", Text = "Bootstrap (Padrão)" },
            new() { Value = "bootswatch-cerulean", Text = "Bootswatch Cerulean" },
            new() { Value = "bootswatch-cosmo", Text = "Bootswatch Cosmo" },
            new() { Value = "bootswatch-flatly", Text = "Bootswatch Flatly" },
            new() { Value = "bootswatch-journal", Text = "Bootswatch Journal" },
            new() { Value = "bootswatch-litera", Text = "Bootswatch Litera" },
            new() { Value = "bootswatch-lumen", Text = "Bootswatch Lumen" },
            new() { Value = "bootswatch-lux", Text = "Bootswatch Lux" },
            new() { Value = "bootswatch-minty", Text = "Bootswatch Minty" },
            new() { Value = "bootswatch-morph", Text = "Bootswatch Morph" },
            new() { Value = "bootswatch-pulse", Text = "Bootswatch Pulse" },
            new() { Value = "bootswatch-sandstone", Text = "Bootswatch Sandstone" },
            new() { Value = "bootswatch-simplex", Text = "Bootswatch Simplex" },
            new() { Value = "bootswatch-sketchy", Text = "Bootswatch Sketchy" },
            new() { Value = "bootswatch-slate", Text = "Bootswatch Slate" },
            new() { Value = "bootswatch-vapor", Text = "Bootswatch Vapor" },
            new() { Value = "bootswatch-yeti", Text = "Bootswatch Yeti" },
            new() { Value = "bootswatch-zephyr", Text = "Bootswatch Zephyr" }
        }
        .Select(x =>
        {
            x.Selected = string.Equals(x.Value, selected, StringComparison.OrdinalIgnoreCase);
            return x;
        })
        .ToList();
    }
}
