using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using log4net;

/// <summary>
/// Summary description for PINUtils
/// </summary>
public class CardSecurityUtils
{
    static string Header = "0000";
    static int HeaderLength = Convert.ToInt16(ConfigurationManager.AppSettings["HsmHeaderLength"]);
    static string decimalizationTable = ConfigurationManager.AppSettings["DecimalisationTable"];
    static string minPinLen = ConfigurationManager.AppSettings["MinPINLength"];
    static int pinLen = Convert.ToInt32(ConfigurationManager.AppSettings["PINLength"]);
    static string pvki = ConfigurationManager.AppSettings["PVKI"];

    HsmUtils h = new HsmUtils();

    ILog logger = LogManager.GetLogger("CardServicesLog");

    public CardSecurityUtils()
    {
        //
        // TODO: Add constructor logic here
        //
    }

    //Set the HSM Header Variable
    public string HSMHeader()
    {
        var hsmHead = string.Empty;
        try
        {
            hsmHead = Header.PadLeft(HeaderLength, '0');
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        return hsmHead;
    }

    //Format the Card Number (PAN) to be used for PIN encryption
    public string PaddedPan(string pan)
    {
        var padPan = string.Empty;
        try
        {
            padPan = pan.Substring((pan.Length - 13), 12);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        return padPan;
    }

    //Format the PIN into a
    public string PaddedPIN(string pin)
    {
        var padPin = string.Empty;
        try
        {
            padPin = pin.PadRight(pinLen, 'F'); ;
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        return padPin;
    }

    //Format the PIN into a
    public string PaddedOffset(string offset)
    {
        var padOffset = string.Empty;
        try
        {
            padOffset = offset.PadRight(12, 'F'); ;
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        return padOffset;
    }

    //Encrypt a Clear PIN
    public string[] EncryptClearPIN(string pan, string pin)
    {
        var encryptedData = new string[2];

        string padPan = PaddedPan(pan);
        string padPIN = PaddedPIN(pin);

        string msg = HSMHeader() + "BA" + padPIN + padPan;

        string hsmResponse = h.Send(msg);

        if ((hsmResponse != "No reply from HSM") && (hsmResponse != ""))
        {
            string errorCode = hsmResponse.Substring(HeaderLength + 2, 2);
            encryptedData[0] = errorCode;
            if (errorCode == "00")
            {
                encryptedData[1] = hsmResponse.Substring(HeaderLength + 4, pinLen);
            }
            else
            {
                encryptedData[1] = "";
            }
        }
        else
        {
            encryptedData[0] = "99";
            encryptedData[1] = "";
        }

        return encryptedData;
    }
    public string[] DecryptClearPIN(string pan, string pin,int pinLength)
    {
        var encryptedData = new string[2];

        string padPan = PaddedPan(pan);
        //string padPIN = PaddedPIN(pin);

        string msg = HSMHeader() + "NG" + padPan+pin;

        string hsmResponse = h.Send(msg);

        if ((hsmResponse != "No reply from HSM") && (hsmResponse != ""))
        {
            string errorCode = hsmResponse.Substring(HeaderLength + 2, 2);
            encryptedData[0] = errorCode;
            if (errorCode == "00")
            {
                encryptedData[1] = hsmResponse.Substring(HeaderLength + 4, pinLength);
            }
            else
            {
                encryptedData[1] = "";
            }
        }
        else
        {
            encryptedData[0] = "99";
            encryptedData[1] = "";
        }

        return encryptedData;
    }

    public string[] ZPKPinBlock(string zpk, string pinBlockFormat, string pan, string clearPINBlock)
    {
        //get zpk from exec crypto_dea1_keys_get 'EMV_KWP'; val_under_ksk
        var pinBlock = new string[2];

        string pvd = PINValidationData(pan);
        string padPan = PaddedPan(pan);
        var padding = "U";

        //string msg = HSMHeader() + "JG" + zpk + pinBlockFormat + padPan + clearPINBlock;
        string msg = HSMHeader() + "JG" + padding + zpk + pinBlockFormat + padPan + clearPINBlock;

        string hsmResponse = h.Send(msg);

        if ((hsmResponse != "No reply from HSM") && (hsmResponse != ""))
        {
            string errorCode = hsmResponse.Substring(HeaderLength + 2, 2);
            pinBlock[0] = errorCode;
            if (errorCode == "00")
            {
                pinBlock[1] = hsmResponse.Substring(HeaderLength + 4, 16);
            }
            else
            {
                pinBlock[1] = "";
            }
        }
        else
        {
            pinBlock[0] = "99";
            pinBlock[1] = "";
        }

        return pinBlock;
    }
    //Get CVV1
    public string GenerateCVV1(string pan, string exp,string cvk, string serviceCode)
    {
        string cvv2 = "";

        string del = ";";
        //string serviceCode = "000";

        try
        {

            string msg = HSMHeader() + "CW" + cvk + pan + del + exp + serviceCode;

            string resp = h.Send(msg);

            if ((resp != "No reply from HSM") && (resp != ""))
            {
                string stat = resp.Substring(HeaderLength + 2, 2);
                cvv2 = resp.Substring(HeaderLength + 4, 3);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cvv2;
    }
    //Get CVV2
    public string GenerateCVV2(string pan, string exp, string cvk)
    {
        string cvv2 = "";

        string del = ";";
        string serviceCode = "000";

        try
        {

            string msg = HSMHeader() + "CW" + cvk + pan + del + exp + serviceCode;

            string resp = h.Send(msg);

            if (resp != "No reply from HSM") 
            {
                if (resp != "")
                {
                    string stat = resp.Substring(HeaderLength + 2, 2);
                    cvv2 = resp.Substring(HeaderLength + 4, 3);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return cvv2;
    }
    //Get the PIN Validation Data
    public string PINValidationData(string pan)
    {
        var pvd = string.Empty;
        try
        {
            pvd = pan.Substring(0, 10) + "N" + pan.Substring((pan.Length - 1), 1); ;
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        return pvd;
    }

    //Generate an IBM PIN Offset from an encrypted Clear PIN
    public string[] GenerateIBMPINOffset(string pan, string encrPIN, string pvk, string pinLength)
    {
        var ibmOffset = new string[2];

        string pvd = PINValidationData(pan);
        string padPan = PaddedPan(pan);

        string msg = HSMHeader() + "DE" + pvk + encrPIN + pinLength.PadLeft(2,'0') + padPan + decimalizationTable + pvd;

        string hsmResponse = h.Send(msg);

        if ((hsmResponse != "No reply from HSM") && (hsmResponse != ""))
        {
            string errorCode = hsmResponse.Substring(HeaderLength + 2, 2);
            ibmOffset[0] = errorCode;
            if (errorCode == "00")
            {
                ibmOffset[1] = hsmResponse.Substring(HeaderLength + 4, Convert.ToInt16(pinLength));
            }
            else
            {
                ibmOffset[1] = "";
            }
        }
        else
        {
            ibmOffset[0] = "99";
            ibmOffset[1] = "";
        }

        return ibmOffset;
    }
    //Generate an IBM PIN Offset from an encrypted Clear PIN
    public string[] DeriveIBMPIN(string pan, string pinOffset, string pvk, string pinLength)
    {
        var ibmOffset = new string[2];

        string pvd = PINValidationData(pan);
        string padPan = PaddedPan(pan);
        string padOffset = PaddedOffset(pinOffset);


        string msg = HSMHeader() + "EE" + pvk + padOffset + pinLength.PadLeft(2, '0') + padPan + decimalizationTable + pvd;

        string hsmResponse = h.Send(msg);

        if ((hsmResponse != "No reply from HSM") && (hsmResponse != ""))
        {
            string errorCode = hsmResponse.Substring(HeaderLength + 2, 2);
            ibmOffset[0] = errorCode;
            if (errorCode == "00")
            {
                ibmOffset[1] = hsmResponse.Substring(HeaderLength + 4, Convert.ToInt16(pinLen));
            }
            else
            {
                ibmOffset[1] = "";
            }
        }
        else
        {
            ibmOffset[0] = "99";
            ibmOffset[1] = "";
        }

        return ibmOffset;
    }
    //Generate a VISA PVV
    public string[] GenerateVISAPVV(string pan, string encrPIN, string kvp, string pvki)
    {
        var visaPVV = new string[2];

        string padPan = PaddedPan(pan);

        string msg = HSMHeader() + "DG" + kvp + encrPIN + padPan + pvki;

        string hsmResponse = h.Send(msg);

        if ((hsmResponse != "No reply from HSM") && (hsmResponse != ""))
        {
            string errorCode = hsmResponse.Substring(HeaderLength + 2, 2); visaPVV[0] = errorCode;
            if (errorCode == "00")
            {
                visaPVV[1] = hsmResponse.Substring(HeaderLength + 4, Convert.ToInt16(minPinLen));
            }
            else
            {
                visaPVV[1] = "";
            }
        }
        else
        {
            visaPVV[0] = "99";
            visaPVV[1] = "";
        }

        return visaPVV;
    }
    //Encrypt PIN Block with zpk
    public string GetPINdata(string pan, string pin, string zpk, string comp1, string comp2)
    {
        string pindata = "";
        //get the clear PIN Block
        string pinBlock = ISO0_PINBlock(pan, pin);
        //get zmk from the clear ZMK components
        string zmk = GetZMKfromComponents(comp1, comp2);
        //Decrypt the encrypted zpk with the zmk
        string decryptedZpk = DecryptZPK(zpk, zmk);
        //Encrypt the PIN Block with the decrypted ZPK
        pindata = EncryptPINBlock(pinBlock, decryptedZpk);

        return pindata;
    }
    //Generate PIN Mailer PIN Block
    public string[] GeneratePINMailerPINBlock(string pan, string pin,string pvki)
    {
        string[] pinblock = new string[2];

        try
        {
            //string clearPINBlock = ISO0_PINBlock(pan, pin);
            var clearPINBlock = EncryptClearPIN(pan, pin);
            pinblock[0] = "00";
            var enc = ZPKPinBlock(pvki, "01", pan, clearPINBlock[1]);
            pinblock[1] = enc[1];
                //EncryptPINBlock(clearPINBlock, pvki);
        }
        catch(Exception ex)
        {
            pinblock[0] = "AA";
            pinblock[1] = "ERROR";

            logger.Info(ex);
        }

        return pinblock;
    }
        //Decrypt Zpk with ZMK formed from ZMK Components
    public string DecryptZPK(string zpk, string zmk)
    {
        string decryptedZpk = h.TripleDESDecryption(zpk, zmk);

        return decryptedZpk;
    }
    //Encrypt PIN Block
    public string EncryptPINBlock(string pinBlock, string key)
    {
        string encryptedPINBlock = h.TripleDesEncryption(pinBlock, key);

        return encryptedPINBlock;
    }
    //Get zmk to decrypt zpk
    public string GetZMKfromComponents(string comp1, string comp2)
    {
        string zmk = h.XORHexStringsFull(comp1, comp2);

        return zmk;
    }
    //Get a PIN from a Clear PIN Block
    public string GeneratePINFromClearISO0PINBlock(string clearPINBlock, string pan)
    {
        var pin = string.Empty;

        try
        {
            string padPan = PadHexPan(pan);
            string padPin = h.XORHexStrings(padPan, clearPINBlock.PadLeft(16, '0'));
            int pinLen = Convert.ToInt32(padPin.Substring(0, 2));
            pin = padPin.Substring(2, pinLen);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return pin;
    }

    //Form ISO-0 / ANSI 98 PIN Block format
    public string ISO0_PINBlock(string pan, string pin)
    {
        var pinBlock = string.Empty;
        try
        {
            string padPan = PadHexPan(pan);
            string padPin = PadHexPin(pin);

            pinBlock = h.XORHexStringsFull(padPin, padPan);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return pinBlock;
    }

    //Get padded PAN to form ISO 0 PINBlock format
    public string PadHexPan(string pan)
    {
        var paddedPan = string.Empty;
        try
        {
            string r12pan = PaddedPan(pan);

            paddedPan = r12pan.PadLeft(16, '0');
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }
        return paddedPan;
    }

    //Get padded PIN to form ISO-0 PINBlock format
    public string PadHexPin(string pin)
    {
        var paddedPIN = string.Empty;

        try
        {
            string prefix = (pin.Length.ToString()).PadLeft(2, '0');

            string appendPIN = prefix + pin;

            paddedPIN = appendPIN.PadRight(16, 'F');
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return paddedPIN;
    }

    //Decrypt Encrypted PIN Block
    public string DecryptISO0PINBlock(string encryptedPinBlock, string key)
    {
        var DecryptedPINBlock = string.Empty;
        try
        {
            DecryptedPINBlock = h.TripleDESDecryption(encryptedPinBlock, key);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return DecryptedPINBlock;
    }

    //Get PIN From Encrypted PIN Block
    public string GetPINFromEncryptedISO0PINBlock(string encryptedPINBlock, string pan, string zpk,string comp1,string comp2)
    {
        var pin = string.Empty;

        //get zmk from the clear ZMK components
        string zmk = GetZMKfromComponents(comp1, comp2);
        //Decrypt the encrypted zpk with the zmk
        string decryptedZpk = DecryptZPK(zpk, zmk);
        string clearPINBlock = DecryptISO0PINBlock(encryptedPINBlock, decryptedZpk);
        pin = GeneratePINFromClearISO0PINBlock(clearPINBlock, pan);

        return pin;
    }

    //Generate PIN Offset From Encrypted PIN Block
    public string[] GenerateIBMOffsetFromEncryptedISO0PINBlock(string zpk, string pvk, string encryptedPINBlock, string pan)
    {
        var ibmOffset = new string[2];

        try
        {
            var padPan = PaddedPan(pan); string pvd = PINValidationData(pan);
            var commandCode = "BK"; var pinBlockKeyType = "001";
            var pinBlockFormat = "01";zpk = "X" + zpk;
            string msg = HSMHeader() + commandCode + pinBlockKeyType + zpk + pvk + encryptedPINBlock +
                         pinBlockFormat + minPinLen + padPan + decimalizationTable + pvd;

            string hsmResponse = h.Send(msg);

            if ((hsmResponse != "No reply from HSM") && (hsmResponse != ""))
            {
                string errorCode = hsmResponse.Substring(HeaderLength + 2, 2);
                ibmOffset[0] = errorCode;
                if (errorCode == "00")
                {
                    ibmOffset[1] = hsmResponse.Substring(HeaderLength + 4, Convert.ToInt16(minPinLen));
                }
                else
                {
                    ibmOffset[1] = "";
                }
            }
            else
            {
                ibmOffset[0] = "99";
                ibmOffset[1] = "";
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return ibmOffset;
    }

}