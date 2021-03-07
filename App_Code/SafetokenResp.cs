using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


// NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://schemas.xmlsoap.org/soap/envelope/", IsNullable = false)]
public partial class Envelope
{

    private EnvelopeBody bodyField;

    /// <remarks/>
    public EnvelopeBody Body
    {
        get
        {
            return this.bodyField;
        }
        set
        {
            this.bodyField = value;
        }
    }
}

/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
public partial class EnvelopeBody
{

    private AddCardHolderResponse addCardHolderResponseField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://services.interswitchng.com/")]
    public AddCardHolderResponse AddCardHolderResponse
    {
        get
        {
            return this.addCardHolderResponseField;
        }
        set
        {
            this.addCardHolderResponseField = value;
        }
    }
}

/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://services.interswitchng.com/")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://services.interswitchng.com/", IsNullable = false)]
public partial class AddCardHolderResponse
{

    private AddCardHolderResponseAddCardHolderResult addCardHolderResultField;

    /// <remarks/>
    public AddCardHolderResponseAddCardHolderResult AddCardHolderResult
    {
        get
        {
            return this.addCardHolderResultField;
        }
        set
        {
            this.addCardHolderResultField = value;
        }
    }
}

/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://services.interswitchng.com/")]
public partial class AddCardHolderResponseAddCardHolderResult
{

    private uint responseCodeField;

    private object responseDescriptionField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://schemas.datacontract.org/2004/07/TechQuest.Framework.ServiceFramework.Cont" +
        "ract")]
    public uint ResponseCode
    {
        get
        {
            return this.responseCodeField;
        }
        set
        {
            this.responseCodeField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://schemas.datacontract.org/2004/07/TechQuest.Framework.ServiceFramework.Cont" +
        "ract", IsNullable = true)]
    public object ResponseDescription
    {
        get
        {
            return this.responseDescriptionField;
        }
        set
        {
            this.responseDescriptionField = value;
        }
    }
}

