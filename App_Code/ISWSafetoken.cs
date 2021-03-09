using log4net;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using RestSharp;
using System.Configuration;
using System.Text;
using System.Xml;
using System.Data.SqlClient;
using System.Data;
using System.Xml.Serialization;
using System.IO;
using RestSharp.Serialization;
using RestSharp.Serialization.Xml;

/// <summary>
/// Summary description for ISWSafetoken
/// </summary>
public class ISWSafetoken
{
    ILog logger = LogManager.GetLogger("CardServicesLog");
    public ISWSafetoken()
    {
        //
        // TODO: Add constructor logic here
        //
    }

    public TokenResult GetToken()
    {
        TokenResult token = new TokenResult();
        try
        {
            string endpoint = ConfigurationManager.AppSettings["tokenendpoint"];
            string rootUrl = ConfigurationManager.AppSettings["baseUrl"];
            string iswCustID = ConfigurationManager.AppSettings["clientID"];
            string iswKey = ConfigurationManager.AppSettings["secret"];
            string fullUri = rootUrl + "/" + endpoint;
            string custID = iswCustID;
            string key = iswKey;
			logger.Error("Full token uri is: " + fullUri + "CustID used is: " + custID + "key used is: " + key);
            var client = new RestClient(fullUri);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(custID + ":" + key)));

            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("scope", "profile");
            IRestResponse response = client.Execute(request);
            token = string.IsNullOrEmpty(response.Content) ? null : JsonConvert.DeserializeObject<TokenResult>(response.Content);
            logger.Error("Token response gotten is: " + JsonConvert.SerializeObject(token));
        }
        catch (Exception ex)
        {
            logger.Error("Exception in method GetToken with error: " + ex + ". Token response gotten is: " + JsonConvert.SerializeObject(token));
        }
        return token;
    }
 
    public string SendSafetoken(string token, SafetokenEntries input)
    {
        string content = string.Empty;
        try
        {
            string endpoint = ConfigurationManager.AppSettings["sTokenendpoint"];
            string rootUrl = ConfigurationManager.AppSettings["sTbaseUrl"];
            string fullUri = rootUrl + "/" + endpoint;
            var client = new RestClient(fullUri);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "text/xml");
            request.AddHeader("SOAPAction", "AddCardHolder");
            request.AddHeader("Authorization", "Bearer " + token);
            
            string value = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ser=\"http://services.interswitchng.com/\" xmlns:tec=\"http://schemas.datacontract.org/2004/07/TechQuest.Framework.ServiceFramework.Contra ct\" xmlns:tec1=\"techquest.interswitchng.com\">\r\n    <soapenv:Header/>\r\n    <soapenv:Body>\r\n        <ser:AddCardHolder>\r\n            <!--Optional:-->\r\n            <ser:request>\r\n                <!--Optional:-->\r\n                <tec:HeaderTerminalId></tec:HeaderTerminalId>\r\n                <!--Optional:-->\r\n                <tec:TerminalId></tec:TerminalId>\r\n                <tec1:AccountNumber></tec1:AccountNumber>\r\n                <!--Optional:-->\r\n                <tec1:Address1></tec1:Address1>\r\n                <!--Optional:-->\r\n                <tec1:Address2></tec1:Address2>\r\n                <tec1:BankId></tec1:BankId>\r\n                <tec1:CardPan></tec1:CardPan>\r\n                <tec1:City></tec1:City>\r\n                <!--Optional:-->\r\n                <tec1:Country></tec1:Country>\r\n                <!--Optional:-->\r\n                <tec1:CountryCode></tec1:CountryCode>\r\n                <!--Optional:-->\r\n                <tec1:Email></tec1:Email>\r\n                <!--Optional:-->\r\n                <tec1:ExpiryDate></tec1:ExpiryDate>\r\n                <!--Optional:-->\r\n                <tec1:FailureReason></tec1:FailureReason>\r\n                <!--Optional:-->\r\n                <tec1:Gender></tec1:Gender>\r\n                <!--Optional:-->\r\n                <tec1:IsApprovedAndEnabled></tec1:IsApprovedAndEnabled>\r\n                <!--Optional:-->\r\n                <tec1:IsFailure></tec1:IsFailure>\r\n                <!--Optional:-->\r\n                <tec1:IsVerveEAccount></tec1:IsVerveEAccount>\r\n                <!--Optional:-->\r\n                <tec1:LastName></tec1:LastName>\r\n                <!--Optional:-->\r\n                <tec1:Othernames></tec1:Othernames>\r\n                <!--Optional:-->\r\n                <tec1:PostCode></tec1:PostCode>\r\n                <!--Optional:-->\r\n                <tec1:PrimaryMobileCountryCode></tec1:PrimaryMobileCountryCode>\r\n                <tec1:PrimaryMobileNumber></tec1:PrimaryMobileNumber>\r\n                <!--Optional:-->\r\n                <tec1:RegistrationChannel></tec1:RegistrationChannel>\r\n                <!--Optional:-->\r\n                <tec1:SecondaryMobileNumber></tec1:SecondaryMobileNumber>\r\n                <!--Optional:-->\r\n                <tec1:State></tec1:State>\r\n                <!--Optional:-->\r\n                <tec1:Title></tec1:Title>\r\n            </ser:request>\r\n        </ser:AddCardHolder>\r\n    </soapenv:Body>\r\n</soapenv:Envelope>";
			
            string xmlContent = value;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlContent);
            XmlNamespaceManager namespaces = new XmlNamespaceManager(doc.NameTable);
            namespaces.AddNamespace("soapenv", @"http://schemas.xmlsoap.org/soap/envelope/");
            namespaces.AddNamespace("ser", @"http://services.interswitchng.com/");
            namespaces.AddNamespace("tec", @"http://schemas.datacontract.org/2004/07/TechQuest.Framework.ServiceFramework.Contra ct");
            namespaces.AddNamespace("tec1", @"techquest.interswitchng.com");

            XmlNodeList parentNode = doc.GetElementsByTagName("ser:request");
            int count = parentNode.Count;
            foreach (XmlNode childNode in parentNode)
            {
                childNode.SelectSingleNode("tec:HeaderTerminalId", namespaces).InnerText = input.headerTerminalId;
                childNode.SelectSingleNode("tec:TerminalId", namespaces).InnerText = input.terminalId;
                childNode.SelectSingleNode("tec1:AccountNumber", namespaces).InnerText = input.accountNumber;
                childNode.SelectSingleNode("tec1:Address1", namespaces).InnerText = input.address1;
                childNode.SelectSingleNode("tec1:Address2", namespaces).InnerText = input.address2;
                childNode.SelectSingleNode("tec1:BankId", namespaces).InnerText = input.bankId.ToString();
                childNode.SelectSingleNode("tec1:CardPan", namespaces).InnerText = input.cardPan.ToString();
                childNode.SelectSingleNode("tec1:City", namespaces).InnerText = input.city;
                childNode.SelectSingleNode("tec1:Country", namespaces).InnerText = input.country;
                childNode.SelectSingleNode("tec1:CountryCode", namespaces).InnerText = input.countryCode.ToString();
                childNode.SelectSingleNode("tec1:Email", namespaces).InnerText = input.email.ToString();
                childNode.SelectSingleNode("tec1:ExpiryDate", namespaces).InnerText = input.expiryDate.ToString();
                childNode.SelectSingleNode("tec1:FailureReason", namespaces).InnerText = input.failureReason.ToString();
                childNode.SelectSingleNode("tec1:Gender", namespaces).InnerText = input.gender;
                childNode.SelectSingleNode("tec1:IsApprovedAndEnabled", namespaces).InnerText = input.isApprovedAndEnabled.ToString().ToLower();
                childNode.SelectSingleNode("tec1:IsFailure", namespaces).InnerText = input.isFailure.ToString().ToLower();
                childNode.SelectSingleNode("tec1:IsVerveEAccount", namespaces).InnerText = input.isVerveEAccount.ToString().ToLower();
                childNode.SelectSingleNode("tec1:LastName", namespaces).InnerText = input.lastName;
                childNode.SelectSingleNode("tec1:Othernames", namespaces).InnerText = input.othernames;
                childNode.SelectSingleNode("tec1:PostCode", namespaces).InnerText = input.postCode.ToString();
                childNode.SelectSingleNode("tec1:PrimaryMobileCountryCode", namespaces).InnerText = input.primaryMobileCountryCode;
                childNode.SelectSingleNode("tec1:PrimaryMobileNumber", namespaces).InnerText = input.primaryMobileNumber.ToString();
                childNode.SelectSingleNode("tec1:RegistrationChannel", namespaces).InnerText = input.registrationChannel.ToString();
                childNode.SelectSingleNode("tec1:SecondaryMobileNumber", namespaces).InnerText = input.secondaryMobileNumber.ToString();
                childNode.SelectSingleNode("tec1:State", namespaces).InnerText = input.state;
                childNode.SelectSingleNode("tec1:Title", namespaces).InnerText = input.title;
            }

            string req = doc.InnerXml;

			logger.Error("XML sent is: " + req);
            request.AddParameter("text/xml", req, ParameterType.RequestBody);
            System.Net.ServicePointManager.Expect100Continue = false;
            IRestResponse response = client.Execute(request);
            XmlAttributeDeserializer deserializer = new XmlAttributeDeserializer();
            string respContent = response.Content;
            logger.Error("response content is: " + respContent);
			if (response.StatusCode == HttpStatusCode.Unauthorized) 
            {
                content = "06";
            }
            else
            {
                var result = deserializer.Deserialize<Envelope>(response);
                content = result.Body.AddCardHolderResponse.AddCardHolderResult.ResponseCode.ToString();
            }
        }
        catch (Exception ex)
        {
            logger.Error("Exception in method SendSafetoken with error: " + ex + ". Content response sent is 91000 for record: " + JsonConvert.SerializeObject(input));
            content = "";
        }
        return content;
    }
    public void UpdateTokenConfig(ConfigData input)
    {
        int row = 0;
        try
        {
            var query = "UPDATE dbo.SafeTokenConfig set IsDeleted = " + input.IsDeleted + ", DateUpdated = '" + input.DateUpdated.ToString("yyyy-MM-dd HH:mm:ss.fff") + "' where IsDeleted = 0";
            var _configuration = ConfigurationManager.AppSettings["config"];
            using (SqlConnection connect = new SqlConnection(_configuration))
            {
                using (SqlCommand cmd = new SqlCommand(query, connect))
                {
                    if (connect.State != ConnectionState.Open)
                    {
                        connect.Open();
                    }
                    cmd.CommandType = System.Data.CommandType.Text;
                    row = cmd.ExecuteNonQuery();
                    connect.Dispose();
                    connect.Close();
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error("Exception at method UpdateTokenConfig: " + ex);
        }
    }
    public void InsertTokenConfig(ConfigData input)
    {
        try
        {
            logger.Error("token data sent to insert is: " + input);
            var query = "Insert into SafeTokenConfig values (@Token,@DateUpdated,@IsDeleted);";
            var _configuration = ConfigurationManager.AppSettings["config"];
            using (SqlConnection connect = new SqlConnection(_configuration))
            {
                using (SqlCommand cmd = new SqlCommand(query, connect))
                {
                    if (connect.State != ConnectionState.Open)
                    {
                        connect.Open();
                    }
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddWithValue("@Token", input.Token);
                    cmd.Parameters.AddWithValue("@DateUpdated", input.DateUpdated.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    cmd.Parameters.AddWithValue("@IsDeleted", input.IsDeleted);
                    cmd.ExecuteNonQuery();
                    connect.Dispose();
                    connect.Close();
                }
            }

        }
        catch (Exception ex)
        {
            logger.Error("Exception at method InsertTokenConfig: " + ex);
        }
    }
    public string GetTokenConfig()
    {
        string token = string.Empty;
        string query = "select Token FROM SafeTokenConfig where IsDeleted = 0";
        SqlDataReader sdr;
        int count;
        try
        {
            var _configuration = ConfigurationManager.AppSettings["config"];
            using (SqlConnection connect = new SqlConnection(_configuration))
            {
                using (SqlCommand cmd = new SqlCommand(query, connect))
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    if (connect.State != ConnectionState.Open)
                    {
                        connect.Open();
                    }
                    sdr = cmd.ExecuteReader();
                    count = sdr.FieldCount;
                    while (sdr.Read())
                    {
                        token = sdr["Token"].ToString();
                    }
                    cmd.Dispose();
                }
                connect.Dispose();
                connect.Close();
            }
        }
        catch (Exception ex)
        {
            logger.Error("Exception at method GetTokenConfig: " + ex);
        }
        return token;
    }
    public string splitEmails(string data)
    {
        string[] dirtyChars = { ",", "\'", "\"", "-", "_", "*", "\\", "?", ":", ";", "+", "'", "&", "`" };

        foreach (string c in dirtyChars)
        {
            data = data.Replace(c, "");
        }
        return data;
    }
    public string cleanData(string data)
    {
        string[] dirtyChars = { ",", "\'", "\"", "-", "_", "*", "\\", "?", ":", ";", "+", "'", "&", "`" };

        foreach (string c in dirtyChars)
        {
            data = data.Replace(c, "");
        }
        return data;
    }
}
public class ConfigData
{
    public string Token { get; set; }
    public string IsDeleted { get; set; }
    public DateTime DateUpdated { get; set; }
}
public class SafetokenDet
{
    public string accountNumber;

    public string address1;

    public string address2;

    public ulong cardPan;

    public string email;

    public ushort expiryDate;

    public string lastName;

    public ulong primaryMobileNumber;

    public string surname;

    public string title;
}
public class SafetokenEntries
{
    public string headerTerminalId;

    public string terminalId;

    public string accountNumber;

    public string address1;

    public string address2;

    public byte bankId;

    public ulong cardPan;

    public string city;

    public string country;

    public string countryCode;

    public object email;

    public ushort expiryDate;

    public byte failureReason;

    public string gender;

    public bool isApprovedAndEnabled;

    public bool isFailure;

    public bool isVerveEAccount;

    public string lastName;

    public string othernames;

    public byte postCode;

    public string primaryMobileCountryCode;

    public ulong primaryMobileNumber;

    public byte registrationChannel;

    public ulong secondaryMobileNumber;

    public string state;

    public string title;
}
public class TokenResult
{
    public string access_token { get; set; }
    public string token_type { get; set; }
    public int expires_in { get; set; }
    public string scope { get; set; }
    public string merchant_code { get; set; }
    public string production_payment_code { get; set; }
    public int productId { get; set; }
    public string requestor_id { get; set; }
    public string[] api_resources { get; set; }
    public object client_logo { get; set; }
    public string payable_id { get; set; }
    public object client_description { get; set; }
    public string payment_code { get; set; }
    public string jti { get; set; }
}