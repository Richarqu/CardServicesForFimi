using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Net.Mail;
using System.Configuration;
using log4net;

/// <summary>
/// Summary description for Mailer
/// </summary>
public class Mailer
{
    readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    public Mailer()
    {
        //
        // TODO: Add constructor logic here
        //
    }
    public string mailFrom = "noreply@Sterlingbankng.com";
    public string mailHost = ConfigurationManager.AppSettings["mailHost"];
    public int mailPort = 25;
    public string mailBody = "";
    public string mailSubject = "";

    public bool msgHtml = true;
    public MailMessage message = new MailMessage();

    public void addFrom(string sender)
    {
        MailAddress address = new MailAddress(sender);
        message.From = address;
    }
    public Mailer(string email)
    {
        MailAddress address = new MailAddress(email);
        message.From = address;
    }

    public void addTo(string temail)
    {
        MailAddress address = new MailAddress(temail);
        message.To.Add(address);
    }
    public void addCC(string cemail)
    {
        MailAddress address = new MailAddress(cemail);
        message.CC.Add(address);
    }

    public void attchFile(string file, string ctype)
    {
        //ContentType ct = new ContentType("application/zip");
        Attachment attach = new Attachment(file);
        message.Attachments.Add(attach);
    }

    public void addBCC(string[] bemail)
    {
        for (int b = 0; b < bemail.Length; b++)
        {
            MailAddress address = new MailAddress(bemail[b]);
            message.Bcc.Add(address);
        }
    }

    public bool sendTheMail()
    {
        SmtpClient smtpClient = new SmtpClient();
        NetworkCredential nc = new NetworkCredential("crdserv@sterlingbankng.com", "k@t@n@098");
        try
        {
            //Set the mail From
            //message.From = new MailAddress(mailFrom);
            // Set the subject of the mail message
            message.Subject = mailSubject;
            //Set the body of the mail message
            message.Body = mailBody;
            // Set the format of the mail message body either HTML OR not
            message.IsBodyHtml = msgHtml;
            //Use Default credentials OR nor
            smtpClient.UseDefaultCredentials = false;
            //Smtp Client credentials
            smtpClient.Credentials = nc;
            //Smtp Host
            smtpClient.Host = mailHost;
            //Smtp Client
            smtpClient.Port = mailPort;
            // Set the priority of the mail message to normal
            message.Priority = MailPriority.Normal;
            //Send the message
            smtpClient.Send(message);
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex);
            return false;
        }
        finally
        {
            message.Dispose();

        }
    }
}