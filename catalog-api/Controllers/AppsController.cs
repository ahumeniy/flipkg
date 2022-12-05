using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Flipkg.Web.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AppsController : ControllerBase
{
    private readonly ILogger<AppsController> _logger;
    private readonly IConfiguration _config;

    public AppsController(ILogger<AppsController> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    [HttpGet(Name = "GetApps")]
    public async Task<Models.ListResponse<Models.App>> Get([FromHeader(Name="continuation-token")] string? continuationToken)
    {
        List<Models.App> results = new List<Models.App>();

        using CosmosClient client = new(
            connectionString: _config["COSMOS_CONNECTION_STRING"]!
        );

        var container = client.GetContainer("Catalog", "Apps");

        var queryable = container.GetItemLinqQueryable<Models.App>(requestOptions: new QueryRequestOptions()
        {
            MaxItemCount = 5
        },
        continuationToken: continuationToken);

        var matches = queryable.OrderByDescending(x => x.DateAdded);

        using var linqFeed = matches.ToFeedIterator();

        string? newContinuationToken = null;

        if (linqFeed.HasMoreResults)
        {
            FeedResponse<Models.App> response = await linqFeed.ReadNextAsync();

            results.AddRange(response);

            if (response.Count > 0)
            {
                newContinuationToken = response.ContinuationToken;
            }
        }

        return new Models.ListResponse<Models.App>(results, newContinuationToken);
    }

    [HttpGet("{owner}/{name}", Name = "Get App")]
    public async Task<Models.App?> GetApp(string owner, string name)
    {
        Models.App? result = null;

        using CosmosClient client = new(
            connectionString: _config["COSMOS_CONNECTION_STRING"]!
        );

        var container = client.GetContainer("Catalog", "Apps");

        Models.App readItem = await container.ReadItemAsync<Models.App>(
            id: $"{owner}::{name}",
            partitionKey: new PartitionKey(owner)
        );

        var queryable = container.GetItemLinqQueryable<Models.App>();

        var matches = queryable.Where(x => x.Owner == owner && x.Name == name);

        using var linqFeed = matches.ToFeedIterator();

        if (linqFeed.HasMoreResults)
        {
            FeedResponse<Models.App> response = await linqFeed.ReadNextAsync();

            if (response.Count > 0)
                result = response.FirstOrDefault();
        }

        return result;
    }

    [HttpPost(Name = "Post App")]
    public async Task<ActionResult<Models.App>> Post([Bind(include: Models.App.POSTBINDS)] Models.App app)
    {
        using CosmosClient client = new(
            connectionString: _config["COSMOS_CONNECTION_STRING"]!
        );

        var container = client.GetContainer("Catalog", "Apps");

        try
        {
            app.Id = $"{app.Owner}|{app.Name}";
            app.DateAdded = DateTime.UtcNow;
            Models.App createdItem = await container.CreateItemAsync<Models.App>(app);

            return CreatedAtAction(nameof(GetApp), new { owner = createdItem.Owner, name = createdItem.Name }, createdItem);
        }
        catch (CosmosException ex)
        {
            _logger.LogWarning(ex, ex.Message);
            return new StatusCodeResult((int)ex.StatusCode);
        }
    }
}