using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using UtilitiesWeb;

namespace LinkChecker.WebSpider
{
    /// <summary>
    /// MailToException
    /// </summary>
    public class MailToException : Exception
    {
        public MailToException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// A link
    /// </summary>
    [DataContract]
    [KnownType(typeof(WebLink.StatusCode))]
    [KnownType(typeof(WebUrlLink))]
    [KnownType(typeof(MailToLink))]
    public abstract class WebLink : IStructureComparable<WebLink> // : IXmlSerializable
    {
        [DataMember(Name = "Href")]
        public string HrefSerialized { get; set; }

        public enum StatusCode
        {
            NotSet = 0,
            Normal = 1,
            ExpectedButNotPresent = 2,
            PresentAndExpected = 3,
            PresentButNotExpected = 4
        }

        [DataMember]
        public StatusCode Status { get; set; }

        public IEnumerable<string> StructureCompare(int indent, WebLink objectB)
        {
            // This will never happen - Uris only match or they don't match - they don't CHANGE!
            if (this.HrefSerialized != objectB.HrefSerialized)
                yield return "".PadLeft(indent) + "Mismatched Uri " + this.HrefSerialized;
            yield break;
        }

        public override string ToString()
        {
            return "Link: " + this.HrefSerialized;
        }


        public static WebLink Factory(Uri urlNextUri)
        {
            if (urlNextUri.Scheme == "mailto")
            {
                return new MailToLink() { HrefSerialized = urlNextUri.OriginalString, Status = StatusCode.Normal } ;
            }
            else
            {
                return new WebUrlLink(urlNextUri){ Status = StatusCode.Normal};
            }
        }

    }



    /// <summary>
    /// A link
    /// </summary>
    [DataContract]
    [KnownType(typeof(WebLink.StatusCode))]
    public class WebUrlLink : WebLink
    {
        [IgnoreDataMember]
        public Uri Href 
        { 
            get { return new Uri(this.HrefSerialized);}
            set 
            {
                // Wordpress seems to flip between adding a tinyurl.com link on the end of some web links or not ...
                // Removed: Link: http://twitter.com/home?status=Seattle by Night+-+
                // Added: Link: http://twitter.com/home?status=Seattle by Night+-+http://tinyurl.com/22nmy42

                // if it has a tinyurl +-+ on the end, rip it off
                string url = value.ToString();
                int i = url.IndexOf("+-+http://tinyurl.com/");
                if (i > 0)
                    url = url.Substring(0, i);

                // sometimes it has just this ... at the end
                if (url.EndsWith("+-+"))
                    url = url.Substring(0, url.Length - 3);

                this.HrefSerialized = url; 
            } 
        }

        public WebUrlLink(Uri href)
        {
            if (href.Scheme == "mailto") throw new MailToException("Cannot use mailto in a WebUrlLink");
            this.Href = href;
        }
    }

    public class LinkEqualityComparer : IEqualityComparer<WebLink>
    {
        public bool Equals(WebLink x, WebLink y)
        {
            string xCanon = x.HrefSerialized.ToLower();
            string yCanon = y.HrefSerialized.ToLower();

            if (xCanon == yCanon) return true;

            // THIS WILL NOT WORK UNLESS HASHCODE IS OVERRIDDEN TOO
            ////if ((xCanon.StartsWith(y.HrefSerialized, StringComparison.CurrentCultureIgnoreCase) || yCanon.StartsWith(x.HrefSerialized, StringComparison.CurrentCultureIgnoreCase)))
            ////{
            ////    if (xCanon.Contains("http://tinyurl.com/") || yCanon.Contains("http://tinyurl.com/"))
            ////    {
            ////        Console.WriteLine("Common start for " + (xCanon.Length > yCanon.Length ? xCanon : yCanon));
            ////        return true;
            ////    }
            ////}

            return false;
        }

        public int GetHashCode(WebLink obj)
        {
            return obj.HrefSerialized.ToLower().GetHashCode();
        }
    }



    /// <summary>
    /// A mailto link
    /// </summary>
    [DataContract]
    [KnownType(typeof(WebLink.StatusCode))]
    public class MailToLink : WebLink
    {
        public IEnumerable<string> StructureCompare(int indent, MailToLink objectB)
        {
            // This will never happen - Uris only match or they don't match - they don't CHANGE!
            if (this.HrefSerialized != objectB.HrefSerialized)
                yield return "".PadLeft(indent) + "Mismatched mailto " + this.HrefSerialized;
            yield break;
        }

        public override string ToString()
        {
            return "Mailto: " + this.HrefSerialized;
        }
    }

}
