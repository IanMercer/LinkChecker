using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using System.Net;
using System.Threading;
using System.Text.RegularExpressions;

namespace LinkChecker.WebSpider
{

    public class WebPageWithDocument
    {
        public WebPage WebPage { get; set; }
        public HtmlDocument HtmlDocument { get; set; }

        public string CleanText
        {
            get
            {
                return UtilitiesWeb.PageCleaner.Process(this.HtmlDocument);
            }
        }

    }

    /// <summary>
    /// A web spider
    /// </summary>
    public abstract class Spider
    {
        public static IEnumerable<WebPage> GetAllPagesUnder(Uri urlRoot, int delayBetweenPagesInSeconds, Regex[] excludedPaths)
        {
            foreach (var pair in GetAllPagesUnderWithDocument(urlRoot, delayBetweenPagesInSeconds, excludedPaths))
                yield return pair.WebPage;
        }

        public static IEnumerable<WebPage> GetAllPagesUnder(Uri urlRoot, int delayBetweenPagesInSeconds)
        {
            foreach (var pair in GetAllPagesUnderWithDocument(urlRoot, delayBetweenPagesInSeconds, new Regex[0]))
                yield return pair.WebPage;
        }


        /// <summary>
        /// Get every WebPage.Internal on a web site (or part of a web site) visiting all internal links just once
        /// plus every external page (or other Url) linked to the web site as a WebPage.External
        /// </summary>
        /// <remarks>
        /// Use .OfType WebPage.Internal to get just the internal ones if that's what you want
        /// </remarks>
        public static IEnumerable<WebPageWithDocument> GetAllPagesUnderWithDocument(Uri urlRoot, int delayBetweenPagesInSeconds, Regex[] excludedPaths)
        {
            // Get the root page ...

            HttpWebResponse webResponse = FetchWebPageWithRetries(urlRoot);

            if (webResponse.ResponseUri.AbsoluteUri != urlRoot.AbsoluteUri)
            {
                Console.WriteLine("*** ROOT REQUEST WAS REDIRECTED USING: " + webResponse.ResponseUri + "***");
                urlRoot = webResponse.ResponseUri;
            }

            var queue = new QueueWithDeduping<PendingCrawlItem>();

            var start = PendingCrawlItem.Factory(urlRoot:urlRoot, href:urlRoot, referrer:urlRoot);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                // pull an item off the queue, inspect it and deal with it
                var nextItem = queue.Dequeue();

                if (excludedPaths.Any(p => p.IsMatch(nextItem.Uri.AbsoluteUri)))
                {
                    Console.WriteLine("Skipping " + nextItem.Uri + " because path is excluded");
                    continue;
                }

                var result = ProcessNextItem(nextItem, queue);
                yield return result;
                // And delay but only on internal pages - external pages don't count
                if (!(result.WebPage is WebPage.External))
                    Thread.Sleep(delayBetweenPagesInSeconds);
            }
        }

        /// <summary>
        /// Process a single url, add items to queue as necessary - no exceptions!!
        /// </summary>
        private static WebPageWithDocument ProcessNextItem(PendingCrawlItem item, QueueWithDeduping<PendingCrawlItem> queue)
        {
            Uri urlRoot = item.UrlRoot;

            // For external items we don't load or crawl them
            if (item is PendingCrawlItem.PendingExternalWebPage)
                return new WebPageWithDocument { WebPage = new WebPage.External(item.Uri, item.UrlRoot, DateTime.UtcNow)};      // We don't care about date in this case

            // It must be an internal page ...
            try
            {
                Uri url = item.Uri;

                HttpWebResponse webResponse = FetchWebPageWithRetries(url);
                DateTime lastModified = webResponse.LastModified;

                if ((int)webResponse.StatusCode >= 300 && (int)webResponse.StatusCode <= 399)
                {
                    // This means we have been directed off the domain we were on
                    string uriString = webResponse.Headers["Location"];
                    return new WebPageWithDocument { WebPage = new WebPage.ExternalRedirect(url, uriString, urlRoot, lastModified)};
                }

                if (webResponse.ContentType.StartsWith("text/html", StringComparison.InvariantCultureIgnoreCase))
                {
                    // COULD LOOK AT LAST-MODIFIED DATE AND PULL FROM CACHE ONLY AS NECESSARY / NOT READ THE WHOLE PAGE OVER AND OVER!
                    //foreach (var header in webResponse.Headers)
                    //{
                    //    Console.WriteLine(header);
                    //}

                    WebPage.Internal result = null;
                    HtmlDocument doc = new HtmlDocument();
                    using (var resultStream = webResponse.GetResponseStream())
                    {
                        doc.Load(resultStream, System.Text.Encoding.UTF8);                   // The HtmlAgilityPack

                        if (doc.DocumentNode != null && doc.DocumentNode.FirstChild != null && doc.DocumentNode.FirstChild.Name == "?xml")
                        {
                            Console.WriteLine("********* NOT AN HTML DOCUMENT, THIS IS XML ************");
                            return new WebPageWithDocument { WebPage = new WebPage.OtherContent(url, urlRoot, "text/xml", lastModified)};
                        }
                        else
                        {
                            result = new WebPage.Internal(uri: url, root: urlRoot, dateTimeLastModified: lastModified);
                        }
                        resultStream.Close();
                    }

                    // TODO: Look at HttpStatus Code: 500 or 404 is not OK (but that would throw exception, right?)

                    result.ProcessDocumentToFindLinks(doc);

                    if (webResponse.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine("********* ODD, WHY DIDN'T THIS THROW AN EXCEPTION ************");
                    }

                    // THIS WAS WRONG - HAD A PAGE ON BLOG CALLED JAVASCRIPT ERROR REPORTING
                    ////// Some web sites have error report pages - we don't want to report them as proper pages!  Should handle this using httpStatus codes!
                    ////if (result.Title != null && result.Title.ToLower().Contains("error report"))
                    ////{
                    ////    return new WebPage.LoadError(url, HttpStatusCode.OK, item.UriReferrer, urlRoot);
                    ////}

                    foreach (var link in result.Links.OfType<WebUrlLink>())
                    {
                        var pendingItem = PendingCrawlItem.Factory(urlRoot:urlRoot, href:link.Href, referrer:url);
                        queue.Enqueue(pendingItem);
                    }

                    result.HtmlDocument = doc;          // put the document in a wekreference on the web page internal

                    // Success, hand off the page
                    return new WebPageWithDocument { WebPage = result, HtmlDocument = doc };
                }
                else
                {
                    // OTHER TYPES OF CONTENT - need to close the resultstream!
                    var resultStream = webResponse.GetResponseStream();         // Exceptions abound ... handled below
                    resultStream.Close();

                    // could return a value for other types of files ... result = new Internal() { Url = url, HtmlDocument = doc };
                    //Console.WriteLine("  Response was a " + webResponse.StatusCode + "  " + webResponse.ContentType);
                    return new WebPageWithDocument { WebPage = new WebPage.OtherContent(item.Uri, item.UrlRoot, webResponse.ContentType, lastModified) };
                }

            }
            // And if we got any kind of exception at all, return an appropriate web page problem object
            catch (WebException webException)
            {
                HttpStatusCode httpCode = 0;
                if (webException != null)
                {
                    var respX = webException.Response as System.Net.HttpWebResponse;
                    if (respX != null) httpCode = respX.StatusCode;
                }
                if (webException.Message.ToLower().Contains("timeout") || webException.Message.ToLower().Contains("timed out"))
                    return new WebPageWithDocument { WebPage = new WebPage.Timeout(item.Uri, urlRoot, DateTime.UtcNow) };
                else
                    return new WebPageWithDocument { WebPage = new WebPage.LoadError(item.Uri, httpCode, item.UriReferrer, urlRoot, DateTime.UtcNow) };
            }
            catch (Exception exception)
            {
                return new WebPageWithDocument { WebPage = new WebPage.ExceptionError(item.Uri, exception, urlRoot, DateTime.UtcNow) };
            }
        }

        public static HttpWebResponse FetchWebPageWithRetries(Uri url, int timeoutMs = 10000)
        {
            HttpWebResponse webResponse = null;

            int retries = 1;
            int safety = 10;        // because I don't like while(true) loops!

            while (safety-- > 0)
            {

                if (url.AbsolutePath.Contains("Error.aspx")) return null;

                try
                {
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
                    webRequest.AllowAutoRedirect = false;
                    webRequest.Accept = "text/html,application/xhtml+xml,application/rss+xml, application/rdf+xml,application/xml;q=0.9,*/*;q=0.8";

                    webRequest.UserAgent = @"Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US; rv:1.9.2.9) Gecko/20100824 Firefox/3.6.9 ( .NET CLR 3.5.30729; .NET4.0E)"; // LinkCheckerBot";
                    webRequest.Timeout = timeoutMs;           // timeout 10s

                    // Get the response ...
                    webResponse = (HttpWebResponse)webRequest.GetResponse();

                    // Now look to see if it's a redirect, if to same domain (or www. +/- same domain, we can keep going, following it)
                    if ((int)webResponse.StatusCode >= 300 && (int)webResponse.StatusCode <= 399)
                    {
                        string uriString = webResponse.Headers["Location"];
                        Console.WriteLine("Redirect to " + uriString ?? "NULL");
                        webResponse.Close();                    // don't forget to close it - or bad things happen!

                        if (string.IsNullOrWhiteSpace(uriString))
                        {
                            throw new WebException("Invalid redirect location for " + url);
                        }

                        Uri newUri = new Uri(uriString, UriKind.RelativeOrAbsolute);

                        // make absolute
                        if (!newUri.IsAbsoluteUri)
                        {
                            newUri = new Uri(url, newUri);
                        }

                        if (newUri.Authority.Replace("www.", "") == url.Authority.Replace("www.", ""))
                        {
                            // same domian (ish)
                            url = newUri;
                            continue;
                        }
                        else
                        {
                            // redirected off domain!
                            break;
                        }
                    }
                    else
                    {
                        break;  // and get out of the while loop
                        // leaving the web response open
                    }
                }
                catch (WebException exception)
                {
                    //Console.WriteLine(exception.Message);
                    if (exception.Message.ToLower().Contains("timeout") || exception.Message.ToLower().Contains("timed out"))
                    {
                        if (--retries > 0)
                        {
                            Console.WriteLine("RETRY " + url.AbsoluteUri);
                            Thread.Sleep(10000);         // extra 10s delay after a timeout
                            continue;
                        }
                    }
                    throw;      // can't handle it, or have retried too many times
                }
            }
            return webResponse;
        }

    }
}