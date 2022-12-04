using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Flipkg.Web.Api.Models;

public class App
{
    public const string POSTBINDS = "Owner,Name,Description,Url,Category,Tags";

     [JsonProperty(PropertyName = "id")]
    public string? Id { get; set; }

    [Required]
    public string Owner { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string? Description { get; set; }

    public DateTime DateAdded { get; set; }

    public string? Url { get; set; }

    public string? Category { get; set; }

    public IEnumerable<string> Tags { get; set; } = new List<string>();

    public IEnumerable<string> Platforms { get; set; } = new List<string>();
}