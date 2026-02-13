namespace ICMarkets.Blockchain.Domain.Validation
{
    public static class BlockchainDataRules
    {
        public static class Symbol
        {
            public const int MinLength = 3;
            public const int MaxLength = 20;
        }

        public static class RequestUrl
        {
            public const int MaxLength = 500;
        }
    }

    public static class SymbolValidator
    {
        public const int MinLength = BlockchainDataRules.Symbol.MinLength;
        public const int MaxLength = BlockchainDataRules.Symbol.MaxLength;

        public static bool IsValid(string? symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return false;

            if (symbol.Length < MinLength || symbol.Length > MaxLength)
                return false;

            return symbol.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        public static string? GetValidationError(string? symbol, string fieldName = "Symbol")
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return $"{fieldName} is required.";

            if (symbol.Length < MinLength || symbol.Length > MaxLength)
                return $"{fieldName} must be between {MinLength} and {MaxLength} characters.";

            if (!symbol.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                return $"{fieldName} must contain only letters, digits, hyphens, or underscores.";

            return null;
        }
    }
}
