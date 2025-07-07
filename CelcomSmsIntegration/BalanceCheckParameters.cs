using System;
using CelcomSmsIntegration;
using PX.Data;
using PX.Data.BQL;

namespace CelcomAfrica.SmsProvider
{
    [Serializable]
    [PXCacheName("SMS Balance Check Parameters")]
    [PXPrimaryGraph(typeof(SMSBalanceCheckMaint))]
    public class BalanceCheckParameters : PXBqlTable, IBqlTable
    {
        #region ParameterID
        [PXDBInt(IsKey = true)]
        [PXDefault(1, PersistingCheck = PXPersistingCheck.Nothing)]
        [PXUIField(DisplayName = "Parameter ID", Visible = false)]
        public virtual int? ParameterID { get; set; }
        public abstract class parameterID : PX.Data.BQL.BqlInt.Field<parameterID> { }
        #endregion

        #region PartnerID
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Partner ID", Visibility = PXUIVisibility.SelectorVisible)]
        [PXDefault(PersistingCheck = PXPersistingCheck.NullOrBlank)]
        public virtual string PartnerID { get; set; }
        public abstract class partnerID : PX.Data.BQL.BqlString.Field<partnerID> { }
        #endregion

        #region ApiKey
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "API Key", Visibility = PXUIVisibility.SelectorVisible)]
        [PXDefault(PersistingCheck = PXPersistingCheck.NullOrBlank)]
        public virtual string ApiKey { get; set; }
        public abstract class apiKey : PX.Data.BQL.BqlString.Field<apiKey> { }
        #endregion
    }
}