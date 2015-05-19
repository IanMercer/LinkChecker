using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinkChecker
{
    public static class HtmlNodeExtensions
    {
        /// <summary>
        /// Gets or Sets the text between the start and end tags of the object.
        /// </summary>
        public static string InnerTextButJustTheTextBits(this HtmlNode n, Func<HtmlNode, bool> includeFilter, Func<HtmlNode, bool> excludeFilter, bool active)
        {
            if (n.NodeType == HtmlNodeType.Text)
            {
                if (active)
                    return CleanHtml(((HtmlTextNode)n).Text.TrimEnd('\r', '\n', '\t'));
                else
                    return "";
            }

            if (n.NodeType == HtmlNodeType.Comment)
            {
                return "";
            }

            if (n.Name.Equals("meta", StringComparison.InvariantCultureIgnoreCase)) return "";         // we do care about the title tag

            if (n.Name.Equals("style", StringComparison.InvariantCultureIgnoreCase)) return "";

            if (n.Name.Equals("script", StringComparison.InvariantCultureIgnoreCase)) return "";

            // Construct a path that we can query to include or exclude elements
            // e.g. html.

            //string nodeName = this.Name ? "";           // e.g. div, p, ...
            //string nodeId = this.Id ?? "";
            //string cssClasses = string.Join(",",this.GetAttributeValue("class", "").Split(' '));

            if (!active)
                if (includeFilter(n)) active = true;

            if (active)
                if (excludeFilter(n)) active = false;

            if (!n.HasChildNodes)
            {
                return string.Empty;
            }

            string s = string.Empty;
            foreach (var node in n.ChildNodes)
            {
                s += node.InnerTextButJustTheTextBits(includeFilter, excludeFilter, active);
            }

            // Remove carriage returns from inside certain tags

            switch (n.Name.ToLowerInvariant())
            {
                case "a":
                case "span":
                    s = s.Replace("\r", " ").Replace("\n", " ");            // rip carriage returns out of spans, p's, a's
                    break;

                // table rows, divs and Ps get a gap between them, spans don't
                case "div":
                case "tr":
                case "p":
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                    s = s + "\n";
                    break;

                // table elements get tab separated
                case "td":
                    s = s + "\t";
                    break;

                // Lists get broken up over lines
                case "li":
                case "br":
                case "dd":
                    s = s + "\n";           // TBD: li elements could go either way
                    break;
            }

            return s;
        }

        /// <summary>
        /// Standard HTML character escape sequences
        /// </summary>
        private static string CleanHtml(string html)
        {
            return html
                        .Replace("“", @"""")
                        .Replace("”", @"""")
                        .Replace("’", @"'")
                        .Replace("&nbsp;", " ")
                        .Replace("&amp;", "&")
                        .Replace("&quot;", @"""")
                        .Replace("&lsquo;", "'")
                        .Replace("&rsquo;", "'")
                        .Replace("&lquo;", "'")
                        .Replace("&rquo;", "'")
                        .Replace("&lt;", "<")
                        .Replace("&gt;", ">")
                        .Replace("&mdash;", "-")
                        .Replace("&ndash;", "-")
                        .Replace("&ldquo;;", @"""")
                        .Replace("&rdquo;", @"""")
                        .Replace("&euro;", "€")
                        .Replace("&lsaquo;", "<")
                        .Replace("&rsaquo;", ">");
        }
    }
}
