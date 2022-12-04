namespace Flipkg.Web.Api.Models;

public class ListResponse<T> where T : new()
{
    public ListResponse(IEnumerable<T> data, string? continuationToken)
    {
        Data = data;
        ContinuationToken = continuationToken;
    }

    public IEnumerable<T> Data { get; set; } = new List<T>();

    public string? ContinuationToken { get; set; }
}