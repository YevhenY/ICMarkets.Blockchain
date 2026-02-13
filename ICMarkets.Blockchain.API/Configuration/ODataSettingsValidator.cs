using Microsoft.Extensions.Options;

namespace ICMarkets.Blockchain.API.Configuration
{
    /// <summary>
    /// Validates ODataSettings configuration at startup.
    /// Ensures OData query limits and allowed properties are correctly configured.
    /// </summary>
    public class ODataSettingsValidator : IValidateOptions<ODataSettings>
    {
        public ValidateOptionsResult Validate(string? name, ODataSettings options)
        {
            if (options == null)
                return ValidateOptionsResult.Fail("OData settings are missing.");

            // Validate MaxTop
            if (options.MaxTop <= 0)
                return ValidateOptionsResult.Fail("OData:MaxTop must be greater than 0.");

            if (options.MaxTop > 1000)
                return ValidateOptionsResult.Fail("OData:MaxTop should not exceed 1000 to prevent performance issues.");

            // Validate AllowedOrderByProperties
            if (options.AllowedOrderByProperties == null || options.AllowedOrderByProperties.Length == 0)
                return ValidateOptionsResult.Fail("OData:AllowedOrderByProperties must contain at least one property.");

            // Validate each property name
            foreach (var property in options.AllowedOrderByProperties)
            {
                if (string.IsNullOrWhiteSpace(property))
                    return ValidateOptionsResult.Fail("OData:AllowedOrderByProperties contains an empty or whitespace property name.");

                if (property.Length > 100)
                    return ValidateOptionsResult.Fail($"OData:AllowedOrderByProperties property '{property}' exceeds maximum length of 100 characters.");

                // Property names should be valid C# identifiers (letters, digits, underscores)
                if (!IsValidPropertyName(property))
                    return ValidateOptionsResult.Fail($"OData:AllowedOrderByProperties property '{property}' is not a valid property name. Use only letters, digits, and underscores.");
            }

            // Check for duplicate property names
            var distinctProperties = options.AllowedOrderByProperties.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinctProperties.Length != options.AllowedOrderByProperties.Length)
                return ValidateOptionsResult.Fail("OData:AllowedOrderByProperties contains duplicate property names (case-insensitive).");

            return ValidateOptionsResult.Success;
        }

        private static bool IsValidPropertyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // First character must be a letter or underscore
            if (!char.IsLetter(name[0]) && name[0] != '_')
                return false;

            // Remaining characters must be letters, digits, or underscores
            return name.All(c => char.IsLetterOrDigit(c) || c == '_');
        }
    }
}
