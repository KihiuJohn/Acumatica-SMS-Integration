using PX.Common;

namespace CelcomAfrica.SmsProvider
{
    [PXLocalizable]
    public static class Messages
    {
        // SMS Provider related messages
        public const string ProviderName = "Celcom Africa SMS Provider";
        public const string ApiKey_DetailID_Display = "API Key";
        public const string PartnerId_DetailID_Display = "Partner ID"; // Used in ISmsProvider settings
        public const string PartnerId_Display = "Partner ID";         // Used in BalanceCheckParameters
        public const string ShortCode_DetailID_Display = "Short Code";

        // SMS Send related errors (from CelcomAfricaSmsProvider.cs)
        public const string SmsFailedError = "SMS Failed: Error {0}: {1}";
        public const string SmsExceptionError = "Failed to send SMS: {0}";
        public const string SmsInvalidPhoneNumber = "Invalid phone number provided.";
        public const string SmsEmptyOrUnexpectedResponse = "Unexpected or empty response from SMS API. Status: {0}, Response: {1}";
        public const string SmsParsingFailed = "Failed to parse response from SMS API.";

        // New for Balance Check
        public const string MissingCredentials = "Partner ID and API Key are required.";
        public const string UnexpectedBalanceResponse = "Unexpected or empty response from balance check API (HTTP Status: {0}). Content: {1}";
        public const string BalanceParsingFailed = "Failed to parse balance check API response: {0}";
        public const string BalanceCheckFailed = "Balance check failed with Celcom Africa API Code {0}: {1}";
        public const string BalanceException = "An error occurred during balance check: {0}";
        public const string BalanceLogInsertError = "Database insert failed: {0}"; // Not directly used in the graph, but good to have
    }
}
