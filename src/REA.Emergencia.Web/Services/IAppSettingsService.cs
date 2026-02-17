namespace REA.Emergencia.Web.Services;

public interface IAppSettingsService
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken);
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken);
}
