# Shaman.Fizzler

A version of Fizzler (CSS selector library) for [Shaman.Dom](https://github.com/antiufo/Shaman.Dom).

See also [Shaman.Http](https://github.com/antiufo/Shaman.Http) for additional pluggable selectors and more HTML/HTTP-oriented features.

## Usage
```csharp
using Shaman.Dom;
using Fizzler.Systems.HtmlAgilityPack;

HtmlElement element = /*…*/;
element.QuerySelectorAll(".div:has(h1):eq(0)");

```
## Supported selectors
| Selector   | Description |
|------------|-------------|
`* |All elements
`div`|Elements with the specified tag name
`#id`|	Elements with the specified id
`.class`|	Elements with the specified class
`[attr]`|	Elements with the specified attribute defined
`[attr='value']`|	Elements with the specified attribute name and value
`[attr~='word']`|Attribute includes the specified word (whitespace-separated)
`[attr!='value']`| Elements with attribute not equal to value (or without attribute)
`[attr^='prefix']`|	Attribute starts with '`prefix`'
`[attr$='suffix']`|	Attribute ends with '`suffix`'
`[attr*='search']`|	Attribute contains '`search`'
`:first-child`|	Elements that are the first child of their parent
`:last-child`|	Elements that are the last child of their parent
`:nth-child(n)`| 	Elements that are the `n`-th child of their parent (1-based)
`:nth-last-child(n)`| 	Elements that are the `n`-th-last-child of their parent (1-based)
`:only-child`|	Elements that are the only child of their parent
`:empty`|	Elements that have no children
`div > p`|Selects the children of the matched elements
`div p`|	Selects the descendant of the matched elements
`prev + next`|	Selects all next elements matching "`next`" that are immediately preceded by a sibling "`prev`"
`prev ~ siblings`|	Selects all sibling elements that follow after the "`prev`" element, have the same parent, and match the filtering "`siblings`" selector.
`:has(b)`|	Elements that contain an element that matches the sub-expression
`:not(.class)`|	Elements that do not match the specified sub-expression
`:contains('text')`|	Elements whose InnerText contains the specified text
`:eq(n)`|	Selects the `n`-th matched element (zero based)
`b:select-parent`|	Selects the parent(s) of the matched node(s)
`div[attr%='[0-9]*']` |	Elements whose attr attribute matches the specified regex
`span:matches('ab?')` |	Elements whose inner text matches the specified regex
`/div`|	Performs the initial selection at the top level of the search context instead of the descendant nodes. For example, `node.QuerySelector("/:select-parent") == node.ParentNode`. Without the slash, the result would be "the parent of the first descendant", probably not what you want.
`body:split-after(hr)` |	Groups the children of `<body>` into a pseudo-element every time a `<hr>` is found. Each `<hr>` will be the first child of its own group. Nodes before the first `<hr>` will be ignored. Note that the sub-selector (`hr`) must only match direct children of the context node. You may want to use `body:split-after(/* > hr)` to force this behavior (see the previous selector)
`body:split-before(hr)`|	Similar to the previous one, except that every `<hr>` will be the last of its own group. Nodes after the last `<hr>` will be ignored.
`body:split-between(hr)`|	Similar to the previous one, except that only content between two `<hr>`s will be included. `<hr>`s themselves won't be part of the groups.
`body:split-all(hr)`|	Similar to the previous one, except that content before the first `<hr>` and after the last `<hr>` will be included too.
`.main:before(hr)`| 	Selects the children of .main preceding the first `<hr>` child, and groups them into a single pseudo-element (`<hr>` is excluded).
`.main:after(h1)`|	Selects the children of .main following the first `<h1>` child, and groups them into a single pseudo-element (`<h1>` is excluded).
`.main:between(h1; hr)`|	Selects the children of .main between the first `<h1>` child and the first following `<hr>` (possibly the same element), grouping them into a single pseudo-element. `<h1>` and `<hr>` are not part of the group. Note the semicolon (`;`) used to separate the two parameters.
`:last`|	Selects the last matched element
`:heading-content(h2:contains('Users'))`|	Groups the next siblings of the specified `<h2>` node into a new pseudo-element, up to the following `<h2>` or `<h1>` (if any)
`tr:nth-cell(3)`|	Returns the `n`th cell (zero based) of a table row, taking `colspan` attributes into account.
`li:skip(2)`|	Skips the first 2 matched nodes.
`tr:skip-last(2)`|	Skips the last 2 matched nodes.

## More features
* Support for .NET Standard
* Support for custom selectors:
```csharp
using Fizzler;
Parser.RegisterCustomSelector<HtmlNode, string>("example", (arg1) =>
{
    return nodes =>
    {
        // return IEnumerable<HtmlNode> from the given ones.
    };
});
```