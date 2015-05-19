using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;

namespace UtilitiesWeb
{
    static class PageCleaner
    {
        public static string Process(HtmlDocument doc)
        {
            var sb = new StringBuilder(2048);

            // Remove script
            foreach (var script in doc.DocumentNode.Descendants("script").ToArray())
                script.Remove();
            foreach (var style in doc.DocumentNode.Descendants("style").ToArray())
                style.Remove();

            var body = doc.DocumentNode.SelectSingleNode("/html/body");

            if (body != null)
            {
                sb.Append(ProcessOneDiv(body, 0));
            }

            return sb.ToString();
        }

        private static string CleanString(string input)
        {
            string output = input.Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("\t", " ").Replace("  ", " ")
                .Replace("\r", " ").Replace("  ", " ")
                .Replace("\n", " ").Replace("  ", " ")
                + " ";

            // Must be a better way!!
            foreach (var ch in Enumerable.Range(128, 254))
            {
                output = output.Replace("&" + ch + ";", new string((char)ch, 1));
            }
            return output;

        }

        private static void logIt(string text, int level)
        {
            //log.Debug("".PadRight(level) + text);
        }

        private static string ProcessOneDiv(HtmlNode node, int level)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                string result = CleanString(node.InnerText).TrimWhiteSpace();
                if (string.IsNullOrWhiteSpace(result)) return "";
                logIt(result, level);

                if (result.Contains("Twitter"))
                {
                    //log.Debug("Ouch!");
                }

                return result;
            }
            else if (node.NodeType == HtmlNodeType.Element)
            {
                // Is this a node we should be ignoring?
                // We reject any div that's got attributes in the disallowed list (css class or id)
                if (node.Name == "div" || node.Name == "ul" || node.Name == "li" || node.Name == "p")       // container nodes we care about
                {
                    var attributesToCheck = node.Attributes.Where(att => att.Name == "class" || att.Name == "id" || att.Name == "name");
                    var disallowed = new string[] 
                    { "right", 
                        "header", "footer", "nav", "navigation", "breadcrumb", "toolbar",
                        "twitter", "tweet", 
                        "sidebar", "col2", "area-4",
                        // ZD NET
                        "ad-text-link","ads-vtllist", "ads_vtlink",
                        // MSFT PRESS PASS
                        "RelatedEntries",

                        // TECHFLASH
                        "media-right", "blog_rtlisting", "taggroup", "homelinks", "teamwrap",

                        "sponsorPost", "sponsorPostWrap", "sponsorPostEntryWrap",
                        "sponsorPostTitle",
                        "post-tags", "hed-1", "hotspot", "related-content", "cmnt-user", "ad-marquee", "ads_vtlLink", "reg-is-logged-out" , "ad-blog2biz", "site-search", "cbs"
                    };

                    if (disallowed.Any(disallow => attributesToCheck.Any(att => att.Value.ToLower().Contains(disallow.ToLower()))))
                    {
                        logIt("Skipping " + node.Name + " attr=" + attributesToCheck.Select(att => att.Name + "=" + att.Value).ToStringList() + node.InnerText.TrimWhiteSpace().LimitWithElipses(60), level);
                        //Thread.Sleep(2000);
                        return "";
                    }
                    else
                    {
                        logIt("Accepted " + node.Name + " attr=" + attributesToCheck.Select(att => att.Name + "=" + att.Value).ToStringList(), level);
                        //Thread.Sleep(2000);
                    }
                }
                // process an inner item
                StringBuilder sb = new StringBuilder();
                foreach (var item in node.ChildNodes)
                {
                    if (item.Name == "br") sb.AppendLine("");
                    else sb.Append(ProcessOneDiv(item, level + 1));
                }

                if (node.Name == "div" || node.Name == "p") sb.Append(" ");

                return sb.ToString();
            }
            else if (node.NodeType == HtmlNodeType.Comment)
            {
                // ignore it completely
                return "";
            }
            else
            {
                logIt("Skipped " + node.NodeType + " " //+ node.InnerHtml.LimitWithElipses(60)
                , level);
                return "";
            }
        }


    }
}
