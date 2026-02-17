using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;

namespace REA.Emergencia.Web.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly ApplicationDbContext _dbContext;

    public AppSettingsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken)
    {
        var normalizedKey = key.Trim();
        var setting = await _dbContext.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == normalizedKey, cancellationToken);

        return setting?.Value;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken)
    {
        var normalizedKey = key.Trim();
        var normalizedValue = value.Trim();

        var setting = await _dbContext.AppSettings
            .FirstOrDefaultAsync(x => x.Key == normalizedKey, cancellationToken);

        if (setting is null)
        {
            setting = new AppSetting
            {
                Key = normalizedKey,
                Value = normalizedValue
            };
            _dbContext.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = normalizedValue;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
