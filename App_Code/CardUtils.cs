using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using log4net;
using Sterling.MSSQL;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System.Data.SqlClient;
using Newtonsoft.Json;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Summary description for Utils
/// </summary>
public class CardUtils
{
    ILog logger = LogManager.GetLogger("CardServicesLog");
    eacbsWebService.banksSoapClient eacbs = new eacbsWebService.banksSoapClient();
    CardSecurityUtils p = new CardSecurityUtils();
    public CardUtils()
    {
        //
        // TODO: Add constructor logic here
        //
    }
    //Get the Postcard Issuer Number based on the IIN (First 6 digits of the PAN)
    public string GetIssuer(string iin)
    {
        string _issuer = "1";

        switch (iin)
        {
            case "506107":
                _issuer = "1";
                break;
            case "533477":
                _issuer = "1";
                break;
            case "400890":
                _issuer = "3";
                break;
            case "400845":
                _issuer = "4";
                break;
            case "525495":
                _issuer = "6";
                break;
            case "403753":
                _issuer = "3";
                break;
            case "515872":
                _issuer = "5";
                break;
            case "522066":
                _issuer = "5";
                break;
        }

        return _issuer;
    }

    public string EnrolSafetokenProd(SafetokenDet safetokenDet)
    {
        ISWSafetoken iSWSafetoken = new ISWSafetoken();
        string response;
        SafetokenEntries computeEntry = new SafetokenEntries();
        try
        {
            string token = iSWSafetoken.GetTokenConfig();
			logger.Error("token received from DB is" + token);
            string headTerminalID = ConfigurationManager.AppSettings["terminalID"].ToString();
            
            computeEntry = new SafetokenEntries()
            {
                accountNumber = safetokenDet.accountNumber.ToString().Trim(),
                address1 = safetokenDet.address1.ToString().Trim(),
                address2 = safetokenDet.address2.ToString().Trim(),
                bankId = 8,
                cardPan = Convert.ToUInt64(safetokenDet.cardPan),
                city = "",
                country = "NG",
                countryCode = "NG",
                email = !string.IsNullOrEmpty(safetokenDet.email) ? iSWSafetoken.splitEmails(safetokenDet.email).Trim() : safetokenDet.email,
                expiryDate = Convert.ToUInt16(safetokenDet.expiryDate),
                failureReason = 0,
                gender = "",
                headerTerminalId = headTerminalID,
                isApprovedAndEnabled = true,
                isFailure = false,
                isVerveEAccount = false,
                lastName = iSWSafetoken.cleanData(safetokenDet.surname.ToString()).Trim(),
                othernames = "",
                postCode = 1,
                primaryMobileCountryCode = "NG",
                primaryMobileNumber = Convert.ToUInt64(safetokenDet.primaryMobileNumber.ToString()),
                registrationChannel = 1,
                secondaryMobileNumber = Convert.ToUInt64(safetokenDet.primaryMobileNumber.ToString()),
                state = "",
                terminalId = headTerminalID,
                title = string.IsNullOrEmpty(safetokenDet.title) ? "Mr" : iSWSafetoken.cleanData(safetokenDet.title).Trim()
            };
            //generate the Safetoken file
            logger.Error("Customer Record sent for upload: " + computeEntry);
            string resp = iSWSafetoken.SendSafetoken(token, computeEntry);
			logger.Error("SendSafetoken response is: " + resp);
            if (resp == "90000")
            {
                response = "00 - Successful";
                logger.Error("Record: " + JsonConvert.SerializeObject(computeEntry) + " has been successfully sent");
            }
            else if (resp == "10002")
            {
                response = "09 - Customer Data Incorrect";
                logger.Error("Record: " + JsonConvert.SerializeObject(computeEntry) + " failed with 09 - Customer Data Incorrect");
            }
            else { response = "99"; logger.Error("Record: " + JsonConvert.SerializeObject(computeEntry) + " failed/returned response:" + resp); }

            //resp == "90000" ? logger.Error($"Record: {JsonConvert.SerializeObject(computeEntry)} has been successfully sent") : logger.Error($"Record: {JsonConvert.SerializeObject(computeEntry)} failed/returned response {resp}");
            //token = resp == string.IsNullOrEmpty(resp) ? 
			//resp = "06";
            if (resp == "06")
            {
                //get new token
                var getToken = iSWSafetoken.GetToken();
                if(getToken != null)
                {
                    token = getToken.access_token;
					logger.Error("token gotten form ISW gettoken is" + token);
                    ConfigData updateInput = new ConfigData()
                    {
                        DateUpdated = DateTime.Now,
                        IsDeleted = "1"
                    };
                    iSWSafetoken.UpdateTokenConfig(updateInput);
                    //insert new token
                    ConfigData insertInput = new ConfigData()
                    {
                        DateUpdated = DateTime.Now,
                        Token = token,
                        IsDeleted = "0"
                    };
                    iSWSafetoken.InsertTokenConfig(insertInput);
                    resp = iSWSafetoken.SendSafetoken(token, computeEntry);
                    if (resp == "90000")
                    {
                        response = "00 - Successful";
                        logger.Error("Record: " + JsonConvert.SerializeObject(computeEntry) + " has been successfully sent");
                    }
                    else if (resp == "10002")
                    {
                        response = "09 - Customer Data Incorrect";
                        logger.Error("Record: " + JsonConvert.SerializeObject(computeEntry) + " failed with 09 - Customer Data Incorrect");
                    }
                    else { response = "99"; logger.Error("Record: " + JsonConvert.SerializeObject(computeEntry) + " failed/returned response:" + resp); }
                }
                else
                {
                    response = "99";
                    logger.Error("Unable to generate token from method GetToken");
                }
                //token = iSWSafetoken.GetToken().access_token;
                //update current token
            }
        }
        catch(Exception ex)
        {
            logger.Error("Record: " + JsonConvert.SerializeObject(computeEntry) + " threw exception " + ex);
            response = "99";
        }
        return response;
    }

    //Get Active Card List
    public List<CardDetail> GetActiveCardList(string account)
    {
        var cardData = new List<CardDetail>();
        string exp = DateTime.Now.ToString("yyMM");

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.customer_id FROM pc_cards c INNER JOIN pc_card_accounts a ON c.pan = a.pan AND c.seq_nr = a.seq_nr INNER JOIN pc_card_programs p ON c.card_program = p.card_program " +
                         "WHERE account_id = @account AND a.date_deleted IS NULL AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@account", account);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {

                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string customer_id = dr["customer_id"].ToString();
                        string cvv2 = string.Empty;
                        var cvk = GetCVV2Key(pan.Trim(), expiry_date.Trim());
                        cvv2 = p.GenerateCVV2(pan, expiry_date.Trim(), cvk);
                        var cardDetail = GetCardDetail(pan, expiry_date);
                        if (cardDetail != null)
                        {
                            cardData.Add(new CardDetail() { PAN = pan, ExpiryDate = expiry_date, CustomerId = customer_id, CardProvider = cardDetail.CardProvider, CardName = cardDetail.CardName, CardStatus = cardDetail.CardStatus, BlockStatus = cardDetail.BlockStatus, Cvv2 = cvv2 });
                        }

                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }

    //GetFarePayCardByCustomerID
    public string GetFareCardsByID(string custID)
    {
        var fareCard = string.Empty;
        try
        {
            string sql = "select pan,customer_id from pc_cards where customer_id like '%" + custID + "' and left(pan,8) in ('53347706','43461606')";

            SqlDataReader sdr;
            var _configuration = ConfigurationManager.AppSettings["PostCard"];
            using (SqlConnection connect = new SqlConnection(_configuration))
            {
                using (SqlCommand cmd = new SqlCommand(sql, connect))
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandTimeout = 2000;
                    if (connect.State != ConnectionState.Open)
                    {
                        connect.Open();
                    }
                    sdr = cmd.ExecuteReader();
                    while (sdr.Read())
                    {
                        fareCard += sdr["pan"].ToString();
                        fareCard += "|";
                        fareCard += sdr["customer_id"].ToString();
                    }
                    cmd.Dispose();
                }
                connect.Dispose();
                connect.Close();
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return fareCard;
    }

    public CardDetail GetCardDetail(string pan, string exp)
    {
        var cardDetails = new CardDetail();
        try
        {
            string issuer = GetCardIssuer(pan, exp);// GetIssuer(pan.Substring(0, 6));

            string sql = "SELECT s.c1_name_on_card,CASE WHEN c.card_status IN ('1') THEN 'ACTIVE' ELSE 'INACTIVE' END AS card_status,c.expiry_date, " +
                "CASE WHEN c.hold_rsp_code IS NULL THEN 'NOT BLOCKED' ELSE 'BLOCKED' END AS BlockStat," +
                "CASE WHEN c.card_program LIKE '%MASTER%' THEN 'MASTERCARD' WHEN c.card_program LIKE '%VISA%' THEN 'VISA' ELSE 'VERVE' END AS CardType " +
                "FROM pc_cards_" + issuer + "_A c INNER JOIN pc_customers_" + issuer + "_A s ON c.customer_id = s.customer_id " +
                "WHERE c.PAN = @pan AND expiry_date = @exp ";

            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws != 0)
                {

                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string name = dr["c1_name_on_card"].ToString();
                        string stat = dr["card_status"].ToString();
                        string hold = dr["BlockStat"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string cardType = dr["CardType"].ToString();



                        cardDetails.CardName = name;
                        cardDetails.CardStatus = stat;
                        cardDetails.BlockStatus = hold;
                        cardDetails.ExpiryDate = expiry_date;
                        cardDetails.CardProvider = cardType;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cardDetails;
    }
    //Get All Card List
    public List<CardDetail> GetAllCardList(string account)
    {
        var cardData = new List<CardDetail>();
        string exp = DateTime.Now.ToString("yyMM");

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.customer_id FROM pc_cards c INNER JOIN pc_card_accounts a ON c.pan = a.pan AND c.seq_nr = a.seq_nr INNER JOIN pc_card_programs p ON c.card_program = p.card_program " +
                         "WHERE account_id = @account AND a.date_deleted IS NULL  AND expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@account", account);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {

                    for (int i = 0; i < rws; i++)
                    {
                        string cvv2 = string.Empty;
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string customer_id = dr["customer_id"].ToString();
                        var cvk = GetCVV2Key(pan.Trim(), expiry_date.Trim());
                        cvv2 = p.GenerateCVV2(pan, expiry_date.Trim(), cvk);
                        var cardDetail = GetCardDetail(pan, expiry_date);
                        if (cardDetail != null)
                        {
                            cardData.Add(new CardDetail() { PAN = pan, ExpiryDate = expiry_date, CustomerId = customer_id, CardProvider = cardDetail.CardProvider, CardName = cardDetail.CardName, CardStatus = cardDetail.CardStatus, BlockStatus = cardDetail.BlockStatus, Cvv2 = cvv2 });
                        }

                    }


                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }

    public string GetCardSeqNr(string pan, string exp)
    {
        string seq_nr = string.Empty;

        string sql = "SELECT MAX(seq_nr) as seq_nr FROM pc_cards WHERE pan = @pan AND expiry_date = @exp";
        Connect cn = new Connect("PostCard")
        {
            Persist = true
        };
        cn.SetSQL(sql);
        cn.AddParam("@pan", pan);
        cn.AddParam("@exp", exp);
        DataSet ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        if (hasTables)
        {
            int rws = ds.Tables[0].Rows.Count;

            if (rws > 0)
            {
                DataRow dr = ds.Tables[0].Rows[0];
                seq_nr = dr["seq_nr"].ToString();
            }
        }
        return seq_nr;
    }
    //Get the Postcard Issuer Number based on the pan and exp
    public string GetCardIssuer(string pan, string exp)
    {
        string _issuer = "1";
		if(pan.Substring(0,8) == "52206650")
		{
			_issuer = "9";
		}
		else
		{
			string sql = "SELECT issuer_nr FROM pc_cards WHERE pan = @pan AND expiry_date = @exp";
			Connect cn = new Connect("PostCard")
			{
				Persist = true
			};
			cn.SetSQL(sql);
			cn.AddParam("@pan", pan);
			cn.AddParam("@exp", exp);
			DataSet ds = cn.Select();
			cn.CloseAll();

			bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
			if (hasTables)
			{
				int rws = ds.Tables[0].Rows.Count;

				if (rws > 0)
				{
					DataRow dr = ds.Tables[0].Rows[0];
					_issuer = dr["issuer_nr"].ToString();

					if (_issuer == "")
					{
						_issuer = "1";
					}
				}
			}
		}

        return _issuer;
    }
    //Get the Postcard Issuer Number based on the pan and seq and exp
    public string GetCardIssuer(string pan, string seq, string exp)
    {
        string _issuer = "1";
		if(pan.Substring(0,8) == "52206650")
		{
			_issuer = "9";
		}
		else if(pan.Substring(0,8) == "53347751" || pan.Substring(0,8) == "53347750" || pan.Substring(0,6) == "506162")
		{
			_issuer = "11";
		}
		else
		{
			string sql = "SELECT issuer_nr FROM pc_cards WHERE pan = @pan AND seq_nr = @seq AND expiry_date = @exp";
			Connect cn = new Connect("PostCard")
			{
				Persist = true
			};
			cn.SetSQL(sql);
			cn.AddParam("@pan", pan);
			cn.AddParam("@exp", exp);
			cn.AddParam("@seq", seq);
			DataSet ds = cn.Select();
			cn.CloseAll();

			bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
			if (hasTables)
			{
				int rws = ds.Tables[0].Rows.Count;

				if (rws > 0)
				{
					DataRow dr = ds.Tables[0].Rows[0];
					_issuer = dr["issuer_nr"].ToString();

					if (_issuer == "")
					{
						_issuer = "1";
					}
				}
			}
		}

        return _issuer;
    }
    //Generate random 4 digits as PIN
    public int GenerateRandomPIN()
    {
        int _min = 1000;
        int _max = 9999;
        Random _rdm = new Random();
        return _rdm.Next(_min, _max);
    }
    
    //Get the extension of a file
    public string FileExtension(string file)
    {
        var ext = string.Empty;

        try
        {
            ext = Path.GetExtension(file);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        return ext;
    }
    //Get the KEY value based on the Key Name
    public string GetCryptoKeys(string keyName)
    {
        string ky = "";

        try
        {
            string sql = "SELECT val_under_ksk FROM crypto_dea1_keys WHERE name = @kyName";
            Connect cn = new Connect("Realtime")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@kyName", keyName);
            DataSet ds = cn.Select();
            cn.CloseAll();

            if (ds != null)
            {
                if (ds.Tables[0].Rows.Count != 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    ky = dr["val_under_ksk"].ToString();
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return ky;
    }
    //Get CVV2 Key
    public string GetCVV2Key(string pan, string exp)
    {
        var cvv2Key = string.Empty;

        try
        {
            string issuer = GetCardIssuer(pan, exp);// GetIssuer(pan.Substring(0, 6));

            string sql = "SELECT p.cvv2_key FROM pc_cards_" + issuer + "_A c INNER JOIN pc_card_programs p ON c.card_program = p.card_program " +
                         "WHERE PAN = @pan AND expiry_date = @exp";
            Connect cn = new Connect("PostCard")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    cvv2Key = GetCryptoKeys(dr["cvv2_key"].ToString());
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cvv2Key;
    }
    //Get CVV2 Key
    public string[] GetCVVData(string pan, string exp)
    {
        string[] cvvDet = new string[2];

        try
        {
            string issuer = GetCardIssuer(pan, exp);// GetIssuer(pan.Substring(0, 6));

            string sql = "SELECT p.cvv2_key,p.service_code FROM pc_cards_" + issuer + "_A c INNER JOIN pc_card_programs p ON c.card_program = p.card_program " +
                         "WHERE PAN = @pan AND expiry_date = @exp";
            Connect cn = new Connect("PostCard")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                if (ds.Tables[0].Rows.Count > 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    cvvDet[0] = GetCryptoKeys((dr["cvv2_key"].ToString()).Trim());
                    cvvDet[1] = (dr["service_code"].ToString()).Trim();
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cvvDet;
    }
    //Get card details
    public string[] GetCardInfo(string pan,string exp)
    {
        var cardInfo = new string[3];

        try
        {
           string sql = @"SELECT TOP 1 c.pan,c.expiry_date,a.account_id FROM pc_cards c INNER JOIN pc_card_accounts a ON c.pan = a.pan AND c.seq_nr = a.seq_nr
                            WHERE c.card_status = 1 AND a.account_type_qualifier = 1 AND c.pan = @pan AND c.expiry_date >= @exp AND account_type_nominated = default_account_type
                            ORDER BY c.expiry_date,c.seq_nr DESC";

            Connect cn = new Connect("PostCard")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTables)
            {
                if (ds.Tables[0].Rows.Count != 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    cardInfo[0] = dr["pan"].ToString();
                    cardInfo[1] = dr["expiry_date"].ToString();
                    cardInfo[2] = dr["account_id"].ToString();
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cardInfo;
    }
    //Get card details
    public string[] GetCardDetails(string pan, string seq, string exp)
    {
        var cardDetails = new string[8];

        try
        {
            string issuer = GetCardIssuer(pan, seq, exp);// GetIssuer(pan.Substring(0, 6));

            string sql = "SELECT s.c1_name_on_card,c.card_program,c.pvv_or_pin_offset,p.pin_verification_key,p.pvki_or_pin_length,p.pin_verification_method FROM pc_cards_" + issuer + "_A c INNER JOIN pc_customers_" + issuer + "_A s ON c.customer_id = s.customer_id " +
                        "INNER JOIN pc_card_programs p on c.card_program = p.card_program  WHERE PAN = @pan AND seq_nr = @seq AND expiry_date = @exp ";

            Connect cn = new Connect("PostCard")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@seq", seq);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTables)
            {
                if (ds.Tables[0].Rows.Count != 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    var kyName = "";
                    cardDetails[0] = dr["card_program"].ToString();
                    cardDetails[1] = dr["pvv_or_pin_offset"].ToString();
                    cardDetails[2] = dr["c1_name_on_card"].ToString();
                    cardDetails[3] = kyName = dr["pin_verification_key"].ToString();
                    cardDetails[4] = GetCryptoKeys(kyName);
                    cardDetails[5] = dr["pvki_or_pin_length"].ToString();
                    cardDetails[6] = dr["pin_verification_method"].ToString();
                    cardDetails[7] = issuer;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cardDetails;
    }
    //Get Card Details
    public string[] GetCardData(string pan, string seq_nr, string exp)
    {
        var cardDetails = new string[9];
        try
        {
            string issuer = GetCardIssuer(pan, seq_nr, exp);// GetIssuer(pan.Substring(0, 6));
            logger.Error("issuer " + issuer);
            string sql = "SELECT s.c1_name_on_card,c.seq_nr,c.card_program,c.pvv_or_pin_offset,p.pin_verification_key,p.pvki_or_pin_length,p.pin_verification_method FROM pc_cards_" + issuer + "_A c INNER JOIN pc_customers_" + issuer + "_A s ON c.customer_id = s.customer_id " +
                        "INNER JOIN pc_card_programs p on c.card_program = p.card_program " +
                        "WHERE c.PAN = @pan AND seq_nr = @seq_nr AND expiry_date = @exp ";

            Connect cn = new Connect("PostCard")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@seq_nr", seq_nr);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTables)
            {
                if (ds.Tables[0].Rows.Count != 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    var kyName = "";
                    cardDetails[0] = dr["card_program"].ToString();
                    cardDetails[1] = dr["pvv_or_pin_offset"].ToString();
                    cardDetails[2] = dr["c1_name_on_card"].ToString();
                    cardDetails[3] = kyName = dr["pin_verification_key"].ToString();
                    cardDetails[4] = GetCryptoKeys(kyName);
                    cardDetails[5] = dr["pvki_or_pin_length"].ToString();
                    cardDetails[6] = dr["pin_verification_method"].ToString();
                    cardDetails[7] = dr["seq_nr"].ToString();
                    cardDetails[8] = issuer;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cardDetails;
    }	
	
    //Get Active Imal Card Details
	public string GetImalActiveCardsByCustomerId(string customer_id)
    {
        var cardData = "";
        string exp = DateTime.Now.ToString("yyMM");
        var cus_Id = customer_id;
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("count", typeof(int));
        crds.Columns.Add("date", typeof(DateTime));

        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }
        DataTable rrr = new DataTable();
        try
        {
            var data = GetImalCustomerActiveCards2(customer_id, cus_Id, exp);
            if (data.Rows.Count > 0)
            {
                rrr.Merge(data);
            }

            if (rrr.Rows.Count > 0)
            {
                DataView dv = rrr.DefaultView;
                dv.Sort = "date desc";
                DataTable sortedDT = dv.ToTable();

                for (int i = 0; i < sortedDT.Rows.Count; i++)
                {
                    DataRow fr = sortedDT.Rows[i];
                    if (cardData != "")
                    {
                        cardData += "~" + fr[0];
                    }
                    else
                    {
                        cardData += fr[0];
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }

        if (cardData == "")
        {
            cardData = "00|00|00";
        }
        return cardData;
    }
	
	public DataTable GetImalCustomerActiveCards2(string customer_id, string cus_Id, string exp)
    {
        var cardData = "00|00|00";
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("Count", typeof(int));
        crds.Columns.Add("Date", typeof(DateTime));

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards_11_a c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1'  AND (LEFT(c.PAN,8) in ('53347750','53347751') OR LEFT(c.PAN,6) in ('506162')) AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        var rr = new string[3];
                        rr[0] = string.Empty; rr[1] = string.Empty;
                        rr[2] = string.Empty;
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();

                        //if (i > 0)
                        //{
                        //    cardData += "~";
                        //}

                        cardData = rr[0] = pan + "|" + expiry_date + "|" + seq_nr + "|" + program;
                        var dd = GetCardActivity(pan, seq_nr);
                        rr[1] = dd[0]; rr[2] = dd[1];
                        crds.Rows.Add(rr);
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return crds;
    }
    //Get Card Stat
    public string GetCardStat(string pan, string exp)
    {
        var cardDetails = string.Empty;
        var cvv2 = string.Empty;


        try
        {
            var cvk = GetCVV2Key(pan.Trim(), exp.Trim());
            cvv2 = p.GenerateCVV2(pan, exp, cvk);
            string issuer = GetCardIssuer(pan, exp);// GetIssuer(pan.Substring(0, 6));

            string sql = "SELECT s.c1_name_on_card,CASE WHEN c.card_status IN ('1') THEN 'ACTIVE' ELSE 'INACTIVE' END AS card_status,c.expiry_date, " +
                "CASE WHEN c.hold_rsp_code IS NULL THEN 'NOT BLOCKED' ELSE 'BLOCKED' END AS BlockStat," +
                "CASE WHEN c.card_program LIKE '%MASTER%' THEN 'MASTERCARD' WHEN c.card_program LIKE '%VISA%' THEN 'VISA' ELSE 'VERVE' END AS CardType " +
                "FROM pc_cards_" + issuer + "_A c INNER JOIN pc_customers_" + issuer + "_A s ON c.customer_id = s.customer_id " +
                "WHERE c.PAN = @pan AND expiry_date = @exp ";

            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws != 0)
                {
                    cardDetails = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string name = dr["c1_name_on_card"].ToString();
                        string stat = dr["card_status"].ToString();
                        string hold = dr["BlockStat"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string cardType = dr["CardType"].ToString();




                        if (i > 0)
                        {
                            cardDetails += "~";
                        }

                        cardDetails += name + "|" + stat + "|" + hold + "|" + expiry_date + "|" + cardType + "|" + cvv2;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cardDetails;
    }

    //Update Pin Offset
    public int UpdateOffset(string pan, string seq, string exp, string newOffset, string oldOffset, string issuer)
    {
        var upVal = 0;

        try
        {
            //string issuer = GetIssuer(pan.Substring(0, 6));

            string sql = "UPDATE pc_cards_" + issuer + "_A SET pvv_or_pin_offset = @npvv WHERE PAN = @pan AND expiry_date = @exp " +
                                          "AND seq_nr = @seq";
            Connect un = new Connect("PostCard")
            {
                Persist = true
            };
            un.SetSQL(sql);
            un.AddParam("@npvv", newOffset);
            un.AddParam("@pan", pan);
            un.AddParam("@seq", seq);
            un.AddParam("@exp", exp);
            upVal = un.Update();
            un.CloseAll();
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return upVal;
    }
    //Update Pin Offset
    public int ActivateAndUpdateOffset(string pan, string seq, string exp, string newOffset, string oldOffset, string issuer)
    {
        var upVal = 0;

        try
        {
            string sql = "UPDATE pc_cards_" + issuer + "_A SET pvv_or_pin_offset = @npvv,card_status = '1' WHERE PAN = @pan AND expiry_date = @exp " +
                                          "AND seq_nr = @seq AND pvv_or_pin_offset = @opvv";
            Connect un = new Connect("PostCard")
            {
                Persist = true
            };
            un.SetSQL(sql);
            un.AddParam("@npvv", newOffset);
            un.AddParam("@pan", pan);
            un.AddParam("@seq", seq);
            un.AddParam("@exp", exp);
            un.AddParam("@opvv", oldOffset);
            upVal = un.Update();
            un.CloseAll();
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return upVal;
    }
    //Check that Card program is allowed for PIN Change
    public bool AllowedCardProgram(string cardProgram)
    {
        bool allowed = true;

        return allowed;
    }
    public int GetPINTries(string pan, string seq, string exp)
    {
        int pinTry = 0;
        string issuer = GetCardIssuer(pan, seq, exp);// GetIssuer(pan.Substring(0, 6));

        string sql = "SELECT * FROM pc_pin_try_counts WHERE pan = @pan AND seq_nr = @seq AND issuer_nr = @issuer";
        Connect cn = new Connect("PostCard");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@pan", pan);
        cn.AddParam("@seq", seq);
        cn.AddParam("@issuer", issuer);
        DataSet ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

        if (hasTables)
        {
            int recs = ds.Tables[0].Rows.Count;

            if (recs > 0)
            {
                DataRow dr = ds.Tables[0].Rows[0];
                var r = dr["pin_try_count"].ToString();

                try
                {
                    pinTry = Convert.ToInt32(r);
                }
                catch
                {

                }
            }
            else
            {
                pinTry = -1;
            }
        }
        else
        {
            pinTry = -1;
        }
        return pinTry;
    }
    public int UpdatePINTries(string pan, string seq, string exp, int chk)
    {
        int pinTry = 0;
        string issuer = GetCardIssuer(pan, seq, exp);// GetIssuer(pan.Substring(0, 6));
        string sql = string.Empty;

        if (chk < 0)
        {
            sql = @"INSERT INTO pc_pin_try_counts (issuer_nr,PAN,seq_nr,pin_try_count,pin2_try_count,last_updated_date,cumulative_pin_try_count,cumulative_pin2_try_count)
                    VALUES (@issuer, @pan, @seq, 1, 0, GETDATE(), 1, 0)";
        }
        else
        {
            sql = @"UPDATE pc_pin_try_counts SET pin_try_count = pin_try_count + 1,cumulative_pin_try_count = cumulative_pin_try_count + 1,
                    last_updated_date = GETDATE() WHERE PAN = @pan AND seq_nr = @seq AND issuer_nr = @issuer";
        }

        Connect cn = new Connect("PostCard");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@pan", pan);
        cn.AddParam("@seq", seq);
        cn.AddParam("@issuer", issuer);
        pinTry = cn.Update();
        cn.CloseAll();

        return pinTry;
    }
    //SFTP Upload
    public bool SFTPUpload(string sftpHost, int sftpPort, string sftpUser, string sftpPwd, string sftpPath, string uploadFile, string sftpName)
    {
        bool uploadStatus = false;

        logger.Error("About to connect to " + sftpName + " SFTP...");

        using (var sClient = new SftpClient(sftpHost, sftpPort, sftpUser, sftpPwd))
        {
            //Connect to the SFTP...
            sClient.Connect();

            if (sClient.IsConnected)
            {
                logger.Error("Connected to the SFTP " + sftpHost + " on port " + sftpPort + " with user " + sftpUser + " ...");

                sClient.ChangeDirectory(sftpPath);
                logger.Error("Directory Changed to " + sftpPath + " ...");

                logger.Error("Uploading File " + Path.GetFileName(uploadFile) + " to " + sftpPath + " ...");

                using (var fileStream = new FileStream(uploadFile, FileMode.Open))
                {
                    logger.Error("Uploading File " + Path.GetFileName(uploadFile) + " of size " + fileStream.Length + " to " + sftpPath + " ...");

                    sClient.BufferSize = 4 * 1024; // bypass Payload error large files
                    sClient.UploadFile(fileStream, Path.GetFileName(uploadFile));
                }

                //Verify that the file was actually uploaded.
                string ff = sftpPath + "/" + Path.GetFileName(uploadFile);
                try
                {
                    SftpFileAttributes fa = sClient.GetAttributes(ff);
                    logger.Error("File " + ff + " uploaded Successfully!!!....");
                    uploadStatus = true;
                }
                catch
                {
                    logger.Error("File " + ff + " not uploaded Successfully!!!....");
                }
            }
        }

        return uploadStatus;
    }
    public string[] GetPINOffSet(string pan, string exp, string pinVerifMethod, string pvki)
    {
        var pinOffset = new string[2];

        try
        {
            var pin = GenerateRandomPIN().ToString();
            var encryptClearPIN = p.EncryptClearPIN(pan, pin);

            if (encryptClearPIN[0] == "00")
            {
                if (pinVerifMethod == "0")
                {
                    var ibmOffset = p.GenerateIBMPINOffset(pan, encryptClearPIN[1], pvki, "4");

                    if (ibmOffset[0] == "00")
                    {
                        pinOffset[0] = pin;
                        pinOffset[1] = ibmOffset[1];
                    }
                    else
                    {
                        pinOffset[0] = ibmOffset[0] + "|4|ERROR GENERTAING PIN OFFSET"; ;
                        pinOffset[1] = "";
                    }
                }
                else if (pinVerifMethod == "1")
                {
                    var visaPVV = p.GenerateVISAPVV(pan, encryptClearPIN[1], pvki, "4");

                    if (visaPVV[0] == "00")
                    {
                        pinOffset[0] = visaPVV[0];
                        pinOffset[1] = visaPVV[1];
                    }
                    else
                    {
                        pinOffset[0] = visaPVV[0] + "|4|ERROR GENERTAING PIN OFFSET";
                        pinOffset[1] = "";
                    }
                }
            }
            else
            {
                pinOffset[0] = encryptClearPIN[0] + "|3|ERROR ENCRYPTING CLEAR PIN";
                pinOffset[1] = "";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Error(pan.Substring(0, 6) + "*********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinOffset[1]);
        return pinOffset;
    }
    public string[] GetPINOffSetFROMPINBLOCK(string pan, string exp, string pinVerifMethod, string pvki, string block)
    {
        var pinOffset = new string[2];
        //string aa = "00";

        try
        {
            //var pin = GenerateRandomPIN().ToString();
            //var encryptClearPIN = p.EncryptClearPIN(pan, pin);
            string dec = p.DecryptZPK(block, pvki);

            if (pinVerifMethod == "0")
            {
                var ibmOffset = p.GenerateIBMPINOffset(pan, block, pvki, "4");

                if (ibmOffset[0] == "00")
                {
                    pinOffset[0] = "";
                    pinOffset[1] = ibmOffset[1];
                }
                else
                {
                    pinOffset[0] = ibmOffset[0] + "|4|ERROR GENERTAING PIN OFFSET"; ;
                    pinOffset[1] = "";
                }
            }
            else if (pinVerifMethod == "1")
            {
                var visaPVV = p.GenerateVISAPVV(pan, block, pvki, "4");

                if (visaPVV[0] == "00")
                {
                    pinOffset[0] = visaPVV[0];
                    pinOffset[1] = visaPVV[1];
                }
                else
                {
                    pinOffset[0] = visaPVV[0] + "|4|ERROR GENERTAING PIN OFFSET";
                    pinOffset[1] = "";
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Error(pan.Substring(0, 6) + "*********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinOffset[1]);
        return pinOffset;
    }
    //InsertMasterPassQRCode
    public string[] GetQRData(string account, string cardprogram)
    {
        string[] resp = new string[2];
        string pan = string.Empty; string seq = string.Empty;
        string exp = string.Empty; string data = string.Empty;

        var issuer = GetIssuerFromCardProgramDet(cardprogram);

        string sql = "SELECT c.pan,c.seq_nr,c.expiry_date FROM pc_cards_" + issuer + "_A c INNER JOIN pc_card_accounts_" + issuer + "_A a ON c.pan = a.pan AND c.seq_nr = a.seq_nr " +
                    "WHERE a.account_id = @account AND c.card_program = @card_program AND hold_rsp_code IS NULL AND c.card_status = '1' and a.date_deleted IS NULL";
        Connect cn = new Connect("PostCard");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@account", account);
        cn.AddParam("@card_program", cardprogram);
        DataSet ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        if (hasTables)
        {
            int rws = ds.Tables[0].Rows.Count;

            if (rws > 0)
            {
                for (int i = 0; i < rws; i++)
                {
                    DataRow dr = ds.Tables[0].Rows[i];
                    pan = (dr["pan"].ToString()).Trim(); ;
                    seq = (dr["seq_nr"].ToString()).Trim();
                    exp = (dr["expiry_date"].ToString()).Trim();

                    if (i > 0)
                    {
                        data += "~";
                    }
                    data += pan + "|" + seq + "|" + exp;
                }
                resp[0] = "00";
                resp[1] = data;
            }
            else
            {
                resp[0] = "NO DATA RETRIEVED FOR ACCOUNT " + account + "!!!";
                resp[1] = "";
            }
        }
        else
        {
            resp[0] = "NO DATA RETRIEVED FOR ACCOUNT " + account + "!!!";
            resp[1] = "";
        }
        return resp;
    }
    //Insert Wallet Card Records
    public string[] InsertWalletCardDetails(string pan, string seq, string exp, string account, string custId, string nameOnCard, string city, string state, string address, string branch, string accType, string curCode, string cardProg, string user)
    {
        bool stat = true;
        var insResponse = new string[2]; var chk = "0";
        var customer_Id = string.Empty; //var branch = string.Empty; 
        var initials = string.Empty; var lastName = string.Empty;
        var firstName = string.Empty; //var nameOnCard = string.Empty; //var address = string.Empty;// var city = string.Empty;
        var region = string.Empty; var country = string.Empty; //var accType = string.Empty; 
        var cardStat = "1";
        var pvv = string.Empty; //var curCode = string.Empty;
        var title = "Mr"; var disc = string.Empty;
        var pvki = string.Empty;
        //string custId = string.Empty;
        var addy1 = string.Empty; var addy2 = string.Empty;

        try
        {
            //DataSet accInfo = new DataSet();

            //if (account != "")
            //{
            //    if (account.Substring(0, 2) == "05")
            //    {
            //        accInfo = GetIMALAccInfo(account);
            //        bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            //        if (hasTables)
            //        {
            //            DataRow dr = accInfo.Tables[0].Rows[0];
            //            customer_Id = dr["CIF_SUB_NO"].ToString();
            //            branch = dr["BRANCH_CODE"].ToString();
            //            string accountGrp = dr["BRIEF_DESC_ENG"].ToString();
            //            accType = ISOAcctType(accountGrp.ToUpper());
            //            curCode = dr["CURRENCY_CODE"].ToString();

            //            address = dr["ADDRESS1_ENG"].ToString() + " " + dr["ADDRESS2_ENG"].ToString() + " " + dr["ADDRESS3_ENG"].ToString();
            //            var addyLen = address.Length;
            //            if (addyLen >= 25)
            //            {
            //                addy1 = address.Substring(0, 25);
            //                addy2 = address.Substring(25, addyLen - 25);
            //            }
            //            city = "LAGOS";// dr["CITY_ENG"].ToString();
            //            region = "LAGOS";
            //            country = "NIGERIA";
            //            title = "MR";
            //            chk = "1";
            //            string[] otherNames = GetCardNames(dr["LONG_NAME_ENG"].ToString());

            //            lastName = otherNames[0]; initials = otherNames[2]; firstName = otherNames[1];
            //            nameOnCard = otherNames[3]; stat = true;
            //        }
            //        else
            //        {
            //            insResponse[0] = account + "|1|ERROR RETRIEVING " + account + " DETAILS";
            //            insResponse[1] = ""; insResponse[2] = "";
            //        }
            //    }
            //    else
            //    {
            //        eacbs.getAccountFullInfo(account);
            //        bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            //        if (hasTables)
            //        {
            //            DataRow dr = accInfo.Tables[0].Rows[0];
            //            customer_Id = custId = dr["CUS_NUM"].ToString();
            //            branch = dr["BRA_CODE"].ToString();
            //            string accountGrp = dr["AccountGroup"].ToString();
            //            accType = ISOAcctType(accountGrp.ToUpper());
            //            curCode = dr["T24_CUR_CODE"].ToString();

            //            DataSet cusInfo = eacbs.getCustomrInfo(customer_Id);
            //            bool hasData = cusInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            //            if (hasData)
            //            {
            //                DataRow drs = cusInfo.Tables[0].Rows[0];
            //                address = drs["Street"].ToString() + " " + drs["Address"].ToString();
            //                city = drs["City"].ToString();
            //                region = drs["State"].ToString();
            //                country = drs["ResidenceCode"].ToString();
            //                title = dr["CourtesyTitle"].ToString();

            //                string[] otherNames = GetCardNames(dr["NAME_LINE2"].ToString(), dr["NAME_LINE1"].ToString());

            //                lastName = otherNames[0]; initials = otherNames[2]; firstName = otherNames[1];
            //                nameOnCard = otherNames[3]; stat = true;
            //            }
            //            else
            //            {
            //                insResponse[0] = customer_Id + "|2|ERROR RETRIEVING CUSTOMER" + customer_Id + " DETAILS";
            //                insResponse[1] = "";
            //            }
            //        }
            //        else
            //        {
            //            insResponse[0] = account + "|1|ERROR RETRIEVING " + account + " DETAILS";
            //            insResponse[1] = "";
            //        }
            //    }
            //}

            if (stat)
            {
                try
                {
                    customer_Id = custId;
                    var addyLen = address.Length;
                    if (addyLen >= 25)
                    {
                        addy1 = address.Substring(0, 25);
                        addy2 = address.Substring(25, addyLen - 25);
                    }

                    region = state;
                    country = "NGA";

                    string[] otherNames = GetCardNames(nameOnCard);

                    lastName = otherNames[0]; initials = otherNames[2]; firstName = otherNames[1];
                    nameOnCard = otherNames[3]; stat = true;
                    //string issuer_nr = GetIssuer(pan.Substring(0, 6));
                    var cardProgDet = GetCardProgDet(cardProg);// GetCardProgramDet(issuer_nr, cardProg);
                    var issuer_nr = cardProgDet[5];
                    pvki = cardProgDet[4]; disc = cardProgDet[3];
                    var pvvData = GetPINOffSet(pan, exp, cardProgDet[1], cardProgDet[0]);
                    pvv = pvvData[1];
                    string insCustomer = InsertCustomer(issuer_nr, customer_Id, title, firstName, initials, lastName, nameOnCard, addy1, addy2, city, region, country, user, chk);
                    if (insCustomer != "")
                    {
                        customer_Id = insCustomer;
                        int insAcc = InsertAccount(issuer_nr, account, accType, curCode, user);
                        if (insAcc > 0)
                        {
                            int insCusAcc = InsertCustomerAccount(issuer_nr, customer_Id, account, accType, user);
                            if (insCusAcc > 0)
                            {
                                int insCard = InsertCards(issuer_nr, pan, seq, cardProg, accType, cardStat, exp, pvki, pvv, disc, branch, customer_Id, user);
                                if (insCard > 0)
                                {
                                    int insCardAcc = InsertCardAccounts(issuer_nr, pan, seq, account, accType, user);
                                    if (insCardAcc > 0)
                                    {
                                        stat = true;
                                        insResponse[0] = "00|8|" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + "-" + customer_Id + " - " + account + "DETAILS INSERTED SUCCESSFULLY";
                                        insResponse[1] = pvvData[0];
                                    }
                                    else
                                    {
                                        insResponse[0] = customer_Id + " - " + account + "|7|ERROR INSERTING CARD_ACCOUNT" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + " DETAILS";
                                        insResponse[1] = "";
                                    }
                                }
                                else
                                {
                                    insResponse[0] = customer_Id + " - " + account + "|6|ERROR INSERTING CARD" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + " DETAILS";
                                    insResponse[1] = "";
                                }
                            }
                            else
                            {
                                insResponse[0] = customer_Id + " - " + account + "|5|ERROR INSERTING CUSTOMER_ACCOUNT" + customer_Id + "_" + account + " DETAILS";
                                insResponse[1] = "";
                            }
                        }
                        else
                        {
                            insResponse[0] = customer_Id + " - " + account + "|4|ERROR INSERTING ACCOUNT" + account + " DETAILS";
                            insResponse[1] = "";
                        }
                    }
                    else
                    {
                        insResponse[0] = customer_Id + " - " + account + "|3|ERROR INSERTING CUSTOMER" + customer_Id + " DETAILS";
                        insResponse[1] = "";
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }

        }
        catch (Exception ex)
        { logger.Error(ex); }

        return insResponse;
    }
    //Insert Card Records
    public string[] InsertVirtualCMSDetails(string pan, string seq, string account, string exp, string cardProg, string user)
    {
        bool stat = true;
        var insResponse = new string[2]; var chk = "0";
        var customer_Id = string.Empty; var branch = string.Empty; var initials = string.Empty; var lastName = string.Empty;
        var firstName = string.Empty; var nameOnCard = string.Empty; var address = string.Empty; var city = string.Empty;
        var region = string.Empty; var country = string.Empty; var accType = string.Empty; var cardStat = "1";
        var pvv = string.Empty; var curCode = string.Empty; var title = string.Empty; var disc = string.Empty;
        var pvki = string.Empty; string custId = string.Empty; var addy1 = string.Empty; var addy2 = string.Empty;

        try
        {
            DataSet accInfo = new DataSet();
            accInfo = null;

            if (account != "")
            {
                if (account.Substring(0, 2) == "05")
                {
                    accInfo = GetIMALAccInfo(account);
                    bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                    if (hasTables)
                    {
                        DataRow dr = accInfo.Tables[0].Rows[0];
                        customer_Id = dr["CIF_SUB_NO"].ToString();
                        branch = dr["BRANCH_CODE"].ToString();
                        string accountGrp = dr["BRIEF_DESC_ENG"].ToString();
                        accType = ISOAcctType(accountGrp.ToUpper());
                        curCode = dr["CURRENCY_CODE"].ToString();

                        address = dr["ADDRESS1_ENG"].ToString() + " " + dr["ADDRESS2_ENG"].ToString() + " " + dr["ADDRESS3_ENG"].ToString();
                        var addyLen = address.Length;
                        if (addyLen >= 25)
                        {
                            addy1 = address.Substring(0, 25);
                            addy2 = address.Substring(25, addyLen - 25);
                        }
                        city = "LAGOS";// dr["CITY_ENG"].ToString();
                        region = "LAGOS";
                        country = "NIGERIA";
                        title = "MR";
                        chk = "1";
                        string[] otherNames = GetCardNames(dr["LONG_NAME_ENG"].ToString());

                        lastName = otherNames[0]; initials = otherNames[2]; firstName = otherNames[1];
                        nameOnCard = otherNames[3]; stat = true;
                    }
                    else
                    {
                        insResponse[0] = account + "|1|ERROR RETRIEVING " + account + " DETAILS";
                        insResponse[1] = ""; insResponse[2] = "";
                    }
                }
                else
                {
                    accInfo = eacbs.getAccountFullInfo(account);
                    bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

                    if (hasTables)
                    {
                        DataRow dr = accInfo.Tables[0].Rows[0];
                        customer_Id = custId = dr["CUS_NUM"].ToString();
                        branch = dr["BRA_CODE"].ToString();
                        string accountGrp = dr["AccountGroup"].ToString();
                        accType = ISOAcctType(accountGrp.ToUpper());
                        curCode = dr["T24_CUR_CODE"].ToString();

                        DataSet cusInfo = eacbs.getCustomrInfo(customer_Id);
                        bool hasData = cusInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                        if (hasData)
                        {
                            DataRow drs = cusInfo.Tables[0].Rows[0];
                            address = drs["Street"].ToString() + " " + drs["Address"].ToString();
                            city = drs["City"].ToString();
                            region = drs["State"].ToString();
                            country = drs["ResidenceCode"].ToString();
                            title = dr["CourtesyTitle"].ToString();

                            string[] otherNames = GetCardNames(dr["NAME_LINE2"].ToString(), dr["NAME_LINE1"].ToString());

                            lastName = otherNames[0]; initials = otherNames[2]; firstName = otherNames[1];
                            nameOnCard = otherNames[3]; stat = true;
                        }
                        else
                        {
                            insResponse[0] = customer_Id + "|2|ERROR RETRIEVING CUSTOMER" + customer_Id + " DETAILS";
                            insResponse[1] = "";
                        }
                    }
                    else
                    {
                        insResponse[0] = account + "|1|ERROR RETRIEVING " + account + " DETAILS";
                        insResponse[1] = "";
                    }
                }
            }

            if (stat)
            {
                try
                {
                    //string issuer_nr = GetIssuer(pan.Substring(0, 6));
                    var cardProgDet = GetCardProgDet(cardProg);// GetCardProgramDet(issuer_nr, cardProg);
                    var issuer_nr = cardProgDet[5];
                    pvki = cardProgDet[4]; disc = cardProgDet[3];
                    var pvvData = GetPINOffSet(pan, exp, cardProgDet[1], cardProgDet[0]);
                    pvv = pvvData[1];
                    string insCustomer = InsertCustomer(issuer_nr, customer_Id, title, firstName, initials, lastName, nameOnCard, addy1, addy2, city, region, country, user, chk);
                    if (insCustomer != "")
                    {
                        customer_Id = insCustomer;
                        int insAcc = InsertAccount(issuer_nr, account, accType, curCode, user);
                        if (insAcc > 0)
                        {
                            int insCusAcc = InsertCustomerAccount(issuer_nr, customer_Id, account, accType, user);
                            if (insCusAcc > 0)
                            {
                                int insCard = InsertCards(issuer_nr, pan, seq, cardProg, accType, cardStat, exp, pvki, pvv, disc, branch, customer_Id, user);
                                if (insCard > 0)
                                {
                                    int insCardAcc = InsertCardAccounts(issuer_nr, pan, seq, account, accType, user);
                                    if (insCardAcc > 0)
                                    {
                                        stat = true;
                                        insResponse[0] = "00|8|" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + "-" + customer_Id + " - " + account + "DETAILS INSERTED SUCCESSFULLY";
                                        insResponse[1] = pvvData[0];
                                    }
                                    else
                                    {
                                        insResponse[0] = customer_Id + " - " + account + "|7|ERROR INSERTING CARD_ACCOUNT" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + " DETAILS";
                                        insResponse[1] = "";
                                    }
                                }
                                else
                                {
                                    insResponse[0] = customer_Id + " - " + account + "|6|ERROR INSERTING CARD" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + " DETAILS";
                                    insResponse[1] = "";
                                }
                            }
                            else
                            {
                                insResponse[0] = customer_Id + " - " + account + "|5|ERROR INSERTING CUSTOMER_ACCOUNT" + customer_Id + "_" + account + " DETAILS";
                                insResponse[1] = "";
                            }
                        }
                        else
                        {
                            insResponse[0] = customer_Id + " - " + account + "|4|ERROR INSERTING ACCOUNT" + account + " DETAILS";
                            insResponse[1] = "";
                        }
                    }
                    else
                    {
                        insResponse[0] = customer_Id + " - " + account + "|3|ERROR INSERTING CUSTOMER" + customer_Id + " DETAILS";
                        insResponse[1] = "";
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }

        }
        catch (Exception ex)
        { logger.Error(ex); }

        return insResponse;
    }
    //Insert Inactive Card Records
    public string[] InsertVirtualCMSData(string issuer_nr, string pan, string seq, string account, string exp, string cardProg, string user, string cardStat = "0")
    {
        bool stat = false;
        var insResponse = new string[2]; var chk = "0";
        var customer_Id = string.Empty; var branch = string.Empty; var initials = string.Empty; var lastName = string.Empty;
        var firstName = string.Empty; var nameOnCard = string.Empty; var address = string.Empty; var city = string.Empty;
        var region = string.Empty; var country = string.Empty; var accType = string.Empty; //var cardStat = cardStat;
        var pvv = string.Empty; var curCode = string.Empty; var title = string.Empty; var disc = string.Empty;
        var pvki = string.Empty; string custId = string.Empty;var addy1 = string.Empty;var addy2 = string.Empty;

        try
        {
            DataSet accInfo = new DataSet();

            if (account != "")
            {
                if (account.Substring(0, 2) == "05")
                {
                    accInfo = GetIMALAccInfo(account);
                    bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                    if (hasTables)
                    {
                        DataRow dr = accInfo.Tables[0].Rows[0];
                        customer_Id = dr["CIF_SUB_NO"].ToString();
                        branch = dr["BRANCH_CODE"].ToString();
                        string accountGrp = dr["BRIEF_DESC_ENG"].ToString();
                        accType = ISOAcctType(accountGrp.ToUpper());
                        curCode = dr["CURRENCY_CODE"].ToString();

                        address = dr["ADDRESS1_ENG"].ToString() + " " + dr["ADDRESS2_ENG"].ToString() + " " + dr["ADDRESS3_ENG"].ToString();
                        var addy = getAddressLines(address);addy1 = addy[0];addy2 = addy[1];
                        city = dr["CITY_ENG"].ToString();
                        region = "LAGOS";
                        country = "NGA";
                        title = "MR";
                        chk = "1";
                        string[] otherNames = GetCardNames(dr["LONG_NAME_ENG"].ToString());

                        lastName = otherNames[0]; initials = otherNames[2]; firstName = otherNames[1];
                        nameOnCard = otherNames[3]; stat = true;
                    }
                    else
                    {
                        insResponse[0] = account + "|1|ERROR RETRIEVING " + account + " DETAILS";
                        insResponse[1] = ""; insResponse[2] = "";
                    }

                }
                else
                {
                    accInfo = eacbs.getAccountFullInfo(account);
                    bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

                    if (hasTables)
                    {
                        DataRow dr = accInfo.Tables[0].Rows[0];
                        customer_Id = custId = dr["CUS_NUM"].ToString();
                        branch = dr["BRA_CODE"].ToString();
                        string accountGrp = dr["AccountGroup"].ToString();
                        accType = ISOAcctType(accountGrp.ToUpper());
                        curCode = dr["T24_CUR_CODE"].ToString();

                        DataSet cusInfo = eacbs.getCustomrInfo(customer_Id);
                        bool hasData = cusInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                        if (hasData)
                        {
                            DataRow drs = cusInfo.Tables[0].Rows[0];
                            address = drs["Street"].ToString() + " " + drs["Address"].ToString();
                            var addy = getAddressLines(address); addy1 = addy[0]; addy2 = addy[1];
                            city = drs["City"].ToString();
                            region = drs["State"].ToString();
                            country = "NGA";// drs["ResidenceCode"].ToString();
                            title = dr["CourtesyTitle"].ToString();

                            string[] otherNames = GetCardNames(dr["NAME_LINE2"].ToString(), dr["NAME_LINE1"].ToString());

                            lastName = otherNames[0]; initials = otherNames[2]; firstName = otherNames[1];
                            nameOnCard = otherNames[3];
                            stat = true;
                        }
                        else
                        {
                            insResponse[0] = customer_Id + "|2|ERROR RETRIEVING CUSTOMER" + customer_Id + " DETAILS";
                            insResponse[1] = "";
                        }
                    }
                    else
                    {
                        insResponse[0] = account + "|1|ERROR RETRIEVING " + account + " DETAILS";
                        insResponse[1] = "";
                    }
                }
            }

            if (stat)
            {
                try
                {
                    //string issuer_nr = GetIssuer(pan.Substring(0, 6));
                    var cardProgDet = GetCardProgramDet(issuer_nr, cardProg);
                    pvki = cardProgDet[4]; disc = cardProgDet[3];
                    var pvvData = GetPINOffSet(pan, exp, cardProgDet[1], cardProgDet[0]);
                    pvv = pvvData[1];
                    string insCustomer = InsertCustomer(issuer_nr, customer_Id, title, firstName, initials, lastName, nameOnCard, addy1, addy2, city, region, country, user, chk);
                    if (insCustomer != "")
                    {
                        customer_Id = insCustomer;
                        int insAcc = InsertAccount(issuer_nr, account, accType, curCode, user);
                        if (insAcc > 0)
                        {
                            int insCusAcc = InsertCustomerAccount(issuer_nr, customer_Id, account, accType, user);
                            if (insCusAcc > 0)
                            {
                                int insCard = InsertCards(issuer_nr, pan, seq, cardProg, accType, cardStat, exp, pvki, pvv, disc, branch, customer_Id, user);
                                if (insCard > 0)
                                {
                                    int insCardAcc = InsertCardAccounts(issuer_nr, pan, seq, account, accType, user);
                                    if (insCardAcc > 0)
                                    {
                                        stat = true;
                                        insResponse[0] = "00|8|" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + "-" + customer_Id + " - " + account + "DETAILS INSERTED SUCCESSFULLY";
                                        insResponse[1] = pvvData[0];
                                    }
                                    else
                                    {
                                        insResponse[0] = customer_Id + " - " + account + "|7|ERROR INSERTING CARD_ACCOUNT" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + " DETAILS";
                                        insResponse[1] = "";
                                    }
                                }
                                else
                                {
                                    insResponse[0] = customer_Id + " - " + account + "|6|ERROR INSERTING CARD" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + " DETAILS";
                                    insResponse[1] = "";
                                }
                            }
                            else
                            {
                                insResponse[0] = customer_Id + " - " + account + "|5|ERROR INSERTING CUSTOMER_ACCOUNT" + customer_Id + "_" + account + " DETAILS";
                                insResponse[1] = "";
                            }
                        }
                        else
                        {
                            insResponse[0] = customer_Id + " - " + account + "|4|ERROR INSERTING ACCOUNT" + account + " DETAILS";
                            insResponse[1] = "";
                        }
                    }
                    else
                    {
                        insResponse[0] = customer_Id + " - " + account + "|3|ERROR INSERTING CUSTOMER" + customer_Id + " DETAILS";
                        insResponse[1] = "";
                    }

                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }

        }
        catch (Exception ex)
        { logger.Error(ex); }

        return insResponse;
    }
    //clean up card data to remove unwanted xters
    public string cleanCardData(string data)
    {
        string[] dirtyChars = { ",", "\'", "/'", "-", "_", "*", "\\", "?", ":", ";", "+", "'", "&", "`", "'", ".", "," };

        foreach (string c in dirtyChars)
        {
            data = data.Replace(c, "");
        }
        return data;
    }
    //return address line 1 and  2
    public string[] getAddressLines(string addy)
    {
        string[] addr = new string[2];

        string addy1 = ""; string addy2 = "";

        addy = addy.Trim();

        if (addy.Length > 30)
        {
            addy1 = addy.Substring(0, 30);
            addy1 = cleanCardData(addy1);
            int l = addy.Length - 30;
            if (l <= 30)
            {
                addy2 = addy.Substring(30, l);
                addy2 = cleanCardData(addy2);
            }
            else
            {
                addy2 = addy.Substring(30, 30);
                addy2 = cleanCardData(addy2);
            }
        }
        else
        {
            addy1 = addy;
            addy1 = cleanCardData(addy1);
        }

        //set the array values
        addr[0] = addy1.Trim();
        addr[1] = addy2.Trim();

        return addr;
    }
    //Get card Program Details
    public string[] GetCardProgramDet(string issuer_nr, string cardProg)
    {
        var progDet = new string[5];

        try
        {
            string sql = @"SELECT * FROM pc_card_programs WHERE issuer_nr =@issuer_nr AND card_program = @card_program";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@issuer_nr", issuer_nr);
            cn.AddParam("@card_program", cardProg);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws > 0)
                {
                    var kyName = string.Empty; var kycName = string.Empty;
                    DataRow dr = ds.Tables[0].Rows[0];
                    kyName = dr["pin_verification_key"].ToString();
                    kycName = dr["cvv_key"].ToString();
                    progDet[0] = GetCryptoKeys(kyName);
                    progDet[1] = dr["pin_verification_method"].ToString();
                    progDet[2] = GetCryptoKeys(kycName);
                    progDet[3] = dr["discretionary_data"].ToString();
                    progDet[4] = dr["pvki_or_pin_length"].ToString();
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return progDet;
    }
    //Get card Program Details
    public string[] GetCardProgDet(string cardProg)
    {
        var progDet = new string[6];

        try
        {
            string sql = @"SELECT * FROM pc_card_programs WHERE card_program = @card_program";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            //cn.AddParam("@issuer_nr", issuer_nr);
            cn.AddParam("@card_program", cardProg);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws > 0)
                {
                    var kyName = string.Empty; var kycName = string.Empty;
                    DataRow dr = ds.Tables[0].Rows[0];
                    kyName = dr["pin_verification_key"].ToString();
                    kycName = dr["cvv_key"].ToString();
                    progDet[0] = GetCryptoKeys(kyName);
                    progDet[1] = dr["pin_verification_method"].ToString();
                    progDet[2] = GetCryptoKeys(kycName);
                    progDet[3] = dr["discretionary_data"].ToString();
                    progDet[4] = dr["pvki_or_pin_length"].ToString();
                    progDet[5] = dr["issuer_nr"].ToString();
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return progDet;
    }
    //Get card Program Details
    public DataSet GetATMs()
    {
        DataSet ds = new DataSet();

        try
        {
            string sql = @"SELECT name_location as Terminal_Location,a.card_acceptor as Terminal_Acceptor,id as Terminal_ID,short_name as Teminal_ShortName
                             FROM [realtime].[dbo].[term] a with (nolock) INNER JOIN [realtime].[dbo].[tm_card_acceptor] b with (nolock) 
                             ON a.card_acceptor = b.card_acceptor";
            Connect cn = new Connect("Realtime");
            cn.Persist = true;
            cn.SetSQL(sql);
            ds = cn.Select();
            cn.CloseAll();
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return ds;
    }

    //public ATMObj GetATMs()
    //{
    //    DataSet ds = new DataSet();

    //    try
    //    {
    //        string sql = @"SELECT name_location as Terminal_Location,a.card_acceptor as Terminal_Acceptor,id as Terminal_ID,short_name as Teminal_ShortName
    //                         FROM [realtime].[dbo].[term] a with (nolock) INNER JOIN [realtime].[dbo].[tm_card_acceptor] b with (nolock) 
    //                         ON a.card_acceptor = b.card_acceptor";
    //        Connect cn = new Connect("Realtime");
    //        cn.Persist = true;
    //        cn.SetSQL(sql);
    //        ds = cn.Select();
    //        cn.CloseAll();
    //    }
    //    catch (Exception ex)
    //    { logger.Error(ex); }
    //    var obj = new ATMObj();
    //    if (ds != null)
    //    {
    //        if (ds.Tables[0].Rows.Count != 0)
    //        {
    //            obj.Teminal_ShortName = ds.Tables[0].Rows[0]["Teminal_ShortName"].ToString();
    //            obj.Terminal_Acceptor = ds.Tables[0].Rows[0]["Terminal_Acceptor"].ToString();
    //            obj.Terminal_ID = ds.Tables[0].Rows[0]["Terminal_ID"].ToString();
    //            obj.Teminal_ShortName = ds.Tables[0].Rows[0]["Teminal_ShortName"].ToString();
    //        }
    //    }
    //    return obj;
    //}

    //public class ATMObj
    //{
    //    public string Terminal_Location { get; set; }
    //    public string Terminal_Acceptor { get; set; }
    //    public string Terminal_ID { get; set; }
    //    public string Teminal_ShortName { get; set; }
    //}
    //Get card Program Details
    public string GetIssuerFromCardProgramDet(string cardProg)
    {
        var issuer = "1"; ;

        try
        {
            string sql = @"SELECT issuer_nr FROM pc_card_programs WHERE card_program = @card_program";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@card_program", cardProg);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws > 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    issuer = dr["issuer_nr"].ToString();
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return issuer;
    }
    //Insert ustomer Details into Postillion
    public string InsertCustomer(string issuer, string customer_id, string title, string fname, string initials, string lastname, string nameOnCard, string addy1, string addy2, string city, string region, string country, string user, string chk)
    {
        var rws = 0; var data = string.Empty;

        var cus_Id = customer_id;
        if (chk == "1")
        {
            cus_Id = customer_id;
        }
        else if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }

        try
        {
            string sql = "SELECT customer_id FROM pc_customers_" + issuer + "_A WHERE customer_id IN (@customer_id,@cus_Id) ORDER BY customer_id DESC";
            Connect cn = new Connect("PostCard")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                rws = ds.Tables[0].Rows.Count;

                if (rws <= 0)
                {
                    string isql = @"INSERT INTO pc_customers_" + issuer + "_A (issuer_nr, customer_id, c1_title,c1_first_name, c1_initials,c1_last_name, c1_name_on_card, postal_address_1,postal_address_2, postal_city, postal_region, postal_country, vip, last_updated_date, last_updated_user) " +
                                "VALUES (@issuer_nr, @customer_id, @title, @fname,@initials, @lastname, @nameOncard, @addy1, @addy2, @city, @region, @country, '0; @dt, @user)";
                    logger.Info("Sql to excecute is ==> " + isql);
                    Connect un = new Connect("PostCard")
                    {
                        Persist = true
                    };
                    un.SetSQL(isql);
                    un.AddParam("@issuer_nr", issuer);
                    logger.Info("@issuer_nr ==> " + issuer);
                    un.AddParam("@customer_id", cus_Id);
                    logger.Info("@customer_id ==> " + cus_Id);
                    un.AddParam("@title", title);
                    logger.Info("@title ==> " + title);
                    un.AddParam("@fname", fname);
                    logger.Info("@fname ==> " + fname);
                    un.AddParam("@initials", initials);
                    logger.Info("@initials ==> " + initials);
                    un.AddParam("@lastname", lastname);
                    logger.Info("@lastname ==> " + lastname);
                    un.AddParam("@nameOncard", nameOnCard);
                    logger.Info("@nameOnCard ==> " + nameOnCard);
                    un.AddParam("@addy1", addy1);
                    logger.Info("@addy1 ==> " + addy1);
                    un.AddParam("@addy2", addy2);
                    logger.Info("@addy2 ==> " + addy2);
                    un.AddParam("@city", city);
                    logger.Info("@city ==> " + city);
                    un.AddParam("@region", region);
                    logger.Info("@region ==> " + region);
                    un.AddParam("@country", country);
                    logger.Info("@country ==> " + country);
                    string updTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff");
                    un.AddParam("@dt", updTime);
                    logger.Info("@dt ==> " + updTime);
                    un.AddParam("@user", user);
                    logger.Info("@user ==> " + user);
                    rws = un.Update();
                    un.CloseAll();

                    if (rws > 0)
                    {
                        data = cus_Id;
                    }
                }
                else
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    data = dr["customer_id"].ToString();
                }
            }
            else
            {
                string isql = @"INSERT INTO pc_customers_" + issuer + "_A (issuer_nr, customer_id, c1_title,c1_first_name, c1_initials,c1_last_name, c1_name_on_card, postal_address_1,postal_address_2, postal_city, postal_region, postal_country, vip, last_updated_date, last_updated_user) " +
                               "VALUES (@issuer_nr, @customer_id, @title, @fname,@initials, @lastname, @nameOncard, @addy1, @addy2, @city, @region, @country, '0', @dt, @user)";
                logger.Info("Sql to excecute is ==> " + isql);
                Connect un = new Connect("PostCard")
                {
                    Persist = true
                };
                un.SetSQL(isql);
                un.AddParam("@issuer_nr", issuer);
                logger.Info("@issuer ==> " + issuer);
                un.AddParam("@customer_id", cus_Id);
                logger.Info("@customer_id ==> " + cus_Id);
                un.AddParam("@title", title);
                logger.Info("@title ==> " + title);
                un.AddParam("@fname", fname);
                logger.Info("@fname ==> " + fname);
                un.AddParam("@initials", initials);
                logger.Info("@initials ==> " + initials);
                un.AddParam("@lastname", lastname);
                logger.Info("@lastname ==> " + lastname);
                un.AddParam("@nameOncard", nameOnCard);
                logger.Info("@nameOnCard ==> " + nameOnCard);
                un.AddParam("@addy1", addy1);
                logger.Info("@addy1 ==> " + addy1);
                un.AddParam("@addy2", addy2);
                logger.Info("@addy2 ==> " + addy2);
                un.AddParam("@city", city);
                logger.Info("@city ==> " + city);
                un.AddParam("@region", region);
                logger.Info("@region ==> " + region);
                un.AddParam("@country", country);
                logger.Info("@country ==> " + country);
                string updTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff");
                un.AddParam("@dt", updTime);
                logger.Info("@dt ==> " + updTime);
                un.AddParam("@user", user);
                logger.Info("@user ==> " + user);
                rws = un.Update();
                un.CloseAll();

                if (rws > 0)
                {
                    data = cus_Id;
                }
            }

        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return data;
    }
    //Insert Account Details into Postilion
    public int InsertAccount(string issuer_nr, string account_id, string account_type, string currency_code, string user)
    {
        var rws = 0;

        try
        {
            string sql = @"SELECT * FROM pc_accounts_" + issuer_nr + "_A WHERE account_id = @account_id";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@account_id", account_id);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                rws = ds.Tables[0].Rows.Count;

                if (rws <= 0)
                {
                    string isql = @"INSERT INTO pc_accounts_" + issuer_nr + "_A (issuer_nr,account_id,account_type,currency_code,last_updated_date,last_updated_user) " +
                            "VALUES (@issuer_nr,@account_id,@account_type,@currency_code,@update_date,@update_user)";
                    logger.Info("Sql to excecute is ==> " + isql);
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@issuer_nr", issuer_nr);
                    un.AddParam("@account_id", account_id);
                    un.AddParam("@account_type", account_type);
                    un.AddParam("@currency_code", currency_code);
                    un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                    un.AddParam("@update_user", user);
                    rws = un.Update();
                    un.CloseAll();
                }
            }
            else
            {
                string isql = @"INSERT INTO pc_accounts_" + issuer_nr + "_A (issuer_nr,account_id,account_type,currency_code,last_updated_date,last_updated_user) " +
                            "VALUES (@issuer_nr,@account_id,@account_type,@currency_code,@update_date,@update_user)";
                logger.Info("Sql to excecute is ==> " + isql);
                Connect un = new Connect("PostCard");
                un.Persist = true;
                un.SetSQL(isql);
                un.AddParam("@issuer_nr", issuer_nr);
                un.AddParam("@account_id", account_id);
                un.AddParam("@account_type", account_type);
                un.AddParam("@currency_code", currency_code);
                un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                un.AddParam("@update_user", user);
                rws = un.Update();
                un.CloseAll();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return rws;
    }
    //Insert Customer Account Details into Postilion
    public int InsertCustomerAccount(string issuer_nr, string customer_id, string account_id, string account_type, string user)
    {
        var rws = 0;
        try
        {
            string sql = @"SELECT * FROM pc_customer_accounts_" + issuer_nr + "_A WHERE account_id = @account_id AND customer_id = @customer_id " +
                         " AND account_type = @account_type";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@account_id", account_id);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@account_type", account_type);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                rws = ds.Tables[0].Rows.Count;

                if (rws <= 0)
                {
                    string isql = @"INSERT INTO pc_customer_accounts_" + issuer_nr + "_A (issuer_nr,customer_id,account_id,account_type,last_updated_date,last_updated_user) " +
                                 "VALUES (@issuer_nr,@customer_id,@account_id,@account_type,@update_date,@update_user)";
                    logger.Info("Sql to excecute is ==> " + isql);
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@issuer_nr", issuer_nr);
                    un.AddParam("@customer_id", customer_id);
                    un.AddParam("@account_id", account_id);
                    un.AddParam("@account_type", account_type);
                    un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                    un.AddParam("@update_user", user);
                    rws = un.Update();
                    un.CloseAll();
                }
            }
            else
            {
                string isql = @"INSERT INTO pc_customer_accounts_" + issuer_nr + "_A (issuer_nr,customer_id,account_id,account_type,last_updated_date,last_updated_user) " +
                                 "VALUES (@issuer_nr,@customer_id,@account_id,@account_type,@update_date,@update_user)";
                logger.Info("Sql to excecute is ==> " + isql);
                Connect un = new Connect("PostCard");
                un.Persist = true;
                un.SetSQL(isql);
                un.AddParam("@issuer_nr", issuer_nr);
                un.AddParam("@customer_id", customer_id);
                un.AddParam("@account_id", account_id);
                un.AddParam("@account_type", account_type);
                un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                un.AddParam("@update_user", user);
                rws = un.Update();
                un.CloseAll();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return rws;
    }
    //Insert Card Details into Postillion
    public int InsertCards(string issuer_nr, string pan, string seq_nr, string card_program, string def_acc_type, string card_status, string expiry_date, string pvki, string pvv, string disc_data, string branch_code, string customer_id, string user)
    {
        var rws = 0;

        try
        {
            string sql = "SELECT * FROM pc_cards_" + issuer_nr + "_A WHERE pan = @pan AND seq_nr = @seq_nr AND expiry_date = @expiry_date";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@seq_nr", seq_nr);
            cn.AddParam("@expiry_date", expiry_date);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                rws = ds.Tables[0].Rows.Count;

                if (rws <= 0)
                {
                    string isql = @"INSERT INTO pc_cards_" + issuer_nr + "_A (issuer_nr,pan,seq_nr,card_program,default_account_type,card_status, " +
                                  "expiry_date,pvki_or_pin_length,pvv_or_pin_offset,discretionary_data,date_issued,date_activated,branch_code, " +
                                 "mailer_destination,last_updated_date,last_updated_user,customer_id,pvki2_or_pin2_length) " +
                                "VALUES (@issuer_nr, @pan, @seq_nr, @card_program, @def_acc_type, @card_status, @expiry_date, @pvki, @pvv, @disc_data, " +
                                "@dt, @dt, @branch_code, '0', @dt, @user,@customer_id,@pvki)";
                    logger.Info("Sql to excecute is ==> " + isql);
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@issuer_nr", issuer_nr);
                    un.AddParam("@pan", pan);
                    un.AddParam("@seq_nr", seq_nr);
                    un.AddParam("@card_program", card_program);
                    un.AddParam("@def_acc_type", def_acc_type);
                    un.AddParam("@card_status", card_status);
                    un.AddParam("@expiry_date", expiry_date);
                    un.AddParam("@pvki", pvki);
                    un.AddParam("@pvv", pvv);
                    un.AddParam("@disc_data", disc_data);
                    un.AddParam("@branch_code", branch_code);
                    un.AddParam("@customer_id", customer_id);
                    un.AddParam("@dt", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                    un.AddParam("@user", user);
                    rws = un.Update();
                    un.CloseAll();
                }
            }
            else
            {
                string isql = @"INSERT INTO pc_cards_" + issuer_nr + "_A (issuer_nr,pan,seq_nr,card_program,default_account_type,card_status, " +
                                 "expiry_date,pvki_or_pin_length,pvv_or_pin_offset,discretionary_data,date_issued,date_activated,branch_code, " +
                                "mailer_destination,last_updated_date,last_updated_user,customer_id,pvki2_or_pin2_length) " +
                               "VALUES (@issuer_nr, @pan, @seq_nr, @card_program, @def_acc_type, @card_status, @expiry_date, @pvki, @pvv, @disc_data, " +
                               "@dt, @dt, @branch_code, '0', @dt, @user,@customer_id,@pvki)";
                logger.Info("Sql to excecute is ==> " + isql);
                Connect un = new Connect("PostCard");
                un.Persist = true;
                un.SetSQL(isql);
                un.AddParam("@issuer_nr", issuer_nr);
                un.AddParam("@pan", pan);
                un.AddParam("@seq_nr", seq_nr);
                un.AddParam("@card_program", card_program);
                un.AddParam("@def_acc_type", def_acc_type);
                un.AddParam("@card_status", card_status);
                un.AddParam("@expiry_date", expiry_date);
                un.AddParam("@pvki", pvki);
                un.AddParam("@pvv", pvv);
                un.AddParam("@disc_data", disc_data);
                un.AddParam("@branch_code", branch_code);
                un.AddParam("@customer_id", customer_id);
                un.AddParam("@dt", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                un.AddParam("@user", user);
                rws = un.Update();
                un.CloseAll();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return rws;
    }
    public int InsertCardsWithRef(string issuer_nr, string pan, string seq_nr, string card_program, string def_acc_type, string card_status, string expiry_date, string pvki, string pvv, string disc_data, string branch_code, string customer_id, string refId, string user)
    {
        var rws = 0;

        try
        {
            string sql = "SELECT * FROM pc_cards_" + issuer_nr + "_A WHERE pan = @pan AND seq_nr = @seq_nr AND expiry_date = @expiry_date";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@seq_nr", seq_nr);
            cn.AddParam("@expiry_date", expiry_date);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                rws = ds.Tables[0].Rows.Count;

                if (rws <= 0)
                {
                    string isql = @"INSERT INTO pc_cards_" + issuer_nr + "_A (issuer_nr,pan,seq_nr,card_program,default_account_type,card_status, " +
                                  "expiry_date,pvki_or_pin_length,pvv_or_pin_offset,discretionary_data,date_issued,date_activated,branch_code, " +
                                 "mailer_destination,last_updated_date,last_updated_user,customer_id,pvki2_or_pin2_length,issuer_reference) " +
                                "VALUES (@issuer_nr, @pan, @seq_nr, @card_program, @def_acc_type, @card_status, @expiry_date, @pvki, @pvv, @disc_data, " +
                                "@dt, @dt, @branch_code, '0', @dt, @user,@customer_id,@pvki,@ref)";
                    logger.Info("Sql to excecute is ==> " + isql);
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@issuer_nr", issuer_nr);
                    un.AddParam("@pan", pan);
                    un.AddParam("@seq_nr", seq_nr);
                    un.AddParam("@card_program", card_program);
                    un.AddParam("@def_acc_type", def_acc_type);
                    un.AddParam("@card_status", card_status);
                    un.AddParam("@expiry_date", expiry_date);
                    un.AddParam("@pvki", pvki);
                    un.AddParam("@pvv", pvv);
                    un.AddParam("@disc_data", disc_data);
                    un.AddParam("@branch_code", branch_code);
                    un.AddParam("@customer_id", customer_id);
                    un.AddParam("@ref", refId);
                    un.AddParam("@dt", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                    un.AddParam("@user", user);
                    rws = un.Update();
                    un.CloseAll();
                }
            }
            else
            {
                string isql = @"INSERT INTO pc_cards_" + issuer_nr + "_A (issuer_nr,pan,seq_nr,card_program,default_account_type,card_status, " +
                                 "expiry_date,pvki_or_pin_length,pvv_or_pin_offset,discretionary_data,date_issued,date_activated,branch_code, " +
                                "mailer_destination,last_updated_date,last_updated_user,customer_id,pvki2_or_pin2_length,issuer_reference) " +
                               "VALUES (@issuer_nr, @pan, @seq_nr, @card_program, @def_acc_type, @card_status, @expiry_date, @pvki, @pvv, @disc_data, " +
                               "@dt, @dt, @branch_code, '0', @dt, @user,@customer_id,@pvki,@ref)";
                logger.Info("Sql to excecute is ==> " + isql);
                Connect un = new Connect("PostCard");
                un.Persist = true;
                un.SetSQL(isql);
                un.AddParam("@issuer_nr", issuer_nr);
                un.AddParam("@pan", pan);
                un.AddParam("@seq_nr", seq_nr);
                un.AddParam("@card_program", card_program);
                un.AddParam("@def_acc_type", def_acc_type);
                un.AddParam("@card_status", card_status);
                un.AddParam("@expiry_date", expiry_date);
                un.AddParam("@pvki", pvki);
                un.AddParam("@pvv", pvv);
                un.AddParam("@disc_data", disc_data);
                un.AddParam("@branch_code", branch_code);
                un.AddParam("@customer_id", customer_id);
                un.AddParam("@ref", refId);
                un.AddParam("@dt", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                un.AddParam("@user", user);
                rws = un.Update();
                un.CloseAll();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return rws;
    }
    //Insert Card Account Details into Postilion
    public int InsertCardAccounts(string issuer_nr, string pan, string seq_nr, string account_id, string account_type, string user)
    {
        var rws = 0;
        try
        {
            string sql = @"SELECT * FROM pc_card_accounts_" + issuer_nr + "_A WHERE pan = @pan AND seq_nr = @seq_nr " +
                         "AND account_id = @account_id AND account_type = @account_type";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@seq_nr", seq_nr);
            cn.AddParam("@account_id", account_id);
            cn.AddParam("@account_type", account_type);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                rws = ds.Tables[0].Rows.Count;

                if (rws <= 0)
                {
                    string isql = @"INSERT INTO pc_card_accounts_" + issuer_nr + "_A (issuer_nr,pan,seq_nr,account_id,account_type_nominated," +
                                 "account_type_qualifier,last_updated_date,last_updated_user,account_type) " +
                                 "VALUES (@issuer_nr,@pan,@seq_nr,@account_id,@account_type,'1',@update_date,@update_user,@account_type)";
                    logger.Info("Sql to excecute is ==> " + isql);
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@issuer_nr", issuer_nr);
                    un.AddParam("@pan", pan);
                    un.AddParam("@seq_nr", seq_nr);
                    un.AddParam("@account_id", account_id);
                    un.AddParam("@account_type", account_type);
                    un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                    un.AddParam("@update_user", user);
                    rws = un.Update();
                    un.CloseAll();
                }
            }
            else
            {
                string isql = @"INSERT INTO pc_card_accounts_" + issuer_nr + "_A (issuer_nr,pan,seq_nr,account_id,account_type_nominated," +
                                "account_type_qualifier,last_updated_date,last_updated_user,account_type) " +
                                "VALUES (@issuer_nr,@pan,@seq_nr,@account_id,@account_type,'1',@update_date,@update_user,@account_type)";
                logger.Info("Sql to excecute is ==> " + isql);
                Connect un = new Connect("PostCard");
                un.Persist = true;
                un.SetSQL(isql);
                un.AddParam("@issuer_nr", issuer_nr);
                un.AddParam("@pan", pan);
                un.AddParam("@seq_nr", seq_nr);
                un.AddParam("@account_id", account_id);
                un.AddParam("@account_type", account_type);
                un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                un.AddParam("@update_user", user);
                rws = un.Update();
                un.CloseAll();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return rws;
    }
    //Get ISO AccountType
    public string ISOAcctType(string accountType)
    {
        var _isoAcc = "20";

        if (accountType == "SAVING ACCOUNT")
        {
            accountType = "SAVINGS";
        }

        switch (accountType)
        {
            case "SAVINGS":
                _isoAcc = "10";
                break;
            case "CURRENT":
                _isoAcc = "20";
                break;
            case "CREDIT":
                _isoAcc = "30";
                break;
        }

        return _isoAcc;
    }
    public DataSet GetIMALAccInfo(string acc)
    {
        DataSet irecs = null;
        try
        {
            //string sql = @"SELECT amf.cif_sub_no,amf.branch_code,gen_ledger.brief_desc_eng,amf.currency_code,cif_address.address1_eng,
            //                cif_address.address2_eng, cif_address.address3_eng,cif_address.city_eng,amf.long_name_eng
            //                FROM   imal.cif, imal.amf, imal.cif_address,imal.gen_ledger WHERE length(amf.additional_reference) = 10 AND
            //                cif.cif_no = amf.cif_sub_no and amf.cif_sub_no = cif_address.cif_no and amf.gl_code = gen_ledger.gl_code
            //                and amf.cif_sub_no IN (SELECT amf.cif_sub_no FROM imal.amf WHERE additional_reference = '" + acc + "')";

            string sql = @"SELECT amf.currency_code, cif.country, amf.sl_no,CIF_address.mobile,CIF_address.tel, cif.work_tel, amf.branch_code, amf.gl_code, gen_ledger.brief_desc_eng,
                            amf.cif_sub_no, amf.additional_reference,amf.cv_avail_bal * -1 as cv_avail_bal, amf.brief_name_eng, CIF_address.email,
                            amf.long_name_eng, cif.address1_eng, cif.address2_eng, cif.address3_eng, cif.status,cif_address.city_eng,
                            amf.description, cif.birth_date, cif.sexe, cif.card_name, id_no2 FROM
                            imal.cif, imal.amf, imal.CIF_address, imal.gen_ledger WHERE 
                            length(amf.additional_reference) = 10 and cif.cif_no = amf.cif_sub_no and amf.cif_sub_no = CIF_address.CIF_NO 
							and amf.gl_code = gen_ledger.gl_code and amf.comp_code = gen_ledger.comp_code and amf.cif_sub_no in 
                            (SELECT amf.cif_sub_no FROM imal.amf WHERE additional_reference = '" + acc + "')";

            OraConn conn = new OraConn(sql, "imalOraConn");
            irecs = conn.query("recs");
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return irecs;
    }
    //Get Card Names,
    public string[] GetCardNames(string name1, string name2 = "")
    {
        var names = new string[4];
        names[0] = string.Empty; names[1] = string.Empty; names[2] = string.Empty; names[3] = string.Empty;
        try
        {
            if (name1 != "")
            {
                string[] namees = name1.Split(' ');
                if (namees.Length > 1)
                {
                    if (namees[0].Length > 25)
                    {
                        names[0] = namees[0].Substring(0, 25);
                    }
                    else
                    {
                        names[0] = namees[0];
                    }

                    if (namees[1].Length > 15)
                    {
                        names[1] = namees[1].Substring(0, 1);
                    }
                    else
                    {
                        names[1] = namees[1];
                    }

                    try
                    {
                        if (namees[2] != "")
                        {
                            if (namees[2].Length > 25)
                            {
                                names[2] = namees[2].Substring(0, 1);
                            }
                            else
                            {
                                names[2] = namees[2];
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
                else
                {
                    if (namees[0].Length > 25)
                    {
                        names[0] = namees[0].Substring(0, 25);
                    }
                    else
                    {
                        names[0] = namees[0];
                    }
                }
            }
            if (name2 != "")
            {
                string[] namees = name2.Split(' ');
                if (namees.Length > 1)
                {
                    if (namees[0].Length > 25)
                    {
                        names[1] = namees[0].Substring(0, 25);
                    }
                    else
                    {
                        names[1] = namees[0];
                    }

                    if (namees[1].Length > 15)
                    {
                        names[2] = namees[1].Substring(0, 1);
                    }
                    else
                    {
                        names[2] = namees[1];
                    }
                }
                else
                {
                    if (namees[0].Length > 25)
                    {
                        names[1] = namees[0].Substring(0, 25);
                    }
                    else
                    {
                        names[1] = namees[0];
                    }
                }
            }


            string allNames = names[0] + " " + names[2] + " " + names[1];

            if (allNames.Length > 25)
            {
                allNames = names[0] + " " + names[1];

                if (allNames.Length > 25)
                {
                    allNames = names[0] + " " + names[1].Substring(0, 1);

                    if (allNames.Length > 25)
                    {
                        allNames = names[0];
                    }
                    else
                    { names[3] = allNames; }
                }
                else
                { names[3] = allNames; }
            }
            else
            { names[3] = allNames; }            
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return names;
    }
    //Get Active Card Details
    public string GetActiveCards(string account)
    {
        var cardData = "00|00|00";
        string exp = DateTime.Now.ToString("yyMM");

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.customer_id FROM pc_cards c INNER JOIN pc_card_accounts a ON c.pan = a.pan AND c.seq_nr = a.seq_nr INNER JOIN pc_card_programs p ON c.card_program = p.card_program " +
                         "WHERE account_id = @account AND a.date_deleted IS NULL AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@account", account);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string customer_id = dr["customer_id"].ToString();

                        if (i > 0)
                        {
                            cardData += "~";
                        }

                        cardData += pan + "|" + expiry_date + "|" + customer_id;
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }

    //Get Active Card Details
    public string GetValidCards(string account)
    {
        var cardData = "00|00|00";
        string exp = DateTime.Now.ToString("yyMM");

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.customer_id FROM pc_cards_1_A c INNER JOIN pc_card_accounts_1_A a ON c.pan = a.pan AND c.seq_nr = a.seq_nr " +
                         "WHERE account_id = @account AND a.date_deleted IS NULL AND c.card_status = '0' AND c.hold_rsp_code IS NULL AND expiry_date >= @exp; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@account", account);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string customer_id = dr["customer_id"].ToString();

                        if (i > 0)
                        {
                            cardData += "~";
                        }

                        cardData += pan + "|" + expiry_date + "|" + customer_id;
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }

	public string GetValidImalCardsByCustomerID(string customer_id)
    {
        var cardData = "00|00|00|00";
        string exp = DateTime.Now.ToString("yyMM");
        string cus_Id = customer_id;
        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }
        try
        {
            string sql = "SELECT distinct(c.pan),c.expiry_date,c.seq_nr,c.card_program FROM pc_cards c INNER JOIN pc_card_accounts a ON c.pan = a.pan AND c.seq_nr = a.seq_nr " +
                         "WHERE c.customer_id IN (@customer_id,@cus_Id) AND a.date_deleted IS NULL AND c.card_status = '0' AND c.hold_rsp_code IS NULL AND expiry_date >= @exp AND (LEFT(c.PAN,8) in ('53347750','53347751','52549550','51587250','51628550','52206650') OR LEFT(c.PAN,6) in ('506162')) AND c.issuer_nr in (11,9,12) AND left(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string card_program = dr["card_program"].ToString();

                        if (i > 0)
                        {
                            cardData += "~";
                        }

                        cardData += pan + "|" + expiry_date + "|" + seq_nr + "|" + card_program;
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
	public string GetValidCardsByCustomerID(string customer_id)
    {
        var cardData = "00|00|00|00";
        string exp = DateTime.Now.ToString("yyMM");
        string cus_Id = customer_id;
        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }
        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards c INNER JOIN pc_card_accounts a ON c.pan = a.pan AND c.seq_nr = a.seq_nr " +
                         "WHERE c.customer_id IN (@customer_id,@cus_Id) AND a.date_deleted IS NULL AND c.card_status = '0' AND c.hold_rsp_code IS NULL AND expiry_date >= @exp AND left(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string card_program = dr["card_program"].ToString();

                        if (i > 0)
                        {
                            cardData += "~";
                        }

                        cardData += pan + "|" + expiry_date + "|" + seq_nr + "|" + card_program;
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
	
    //Get Active Card Details
    public string GetActiveCardsByCustomerId(string customer_id)
    {
        var cardData = "";
        string exp = DateTime.Now.ToString("yyMM");
        var cus_Id = customer_id;
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("count", typeof(int));
        crds.Columns.Add("date", typeof(DateTime));

        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }
        DataTable rrr = new DataTable();
        try
        {
            var data = GetCustomerActiveCards2(customer_id, cus_Id, exp);
            if (data.Rows.Count > 0)
            {
                rrr.Merge(data);
            }
            //string[] issuers = (ConfigurationManager.AppSettings["allowedIssuer"]).Split(';');
            //if (issuers.Length > 0)
            //{

            //    cardData = "";
            //    for (int i = 0; i < issuers.Length; i++)
            //    {
            //        string issuer = issuers[i];

            //        var data = GetActiveCardsPerIssuerByCustomer2(customer_id, cus_Id, issuer, exp);

            //        if (data.Rows.Count > 0)
            //        {
            //            rrr.Merge(data);
            //            //if (cardData != "")
            //            //{
            //            //    cardData += "~" + data;
            //            //}
            //            //else
            //            //{
            //            //    cardData += data;
            //            //}
            //        }
            //    }
            //}

            if (rrr.Rows.Count > 0)
            {
                DataView dv = rrr.DefaultView;
                dv.Sort = "date desc";
                DataTable sortedDT = dv.ToTable();

                for (int i = 0; i < sortedDT.Rows.Count; i++)
                {
                    DataRow fr = sortedDT.Rows[i];
                    if (cardData != "")
                    {
                        cardData += "~" + fr[0];
                    }
                    else
                    {
                        cardData += fr[0];
                    }
                }
            }
            //string sql = "SELECT pan,expiry_date,seq_nr FROM pc_cards_1_A WHERE customer_id IN (@customer_id,@cus_Id) " +
            //             "AND card_status = '1' AND hold_rsp_code IS NULL AND expiry_date >= @exp; ";
            //Connect cn = new Connect("PostCard")
            //{
            //    Persist = true
            //};
            //cn.SetSQL(sql);
            //cn.AddParam("@customer_id", customer_id);
            //cn.AddParam("@cus_Id", cus_Id);
            //cn.AddParam("@exp", exp);
            //DataSet ds = cn.Select();
            //cn.CloseAll();

            //bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            //if (hasTables)
            //{
            //    int rws = ds.Tables[0].Rows.Count;

            //    if (rws > 0)
            //    {
            //        cardData = "";
            //        for (int i = 0; i < rws; i++)
            //        {
            //            DataRow dr = ds.Tables[0].Rows[i];
            //            string pan = dr["pan"].ToString();
            //            string expiry_date = dr["expiry_date"].ToString();
            //            string seq_nr = dr["seq_nr"].ToString();

            //            if (i > 0)
            //            {
            //                cardData += "~";
            //            }

            //            cardData += pan + "|" + expiry_date + "|" + seq_nr;
            //        }
            //    }
            //}
        }
        catch (Exception ex)
        { logger.Error(ex); }

        if (cardData == "")
        {
            cardData = "00|00|00";
        }
		logger.Error("GetActiveCardsByCustomerId - Customer card details by customer_id: " + customer_id + " is " + mask(cardData));
        return cardData;
    }
	
	private string mask(string input)
    {
        string output = "";
        try
        {
            if (input.Contains("|"))
            {
                var splitOutput = input.Split('|');
                foreach (string obj in splitOutput)
                {
                    if (obj.Contains('~'))
                    {
                        var splitOutput2 = input.Split('~');
                        foreach (string obj2 in splitOutput2)
                        {
                            if (obj2.Contains("|"))
                            {
                                var splitOutput3 = obj2.Split('|');
                                foreach (string objj in splitOutput3)
                                {
                                    string chk2 = objj;
                                    if (objj.Length == 16 || objj.Length == 19)
                                    {
                                        chk2 = "";
                                        chk2 = objj.Substring(0, 6) + "******" + objj.Substring(objj.Length - 4, 4);
                                    }
                                    output += chk2 + "~";
                                }
                            } 
                        }
                        output.Remove(output.Length - 1, 1);
                    }
                    else
                    {
                        string chk = obj;
                        if (obj.Length == 16 || obj.Length == 19)
                        {
                            chk = "";
                            chk = obj.Substring(0, 6) + "******" + obj.Substring(obj.Length - 4, 4);
                        }
                        output += chk + "|";
                    }
                }
                output.Remove(output.Length - 1, 1);
            }
        }
        catch (Exception ex) 
        {
            logger.Error("Exception in method mask");
            output = "";
        }
        return output;
    }

    public string GetAllActiveCardsByCustomerId(string customer_id)
    {
        var cardData = "";
        string exp = DateTime.Now.ToString("yyMM");
        var cus_Id = customer_id;
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("count", typeof(int));
        crds.Columns.Add("date", typeof(DateTime));

        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }
        DataTable rrr = new DataTable();
        try
        {
            var data = GetCustomerActiveCards2(customer_id, cus_Id, exp);
            if (data.Rows.Count > 0)
            {
                rrr.Merge(data);
            }
            //string[] issuers = (ConfigurationManager.AppSettings["allowedIssuer"]).Split(';');
            //if (issuers.Length > 0)
            //{

            //    cardData = "";
            //    for (int i = 0; i < issuers.Length; i++)
            //    {
            //        string issuer = issuers[i];

            //        var data = GetActiveCardsPerIssuerByCustomer2(customer_id, cus_Id, issuer, exp);

            //        if (data.Rows.Count > 0)
            //        {
            //            rrr.Merge(data);
            //        }
            //    }
            //}

            if (rrr.Rows.Count > 0)
            {
                DataView dv = rrr.DefaultView;
                dv.Sort = "date desc";
                DataTable sortedDT = dv.ToTable();

                for (int i = 0; i < sortedDT.Rows.Count; i++)
                {
                    DataRow fr = sortedDT.Rows[i];
                    if (cardData != "")
                    {
                        cardData += "~" + fr[0];
                    }
                    else
                    {
                        cardData += fr[0];
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }

        var empCards = GetEMPCards(customer_id, cus_Id);

        if (empCards != "")
        {
            cardData += empCards;
        }

        if (cardData == "")
        {
            cardData = "00|00|00";
        }
        return cardData;
    }
    //Get Active Card Details
    public string GetValidCardsByCustomerId(string customer_id)
    {
        var cardData = "00|00|00";
        string exp = DateTime.Now.ToString("yyMM");
        var cus_Id = customer_id;

        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }

        cardData = GetCustomerActiveCards(customer_id, cus_Id, exp);
        //string[] issuers = (ConfigurationManager.AppSettings["allowedIssuer"]).Split(';');
        //if (issuers.Length > 0)
        //{

        //    cardData = "";
        //    for (int i = 0; i < issuers.Length; i++)
        //    {
        //        string issuer = issuers[i];

        //        string data = GetValidCardsPerIssuerByCustomer(customer_id, cus_Id, issuer, exp);

        //        if (data != "00|00|00")
        //        {
        //            if (cardData != "")
        //            {
        //                cardData += "~" + data;
        //            }
        //            else
        //            {
        //                cardData += data;
        //            }
        //        }
        //    }
        //}
        if (cardData == "")
        {
            cardData = "00|00|00";
        }
        return cardData;
    }
    //Hotlist a card
    public string HotlistCard(string pan, string exp, string customer_id, string type)
    {
        string stat = ""; var rsp_code = "43";

        if (type == "1")
        {
            rsp_code = "17";
        }
        try
        {
            var cus_Id = customer_id;

            if (customer_id.Length < 7)
            {
                cus_Id = customer_id.PadLeft(7, '0');

            }

            string issuer_nr = GetCardIssuer(pan, exp);// GetIssuer(pan.Substring(0, 6));
            string sql = "SELECT seq_nr FROM pc_cards_" + issuer_nr + "_A WHERE pan= @pan AND expiry_date = @exp AND " +
                        "customer_id IN (@customer_id,@cus_id)";
            Connect cn = new Connect("PostCard")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@exp", exp);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);

            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws > 0)
                {
                    string isql = "UPDATE pc_cards_" + issuer_nr + "_A SET hold_rsp_code = @rspCode WHERE pan= @pan AND expiry_date = @exp AND " +
                            "customer_id IN (@customer_id,@cus_id)";
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@pan", pan);
                    un.AddParam("@exp", exp);
                    un.AddParam("@customer_id", customer_id);
                    un.AddParam("@cus_Id", cus_Id);
                    un.AddParam("@rspCode", rsp_code);
                    int upd = un.Update();
                    un.CloseAll();

                    if (upd > 0)
                    {
                        stat = "00";
                    }
                    else
                    {
                        stat = "ERROR HOTLISTING CARD " + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4);
                    }
                }
                else
                {
                    stat = "ERROR RETRIEVING CARD " + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " DETAILS";
                }
            }
            else
            {
                stat = "ERROR RETRIEVING CARD " + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " DETAILS"; ;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return stat;
    }
    public string DeHotlistCard(string pan, string exp, string customer_id)
    {
        string stat = "";

        try
        {
            var cus_Id = customer_id;

            if (customer_id.Length < 7)
            {
                cus_Id = customer_id.PadLeft(7, '0');

            }

            string issuer_nr = GetCardIssuer(pan, exp);// GetIssuer(pan.Substring(0, 6));
            string sql = "SELECT seq_nr FROM pc_cards_" + issuer_nr + "_A WHERE pan= @pan AND expiry_date = @exp AND " +
                        "customer_id IN (@customer_id,@cus_id)";
            Connect cn = new Connect("PostCard")
            {
                Persist = true
            };
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@exp", exp);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);

            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws > 0)
                {
                    string isql = "UPDATE pc_cards_" + issuer_nr + "_A SET hold_rsp_code = NULL WHERE pan= @pan AND expiry_date = @exp AND " +
                            "customer_id IN (@customer_id,@cus_id)";
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@pan", pan);
                    un.AddParam("@exp", exp);
                    un.AddParam("@customer_id", customer_id);
                    un.AddParam("@cus_Id", cus_Id);
                    int upd = un.Update();
                    un.CloseAll();

                    if (upd > 0)
                    {
                        stat = "00";
                    }
                    else
                    {
                        stat = "ERROR HOTLISTING CARD " + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4);
                    }
                }
                else
                {
                    stat = "ERROR RETRIEVING CARD " + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " DETAILS";
                }
            }
            else
            {
                stat = "ERROR RETRIEVING CARD " + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " DETAILS"; ;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return stat;
    }
	//Nibss POS Feed Get POSTransactionsbyterminal
	public List<NIBSSFeedDto> GetPOSTransByTerminal (string terminal,DateTime startDate, DateTime endDate)
    {
        NIBSSFeedDto _NibssFeed = new NIBSSFeedDto();
        List<NIBSSFeedDto> _NibssFeedList = new List<NIBSSFeedDto>();
        string sDate = startDate.Year.ToString().PadLeft(4,'0') +"-"+ startDate.Month.ToString().PadLeft(2, '0') + "-" + startDate.Day.ToString().PadLeft(2, '0');
        string eDate = endDate.Year.ToString().PadLeft(4, '0') + "-" + endDate.Month.ToString().PadLeft(2, '0') + "-" + endDate.Day.ToString().PadLeft(2, '0');

        try
        {
            string query = "SELECT b.TRANSACTION_TIMESTAMP as Transaction_Time,b.PRIMARY_ACCOUNT_NUMBER as Pan,b.A041_CARD_ACCEPTOR_TERMINAL_ID as Terminal_ID,b.A102_ACCOUNT_IDENT_1 as Account_No,b.TRANSACTION_AMOUNT as Transaction_Amount,b.AMOUNT_SIGNAL as Tran_Amount_Signal,b.FEED_TIMESTAMP as Feed_Timestamp,b.A090_ORIG_TRANSM_DATE_TIME as Transmission_Time,a.CHANNEL_MESSAGE_CODE as Message_Req,b.CHANNEL_MESSAGE_CODE as Message_Rsp,a.CLEARING_PERIOD as Clearing_Period,a.TRANSACTION_ID as Transaction_ID,a.A090_ORIG_SYS_TRACE_AUDIT_NR as System_Trace_Audit_Nr_Req,b.A011_SYSTEM_TRACE_AUDIT_NUMBER as System_Trace_Audit_Nr_Res,a.RETRIEVAL_REFERENCE_NUMBER as Retrieval_Ref_Nr_Req,b.RETRIEVAL_REFERENCE_NUMBER as Retrieval_Ref_Nr_Res,a.A043_CARD_ACC_NME_LOC_8583 as Card_Acceptor_Name_Loc,b.A041_CARD_ACCEPTOR_TERMINAL_ID as Terminal_ID,b.AUTH_RESPONSE_CODE as Response_Code,b.A028_TRANSACTION_FEE_AMOUNT as Surcharge,b.A028_TRANSACTION_FEE_SIGNAL as Surcharge_Signal,b.A090_ORIG_ACQNG_INST_ID_CODE as Acq_Inst_ID,b.A123_POS_DATA_CODE_8583 as Pos_Data_Code,a.CARD_EXPIRATION_DATE as Card_Expiry,b.MERCHANT_CATEGORY_CODE as Merchant_Category_Code,a.TERMINAL_TYPE as Terminal_Type,a.TRANSACTION_CURRENCY_CODE as Currency_Code,b.BANK_CODE as Bank_Code,b.FWD_INSTITUTION_ID_CODE as Fwd_Inst_ID FROM [SterlingPOST24].[dbo].[FinChannelRequest] a with (nolock) inner join [SterlingPOST24].[dbo].[FinChannelResponse] b with (nolock) on a.CLEARING_PERIOD = b.CLEARING_PERIOD and a.TRANSACTION_ID = b.TRANSACTION_ID where AMOUNT_SIGNAL = 'D' and b.A041_CARD_ACCEPTOR_TERMINAL_ID = '"+terminal+"' and b.TRANSACTION_TIMESTAMP between '"+sDate+" 00:00:00.000' and '"+eDate+" 23:59:59.997' and b.AUTH_RESPONSE_CODE = '00'";
            //and left(b.PRIMARY_ACCOUNT_NUMBER, 6) = '533477'
            SqlDataReader dr;
            var _configuration = ConfigurationManager.AppSettings["SQLDB6"];
            using (SqlConnection connect = new SqlConnection(_configuration))
            {
                using (SqlCommand cmd = new SqlCommand(query, connect))
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    //cmd.CommandTimeout = 30000;
                    if (connect.State != ConnectionState.Open)
                    {
                        connect.Open();
                    }
                    dr = cmd.ExecuteReader();
                    while (dr.Read())
                    {
                        _NibssFeed = new NIBSSFeedDto
                        {
                            AccountNo = dr["Account_No"].ToString(),
                            AcqInstitutionID = dr["Acq_Inst_ID"].ToString(),
                            BankCode = dr["Bank_Code"].ToString(),
                            CardAcceptorNameLoc = dr["Card_Acceptor_Name_Loc"].ToString(),
                            CardExpiry = Convert.ToInt32(dr["Card_Expiry"].ToString()),
                            ClearingPeriod = dr["Clearing_Period"].ToString(),
                            CurrencyCode = Convert.ToInt32(dr["Currency_Code"].ToString()),
                            FeedTimeStamp = Convert.ToDateTime(dr["Feed_Timestamp"].ToString()),
                            FwdInstitutionID = dr["Fwd_Inst_ID"].ToString(),
                            MerchantCatCode = dr["Merchant_Category_Code"].ToString(),
                            MessageReq = dr["Message_Req"].ToString(),
                            MessageRes = dr["Message_Rsp"].ToString(),
                            Pan = dr["Pan"].ToString(),
                            PosDataCode = dr["Pos_Data_Code"].ToString(),
                            ResponseCode = dr["Response_Code"].ToString(),
                            RRNReq = dr["Retrieval_Ref_Nr_Req"].ToString(),
                            RRNRes = dr["Retrieval_Ref_Nr_Res"].ToString(),
                            StanNrReq = dr["System_Trace_Audit_Nr_Req"].ToString(),
                            StanNrRes = dr["System_Trace_Audit_Nr_Res"].ToString(),
                            Surcharge = Convert.ToDouble(dr["Surcharge"].ToString()),
                            SurchargeSignal = dr["Surcharge_Signal"].ToString(),
                            TerminalID = dr["Terminal_ID"].ToString(),
                            TerminalType = dr["Terminal_Type"].ToString(),
                            TranAmount = Convert.ToDouble(dr["Transaction_Amount"].ToString()),
                            TranAmountSignal = dr["Tran_Amount_Signal"].ToString(),
                            TransactionID = dr["Transaction_ID"].ToString(),
                            TransactionTime = Convert.ToDateTime(dr["Transaction_Time"].ToString()),
                            TransmissionTime = Convert.ToDateTime(dr["Transmission_Time"].ToString())
                        };
                        _NibssFeedList.Add(_NibssFeed);
                    }
                    cmd.Dispose();
                }
                connect.Dispose();
                connect.Close();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.Message);
        }
        return _NibssFeedList;
    }
    private string CalPOSTranFeePercent (string catCode)
    {
        string fee = string.Empty;
        //POSFeeSettledAmt _posFeeSet = new POSFeeSettledAmt();
        try
        {
            string query = "select MCC_CODE,CHARGE_PERCENT from [SterlingPOST24].[dbo].[Merchant_Category_Fee] where MCC_CODE = '" + catCode + "'";
            SqlDataReader dr;
            var _configuration = ConfigurationManager.AppSettings["SQLDB6"];
            using (SqlConnection connect = new SqlConnection(_configuration))
            {
                using (SqlCommand cmd = new SqlCommand(query, connect))
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    //cmd.CommandTimeout = 30000;
                    if (connect.State != ConnectionState.Open)
                    {
                        connect.Open();
                    }
                    dr = cmd.ExecuteReader();
                    while (dr.Read())
                    {
                        fee = dr["CHARGE_PERCENT"].ToString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error("In CalPOSTranFeeSettledAmount: " + ex.Message);
        }
        return fee;
    }

    public List<NIBSSFeedDto> GetPOSTransByAccountNo(string accountNo, DateTime startDate, DateTime endDate)
    {
        NIBSSFeedDto _NibssFeed = new NIBSSFeedDto();
        List<NIBSSFeedDto> _NibssFeedList = new List<NIBSSFeedDto>();
        string sDate = startDate.Year.ToString().PadLeft(4, '0') + "-" + startDate.Month.ToString().PadLeft(2, '0') + "-" + startDate.Day.ToString().PadLeft(2, '0');
        string eDate = endDate.Year.ToString().PadLeft(4, '0') + "-" + endDate.Month.ToString().PadLeft(2, '0') + "-" + endDate.Day.ToString().PadLeft(2, '0');

        try
        {
            string query = "SELECT c.Merchant_Name as Merchant_Name,c.ADDRESS as Merchant_Address,c.PHONE as Merchant_Phone_No, c.AccountNo2 as Merchant_Account_No,b.TRANSACTION_TIMESTAMP as Transaction_Time,b.PRIMARY_ACCOUNT_NUMBER as Pan,b.A041_CARD_ACCEPTOR_TERMINAL_ID as Terminal_ID,b.A102_ACCOUNT_IDENT_1 as Customer_Account_No,b.TRANSACTION_AMOUNT as Transaction_Amount,b.AMOUNT_SIGNAL as Tran_Amount_Signal,b.FEED_TIMESTAMP as Feed_Timestamp,b.A090_ORIG_TRANSM_DATE_TIME as Transmission_Time,a.CHANNEL_MESSAGE_CODE as Message_Req,b.CHANNEL_MESSAGE_CODE as Message_Rsp,a.CLEARING_PERIOD as Clearing_Period,a.TRANSACTION_ID as Transaction_ID,a.A090_ORIG_SYS_TRACE_AUDIT_NR as System_Trace_Audit_Nr_Req,b.A011_SYSTEM_TRACE_AUDIT_NUMBER as System_Trace_Audit_Nr_Res,a.RETRIEVAL_REFERENCE_NUMBER as Retrieval_Ref_Nr_Req,b.RETRIEVAL_REFERENCE_NUMBER as Retrieval_Ref_Nr_Res,a.A043_CARD_ACC_NME_LOC_8583 as Card_Acceptor_Name_Loc,b.A041_CARD_ACCEPTOR_TERMINAL_ID as Terminal_ID,b.AUTH_RESPONSE_CODE as Response_Code,b.A028_TRANSACTION_FEE_AMOUNT as Surcharge,b.A028_TRANSACTION_FEE_SIGNAL as Surcharge_Signal,b.A090_ORIG_ACQNG_INST_ID_CODE as Acq_Inst_ID,b.A123_POS_DATA_CODE_8583 as Pos_Data_Code,a.CARD_EXPIRATION_DATE as Card_Expiry,b.MERCHANT_CATEGORY_CODE as Merchant_Category_Code,a.TERMINAL_TYPE as Terminal_Type,a.TRANSACTION_CURRENCY_CODE as Currency_Code,b.BANK_CODE as Bank_Code,b.FWD_INSTITUTION_ID_CODE as Fwd_Inst_ID FROM [SterlingPOST24].[dbo].[FinChannelRequest] a with (nolock) inner join [SterlingPOST24].[dbo].[FinChannelResponse] b with (nolock) on a.CLEARING_PERIOD = b.CLEARING_PERIOD and a.TRANSACTION_ID = b.TRANSACTION_ID join [SterlingPOST24].[dbo].[Sterling_Merchant_list] c with (nolock) on c.Terminal_id = b.A041_CARD_ACCEPTOR_TERMINAL_ID where AMOUNT_SIGNAL = 'D' and c.AccountNo2 = '" + accountNo + "' and b.TRANSACTION_TIMESTAMP between '" + sDate + " 00:00:00.000' and '" + eDate + " 23:59:59.997' and b.AUTH_RESPONSE_CODE = '00' order by b.TRANSACTION_TIMESTAMP desc";
            //and left(b.PRIMARY_ACCOUNT_NUMBER, 6) = '533477'
            SqlDataReader dr;
            var _configuration = ConfigurationManager.AppSettings["SQLDB6"];
            using (SqlConnection connect = new SqlConnection(_configuration))
            {
                using (SqlCommand cmd = new SqlCommand(query, connect))
                {
                    cmd.CommandType = System.Data.CommandType.Text;
                    //cmd.CommandTimeout = 30000;
                    if (connect.State != ConnectionState.Open)
                    {
                        connect.Open();
                    }
                    dr = cmd.ExecuteReader();
                    while (dr.Read())
                    {
                        string fee = string.Empty;
                        string amt = string.Empty;
                        string _amt = string.Empty;
                        string _fee = string.Empty;
                        string mcc = dr["Merchant_Category_Code"].ToString().Trim();
                        fee = CalPOSTranFeePercent(mcc);
                        //double dFee = Convert.ToDouble(fee);
                        //double dAmt = Convert.ToDouble(amt);
                        amt = dr["Transaction_Amount"].ToString();
                        //consider these category codes with flat fee of 100 naira 4511,4582,4722
                        if (mcc.Contains("4511" )|| mcc.Contains("4582")||mcc.Contains("4722"))
                        {
                            _amt = (Convert.ToDouble(amt) - 100).ToString();
                            _fee = "100";
                        }
                        else 
                        { 
                            _amt = (Convert.ToDouble(amt) - ((Convert.ToDouble(fee) / 100) * Convert.ToDouble(amt))).ToString();
                            _fee = (Convert.ToDouble(fee) / 100 * Convert.ToDouble(amt)).ToString();
                        }
                        _NibssFeed = new NIBSSFeedDto
                        {
                            AccountNo = dr["Customer_Account_No"].ToString(),
                            AcqInstitutionID = dr["Acq_Inst_ID"].ToString(),
                            BankCode = dr["Bank_Code"].ToString(),
                            CardAcceptorNameLoc = dr["Card_Acceptor_Name_Loc"].ToString(),
                            CardExpiry = Convert.ToInt32(dr["Card_Expiry"].ToString()),
                            ClearingPeriod = dr["Clearing_Period"].ToString(),
                            CurrencyCode = Convert.ToInt32(dr["Currency_Code"].ToString()),
                            FeedTimeStamp = Convert.ToDateTime(dr["Feed_Timestamp"].ToString()),
                            FwdInstitutionID = dr["Fwd_Inst_ID"].ToString(),
                            MerchantCatCode = dr["Merchant_Category_Code"].ToString(),
                            MerchantAcctNo = dr["Merchant_Account_No"].ToString(),
                            MerchantAddress = dr["Merchant_Address"].ToString(),
                            MerchantName = dr["Merchant_Name"].ToString(),
                            MerchantPhoneNo = dr["Merchant_Phone_No"].ToString(),
                            MessageReq = dr["Message_Req"].ToString(),
                            MessageRes = dr["Message_Rsp"].ToString(),
                            Pan = dr["Pan"].ToString(),
                            PosDataCode = dr["Pos_Data_Code"].ToString(),
                            ResponseCode = dr["Response_Code"].ToString(),
                            RRNReq = dr["Retrieval_Ref_Nr_Req"].ToString(),
                            RRNRes = dr["Retrieval_Ref_Nr_Res"].ToString(),
                            StanNrReq = dr["System_Trace_Audit_Nr_Req"].ToString(),
                            StanNrRes = dr["System_Trace_Audit_Nr_Res"].ToString(),
                            Surcharge = Convert.ToDouble(dr["Surcharge"].ToString()),
                            SurchargeSignal = dr["Surcharge_Signal"].ToString(),
                            TerminalID = dr["Terminal_ID"].ToString(),
                            TerminalType = dr["Terminal_Type"].ToString(),
                            TranAmount = Convert.ToDouble(dr["Transaction_Amount"].ToString()),
                            TransactionFee = _fee,
                            SettledAmount = _amt,
                            TranAmountSignal = dr["Tran_Amount_Signal"].ToString(),
                            TransactionID = dr["Transaction_ID"].ToString(),
                            TransactionTime = Convert.ToDateTime(dr["Transaction_Time"].ToString()),
                            TransmissionTime = Convert.ToDateTime(dr["Transmission_Time"].ToString())
                        };
                        _NibssFeedList.Add(_NibssFeed);
                    }
                    cmd.Dispose();
                }
                connect.Dispose();
                connect.Close();
            }
        }
        catch (Exception ex)
        {
            logger.Error("In GetPOSTransByAccountNo: " + ex.Message);
        }
        return _NibssFeedList;
    }
	
    //Create MVisa Records
    public string[] CreateMVisaCards(string refId, string account)
    {
        account = (account.Trim()).PadLeft(10, '0');
        bool stat = true; var insResponse = new string[3];
        string[] mVisaData = CreateMVisaPAN(refId, account);
        string pan = mVisaData[0];
        if (pan != "0")
        {
            string seq = mVisaData[1];
            string exp = mVisaData[2];
            string cardProg = mVisaData[3]; //"SBPMVISA";
            string user = "mVisa";

            var customer_Id = string.Empty; var branch = string.Empty; var initials = string.Empty; var lastName = string.Empty;
            var firstName = string.Empty; var nameOnCard = string.Empty; var address = string.Empty; var city = string.Empty;
            var region = string.Empty; var country = string.Empty; var accType = string.Empty; var cardStat = "1";
            var pvv = string.Empty; var curCode = string.Empty; var title = string.Empty; var disc = string.Empty;
            var pvki = string.Empty; string custId = string.Empty;

            try
            {
                DataSet accInfo = eacbs.getAccountFullInfo(account);
                bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

                if (hasTables)
                {
                    DataRow dr = accInfo.Tables[0].Rows[0];
                    customer_Id = dr["CUS_NUM"].ToString();
                    branch = dr["BRA_CODE"].ToString();
                    string accountGrp = dr["AccountGroup"].ToString();
                    accType = ISOAcctType(accountGrp.ToUpper());
                    curCode = dr["T24_CUR_CODE"].ToString();

                    DataSet cusInfo = eacbs.getCustomrInfo(customer_Id);
                    bool hasData = cusInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
                    if (hasData)
                    {
                        DataRow drs = cusInfo.Tables[0].Rows[0];
                        address = drs["Street"].ToString() + " " + drs["Address"].ToString();
                        city = drs["City"].ToString();
                        region = drs["State"].ToString();
                        country = drs["ResidenceCode"].ToString();
                        title = dr["CourtesyTitle"].ToString();

                        string[] otherNames = GetCardNames(dr["NAME_LINE2"].ToString(), dr["NAME_LINE1"].ToString());

                        lastName = otherNames[0]; initials = otherNames[2]; firstName = otherNames[1];
                        nameOnCard = otherNames[3];


                        var cardProgDet = GetCardProgDet(cardProg);
                        string issuer_nr = cardProgDet[5];//;GetIssuer(pan.Substring(0, 6));

                        pvki = cardProgDet[4]; disc = cardProgDet[3];
                        var pvvData = GetPINOffSet(pan, exp, cardProgDet[1], cardProgDet[0]);
                        pvv = pvvData[1];
                        string insCustomer = InsertCustomer(issuer_nr, customer_Id, title, firstName, initials, lastName, nameOnCard, address, address, city, region, country, user, "0");
                        if (insCustomer != "")
                        {
                            customer_Id = insCustomer;
                            int insAcc = InsertAccount(issuer_nr, account, accType, curCode, user);
                            if (insAcc > 0)
                            {
                                int insCusAcc = InsertCustomerAccount(issuer_nr, customer_Id, account, accType, user);
                                if (insCusAcc > 0)
                                {
                                    int insCard = InsertCardsWithRef(issuer_nr, pan, seq, cardProg, accType, cardStat, exp, pvki, pvv, disc, branch, customer_Id, refId, user);
                                    if (insCard > 0)
                                    {
                                        int insCardAcc = InsertCardAccounts(issuer_nr, pan, seq, account, accType, user);
                                        if (insCardAcc > 0)
                                        {
                                            stat = true;
                                            insResponse[0] = "00|8|" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + "-" + customer_Id + " - " + account + "DETAILS INSERTED SUCCESSFULLY";
                                            insResponse[1] = pvvData[0];
                                            insResponse[2] = pan + "|" + seq + "|" + exp;
                                        }
                                        else
                                        {
                                            insResponse[0] = customer_Id + " - " + account + "|7|ERROR INSERTING CARD_ACCOUNT" + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + " DETAILS";
                                            insResponse[1] = ""; insResponse[2] = "";
                                        }
                                    }
                                    else
                                    {
                                        insResponse[0] = customer_Id + " - " + account + "|6|ERROR INSERTING CARD " + pan.Substring(0, 6) + "******" + pan.Substring(pan.Length - 4, 4) + "-" + exp + "-" + seq + " DETAILS";
                                        insResponse[1] = ""; insResponse[2] = "";
                                    }
                                }
                                else
                                {
                                    insResponse[0] = customer_Id + " - " + account + "|5|ERROR INSERTING CUSTOMER_ACCOUNT " + customer_Id + "_" + account + " DETAILS";
                                    insResponse[1] = ""; insResponse[2] = "";
                                }
                            }
                            else
                            {
                                insResponse[0] = customer_Id + " - " + account + "|4|ERROR INSERTING ACCOUNT " + account + " DETAILS";
                                insResponse[1] = ""; insResponse[2] = "";
                            }
                        }
                        else
                        {
                            insResponse[0] = customer_Id + " - " + account + "|3|ERROR INSERTING CUSTOMER " + customer_Id + " DETAILS";
                            insResponse[1] = ""; insResponse[2] = "";
                        }
                    }
                    else
                    {
                        insResponse[0] = customer_Id + "|2|ERROR RETRIEVING CUSTOMER" + customer_Id + " DETAILS";
                        insResponse[1] = ""; insResponse[2] = "";
                    }
                }
                else
                {
                    insResponse[0] = account + "|1|ERROR RETRIEVING " + account + " DETAILS";
                    insResponse[1] = ""; insResponse[2] = "";
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }
        else
        {
            insResponse[0] = account + "|1|ERROR GENERATING PAN FOR " + account + " !!!";
            insResponse[1] = ""; insResponse[2] = "0|0|0";
        }

        return insResponse;
    }
    //Create mVisa PAN

    public string[] GetCustomerDetails(string account)
    {
        string[] cusInfo = new string[5];
        cusInfo[0] = cusInfo[1] = cusInfo[2] = cusInfo[3] = cusInfo[4] = string.Empty;

        if (account.Substring(0, 2) == "05")
        {
            DataSet accInfo = GetIMALAccInfo(account);
            bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTables)
            {
                DataRow dr = accInfo.Tables[0].Rows[0];
                cusInfo[0] = dr["mobile"].ToString();
                cusInfo[1] = "Mr";
                string[] otherNames = GetCardNames(dr["LONG_NAME_ENG"].ToString());
                cusInfo[2] = otherNames[1];
                cusInfo[3] = otherNames[0];
                cusInfo[4] = dr["EMAIL"].ToString();
            }
        }
        else
        {
            DataSet accInfo = eacbs.getAccountFullInfo(account);
            bool hasTables = accInfo.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTables)
            {
                DataRow dr = accInfo.Tables[0].Rows[0];
                cusInfo[0] = dr["MOB_NUM"].ToString();
                cusInfo[1] = dr["CourtesyTitle"].ToString();
                cusInfo[2] = dr["NAME_LINE1"].ToString();
                cusInfo[3] = dr["NAME_LINE2"].ToString();
                cusInfo[4] = dr["EMAIL"].ToString();
            }
        }

        return cusInfo;
    }
    public string[] CreateMVisaPAN(string refId, string account)
    {
        string[] mvisaData = new string[5];

        long cus = InsertMData(refId, account);
        if (cus > 0)
        {
            string pan = string.Empty; string seq = "001"; string exp = "2050";

            string sql = "SELECT ofi_IIN,ofi_extension,ofi_productCode,ofi_cardprogram,ofi_discretionaryData,ofi_cardExpiry FROM tbl_cpConfig WHERE ofiName = @name";
            Connect cn = new Connect("OfiConfig");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@name", "MVISA");
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    string IIN = dr["ofi_IIN"].ToString();
                    string ext = dr["ofi_extension"].ToString();
                    var len = 7;
                    string prodCode = dr["ofi_productCode"].ToString();
                    if (ext != "")
                    {
                        len = len - ext.Length;
                    }
                    mvisaData[3] = dr["ofi_cardprogram"].ToString(); ;
                    mvisaData[4] = dr["ofi_discretionaryData"].ToString(); ;
                    pan = Generate16DigitsPAN(IIN, ext, prodCode, (cus.ToString()).PadLeft(len, '0'));

                }
                mvisaData[0] = pan;
                mvisaData[1] = seq;
                mvisaData[2] = exp;
            }
        }
        else
        {
            mvisaData[0] = "0";
            mvisaData[1] = "0";
            mvisaData[2] = "0";
        }
        return mvisaData;
    }
    public long InsertMData(string refId, string account)
    {
        long rwId = 0;

        string sql = "INSERT INTO [mVisa] (Reference,Account) output INSERTED.rec_Id VALUES (@ref,@account)";
        logger.Info("Sql to excecute is ==> " + sql);
        Connect un = new Connect("OfiConfig");
        un.Persist = true;
        un.SetSQL(sql);
        un.AddParam("@ref", refId);
        un.AddParam("@account", account);
        var rw = un.Insert();
        un.CloseAll();

        try
        {
            rwId = Convert.ToInt64(rw);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return rwId;
    }
    public string Generate16DigitsPAN(string IIN, string ext, string pc, string cn)
    {
        string pan = "a";

        string basepan = IIN + ext + pc + cn;

        //concatenate the base pan and check digit to form a 19 didgit pan
        pan = basepan + CalcLuhnDigit(basepan).ToString();

        return pan;
    }
    public string GeneratePAN(string IIN, string ext, string pc, string bc, string cn)
    {
        string pan = "a";

        string basepan = IIN + ext + pc + bc + "0" + cn;
        if (basepan.Length > 18)
        {
            basepan = IIN + ext + pc + bc + cn;
        }

        //concatenate the base pan and check digit to form a 19 didgit pan
        pan = basepan + CalcLuhnDigit(basepan).ToString();

        return pan;
    }
    //calculate the luhn digit for each pan
    public static int CalcLuhnDigit(string pan)
    {
        char[] digits = pan.ToCharArray();
        int total = 0;
        for (int i = 17; i >= 0; i--)
        {
            try
            {
                int c = Convert.ToInt16(digits[i].ToString());
                if ((i % 2) == 1)
                {
                    c = c * 2;
                    char[] m = c.ToString().ToCharArray();
                    for (int j = 0; j < m.Length; j++)
                    {
                        int n = Convert.ToInt16(m[j].ToString());
                        total += n;
                    }
                }
                else
                {
                    total += c;
                }
            }
            catch
            {
                break;
            }
        }
        int e = total % 10;
        if (e != 0)
        {
            e = 10 - e;
        }
        return e;
    }
    //Get EMP Cards
    public static string GetEMPCards(string customer_id, string cus_Id)
    {
        var crds = string.Empty;

        string sql = @"SELECT CardNumber,'001' AS seq,DateExpired,p.Name FROM Card c INNER JOIN Product p on LEFT(c.cardNumber, 6) = p.Code
                        WHERE c.Customer_Id IN (@customer_id,@cus_Id) AND p.Description != 'LEDGER' AND  Misc IN('Visa Credit', 'Visa Products') AND c.CardState = '1'
                        AND c.CardStatus = '1'";
        Connect cn = new Connect("CardReq");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@customer_id", customer_id);
        cn.AddParam("@cus_Id", cus_Id);
        DataSet ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        if (hasTables)
        {
            int rws = ds.Tables[0].Rows.Count;
            if (rws > 0)
            {
                for (int i = 0; i < rws; i++)
                {
                    DataRow dr = ds.Tables[0].Rows[i];
                    string pan = dr["CardNumber"].ToString();
                    string expiry_date = dr["DateExpired"].ToString();
                    var exp = Convert.ToDateTime(expiry_date).ToString("yyMM");
                    string seq_nr = dr["seq"].ToString();
                    string program = (dr["Name"].ToString()).ToUpper();

                    program = program.Replace(" CONTRACT", "");
                    program = program.Replace(" NAIRA", "");
                    program = program.Replace(" DOLLAR", "");
                    program = program.Replace(" (NAIRA)", "");
                    program = program.Replace(" (DOLLAR)", "");
                    program = program.Replace(' ', '_');

                    crds += "~" + pan + "|" + seq_nr + "|" + exp + "|" + program;
                }
            }
        }

        return crds;
    }
    //Get ActiveCards Per Issuer Per Customer
    public DataTable GetActiveCardsPerIssuerByCustomer2(string customer_id, string cus_Id, string issuer, string exp)
    {
        var cardData = "00|00|00";
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("Count", typeof(int));
        crds.Columns.Add("Date", typeof(DateTime));

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards_" + issuer + "_A c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        var rr = new string[3];
                        rr[0] = string.Empty; rr[1] = string.Empty;
                        rr[2] = string.Empty;
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();

                        //if (i > 0)
                        //{
                        //    cardData += "~";
                        //}

                        cardData = rr[0] = pan + "|" + expiry_date + "|" + seq_nr + "|" + program;
                        var dd = GetCardActivity(pan, seq_nr);
                        rr[1] = dd[0]; rr[2] = dd[1];
                        crds.Rows.Add(rr);
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return crds;
    }
	public string GetImalCustomerActiveCards(string customer_id, string cus_Id, string exp)
    {
        var cardData = "00|00|00";
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("Count", typeof(int));
        crds.Columns.Add("Date", typeof(DateTime));

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND (LEFT(c.PAN,8) in ('53347750','53347751') OR LEFT(c.PAN,6) in ('506162')) AND c.issuer_nr = 1 AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        var rr = new string[3];
                        rr[0] = string.Empty; rr[1] = string.Empty;
                        rr[2] = string.Empty;
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();

                        if (i > 0)
                        {
                            cardData += "~";
                        }

                        cardData += pan + "|" + expiry_date + "|" + seq_nr + "|" + program;
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    public DataTable GetCustomerActiveCards2(string customer_id, string cus_Id, string exp)
    {
        var cardData = "00|00|00";
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("Count", typeof(int));
        crds.Columns.Add("Date", typeof(DateTime));

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        var rr = new string[3];
                        rr[0] = string.Empty; rr[1] = string.Empty;
                        rr[2] = string.Empty;
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();

                        //if (i > 0)
                        //{
                        //    cardData += "~";
                        //}

                        cardData = rr[0] = pan + "|" + expiry_date + "|" + seq_nr + "|" + program;
                        var dd = GetCardActivity(pan, seq_nr);
                        rr[1] = dd[0]; rr[2] = dd[1];
                        crds.Rows.Add(rr);
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return crds;
    }
    public string GetCustomerActiveCards(string customer_id, string cus_Id, string exp)
    {
        var cardData = "00|00|00";
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("Count", typeof(int));
        crds.Columns.Add("Date", typeof(DateTime));

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        var rr = new string[3];
                        rr[0] = string.Empty; rr[1] = string.Empty;
                        rr[2] = string.Empty;
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();

                        if (i > 0)
                        {
                            cardData += "~";
                        }

                        cardData += pan + "|" + expiry_date + "|" + seq_nr + "|" + program;
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    public string GetActiveCardsPerIssuerByCustomer(string customer_id, string cus_Id, string issuer, string exp)
    {
        var cardData = "00|00|00";
        DataTable crds = new DataTable();
        crds.Columns.Add("PAN", typeof(string));
        crds.Columns.Add("Count", typeof(int));
        crds.Columns.Add("Date", typeof(DateTime));

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards_" + issuer + "_A c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        var rr = new string[3];
                        rr[0] = string.Empty; rr[1] = string.Empty;
                        rr[2] = string.Empty;
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();

                        if (i > 0)
                        {
                            cardData += "~";
                        }

                        cardData += pan + "|" + expiry_date + "|" + seq_nr + "|" + program;
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    public static string[] GetCardActivity(string pan, string seq)
    {
        var dd = new string[2];
        //var dt2 = DateTime.Now; var dt1 = dt2.AddMonths(-3);
        //string sql = @"SELECT MAX(PAN) AS PAN,MAX(SEQ_NR) AS PAN,COUNT(tran_ref_nr) AS Count,MAX(tran_local_datetime) AS Date
        //             FROM [postcard].[dbo].[pc_card_activity]
        //             WHERE PAN = @pan AND seq_nr = @seq  AND tran_local_datetime
        //            BETWEEN @dt1 AND @dt2  group by PAN";
        //Connect cn = new Connect("PostCard");
        //cn.Persist = true;
        //cn.SetSQL(sql);
        //cn.AddParam("@pan", pan);
        //cn.AddParam("@seq", seq);
        //cn.AddParam("@dt1", dt1.ToString("yyyy-MM-dd hh:mm:ss.fff"));
        //cn.AddParam("@dt2", dt2.ToString("yyyy-MM-dd hh:mm:ss.fff"));
        //DataSet ds = cn.Select();
        //cn.CloseAll();

        //bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        //if (hasTables)
        //{
        //    int rws = ds.Tables[0].Rows.Count;
        //    if (rws > 0)
        //    {
        //        DataRow dr = ds.Tables[0].Rows[0];
        //        dd[0] = dr["Count"].ToString().Trim();
        //        dd[1] = dr["Date"].ToString().Trim();
        //    }
        //    else
        //    {
        //        dd[0] = "0";
        //        dd[1] = "1900-01-01 00:00:00.000";
        //    }
        //}
        //else
        //{
        //    dd[0] = "0";
        //    dd[1] = "1900-01-01 00:00:00.000";
        //}

        return dd;
    }
    //Get ActiveCards Per Issuer Per Customer
    public string GetIBSActiveCardsPerIssuerByCustomer(string customer_id, string cus_Id, string issuer, string exp)
    {
        var cardData = "00|00|00";

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards_" + issuer + "_A c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();

                        if (i > 0)
                        {
                            cardData += ",";
                        }

                        cardData += "{\"PAN\":\"" + pan + "\",\"Expiry Date\":\"" + expiry_date + "\",\"Card Sequence\":\"" + seq_nr + "\",\"Card Program\":\"" + program + "\"}";
                    }
                    //cardData += "]";
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }

    //Get ActiveCards Per Issuer Per Customer
    public string GetJSONActiveCardsPerIssuerByCustomer(string customer_id, string cus_Id, string issuer, string exp)
    {
        var cardData = "00|00|00";

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards_" + issuer + "_A c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();
                        string acc = GetAccountsPerCards(pan, seq_nr, issuer);

                        if (i > 0)
                        {
                            cardData += ",";
                        }

                        cardData += "{\"PAN\":\"" + pan + "\",\"Expiry Date\":\"" + expiry_date + "\",\"Card Sequence\":\"" + seq_nr + "\",\"Card Program\":\"" + program + "\",\"Accounts\":\"" + acc + "\"}";
                    }
                    //cardData += "]";
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    public string GetJSONActiveCardsByCustomer(string customer_id, string cus_Id, string exp)
    {
        var cardData = "00|00|00";

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '1' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051'; ";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();
                        //string acc = GetAccountsPerCards(pan, seq_nr, issuer);
                        string acc = GetCardAcounts(pan, seq_nr);

                        if (i > 0)
                        {
                            cardData += ",";
                        }

                        cardData += "{\"PAN\":\"" + pan + "\",\"Expiry Date\":\"" + expiry_date + "\",\"Card Sequence\":\"" + seq_nr + "\",\"Card Program\":\"" + program + "\",\"Accounts\":\"" + acc + "\"}";
                    }
                    //cardData += "]";
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    //Get Valic Cards Per Issuer Per Customer
    public string GetValidCardsPerIssuerByCustomer(string customer_id, string cus_Id, string issuer, string exp)
    {
        var cardData = "00|00|00";

        try
        {
            string sql = "SELECT c.pan,c.expiry_date,c.seq_nr,c.card_program FROM pc_cards_" + issuer + "_A c INNER JOIN pc_card_programs p ON c.card_program = p.card_program" +
                        " WHERE c.customer_id IN (@customer_id,@cus_Id) AND c.card_status = '0' AND c.hold_rsp_code IS NULL AND c.expiry_date >= @exp AND p.cvv_key IS NOT NULL AND LEFT(c.PAN,6) != '628051';";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@customer_id", customer_id);
            cn.AddParam("@cus_Id", cus_Id);
            cn.AddParam("@exp", exp);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;

                if (rws > 0)
                {
                    cardData = "";
                    for (int i = 0; i < rws; i++)
                    {
                        DataRow dr = ds.Tables[0].Rows[i];
                        string pan = dr["pan"].ToString();
                        string expiry_date = dr["expiry_date"].ToString();
                        string seq_nr = dr["seq_nr"].ToString();
                        string program = dr["card_program"].ToString();

                        if (i > 0)
                        {
                            cardData += "~";
                        }

                        cardData += pan + "|" + expiry_date + "|" + seq_nr + "|" + program;
                    }
                }
            }
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    //Get Active Card Detailsstin
    public string GetLimitActiveCardsByCustomerId(string customer_id)
    {
        var cardData = "00|00|00";
        string exp = DateTime.Now.ToString("yyMM");
        var cus_Id = customer_id;

        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }

        cardData = GetCustomerActiveCards(customer_id, cus_Id, exp);
        //string[] issuers = (ConfigurationManager.AppSettings["allowedIssuer"]).Split(';');
        //if (issuers.Length > 0)
        //{

        //    cardData = "";
        //    for (int i = 0; i < issuers.Length; i++)
        //    {
        //        string issuer = issuers[i];

        //        string data = GetActiveCardsPerIssuerByCustomer(customer_id, cus_Id, issuer, exp);

        //        if (data != "00|00|00")
        //        {
        //            if (cardData != "")
        //            {
        //                cardData += "~" + data;
        //            }
        //            else
        //            {
        //                cardData += data;
        //            }
        //        }
        //    }
        //}
        if (cardData == "")
        {
            cardData = "00|00|00";
        }
        return cardData;
    }

    //Get Active Card Detailsstin
    public string GetJSONActiveCardsByCustomerId(string customer_id)
    {
        var cardData = "00|00|00";
        string exp = DateTime.Now.ToString("yyMM");
        var cus_Id = customer_id;

        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }

        cardData = "[";
        var data = GetJSONActiveCardsByCustomer(customer_id, cus_Id, exp);
        if (data != "00|00|00")
        {
            if (cardData != "[")
            {
                cardData += "," + data;
            }
            else
            {
                cardData += data;
            }
        }
        cardData += "]";

        //string[] issuers = (ConfigurationManager.AppSettings["allowedIssuer"]).Split(';');
        //if (issuers.Length > 0)
        //{


        //    //for (int i = 0; i < issuers.Length; i++)
        //    //{
        //    //    string issuer = issuers[i];

        //    //    //string data = GetIBSActiveCardsPerIssuerByCustomer(customer_id, cus_Id, issuer, exp);
        //    //    string data = GetJSONActiveCardsPerIssuerByCustomer(customer_id, cus_Id, issuer, exp);

        //    //    if (data != "00|00|00")
        //    //    {
        //    //        if (cardData != "[")
        //    //        {
        //    //            cardData += "," + data;
        //    //        }
        //    //        else
        //    //        {
        //    //            cardData += data;
        //    //        }
        //    //    }
        //    //}
        //    //cardData += "]";
        //}
        if (cardData == "")
        {
            cardData = "00|00|00";
        }
        return cardData;
    }

    //Get Active Card Detailsstin
    public string GetJSONVisaActiveCardsByCustomerId(string customer_id)
    {
        var cardData = "00|00|00";
        string exp = DateTime.Now.ToString("yyMM");
        var cus_Id = customer_id;

        if (customer_id.Length < 7)
        {
            cus_Id = customer_id.PadLeft(7, '0');
        }

        string[] issuers = (ConfigurationManager.AppSettings["VisaIssuer"]).Split(';');
        if (issuers.Length > 0)
        {

            cardData = "[";
            for (int i = 0; i < issuers.Length; i++)
            {
                string issuer = issuers[i];

                string data = GetJSONActiveCardsPerIssuerByCustomer(customer_id, cus_Id, issuer, exp);

                if (data != "00|00|00")
                {
                    if (cardData != "[")
                    {
                        cardData += "," + data;
                    }
                    else
                    {
                        cardData += data;
                    }
                }
            }
            cardData += "]";
        }
        if (cardData == "")
        {
            cardData = "00|00|00";
        }
        return cardData;
    }

    public string GetAccountsPerCards(string pan, string seq, string issuer)
    {
        var acc = string.Empty;
        string sql = "SELECT account_id FROM pc_card_accounts_" + issuer + "_A WHERE pan = @pan AND seq_nr = @seq AND date_deleted IS NULL";
        Connect cn = new Connect("PostCard");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@pan", pan);
        cn.AddParam("@seq", seq);
        DataSet ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        if (hasTables)
        {
            int rws = ds.Tables[0].Rows.Count;
            if (rws > 0)
            {
                for (int i = 0; i < rws; i++)
                {
                    DataRow dr = ds.Tables[0].Rows[i];

                    if (i > 0)
                    {
                        acc += ";";
                    }
                    acc += dr["account_id"].ToString();
                }
            }
        }

        return acc;
    }
    public string GetCardAcounts(string pan, string seq)
    {
        var acc = string.Empty;
        string sql = "SELECT account_id FROM pc_card_accounts WHERE pan = @pan AND seq_nr = @seq AND date_deleted IS NULL";
        Connect cn = new Connect("PostCard");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@pan", pan);
        cn.AddParam("@seq", seq);
        DataSet ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        if (hasTables)
        {
            int rws = ds.Tables[0].Rows.Count;
            if (rws > 0)
            {
                for (int i = 0; i < rws; i++)
                {
                    DataRow dr = ds.Tables[0].Rows[i];

                    if (i > 0)
                    {
                        acc += ";";
                    }
                    acc += dr["account_id"].ToString();
                }
            }
        }

        return acc;
    }
    //Increase Web Trxn Limit
    public string ModifyWebLimit(string pan, string seq_nr, string card_program, string limit)
    {
        string stat = string.Empty; string limUser = "limitUser";

        //Set Limit Vraiables
        string dly_nr_Purch = string.Empty; string dly_purch_amt = string.Empty; string dly_purch_off_amt = string.Empty;
        string dly_nr_with = string.Empty; string dly_cash_amt = string.Empty; string dly_cash_off_amt = string.Empty;
        string dly_cnp_amt = string.Empty; string dly_cnp_off_amt = string.Empty;
        //string dly_dep_amt = string.Empty;
        //string wkly_nr_purch = string.Empty; string wkly_purch_amt = string.Empty; string wkly_purch_off_amt = string.Empty;
        //string wkly_cash_nr = string.Empty; string wkly_cash_amt = string.Empty; string wkly_cash_off_amt = string.Empty;
        //string wkly_cnp_amt = string.Empty; string wkly_cnp_off_amt = string.Empty; string wkly_dep_amt = string.Empty;
        //string mthly_purch_nr = string.Empty; string mthly_purch_amt = string.Empty; string mthly_purch_off_amt = string.Empty;
        //string mthly_cash_nr = string.Empty; string mthly_cash_amt = string.Empty; string mthly_cash_off_amt = string.Empty;
        //string mthly_cnp_amt = string.Empty; string mthly_cnp_off_amt = string.Empty; string mthly_dep = string.Empty;
        //string overall_trxn_purch_amt = string.Empty; string off_trxn_purch_amt = string.Empty; string overall_trxn_cash_amt = string.Empty;
        //string off_trxn_cash_amt = string.Empty; string overall_trxn_cnp_amt = string.Empty; string off_trxn_cnp_amt = string.Empty;
        //string overall_trxn_dep_amt = string.Empty; string lastUpdate = string.Empty; string User = string.Empty;
        string dly_pymnt_nr = string.Empty; string dly_pymt_amt = string.Empty; string dly_pymt_off_amt = string.Empty;
        //string wkly_pymnt_nr = string.Empty; string wkly_pymt_amt = string.Empty; string wkly_pymt_off_amt = string.Empty;
        //string mthly_pymnt_nr = string.Empty; string mthly_pymt_amt = string.Empty;
        //string mthly_pymt_off_amt = string.Empty; string overall_trxn_pymt_amt = string.Empty; string off_trxn_pymt_amt = string.Empty;

        try
        {
            string sql = @"exec cm_ld_card_override_lims 1,@pan, @seq_nr, @card_program";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@seq_nr", seq_nr);
            cn.AddParam("@card_program", card_program);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws > 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    dly_nr_Purch = dr["goods_nr_trans_lim"].ToString();
                    dly_nr_with = dr["cash_nr_trans_lim"].ToString();
                    dly_cash_amt = dr["cash_lim"].ToString();
                    dly_pymnt_nr = dr["paymnt_nr_trans_lim"].ToString();

                    string isql = @"exec cm_ins_card_override_lims 1,@pan,@seq_nr,@dly_nr_Purch,@dly_purch_amt,0,@dly_nr_with,@dly_cash_amt,0,@dly_cnp_amt,
                                 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,@update_date,@update_user,@dly_pymnt_nr,@dly_pymt_amt,0,0,0,0,0,0,0,0,0";
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@pan", pan);
                    un.AddParam("@seq_nr", seq_nr);
                    un.AddParam("@dly_nr_Purch", dly_nr_Purch);
                    un.AddParam("@dly_purch_amt", limit);
                    un.AddParam("@dly_nr_with", dly_nr_with);
                    un.AddParam("@dly_cash_amt", dly_cash_amt);
                    un.AddParam("@dly_cnp_amt", limit);
                    un.AddParam("@dly_pymnt_nr", dly_pymnt_nr);
                    un.AddParam("@dly_pymt_amt", limit);
                    un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                    un.AddParam("@update_user", limUser);
                    int upd = un.Update();
                    un.CloseAll();

                    if (upd > 0)
                    {
                        stat = "00";
                    }
                    else
                    {
                        stat = "2|ERROR SETTING CARD LIMITS";
                    }
                }
                else
                {
                    stat = "1|ERROR RETRIEVING CARD LIMITS";
                }
            }
            else
            {
                stat = "1|ERROR RETRIEVING CARD LIMITS";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return stat;
    }

    //Increase Web Trxn Limit
    public string ModifyPaymentLimit(string pan, string seq_nr, string card_program, string limit)
    {
        string stat = string.Empty; string limUser = "limitUser";

        //Set Limit Vraiables
        string dly_nr_Purch = string.Empty; string dly_purch_amt = string.Empty; string dly_purch_off_amt = string.Empty;
        string dly_nr_with = string.Empty; string dly_cash_amt = string.Empty; string dly_cash_off_amt = string.Empty;
        string dly_cnp_amt = string.Empty; string dly_cnp_off_amt = string.Empty;
        //string dly_dep_amt = string.Empty;
        //string wkly_nr_purch = string.Empty; string wkly_purch_amt = string.Empty; string wkly_purch_off_amt = string.Empty;
        //string wkly_cash_nr = string.Empty; string wkly_cash_amt = string.Empty; string wkly_cash_off_amt = string.Empty;
        //string wkly_cnp_amt = string.Empty; string wkly_cnp_off_amt = string.Empty; string wkly_dep_amt = string.Empty;
        //string mthly_purch_nr = string.Empty; string mthly_purch_amt = string.Empty; string mthly_purch_off_amt = string.Empty;
        //string mthly_cash_nr = string.Empty; string mthly_cash_amt = string.Empty; string mthly_cash_off_amt = string.Empty;
        //string mthly_cnp_amt = string.Empty; string mthly_cnp_off_amt = string.Empty; string mthly_dep = string.Empty;
        //string overall_trxn_purch_amt = string.Empty; string off_trxn_purch_amt = string.Empty; string overall_trxn_cash_amt = string.Empty;
        //string off_trxn_cash_amt = string.Empty; string overall_trxn_cnp_amt = string.Empty; string off_trxn_cnp_amt = string.Empty;
        //string overall_trxn_dep_amt = string.Empty; string lastUpdate = string.Empty; string User = string.Empty;
        string dly_pymnt_nr = string.Empty; string dly_pymt_amt = string.Empty; string dly_pymt_off_amt = string.Empty;
        //string wkly_pymnt_nr = string.Empty; string wkly_pymt_amt = string.Empty; string wkly_pymt_off_amt = string.Empty;
        //string mthly_pymnt_nr = string.Empty; string mthly_pymt_amt = string.Empty;
        //string mthly_pymt_off_amt = string.Empty; string overall_trxn_pymt_amt = string.Empty; string off_trxn_pymt_amt = string.Empty;

        try
        {
            string sql = @"exec cm_ld_card_override_lims 1,@pan, @seq_nr, @card_program";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@seq_nr", seq_nr);
            cn.AddParam("@card_program", card_program);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws > 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    dly_nr_Purch = dr["goods_nr_trans_lim"].ToString();
                    dly_nr_with = dr["cash_nr_trans_lim"].ToString();
                    dly_cash_amt = dr["cash_lim"].ToString();
                    dly_pymnt_nr = dr["paymnt_nr_trans_lim"].ToString();
                    dly_cnp_amt = dr["cnp_lim"].ToString();
                    dly_purch_amt = dr["goods_lim"].ToString();

                    string isql = @"exec cm_ins_card_override_lims 1,@pan,@seq_nr,@dly_nr_Purch,@dly_purch_amt,0,@dly_nr_with,@dly_cash_amt,0,@dly_cnp_amt,
                                 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,@update_date,@update_user,@dly_pymnt_nr,@dly_pymt_amt,0,0,0,0,0,0,0,0,0";
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@pan", pan);
                    un.AddParam("@seq_nr", seq_nr);
                    un.AddParam("@dly_nr_Purch", dly_nr_Purch);
                    un.AddParam("@dly_purch_amt", dly_purch_amt);
                    un.AddParam("@dly_nr_with", dly_nr_with);
                    un.AddParam("@dly_cash_amt", dly_cash_amt);
                    un.AddParam("@dly_cnp_amt", dly_cnp_amt);
                    un.AddParam("@dly_pymnt_nr", dly_pymnt_nr);
                    un.AddParam("@dly_pymt_amt", limit);
                    un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                    un.AddParam("@update_user", limUser);
                    int upd = un.Update();
                    un.CloseAll();

                    if (upd > 0)
                    {
                        stat = "00";
                    }
                    else
                    {
                        stat = "2|ERROR SETTING CARD LIMITS";
                    }
                }
                else
                {
                    stat = "1|ERROR RETRIEVING CARD LIMITS";
                }
            }
            else
            {
                stat = "1|ERROR RETRIEVING CARD LIMITS";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return stat;
    }

    //Increase Web Trxn Limit
    public string ModifyPurchaseLimit(string pan, string seq_nr, string card_program, string limit)
    {
        string stat = string.Empty; string limUser = "limitUser";

        //Set Limit Vraiables
        string dly_nr_Purch = string.Empty; string dly_purch_amt = string.Empty; string dly_purch_off_amt = string.Empty;
        string dly_nr_with = string.Empty; string dly_cash_amt = string.Empty; string dly_cash_off_amt = string.Empty;
        string dly_cnp_amt = string.Empty; string dly_cnp_off_amt = string.Empty;
        //string dly_dep_amt = string.Empty;
        //string wkly_nr_purch = string.Empty; string wkly_purch_amt = string.Empty; string wkly_purch_off_amt = string.Empty;
        //string wkly_cash_nr = string.Empty; string wkly_cash_amt = string.Empty; string wkly_cash_off_amt = string.Empty;
        //string wkly_cnp_amt = string.Empty; string wkly_cnp_off_amt = string.Empty; string wkly_dep_amt = string.Empty;
        //string mthly_purch_nr = string.Empty; string mthly_purch_amt = string.Empty; string mthly_purch_off_amt = string.Empty;
        //string mthly_cash_nr = string.Empty; string mthly_cash_amt = string.Empty; string mthly_cash_off_amt = string.Empty;
        //string mthly_cnp_amt = string.Empty; string mthly_cnp_off_amt = string.Empty; string mthly_dep = string.Empty;
        //string overall_trxn_purch_amt = string.Empty; string off_trxn_purch_amt = string.Empty; string overall_trxn_cash_amt = string.Empty;
        //string off_trxn_cash_amt = string.Empty; string overall_trxn_cnp_amt = string.Empty; string off_trxn_cnp_amt = string.Empty;
        //string overall_trxn_dep_amt = string.Empty; string lastUpdate = string.Empty; string User = string.Empty;
        string dly_pymnt_nr = string.Empty; string dly_pymt_amt = string.Empty; string dly_pymt_off_amt = string.Empty;
        //string wkly_pymnt_nr = string.Empty; string wkly_pymt_amt = string.Empty; string wkly_pymt_off_amt = string.Empty;
        //string mthly_pymnt_nr = string.Empty; string mthly_pymt_amt = string.Empty;
        //string mthly_pymt_off_amt = string.Empty; string overall_trxn_pymt_amt = string.Empty; string off_trxn_pymt_amt = string.Empty;

        try
        {
            string sql = @"exec cm_ld_card_override_lims 1,@pan, @seq_nr, @card_program";
            Connect cn = new Connect("PostCard");
            cn.Persist = true;
            cn.SetSQL(sql);
            cn.AddParam("@pan", pan);
            cn.AddParam("@seq_nr", seq_nr);
            cn.AddParam("@card_program", card_program);
            DataSet ds = cn.Select();
            cn.CloseAll();

            bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
            if (hasTables)
            {
                int rws = ds.Tables[0].Rows.Count;
                if (rws > 0)
                {
                    DataRow dr = ds.Tables[0].Rows[0];
                    dly_nr_Purch = dr["goods_nr_trans_lim"].ToString();
                    dly_nr_with = dr["cash_nr_trans_lim"].ToString();
                    dly_cash_amt = dr["cash_lim"].ToString();
                    dly_pymnt_nr = dr["paymnt_nr_trans_lim"].ToString();
                    dly_cnp_amt = dr["cnp_lim"].ToString(); ;
                    dly_pymt_amt = dr["paymnt_lim"].ToString(); ;

                    string isql = @"exec cm_ins_card_override_lims 1,@pan,@seq_nr,@dly_nr_Purch,@dly_purch_amt,0,@dly_nr_with,@dly_cash_amt,0,@dly_cnp_amt,
                                 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,@update_date,@update_user,@dly_pymnt_nr,@dly_pymt_amt,0,0,0,0,0,0,0,0,0";
                    Connect un = new Connect("PostCard");
                    un.Persist = true;
                    un.SetSQL(isql);
                    un.AddParam("@pan", pan);
                    un.AddParam("@seq_nr", seq_nr);
                    un.AddParam("@dly_nr_Purch", dly_nr_Purch);
                    un.AddParam("@dly_purch_amt", limit);
                    un.AddParam("@dly_nr_with", dly_nr_with);
                    un.AddParam("@dly_cash_amt", dly_cash_amt);
                    un.AddParam("@dly_cnp_amt", dly_cnp_amt);
                    un.AddParam("@dly_pymnt_nr", dly_pymnt_nr);
                    un.AddParam("@dly_pymt_amt", dly_pymt_amt);
                    un.AddParam("@update_date", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
                    un.AddParam("@update_user", limUser);
                    int upd = un.Update();
                    un.CloseAll();

                    if (upd > 0)
                    {
                        stat = "00";
                    }
                    else
                    {
                        stat = "2|ERROR SETTING CARD LIMITS";
                    }
                }
                else
                {
                    stat = "1|ERROR RETRIEVING CARD LIMITS";
                }
            }
            else
            {
                stat = "1|ERROR RETRIEVING CARD LIMITS";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return stat;
    }

    public DataSet GetFullCountryList()
    {
        DataSet ds = new DataSet();

        string sql = @"SELECT currency_code,alpha_code,Country AS Country_Name,Alpha_2_code AS ISO_2_Code,Alpha_3_code AS ISO_3_Code 
                        FROM Currency_Map m INNER JOIN tm_currencies c ON m.Numeric_code = c.currency_code ORDER BY Country ASC";
        Connect cn = new Connect("Realtime");
        cn.Persist = true;
        cn.SetSQL(sql);
        ds = cn.Select();
        cn.CloseAll();

        return ds;
    }

    public DataSet GetCountryDataByISO2Code(string code)
    {
        DataSet ds = new DataSet();

        string sql = @"SELECT currency_code,alpha_code,Country AS Country_Name,Alpha_2_code AS ISO_2_Code,Alpha_3_code AS ISO_3_Code,
                        nr_decimals,rate AS mst_Rate 
                        FROM Currency_Map m RIGHT JOIN tm_currencies c ON m.Numeric_code = c.currency_code 
                        WHERE Alpha_2_code = @code ORDER BY Country ASC";
        Connect cn = new Connect("Realtime");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@code", code);
        ds = cn.Select();
        cn.CloseAll();

        return ds;
    }

    public DataSet GetCountryDataByCurCode(string curr)
    {
        DataSet ds = new DataSet();

        string sql = @"SELECT currency_code,alpha_code,Country AS Country_Name,Alpha_2_code AS ISO_2_Code,Alpha_3_code AS ISO_3_Code,
                        nr_decimals,rate AS mst_Rate 
                        FROM Currency_Map m RIGHT JOIN tm_currencies c ON m.Numeric_code = c.currency_code 
                        WHERE currency_code = @curr ORDER BY Country ASC";
        Connect cn = new Connect("Realtime");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@curr", curr);
        ds = cn.Select();
        cn.CloseAll();

        return ds;
    }

    public string GetJSONCardTrxnPerTerminalCurrMonth(string acc)
    {
        var trxns = string.Empty;
        DateTime now = DateTime.Now;
        var startDate = new DateTime(now.Year, now.Month, 1);
        string date1 = startDate.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string date2 = now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        string sql = @"exec sbp_getTransactionsPerTerminal @acc, @dt1, @dt2";
        //string sql = @"SELECT CASE WHEN MAX(pos_terminal_type) = '01' THEN 'POS' WHEN MAX(pos_terminal_type) = '02' THEN 'ATM' ELSE 'WEB/MOBILE' END AS Terminal,COUNT(RRN) as Trxn_Count
        //                FROM [postilion_office].[dbo].[vw_FullTrxnDet] WHERE From_Account = @acc AND rsp_code_rsp = '00'
        //                AND Transaction_Date BETWEEN @dt1 AND @dt2 GROUP BY pos_terminal_type";
        Connect cn = new Connect("OfficeConn");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@acc", acc);
        cn.AddParam("@dt1", date1);
        cn.AddParam("@dt2", date2);
        var ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        if (hasTables)
        {
            int rws = ds.Tables[0].Rows.Count;

            if (rws > 0)
            {
                trxns = "[";
                for (int i = 0; i < rws; i++)
                {
                    DataRow dr = ds.Tables[0].Rows[i];
                    string Termial = dr["Terminal"].ToString();
                    string Trxn_Count = dr["Trxn_Count"].ToString();
                    if (i == 0)
                    {
                        trxns += "{\"";
                    }
                    else if (i == (rws - 1))
                    {
                        trxns += ",";
                    }
                    else
                    {
                        trxns += "\",\"";
                    }

                    trxns += Termial + "\":\"" + Trxn_Count;
                }
                trxns += "\"}]";
            }
        }

        return trxns;
    }
    public string GetLocalLimit(string issuer, string pan, string seq, string cardProgram)
    {
        var lim = string.Empty; var exists = true;
        var purcCnt = string.Empty; var purchLim = string.Empty; var cashCnt = string.Empty; var cashLim = string.Empty;
        var cnp = string.Empty; var pymtCnt = string.Empty; var pymtLim = string.Empty;

        string sql = @"SELECT goods_nr_trans_lim AS PurchaseCount,goods_lim/100 AS PurchaseLimit,cash_nr_trans_lim AS CashWithdrawalCount,
	                    cash_lim/100 AS CashWithdrawalLimit,cnp_lim/100 AS CardNotPresentLimit,paymnt_nr_trans_lim AS PaymentCount,
	                    paymnt_lim/100 AS PaymentLimit FROM [pc_card_override_lim_" + issuer + "_A] WHERE PAN = @pan AND seq_nr = @seq AND date_deleted IS NULL;";
        Connect cn = new Connect("Postcard");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@pan", pan);
        cn.AddParam("@seq", seq);
        var ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        if (hasTables)
        {
            int rws = ds.Tables[0].Rows.Count;
            if (rws > 0)
            {
                DataRow dr = ds.Tables[0].Rows[0];
                purcCnt = dr[0].ToString(); purchLim = FormatValString(dr[1].ToString()); cashCnt = dr[2].ToString(); cashLim = FormatValString(dr[3].ToString());
                cnp = FormatValString(dr[4].ToString()); pymtCnt = dr[5].ToString(); pymtLim = FormatValString(dr[6].ToString());

            }
            else
            {
                exists = false;
            }
        }
        else
        {
            exists = false;
        }

        if (!exists)
        {
            string sqli = @"select goods_nr_trans_lim AS PurchaseCount,goods_lim/100 AS PurchaseLimit,cash_nr_trans_lim AS CashWithdrawalCount,
	                        cash_lim/100 AS CashWithdrawalLimit,cnp_lim/100 AS CardNotPresentLimit,paymnt_nr_trans_lim AS PaymentCount,
	                        paymnt_lim/100 AS PaymentLimit FROM pc_card_programs WHERE card_program = @cardProg";
            Connect cni = new Connect("Postcard");
            cni.Persist = true;
            cni.SetSQL(sqli);
            cni.AddParam("@cardProg", cardProgram);
            var dsi = cni.Select();
            cni.CloseAll();

            bool hasTablesi = dsi.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTablesi)
            {
                int rwsi = dsi.Tables[0].Rows.Count;
                if (rwsi > 0)
                {
                    DataRow dri = dsi.Tables[0].Rows[0];
                    purcCnt = dri[0].ToString(); purchLim = FormatValString(dri[1].ToString()); cashCnt = dri[2].ToString(); cashLim = FormatValString(dri[3].ToString());
                    cnp = FormatValString(dri[4].ToString()); pymtCnt = dri[5].ToString(); pymtLim = FormatValString(dri[6].ToString());
                }
            }
        }

        lim = purcCnt + "|" + purchLim + "|" + cashCnt + "|" + cashLim + "|" + cnp + "|" + pymtCnt + "|" + pymtLim;

        return lim;
    }
    //Get Foriegn Limits
    public string GetForeignLimit(string issuer, string pan, string seq, string cardProgram)
    {
        var lim = "0"; var exists = true;
        var forCnt = "0"; var forLim = "0";

        string sql = @"SELECT [limits] FROM [postcard].[dbo].[pc_card_ext_lim_" + issuer + "_A] WHERE PAN = @pan AND seq_nr = @seq AND date_deleted IS NULL;";
        Connect cn = new Connect("Postcard");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@pan", pan);
        cn.AddParam("@seq", seq);
        var ds = cn.Select();
        cn.CloseAll();

        bool hasTables = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);
        if (hasTables)
        {
            int rws = ds.Tables[0].Rows.Count;
            if (rws > 0)
            {
                DataRow dr = ds.Tables[0].Rows[0];
                var flim = dr[0].ToString();

                if (flim != "")
                {
                    var sp = flim.Split('|');

                    if (sp.Length > 0)
                    {
                        forCnt = sp[2].ToString();
                        forLim = FormatValString(sp[3].ToString());
                    }
                }
            }
            else
            {
                exists = false;
            }
        }
        else
        {
            exists = false;
        }

        if (!exists)
        {
            string sqli = @"SELECT online_nr_trans_lim AS FroeignTransactionCount,online_totals_lim / 100 AS ForeignTransactionionsLimit
                            FROM [postcard].[dbo].[pc_limits_profile_rules] r INNER JOIN [postcard].[dbo].[pc_limits_profile_assocs] p
                            ON r.limits_profile = p.limits_profile WHERE p.card_program = @cardProg";
            Connect cni = new Connect("Postcard");
            cni.Persist = true;
            cni.SetSQL(sqli);
            cni.AddParam("@cardProg", cardProgram);
            var dsi = cni.Select();
            cni.CloseAll();

            bool hasTablesi = dsi.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

            if (hasTablesi)
            {
                int rwsi = dsi.Tables[0].Rows.Count;
                if (rwsi > 0)
                {
                    DataRow dri = dsi.Tables[0].Rows[0];
                    forCnt = dri[0].ToString(); forLim = FormatValString(dri[1].ToString());
                }
            }
        }

        lim = forCnt + "|" + forLim;

        return lim;
    }
    public string GetCardLimits(string pan, string seq, string cardProgram)
    {
        var lim = "0|0|0|0|0|0|0|0|0";
        var issuer = GetIssuerFromCardProgramDet(cardProgram);

        string sql = @"SELECT [limits] FROM [postcard].[dbo].[pc_cards_" + issuer + "_A] WHERE PAN = @pan AND seq_nr = @seq AND date_deleted IS NULL AND hold_rsp_code IS NULL and expiry_date > " + DateTime.Now.ToString("yyMM");
        Connect cn = new Connect("Postcard");
        cn.Persist = true;
        cn.SetSQL(sql);
        cn.AddParam("@pan", pan);
        cn.AddParam("@seq", seq);
        var ds = cn.Select();
        cn.CloseAll();

        bool hasTablesi = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

        if (hasTablesi)
        {
            int rwsi = ds.Tables[0].Rows.Count;
            if (rwsi > 0)
            {
                DataRow dri = ds.Tables[0].Rows[0];
                lim = GetLocalLimit(issuer, pan, seq, cardProgram);

                lim += "|" + GetForeignLimit(issuer, pan, seq, cardProgram);
            }
        }

        return lim;
    }
    public string FormatValString(string val)
    {
        var fmt = string.Empty;

        if (val != "")
        {
            fmt = Convert.ToDouble(val).ToString("#.##");
        }
        else
        {
            fmt = val;
        }

        return fmt;
    }
    public string GetTrack2(string disc, string pan, string exp, string serviceCode, string seq_nr, string pvv, string cvv)
    {
        var track2Data = pan + "=" + exp + serviceCode;

        switch (disc)
        {
            case "SSPPPPCCC":
                track2Data += seq_nr.Substring(0, 2) + pvv.Substring(0, 4) + cvv.Substring(0, 3);
                break;
            case "SSSPPPP":
                track2Data += seq_nr.Substring(0, 3) + pvv.Substring(0, 4);
                break;
            case "SSCCC":
                track2Data += seq_nr.Substring(0, 2) + cvv.Substring(0, 3);
                break;
            case "1PPPPCCC":
                track2Data += "1" + pvv.Substring(0, 4) + cvv.Substring(0, 3);
                break;
        }


        return track2Data;
    }
    public string[] GetCardOutputInfo(string issuer, string card_program)
    {
        var info = new string[22];
        var pinExportKeyName = string.Empty; var pvkikeyName = string.Empty;
        string sql = @"SELECT i.issuer_name,i.card_production_issuer_id, p.service_code,p.discretionary_data,p.card_program_id,i.card_mailer_return_addr_1,
                        i.card_mailer_return_addr_2,i.card_mailer_return_city,i.card_mailer_return_region,i.card_mailer_return_postal,i.card_mailer_msg_line_1,
                        i.card_mailer_msg_line_2,i.card_mailer_msg_line_3,i.card_mailer_msg_line_4,i.pin_mailer_return_address_1,i.pin_mailer_return_address_2,
                        i.pin_mailer_return_city,i.pin_mailer_return_region,i.pin_mailer_return_postal,i.pin_export_key,p.pin_verification_key " +
                        "FROM pc_issuers i INNER JOIN pc_card_programs p ON i.issuer_nr = p.issuer_nr WHERE p.card_program = @card_program AND i.issuer_nr = @issuer";
        Connect cn = new Connect("Postcard")
        {
            Persist = true
        };
        cn.SetSQL(sql);
        cn.AddParam("@card_program", card_program);
        cn.AddParam("@issuer", issuer);
        DataSet ds = cn.Select();
        cn.CloseAll();

        bool hasRow = ds.Tables.Cast<DataTable>().Any(table => table.Rows.Count != 0);

        if (hasRow)
        {
            DataRow dr = ds.Tables[0].Rows[0];
            int colCnt = ds.Tables[0].Columns.Count;

            for (int j = 0; j < colCnt; j++)
            {
                info[j] = dr[j].ToString();

                if (j == (colCnt - 2))
                {
                    pinExportKeyName = dr[j].ToString();
                }
                if (j == (colCnt - 1))
                {
                    pvkikeyName = dr[j].ToString();
                }
            }

            if (pinExportKeyName != "")
            {
                info[colCnt - 1] = GetCryptoKeys(pinExportKeyName);
            }
            if (pvkikeyName != "")
            {
                info[colCnt] = GetCryptoKeys(pvkikeyName);
            }
        }
        return info;
    }
    public string GenerateOutputFileRecords(string servicecode, string pan, string seq_nr, string exp, string nameOnCard, string title, string firstname, string midname, string surname, string cusnum, string cvv, string cvv2, string pinOffset, string pinBlock, string disc, string cardAddy1, string cardAddy2, string cardCity, string cardRegion, string cardPostal, string pinAddy1, string pinAddy2, string pinCity, string pinRegion, string pinPostal)
    {
        var recs = string.Empty;
        var sep = ",";
        var track2 = GetTrack2(disc, pan, exp, servicecode, seq_nr, pinOffset, cvv);
        recs = pan + sep + seq_nr + sep + exp + sep + nameOnCard + sep + title + sep + firstname + sep + midname + sep + surname + sep + cusnum + sep + sep + sep + "0"
                + sep + cvv + sep + cvv2 + sep + "0" + sep + pinOffset + sep + pinBlock + sep + "1,1,1," + track2 + sep + cardAddy1 + sep + cardAddy2 + sep + cardCity + sep
                + cardRegion + sep + cardPostal + sep + pinAddy1 + sep + pinAddy2 + sep + pinCity + sep + pinRegion + sep + pinPostal;

        return recs;
    }
    public string GenOuputFileHeader(string issuer, string issuerName, string cardProdId, string cardProgramId, string card_program, string cardAddy1, string cardAddy2, string cardCity, string cardRegion, string cardPostal, string cardMsg1, string cardMsg2, string cardMsg3, string cardMsg4, string pinAddy1, string pinAddy2, string pinCity, string pinRegion, string pinPostal)
    {
        var header = "<Header>";
        var sep = ",";

        header += sep + issuer + sep + issuerName + sep + cardProdId + sep + cardProgramId + sep + card_program + sep + cardAddy1 + sep + cardAddy2 + sep + cardCity
                  + sep + cardRegion + sep + cardPostal + sep + cardMsg1 + sep + cardMsg2 + sep + cardMsg3 + sep + cardMsg4 + sep + pinAddy1 + sep + pinAddy2 + sep + pinCity
                  + sep + pinRegion + sep + pinPostal;
        return header;
    }
    public string GenOuputFileTrailer(int headerCount, int recordsCount, int cardCarriers, int pinMailers)
    {
        var trailer = "<Trailer>";

        trailer += "," + headerCount + "," + recordsCount + "," + cardCarriers + "," + pinMailers;

        return trailer;
    }
}