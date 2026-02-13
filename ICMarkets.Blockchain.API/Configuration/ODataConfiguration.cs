using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using ICMarkets.Blockchain.Application.Features;

namespace ICMarkets.Blockchain.API.Configuration
{
    /// <summary>
    /// Configures the OData Entity Data Model (EDM) for the Blockchain API.
    /// </summary>
    public static class ODataConfiguration
    {
        public static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();

            // Register BlockchainHistory entity set
            // This enables OData queries on /odata/BlockchainHistory
            builder.EntitySet<BlockchainDto>("BlockchainHistory")
                   .EntityType
                   .HasKey(e => e.Id);

            return builder.GetEdmModel();
        }
    }
}
