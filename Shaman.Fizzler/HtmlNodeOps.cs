using Fizzler;

namespace Fizzler.Systems.HtmlAgilityPack
{
    #region Imports

    using System;
    using System.Linq;
    using global::Shaman.Dom;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Shaman.Runtime;

    #endregion

    /// <summary>
    /// An <see cref="IElementOps{TElement}"/> implementation for <see cref="HtmlNode"/>
    /// from <a href="http://www.codeplex.com/htmlagilitypack">HtmlAgilityPack</a>.
    /// </summary>
    public class HtmlNodeOps : IElementOps<HtmlNode>
    {
        private static readonly IEnumerable<HtmlNode> EmptyEnumerable = Enumerable.Empty<HtmlNode>();

        public static string GetQualifiedName(NamespacePrefix ns, string name)
        {
            if (ns.IsSpecific) return (ns.Text + ":" + name).ToLowerFast();
            return name.ToLowerFast();
        }

        public virtual Selector<HtmlNode> Type(NamespacePrefix prefix, string type)
        {
            var qualified = GetQualifiedName(prefix, type);
            return (nodes => nodes.Where(n => n.TagName == qualified));
        }

        public virtual Selector<HtmlNode> Universal(NamespacePrefix prefix)
        {
            return prefix.IsSpecific
                 ? (Selector<HtmlNode>)(nodes => EmptyEnumerable)
                 : (nodes => nodes.Where(x => x.IsElement()));
        }

        public virtual Selector<HtmlNode> Id(string id)
        {
            return nodes =>
            {
                var element = nodes.FirstOrDefault(n => n.Id == id);
                return element != null ? new[] { element } : EmptyEnumerable;
            };
        }

        public virtual Selector<HtmlNode> Class(string clazz)
        {
            return nodes => nodes.Where(n => n.HasClass(clazz));
        }

        public virtual Selector<HtmlNode> AttributeExists(NamespacePrefix prefix, string name)
        {
            var qualified = GetQualifiedName(prefix, name);
            return (nodes => nodes.Where(n => n.GetAttributeValue(qualified) != null));
        }

        public virtual Selector<HtmlNode> AttributeExact(NamespacePrefix prefix, string name, string value)
        {
            var qualified = GetQualifiedName(prefix, name);
            return (nodes => nodes.Where(x => x.GetAttributeValue(name) == value));
        }

        public virtual Selector<HtmlNode> AttributeNotEqual(NamespacePrefix prefix, string name, string value)
        {
            var qualified = GetQualifiedName(prefix, name);
            return (nodes => nodes.Where(x => x.IsElement() && x.GetAttributeValue(qualified) != value));
        }

        public virtual Selector<HtmlNode> AttributeIncludes(NamespacePrefix prefix, string name, string value)
        {
            var qualified = GetQualifiedName(prefix, name);
            return (nodes => nodes.Where(x =>
            {
                var a = x.GetAttributeValue(qualified);
                return a != null && a.Split(' ').Contains(value);
            }));
        }

        public virtual Selector<HtmlNode> AttributeRegexMatch(NamespacePrefix prefix, string name, string value)
        {
            var regex = CreateRegex(value);
            var qualified = GetQualifiedName(prefix, name);
            return (nodes => nodes.Where(x =>
            {
                var a = x.GetAttributeValue(qualified);
                return a != null &&
#if SALTARELLE
                regex.Test(a);
#else
                regex.IsMatch(a);
#endif
            }));
        }

        public virtual Selector<HtmlNode> AttributeDashMatch(NamespacePrefix prefix, string name, string value)
        {
            var qualified = GetQualifiedName(prefix, name);
            return string.IsNullOrEmpty(value)
                 ? (Selector<HtmlNode>)(nodes => EmptyEnumerable)
                 : (nodes => nodes.Where(x =>
                 {
                     var a = x.GetAttributeValue(qualified);
                     return a != null && a.Split('-').Contains(value);
                 }));
        }

        public Selector<HtmlNode> AttributePrefixMatch(NamespacePrefix prefix, string name, string value)
        {
            var qualified = GetQualifiedName(prefix, name);
            return string.IsNullOrEmpty(value)
                 ? (Selector<HtmlNode>)(nodes => EmptyEnumerable)
                 : (nodes => nodes.Where(x =>
                 {
                     var a = x.GetAttributeValue(qualified);
                     return a != null && a.StartsWith(value);
                 }));

        }

        public Selector<HtmlNode> AttributeSuffixMatch(NamespacePrefix prefix, string name, string value)
        {
            var qualified = GetQualifiedName(prefix, name);
            return string.IsNullOrEmpty(value)
                 ? (Selector<HtmlNode>)(nodes => EmptyEnumerable)
                 : (nodes => nodes.Where(x =>
                 {
                     var a = x.GetAttributeValue(qualified);
                     return a != null && a.EndsWith(value);
                 }));
        }

        public Selector<HtmlNode> AttributeSubstring(NamespacePrefix prefix, string name, string value)
        {
            var qualified = GetQualifiedName(prefix, name);
            return string.IsNullOrEmpty(value)
                 ? (Selector<HtmlNode>)(nodes => EmptyEnumerable)
                 : (nodes => nodes.Where(x =>
                 {
                     var a = x.GetAttributeValue(qualified);
                     return a != null && a.Contains(value);
                 }));
        }

        public virtual Selector<HtmlNode> FirstChild()
        {
            return nodes => nodes.Where(n => !n.ElementsBeforeSelf().Any());
        }

        public virtual Selector<HtmlNode> LastChild()
        {
            return nodes => nodes.Where(n => n.ParentNode.NodeType != HtmlNodeType.Document
                                          && !n.ElementsAfterSelf().Any());
        }

        public virtual Selector<HtmlNode> NthChild(int a, int b)
        {
            if (a != 1)
                throw new NotSupportedException("The nth-child(an+b) selector where a is not 1 is not supported.");

            return nodes => from n in nodes
                            let elements = n.ParentNode.ChildNodes.Where(x => x.IsElement()).Take(b).ToArray()
                            where elements.Length == b && elements.Last().Equals(n)
                            select n;
        }

        public virtual Selector<HtmlNode> OnlyChild()
        {
            return nodes => nodes.Where(n => n.ParentNode.NodeType != HtmlNodeType.Document
                                          && !n.ElementsAfterSelf().Concat(n.ElementsBeforeSelf()).Any());
        }

        public virtual Selector<HtmlNode> Empty()
        {
            return nodes => nodes.Where(n => n.IsElement() && n.ChildNodes.Count == 0);
        }

        public virtual Selector<HtmlNode> Child()
        {
            return nodes => nodes.SelectMany(n => n.ChildNodes.Where(x => x.IsElement()));
        }

        public virtual Selector<HtmlNode> Descendant()
        {
            return nodes => nodes.SelectMany(n => n.Descendants().Where(x => x.IsElement()));
        }

        public virtual Selector<HtmlNode> Adjacent()
        {
            return nodes => nodes.SelectMany(n => n.ElementsAfterSelf().Take(1));
        }

        public virtual Selector<HtmlNode> GeneralSibling()
        {
            return nodes => nodes.SelectMany(n => n.ElementsAfterSelf());
        }

        public Selector<HtmlNode> NthLastChild(int a, int b)
        {
            if (a != 1)
                throw new NotSupportedException("The nth-last-child(an+b) selector where a is not 1 is not supported.");

            return nodes => from n in nodes
                            let elements = n.ParentNode.ChildNodes.Where(x => x.IsElement()).Skip(Math.Max(0, n.ParentNode.ChildNodes.Count(x => x.IsElement()) - b)).Take(b).ToArray()
                            where elements.Length == b && elements.First().Equals(n)
                            select n;
        }

        public Selector<HtmlNode> Eq(int n)
        {
            return nodes =>
            {
                var node = nodes.ElementAtOrDefault(n);
                return node != null ? new[] { node } : EmptyEnumerable;
            };
        }

        public Selector<HtmlNode> Has(ISelectorGenerator subgenerator)
        {
            var castedGenerator = (SelectorGenerator<HtmlNode>)subgenerator;

            var compiled = castedGenerator.Selector;

            return nodes => nodes.Where(n => compiled(new[] { n }).Any());
        }

        public Selector<HtmlNode> SplitAfter(ISelectorGenerator subgenerator)
        {
            return nodes => nodes.SelectMany(x => Split(subgenerator, x, false, true, true));
        }

        public Selector<HtmlNode> SplitBefore(ISelectorGenerator subgenerator)
        {
            return nodes => nodes.SelectMany(x => Split(subgenerator, x, true, false, false));
        }

        public Selector<HtmlNode> SplitBetween(ISelectorGenerator subgenerator)
        {
            return nodes => nodes.SelectMany(x => Split(subgenerator, x, false, false, null));
        }

        public Selector<HtmlNode> SplitAll(ISelectorGenerator subgenerator)
        {
            return nodes => nodes.SelectMany(x => Split(subgenerator, x, true, true, null));
        }

        private Selector<HtmlNode> GetSelector(ISelectorGenerator subgenerator)
        {
            return ((SelectorGenerator<HtmlNode>)subgenerator).Selector;
        }

        private IEnumerable<HtmlNode> Split(ISelectorGenerator subgenerator, HtmlNode parent, bool keepBefore, bool keepAfter, bool? rightEndOpen)
        {
            Selector<HtmlNode> compiled = this.GetSelector(subgenerator);
            HtmlNode[] array = parent.ChildNodes.ToArray<HtmlNode>();
            List<int> list = new List<int>();
            int splitterIndex = 0;
            foreach (HtmlNode current in compiled(new HtmlNode[]
            {
                parent
            }))
            {
#if SALTARELLE
                splitterIndex = array.IndexOf(current, splitterIndex);
#else
                splitterIndex = Array.IndexOf<HtmlNode>(array, current, splitterIndex);
#endif

                if (splitterIndex == -1)
                {

                    throw new FormatException("The node splitter must be a direct child of the context node.");
                }
                list.Add(splitterIndex);
            }
            if (list.Count == 0)
            {
                if (keepBefore & keepAfter)
                {
                    yield return parent;
                }
                yield break;
            }
            HtmlDocument ownerDocument = parent.OwnerDocument;
            bool flag = keepBefore != keepAfter;
            if (keepBefore)
            {
                yield return this.CreateNodesGroup(ownerDocument, array, 0, list[0] + (flag ? 0 : -1), rightEndOpen);
            }
            int num4;
            for (int i = 1; i < list.Count; i = num4 + 1)
            {
                int num2 = list[i - 1] + 1;
                int num3 = list[i] - 1;
                if (flag)
                {
                    if (keepAfter)
                    {
                        num2--;
                    }
                    else
                    {
                        num3++;
                    }
                }
                yield return this.CreateNodesGroup(ownerDocument, array, num2, num3, rightEndOpen);
                num4 = i;
            }
            if (keepAfter)
            {
                yield return this.CreateNodesGroup(ownerDocument, array, list[list.Count - 1] + (flag ? 0 : 1), array.Length - 1, rightEndOpen);
            }
            yield break;
        }


        public Selector<HtmlNode> Before(ISelectorGenerator subgenerator)
        {
            return nodes => nodes.SelectNonNull(parent =>
            {
                var end = IndexOfChild(subgenerator, parent, 0);
                return end != null ? CreateNodesGroup(parent.OwnerDocument, parent.ChildNodes, 0, end.Value - 1, false) : null;
            });
        }

        public Selector<HtmlNode> After(ISelectorGenerator subgenerator)
        {
            return nodes => nodes.SelectNonNull(parent =>
            {
                var start = IndexOfChild(subgenerator, parent, 0);
                return start != null ? CreateNodesGroup(parent.OwnerDocument, parent.ChildNodes, start.Value + 1, parent.ChildNodes.Count - 1, true) : null;
            });
        }

        public Selector<HtmlNode> Between(ISelectorGenerator startGenerator, ISelectorGenerator endGenerator)
        {

            return nodes => nodes.SelectNonNull(parent =>
            {
                var start = IndexOfChild(startGenerator, parent, 0);
                if (start == null) return null;
                var end = IndexOfChild(endGenerator, parent, start.Value);
                if (end == null) return null;

                return CreateNodesGroup(parent.OwnerDocument, parent.ChildNodes, start.Value + 1, end.Value - 1, null);
            });
        }

        private int? IndexOfChild(ISelectorGenerator subgenerator, HtmlNode parent, int startIndex)
        {
            var selector = GetSelector(subgenerator);

            var children = parent.ChildNodes;
            var limit = selector(new[] { parent })
                .Select(x => new { Node = x, Position = children.IndexOf(x) })
                .FirstOrDefault(x =>
                {
                    if (x.Position == -1)
                        throw new FormatException("The limit node must be a direct child of the context node.");
                    return x.Position >= startIndex;
                });

            return limit != null ? limit.Position : (int?)null;
        }

        private HtmlNode CreateNodesGroup(HtmlDocument doc, IList<HtmlNode> nodes, int start, int last, bool? rightOpenEnd)
        {
            var group = doc.CreateElement("fizzler-node-group");
            if (rightOpenEnd != null) group.SetAttributeValue("fizzler-group-direction", rightOpenEnd.Value ? "right" : "left");
            for (int i = start; i <= last; i++)
            {
                group.ChildNodes.Add(nodes[i]);
            }
            return group;
        }

        public Selector<HtmlNode> Not(ISelectorGenerator subgenerator)
        {
            var castedGenerator = (SelectorGenerator<HtmlNode>)subgenerator;

            var compiled = castedGenerator.Selector;

            return nodes =>
            {
                var matches = compiled(nodes).ToList();
                return nodes.Except(matches);
            };
        }


        private static string GetInnerTextCached(HtmlNode node)
        {
#if SALTARELLE

            var dyn = (dynamic)node;
            var q = dyn.cachedInnerText;
            if (q != null) return q;
            if (node.NodeType == HtmlNodeType.Comment) {
                q = ((HtmlCommentNode)node).Comment;
            } else if (node.NodeType == HtmlNodeType.Text) {
                q = ((HtmlTextNode)node).Text;
            } else {
                var sb = "";
                foreach (var child in node.ChildNodes)
                {
                    sb += GetInnerTextCached(child);
                }
                q = sb;
            }
            dyn.cachedInnerText = q;
            return q;

#else
            return node.InnerText;
#endif
        }
        private static bool GetInnerTextCachedContains(HtmlNode node, string text)
        {
            var t = GetInnerTextCached(node);
            return t.Contains(text);
        }

        public Selector<HtmlNode> SelectParent()
        {
            return nodes => nodes.SelectNonNull(x => x.ParentNode);
        }

        public Selector<HtmlNode> Contains(string text)
        {
            return nodes => nodes.Where(x => GetInnerTextCachedContains(x, text));
        }

        public Selector<HtmlNode> Matches(string pattern)
        {
            var regex = CreateRegex(pattern);
            return nodes => nodes.Where(x =>
            {
                var a = x.InnerText;
#if SALTARELLE
                return regex.Test(a);
#else
                return regex.IsMatch(a);
#endif
            });
        }

        private static Regex CreateRegex(string pattern)
        {
            try
            {
#if SALTARELLE
                return new Regex(pattern);
#else
                return new Regex(pattern, RegexOptions.Multiline | RegexOptions.Singleline);
#endif
            }
            catch (ArgumentException ex)
            {
                throw new FormatException(ex.Message, ex);
            }
        }

        public Selector<HtmlNode> Last()
        {
            return nodes =>
            {
                var last = nodes.LastOrDefault();
                return last != null ? new[] { last } : EmptyEnumerable;
            };
        }

    }
}
