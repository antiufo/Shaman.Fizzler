namespace Fizzler
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a selector implementation over an arbitrary type of elements.
    /// </summary>
    /// <typeparam name="TElement">The type of elements.</typeparam>
    /// <param name="elements">The elements to filter or process.</param>
    /// <returns>The result of the selector.</returns>
    public delegate IEnumerable<TElement> Selector<TElement>(IEnumerable<TElement> elements);
}