using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using UtilitiesWeb;
using System.Text.RegularExpressions;

namespace LinkChecker.WebSpider
{
    /// <summary>
    /// A web site contains web pages
    /// </summary>
    [DataContract]
    [KnownType(typeof(IEnumerable<WebPage>))]
    [KnownType(typeof(WebPage))]
    [KnownType(typeof(WebPage.OtherContent))]
    //[KnownType(typeof(WebPage.Error))]
    //[KnownType(typeof(WebPage.ExceptionError))]
    //[KnownType(typeof(WebPage.External))]
    //[KnownType(typeof(WebPage.Internal))]
    //[KnownType(typeof(WebPage.LoadError))]
    //[KnownType(typeof(WebPage.OtherContent))]
    //[KnownType(typeof(WebPage.Timeout))]
    public class WebSite : IExtensibleDataObject, IStructureComparable<WebSite>
    {
        [DataMember]
        public string DomainAndPath { get; private set; }

        [IgnoreDataMember]
        public Uri Uri { get { return new Uri("http://" + this.DomainAndPath); } }

        [DataMember]
        private WebPageList webPages {get; set;}

        /// <summary>
        /// ICK: If you don't enumerate all of these then the webpages list will not be correct!
        /// If you enumerate it more than once you've caused a refetch!
        /// Need to add caching to Fetch to fix this ...
        /// </summary>
        [IgnoreDataMember]
        public IEnumerable<WebPage> WebPages
        {
            get 
            {
                // Slightly more complex than we'd like since we want lazy evaluation
                if (this.webPages == null)
                {
                    this.webPages = new WebPageList();
                    foreach (var webPage in this.GetAllPages())
                    {
                        //// handle exclusions
                        //if (this.ExcludedPaths.Any(p => p.IsMatch(webPage.Url)))
                        //    continue;
                        this.webPages.Add(webPage);
                        yield return webPage;
                    }
                }
                else
                    foreach (var webPage in webPages)
                    {
                        //if (this.ExcludedPaths.Any(p => webPage.Url.PathAndQuery.StartsWith(p)))
                        //    continue;
                        yield return webPage;
                    }
            }
        }

        [IgnoreDataMember]
        private int delayBetweenPagesInMilliseconds { get; set; }

        /// <summary>
        /// Construct a new instance of a WebSite (or partial WebSite)
        /// </summary>
        /// <param name="domain"></param>
        public WebSite(string domainAndPath, int delayBetweenPagesInMilliseconds)
        {
            this.DomainAndPath = domainAndPath;
            this.delayBetweenPagesInMilliseconds = delayBetweenPagesInMilliseconds;
            this.webPages = null;
        }

        public IEnumerable<WebPage> GetAllPages()
        {
            return Spider.GetAllPagesUnder(this.Uri, this.delayBetweenPagesInMilliseconds, this.ExcludedPaths);
        }

        /// <summary>
        /// Get all the warnings and errors for this site and the pages under it
        /// </summary>
        public IEnumerable<string> WarningsAndErrors()
        {
            // Find all the titles that were used more than once
            var gpByTitle = this.webPages.OfType<WebPage.Internal>().GroupBy(wp => wp.Title);
            var duplicates = gpByTitle.Where(gp => gp.Count() > 1);
            foreach (var s in duplicates)
            {
                yield return "Duplicate title '" + s.Key + "' on " + string.Join(", ", s.Select(wp => wp.Url));
            }

            // Other kinds of errors
            //foreach (var wp in this.webPages.Where(wp => wp.)
            //{

            //}
        }

        public ExtensionDataObject ExtensionData {get; set;}

        [IgnoreDataMember]
        private IDictionary<string, int> titles = new Dictionary<string, int>();

        [IgnoreDataMember]
        public IDictionary<string, int> Titles
        {
            get { return this.titles; }
            set { this.titles = value; }
        }

        [IgnoreDataMember]
        private IList<string> errors = new List<string>();

        [IgnoreDataMember]
        public IEnumerable<string> Errors
        {
            get { return this.errors; }
        }

        /// <summary>
        /// Add an error at the Site Level
        /// </summary>
        /// <param name="value"></param>
        public void AddError (string value)
        {
            this.errors.Add(value);
        }


        /// <summary>
        /// Compare this web site with another
        /// </summary>
        public IEnumerable<string> StructureCompare(int indent, WebSite objectB)
        {
            var heading = new[] { "".PadLeft(indent) + "WEBSITE " + this.DomainAndPath };
            var comparisonResult = StructureComparison.Compare<WebSite>(indent, this, objectB, ws => ws.DomainAndPath);
            // Now handle the pages
            var pageResult = StructureComparison.CompareList<WebPage>(indent + 3, this.WebPages, objectB.WebPages, new PageEqualityComparer());

            var combined = comparisonResult.Concat(pageResult);
            if (combined.Count() > 0)
                foreach (var line in heading.Concat(combined))
                    yield return line;
            else
                yield break; // return new[] { "".PadLeft(indent) + "WEBSITE " + this.Domain + " MATCHES PREVIOUS VERSION" };
        }

        /// <summary>
        /// Any paths we want to exclude e.g. some directory of people, login, ... use | or , separator
        /// </summary>
        [DataMember]
        private string excludedPathString { get; set; }

        private Regex[] excludedPaths;

        [IgnoreDataMember]
        private Regex[] ExcludedPaths
        {
            get
            {
                if (this.excludedPaths == null)
                {
                    this.excludedPaths = (this.excludedPathString ?? "")
                                                .Split(',','|')
                                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                                .Select(x => new Regex(x, RegexOptions.Compiled))
                                                .ToArray();

                }
                return this.excludedPaths;
            }
        }

        /// <summary>
        /// Fluent exception adding
        /// </summary>
        /// <param name="excludedPaths"></param>
        /// <returns></returns>
        public WebSite Except(string excludedPathRegularExpressions)
        {
            this.excludedPathString = excludedPathRegularExpressions;
            return this;
        }
    }


    [CollectionDataContract(ItemName = "Page")]
    public class WebPageList : List<WebPage>
    {
        public WebPageList() : base() { }
        public WebPageList(IEnumerable<WebPage> pages)
            : base(pages)
        {
        }
    }
}