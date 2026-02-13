namespace ICMarkets.Blockchain.Domain.Entities
{
    public class BlockchainData
    {
        public Guid Id { get; set; }

        public required string Symbol { get; set; }

        public required string RequestUrl { get; set; }

        public required string JsonResponse { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
