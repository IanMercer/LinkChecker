using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HtmlAgilityPack;
using UtilitiesWeb;
using System.Net;

namespace LinkChecker.WebSpider
{
    public partial class WebPage
    {
        /// <summary>
        /// Internal page 
        /// </summary>
        [DataContract]
        public class Internal : WebPage
        {
            private WeakReference htmlDocument;

            /// <summary>
            /// For internal pages we load the document for you - this is NOT persisted!
            /// </summary>
            [IgnoreDataMember]
            public virtual HtmlDocument HtmlDocument
            {
                get
                {
                    HtmlDocument doc = (HtmlDocument)this.htmlDocument.Target;
                    if (doc == null)
                    {
                        Console.WriteLine("Warning: refetching document, only load documents immediately after fetching");
                        HttpWebResponse webResponse = Spider.FetchWebPageWithRetries(this.Url, 30000);
                        if (webResponse != null)
                        {
                            try
                            {
                                if (webResponse.ContentType.StartsWith("text/html", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    using (var resultStream = webResponse.GetResponseStream())
                                    {
                                        doc.Load(resultStream); // The HtmlAgilityPack
                                        this.htmlDocument = new WeakReference(doc);
                                    }
                                }
                            }
                            finally
                            {
                                webResponse.Close();
                            }
                        }
                    }
                    return doc;
                }
                internal set
                {
                    this.htmlDocument = new WeakReference(value);
                }
            }

            [DataMember]
            public string Title { get; private set; }

            [DataMember]
            public string MetaDescription { get; private set; }

            [DataMember]
            public bool HasErrors 
            { 
                get { return this.errorMessages.Count() > 0; }
                set 
                { 
                    // do nothing .. during deserialization ... 
                }
            }

            [DataMember]
            public bool HasWarnings 
            { 
                get { return this.seoWarnings.Count() > 0; }
                set 
                { 
                    // do nothing .. during deserialization ... 
                }
            }

            [IgnoreDataMember]
            private List<string> errorMessages = new List<string>();

            [IgnoreDataMember]
            public IEnumerable<string> ErrorMessages { get { return errorMessages; } }

            [IgnoreDataMember]
            private List<string> seoWarnings = new List<string>();

            [IgnoreDataMember]
            public IEnumerable<string> SeoWarnings { get { return seoWarnings; } }

            [IgnoreDataMember]
            private bool processed = false;

            [IgnoreDataMember]
            private List<WebLink> links = new List<WebLink>();

            //            [CollectionDataContract(ItemName = "WebLink")]
            [DataMember(Name="Links", Order=10)]
            public IEnumerable<WebLink> Links { get { return this.links; } set { this.links = new List<WebLink>(value); } }

            public Internal(Uri uri, Uri root, DateTime dateTimeLastModified)
                : base(uri, root, dateTimeLastModified)
            {
                processed = false;
                this.Title = null;
                this.MetaDescription = null;
            }

            public override IEnumerable<string> StructureCompare(int indent, WebPage objectB)
            {
                WebPage.Internal wib = objectB as WebPage.Internal;
                if (wib == null) return new[] { "".PadLeft(indent) + "WEBPAGE + " + this.Url + " NOT THE SAME TYPE" };
                else
                {
                    var heading = new[] { "".PadLeft(indent) + "WEBPAGE " + this.Url };
                    IEnumerable<string> nullResult = new List<string>();
                    var comparisonResult = StructureComparison.Compare<WebPage.Internal>(indent, this, wib, wp => wp.Title, wp => wp.MetaDescription);
                    // Now handle the links on it
                    var linkResult = StructureComparison.CompareList<WebLink>(indent + 3, this.Links, wib.Links, new LinkEqualityComparer());
                    if (comparisonResult.Count() > 0 || linkResult.Count() > 0)
                        return heading.Concat(comparisonResult.Concat(linkResult));
                    else
                        return nullResult;
                }
            }


            /// <summary>
            /// Processes the document for an Internal Page and processes all the metadata and links
            /// </summary>
            public void ProcessDocumentToFindLinks(HtmlDocument doc)
            {
                if (processed) return;
                processed = true;

                string requiredLinkMeta = "";

                var titles = doc.DocumentNode.SelectNodes(@"/html/head/title");
                if (titles != null)
                {
                    if (titles.Count() > 1)
                    {
                        this.errorMessages.Add ("*** ERROR: MORE THAN ONE TITLE TAG FOUND ****");
                        //                        log.Debug(" *** MORE THAN ONE TITLE");
                    }
                    foreach (var title in titles.Take(1))
                    {
                        string titleText = title.InnerText.Replace("\r", "").Replace("\n", "").Trim();
                        //                        log.Debug("  title       = " + titleText);
                        this.Title = titleText;
                        if (titleText.Length < 10)
                            seoWarnings.Add("Title is rather short '" + titleText + "'");
                        if (titleText.Length > 70)
                            seoWarnings.Add("Title is too long '" + titleText + "' (" + titleText.Length + ")");
                    }

                }

                var meta = doc.DocumentNode.SelectNodes(@"//meta[@content]");
                if (meta != null)
                {
                    foreach (var m in meta)
                    {
                        if (m.Attributes["http-equiv"] != null && string.Equals(m.Attributes["http-equiv"].Value, "refresh", StringComparison.InvariantCultureIgnoreCase))
                        {
                            // Deal with horrid old fashioned sites ... (like Microsoft.com!)
                            // <META HTTP-EQUIV="Refresh" CONTENT="0; URL=default.mspx">
                            if (m.Attributes["content"] != null)
                            {
                                string equivContent = m.Attributes["content"].Value;
                                int indexOfUrl = equivContent.IndexOf("URL=", StringComparison.InvariantCultureIgnoreCase);
                                if (indexOfUrl >= 0)
                                {
                                    string newUrl = (equivContent + " ").Substring(indexOfUrl + 4);
                                    // Now convert that to absolute ...
                                    Uri urlNextUri = new Uri(newUrl, UriKind.RelativeOrAbsolute);
                                    if (!urlNextUri.IsAbsoluteUri)
                                    {
                                        // Wow, this is fragile if the trailing / is missing and the redirect wasn't absolute!
                                        urlNextUri = new Uri(this.Url, urlNextUri);
                                    }
                                    this.links.Add(new WebUrlLink(urlNextUri){ Status = WebLink.StatusCode.Normal });
                                }
                            }
                        }

                        if (m.Attributes["name"] == null || m.Attributes["content"] == null) continue;
                        string mName = m.Attributes["name"].Value;
                        string mValue = m.Attributes["content"].Value;

                        switch (mName.ToLower())
                        {
                            case "robots":
                                break;
                            case "description":
                                this.MetaDescription = mValue;
                                //                                Console.WriteLine("  " + mName + " = " + mValue);
                                break;
                            case "keywords":
                                //Console.WriteLine(mName + " = " + mValue);
                                break;
                            case "linkedpages":
                                //                                Console.WriteLine("  " + mName + " = " + mValue);
                                requiredLinkMeta = mValue;
                                break;
                            default:
                                //Console.WriteLine("???" + mName + " = " + mValue);
                                break;
                        }
                    }
                }


                // Base Uri
                Uri baseUrl = this.Url;


                // Is there a BASE tag on the page?

                var baseTag = doc.DocumentNode.SelectSingleNode(@"/html/head/base");
                if (baseTag != null)
                {
                    string hrefBase = baseTag.GetAttributeValue("href", "");
                    if (!string.IsNullOrWhiteSpace(hrefBase))
                        baseUrl = new Uri(hrefBase);
                }



                // Get the required links ...

                Dictionary<string, int> requiredLinks = requiredLinkMeta
                                                .Split(',')
                                                .Select(x => x.Trim().ToLower())
                                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                                .Distinct()                                     // Just in case any were duplicated
                                                .ToDictionary(x => x, x => 1);                  // need one for each (can add a count later)

                var suggestedLinks = new HashSet<string>();
                string from = "/" + this.uriRoot.MakeRelativeUri(this.Url).OriginalString;      // the relative address of this current page

                var links = doc.DocumentNode.SelectNodes(@"//a[@href]");
                //var maps = doc.DocumentNode.SelectNodes(@"//area[@href]") ?? Enumerable.Empty<HtmlNode>();
                //var together = links.Concat(maps);

                foreach (var link in links)
                {
                    var hrefAttr = link.Attributes["href"];
                    string urlNext = hrefAttr.Value.Replace("&amp;", "&").Replace("%20", "").Trim();            
                                            // ick - some web sites have url encoded hrefs - apparently that's just fine!
                                            // and one had spaces on the end

                    string nextUrlClean = urlNext.Trim().ToLower();

                    if (nextUrlClean.StartsWith("javascript", StringComparison.InvariantCultureIgnoreCase)) continue;      // ignore javascript on buttons using a tags
                    if (nextUrlClean == "#") continue;

                    Uri urlNextUri = null;

                    try
                    {
                        urlNextUri = new Uri(urlNext, UriKind.RelativeOrAbsolute);       // one bad website crashed us here, so we protect it in try catch
                    }
                    catch (UriFormatException ex)
                    {
                        Console.WriteLine("Malformed href on page " + this.Url + " = " + hrefAttr.Value);
                        this.errorMessages.Add("Malformed href on page " + this.Url + " = " + hrefAttr.Value);
                        continue;
                    }

                    Console.WriteLine(urlNext);

                    // Make it absolute if it's relative
                    if (!urlNextUri.IsAbsoluteUri)
                    {
                        urlNextUri = new Uri(baseUrl, urlNextUri);     // relative to the current page NOT the root!
                    }

                    // Make the web link ...
                    WebLink webLink = WebLink.Factory(urlNextUri);

                    bool destinationHrefIsLocalExact = urlNextUri.Authority == this.uriRoot.Authority;
                    bool sameApartFromWWW = urlNextUri.Authority.Replace("www.", "") == this.uriRoot.Authority.Replace("www.", "");

                    bool destinationHrefIsLocal = destinationHrefIsLocalExact || sameApartFromWWW;

                    if (destinationHrefIsLocalExact != sameApartFromWWW && (webLink is WebUrlLink))
                    {
                        this.seoWarnings.Add("Internal links mix www. and non www. links - be consistent: " + urlNextUri);
                    }

                    if (!String.IsNullOrWhiteSpace(requiredLinkMeta))
                    {
                        if (requiredLinks.ContainsKey(nextUrlClean))
                        {
                            requiredLinks[nextUrlClean]--;                          // count our link
                            //Console.WriteLine("Is in list " + urlNext);
                            webLink.Status = WebLink.StatusCode.PresentAndExpected;
                        }
                        else
                        {
                            //Console.WriteLine("Not in list " + urlNext);
                            // There might be a missing link here ...
                            string to = destinationHrefIsLocal ? urlNextUri.AbsolutePath : urlNextUri.AbsoluteUri;
                            if (from != to && !suggestedLinks.Contains(to))
                            {
                                suggestedLinks.Add(to);
                            }
                            webLink.Status = WebLink.StatusCode.PresentButNotExpected;
                        }
                    }
                    // And now add it
                    this.links.Add(webLink);
                }

                if (requiredLinkMeta != "")
                {
                    // We are on a site that has required links set

                    //if (showSuggestedLinks)
                    //foreach (var suggestion in suggestedLinks)
                    //{
                    //    Console.WriteLine(@"Consider adding [LinkedPage(""" + suggestion + @""")]");
                    //}

                    int count = requiredLinks.Count(x => x.Value > 0);

                    if (count == 0 && requiredLinks.Count > 0)
                    {
                        Console.WriteLine("  +++ all required links found on page");
                    }
                    else
                    {
                        foreach (var missingLink in requiredLinks.Where(x => x.Value > 0))
                        {
                            WebLink missing = WebLink.Factory(new Uri(missingLink.Key, UriKind.RelativeOrAbsolute));
                            missing.Status = WebLink.StatusCode.ExpectedButNotPresent;
                            this.links.Add(missing);
                            this.errorMessages.Add("ERROR: Missing one or more required links " + missingLink.Key);
                            //Console.WriteLine("  **** MISSING LINK FROM " + url + " to " + missingLink);
                            //failed++;
                        }
                    }
                }
            }

            public override string ToString()
            {
                return "Web Page " + this.Url + " ( " + this.Links.Count() + " links)";
            }

        }

    }

    // TODO: Considering using this for serialization instead of just Href strings ***************

    [CollectionDataContract(ItemName = "Link")]
    public class WebLinkList : List<WebLink>
    {
        public WebLinkList() : base() { }
        public WebLinkList(IEnumerable<WebLink> links)
            : base(links)
        {
        }
    }


}