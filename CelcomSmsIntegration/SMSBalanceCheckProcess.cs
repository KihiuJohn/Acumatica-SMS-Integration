using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PX.Data;
using PX.Data.BQL.Fluent;
using CelcomAfrica.SmsProvider;
using System.Collections.Generic;

namespace CelcomSmsIntegration
{
    public class SMSBalanceCheckProcess : PXGraph<SMSBalanceCheckProcess>
    {
        public PXSave<BalanceCheckParameters> Save;
        public PXCancel<BalanceCheckParameters> Cancel;
        public PXProcessing<BalanceCheckParameters, Where<BalanceCheckParameters.parameterID, Equal<Required<BalanceCheckParameters.parameterID>>>> RequestView;

        public SMSBalanceCheckProcess()
        {
            // Force a fresh database query for the record with ParameterID = 1
            RequestView.Cache.Clear();
            BalanceCheckParameters parameters = RequestView.SelectSingle(1);
            if (parameters == null)
            {
                parameters = new BalanceCheckParameters
                {
                    ParameterID = 1,
                    PartnerID = "",
                    ApiKey = ""
                };
                PXTrace.WriteInformation("Default record set in memory: ParameterID=1, PartnerID=, ApiKey=");
            }
            else
            {
                PXTrace.WriteInformation($"Fetched record: ParameterID={parameters.ParameterID}, PartnerID={parameters.PartnerID}, ApiKey={parameters.ApiKey}");
            }

            if (parameters != null)
            {
                RequestView.Current = parameters;
                RequestView.Cache.IsDirty = false;
            }
            else
            {
                PXTrace.WriteInformation("Failed to set current record due to null parameters.");
            }
        }

        public PXAction<BalanceCheckParameters> Process;
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Process", MapEnableRights = PXCacheRights.Update, MapViewRights = PXCacheRights.Select)]
        protected virtual IEnumerable process(PXAdapter adapter)
        {
            PXTrace.WriteInformation("Process action triggered.");
            BalanceCheckParameters current = RequestView.Current;
            if (current == null)
            {
                // Acuminator disable once PX1050 HardcodedStringInLocalizationMethod [Justification]
                throw new PXException("No parameters record found.");
            }

            if (string.IsNullOrEmpty(current.PartnerID) || string.IsNullOrEmpty(current.ApiKey))
            {
                // Acuminator disable once PX1051 NonLocalizableString [Justification]
                throw new PXException(CelcomAfrica.SmsProvider.Messages.MissingCredentials);
            }

            RequestView.SetProcessDelegate(delegate (List<BalanceCheckParameters> list)
            {
                var graph = PXGraph.CreateInstance<SMSBalanceCheckProcess>();
                foreach (var item in list)
                {
                    PXTrace.WriteInformation($"Processing item: ParameterID={item.ParameterID}, PartnerID={item.PartnerID}, ApiKey={item.ApiKey}");
                    if (graph.RequestView.SelectSingle(1) == null)
                    {
                        var newParams = new BalanceCheckParameters
                        {
                            ParameterID = 1,
                            PartnerID = item.PartnerID,
                            ApiKey = item.ApiKey
                        };
                        graph.RequestView.Insert(newParams);
                        graph.RequestView.Cache.Persist(PXDBOperation.Insert);
                        PXTrace.WriteInformation($"Default record inserted and persisted: ParameterID=1, PartnerID={item.PartnerID}, ApiKey={item.ApiKey}");
                    }
                    graph.ProcessBalanceCheck(item);
                }
            });
            return adapter.Get();
        }

        public virtual void ProcessBalanceCheck(BalanceCheckParameters parameters)
        {
            if (parameters == null)
            {
                // Acuminator disable once PX1050 HardcodedStringInLocalizationMethod [Justification]
                throw new PXException("No parameters record found.");
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

                    var graph = PXGraph.CreateInstance<SMSBalanceCheckMaint>();
                    var logEntry = (SMSBalanceLog)graph.BalanceLogs.Insert(new SMSBalanceLog
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

                    graph.Persist();
                    PXProcessing.SetInfo(0, "Balance check processed successfully.");
                }
            }
            catch (HttpRequestException ex)
            {
                var graph = PXGraph.CreateInstance<SMSBalanceCheckMaint>();
                graph.BalanceLogs.Insert(new SMSBalanceLog
                {
                    PartnerID = parameters.PartnerID,
                    ResponseCode = 901,
                    CreatedDateTime = DateTime.UtcNow
                });
                graph.Persist();
                PXProcessing.SetError(0, $"Network error: {ex.Message}");
                // Acuminator disable once PX1051 NonLocalizableString [Justification]
                throw new PXException(CelcomAfrica.SmsProvider.Messages.BalanceException, ex.Message);
            }
            catch (JsonException jsonEx)
            {
                var graph = PXGraph.CreateInstance<SMSBalanceCheckMaint>();
                graph.BalanceLogs.Insert(new SMSBalanceLog
                {
                    PartnerID = parameters.PartnerID,
                    ResponseCode = 903,
                    CreatedDateTime = DateTime.UtcNow
                });
                graph.Persist();
                PXProcessing.SetError(0, $"Parsing error: {jsonEx.Message}");
                // Acuminator disable once PX1051 NonLocalizableString [Justification]
                throw new PXException(CelcomAfrica.SmsProvider.Messages.BalanceParsingFailed, jsonEx.Message);
            }
        }
    }
}