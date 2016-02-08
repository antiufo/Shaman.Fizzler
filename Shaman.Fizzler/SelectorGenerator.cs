namespace Fizzler
{
    using Shaman.Dom;
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.Linq;

    #endregion

    /// <summary>
    /// A selector generator implementation for an arbitrary document/element system.
    /// </summary>
    public class SelectorGenerator<TElement> : ISelectorGenerator
    {
        private readonly IEqualityComparer<TElement> _equalityComparer;
#if SALTARELLE
        private readonly Stack<Selector<TElement>> _selectors;
#else
        private readonly Stack<Selector<TElement>> _selectors;
#endif
        private bool anchorToRoot;

        public SelectorGenerator(IElementOps<TElement> ops) : this(ops, null) { }

        public SelectorGenerator(IElementOps<TElement> ops, IEqualityComparer<TElement> equalityComparer)
        {
            if (ops == null) throw new ArgumentNullException("ops");
            Ops = ops;
            _equalityComparer = equalityComparer ?? EqualityComparer<TElement>.Default;
            _selectors = new Stack<Selector<TElement>>();
        }

        public Selector<TElement> Selector { get; private set; }
        object ISelectorGenerator.SelectorObject { get { return Selector; } }

        public IElementOps<TElement> Ops { get; private set; }

        public IEnumerable<Selector<TElement>> GetSelectors()
        {
#if SALTARELLE
            var selectors = Script.Reinterpret<List<Selector<TElement>>>(_selectors);
#else
            var selectors = _selectors;
#endif
            var top = Selector;
            return top == null
                 ? selectors.Select(s => s)
                 : selectors.Concat(Enumerable.Repeat(top, 1));
        }

#if SALTARELLE
        private static string ToCacheKey(object obj)
        {
            return obj.ToString();
        }

        protected void Add(Selector<TElement> selector, object key0)
        {
            AddWithKey(selector, ToCacheKey(key0));
        }
        protected void Add(Selector<TElement> selector, object key0, object key1)
        {
            AddWithKey(selector, ToCacheKey(key0) + "|" + ToCacheKey(key1));
        }
        protected void Add(Selector<TElement> selector, object key0, object key1, object key2)
        {
            AddWithKey(selector, ToCacheKey(key0) + "|" + ToCacheKey(key1) + "|" + ToCacheKey(key2));
        }
        protected void Add(Selector<TElement> selector, object key0, object key1, object key2, object key3)
        {
            AddWithKey(selector, ToCacheKey(key0) + "|" + ToCacheKey(key1) + "|" + ToCacheKey(key2) + "|" + ToCacheKey(key3));
        }
#else
        protected void Add(Selector<TElement> selector, object key0)
        {
            AddWithKey(selector, null);
        }
        protected void Add(Selector<TElement> selector, object key0, object key1)
        {
            AddWithKey(selector, null);
        }
        protected void Add(Selector<TElement> selector, object key0, object key1, object key2)
        {
            AddWithKey(selector, null);
        }
        protected void Add(Selector<TElement> selector, object key0, object key1, object key2, object key3)
        {
            AddWithKey(selector, null);
        }
        
#endif



        protected void Add(Selector<TElement> selector)
        {
            AddWithKey(selector, null);
        }

#if SALTARELLE
        private static string GetListKey(IEnumerable<TElement> elements)
        {
            var inputKey = Script.Reinterpret<string>(((dynamic)elements).listKey);
            if (inputKey == null)
            {
                var inputList = elements as List<TElement>;
                if (inputList != null)
                {
                    var s = "";
                    for (int i = 0; i < inputList.Count; i++)
                    {
                        s += ";";
                        s += Script.Reinterpret<HtmlNode>(inputList[i]).NodeId;
                    }
                    inputKey = s;
                    ((dynamic)inputList).listKey = s;
                    listCache[s] = inputList;
                }
                else
                {
                    var s = "";
                    inputList = new List<TElement>();
                    foreach (var item in elements)
                    {
                        s += ";";
                        inputList.Add(item);
                        s += Script.Reinterpret<HtmlNode>(item).NodeId;
                    }
                    inputKey = s;
                    ((dynamic)inputList).listKey = s;
                    listCache[s] = inputList;
                }
            }
            return inputKey;
        }
#endif
        protected void AddWithKey(Selector<TElement> selector, string keystr)
        {
            if (selector == null) throw new ArgumentNullException("selector");



            Selector<TElement> cachedSelector;


            if (keystr != null)
            {
#if SALTARELLE
                var cc = cache;
                JsDictionary<string, string> cache1;
#if SALTARELLE
                cache1 = cc[keystr];
                if (cache1 == null)
#else
                if (!cc.TryGetValue(keystr, out cache1))
#endif
                {
                    cache1 = new JsDictionary<string, string>();
                    cc[keystr] = cache1;
                }


                cachedSelector = elements =>
                 {
                     var input = GetListKey(elements);

                     var resultsKey = cache1[input];
                     if (resultsKey != null)
                     {
                         var result = listCache[resultsKey];
                         if (result != null) return result;
                     }

                     var p = selector(elements);
                     var output = GetListKey(p);
                     cache1[input] = output;
                     return listCache[output];
                 };
#else
                throw new ArgumentException();
#endif
            }
            else
            {
                cachedSelector = selector;
            }
            var top = Selector;
            Selector = top == null ? cachedSelector : (elements => cachedSelector(top(elements)));
        }


#if SALTARELLE

        public static void ClearCache()
        {
            cache = new JsDictionary<string, JsDictionary<string, string>>();
            listCache = new JsDictionary<string, List<TElement>>();
        }

        private static JsDictionary<string, JsDictionary<string, string>> cache = new JsDictionary<string, JsDictionary<string, string>>();
        private static JsDictionary<string, List<TElement>> listCache = new JsDictionary<string, List<TElement>>();
#endif


        public virtual void OnInit()
        {
            _selectors.Clear();
            Selector = null;
            anchorToRoot = false;
        }

        public virtual void OnSelector()
        {
            if (Selector != null)
                _selectors.Push(Selector);
            Selector = null;
        }

        public virtual void OnClose()
        {
            var sum = GetSelectors().Aggregate((a, b) => (elements => a(elements).Concat(b(elements))));
            var normalize = anchorToRoot ? (x => x) : Ops.Descendant();
            Selector = elements => sum(normalize(elements)).Distinct(_equalityComparer);
            _selectors.Clear();
        }

        public virtual void Id(string id)
        {
            Add(Ops.Id(id), "Id", id);
        }

        public virtual void Class(string clazz)
        {
            Add(Ops.Class(clazz), "Class", clazz);
        }

        public virtual void Type(NamespacePrefix prefix, string type)
        {
            Add(Ops.Type(prefix, type), "Type", prefix, type);
        }

        public virtual void Universal(NamespacePrefix prefix)
        {
            Add(Ops.Universal(prefix), "Universal", prefix);
        }

        public virtual void AttributeExists(NamespacePrefix prefix, string name)
        {
            Add(Ops.AttributeExists(prefix, name), "AttributeExists", prefix, name);
        }

        public virtual void AttributeExact(NamespacePrefix prefix, string name, string value)
        {
            Add(Ops.AttributeExact(prefix, name, value), "AttributeExact", prefix, name, value);
        }

        public void AttributeNotEqual(NamespacePrefix prefix, string name, string value)
        {
            Add(Ops.AttributeNotEqual(prefix, name, value), "AttributeNotEqual", prefix, name, value);
        }

        public virtual void AttributeIncludes(NamespacePrefix prefix, string name, string value)
        {
            Add(Ops.AttributeIncludes(prefix, name, value), "AttributeIncludes", prefix, name, value);
        }

        public virtual void AttributeRegexMatch(NamespacePrefix prefix, string name, string value)
        {
            Add(Ops.AttributeRegexMatch(prefix, name, value), "AttributeRegexMatch", prefix, name, value);
        }

        public virtual void AttributeDashMatch(NamespacePrefix prefix, string name, string value)
        {
            Add(Ops.AttributeDashMatch(prefix, name, value), "AttributeDashMatch", prefix, name, value);
        }

        public void AttributePrefixMatch(NamespacePrefix prefix, string name, string value)
        {
            Add(Ops.AttributePrefixMatch(prefix, name, value), "AttributePrefixMatch", prefix, name, value);
        }

        public void AttributeSuffixMatch(NamespacePrefix prefix, string name, string value)
        {
            Add(Ops.AttributeSuffixMatch(prefix, name, value), "AttributeSuffixMatch", prefix, name, value);
        }

        public void AttributeSubstring(NamespacePrefix prefix, string name, string value)
        {
            Add(Ops.AttributeSubstring(prefix, name, value), "AttributeSubstring", prefix, name, value);
        }

        public virtual void FirstChild()
        {
            Add(Ops.FirstChild(), "FirstChild");
        }

        public virtual void LastChild()
        {
            Add(Ops.LastChild(), "LastChild");
        }

        public virtual void NthChild(int a, int b)
        {
            Add(Ops.NthChild(a, b), "NthChild", a, b);
        }

        public virtual void OnlyChild()
        {
            Add(Ops.OnlyChild(), "OnlyChild");
        }

        public virtual void Empty()
        {
            Add(Ops.Empty(), "Empty");
        }

        public virtual void Child()
        {
            Add(Ops.Child(), "Child");
        }

        public virtual void Descendant()
        {
            Add(Ops.Descendant(), "Descendant");
        }

        public virtual void Adjacent()
        {
            Add(Ops.Adjacent(), "Adjacent");
        }

        public virtual void GeneralSibling()
        {
            Add(Ops.GeneralSibling(), "GeneralSibling");
        }

        public void NthLastChild(int a, int b)
        {
            Add(Ops.NthLastChild(a, b), "NthLastChild", a, b);
        }

        public void Eq(int n)
        {
            Add(Ops.Eq(n), "Eq", n);
        }

        public void Has(ISelectorGenerator subgenerator)
        {
            Add(Ops.Has(subgenerator));
        }

        public void SplitAfter(ISelectorGenerator subgenerator)
        {
            Add(Ops.SplitAfter(subgenerator));
        }

        public void SplitBefore(ISelectorGenerator subgenerator)
        {
            Add(Ops.SplitBefore(subgenerator));
        }

        public void SplitBetween(ISelectorGenerator subgenerator)
        {
            Add(Ops.SplitBetween(subgenerator));
        }

        public void SplitAll(ISelectorGenerator subgenerator)
        {
            Add(Ops.SplitAll(subgenerator));
        }

        public void Before(ISelectorGenerator subgenerator)
        {
            Add(Ops.Before(subgenerator));
        }

        public void After(ISelectorGenerator subgenerator)
        {
            Add(Ops.After(subgenerator));
        }

        public void Between(ISelectorGenerator startGenerator, ISelectorGenerator endGenerator)
        {
            Add(Ops.Between(startGenerator, endGenerator));
        }

        public void Not(ISelectorGenerator subgenerator)
        {
            Add(Ops.Not(subgenerator));
        }

        public void SelectParent()
        {
            Add(Ops.SelectParent(), "SelectParent");
        }

        public void Contains(string text)
        {
            Add(Ops.Contains(text), "Contains", text);
        }

        public void Matches(string regex)
        {
            Add(Ops.Matches(regex), "Matches", regex);
        }

        public void CustomSelector(object selector)
        {
            Add((Selector<TElement>)selector);
        }

        public ISelectorGenerator CreateNew()
        {
            return new SelectorGenerator<TElement>(Ops, _equalityComparer);
        }


        public void AnchorToRoot()
        {
            anchorToRoot = true;
        }

        public void Last()
        {
            Add(Ops.Last(), "Last");
        }
    }
}
