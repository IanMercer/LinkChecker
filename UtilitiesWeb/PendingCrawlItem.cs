using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LinkChecker.WebSpider
{
    /// <summary>
    /// An item waiting in the queue to be processed
    /// </summary>
    public abstract class PendingCrawlItem
    {
        public Uri UrlRoot { get; private set; }
        public Uri Uri { get; private set; }
        public Uri UriReferrer { get; private set; }

        /// <summary>
        /// Create a PendingCrawlItem from an href which may be relative or absolute (so need urlRoot also)
        /// </summary>
        /// <param name="urlRoot"></param>
        /// <param name="href"></param>
        /// <returns>An instance of either a PendingLocalWebPage or a PendingExternalWebPage</returns>
        public static PendingCrawlItem Factory (Uri urlRoot, Uri href, Uri referrer)
        {
            // Make it absolute if it's relative
            if (!href.IsAbsoluteUri)
            {
                href = new Uri(urlRoot, href);
            }

            if (href.Scheme == "mailto") throw new MailToException("Can't crawl a mailto " + href.ToString());

            PendingCrawlItem result;

            // used to use .isBaseOf on Uri class but that doesn't work unless the case is the same! (Windows vs Unix issue!!!)


            if (!string.Equals(href.Authority.Replace("www.",""), urlRoot.Authority.Replace("www.",""), StringComparison.InvariantCultureIgnoreCase))
            {
                // Completely different authority = off domain (modulo having a www or not on there)
                return new PendingExternalWebPage() { Uri = href, UrlRoot = urlRoot, UriReferrer = referrer };
            }

            // Is it on site and under the base directory specified?
            // NOTE: Use PATH here and NOT AbsoluteUri because we may be on www. versus non-www. domains
            // Could instead do the same www. trick as above??
            string rootMappedToNothing = new Uri(urlRoot, "").AbsolutePath;                                   // Probably empty or /
            string hrefMappedToX = new Uri(href, "X").AbsolutePath.TrimEnd("0123456789".ToArray());           // take any numbers off the end (paging)
            if (hrefMappedToX.StartsWith(rootMappedToNothing))
            {
                result = new PendingLocalWebPage() { Uri = href, UrlRoot = urlRoot, UriReferrer = referrer };
                return result;
            }

            //string cleanRoot = urlRoot.AbsolutePath;

            ////cleanRoot = ignorableTail.Replace(cleanRoot, "");

            //if (href.AbsolutePath.StartsWith(cleanRoot.TrimEnd('/'), StringComparison.InvariantCultureIgnoreCase))
            //{
            //    result = new PendingLocalWebPage() { Uri = href, UrlRoot = urlRoot, UriReferrer = referrer };
            //}
            //else
            {
                // This isn't really an ExternalWebPage, it's an Excluded Web page .. TODO: make new class here
                result = new PendingExternalWebPage() { Uri = href, UrlRoot = urlRoot, UriReferrer = referrer };
            }
            return result;
        }

        private PendingCrawlItem() { }

        public class PendingLocalWebPage : PendingCrawlItem { }

        public class PendingExternalWebPage : PendingCrawlItem {  }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            PendingCrawlItem other = obj as PendingCrawlItem;
            if (other == null) return false;
            return other.Uri == this.Uri;
        }

        public override int GetHashCode()
        {
            return this.Uri.GetHashCode();
        }

    }
}
