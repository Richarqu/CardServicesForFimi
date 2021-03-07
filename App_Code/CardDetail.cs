using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// Summary description for CardDetail
/// </summary>
public class CardDetail
{
    public string PAN { get; set; }
    public string ExpiryDate { get; set; }
    public string CustomerId { get; set; }
    public string CardProvider { get; set; }
    public string CardName { get; set; }
    public string Cvv2 { get; set; }
    public string CardStatus { get; set; }
    public string BlockStatus { get; set; }
}