using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using System.Net;
using System.Diagnostics.Contracts;
using System.Threading;
using Nini.Config;
using System.Runtime.Serialization;
using System.IO;
using System.Xml;
using System.Diagnostics;
using LinkChecker.WebSpider;
using System.Reflection;
using System.Text.RegularExpressions;
using Fizzler;
using Fizzler.Systems.HtmlAgilityPack;

namespace LinkChecker
{
    class Program
    {
        //static string cssSelector = "table tr td.itm:parent";
        //static string includeSelector = "head title,body";
        //static string excludeSelector = ".pageHeader,#header,.header,#footer,.footer,#sidebar,.sidebar,#feedbackTab,#feedback,.feedback,#feedbackdialog," +
        //                                "#smalltype,.share-links-footer,block-ec_blogs";

        //static string cssSelector = "-head>+title," + 
        //                            "+body>-header|*footer|#sidebar>+heading,+text," + 
        //                                    "-#feedbackdialog," +
        //                                    "-#smalltype" +
        //                                    ")";

        // Or, construct a path on each element and just use a regex?  NAH ...fails for ... td.abc:parent

        // html/head/title
        // html/body(.a .b .c)/div(.content)/

        [Flags]
        enum ResultCode
        {
            PAGE_ERROR = 1,
            LINK_ERROR = 2,
            SEO_ERROR = 4,
            CHANGE_DETECTED = 8,
            PROGRAM_ERROR = 128
        }


        static int Main(string[] args)
        {
            ResultCode resultCode = 0;

            // use a configuration file to list all the sites you want to check
            // provide SMTP server information too

            var version = Assembly.GetEntryAssembly().GetName().Version;
            string startup = "LINKCHECKER " + version + " (c) Ian Mercer 2010 http://blog.abodit.com";

            Console.WriteLine(startup);
            Console.WriteLine("".PadLeft(startup.Length, '-'));
            Console.WriteLine();

            if (!File.Exists("LinkChecker.ini"))
            {
                Console.WriteLine("Please reinstall, you need both the .EXE and the .INI file");
                Console.WriteLine("See http://blog.abodit.com");
            }

            IConfigSource mainSource = new IniConfigSource("LinkChecker.ini");

            ArgvConfigSource argSource = new ArgvConfigSource(args);
            //argSource.AddSwitch("SMTP", "Server", "smtp");
            //argSource.AddSwitch("SMTP", "Port", "port");
            //argSource.AddSwitch("SMTP", "Username", "user");
            //argSource.AddSwitch("SMTP", "Password", "password");

            argSource.AddSwitch("Settings", "Domain", "d");                // a single domain

            argSource.AddSwitch("Settings", "Delay", "delay");             //

            argSource.AddSwitch("Settings", "IncludeSelectors", "is");     // Exclude pages option
            argSource.AddSwitch("Settings", "ExcludeSelectors", "xs");     // Exclude pages option

            argSource.AddSwitch("Settings", "ExcludePages", "xp");         // Exclude pages option
            argSource.AddSwitch("Settings", "All", "all");                 //
            argSource.AddSwitch("Settings", "Pages", "pages");             //
            argSource.AddSwitch("Settings", "Seo", "seo");                 //
            argSource.AddSwitch("Settings", "Changes", "changes");         // 
            argSource.AddSwitch("Settings", "Limit", "limit");             // 
            argSource.AddSwitch("Settings", "Dump", "dump");               //  dump all text found
            argSource.AddSwitch("Settings", "Suggestions", "suggestions");

            mainSource.Merge(argSource);

            //var SMTPConfig = mainSource.Configs["SMTP"];
            var SettingsConfig = mainSource.Configs["Settings"];

            //string SMTPServer = SMTPConfig.GetString("Server", "");
            //int SMTPPort = SMTPConfig.GetInt("Port", 25);
            //string SMTPUsername = SMTPConfig.GetString("Username", "");
            //string SMTPPassword = SMTPConfig.GetString("Password", "");

            //SMTPConfig.Set("Server", SMTPServer);
            //SMTPConfig.Set("Port", SMTPPort);
            //SMTPConfig.Set("Username", SMTPUsername);
            //SMTPConfig.Set("Password", SMTPPassword);

            int delayBetween = SettingsConfig.GetInt("Delay", 10);
            SettingsConfig.Set("Delay", delayBetween);

            string excludedPathSetting = SettingsConfig.GetString("ExcludePages", ""); //"");       // common duplicate pages / comments
            string excludedPaths = excludedPathSetting;
            SettingsConfig.Set("ExcludePages", excludedPaths);

            string includeSelector = SettingsConfig.GetString("IncludeSelectors", "head title,body");
            string excludeSelector = SettingsConfig.GetString("ExcludeSelectors", ".pageHeader,#header,.header,#footer,.footer,#sidebar,.sidebar," +                                                                        "#feedbackTab,#feedback,.feedback,#feedbackdialog," +
                                            "#smalltype,.share-links-footer,block-ec_blogs");
            SettingsConfig.Set("IncludeSelector", includeSelector);
            SettingsConfig.Set("ExcludeSelector", excludeSelector);

            string listPages = SettingsConfig.GetString("Pages", "").ToLower();
            if (listPages != "none" && listPages != "list") listPages = "error";
            SettingsConfig.Set("Pages", listPages);

            string seo = SettingsConfig.GetString("Seo", "").ToLower();
            if (seo != "none" && seo != "list") seo = "error";
            SettingsConfig.Set("Seo", seo);

            string changes = SettingsConfig.GetString("Changes", "").ToLower();
            if (changes != "none" && changes != "error") changes = "list";
            SettingsConfig.Set("Changes", changes);

            int limit = SettingsConfig.GetInt("Limit", 3000);               // 3000 pages limit by default

            string dumpFilePath = SettingsConfig.GetString("Dump");
            if (dumpFilePath != null)
                SettingsConfig.Set("Dump", dumpFilePath);

            bool showSuggestedLinks = !string.IsNullOrWhiteSpace(SettingsConfig.Get("Suggestions", ""));
            SettingsConfig.Set("Suggestions", showSuggestedLinks);

            string domainSingle = SettingsConfig.GetString("Domain", "");

            // Save any changes back to the config file
            // Don't do this because then they affect everyone after that ...
            //mainSource.Save();

            if (string.IsNullOrWhiteSpace(domainSingle))
            {
                Console.WriteLine("  Usage:    linkchecker -d example.com");
                Console.WriteLine("  Parameters:");
                Console.WriteLine("     -d:example.com               The domain or starting Url you wish to check");
                Console.WriteLine("                                      all pages at the same level or below will be scanned");
                Console.WriteLine("       -xp:path1,path2            Exclude any paths that include path1, path2, ...");
                Console.WriteLine("                                      e.g. -xp:comment,/recommend,/email,/print");
                Console.WriteLine();
                Console.WriteLine("     -all                         Complete dump including pages, changes and errors (default)");
                Console.WriteLine("       -pages:none|list|error       Detailed information about page and links (default = error)");
                Console.WriteLine("       -seo:none|list|error         Information about any SEO issues (default = error)");
                Console.WriteLine("       -changes:none|list|error     Changes to pages or links since last run (default=list)");
                Console.WriteLine();
                Console.WriteLine("     -delay 10                    Delay 10 seconds between pages (less load on server being tested)");
                Console.WriteLine("     -limit 3000                  Limit how many pages (3000 default)");
                Console.WriteLine();
                Console.WriteLine("     -dump filename               Dump all content from pages to a file");
                Console.WriteLine("       -xs:domSelector2,...         Exclude DOM elements that match jQuery style selectors");
                Console.WriteLine("       -is:domSelector1,...         Include DOM elements that match jQuery style selectors");
                Console.WriteLine("                                       e.g. -is:head title,body -xs:footer,#header,#sidebar,.comment");
                Console.WriteLine();
                Console.WriteLine("                                    -xs and -is are applied throughout the DOM tree to select elements");
                Console.WriteLine();
                Console.WriteLine("     You can put <meta linkedpages=\"\\url,\\url2,...\"> on any page to check that those links");
                Console.WriteLine("     are present on the page somewhere.  In effect a 'link contract' that the page must meet.");
                Console.WriteLine("     This allows you to check that none of the key links on your site are broken.");
                Console.WriteLine();
                Console.WriteLine("     -suggestions                 Lists all the links that are on a page but not in a linkedpages meta tag");
                Console.WriteLine("                                    e.g. <meta linkedpages=\"\\login,\\logout,\\home\">");
                Console.WriteLine();

                Console.WriteLine();

                Console.WriteLine("     RESULT CODE is non-zero if there are any errors and you specified error on any element above");

                //Console.WriteLine("     -smtp    An SMTP server to email the results to");
                //Console.WriteLine("     -port    SMTP Server port (25)");
                //Console.WriteLine("     -user    SMTP Server user name");    
                //Console.WriteLine("     -password SMTP Server password");
                Console.WriteLine();
                Console.WriteLine("  Settings may also be placed in a file LinkChecker.ini");
                Console.WriteLine();
                Console.WriteLine("  An XML dump of your web site will be placed in a subdirectory and used for subsequent runs as a comparison");
                Console.WriteLine("  to alert you when new pages appear or links get broken between pages on your site.");
                Console.WriteLine();
                return 1;
            }

            int failed = 0;

            Console.WriteLine("Domain = " + (domainSingle ?? "null"));

            // Display the config settings to the user
            //if (!string.IsNullOrWhiteSpace(SMTPServer))
            //    Console.WriteLine("SMTP Server " + SMTPServer);

            string cleanDomain = domainSingle.Replace("http://", "");
            Uri urlRoot = new Uri("http://" + cleanDomain);

            string directoryPath = System.IO.Path.Combine(Environment.CurrentDirectory, SafeFileName(cleanDomain));
            if (!System.IO.Directory.Exists(directoryPath))
                System.IO.Directory.CreateDirectory(directoryPath);

            string fileName = DateTime.Now.ToString("yyyy-MM-dd-HH;mm") + ".xml";
            string filePath = Path.Combine(directoryPath, fileName);

            // A web site or part of a web site, e.g. www.microsoft.com/presspass
            WebSite webSite = new WebSite(cleanDomain, delayBetween*1000)
                .Except(excludedPaths);

            // Collect error messages
            List<string> errorMessages = new List<string>();

            // Collect SEO warnings
            List<string> seoWarnings = new List<string>();

            // Look for duplicate titles
            Dictionary<string, int> titleCounts = new Dictionary<string, int>();

            Console.WriteLine(" _______________________________________________________________________________________ ");

            int countdown = limit;

            foreach (var webPage in webSite.WebPages)
            {
                // Count only errors and good pages ...
                if ((!(webPage is WebPage.External) && !(webPage is WebPage.OtherContent)) && countdown-- < 0)
                {
                    Console.WriteLine("Limit reached, use something more than  -limit " + limit + "");
                    break;
                }

                Uri url = webPage.Url;

                if (webPage is WebPage.Internal)
                {
                    WebPage.Internal webPageInternal = webPage as WebPage.Internal;
                    if (listPages != "none")
                    {
                        Console.WriteLine(url);
                        Console.WriteLine("  title       = " + webPageInternal.Title);
                        Console.WriteLine("  description = " + webPageInternal.MetaDescription);

                        foreach (var message in webPageInternal.ErrorMessages)
                        {
                            string err = "  ERROR ON PAGE = " + message;
                            Console.WriteLine(err);
                        }
                        errorMessages.AddRange(webPageInternal.ErrorMessages);

                        foreach (var message in webPageInternal.SeoWarnings)
                        {
                            string err = "  WARNING = " + message;
                            Console.WriteLine(err);
                        }
                    }

                    errorMessages.AddRange(webPageInternal.ErrorMessages);
                    seoWarnings.AddRange(webPageInternal.SeoWarnings);

                    // Check for duplicates too
                    string title = webPageInternal.Title ?? "";

                    if (titleCounts.ContainsKey(title))
                        titleCounts[title]++;
                    else
                        titleCounts.Add(title, 1);

                    if (!string.IsNullOrWhiteSpace(dumpFilePath))
                    {
                        var documentNode = webPageInternal.HtmlDocument.DocumentNode;

                        //<td class="itm" style="text-align:left" nowrap><a href="/ESIG">ESIG</a></td>                        //<td class="dsc">Engineering Science Interactive Graphics</td>
                        var includeSelected =  documentNode.QuerySelectorAll(includeSelector).ToList();
                        var excludeSelected = documentNode.QuerySelectorAll(excludeSelector).ToList();

                        Func<HtmlNode, bool?> includeFilter = node => includeSelected.Contains(node);
                        Func<HtmlNode, bool?> excludeFilter = node => excludeSelected.Contains(node);

                        using (FileStream file = File.Open(dumpFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                        using (StreamWriter sw = new StreamWriter(file))
                        {
                            sw.WriteLine(url);
                            sw.WriteLine("  title       = " + webPageInternal.Title);
                            sw.WriteLine("  description = " + webPageInternal.MetaDescription);

                            var selected = documentNode.InnerTextButJustTheTextBits(includeFilter, excludeFilter, false);
                            var lines = selected.Split('\n', '\r')
                                                .Select(s => s.Replace('\t', ' '))
                                                .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length > 1);           // ignore single character lines
                            foreach (var line in lines)
                                sw.WriteLine(line.Trim());

                            //var lines = webPageInternal.HtmlDocument.DocumentNode.InnerTextButJustTheTextBits(filterPredicate).Split('\n', '\r')
                            //                            .Select(s => s.Replace('\t', ' '))
                            //                            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length>1);           // ignore single character lines
                            //foreach (var line in lines)
                            //    sw.WriteLine(line.Trim());

                            sw.WriteLine(" _______________________________________________________________________________________ ");
                        }
                    }

                }
                else if (webPage is WebPage.External || webPage is WebPage.OtherContent)
                {
                    if (listPages != "none")
                    {
                        Console.WriteLine(webPage.ToString());
                    }
                }
                else if (webPage is WebPage.Error)
                {
                    Console.WriteLine(webPage.ToString());
                    failed++;
                    errorMessages.Add(webPage.ToString());
                }
                else
                {
                    throw new ApplicationException("Unexpected type " + webPage);
                }


                //if (showSuggestedLinks)
                //foreach (var suggestion in webPageInternal.SuggestedLinks)
                //{
                //    Console.WriteLine(@"Consider adding [LinkedPage(""" + suggestion + @""")]");
                //}

                //int count = requiredLinks.Count(x => x.Value > 0);

                //if (count == 0 && requiredLinks.Count > 0)
                //{
                //    Console.WriteLine("  +++ all required links found on page");
                //}
                //else
                //{
                //    foreach (var missingLink in requiredLinks.Where(x => x.Value > 0))
                //    {
                //        Console.WriteLine("  **** MISSING LINK FROM " + url + " to " + missingLink);
                //        failed++;
                //    }
                //}

                if (listPages != "none")
                    Console.WriteLine("|---------------------------------------------------------------------------------------|");

            }

            // Save the results ...

            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.Indent = true;
            //xmlWriterSettings.CheckCharacters = false;
            xmlWriterSettings.NewLineHandling = NewLineHandling.Entitize;

            // Serialize to disk
            DataContractSerializer serializer = new DataContractSerializer(typeof(WebSite), new[]
                    {typeof(WebPage)
                    , typeof(WebPage.Error)
                    , typeof(WebPage.ExceptionError)
                    , typeof(WebPage.External)
                    , typeof(WebPage.Internal)
                    , typeof(WebPage.LoadError)
                    , typeof(WebPage.OtherContent)
                    , typeof(WebPage.Timeout)
                    , typeof(System.UriFormatException)             // ick!
                    ,typeof(WebLink)

                    });

            // Now compare that against the previous version
            var lastFile = Directory.EnumerateFiles(directoryPath).Select(f => new FileInfo(f)).OrderByDescending(d => d.CreationTimeUtc).FirstOrDefault();

            WebSite previous = null;
            // Read the previous last file in
            if (lastFile != null)
            {
                try
                {
                    using (XmlReader xmlReader = XmlReader.Create(new FileStream(lastFile.FullName, FileMode.Open, FileAccess.Read)))
                    {
                        previous = (WebSite)serializer.ReadObject(xmlReader);
                    }
                }
                catch (System.Runtime.Serialization.SerializationException ex)  // the one we expect
                {
                    previous = null;
                    Console.WriteLine(ex);
                    resultCode = resultCode | ResultCode.PROGRAM_ERROR;
                }
                catch (Exception ex)        // and for now, catch all others ...
                {
                    previous = null;
                    Console.WriteLine(ex);
                    resultCode = resultCode | ResultCode.PROGRAM_ERROR;
                }
            }

            if (changes != "none")
            {
                Console.WriteLine(@"|------------------------------------COMPARISON-----------------------------------------|");

                bool writeXML = true; // unless ...

                try
                {
                    if (previous != null)
                    {
                        var changeLines = previous.StructureCompare(0, webSite).ToArray();

                        if (changeLines.Any())
                        {
                            resultCode = resultCode | ResultCode.CHANGE_DETECTED;
                            foreach (var s in changeLines)
                            {
                                Console.WriteLine(s);
                            }
                        }
                        else
                        {
                            writeXML = false;
                            Console.WriteLine("NO CHANGES DETECTED SINCE " + lastFile.CreationTime.ToShortDateString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Please report: Error comparing " + ex.ToString());
                }

                if (writeXML)
                {
                    try
                    {
                        // And write the updates to a new file
                        using (XmlWriter xmlWriter = XmlWriter.Create(new FileStream(filePath, FileMode.CreateNew), xmlWriterSettings))
                        {
                            serializer.WriteObject(xmlWriter, webSite);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to write file " + ex.Message);
                    }
                }
            }

            if (errorMessages.Count > 0)
            {
                resultCode = resultCode | ResultCode.PAGE_ERROR;

                Console.WriteLine(@"|-------------------------------------ERRORS " + errorMessages.Count() + "-----------------------------------------|");
                foreach (var err in errorMessages)
                {
                    Console.WriteLine(err);
                }
            }
            else
            {
                Console.WriteLine("No errors");
            }

            // Find duplicate page titles
            var duplicatePageTitles = titleCounts.Where(x => x.Value > 1).ToList();
            foreach (var duplicatePageTitle in duplicatePageTitles)
            {
                seoWarnings.Add("Duplicate title '" + duplicatePageTitle.Key + "' on " + duplicatePageTitle.Value + " pages");
            }

            if (seo != "none")
            {
                if (seoWarnings.Any())
                {
                    if (seo == "error")
                        resultCode = resultCode | ResultCode.SEO_ERROR;

                    Console.WriteLine(@"|------------------------------------SEO WARNINGS (" + seoWarnings.Count() + ")------------------------------------|");
                    foreach (var err in seoWarnings)
                    {
                        Console.WriteLine(err);
                    }
                }
            }

            Console.WriteLine(@"\_______________________________________________________________________________________/");
            Console.WriteLine();

            //Console.ReadKey();

            // Now decide whether to fail or not ...

            if (resultCode > 0)
            {
                Console.WriteLine("**** FAILED WITH ERROR CODE ****");
                if ((resultCode & ResultCode.CHANGE_DETECTED) != 0) Console.WriteLine("  Changes detected");
                if ((resultCode & ResultCode.LINK_ERROR) != 0) Console.WriteLine("  Link error");
                if ((resultCode & ResultCode.PAGE_ERROR) != 0) Console.WriteLine("  Page error");
                if ((resultCode & ResultCode.PROGRAM_ERROR) != 0) Console.WriteLine("  Program error");
                if ((resultCode & ResultCode.SEO_ERROR) != 0) Console.WriteLine("  SEO error");
            }


            if (Environment.MachineName == "XPS")
                Thread.Sleep(20000);

            return (int)resultCode;
        }


        public static string SafeFileName(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

    }
}