using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace UtilitiesWeb
{

    public interface IStructureComparable<T>
    {
        IEnumerable<string> StructureCompare(int indent, T objectB);
    }


    /// <summary>
    /// Compares two structures of type T to tell you what has changed between them
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class StructureComparison
    {

        public static IEnumerable<string> Compare<T> (int indent, T objectA, T objectB, params Expression<Func<T,string>>[] Selectors)
        {
            foreach (Expression<Func<T, string>> expr in Selectors)
            {
                var me = expr.Body as MemberExpression;
                string field = (me != null) ? me.Member.Name + ":" : "";

                string sA = expr.Compile().Invoke(objectA);
                string sB = expr.Compile().Invoke(objectB);

                if (sA != sB)
                    yield return field + ("".PadLeft(indent)) + sA + " -> " + sB;
            }
        }


        /// <summary>
        /// Compare two lists of objects and then compare the ones inside them ...
        /// </summary>
        public static IEnumerable<string> CompareList<T>(int indent, IEnumerable<T> objectAs, IEnumerable<T> objectBs, IEqualityComparer<T> equalityComparer)
            where T: IStructureComparable<T>
        {
            string typeName = typeof(T).GetType().Name;

            // Find all the A's or B's that aren't in the other ... they are new or missing
            var missingAs = objectAs.Except(objectBs, equalityComparer);
            var newBs = objectBs.Except(objectAs, equalityComparer);
            var matched = objectAs.Intersect(objectBs, equalityComparer).Select(x => new{a = x, b = objectBs.Where(b => equalityComparer.Equals(x, b)).FirstOrDefault()});

            foreach (var ma in missingAs)
                yield return "".PadLeft(indent) + "Removed: " + ma.ToString();

            foreach (var nb in newBs)
                yield return "".PadLeft(indent) + "Added  : " + nb.ToString();

            // And then compare all the ones that are the same ... treating them as IStructureComparable<T>
            var results = matched.SelectMany(match => match.a.StructureCompare(indent + 3, match.b));
            foreach (var res in results)
                yield return res;
        }
    }
}
