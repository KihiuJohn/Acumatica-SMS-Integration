using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PX.Data;
using PX.SmsProvider;
using System.Threading.Tasks;
using CelcomAfrica.SmsProvider; // Assuming this namespace contains Messages and SMSLog

namespace CelcomAfrica.SmsProvider
{
    public class CelcomAfricaSmsProvider : ISmsProvider
    {
        #region DetailIDs const
        private const string ApiKey_DetailID = "API_KEY";
        private const string PartnerId_DetailID = "PARTNER_ID";
        private const string ShortCode_DetailID = "SHORT_CODE";
        #endregion

        // Define CUSTOM INTERNAL error codes for situations where Celcom Africa's API
        // does NOT provide a structured response code (e.g., network issues, invalid API response format).
        private const int ErrorCode_NetworkError = 901;    // Connection failed, timeout, DNS error (before HTTP response)
        private const int ErrorCode_EmptyOrInvalidApiResponseFormat = 902; // Got HTTP response, but content was empty or not JSON-structured, or unexpected JSON structure
        private const int ErrorCode_JsonParsingFailed = 903; // Got HTTP response, content was JSON-like but unparsable
        private const int ErrorCode_UnhandledException = 999; // General fallback for any other unexpected internal error

        private string m_ApiKey;
        public string ApiKey => m_ApiKey;

        private string m_PartnerId;
        public string PartnerId => m_PartnerId;

        private string m_ShortCode;
        public string ShortCode => m_ShortCode;

        public string Id => "CELCOM";
        public string Name => Messages.ProviderName;

        public IEnumerable<PXFieldState> ExportSettings
        {
            get
            {
                var settings = new List<PXFieldState>();

                var apiKey = (PXStringState)PXStringState.CreateInstance(
                    m_ApiKey, null, false, ApiKey_DetailID, null, 1, null, null, null, null, null);
                apiKey.DisplayName = Messages.ApiKey_DetailID_Display;
                settings.Add(apiKey);

                var partnerId = (PXStringState)PXStringState.CreateInstance(
                    m_PartnerId, null, false, PartnerId_DetailID, null, 1, null, null, null, null, null);
                partnerId.DisplayName = Messages.PartnerId_Display;
                settings.Add(partnerId);

                var shortCode = (PXStringState)PXStringState.CreateInstance(
                    m_ShortCode, null, false, ShortCode_DetailID, null, 1, null, null, null, null, null);
                shortCode.DisplayName = Messages.ShortCode_DetailID_Display;
                settings.Add(shortCode);

                return settings;
            }
        }

        public void LoadSettings(IEnumerable<ISmsProviderSetting> settings)
        {
            foreach (ISmsProviderSetting detail in settings)
            {
                switch (detail.Name.ToUpper())
                {
                    case ApiKey_DetailID: m_ApiKey = detail.Value; break;
                    case PartnerId_DetailID: m_PartnerId = detail.Value; break;
                    case ShortCode_DetailID: m_ShortCode = detail.Value; break;
                }
            }
        }

        [Obsolete]
        public async Task SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken)
        {
            var phoneNumber = request.RecipientPhoneNbr;
            var message = request.RecipientSMSMessage; // This is the full message from Acumatica
            string rawResponseContent = null; // Stores the raw text received from the HTTP response
            int httpStatusCode = 0;           // Stores the HTTP status code (e.g., 200, 404, 500)

            // These will store the application-level response code and description from Celcom Africa's JSON payload
            int celcomApiResponseCode = 0;
            string celcomApiResponseDescription = null;
            string celcomMessageId = null;

            try
            {
                // Optional: Basic validation for message length before sending
                // This value (e.g., 1600) should be based on Celcom Africa's actual SMS length limits.
                const int MaxSmsMessageLength = 1600; // Example: Max characters for multi-part SMS
                if (message.Length > MaxSmsMessageLength)
                {
                    string truncateMessage = $"SMS message exceeds maximum allowed length ({MaxSmsMessageLength} characters). Message length: {message.Length}. Truncating for logging purposes.";
                    PXTrace.WriteWarning(truncateMessage);

                    // Log the error immediately
                    PXDatabase.Insert<SMSLog>(
                        new PXDataFieldAssign<SMSLog.mobile>(phoneNumber),
                        new PXDataFieldAssign<SMSLog.message>(message.Substring(0, Math.Min(message.Length, 2048))), // Log potentially truncated message for context if column is smaller
                        new PXDataFieldAssign<SMSLog.shortCode>(m_ShortCode),
                        new PXDataFieldAssign<SMSLog.responseCode>(ErrorCode_UnhandledException), // Use a suitable error code for message too long
                        new PXDataFieldAssign<SMSLog.responseDescription>(truncateMessage),
                        new PXDataFieldAssign("CreatedByID", PXAccess.GetUserID()),
                        new PXDataFieldAssign<SMSLog.createdDateTime>(DateTime.UtcNow)
                    );
                    // Acuminator disable once PX1051 NonLocalizableString [Justification]
                    throw new PXException(Messages.SmsExceptionError, truncateMessage);
                }


                using (var client = new HttpClient())
                {
                    // It's good practice to set a timeout to prevent hanging indefinitely
                    client.Timeout = TimeSpan.FromSeconds(30);

                    var requestUrl = "https://isms.celcomafrica.com/api/services/sendsms/";
                    var payload = new
                    {
                        partnerID = m_PartnerId,
                        apikey = m_ApiKey,
                        mobile = phoneNumber,
                        // IMPORTANT FIX: Removed Uri.EscapeDataString. JsonConvert.SerializeObject handles JSON escaping.
                        message = message,
                        shortcode = m_ShortCode,
                        pass_type = "plain"
                    };

                    var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    PXTrace.WriteInformation($"SMS Payload: {JsonConvert.SerializeObject(payload)}");

                    HttpResponseMessage httpResponse = null;
                    try
                    {
                        httpResponse = await client.PostAsync(requestUrl, content, cancellationToken);
                        httpStatusCode = (int)httpResponse.StatusCode;
                        rawResponseContent = await httpResponse.Content.ReadAsStringAsync();

                        PXTrace.WriteInformation($"HTTP Status Code Received: {httpStatusCode} ({httpResponse.StatusCode})");
                        PXTrace.WriteInformation($"SMS Raw Response Content: {rawResponseContent}");
                    }
                    catch (HttpRequestException ex) // This catches network issues, DNS failures, connection refused, etc.
                    {
                        PXTrace.WriteInformation($"SMS Network Error (HttpRequestException): {ex.Message}");
                        // Log to SMSLog using our internal error code for network issues
                        PXDatabase.Insert<SMSLog>(
                            new PXDataFieldAssign<SMSLog.mobile>(phoneNumber),
                            new PXDataFieldAssign<SMSLog.message>(message),
                            new PXDataFieldAssign<SMSLog.shortCode>(m_ShortCode),
                            new PXDataFieldAssign<SMSLog.responseCode>(ErrorCode_NetworkError),
                            new PXDataFieldAssign<SMSLog.responseDescription>($"Network connection failed: {ex.Message}{(ex.InnerException != null ? " Inner: " + ex.InnerException.Message : "")}"),
                            new PXDataFieldAssign("CreatedByID", PXAccess.GetUserID()),
                            new PXDataFieldAssign<SMSLog.createdDateTime>(DateTime.UtcNow)
                        );
                        // Acuminator disable once PX1051 NonLocalizableString [Justification]
                        throw new PXException(Messages.SmsExceptionError, "Network error during SMS send.", ex);
                    }

                    // --- We have successfully received an HTTP response (httpResponse is not null here) ---

                    JObject parsedJsonResult;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(rawResponseContent) || !rawResponseContent.TrimStart().StartsWith("{"))
                        {
                            // We received a response, but it's not valid JSON format from the start (e.g., HTML error page, empty)
                            // Log using our internal error code for unexpected response format
                            PXDatabase.Insert<SMSLog>(
                                new PXDataFieldAssign<SMSLog.mobile>(phoneNumber),
                                new PXDataFieldAssign<SMSLog.message>(message),
                                new PXDataFieldAssign<SMSLog.shortCode>(m_ShortCode),
                                new PXDataFieldAssign<SMSLog.responseCode>(ErrorCode_EmptyOrInvalidApiResponseFormat),
                                new PXDataFieldAssign<SMSLog.responseDescription>($"API returned non-JSON or empty response. HTTP Status: {httpStatusCode}. Content: {rawResponseContent ?? "[EMPTY]"}"),
                                new PXDataFieldAssign("CreatedByID", PXAccess.GetUserID()),
                                new PXDataFieldAssign<SMSLog.createdDateTime>(DateTime.UtcNow)
                            );
                            // Acuminator disable once PX1051 NonLocalizableString [Justification]
                            throw new PXException(Messages.SmsEmptyOrUnexpectedResponse, httpStatusCode, rawResponseContent);
                        }

                        parsedJsonResult = JObject.Parse(rawResponseContent);
                    }
                    catch (JsonException ex) // Catches if the content *looks* like JSON but is structurally invalid
                    {
                        PXTrace.WriteInformation($"SMS JSON Parsing Error (JsonException): {ex.Message}");
                        // Log using our internal error code for JSON parsing failures
                        PXDatabase.Insert<SMSLog>(
                            new PXDataFieldAssign<SMSLog.mobile>(phoneNumber),
                            new PXDataFieldAssign<SMSLog.message>(message),
                            new PXDataFieldAssign<SMSLog.shortCode>(m_ShortCode),
                            new PXDataFieldAssign<SMSLog.responseCode>(ErrorCode_JsonParsingFailed),
                            new PXDataFieldAssign<SMSLog.responseDescription>($"Failed to parse API JSON response. HTTP Status: {httpStatusCode}. Error: {ex.Message}"),
                            new PXDataFieldAssign("CreatedByID", PXAccess.GetUserID()),
                            new PXDataFieldAssign<SMSLog.createdDateTime>(DateTime.UtcNow)
                        );
                        // Acuminator disable once PX1051 NonLocalizableString [Justification]
                        throw new PXException(Messages.SmsExceptionError, $"Failed to parse API response: {ex.Message}", ex);
                    }

                    // --- We have successfully parsed the JSON response. Now extract Celcom Africa's application-level codes ---

                    // FIX: Safely access the 'responses' array and its first element to prevent "Index out of range"
                    JArray responsesArray = parsedJsonResult["responses"] as JArray;

                    if (responsesArray != null && responsesArray.Count > 0)
                    {
                        var firstResponse = responsesArray[0];
                        celcomApiResponseCode = (int?)firstResponse?["response-code"] ?? 0;
                        celcomApiResponseDescription = firstResponse?["response-description"]?.ToString();
                        celcomMessageId = firstResponse?["messageid"]?.ToString();
                    }
                    else
                    {
                        // Handle cases where 'responses' array is missing or empty
                        PXTrace.WriteWarning($"SMS API Response: 'responses' array missing or empty or not a JArray. Raw content: {rawResponseContent}");
                        celcomApiResponseCode = ErrorCode_EmptyOrInvalidApiResponseFormat; // Use internal error code
                        celcomApiResponseDescription = $"API returned response without expected 'responses' array or it was empty. HTTP Status: {httpStatusCode}. Raw: {rawResponseContent}";
                        // If Celcom Africa sometimes returns a different structure for errors (e.g., error_code at root),
                        // you could add logic here to try and parse that alternative structure.
                    }

                    PXTrace.WriteInformation($"Celcom Africa API Response - Code: {celcomApiResponseCode}, Description: {celcomApiResponseDescription}, MessageID: {celcomMessageId}");

                    // Always log the Celcom Africa API response (success or failure) to the SMSLog table.
                    // This is key: We log THEIR response code and description if we successfully extracted it.
                    PXDatabase.Insert<SMSLog>(
                        new PXDataFieldAssign<SMSLog.mobile>(phoneNumber),
                        new PXDataFieldAssign<SMSLog.message>(message), // Log the original full message
                        new PXDataFieldAssign<SMSLog.shortCode>(m_ShortCode),
                        new PXDataFieldAssign<SMSLog.responseCode>(celcomApiResponseCode), // THIS IS THE CELCOM AFRICA CODE (e.g., 200, 1004, 4091) or our custom code
                        new PXDataFieldAssign<SMSLog.responseDescription>(celcomApiResponseDescription), // THIS IS THEIR DESCRIPTION or our custom description
                        new PXDataFieldAssign<SMSLog.messageID>(celcomMessageId), // Will be null for errors, populated for success
                        new PXDataFieldAssign("CreatedByID", PXAccess.GetUserID()), // Ensure this gets a valid user ID in all contexts
                        new PXDataFieldAssign<SMSLog.createdDateTime>(DateTime.UtcNow)
                    );

                    // Now, decide if we throw an exception based on Celcom Africa's application-level code
                    if (celcomApiResponseCode != 200)
                    {
                        // Celcom Africa reported an application-level error (e.g., 1004, 4091)
                        // Acuminator disable once PX1051 NonLocalizableString [Justification]
                        throw new PXException(Messages.SmsFailedError, celcomApiResponseCode, celcomApiResponseDescription);
                    }

                    PXTrace.WriteInformation($"SMS Sent Successfully by Celcom Africa: MessageID = {celcomMessageId}");
                }
            }
            catch (PXException) // Catch PXExceptions thrown by us earlier, re-throw to maintain flow
            {
                throw;
            }
            catch (Exception ex) // General catch-all for any other unexpected exceptions not specifically handled
            {
                PXTrace.WriteInformation($"SMS General Unhandled Error: {ex.Message}");
                // Log to SMSLog using our most general internal error code
                PXDatabase.Insert<SMSLog>(
                    new PXDataFieldAssign<SMSLog.mobile>(phoneNumber),
                    new PXDataFieldAssign<SMSLog.message>(message),
                    new PXDataFieldAssign<SMSLog.shortCode>(m_ShortCode),
                    new PXDataFieldAssign<SMSLog.responseCode>(ErrorCode_UnhandledException),
                    new PXDataFieldAssign<SMSLog.responseDescription>($"Unhandled internal exception: {ex.Message}{(ex.InnerException != null ? " Inner: " + ex.InnerException.Message : "")}"),
                    new PXDataFieldAssign("CreatedByID", PXAccess.GetUserID()),
                    new PXDataFieldAssign<SMSLog.createdDateTime>(DateTime.UtcNow)
                );
                // Acuminator disable once PX1051 NonLocalizableString [Justification]
                throw new PXException(Messages.SmsExceptionError, "An unexpected error occurred during SMS send.", ex);
            }
        }
    }
}