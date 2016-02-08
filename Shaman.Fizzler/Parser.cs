namespace Fizzler
{
    #region Imports

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;

#if SALTARELLE
    using OptionalToken = Token;
    using OptionalNamespacePrefix = NamespacePrefix;
#else
    using OptionalToken = System.Nullable<Token>;
    using OptionalNamespacePrefix = System.Nullable<NamespacePrefix>;
#endif

    #endregion

    /// <summary>
    /// Semantic parser for CSS selector grammar.
    /// </summary>
    public sealed class Parser
    {

        internal class CustomSelectorInfo
        {
            public readonly Delegate Delegate;
            public readonly Type[] ArgTypes;
            public CustomSelectorInfo(Delegate deleg, params Type[] argTypes)
            {
                this.Delegate = deleg;
                this.ArgTypes = argTypes;
            }
        }


        private readonly Reader<Token> _reader;
        private readonly ISelectorGenerator _generator;
        private readonly bool _expectEoi;

        private Parser(Reader<Token> reader, ISelectorGenerator generator)
            : this(reader, generator, true)
        {
        }


        private Parser(Reader<Token> reader, ISelectorGenerator generator, bool expectEoi)
        {
            Debug.Assert(reader != null);
            Debug.Assert(generator != null);
            _reader = reader;
            _generator = generator;
            _expectEoi = expectEoi;
        }

        /// <summary>
        /// Parses a CSS selector group and generates its implementation.
        /// </summary>
		public static TGenerator Parse<TGenerator>(string selectors, TGenerator generator) where TGenerator : ISelectorGenerator
        {
            return Parser.Parse<TGenerator, TGenerator>(selectors, generator, g => g);
        }
        /// <summary>
        /// Parses a CSS selector group and generates its implementation.
        /// </summary>
		public static T Parse<TGenerator, T>(string selectors, TGenerator generator, Func<TGenerator, T> resultor) where TGenerator : ISelectorGenerator
        {
            if (selectors == null)
            {
                throw new ArgumentNullException("selectors");
            }
            if (selectors.Length == 0)
            {
                throw new ArgumentException(null, "selectors");
            }
            return Parser.Parse<TGenerator, T>(Tokener.Tokenize(selectors), generator, resultor);
        }

        /// <summary>
        /// Parses a tokenized stream representing a CSS selector group and 
        /// generates its implementation.
        /// </summary>
		public static TGenerator Parse<TGenerator>(IEnumerable<Token> tokens, TGenerator generator) where TGenerator : ISelectorGenerator
        {
            return Parser.Parse<TGenerator, TGenerator>(tokens, generator, g => g);
        }
        public static T Parse<TGenerator, T>(IEnumerable<Token> tokens, TGenerator generator, Func<TGenerator, T> resultor) where TGenerator : ISelectorGenerator
        {
            if (tokens == null)
            {
                throw new ArgumentNullException("tokens");
            }
            if (resultor == null)
            {
                throw new ArgumentNullException("resultor");
            }
            new Parser(new Reader<Token>(tokens.GetEnumerator()), generator).Parse(false);
            return resultor(generator);
        }

        private void Parse(bool alwaysAnchor)
        {
            this._generator.OnInit();
            if (alwaysAnchor)
            {
                this._generator.AnchorToRoot();
            }
            if (this.TryRead(Parser.ToTokenSpec(Token.Slash())) != null)
            {
                this._generator.AnchorToRoot();
                this.TryRead(Parser.ToTokenSpec(TokenKind.WhiteSpace));
            }
            this.SelectorGroup();
            this._generator.OnClose();
        }

        private void SelectorGroup()
        {
            //selectors_group
            //  : selector [ COMMA S* selector ]*
            //  ;

            Selector();
            while (TryRead(ToTokenSpec(Token.Comma())) != null)
            {
                TryRead(ToTokenSpec(TokenKind.WhiteSpace));
                Selector();
            }

            if (_expectEoi)
                Read(ToTokenSpec(TokenKind.Eoi));
        }

        private void Selector()
        {
            _generator.OnSelector();

            //selector
            //  : simple_selector_sequence [ combinator simple_selector_sequence ]*
            //  ;

            SimpleSelectorSequence();
            while (TryCombinator())
                SimpleSelectorSequence();
        }

        private bool TryCombinator()
        {
            //combinator
            //  /* combinators can be surrounded by whitespace */
            //  : PLUS S* | GREATER S* | TILDE S* | S+
            //  ;

            var token = this.TryRead(new TokenSpec[]
            {
                Parser.ToTokenSpec(TokenKind.Plus),
                Parser.ToTokenSpec(TokenKind.Greater),
                Parser.ToTokenSpec(TokenKind.Tilde),
                Parser.ToTokenSpec(TokenKind.WhiteSpace)
            });
            if (token == null)
            {
                return false;
            }
            if (token.Value.Kind == TokenKind.WhiteSpace)
            {
                this._generator.Descendant();
            }
            else
            {
                TokenKind kind = token.Value.Kind;
                if (kind != TokenKind.Plus)
                {
                    if (kind != TokenKind.Greater)
                    {
                        if (kind == TokenKind.Tilde)
                        {
                            this._generator.GeneralSibling();
                        }
                    }
                    else
                    {
                        this._generator.Child();
                    }
                }
                else
                {
                    this._generator.Adjacent();
                }
                this.TryRead(Parser.ToTokenSpec(TokenKind.WhiteSpace));
            }
            return true;
        }

        private void SimpleSelectorSequence()
        {
            //simple_selector_sequence
            //  : [ type_selector | universal ]
            //    [ HASH | class | attrib | pseudo | negation ]*
            //  | [ HASH | class | attrib | pseudo | negation ]+
            //  ;

            var named = false;
            for (var modifiers = 0; ; modifiers++)
            {
                var token = TryRead(ToTokenSpec(TokenKind.Hash), ToTokenSpec(Token.Dot()), ToTokenSpec(Token.LeftBracket()), ToTokenSpec(Token.Colon()));

                if (token == null)
                {
                    if (named || modifiers > 0)
                        break;
                    TypeSelectorOrUniversal();
                    named = true;
                }
                else
                {
                    if (modifiers == 0 && !named)
                        _generator.Universal(NamespacePrefix.None); // implied

                    if (token.Value.Kind == TokenKind.Hash)
                    {
                        _generator.Id(token.Value.Text);
                    }
                    else
                    {
                        Unread(token.Value);
                        switch (token.Value.Text[0])
                        {
                            case '.': Class(); break;
                            case '[': Attrib(); break;
                            case ':': Pseudo(); break;
                            default: throw new Exception("Internal error.");
                        }
                    }
                }
            }
        }

        private void Pseudo()
        {
            //pseudo
            //  /* '::' starts a pseudo-element, ':' a pseudo-class */
            //  /* Exceptions: :first-line, :first-letter, :before and :after. */
            //  /* Note that pseudo-elements are restricted to one per selector and */
            //  /* occur only in the last simple_selector_sequence. */
            //  : ':' ':'? [ IDENT | functional_pseudo ]
            //  ;

            PseudoClass(); // We do pseudo-class only for now
        }

        private void PseudoClass()
        {
            //pseudo
            //  : ':' [ IDENT | functional_pseudo ]
            //  ;

            Read(ToTokenSpec(Token.Colon()));
            if (!TryFunctionalPseudo())
            {
                var clazz = Read(ToTokenSpec(TokenKind.Ident)).Text;
                switch (clazz)
                {
                    case "first-child": _generator.FirstChild(); break;
                    case "last-child": _generator.LastChild(); break;
                    case "only-child": _generator.OnlyChild(); break;
                    case "empty": _generator.Empty(); break;
                    case "last": _generator.Last(); break;
                    case "select-parent": _generator.SelectParent(); break;
                    default: CustomSelector(clazz, false); break;
                }
            }
        }

        private void CustomSelector(string name, bool hasArguments)
        {
            Parser.CustomSelectorInfo customSelectorInfo;
            if (!Parser.CustomSelectors.TryGetValue(name, out customSelectorInfo))
            {
                throw new FormatException(string.Format("Unknown pseudo-selector '{0}'.", new object[]
                {
                    name
                }));
            }
            Type[] argTypes = customSelectorInfo.ArgTypes;
            object[] array = new object[argTypes.Length];
            for (int i = 0; i < argTypes.Length; i++)
            {
                if (i != 0)
                {
                    this.Read(Parser.ToTokenSpec(Token.Semicolon()));
                    this.Read(Parser.ToTokenSpec(TokenKind.WhiteSpace));
                }
                Type type = argTypes[i];
                if (type == typeof(string))
                {
                    array[i] = this.Read(Parser.ToTokenSpec(TokenKind.String)).Text;
                }
                else
                {
                    if (type == typeof(byte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong) || type == typeof(sbyte) || type == typeof(short) || type == typeof(int) || type == typeof(long))
                    {
                        array[i] = int.Parse(this.Read(Parser.ToTokenSpec(TokenKind.Integer)).Text
#if !SALTARELLE
                            , CultureInfo.InvariantCulture
#endif
                            );
                    }
                    else
                    {
#if !SALTARELLE
                        if (type.GetGenericTypeDefinition() != typeof(Selector<>))
                        {
                            throw new ArgumentException(string.Format("Unsupported parameter type for custom selector '{0}'", new object[]
                            {
                                name
                            }));
                        }
#endif
                        array[i] = this.ParseSubGenerator(false).SelectorObject;
                    }
                }
            }
            
           
#if SALTARELLE
            var func = Script.Reinterpret<Function>(customSelectorInfo.Delegate);
            var selector = func.Apply(null, array);
#else
            object selector = customSelectorInfo.Delegate.DynamicInvoke(array);
#endif
            this._generator.CustomSelector(selector);

        }

        private bool TryFunctionalPseudo()
        {
            //functional_pseudo
            //  : FUNCTION S* expression ')'
            //  ;

            var token = TryRead(ToTokenSpec(TokenKind.Function));
            if (token == null)
                return false;

            TryRead(ToTokenSpec(TokenKind.WhiteSpace));

            var func = token.Value.Text;
            switch (func)
            {
                case "eq": Eq(); break;
                case "nth-child": Nth(); break;
                case "nth-last-child": NthLast(); break;
                case "has": Has(); break;
                case "split-after": SplitAfter(); break;
                case "split-before": SplitBefore(); break;
                case "split-between": SplitBetween(); break;
                case "split-all": SplitAll(); break;
                case "before": Before(); break;
                case "after": After(); break;
                case "between": Between(); break;
                case "not": Not(); break;
                case "contains": Contains(); break;
                case "matches": Matches(); break;
                default: CustomSelector(func, true); break;
            }

            Read(ToTokenSpec(Token.RightParenthesis()));
            return true;
        }


        private void Contains()
        {
            var text = Read(ToTokenSpec(TokenKind.String)).Text;
            _generator.Contains(text);
        }


        private void Matches()
        {
            var text = Read(ToTokenSpec(TokenKind.String)).Text;
            _generator.Matches(text);
        }


        private void Has()
        {
            ParseWithExpression(_generator.Has);
        }

        private void SplitAfter()
        {
            ParseWithExpression(_generator.SplitAfter);
        }

        private void SplitBefore()
        {
            ParseWithExpression(_generator.SplitBefore);
        }

        private void SplitBetween()
        {
            ParseWithExpression(_generator.SplitBetween);
        }

        private void SplitAll()
        {
            ParseWithExpression(_generator.SplitAll);
        }

        private void Before()
        {
            ParseWithExpression(_generator.Before);
        }

        private void After()
        {
            ParseWithExpression(_generator.After);
        }

        private void Between()
        {
            var gen1 = ParseSubGenerator(false);
            Read(ToTokenSpec(Token.Semicolon()));
            Read(ToTokenSpec(TokenKind.WhiteSpace));
            var gen2 = ParseSubGenerator(false);
            _generator.Between(gen1, gen2);
        }

        private void Not()
        {
            ParseWithExpression(_generator.Not, true);
        }

        private void ParseWithExpression(Action<ISelectorGenerator> generatorMethod, bool alwaysAnchor = false)
        {
            generatorMethod(ParseSubGenerator(alwaysAnchor));
        }

        private ISelectorGenerator ParseSubGenerator(bool alwaysAnchor)
        {
            var subgenerator = _generator.CreateNew();
            var inner = new Parser(_reader, subgenerator, false);
            inner.Parse(alwaysAnchor);
            return subgenerator;
        }

        private void Nth()
        {
            //nth
            //  : S* [ ['-'|'+']? INTEGER? {N} [ S* ['-'|'+'] S* INTEGER ]? |
            //         ['-'|'+']? INTEGER | {O}{D}{D} | {E}{V}{E}{N} ] S*
            //  ;

            // TODO Add support for the full syntax
            // At present, only INTEGER is allowed

            _generator.NthChild(1, NthB());
        }

        private void NthLast()
        {
            //nth
            //  : S* [ ['-'|'+']? INTEGER? {N} [ S* ['-'|'+'] S* INTEGER ]? |
            //         ['-'|'+']? INTEGER | {O}{D}{D} | {E}{V}{E}{N} ] S*
            //  ;

            // TODO Add support for the full syntax
            // At present, only INTEGER is allowed

            _generator.NthLastChild(1, NthB());
        }

        private void Eq()
        {
            _generator.Eq(NthB());
        }


        private int NthB()
        {
            return int.Parse(Read(ToTokenSpec(TokenKind.Integer)).Text
#if !SALTARELLE
                , CultureInfo.InvariantCulture
#endif
                );
        }

        private void Attrib()
        {
            //attrib
            //  : '[' S* [ namespace_prefix ]? IDENT S*
            //        [ [ PREFIXMATCH |
            //            SUFFIXMATCH |
            //            SUBSTRINGMATCH |
            //            '=' |
            //            INCLUDES |
            //            DASHMATCH ] S* [ IDENT | STRING ] S*
            //        ]? ']'
            //  ;

            Read(ToTokenSpec(Token.LeftBracket()));
            var prefix = TryNamespacePrefix() ?? NamespacePrefix.None;
            var name = Read(ToTokenSpec(TokenKind.Ident)).Text;

            var hasValue = false;
            while (true)
            {
                var op = TryRead(
                    ToTokenSpec(Token.Equals()),
                    ToTokenSpec(TokenKind.NotEqual),
                    ToTokenSpec(TokenKind.Includes),
                    ToTokenSpec(TokenKind.RegexMatch),
                    ToTokenSpec(TokenKind.DashMatch),
                    ToTokenSpec(TokenKind.PrefixMatch),
                    ToTokenSpec(TokenKind.SuffixMatch),
                    ToTokenSpec(TokenKind.SubstringMatch));

                if (op == null)
                    break;

                hasValue = true;
                var value = Read(ToTokenSpec(TokenKind.String), ToTokenSpec(TokenKind.Ident)).Text;

                if (op.Value == Token.Equals())
                {
                    _generator.AttributeExact(prefix, name, value);
                }
                else
                {
                    switch (op.Value.Kind)
                    {
                        case TokenKind.Includes: _generator.AttributeIncludes(prefix, name, value); break;
                        case TokenKind.RegexMatch: _generator.AttributeRegexMatch(prefix, name, value); break;
                        case TokenKind.DashMatch: _generator.AttributeDashMatch(prefix, name, value); break;
                        case TokenKind.PrefixMatch: _generator.AttributePrefixMatch(prefix, name, value); break;
                        case TokenKind.SuffixMatch: _generator.AttributeSuffixMatch(prefix, name, value); break;
                        case TokenKind.SubstringMatch: _generator.AttributeSubstring(prefix, name, value); break;
                        case TokenKind.NotEqual: _generator.AttributeNotEqual(prefix, name, value); break;
                    }
                }
            }

            if (!hasValue)
                _generator.AttributeExists(prefix, name);

            Read(ToTokenSpec(Token.RightBracket()));
        }

        private void Class()
        {
            //class
            //  : '.' IDENT
            //  ;

            Read(ToTokenSpec(Token.Dot()));
            _generator.Class(Read(ToTokenSpec(TokenKind.Ident)).Text);
        }

        private OptionalNamespacePrefix TryNamespacePrefix()
        {
            //namespace_prefix
            //  : [ IDENT | '*' ]? '|'
            //  ;

            var pipe = Token.Pipe();
            var token = TryRead(ToTokenSpec(TokenKind.Ident), ToTokenSpec(Token.Star()), ToTokenSpec(pipe));

            if (token == null)
                return null;

            if (token.Value == pipe)
                return NamespacePrefix.Empty;

            var prefix = token.Value;
            if (TryRead(ToTokenSpec(pipe)) == null)
            {
                Unread(prefix);
                return null;
            }

            return prefix.Kind == TokenKind.Ident
                 ? new NamespacePrefix(prefix.Text)
                 : NamespacePrefix.Any;
        }

        private void TypeSelectorOrUniversal()
        {
            //type_selector
            //  : [ namespace_prefix ]? element_name
            //  ;
            //element_name
            //  : IDENT
            //  ;
            //universal
            //  : [ namespace_prefix ]? '*'
            //  ;

            var prefix = TryNamespacePrefix() ?? NamespacePrefix.None;
            var token = Read(ToTokenSpec(TokenKind.Ident), ToTokenSpec(Token.Star()));
            if (token.Kind == TokenKind.Ident)
                _generator.Type(prefix, token.Text);
            else
                _generator.Universal(prefix);
        }

        private Token Peek()
        {
            return _reader.Peek();
        }

        private Token Read(TokenSpec spec)
        {
            var token = TryRead(spec);
            if (token == null)
            {
                throw new FormatException(
                    string.Format(@"Unexpected token '{0}' where '{1}' was expected.",
                    Token.ToString(Peek().Kind), Token.ToString(spec.AsTokenKind)));
            }
            return token.Value;
        }

        private Token Read(params TokenSpec[] specs)
        {
            var token = TryRead(specs);
            if (token == null)
            {
                throw new FormatException(string.Format(
                    @"Unexpected token '{0}' where one of [{1}] was expected.",
                    Token.ToString(Peek().Kind), string.Join(", ", specs.Select(k => Token.ToString(k.AsTokenKind)).ToArray())));
            }
            return token.Value;
        }

        private OptionalToken TryRead(params TokenSpec[] specs)
        {
            foreach (var kind in specs)
            {
                var token = TryRead(kind);
                if (token != null)
                    return token;
            }
            return null;
        }

        private OptionalToken TryRead(TokenSpec spec)
        {
            var token = Peek();
            if (spec.IsTokenKind && spec.AsTokenKind != token.Kind) return null;
            if (!spec.IsTokenKind && spec.AsToken != token) return null;
            _reader.Read();
            return token;
        }

        private void Unread(Token token)
        {
            _reader.Unread(token);
        }

        private static TokenSpec ToTokenSpec(TokenKind kind)
        {
            return new TokenSpec
            {
                AsTokenKind = kind,
                IsTokenKind = true
            };
        }

        private static TokenSpec ToTokenSpec(Token token)
        {
            return new TokenSpec
            {
                AsToken = token
            };
        }

        internal static Dictionary<string, CustomSelectorInfo> CustomSelectors = new Dictionary<string, CustomSelectorInfo>();

        /// <summary>
        /// Registers a custom pseudo-selector which takes no arguments.
        /// </summary>
        /// <typeparam name="TNode">The type of HTML nodes.</typeparam>
        /// <param name="name">The name of the pseudo-selector, with no colons.</param>
        /// <param name="selector">A factory for the selection delegate.</param>
        public static void RegisterCustomSelector<TNode>(string name, Func<Selector<TNode>> selector)
        {
            Parser.CustomSelectors.Add(name, new Parser.CustomSelectorInfo(selector, new Type[0]));
        }

        /// <summary>
        /// Registers a custom pseudo-selector which takes one argument.
        /// </summary>
        /// <remarks>The type of the argument can be a primitive type, a string, or <see cref="Selector{TElement}"/> (for sub-selector expressions).</remarks>
        /// <typeparam name="TNode">The type of HTML nodes.</typeparam>
        /// <typeparam name="T1">The type of the only argument.</typeparam>
        /// <param name="name">The name of the pseudo-selector, with no colons.</param>
        /// <param name="selector">A factory for the selection delegate.</param>
		public static void RegisterCustomSelector<TNode, T1>(string name, Func<T1, Selector<TNode>> selector)
        {
            Parser.CustomSelectors.Add(name, new Parser.CustomSelectorInfo(selector, new Type[]
            {
                typeof(T1)
            }));
        }

        /// <summary>
        /// Registers a custom pseudo-selector which takes two arguments.
        /// </summary>
        /// /// <remarks>The type of the arguments can be a primitive type, a string, or <see cref="Selector{TElement}"/> (for sub-selector expressions).</remarks>
        /// <typeparam name="TNode">The type of HTML nodes.</typeparam>
        /// <typeparam name="T1">The type of the first argument.</typeparam>
        /// <typeparam name="T2">The type of the second argument.</typeparam>
        /// <param name="name">The name of the pseudo-selector, with no colons.</param>
        /// <param name="selector">A factory for the selection delegate.</param>
		public static void RegisterCustomSelector<TNode, T1, T2>(string name, Func<T1, T2, Selector<TNode>> selector)
        {
            Parser.CustomSelectors.Add(name, new Parser.CustomSelectorInfo(selector, new Type[]
            {
                typeof(T1),
                typeof(T2)
            }));
        }

        /// <summary>
        /// Registers a custom pseudo-selector which takes three arguments.
        /// </summary>
        /// /// <remarks>The type of the arguments can be a primitive type, a string, or <see cref="Selector"/> (for sub-selector expressions).</remarks>
        /// <typeparam name="TNode">The type of HTML nodes.</typeparam>
        /// <typeparam name="T1">The type of the first argument.</typeparam>
        /// <typeparam name="T2">The type of the second argument.</typeparam>
        /// <typeparam name="T3">The type of the third argument.</typeparam>
        /// <param name="name">The name of the pseudo-selector, with no colons.</param>
        /// <param name="selector">A factory for the selection delegate.</param>
		public static void RegisterCustomSelector<TNode, T1, T2, T3>(string name, Func<T1, T2, T3, Selector<TNode>> selector)
        {
            Parser.CustomSelectors.Add(name, new Parser.CustomSelectorInfo(selector, new Type[]
            {
                typeof(T1),
                typeof(T2),
                typeof(T3)
            }));
        }

    }
}
