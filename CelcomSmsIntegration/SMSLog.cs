using System;
using PX.Data;

namespace CelcomAfrica.SmsProvider
{
    [Serializable]
    [PXCacheName("SMS Log")]
    public class SMSLog : PXBqlTable, IBqlTable
    {
        #region SMSLogID
        [PXDBIdentity(IsKey = true)]
        [PXUIField(DisplayName = "SMS Log ID")]
        public int? SMSLogID { get; set; }
        public abstract class sMSLogID : PX.Data.BQL.BqlInt.Field<sMSLogID> { }
        #endregion

        #region Mobile
        [PXDBString(20)]
        [PXUIField(DisplayName = "Mobile")]
        public string Mobile { get; set; }
        public abstract class mobile : PX.Data.BQL.BqlString.Field<mobile> { }
        #endregion

        #region Message
        [PXDBString(255)]
        [PXUIField(DisplayName = "Message")]
        public string Message { get; set; }
        public abstract class message : PX.Data.BQL.BqlString.Field<message> { }
        #endregion

        #region ShortCode
        [PXDBString(50)]
        [PXUIField(DisplayName = "Short Code")]
        public string ShortCode { get; set; }
        public abstract class shortCode : PX.Data.BQL.BqlString.Field<shortCode> { }
        #endregion

        #region ResponseCode
        [PXDBInt]
        [PXUIField(DisplayName = "Response Code")]
        public int? ResponseCode { get; set; }
        public abstract class responseCode : PX.Data.BQL.BqlInt.Field<responseCode> { }
        #endregion

        #region ResponseDescription
        [PXDBString(255)]
        [PXUIField(DisplayName = "Response Description")]
        public string ResponseDescription { get; set; }
        public abstract class responseDescription : PX.Data.BQL.BqlString.Field<responseDescription> { }
        #endregion

        #region MessageID
        [PXDBString(50)]
        [PXUIField(DisplayName = "Message ID")]
        public string MessageID { get; set; }
        public abstract class messageID : PX.Data.BQL.BqlString.Field<messageID> { }
        #endregion

        #region NetworkID
        [PXDBString(20)]
        [PXUIField(DisplayName = "Network ID")]
        public string NetworkID { get; set; }
        public abstract class networkID : PX.Data.BQL.BqlString.Field<networkID> { }
        #endregion

        #region CreatedDateTime
        [PXDBCreatedDateTime]
        [PXUIField(DisplayName = "Created Date")]
        public DateTime? CreatedDateTime { get; set; }
        public abstract class createdDateTime : PX.Data.BQL.BqlDateTime.Field<createdDateTime> { }
        #endregion

        #region CreatedByID
        [PXDBCreatedByID]
        [PXUIField(DisplayName = "Created By")]
        public Guid? CreatedByID { get; set; }
        public abstract class createdByID : PX.Data.BQL.BqlGuid.Field<createdByID> { }
        #endregion
    }
}