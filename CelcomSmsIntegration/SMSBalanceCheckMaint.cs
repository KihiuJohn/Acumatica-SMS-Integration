using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PX.Data;
using PX.Data.BQL.Fluent;
using CelcomAfrica.SmsProvider;

namespace CelcomSmsIntegration
{
    public class SMSBalanceCheckMaint : PXGraph<SMSBalanceCheckMaint>
    {
        public PXSave<BalanceCheckParameters> Save;
        public PXCancel<BalanceCheckParameters> Cancel;
        public PXSelect<BalanceCheckParameters> RequestView;
        public PXSelect<SMSBalanceLog> BalanceLogs;

        public SMSBalanceCheckMaint()
        {
            RequestView.Cache.AllowDelete = false;
        }

        public PXAction<BalanceCheckParameters> CheckBalance;
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Check Balance")]
        protected virtual IEnumerable checkBalance(PXAdapter adapter)
        {
            BalanceCheckParameters parameters = RequestView.Current as BalanceCheckParameters;
            if (parameters == null)
            {
                parameters = RequestView.Insert(new BalanceCheckParameters());
            }

            if (string.IsNullOrEmpty(parameters.PartnerID) || string.IsNullOrEmpty(parameters.ApiKey))
            {
                // Acuminator disable once PX1051 NonLocalizableString [Justification]
                throw new PXException(CelcomAfrica.SmsProvider.Messages.MissingCredentials);
            }

            string requestUrl = "https://isms.celcomafrica.com/api/services/getbalance/";
            var payload = new
            {
                partnerID = parameters.PartnerID,
                apikey = parameters.ApiKey
            };

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    var response = client.PostAsync(requestUrl, content).Result;
                    var rawResponseContent = response.Content.ReadAsStringAsync().Result;

                    if (string.IsNullOrWhiteSpace(rawResponseContent) || !rawResponseContent.TrimStart().StartsWith("{"))
                    {
                        // Acuminator disable once PX1051 NonLocalizableString [Justification]
                        throw new PXException(CelcomAfrica.SmsProvider.Messages.UnexpectedBalanceResponse, (int)response.StatusCode, rawResponseContent ?? "[EMPTY]");
                    }

                    JObject result = JObject.Parse(rawResponseContent);
                    int responseCode = int.TryParse(result["response-code"]?.ToString(), out int code) ? code : 0;
                    decimal? credit = decimal.TryParse(result["credit"]?.ToString(), out decimal cred) ? (decimal?)cred : null;
                    string partnerId = result["partner-id"]?.ToString() ?? parameters.PartnerID;

                    var logEntry = BalanceLogs.Insert(new SMSBalanceLog
                    {
                        PartnerID = partnerId,
                        ResponseCode = responseCode,
                        Credit = credit,
                        CreatedDateTime = DateTime.UtcNow
                    });

                    if (responseCode != 200)
                    {
                        // Acuminator disable once PX1051 NonLocalizableString [Justification]
                        throw new PXException(CelcomAfrica.SmsProvider.Messages.BalanceCheckFailed, responseCode, result.ToString());
                    }

                    PXUIFieldAttribute.SetWarning<BalanceCheckParameters.partnerID>(RequestView.Cache, parameters, $"Current Balance: {credit}");

                    Actions.PressSave();
                }
            }
            catch (HttpRequestException ex)
            {
                BalanceLogs.Insert(new SMSBalanceLog
                {
                    PartnerID = parameters.PartnerID,
                    ResponseCode = 901,
                    CreatedDateTime = DateTime.UtcNow
                });
                // Acuminator disable once PX1051 NonLocalizableString [Justification]
                throw new PXException(CelcomAfrica.SmsProvider.Messages.BalanceException, $"Network error: {ex.Message}");
            }
            catch (JsonException jsonEx)
            {
                BalanceLogs.Insert(new SMSBalanceLog
                {
                    PartnerID = parameters.PartnerID,
                    ResponseCode = 903,
                    CreatedDateTime = DateTime.UtcNow
                });
                // Acuminator disable once PX1051 NonLocalizableString [Justification]
                throw new PXException(CelcomAfrica.SmsProvider.Messages.BalanceParsingFailed, jsonEx.Message);
            }

            BalanceLogs.View.RequestRefresh();
            return adapter.Get();
        }

        public PXAction<BalanceCheckParameters> StartSchedule;
        [PXButton]
        [PXUIField(DisplayName = "Start Schedule")]
        protected virtual IEnumerable startSchedule(PXAdapter adapter)
        {
            BalanceCheckParameters parameters = RequestView.Current;
            if (parameters == null)
            {
                parameters = RequestView.Insert(new BalanceCheckParameters());
            }

            if (parameters.IsActive == true)
            {
                int intervalMinutes = parameters.ScheduleInterval ?? 10; // Default to 10 minutes if null
                                                                         // Acuminator disable once PX1008 LongOperationDelegateSynchronousExecution [Justification]
                PXLongOperation.StartOperation(this, () => RunScheduledCheckIndependently(parameters.PartnerID, parameters.ApiKey, intervalMinutes, parameters.IsActive));
                PXTrace.WriteInformation($"Schedule started with interval: {intervalMinutes} minutes.");
            }
            else
            {
                PXTrace.WriteInformation("Schedule is inactive.");
            }

            return adapter.Get();
        }

        private void RunScheduledCheckIndependently(string partnerId, string apiKey, int intervalMinutes, bool? isActive)
        {
            while (isActive == true)
            {
                var graph = PXGraph.CreateInstance<SMSBalanceCheckMaint>();
                var parameters = new BalanceCheckParameters
                {
                    PartnerID = partnerId,
                    ApiKey = apiKey
                };
                graph.RequestView.Current = parameters;
                graph.checkBalance(new PXAdapter(graph.RequestView));
                System.Threading.Thread.Sleep(intervalMinutes * 60 * 1000); // Convert minutes to milliseconds
                // Note: isActive is not dynamically updated here; it reflects the initial value
            }
            PXTrace.WriteInformation("Schedule stopped due to deactivation.");
        }
    }
}