using System.Text.Json.Serialization;

namespace InventoryManager.Models
{
    /// <summary>
    /// Generic wrapper for ERPNext REST API responses.
    /// ERPNext returns data in the format: { "data": [ ... ] }
    /// </summary>
    public class ErpNextApiResponse<T>
    {
        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = new();
    }
}
