namespace Fizzler.Systems.HtmlAgilityPack
{
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using global::Shaman.Dom;

    #endregion

    /// <summary>
    /// HtmlNode extension methods.
    /// </summary>
    internal static class HtmlNodeExtensions
    {


        /// <summary>
        /// Returns a collection of the sibling elements after this node.
        /// </summary>
        public static IEnumerable<HtmlNode> ElementsAfterSelf(this HtmlNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return node.NodesAfterSelf().Where(x => x.IsElement()); 
        }

        public static bool IsElement(this HtmlNode node)
        {
            var t = node.NodeType;
            return t == HtmlNodeType.Element || t == HtmlNodeType.Document;
        }

        /// <summary>
        /// Returns a collection of the sibling nodes after this node.
        /// </summary>
        public static IEnumerable<HtmlNode> NodesAfterSelf(this HtmlNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return NodesAfterSelfImpl(node);
        }

        private static IEnumerable<HtmlNode> NodesAfterSelfImpl(HtmlNode node)
        {
            while ((node = node.NextSibling) != null)
                yield return node;
        }

        /// <summary>
        /// Returns a collection of the sibling elements before this node.
        /// </summary>
        public static IEnumerable<HtmlNode> ElementsBeforeSelf(this HtmlNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return node.NodesBeforeSelf().Where(x => x.IsElement());
        }

        /// <summary>
        /// Returns a collection of the sibling nodes before this node.
        /// </summary>
        public static IEnumerable<HtmlNode> NodesBeforeSelf(this HtmlNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return NodesBeforeSelfImpl(node);
        }

        private static IEnumerable<HtmlNode> NodesBeforeSelfImpl(HtmlNode node)
        {
            while ((node = node.PreviousSibling) != null)
                yield return node;
        }

        /// <summary>
        /// Returns a collection of nodes that contains this element 
        /// followed by all descendant nodes of this element.
        /// </summary>
        public static IEnumerable<HtmlNode> DescendantsAndSelf(this HtmlNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return Enumerable.Repeat(node, 1).Concat(node.Descendants());
        }

        /// <summary>
        /// Returns a collection of all descendant nodes of this element.
        /// </summary>
        public static IEnumerable<HtmlNode> Descendants(this HtmlNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return DescendantsImpl(node);
        }

        private static IEnumerable<HtmlNode> DescendantsImpl(HtmlNode node)
        {
            Debug.Assert(node != null);
            foreach (var child in node.ChildNodes)
            {
                yield return child;
                foreach (var descendant in child.Descendants())
                    yield return descendant;
            }
        }

    }
}