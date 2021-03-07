using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using log4net;
using System.Data;
/// <summary>
/// Summary description for Cards
/// </summary>
[WebService(Namespace = "http://tempuri.org/")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
// To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
// [System.Web.Script.Services.ScriptService]
public class Cards :  System.Web.Services.WebService {

    ILog logger = LogManager.GetLogger("CardServicesLog");
    CardSecurityUtils p = new CardSecurityUtils();
    Mailer m = new Mailer();
    CardUtils c = new CardUtils();
    tranwallWS.CardControl tw = new tranwallWS.CardControl();

    public Cards() {

        //Uncomment the following line if using designed components 
        //InitializeComponent(); 
    }

    //Generate and Update pin offset Go Money
    [WebMethod]
    public string[] GetSelectPINAndUpdatePINOffset_GO(string pan, string exp, string selectedPin)
    {
        var pinStat = new string[2];

        try
        {
            string seq_nr = c.GetCardSeqNr(pan, exp);
            logger.Debug("Get PINOffset PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            var pinOffset = GetPINOffSetFromSelectedPIN(pan, seq_nr, exp, selectedPin);

            if (pinOffset[2] == "00")
            {
                if ((pinOffset[3] != "") && (pinOffset[3] != null))
                {
                    logger.Debug("Updating PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    var upd = c.UpdateOffset(pan, pinOffset[4], exp, pinOffset[3], pinOffset[0], pinOffset[5]);

                    if (upd > 0)
                    {
                        pinStat[0] = "00|5|OFFSET UPDATED SUCESSFULLY";
                        pinStat[1] = pinOffset[3];
                        logger.Debug("PIN Offset updated successfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    }
                    else
                    {
                        pinStat[0] = "AA|5|ERROR UPDATING OFFSET";
                        pinStat[1] = "";
                        logger.Debug("Error updating PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    }
                }
                else
                {
                    pinStat[0] = pinOffset[2];
                    pinStat[1] = "";
                    logger.Debug("PIN Offset is null for  PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                }
            }
            else
            {
                pinStat[0] = pinOffset[2] + "|5|ERROR RETRIEVING OFFSET";
                pinStat[1] = "";
                logger.Debug("Unable to retrieve Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinStat[0]);
        return pinStat;
    }
	
	//GetImalActiveCardsByCustomerId
    [WebMethod]
    public string GetImalActiveCardsByCustomer(string customer_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetImalActiveCardsByCustomerId(customer_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }

    //Enrol Safetoken
    [WebMethod]
    public string EnrolSafetoken(SafetokenDet Input)
    {
        string output = "99";
        try
        {
            output = c.EnrolSafetokenProd(Input);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return output;
    }

    [WebMethod]
    public string[] GenerateAndUpdateRandomPIN_GO(string pan, string exp)
    {
        var pinStat = new string[2];

        try
        {
            string seq_nr = c.GetCardSeqNr(pan, exp);
            var selectedPin = c.GenerateRandomPIN().ToString();

            var pinOffset = GetPINOffSetFromSelectedPIN(pan, seq_nr, exp, selectedPin);

            if (pinOffset[2] == "00")
            {
                if ((pinOffset[3] != "") && (pinOffset[3] != null))
                {
                    var upd = c.UpdateOffset(pan, pinOffset[4], exp, pinOffset[3], pinOffset[0], pinOffset[5]);

                    if (upd > 0)
                    {
                        pinStat[0] = "00|5|OFFSET UPDATED SUCESSFULLY";
                        pinStat[1] = pinOffset[3];
                    }
                    else
                    {
                        pinStat[0] = "AA|5|ERROR UPDATING OFFSET";
                        pinStat[1] = "";
                    }
                }
                else
                {
                    pinStat[0] = pinOffset[2];
                    pinStat[1] = "";
                }
            }
            else
            {
                pinStat[0] = pinOffset[2] + "|5|ERROR RETRIEVING OFFSET";
                pinStat[1] = "";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinStat[0]);
        return pinStat;
    }
    [WebMethod]
    public List<CardDetail> GetActiveCardList(string account_id)
    {
        var cardData = new List<CardDetail>();

        try
        {
            cardData = c.GetActiveCardList(account_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }

    //Retrieve FarePay pan from customer id
    [WebMethod]
    public string GetFarePayCardByID(string customerID)
    {
        var pan = string.Empty;

        try
        {
            pan = c.GetFareCardsByID(customerID);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return pan;
    }

    //Get Enabled Channels
    [WebMethod]
    public string GetEnabledChannelsForNonNuban(string pan)
    {
        var channels = string.Empty;

        try
        {
            channels = tw.GetEnabledChannels(pan);

            if ((channels == "UNKNOWN_ACCOUNT") || (channels == "AACCOUNT NOT ENROLLED") || (channels == "ACCOUNT NOT ENROLLED"))
            {
                channels = "No Enabled Channel for Pan";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return channels;
    }
    [WebMethod]
    public List<CardDetail> GetAllActiveCardList(string account_id)
    {
        var cardData = new List<CardDetail>();

        try
        {
            cardData = c.GetAllCardList(account_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    //Generate and Update PIN Offset
    [WebMethod]
    public string[] GenerateAndUpdateRandomPIN(string pan, string seq_nr, string exp)
    {
        var pinStat = new string[2];

        try
        {
            var selectedPin = c.GenerateRandomPIN().ToString();

            var pinOffset = GetPINOffSetFromSelectedPIN(pan, seq_nr, exp, selectedPin);

            if (pinOffset[2] == "00")
            {
                if ((pinOffset[3] != "") && (pinOffset[3] != null))
                {
                    var upd = c.UpdateOffset(pan, pinOffset[4], exp, pinOffset[3], pinOffset[0], pinOffset[5]);

                    if (upd > 0)
                    {
                        pinStat[0] = "00|5|OFFSET UPDATED SUCESSFULLY";
                        pinStat[1] = pinOffset[3];
                    }
                    else
                    {
                        pinStat[0] = "AA|5|ERROR UPDATING OFFSET";
                        pinStat[1] = "";
                    }
                }
                else
                {
                    pinStat[0] = pinOffset[2];
                    pinStat[1] = "";
                }
            }
            else
            {
                pinStat[0] = pinOffset[2] + "|5|ERROR RETRIEVING OFFSET";
                pinStat[1] = "";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinStat[0]);
        return pinStat;
    }
    [WebMethod]
    public DataSet GetATMDetails()
    {
        var ds = new DataSet();
        try
        {
            ds = c.GetATMs();
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return ds;
    }
    //Generate and Update PIN Offset
    [WebMethod]
    public string[] GetSelectPINAndUpdatePINOffset(string pan, string seq_nr, string exp,string selectedPin)
    {
        var pinStat = new string[2];

        try
        {
            logger.Debug("Get PINOffset PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            var pinOffset = GetPINOffSetFromSelectedPIN(pan, seq_nr, exp, selectedPin);

            if (pinOffset[2] == "00")
            {
                if ((pinOffset[3] != "") && (pinOffset[3] != null))
                {
                    logger.Debug("Updating PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    var upd = c.UpdateOffset(pan, pinOffset[4], exp, pinOffset[3], pinOffset[0],pinOffset[5]);

                    if (upd > 0)
                    {
                        pinStat[0] = "00|5|OFFSET UPDATED SUCESSFULLY";
                        pinStat[1] = pinOffset[3];
                        logger.Debug("PIN Offset updated successfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    }
                    else
                    {
                        pinStat[0] = "AA|5|ERROR UPDATING OFFSET";
                        pinStat[1] = "";
                        logger.Debug("Error updating PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    }
                }
                else
                {
                    pinStat[0] = pinOffset[2];
                    pinStat[1] = "";
                    logger.Debug("PIN Offset is null for  PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                }
            }
            else
            {
                pinStat[0] = pinOffset[2]+"|5|ERROR RETRIEVING OFFSET";
                pinStat[1] = "";
                logger.Debug("Unable to retrieve Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinStat[0]);
        return pinStat;
    }
	//Activate New Card Go
	[WebMethod]
    public string[] ActivateNewCardGO(string pan, string exp, string selectedPin)
    {
        var pinStat = new string[2];

        try
        {
			string seq_nr = c.GetCardSeqNr(pan, exp);
            //var randomPin = c.GenerateRandomPIN().ToString();
            var pinOffset = GetPINOffSetFromSelectedPIN(pan, seq_nr, exp, selectedPin);//GetPINOffSet(pan, seq, exp,selectedPin);

            if (pinOffset[2] == "00")
            {
                if ((pinOffset[3] != "") && (pinOffset[3] != null))
                {
                    var upd = c.ActivateAndUpdateOffset(pan, pinOffset[4], exp, pinOffset[3], pinOffset[0], pinOffset[5]);

                    if (upd > 0)
                    {
                        pinStat[0] = "00|5|OFFSET UPDATED SUCESSFULLY";
                        pinStat[1] = pinOffset[3];
                    }
                    else
                    {
                        pinStat[0] = "AA|5|ERROR UPDATING OFFSET";
                        pinStat[1] = "";
                    }
                }
                else
                {
                    pinStat[0] = pinOffset[2];
                    pinStat[1] = "";
                }
            }
            else
            {
                pinStat[0] = "AA|5|ERROR RETRIEVING OFFSET";
                pinStat[1] = "";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinStat[0]);
        return pinStat;
    }
    //Generate and Update PIN Offset
    [WebMethod]
    public string[] ActivateNewCard(string pan, string seq_nr, string exp, string selectedPin)
    {
        var pinStat = new string[2];

        try
        {
            //var randomPin = c.GenerateRandomPIN().ToString();
            var pinOffset = GetPINOffSetFromSelectedPIN(pan, seq_nr, exp, selectedPin);//GetPINOffSet(pan, seq, exp,selectedPin);

            if (pinOffset[2] == "00")
            {
                if ((pinOffset[3] != "") && (pinOffset[3] != null))
                {
                    var upd = c.ActivateAndUpdateOffset(pan, pinOffset[4], exp, pinOffset[3], pinOffset[0], pinOffset[5]);

                    if (upd > 0)
                    {
                        pinStat[0] = "00|5|OFFSET UPDATED SUCESSFULLY";
                        pinStat[1] = pinOffset[3];
                    }
                    else
                    {
                        pinStat[0] = "AA|5|ERROR UPDATING OFFSET";
                        pinStat[1] = "";
                    }
                }
                else
                {
                    pinStat[0] = pinOffset[2];
                    pinStat[1] = "";
                }
            }
            else
            {
                pinStat[0] = "AA|5|ERROR RETRIEVING OFFSET";
                pinStat[1] = "";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinStat[0]);
        return pinStat;
    }
    //Generate PIN Offset from selected PIN
    [WebMethod]
    public string[] GetPINOffSet(string pan, string seq, string exp,string pin)
    {
        var pinOffset = new string[5];

        try
        {            
            var cardDetails = c.GetCardDetails(pan, seq, exp);
            if (cardDetails.Length == 8)
            {
                pinOffset[0] = cardDetails[1];
                pinOffset[4] = cardDetails[7];
                if (c.AllowedCardProgram(cardDetails[0]))
                {
                    pinOffset[1] = pin; 

                    var encryptClearPIN = p.EncryptClearPIN(pan, pin);

                    if (encryptClearPIN[0] == "00")
                    {
                        if (cardDetails[6] == "0")
                        {
                            var ibmOffset = p.GenerateIBMPINOffset(pan, encryptClearPIN[1], cardDetails[4], cardDetails[5]);

                            if (ibmOffset[0] == "00")
                            {
                                pinOffset[2] = ibmOffset[0];
                                pinOffset[3] = ibmOffset[1];
                            }
                            else
                            {
                                pinOffset[2] = ibmOffset[0] + "|4|ERROR GENERTAING PIN OFFSET"; ;
                                pinOffset[3] = "";
                            }
                        }
                        else if (cardDetails[6] == "1")
                        {
                            var visaPVV = p.GenerateVISAPVV(pan, encryptClearPIN[1], cardDetails[4], cardDetails[5]);

                            if (visaPVV[0] == "00")
                            {
                                pinOffset[2] = visaPVV[0];
                                pinOffset[3] = visaPVV[1];
                            }
                            else
                            {
                                pinOffset[2] = visaPVV[0] + "|4|ERROR GENERTAING PIN OFFSET";
                                pinOffset[3] = "";
                            }
                        }
                    }
                    else
                    {
                        pinOffset[2] = encryptClearPIN[0] + "|3|ERROR ENCRYPTING CLEAR PIN";
                        pinOffset[3] = "";
                    }
                }
                else
                {
                    pinOffset[2] = "AA|2|CARD PROGRAM NOT ALLOWED FOR E-PIN";
                    pinOffset[3] = "";
                }

            }
            else
            {
                pinOffset[2] = "AA|1|ERROR RETRIEVING CARD DETAILS";
                pinOffset[3] = "";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinOffset[2]);
        return pinOffset;
    }
    public string[] GetCardPINOffSet(string pan, string seq_nr, string exp, string pin)
    {
        var pinOffset = new string[4];
        logger.Debug("Get PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
        try
        {
            var cardDetails = c.GetCardData(pan, seq_nr, exp);
            logger.Debug("Getting card details for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            if (cardDetails.Length == 9)
            {
                pinOffset[0] = cardDetails[1];

                if (c.AllowedCardProgram(cardDetails[0]))
                {
                    pinOffset[1] = pin;
                    logger.Debug("Getting encrypted Clear PIN for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    var encryptClearPIN = p.EncryptClearPIN(pan, pin);

                    if (encryptClearPIN[0] == "00")
                    {
                        logger.Debug("Verifying encrypted Clear PIN for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        if (cardDetails[6] == "0")
                        {
                            var ibmOffset = p.GenerateIBMPINOffset(pan, encryptClearPIN[1], cardDetails[4], cardDetails[5]);
                            logger.Debug("Getting IBM PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                            if (ibmOffset[0] == "00")
                            {
                                pinOffset[2] = ibmOffset[0];
                                logger.Debug("IBM PIN Offset retrieved successfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                                pinOffset[3] = ibmOffset[1];
                            }
                            else
                            {
                                pinOffset[2] = ibmOffset[0] + "|4|ERROR GENERTAING PIN OFFSET";
                                logger.Debug("Error retrieving PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                                pinOffset[3] = "";
                            }
                        }
                        else if (cardDetails[6] == "1")
                        {
                            logger.Debug("Retrieving VISA PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                            var visaPVV = p.GenerateVISAPVV(pan, encryptClearPIN[1], cardDetails[4], cardDetails[5]);

                            if (visaPVV[0] == "00")
                            {
                                pinOffset[2] = visaPVV[0];
                                pinOffset[3] = visaPVV[1];
                                logger.Debug("Visa PIN Offset retrieved successfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                            }
                            else
                            {
                                pinOffset[2] = visaPVV[0] + "|4|ERROR GENERTAING PIN OFFSET";
                                logger.Debug("Error retrieving VISA PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                                pinOffset[3] = "";
                            }
                        }
                    }
                    else
                    {
                        pinOffset[2] = encryptClearPIN[0] + "|3|ERROR ENCRYPTING CLEAR PIN";
                        logger.Debug(encryptClearPIN[0] + "|3|ERROR ENCRYPTING CLEAR PIN for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        pinOffset[3] = "";
                    }
                }
                else
                {
                    pinOffset[2] = "AA|2|CARD PROGRAM NOT ALLOWED FOR E-PIN";
                    logger.Debug("AA|2|CARD PROGRAM NOT ALLOWED FOR E-PIN for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    pinOffset[3] = "";
                }

            }
            else
            {
                pinOffset[2] = "AA|1|ERROR RETRIEVING CARD DETAILS";
                logger.Debug("AA|1|ERROR RETRIEVING CARD DETAILS for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                pinOffset[3] = "";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0, 6) + "*********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinOffset[2]);
        return pinOffset;
    }
    //Generate CVV1 to be embedded on the magnetic stripe
    [WebMethod]
    public string GenerateCVV1(string pan,string exp)
    {
        var cvv2 = string.Empty;

        try
        {
            var cvk = c.GetCVVData(pan.Trim(), exp.Trim());
            cvv2 = p.GenerateCVV1(pan, exp, cvk[0],cvk[1]);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cvv2;
    }
    //Generate CVV2 to printed on the back of the card
    [WebMethod]
    public string GenerateCVV2(string pan, string exp)
    {
        var cvv2 = string.Empty;

        try
        {
            var cvk = c.GetCVV2Key(pan.Trim(), exp.Trim());
            cvv2 = p.GenerateCVV2(pan, exp, cvk);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cvv2;
    }
    //Generate PIN Block
    [WebMethod]
    public string[] GeneratePINBlock(string pan, string pin, string pvki)
    {
        var pinblock = new string[2];

        try
        {
            pinblock = p.GeneratePINMailerPINBlock(pan, pin, pvki);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return pinblock;
    }    
    //Generate PIN From Clear PinBlock
    [WebMethod]
    public string GetPINFromClearPINBlock(string pan, string pin)
    {
        var aPin = string.Empty;

        try
        {
            var pinBlock = p.ISO0_PINBlock(pan, pin);
            aPin = p.GeneratePINFromClearISO0PINBlock(pinBlock, pan);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());

        }
        return aPin;
    }
    [WebMethod]
    public string[] GetPINFromPINOffset(string pan, string pin,string pvk,int pinLength)
    {
        var aPin = new string[2];

        try
        {
            string[] val = p.DeriveIBMPIN(pan, pin, pvk, pinLength.ToString());
            aPin = p.DecryptClearPIN(pan, val[1],pinLength);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());

        }
        return aPin;
    }
    //Generate PIN Offset from Customer Selected PIN
    [WebMethod]
    public string[] GetPINOffSetFromSelectedPIN(string pan, string seq_nr, string exp, string pin)
    {
        var pinOffset = new string[6];
        logger.Debug("Getting PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
        try
        {
            logger.Debug("Getting Card Details for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            var cardDetails = c.GetCardData(pan, seq_nr, exp);
            pinOffset[0] = cardDetails[1];
            pinOffset[4] = cardDetails[7];
            pinOffset[5] = cardDetails[8];
            logger.Debug(cardDetails[1] + "|" + cardDetails[2] + "|" + cardDetails[3] + "|" + cardDetails[4] + "|" + cardDetails[5] + "|" + cardDetails[6] + "|" + cardDetails[7] + "|" + cardDetails[8]);

            if (c.AllowedCardProgram(cardDetails[0]))
            {
                logger.Debug("PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp + "'s card program Allowed");
                pinOffset[1] = pin;

                logger.Debug("Encrypting Clear PIN for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                var encryptClearPIN = p.EncryptClearPIN(pan, pin);

                if (encryptClearPIN[0] == "00")
                {
                    logger.Debug("Clear PIN encrypted sucessfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    if (cardDetails[6] == "0")
                    {
                        logger.Debug("Calculating IBM PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        var ibmOffset = p.GenerateIBMPINOffset(pan, encryptClearPIN[1], cardDetails[4], cardDetails[5]);
                        if (ibmOffset[0] == "00")
                        {
                            pinOffset[2] = ibmOffset[0];
                            pinOffset[3] = ibmOffset[1];

                            logger.Debug("IBM PIN Offset calculated successfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        }
                        else
                        {
                            pinOffset[2] = ibmOffset[0];
                            pinOffset[3] = "";
                            logger.Debug("Error calcutating IBM PIN offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        }
                    }
                    else if (cardDetails[6] == "1")
                    {
                        logger.Debug("Calculating VISA PIN Offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        var visaPVV = p.GenerateVISAPVV(pan, encryptClearPIN[1], cardDetails[4], cardDetails[5]);

                        if (visaPVV[0] == "00")
                        {
                            pinOffset[2] = visaPVV[0];
                            pinOffset[3] = visaPVV[1];
                            logger.Debug("VISA PIN Offset calculated successfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        }
                        else
                        {
                            pinOffset[2] = visaPVV[0];
                            pinOffset[3] = visaPVV[1];
                            logger.Debug("Error calcutating VISA PIN offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        }
                    }
                }
                else
                {
                    pinOffset[2] = encryptClearPIN[0];
                    pinOffset[3] = "";
                    logger.Debug("Error Encrypting clear pin for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        logger.Info(pan.Substring(0,6) + "********"+pan.Substring(pan.Length-4,4)+"---- "+pinOffset[0]);
        return pinOffset;
    }
    //Upload files through SFTP
    [WebMethod]
    public bool SFTPFileUpload(string sftpHost, int sftpPort, string sftpUser, string sftpPwd, string sftpPath, string uploadFile, string sftpName)
    {
        return c.SFTPUpload(sftpHost,sftpPort,sftpUser,sftpPwd,sftpPath,uploadFile,sftpName);
    }
    [WebMethod]
    //Validate PIN OffSet
    public string ValidateCardPIN(string pan, string seq_nr, string exp, string pin)
    {
        var pinValidation = "";

        logger.Error("PIN Validation for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
        try
        {
            int pinTries = c.GetPINTries(pan, seq_nr,exp);
            if (pinTries < 3)
            {
                var pinOffSet = GetCardPINOffSet(pan, seq_nr, exp, pin);

                if (pinOffSet[2] == "00")
                {
                    if (pinOffSet[3] == pinOffSet[0])
                    {
                        pinValidation = "00";
                        logger.Error("PIN Offset retrieved sucessfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    }
                    else
                    {
                        pinValidation = "BB|5|INCORRECT PIN";
                        int tt = c.UpdatePINTries(pan,seq_nr,exp,pinTries);
                        if (tt > 0)
                        {
                            logger.Error("Failed PIN Tries updated sucessfuly for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        }
                        else
                        {
                            logger.Error("Unable to update failed PIN tries for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        }
                        logger.Error("BB|5|INCORRECT PIN for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    }
                }
                else
                {
                    pinValidation = pinOffSet[2];
                    logger.Error("Unable to retrieve PIN offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                }
            }
            else
            {
                pinValidation = "99|0|PIN TRIES EXCEEDED";
                logger.Error(pinTries + " already attempted for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinValidation);
        return pinValidation;
    }
    [WebMethod]
    public string GetActiveCards(string account_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetActiveCards(account_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    [WebMethod]
    public string GetValidNewCards(string account_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetValidCards(account_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    [WebMethod]
    public string GetActiveCardsByCustomer(string customer_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetActiveCardsByCustomerId(customer_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    [WebMethod]
    public string GetAllActiveCardsByCustomer(string customer_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetAllActiveCardsByCustomerId(customer_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    [WebMethod]
    public string GetValidNewCardsByCustomer(string customer_id)
    {
        var cardData = string.Empty;

        try
        {
            //cardData = c.GetValidCardsByCustomerId(customer_id);
            cardData = c.GetValidCardsByCustomerID(customer_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
	[WebMethod]
    public string GetValidNewImalCardsByCustomer(string customer_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetValidImalCardsByCustomerID(customer_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    [WebMethod]
    public string GetLimitCardsByCustomer(string customer_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetLimitActiveCardsByCustomerId(customer_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    [WebMethod]
    public string GetJSONActiveCardsByCustomer(string customer_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetJSONActiveCardsByCustomerId(customer_id);
        }
        catch (Exception ex)
        { logger.Error(ex); }
        return cardData;
    }
    [WebMethod]
    public string GetJSONActiveVISACardsByCustomer(string customer_id)
    {
        var cardData = string.Empty;

        try
        {
            cardData = c.GetJSONVisaActiveCardsByCustomerId(customer_id);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return cardData;
    }
	//NIBSS POS Transaction Feed - Retrieve POS Transactions By Terminal
    [WebMethod]
    public List<NIBSSFeedDto> GetPOSTransByTerminal(string terminalID,DateTime startDate,DateTime endDate)
    {
        List<NIBSSFeedDto> _NIBSSFeedDto = new List<NIBSSFeedDto>();
        try
        {
            _NIBSSFeedDto = c.GetPOSTransByTerminal(terminalID,startDate,endDate);
        }
        catch (Exception ex)
        {
            logger.Error(ex.Message);
        }
        return _NIBSSFeedDto;
    }

    //NIBSS POS Transaction Feed - Retrieve POS Transactions By AccountNumber
    [WebMethod]
    public List<NIBSSFeedDto> GetPOSTransByAccountNo(string accountNo, DateTime startDate, DateTime endDate)
    {
        List<NIBSSFeedDto> _NIBSSFeedDto = new List<NIBSSFeedDto>();
        try
        {
            _NIBSSFeedDto = c.GetPOSTransByAccountNo(accountNo, startDate, endDate);
        }
        catch (Exception ex)
        {
            logger.Error(ex.Message);
        }
        return _NIBSSFeedDto;
    }
	
    //Insert Active CMS Details
    [WebMethod]
    public string[] InsertCardData(string pan, string seq, string account, string exp, string cardProg, string user)
    {
        var insStat = new string[2];

        try
        {
            insStat = c.InsertVirtualCMSDetails(pan, seq, account, exp, cardProg, user);
        }
        catch (Exception ex)
        { logger.Error(ex); }

        return insStat;
    }
    //Insert Wallet Details
    [WebMethod]
    public string[] InserWalletCardData(string pan, string seq, string exp, string account, string custId, string nameOnCard, string city, string state, string address, string branch, string accType, string curCode, string cardProg, string user)
    {
        var insStat = new string[2];

        try
        {
            insStat = c.InsertWalletCardDetails(pan,seq,exp,account,custId,nameOnCard,city,state,address,branch,accType,curCode,cardProg,user);
        }
        catch (Exception ex)
        { logger.Error(ex); }

        return insStat;
    }
    [WebMethod]
    public string GetCardLimits(string pan, string seq, string cardProgram)
    {
        var lim = string.Empty;

        try
        {
            lim = c.GetCardLimits(pan, seq, cardProgram);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return lim;
    }
    //Insert Inactive CMS Details
    [WebMethod]
    public string[] InsertVirtualCardData(string issuer_nr,string pan, string seq, string account, string exp, string cardProg, string user, string stat = "0")
    {
        var insStat = new string[2];

        try
        {
            insStat = c.InsertVirtualCMSData(issuer_nr,pan, seq, account, exp, cardProg, user,stat);
        }
        catch (Exception ex)
        { logger.Error(ex); }

        return insStat;
    }
    //Insert MS Details
    [WebMethod]
    public string CreatemVisaData(string refId,string account)
    {
        var insStat = new string[3];
        string pan = string.Empty;

        try
        {
            insStat = c.CreateMVisaCards(refId,account);

            if (insStat[1] == "00")
            {
                pan = insStat[2];
            }
            else
            {
                pan = insStat[2];
            }
            logger.Error(insStat[0]);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return pan;
    }
    [WebMethod]
    //Hotlist a card
    public string BlockCard(string pan,string exp,string customer_id,string type)
    {
        string resp = string.Empty;

        try
        {
            resp = c.HotlistCard(pan, exp, customer_id,type);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return resp;
    }
    [WebMethod]
    //Hotlist a card
    public string UnBlockCard(string pan, string exp, string customer_id)
    {
        string resp = string.Empty;

        try
        {
            resp = c.DeHotlistCard(pan, exp, customer_id);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return resp;
    }
    [WebMethod]
    //Hotlist a card
    public string GetCardStat(string pan, string exp)
    {
        string resp = string.Empty;

        try
        {
            resp = c.GetCardStat(pan, exp);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return resp;
    }
    [WebMethod]
    //Set Card Web Limit
    public string ModifyCardWebLimit(string pan,string seq_nr,string card_program,string limit)
    {
        string stat = string.Empty;

        try
        {
            stat = c.ModifyWebLimit(pan, seq_nr, card_program, limit);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return stat;
    }
    [WebMethod]
    //Set Card Purchase Limit
    public string ModifyCardPurchaseLimit(string pan, string seq_nr, string card_program, string limit)
    {
        string stat = string.Empty;

        try
        {
            stat = c.ModifyPurchaseLimit(pan, seq_nr, card_program, limit);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return stat;
    }
    [WebMethod]
    //Set Card Payment Limit
    public string ModifyCardPaymentLimit(string pan, string seq_nr, string card_program, string limit)
    {
        string stat = string.Empty;

        try
        {
            stat = c.ModifyPaymentLimit(pan, seq_nr, card_program, limit);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        return stat;
    }
    [WebMethod]
    public string GetQRCodeData(string account, string cardprogram)
    {
        var insStat = new string[2];
        string mpassData = string.Empty;

        try
        {
            insStat = c.GetQRData(account,cardprogram);

            if (insStat[0] == "00")
            {
                mpassData = insStat[1];
            }
            else
            {
                mpassData = "00|00|00";
                logger.Error(insStat[0]);
            }
            
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return mpassData;
    }
    [WebMethod]
    public string GetMPassData(string account)
    {
        var insStat = new string[2];
        string mpassData = string.Empty;

        var cardprogram = "SBP_MASTERCARD_QR";

        try
        {
            insStat = c.GetQRData(account, cardprogram);

            if (insStat[0] == "00")
            {
                mpassData = insStat[1];
            }
            else
            {
                mpassData = "00|00|00";
                logger.Error(insStat[0]);
            }

        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return mpassData;
    }
    //Get Full CounrtyList
    [WebMethod]
    public DataSet GetFullCountryList()
    {
        var ds = new DataSet();

        try
        {
            ds = c.GetFullCountryList();
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return ds;

    }
    //Get Country Data By Currency Code
    [WebMethod]
    public DataSet GetCountryByCurrCode(string curCode)
    {
        var ds = new DataSet();

        try
        {
            ds = c.GetCountryDataByCurCode(curCode);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return ds;

    }
    //Get ISO Country by ISO2 Code
    [WebMethod]
    public DataSet GetCountryByISO2Code(string isoCode)
    {
        var ds = new DataSet();

        try
        {
            ds = c.GetCountryDataByISO2Code(isoCode);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return ds;

    }
    //Get Enabled Countries
    [WebMethod]
    public string GetEnabledCountries(string pan)
    {
        var countries = string.Empty;

        try
        {
            countries = tw.GetEnabledCountries(pan);
            if ((countries == "UNKNOWN_ACCOUNT") ||(countries == "")||(countries == "null") || (countries == null))
            {
                var exp = DateTime.Now.ToString("yyMM");
                var cardInfo = c.GetCardInfo(pan, exp);

                if (cardInfo.Length > 0)
                {
                    var rsp = OnboardCustomerCardControl(cardInfo[0], cardInfo[1], cardInfo[2]);

                    if (rsp == "00:SUCCESS")
                    {
                        countries = tw.GetEnabledCountries(pan);
                    }
                    else
                    {
                        countries = rsp;
                    }
                }
                else
                {
                    countries = "ACCOUNT NOT ENROLLED";
                }
            }
        }
        catch(Exception ex)
        {
            logger.Error(ex);
        }

        return countries;
    }
    //Get Enabled Channels
    [WebMethod]
    public string GetEnabledChannels(string pan)
    {
        var channels = string.Empty;

        try
        {
            channels = tw.GetEnabledChannels(pan);

            if ((channels == "UNKNOWN_ACCOUNT") || (channels == "AACCOUNT NOT ENROLLED") || (channels == "ACCOUNT NOT ENROLLED"))
            {
                var exp = DateTime.Now.ToString("yyMM");
                var cardInfo = c.GetCardInfo(pan, exp);

                if (cardInfo.Length > 0)
                {
                    var rsp = OnboardCustomerCardControl(cardInfo[0], cardInfo[1], cardInfo[2]);

                    if (rsp == "00:SUCCESS")
                    {
                        channels = tw.GetEnabledChannels(pan);
                    }
                    else
                    {
                        channels = rsp;
                    }
                }
                else
                {
                    channels = "ACCOUNT NOT ENROLLED";
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return channels;
    }
    //Enable Channel
    [WebMethod]
    public string EnableChannel(string pan,int channel)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.EnableChannel(pan,channel);
            if (resp == "UNKNOWN_ACCOUNT")
            {
                var exp = DateTime.Now.ToString("yyMM");
                var cardInfo = c.GetCardInfo(pan, exp);

                if (cardInfo.Length > 0)
                {
                    var rsp = OnboardCustomerCardControl(cardInfo[0], cardInfo[1], cardInfo[2]);

                    if (rsp == "00:SUCCESS")
                    {
                        resp = tw.EnableChannel(pan, channel);
                    }
                    else
                    {
                        resp = rsp;
                    }
                }
                else
                {
                    resp = "ACCOUNT NOT ENROLLED";
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }

    //Enable Channel Wallet
    [WebMethod]
    public string EnableChannelForNonNubanCustomer(string pan, int channel, string customeruniqueId, string title, string fname, string sname, string email)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.EnableChannel(pan, channel);
            if (resp == "UNKNOWN_ACCOUNT")
            {
                var exp = DateTime.Now.ToString("yyMM");
                var cardInfo = c.GetCardInfo(pan, exp);

                if (cardInfo.Length > 0)
                {
                    var rsp = OnboardCustomerCardControlWallet(cardInfo[0], cardInfo[1], customeruniqueId, title, fname, sname, email);

                    if (rsp == "00:SUCCESS")
                    {
                        resp = tw.EnableChannel(pan, channel);
                    }
                    else
                    {
                        resp = rsp;
                    }
                }
                else
                {
                    resp = "ACCOUNT NOT ENROLLED";
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }

    //Enable Channel Wallet
    [WebMethod]
    public string EnableChannelForEnrolledNonNubanCustomer(string pan, int channel)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.EnableChannel(pan, channel);

        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }
    [WebMethod]
    public string OnboardCustomerCardControl(string pan, string exp,string account_id)
    {
        var resp = string.Empty;

        try
        {
            var customerDetails = c.GetCustomerDetails(account_id);
            if (customerDetails.Length == 5)
            {
                var phoneno = customerDetails[0]; var title = customerDetails[1];
                var fname = customerDetails[2]; var sname = customerDetails[3];
                string email = customerDetails[4];

                if (phoneno != "")
                {
                    resp = tw.CustomerOnboarding(pan, exp, phoneno, title, fname, sname, email);
                }
                else
                {
                    resp = "No registered Mobile";
                    logger.Error("No Mobile retrieved for account " + account_id + " for Card ControlOnboarding"); ;
                }
            }
            else
            {
                resp = "No account data";
                logger.Error("No data retrieved for account "+account_id + " for Card ControlOnbording");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }
    [WebMethod]
    //Validate PIN OffSet Go Money
    public string ValidateCardPIN_GO(string pan, string exp, string pin)
    {
        var pinValidation = "";
        try
        {
            string seq_nr = c.GetCardSeqNr(pan, exp);

            logger.Error("PIN Validation for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            int pinTries = c.GetPINTries(pan, seq_nr, exp);
            if (pinTries < 3)
            {
                var pinOffSet = GetCardPINOffSet(pan, seq_nr, exp, pin);

                if (pinOffSet[2] == "00")
                {
                    if (pinOffSet[3] == pinOffSet[0])
                    {
                        pinValidation = "00";
                        logger.Error("PIN Offset retrieved sucessfully for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    }
                    else
                    {
                        pinValidation = "BB|5|INCORRECT PIN";
                        int tt = c.UpdatePINTries(pan, seq_nr, exp, pinTries);
                        if (tt > 0)
                        {
                            logger.Error("Failed PIN Tries updated sucessfuly for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        }
                        else
                        {
                            logger.Error("Unable to update failed PIN tries for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                        }
                        logger.Error("BB|5|INCORRECT PIN for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                    }
                }
                else
                {
                    pinValidation = pinOffSet[2];
                    logger.Error("Unable to retrieve PIN offset for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
                }
            }
            else
            {
                pinValidation = "99|0|PIN TRIES EXCEEDED";
                logger.Error(pinTries + " already attempted for PAN:-" + pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " SequenceNumber:- " + seq_nr + " Expiry_Date:- " + exp);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
        logger.Info(pan.Substring(0, 6) + "********" + pan.Substring(pan.Length - 4, 4) + " ---- " + pinValidation);
        return pinValidation;
    }
    [WebMethod]
    public string OnboardCustomerCardControlWallet(string pan, string exp, string customeruiqueId, string title, string fname, string sname, string email)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.CustomerOnboarding(pan, exp, customeruiqueId, title, fname, sname, email);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }


    //Enable Country
    [WebMethod]
    public string EnableCountry(string pan, string country)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.EnableCountry(pan, country);
            if (resp == "UNKNOWN_ACCOUNT")
            {
                var exp = DateTime.Now.ToString("yyMM");
                var cardInfo = c.GetCardInfo(pan, exp);

                if (cardInfo.Length > 0)
                {
                    var rsp = OnboardCustomerCardControl(cardInfo[0], cardInfo[1], cardInfo[2]);

                    if (rsp == "00:SUCCESS")
                    {
                        resp = tw.EnableCountry(pan, country);
                    }
                    else
                    {
                        resp = rsp;
                    }
                }
                else
                {
                    resp = "ACCOUNT NOT ENROLLED";
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }
    //Disable Country
    [WebMethod]
    public string DisableCountry(string pan, string country)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.DisableCountry(pan, country);

            if (resp == "UNKNOWN_ACCOUNT")
            {
                var exp = DateTime.Now.ToString("yyMM");
                var cardInfo = c.GetCardInfo(pan, exp);

                if (cardInfo.Length > 0)
                {
                    var rsp = OnboardCustomerCardControl(cardInfo[0], cardInfo[1], cardInfo[2]);

                    if (rsp == "00:SUCCESS")
                    {
                        resp = tw.DisableCountry(pan, country);
                    }
                    else
                    {
                        resp = rsp;
                    }
                }
                else
                {
                    resp = "ACCOUNT NOT ENROLLED";
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }
    //Disable CHANNEL
    [WebMethod]
    public string DisableChannel(string pan, int channel)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.DisableChannel(pan, channel);
            if (resp == "UNKNOWN_ACCOUNT")
            {
                var exp = DateTime.Now.ToString("yyMM");
                var cardInfo = c.GetCardInfo(pan, exp);

                if (cardInfo.Length > 0)
                {
                    var rsp = OnboardCustomerCardControl(cardInfo[0], cardInfo[1], cardInfo[2]);

                    if (rsp == "00:SUCCESS")
                    {
                        resp = tw.DisableChannel(pan, channel);
                    }
                    else
                    {
                        resp = rsp;
                    }
                }
                else
                {
                    resp = "ACCOUNT NOT ENROLLED";
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }


    //Disable CHANNEL for Non Nuban Customer
    [WebMethod]
    public string DisableChannelForNonNubanCustomer(string pan, int channel, string customeruiqueId, string title, string fname, string sname, string email)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.DisableChannel(pan, channel);
            if (resp == "UNKNOWN_ACCOUNT")
            {
                var exp = DateTime.Now.ToString("yyMM");
                var cardInfo = c.GetCardInfo(pan, exp);

                if (cardInfo.Length > 0)
                {
                    var rsp = OnboardCustomerCardControlWallet(cardInfo[0], cardInfo[1], customeruiqueId, title, fname, sname, email);

                    if (rsp == "00:SUCCESS")
                    {
                        resp = tw.DisableChannel(pan, channel);
                    }
                    else
                    {
                        resp = rsp;
                    }
                }
                else
                {
                    resp = "ACCOUNT NOT ENROLLED";
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }


    //Disable CHANNEL for enrolled Non Nuban Customer
    [WebMethod]
    public string DisableChannelForEnrolledNonNubanCustomer(string pan, int channel)
    {
        var resp = string.Empty;

        try
        {
            resp = tw.DisableChannel(pan, channel);

        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }

    //Get Transaction Per Terminal
    [WebMethod]
    public string GetJSONCardTrxnPerTermCurrMonth(string acc)
    {
        var resp = string.Empty;

        try
        {
            resp = c.GetJSONCardTrxnPerTerminalCurrMonth(acc);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }
    //Get Transaction Per Terminal
    [WebMethod]
    public string[] GetCardOutputInfo(string issuer, string card_program)
    {
        var resp = new string[24];

        try
        {
            resp = c.GetCardOutputInfo(issuer,card_program);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }
    //Get Transaction Per Terminal
    [WebMethod]
    public string GenOuputFileTrailer(int headerCount, int recordsCount, int cardCarriers, int pinMailers)
    {
        var resp = string.Empty;

        try
        {
            resp = c.GenOuputFileTrailer(headerCount,recordsCount,cardCarriers,pinMailers);
        }
        catch (Exception ex)
        {
            logger.Error(ex); 
        }

        return resp;
    }
    //Get Transaction Per Terminal
    [WebMethod]
    public string GenOuputFileHeader(string issuer, string issuerName, string cardProdId, string cardProgramId, string card_program, string cardAddy1, string cardAddy2, string cardCity, string cardRegion, string cardPostal, string cardMsg1, string cardMsg2, string cardMsg3, string cardMsg4, string pinAddy1, string pinAddy2, string pinCity, string pinRegion, string pinPostal)
    {
        var resp = string.Empty;

        try
        {
            resp = c.GenOuputFileHeader(issuer,issuerName,cardProdId,cardProgramId,card_program,cardAddy1,cardAddy2,cardCity,cardRegion,cardPostal,cardMsg1,cardMsg2,cardMsg3,cardMsg4,pinAddy1,pinAddy2,pinCity,pinRegion,pinPostal);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }
    //Get Transaction Per Terminal
    [WebMethod]
    public string GenerateOutputFileRecords(string servicecode, string pan, string seq_nr, string exp, string nameOnCard, string title, string firstname, string midname, string surname, string cusnum, string pinOffset, string disc, string cardAddy1, string cardAddy2, string cardCity, string cardRegion, string cardPostal, string pinAddy1, string pinAddy2, string pinCity, string pinRegion, string pinPostal,string pin_Export_key,int pinLength,string pvki)
    {
        var resp = string.Empty;

        var cvv = GenerateCVV1(pan, exp);
        var cvv2 = GenerateCVV2(pan, exp);
        var pin = string.Empty;

        if (pinOffset == "")
        {
            pin = c.GenerateRandomPIN().ToString();
            var pinOff = GetSelectPINAndUpdatePINOffset(pan, seq_nr, exp, pin);
            pinOffset = pinOff[1];
        }
        else
        {
            if (pinLength == 1)
            {
                pin = c.GenerateRandomPIN().ToString();

            }
            else
            {
                var pinData = GetPINFromPINOffset(pan, pinOffset, pvki, pinLength);
                pin = pinData[1];
            }
        }
        var pinBk = GeneratePINBlock(pan, pin, pin_Export_key);
        var pinBlock = pinBk[1];

        try
        {
            resp = c.GenerateOutputFileRecords(servicecode,pan,seq_nr,exp,nameOnCard,title,firstname,midname,surname,cusnum,cvv,cvv2,pinOffset,pinBlock,disc,cardAddy1,cardAddy2,cardCity,cardRegion,cardPostal,pinAddy1,pinAddy2,pinCity,pinRegion,pinPostal);
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return resp;
    }
}
