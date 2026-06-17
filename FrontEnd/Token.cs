namespace ACSDlg.Core
{
    /// <summary>Lexical categories of the ACSDlg format.</summary>
    public enum TokenType
    {
        Hash,
        KeywordNode,
        KeywordJump,
        KeywordChoice,
        KeywordEnd,
        Identifier,
        Colon,
        Semicolon,
        LBrace,
        RBrace,
        String,
        Text,
        EndOfFile,
    }

    /// <summary>
    /// One lexical unit. <see cref="Type"/> says what kind of token, <see cref="Value"/> carries
    /// the variable content (identifiers, text, labels — unused for punctuators).
    /// <see cref="Line"/>/<see cref="Column"/> are 1-based, for error messages and future tooling.
    /// </summary>
    public struct Token
    {
        public TokenType Type;
        public string Value;
        public int Line;
        public int Column;

        public Token(TokenType pType, string pValue, int pLine, int pColumn)
        {
            Type = pType;
            Value = pValue;
            Line = pLine;
            Column = pColumn;
        }

        public override string ToString() => $"{Type}('{Value}') @ {Line}:{Column}";
    }
}
