namespace XNDmjApi.Loxone.Services;

public class LoxoneCommandService
{
    private readonly IHttpClientFactory _factory;

    public LoxoneCommandService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<(bool ok, string body, string urlPath)> SendLightCommandAsync(string command, CancellationToken ct)
    {
        var client = _factory.CreateClient("loxone");

        string urlPath = command.Trim();

        //if (!urlPath.StartsWith("dev/", StringComparison.OrdinalIgnoreCase))
        //{
        //    urlPath = "dev/sps/io/LightCommand/" + urlPath;
        //}

        try
        {
            var resp = await client.GetAsync(urlPath, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return (resp.IsSuccessStatusCode, body, urlPath);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, urlPath);
        }
    }
}