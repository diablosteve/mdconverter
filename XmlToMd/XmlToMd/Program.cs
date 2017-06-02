using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using System.ServiceModel.Syndication;

namespace XmlToMd
{
    class Program
    {
        static void Main(string[] args)
        {
            string dirName = string.Empty;
            string dirPath = "SnBlogFolder";
            var url = new Uri("https://blog.sensenet.com/syndication.axd");

            foreach (var arg in args)
            {
                if (arg.StartsWith("http://") || arg.StartsWith("https://"))
                {
                    url = new Uri(arg.ToString());
                }
                if (arg.Contains(":\\") || arg.StartsWith("\\") || arg.StartsWith("\\"))
                {
                    if (Environment.SystemDirectory.Contains(arg.Substring(0, 3)))
                    {
                        dirPath = @arg;
                        
                    }
                    else {
                        Console.WriteLine("Error: " + arg);
                    }
                }
                if (arg.Substring(1,2).ToString() == ("://").ToString() || arg.StartsWith("//") || arg.StartsWith("////"))
                {
                    Console.WriteLine("* One argument not formed well! *");
                }
            }

            
            var client = new HttpClient();
            var resultText = client.GetStringAsync(url);
            
            if (resultText.Result.TrimStart().StartsWith("<!DOCTYPE"))
            {
                string innerPost = resultText.Result.Trim();
                var regMatchStart = Regex.Match(innerPost, "(<div.*class=\"postItem\".*>)");
                var regMatch = Regex.Match(innerPost, "(<div.*class=\"postItem\".*>)[\\s].*(<\\/div>)");
                Console.WriteLine(regMatch.Success);
                Console.WriteLine(regMatch.Value);
            }
            else if (resultText.Result.StartsWith("<?xml") || resultText.Result.StartsWith("<rss"))
            {
                var result = client.GetStreamAsync(url);

                XmlReader reader = XmlReader.Create(result.Result);
                SyndicationFeed feed = SyndicationFeed.Load(reader);
                reader.Close();

                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                foreach (SyndicationItem item in feed.Items)
                {
                    dirName = string.Empty;
                    /* 
                     * Not need to create, the Stream will do it if not exists
                     */
                    foreach (var linkEl in item.Links)
                    {
                        dirName = string.Empty;
                        var linkStringArray = linkEl.Uri.ToString().Split(new string[] { "https://blog.sensenet.com/post/" }, StringSplitOptions.None)[1].Split('.')[0].Split('/');

                        string last = linkStringArray.Last();
                        foreach (string aItem in linkStringArray)
                        {
                            dirName += aItem + (aItem.Equals(last) ? ".md" : "-");
                        }
                        //if (File.Exists(dirPath + "/" + dirName.Trim()))
                        {
                            string lines = blogReader(item);
                            StreamWriter file = new StreamWriter(dirPath + "/" + dirName.Trim());
                            file.WriteLine(lines);
                            file.Close();
                        }
                    }
                    /*foreach (SyndicationElementExtension extension in item.ElementExtensions)
                       {
                           XElement ele = extension.GetObject<XElement>();
                           Console.WriteLine(ele.Name + " - " + ele.Value);
                       }
                       */

                    //Console.WriteLine(" --------------------- ");
                }
            }
        }
        public static string blogReader(SyndicationItem linkEl) {
            string blogString = string.Empty;

            // Set header of MD
            blogString += "---\r\n";
            blogString += "title: \"" + linkEl.Title.Text + "\"\r\n";
            blogString += "author: " + authorReplacer(linkEl.GetPublisher()) + "\r\n";
            blogString += "tags: ";
            foreach (var category in linkEl.Categories)
            {
                blogString += category.Name + ", ";
            }
            blogString += "\r\n---\r\n\r\n";

            var leadEndIndex = linkEl.Summary.Text.IndexOf(".");
            blogString += htmlTagSearch(linkEl.Summary.Text.Substring(0, leadEndIndex));

            blogString += "\r\n\r\n---\r\n\r\n";

            /*blogString += linkEl.Summary.Text.Replace("<p>","").Replace("</p>", "\r\n").Replace("<h2>", "## ").Replace("</h2>", "\r\n").Replace("<br />", "\r\n")
                          .Replace("<ul>", "").Replace("</ul>", "").Replace("<li>", "-   ").Replace("</li>", "")
                          .Replace("&hellip;", "...").Replace("<strong>", "**").Replace("</strong>", "**")
                          .Replace("<blockquote>", "> ").Replace("</blockquote>", "").Replace("<em>", "_").Replace("</em>", "_");*/

            blogString += htmlTagSearch(linkEl.Summary.Text.Replace("\r\n", ""));
            blogString = blogString.Replace("&hellip;", "...").Replace("&amp;nbsp;", " ").Replace("&nbsp;", " ").Replace("&rsquo;", "’").Replace("&ldquo;", "“")
                    .Replace("&rdquo;", "”").Replace("&ndash;", "–");
            blogString = Regex.Replace(blogString, "##[ ]+[ \r\n]", "");

            var splittedBlog = blogString.Split(new string[] { "#pre;" }, StringSplitOptions.None);
            if (splittedBlog.Length > 1) { 
                blogString = codeRewriter(splittedBlog);
            }
            return blogString;
        }

        public static string htmlTagSearch(string blogString) {
            string blogReturn = blogString;
            var regMatch = Regex.Match(blogReturn, "<.*?>");
            while (regMatch.Success)
            {
                blogReturn = htmlTagBuilder(blogReturn, regMatch);
                regMatch = Regex.Match(blogReturn, "<.*?>");
            }
            return blogReturn;
        }

        private static string htmlTagBuilder(string blogString, Match regMatch) {
            var indexStart = regMatch.Index;
            if (indexStart > -1)
            {
                var indexEnd = regMatch.Index + regMatch.Length;
                string subEl = "";
                var indexEndVal = (blogString.Length - indexEnd);
                subEl = blogString.Substring(indexStart, indexEnd - indexStart);
                var offset = 0;
                if (subEl == "<blockquote>" || subEl == "</blockquote>" || subEl == "<ul>") {
                    offset = 1;
                }
                subEl = htmlTagRewriter(subEl);

                blogString = ((indexStart>0)?(blogString.Substring(0, indexStart)):"") + subEl.Replace( "#space;", " ") + blogString.Substring(indexEnd + offset, indexEndVal - offset);
            }

            return blogString;
        }

        protected static string[] prevATag = new string[] { };

        private static string htmlTagRewriter(string htmlTag) {
            string newTag = "";

            htmlTag = propStringHandler(htmlTag);
            var splitter = (htmlTag.Split(' '));
            if (splitter.Length >= 1) {
                switch (splitter[0]) {
                    case "<p":
                    case "<p>":
                    case "</li>":
                    case "<ul":
                    case "<ul>":
                    case "</ul>":
                    case "</blockquote>":
                    case "<tr>":
                    case "</tr>":
                        newTag = "";
                        break;
                    case "</p>":
                    case "</h1>":
                    case "</h2>":
                    case "</h3>":
                    case "</h4>":
                    case "<br/>":
                    case "<br":
                        newTag = "\r\n";
                        break;
                    case "<h1":
                    case "<h1>":
                        newTag = "# ";
                        break;
                    case "<h2":
                    case "<h2>":
                        newTag = "## ";
                        break;
                    case "<h3":
                    case "<h3>":
                        newTag = "### ";
                        break;
                    case "<h4":
                    case "<h4>":
                        newTag = "#### ";
                        break;
                    case "<h5":
                    case "<h5>":
                        newTag = "#### ";
                        break;
                    case "<h6":
                    case "<h6>":
                        newTag = "#### ";
                        break;
                    case "<del":
                    case "<del>":
                    case "</del>":
                        newTag = "#### ";
                        break;
                    case "<li":
                    case "<li>":
                        newTag = "-   ";
                        break;
                    case "<strong":
                    case "</strong":
                    case "<strong>":
                    case "</strong>":
                        newTag = "**";
                        break;
                    case "<em>":
                    case "</em>":
                        newTag = "_";
                        break;
                    case "<blockquote>":
                        newTag = "> ";
                        break;
                    case "<a":
                        newTag = "[";
                        prevATag = splitter;
                        break;
                    case "</a>":
                        newTag = specRewriter(prevATag);
                        prevATag = new string[] { };
                        break;
                    case "<img":
                        newTag = specRewriter(splitter);
                        break;
                    case "<pre":
                    case "<pre>":
                    case "</pre>":
                        newTag = specRewriter(splitter);
                        break;
                    case "<table>":
                        newTag = "#table;";
                        break;
                    case "</table>":
                        newTag = "#tableend;";
                        break;
                    case "<td":
                    case "<td>":
                    case "</td>":
                    case "<th>":
                    case "</th>":
                        newTag = "|";
                        break;
                    case "":
                        break;
                    default:
                        break;
                }
            }

            return newTag;
        }

        private static string propStringHandler(string htmlTag) {
            string newProp = "";
            var tagSplitter = htmlTag.Split(new string[] { "=\"" }, StringSplitOptions.None);
            newProp = tagSplitter[0];
            foreach (string tagEl in tagSplitter) {
                if (tagEl != newProp) {
                    var replacerSplit = tagEl.Split('"');
                    newProp += ("=" + '\u0022') + replacerSplit[0].Replace(" ","#space;");
                    newProp += ('\u0022') + replacerSplit[1];
                }
            }
            return newProp;
        }

        private static string codeRewriter(string[] itemArray) {
            int i = 0;
            string result = "```";
            foreach (var item in itemArray) {
                if (i++ % 2 != 0)
                {
                    result += "\t" + Regex.Replace(item, "[\r\n]", "\r\n\t");
                }
                else {
                    result += item;
                }
            }
            result += "```";
            return result;
        }

        private static string specRewriter(string[] itemArray) {
            if (itemArray[0].StartsWith("<pre") || itemArray[0].StartsWith("</pre>"))
            {
                return "#pre;";
            }
            string stringStart = String.Empty + (itemArray[0].StartsWith("<img")?"![": String.Empty);
            string link = String.Empty;
            string name = String.Empty;
            string innerName = String.Empty;

            if (itemArray.Length > 1) {
                foreach (var item in itemArray) {
                    if (itemArray[0].StartsWith("<img")) { 
                        if (item.StartsWith("alt")) {
                            name = item.Split('=')[1].Replace("\"", "");
                        }
                    }
                    if (item.StartsWith("src") || item.StartsWith("href")) {
                        link = item.Split('=')[1].Replace("\"","");
                    }
                    if (item.StartsWith("title"))
                    {
                        innerName = item.Split('=')[1].Replace("\"", "");
                    }
                }
            }
            
            return stringStart + name + "](" + link + (innerName != "" ? " \"" : "") + innerName + (innerName!=""?"\"":"") + ")";
        }

        private static string authorReplacer(string item) {
            string result = item;

            string startupPath = @"" + new DirectoryInfo(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent.FullName).Parent.FullName + "\\authorReplacer.txt" ;
            string[] lines = File.ReadAllLines(startupPath);
            foreach (var line in lines) {
                var stringArray = line.Split(':');
                if (item == stringArray[0] && stringArray[1].Trim() != "") {
                    result = stringArray[1].Replace(";","").Trim();
                }
            }
            return result;
        }
    }

    public static class SyndicationItemExtensions
    {
        public static string GetCreator(this SyndicationItem item)
        {
            var creator = item.GetElementExtensionValueByOuterName("creator");
            return creator;
        }
        public static string GetPublisher(this SyndicationItem item)
        {
            var publisher = item.GetElementExtensionValueByOuterName("publisher");
            return publisher;
        }

        private static string GetElementExtensionValueByOuterName(this SyndicationItem item, string outerName)
        {
            if (item.ElementExtensions.All(x => x.OuterName != outerName)) return null;
            return item.ElementExtensions.Single(x => x.OuterName == outerName).GetObject<XElement>().Value;
        }
    }

    /* static class XmlToMarkdown
     {
         internal static string ToMarkDown(this XNode e)
         {
             var templates = new Dictionary<string, string>
                 {
                     {"doc", "## {0} ##\n\n{1}\n\n"},
                     {"type", "# {0}\n\n{1}\n\n---\n"},
                     {"field", "##### {0}\n\n{1}\n\n---\n"},
                     {"property", "##### {0}\n\n{1}\n\n---\n"},
                     {"method", "##### {0}\n\n{1}\n\n---\n"},
                     {"event", "##### {0}\n\n{1}\n\n---\n"},
                     {"summary", "{0}\n\n"},
                     {"remarks", "\n\n>{0}\n\n"},
                     {"example", "_C# code_\n\n```c#\n{0}\n```\n\n"},
                     {"seePage", "[[{1}|{0}]]"},
                     {"seeAnchor", "[{1}]({0})"},
                     {"param", "|Name | Description |\n|-----|------|\n|{0}: |{1}|\n" },
                     {"exception", "[[{0}|{0}]]: {1}\n\n" },
                     {"returns", "Returns: {0}\n\n"},
                     {"none", ""}
                 };
             var d = new Func<string, XElement, string[]>((att, node) => new[]
                 {
                     node.Attribute(att).Value,
                     node.Nodes().ToMarkDown()
                 });
             var methods = new Dictionary<string, Func<XElement, IEnumerable<string>>>
                 {
                     {"doc", x=> new[]{
                         x.Element("assembly").Element("name").Value,
                         x.Element("members").Elements("member").ToMarkDown()
                     }},
                     {"type", x=>d("name", x)},
                     {"field", x=> d("name", x)},
                     {"property", x=> d("name", x)},
                     {"method",x=>d("name", x)},
                     {"event", x=>d("name", x)},
                     {"summary", x=> new[]{ x.Nodes().ToMarkDown() }},
                     {"remarks", x => new[]{x.Nodes().ToMarkDown()}},
                     {"example", x => new[]{x.Value.ToCodeBlock()}},
                     {"seePage", x=> d("cref", x) },
                     {"seeAnchor", x=> { var xx = d("cref", x); xx[0] = xx[0].ToLower(); return xx; }},
                     {"param", x => d("name", x) },
                     {"exception", x => d("cref", x) },
                     {"returns", x => new[]{x.Nodes().ToMarkDown()}},
                     {"none", x => new string[0]}
                 };

             string name;
             if (e.NodeType == XmlNodeType.Element)
             {
                 var el = (XElement)e;
                 name = el.Name.LocalName;
                 if (name == "member")
                 {
                     switch (el.Attribute("name").Value[0])
                     {
                         case 'F': name = "field"; break;
                         case 'P': name = "property"; break;
                         case 'T': name = "type"; break;
                         case 'E': name = "event"; break;
                         case 'M': name = "method"; break;
                         default: name = "none"; break;
                     }
                 }
                 if (name == "see")
                 {
                     var anchor = el.Attribute("cref").Value.StartsWith("!:#");
                     name = anchor ? "seeAnchor" : "seePage";
                 }
                 var vals = methods[name](el).ToArray();
                 string str = "";
                 switch (vals.Length)
                 {
                     case 1: str = string.Format(templates[name], vals[0]); break;
                     case 2: str = string.Format(templates[name], vals[0], vals[1]); break;
                     case 3: str = string.Format(templates[name], vals[0], vals[1], vals[2]); break;
                     case 4: str = string.Format(templates[name], vals[0], vals[1], vals[2], vals[3]); break;
                 }

                 return str;
             }

             if (e.NodeType == XmlNodeType.Text)
                 return Regex.Replace(((XText)e).Value.Replace('\n', ' '), @"\s+", " ");

             return "";
         }

         internal static string ToMarkDown(this IEnumerable<XNode> es)
         {
             return es.Aggregate("", (current, x) => current + x.ToMarkDown());
         }

         static string ToCodeBlock(this string s)
         {
             var lines = s.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
             var blank = lines[0].TakeWhile(x => x == ' ').Count() - 4;
             return string.Join("\n", lines.Select(x => new string(x.SkipWhile((y, i) => i < blank).ToArray())));
         }
     }*/
}