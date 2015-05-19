using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UtilitiesWeb
{
    public static class StringUtility
    {
        /// <summary>
        /// A better ToString for Enumerable objects, limited to 80 characters (mostly for logging)
        /// </summary>
        public static string ToStringList<T>(this IEnumerable<T> collection)
        {
            return collection.ToStringList(80);
        }

        /// <summary>
        /// A better ToString for Enumerable objects, limited to limit characters (mostly for logging)
        /// </summary>
        public static string ToStringList<T>(this IEnumerable<T> collection, int limit)
        {
            return string.Join(", ", collection.Take(100).Select(x => x == null ? "null" : x.ToString())).LimitWithElipsesOnWordBoundary(limit);
        }


        /// <summary>
        /// Joins the first 'count' values from the enumerable set (will go up to 3 over on count to avoid orphans)
        /// </summary>
        public static string ToSummary(this IEnumerable<string> stringsToJoin, int count)
        {
            int showCount = count;
            int actualCount = stringsToJoin.Count();
            string result;

            // Orphan control ...
            if (actualCount < count + 3) showCount = actualCount;
            result = string.Join(", ", stringsToJoin.Take(showCount)) + (actualCount > showCount ? (" and " + (actualCount - showCount) + " others") : "");
            return result;
        }

        /// <summary>
        /// Default summary lists the first five items
        /// </summary>
        public static string ToSummary(this IEnumerable<string> stringsToJoin)
        {
            return stringsToJoin.ToSummary(5);
        }


        /// <summary>
        /// Trims white space from a string down to a single space for each occurrence
        /// </summary>
        /// <remarks>
        /// Good for dumping out HTML innertext
        /// </remarks>
        public static string TrimWhiteSpace(this string str)
        {
            return Regex.Replace(str.Replace("\r", " ").Replace("\n", " "), "\\s+", " ", RegexOptions.Compiled);
        }

        /// <summary>
        /// Substring but OK if shorter
        /// </summary>
        public static string Limit(this string str, int characterCount)
        {
            if (str.Trim().Length <= characterCount) return str.Trim();
            else return str.Substring(0, characterCount).TrimEnd(' ');
        }

        /// <summary>
        /// Substring with elipses but OK if shorter, will take 3 characters off character count if necessary
        /// </summary>
        public static string LimitWithElipses(this string str, int characterCount)
        {
            if (characterCount < 5) return str.Limit(characterCount);       // Can't do much with such a short limit
            if (str.Trim().Length <= characterCount) return str.Trim();
            if (str.Length <= characterCount - 3) return str;
            else return str.Substring(0, characterCount - 3) + "...";
        }

        /// <summary>
        /// Limit with unique code on end
        /// </summary>
        public static string LimitWithUniqueCode(this string str, int characterCount)
        {
            string hash = str.GetHashCode().ToString();
            if (characterCount < hash.Length) return str.Limit(characterCount);       // Can't do much with such a short limit
            if (str.Trim().Length <= characterCount) return str.Trim();
            if (str.Length <= characterCount - hash.Length) return str;
            else return str.Substring(0, characterCount - hash.Length) + hash;
        }



        /// <summary>
        /// Substring with elipses but OK if shorter, will take 3 characters off character count if necessary
        /// tries to land on a space.
        /// </summary>
        public static string LimitWithElipsesOnWordBoundary(this string str, int characterCount)
        {
            if (characterCount < 5) return str.Limit(characterCount);       // Can't do much with such a short limit
            if (str.Trim().Length <= characterCount)
                return str.Trim();
            if (str.Length <= characterCount - 3)
                return str;
            else
            {
                int lastspace = str.Substring(0, characterCount - 3).LastIndexOf(' ');
                if (lastspace > 0 && lastspace > characterCount - 10)
                {
                    return str.Substring(0, lastspace) + "...";
                }
                else
                {
                    // No suitable space was found
                    return str.Substring(0, characterCount - 3) + "...";
                }
            }
        }

        /// <summary>
        /// Remove a string or a list of strings from the end of a string - repeats until no more removals can be made
        /// </summary>
        public static string TrimStringInsensitive(this string str, params string[] stringToRemove)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (string s in stringToRemove)
                {
                    if (str.EndsWith(s, StringComparison.InvariantCultureIgnoreCase))
                    {
                        str = str.Substring(0, str.Length - s.Length).Trim();
                        changed = true;
                    }
                }
            }
            return str;
        }


        /// <summary>
        /// Remove a string or a list of strings (regardless of case) from another string
        /// </summary>
        public static string RemoveInsensitive(this string str, params string[] stringToRemove)
        {
            string result = str;
            // TODO: Fix this to work properly ... this is a hack - doesn't handle mixed cases
            foreach (string s in stringToRemove)
                result = result.Replace(s, "").Replace(s.ToLowerInvariant(), "").Replace(s.ToUpperInvariant(), "");
            return result;
        }

        /// <summary>
        /// Remove a list of characters from a string (case sensitive)
        /// </summary>
        public static string Remove(this string str, params char[] charsToRemove)
        {
            var sb = new StringBuilder(str.Length);
            foreach (char c in str)
            {
                if (!charsToRemove.Contains(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Replace a string (regardless of case)
        /// </summary>
        public static string ReplaceInsensitive(this string str, string stringToRemove, string replaceString)
        {
            if (string.IsNullOrEmpty(stringToRemove)) throw new ArgumentNullException("String to remove must be something");

            int i = 0;
            while (i < str.Length && i >= 0)
            {
                i = str.IndexOf(stringToRemove, i, StringComparison.CurrentCultureIgnoreCase);
                if (i > 0)
                {
                    str = str.Substring(0, i) + replaceString + str.Substring(i + stringToRemove.Length);
                    i = i + 1;          // to prevent repeat execution on the same spot over and over
                }
            }

            return str;
        }

        /// <summary>
        /// Convenience method
        /// </summary>
        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }

        /// <summary>
        /// Convenience method to avoid all those !string.IsNullOrEmpty usages
        /// </summary>
        public static bool IsNotNullOrEmpty(this string str)
        {
            return !string.IsNullOrEmpty(str);
        }

    }
}