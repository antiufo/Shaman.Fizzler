using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fizzler
{
#if SALTARELLE
    internal class TokenSpec
#else
    internal struct TokenSpec
#endif
    {
        public bool IsTokenKind;
        public Token AsToken;
        public TokenKind AsTokenKind;
    }
}
