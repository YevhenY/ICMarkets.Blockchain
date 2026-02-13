namespace ICMarkets.Blockchain.API.Configuration
{
    /// <summary>
    /// Configuration settings for OData query options.
    /// Used to control validation and query behavior in GetHistory endpoint.
    /// </summary>
    public class ODataSettings
    {
        /// <summary>
        /// Maximum number of records that can be requested using $top. Default: 100
        /// </summary>
        public int MaxTop { get; set; } = 100;

        /// <summary>
        /// Properties that are allowed in $orderby queries.
        /// </summary>
        public string[] AllowedOrderByProperties { get; set; } = Array.Empty<string>();
    }
}
