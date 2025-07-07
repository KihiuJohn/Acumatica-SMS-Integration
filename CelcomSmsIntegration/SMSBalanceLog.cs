using System;
using PX.Data;
using PX.Data.BQL;

namespace CelcomAfrica.SmsProvider
{
    [Serializable]
    [PXCacheName("SMS Balance Log")]
    public class SMSBalanceLog : PXBqlTable, IBqlTable
    {
        #region BalanceLogID
        [PXDBIdentity(IsKey = true)]
        public virtual int? BalanceLogID { get; set; }
        public abstract class balanceLogID : PX.Data.BQL.BqlInt.Field<balanceLogID> { }
        #endregion

        #region PartnerID
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Partner ID")]
        public virtual string PartnerID { get; set; }
        public abstract class partnerID : PX.Data.BQL.BqlString.Field<partnerID> { }
        #endregion

        #region ResponseCode
        [PXDBInt]
        [PXUIField(DisplayName = "Response Code")]
        public virtual int? ResponseCode { get; set; }
        public abstract class responseCode : PX.Data.BQL.BqlInt.Field<responseCode> { }
        #endregion

        #region Credit
        [PXDBDecimal(5)]
        [PXUIField(DisplayName = "Credit")]
        public virtual decimal? Credit { get; set; }
        public abstract class credit : PX.Data.BQL.BqlDecimal.Field<credit> { }
        #endregion

        #region CreatedDateTime
        [PXDBDateAndTime]
        [PXUIField(DisplayName = "Created Date", Enabled = false)]
        public virtual DateTime? CreatedDateTime { get; set; }
        public abstract class createdDateTime : PX.Data.BQL.BqlDateTime.Field<createdDateTime> { }
        #endregion

        #region TStamp
        [PXDBTimestamp]
        public virtual byte[] TStamp { get; set; }
        public abstract class tStamp : PX.Data.BQL.BqlByteArray.Field<tStamp> { }
        #endregion
    }
}