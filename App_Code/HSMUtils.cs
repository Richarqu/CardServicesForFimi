using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using ThalesSim.Core;
using ThalesSim.Core.TCP;
using ThalesSim.Core.Cryptography;
using log4net;

/// <summary>
/// Summary description for HsmUtils
/// </summary>
public class HsmUtils
{
    WorkerClient Thales;
    public string ThalesData = string.Empty;
    public string hsmIP;
    public int hsmPort = 9990;


    ILog logger = LogManager.GetLogger("CardServicesLog");

    public HsmUtils()
    {
        //
        // TODO: Add constructor logic here
        //
    }

    //Establish Connection to the HSM
    public void Connect()
    {
        hsmIP = ConfigurationManager.AppSettings["HsmHost"];
        hsmPort = int.Parse(ConfigurationManager.AppSettings["HsmPort"]);
        this.Thales = new WorkerClient(new TcpClient(hsmIP, hsmPort));
        //this.Thales = new WorkerClient(new TcpClient(ConfigurationManager.AppSettings["HsmHost"], int.Parse(ConfigurationManager.AppSettings["HsmPort"])));
        this.Thales.MessageArrived += new WorkerClient.MessageArrivedEventHandler(thales_MessageArrived);
        this.Thales.InitOps();
    }

    //Terminate the Worker Client session
    public void Close()
    {
        Thales.TermClient();
    }

    //Send Command to the HSM
    public string Send(string command)
    {        
        string reply = string.Empty;
        try
        {
            this.Connect();
            reply = SendFunctionCommand(command);
            reply = string.IsNullOrEmpty(reply) ? "No reply from HSM" : reply;
            this.Close();
        }
        catch(Exception ex)
        {
            //
            logger.Error(ex.ToString());// throw;
            logger.Debug("Executing HSM Command :- "+ command);
            logger.Error("Unable to connect to "+ hsmIP +" on port "+ hsmPort);
        }
        finally
        {
            this.Close();
        }

        return reply;
    }

    //Fetch data sent by the HSM
    private void thales_MessageArrived(WorkerClient sender, ref byte[] b, int len)
    {
        string s = string.Empty;

        for (int i = 0; i < len; i++)
        {
            s = s + Convert.ToChar(b[i]);
        }

        this.ThalesData = s;
    }

    //Send the Command to the HSM
    private string SendFunctionCommand(string s)
    {
        ThalesData = "";
        this.Thales.send(s);

        while (ThalesData == string.Empty && this.Thales.IsConnected)
        {
            System.Threading.Thread.Sleep(1);
        }

        if (!this.Thales.IsConnected)
        {
            return string.Empty;
        }
        else
        {
            return ThalesData;
        }
    }

    //XOR two hexadecimal Strings
    public string XORHexStringsFull(string hex1, string hex2)
    {
        var xorResult = string.Empty;
        try
        {
            xorResult = Utility.XORHexStringsFull(hex1, hex2);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return xorResult;
    }

    //XOR two hexadecimal Strings
    public string XORHexStrings(string hex1, string hex2)
    {
        var xorResult = string.Empty;

        try
        {
            xorResult = Utility.XORHexStrings(hex1, hex2); ;
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return xorResult;
    }

    //Do TripleDES Decryption
    public string TripleDESDecryption(string encryptedData, string key)
    {
        var decryptedData = string.Empty;

        try
        {
            HexKey hKey = new HexKey(key);
            decryptedData = TripleDES.TripleDESDecrypt(hKey, encryptedData);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return decryptedData;
    }

    //Do TripleDES Encryption
    public string TripleDesEncryption(string clearData, string key)
    {
        var encryptedData = string.Empty;

        try
        {
            HexKey hKey = new HexKey(key);
            encryptedData = TripleDES.TripleDESEncrypt(hKey,clearData);
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
        }

        return encryptedData;
    }
}