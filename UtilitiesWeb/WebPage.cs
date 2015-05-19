using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;
using UtilitiesWeb;

namespace LinkChecker.WebSpider
{
    /// <summary>
    /// A result encapsulating the Url and the HtmlDocument
    /// </summary>
    [DataContract]
    [KnownType(typeof(IEnumerable<WebLink>))]
    [KnownType(typeof(WebLink))]
    [KnownType(typeof(WebPage.Error))]
    [KnownType(typeof(WebPage.ExceptionError))]
    [KnownType(typeof(WebPage.ExternalRedirect))]
    [KnownType(typeof(WebPage.Internal))]
    [KnownType(typeof(WebPage.LoadError))]
    [KnownType(typeof(WebPage.OtherContent))]
    [KnownType(typeof(WebPage.Timeout))]
    public abstract partial class WebPage : IStructureComparable<WebPage>
    {
        [IgnoreDataMember]
        public Uri Url { get; set;}

        [DataMember(Name="Url", Order=1)]
        public string UrlSerialized { get { return this.Url.AbsoluteUri; } set { this.Url = new Uri(value); } }

        [DataMember]
        private Uri uriRoot { get; set; }

        [DataMember]
        public DateTime DateTimeLastModified { get; set; }

        protected WebPage(Uri uri, Uri root, DateTime dateTimeLastModified)
        {
            this.Url = uri;
            this.uriRoot = root;
            this.DateTimeLastModified = dateTimeLastModified;
        }

        public abstract IEnumerable<string> StructureCompare(int indent, WebPage objectB);

        /// <summary>
        /// Error loading page
        /// </summary>
        [DataContract]
        public abstract class Error : WebPage
        {
            protected Error(Uri uri, Uri uriRoot, DateTime dateTimeLastModified)
                : base(uri, uriRoot, dateTimeLastModified)
            {
            }

            public override IEnumerable<string> StructureCompare(int indent, WebPage objectB)
            {
                if (objectB is Error) 
                    yield break;
                else
                    yield return "ERROR - Comparing an error page to " + objectB.GetType().Name;
            }
        }

        /// <summary>
        /// Timeout loading page (no HttpStatusCode)
        /// </summary>
        [DataContract]
        public class Timeout : Error
        {
            public Timeout(Uri uri, Uri root, DateTime dateTimeLastModified) : base(uri, root, dateTimeLastModified) { }
            public override IEnumerable<string> StructureCompare(int indent, WebPage objectB)
            {
                if (objectB is Timeout)
                    yield break;
                else
                    yield return "ERROR - Comparing a timeout to " + objectB.GetType().Name;
            }
            public override string ToString() { return "TIMEOUT: " + Url; }
        }

        /// <summary>
        /// Error loading page
        /// </summary>
        [DataContract]
        public class LoadError : Error
        {
            [DataMember]
            public Uri Referrer { get; private set; }

            [DataMember]
            public int HttpStatusCode { get; set; }

            public LoadError(Uri uri, HttpStatusCode httpStatusCode, Uri referrer, Uri root, DateTime dateTimeLastModified)
                : base(uri, root, dateTimeLastModified)
            {
                this.HttpStatusCode = (int)httpStatusCode;
                this.Referrer = referrer;
            }
            public override IEnumerable<string> StructureCompare(int indent, WebPage objectB)
            {
                if (objectB is LoadError)
                    yield break;
                else
                    yield return "ERROR - Comparing a Load error to " + objectB.GetType().Name;
            }
            public override string ToString() { return "ERROR: " + HttpStatusCode + ": " + Url + " Referrer:" + Referrer.AbsoluteUri; }
        }

        /// <summary>
        /// Non-http exception error
        /// </summary>
        [DataContract]
        public class ExceptionError : Error
        {
            [DataMember]
            public string ExceptionMessage { get; set; }

            public ExceptionError(Uri uri, Exception ex, Uri root, DateTime dateTimeLastModified) : base(uri, root, dateTimeLastModified) 
            { 
                this.ExceptionMessage = ex.ToString(); 
            }

            public override IEnumerable<string> StructureCompare(int indent, WebPage objectB)
            {
                if (objectB is ExceptionError)
                    yield break;
                else
                    yield return "ERROR - Comparing an exception page to " + objectB.GetType().Name;
            }

            public override string ToString() { return "ERROR: " + this.ExceptionMessage + " " + Url; }
        }

        /// <summary>
        /// External page - not followed
        /// </summary>
        /// <remarks>
        /// No body - go load it yourself
        /// </remarks>
        [DataContract]
        [KnownType(typeof(WebPage.ExternalRedirect))]
        public class External : WebPage
        {
            public External(Uri uri, Uri uriRoot, DateTime dateTimeLastModified) : base(uri, uriRoot, dateTimeLastModified) { }

            public override IEnumerable<string> StructureCompare(int indent, WebPage objectB)
            {
                // We ignore any changes on external pages - they will always be external, we don't care!
                yield break;
            }

            public override string ToString() { return "EXTERNAL PAGE NOT FOLLOWED " + this.Url; }
        }


        /// <summary>
        /// Redirect to an external page - not followed
        /// </summary>
        [DataContract]
        public class ExternalRedirect : External
        {
            [DataMember]
            private string uriRedirect;

            public ExternalRedirect(Uri uri, string redirect, Uri uriRoot, DateTime dateTimeLastModified) : base(uri, uriRoot, dateTimeLastModified) { this.uriRedirect = redirect; }

            public override IEnumerable<string> StructureCompare(int indent, WebPage objectB)
            {
                // We ignore any changes on external pages - they will always be external, we don't care!
                yield break;
            }

            public override string ToString() { return "EXTERNAL REDIRECT FROM " + this.Url + " TO " + this.uriRedirect; }

            // We don't care if the external redirect changes - maybe we should - check implemtation of comparable ...
        }


        /// <summary>
        /// Some other kind of content we don't care about
        /// </summary>
        [DataContract]
        public class OtherContent : WebPage
        {
            [DataMember]
            public string MimeType { get; private set; }

            public OtherContent(Uri uri, Uri uriRoot, string mimeType, DateTime dateTimeLastModified) : base(uri, uriRoot, dateTimeLastModified) { this.MimeType = mimeType; }


            public override string ToString() { return "OTHER CONTENT " + this.MimeType + " " + this.Url; }

            public override IEnumerable<string> StructureCompare(int indent, WebPage objectB)
            {
                WebPage.OtherContent wib = objectB as WebPage.OtherContent;
                if (wib == null)
                    yield return "".PadLeft(indent) + "OTHERCONTENT + " + this.Url + " NOT THE SAME TYPE";
                else
                    yield break;        // same Url, same type, that's good enough for us (didn't check same file type, oh well ...)
            }
        }

    }


    /// <summary>
    /// Two pages are Equal if they have the same Url - from the point of view of forming a union or intersection!
    /// </summary>
    public class PageEqualityComparer : IEqualityComparer<WebPage>
    {
        public bool Equals(WebPage x, WebPage y)
        {
            if (!string.Equals(x.Url.AbsoluteUri, y.Url.AbsoluteUri, StringComparison.InvariantCultureIgnoreCase)) return false;       // Must have same URL
            if (x.GetType() != y.GetType()) return false;                   // May have gone from OK to error
            return true;
        }

        public int GetHashCode(WebPage obj)
        {
            if (obj.Url == null) return obj.GetType().GetHashCode();
            else return obj.Url.AbsoluteUri.ToLower().GetHashCode() ^ obj.GetType().GetHashCode();
        }

    }

}