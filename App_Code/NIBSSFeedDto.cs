using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// Summary description for NIBSSFeedDto
/// </summary>
public class NIBSSFeedDto
{
    public DateTime TransactionTime { get; set; }
    public string Pan { get; set; }
    public string TerminalID { get; set; }
    public string AccountNo { get; set; }
    public double TranAmount { get; set; }
    public string TranAmountSignal { get; set; }
    public DateTime FeedTimeStamp { get; set; }
    public DateTime TransmissionTime { get; set; }
    public string MessageReq { get; set; }
    public string MessageRes { get; set; }
    public string ClearingPeriod { get; set; }
    public string TransactionID { get; set; }
    public string StanNrReq { get; set; }
    public string StanNrRes { get; set; }
    public string RRNReq { get; set; }
    public string RRNRes { get; set; }
    public string CardAcceptorNameLoc { get; set; }
    public string ResponseCode { get; set; }
    public double Surcharge { get; set; }
    public string SurchargeSignal { get; set; }
    public string AcqInstitutionID { get; set; }
    public string PosDataCode { get; set; }
    public int CardExpiry { get; set; }
    public string MerchantCatCode { get; set; }
    public string TerminalType { get; set; }
    public int CurrencyCode { get; set; }
    public string BankCode { get; set; }
    public string FwdInstitutionID { get; set; }
    public string MerchantName { get; set; }
    public string MerchantAddress { get; set; }
    public string MerchantPhoneNo { get; set; }
    public string MerchantAcctNo { get; set; }
    public string TransactionFee { get; set; }
    public string SettledAmount { get; set; }
}