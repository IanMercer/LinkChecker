using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HtmlAgilityPack;
using System.Net;
using LinkChecker.WebSpider;
using System.IO;
using System.Xml;

namespace UtilitiesWeb
{
    public static class LoadXMLFaultTolerant
    {

        public static XDocument Load(string url)
        {
            HttpWebResponse webResponse = Spider.FetchWebPageWithRetries(new Uri(url), 60000);      // 1 minute!

            using (var resultStream = webResponse.GetResponseStream())
            {
                HtmlDocument doc = new HtmlDocument();
                doc.Load(resultStream); // The HtmlAgilityPack
                doc.OptionOutputAsXml = true;

                using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
                {
                    using (XmlTextWriter xtw = new XmlTextWriter(stream, null))
                    {
                        doc.Save(xtw);
                        stream.Position = 0;
                        XDocument xdoc = XDocument.Load(stream);
                        return xdoc;
                    }
                }
            }
        }

    }
}
