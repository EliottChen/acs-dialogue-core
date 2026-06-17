using System;
using System.Collections.Generic;
using System.Text;

namespace ACSDlg.Core
{
    /// <summary>
    /// Splits ACSDlg source text into tokens, character by character in one pass.
    /// Does not understand the grammar — it labels. Handles the first-colon rule
    /// (switching to TEXT mode until the terminating ';'), the '\;' escape,
    /// '//' comments and quoted strings. The content of [...] markup stays inside
    /// the Text token, undissected (that is <see cref="MarkupParser"/>'s job).
    /// Throws <see cref="FormatException"/> with line/column on the first problem (v0).
    /// </summary>
    public class Lexer
    {
        private readonly string _src;
        private int _idx;
        private int _line = 1;
        private int _column = 1;

        public Lexer(string pSource)
        {
            _src = pSource;
        }

        public List<Token> Tokenize()
        {
            List<Token> lTokens = new List<Token>();

            while (true)
            {
                SkipWhitespaceAndComments();
                if (_idx >= _src.Length)
                {
                    lTokens.Add(new Token(TokenType.EndOfFile, "", _line, _column));
                    return lTokens;
                }

                char lChar = _src[_idx];
                int lLine = _line, lColumn = _column;

                switch (lChar)
                {
                    case '#':
                        AdvanceChar();
                        lTokens.Add(new Token(TokenType.Hash, "#", lLine, lColumn));
                        break;
                    case '{':
                        AdvanceChar();
                        lTokens.Add(new Token(TokenType.LBrace, "{", lLine, lColumn));
                        break;
                    case '}':
                        AdvanceChar();
                        lTokens.Add(new Token(TokenType.RBrace, "}", lLine, lColumn));
                        break;
                    case ';':
                        AdvanceChar();
                        lTokens.Add(new Token(TokenType.Semicolon, ";", lLine, lColumn));
                        break;
                    case ':':
                        // First colon of a statement: everything after it is one Text token,
                        // up to the unescaped ';'. Colons inside the text are plain text.
                        AdvanceChar();
                        lTokens.Add(new Token(TokenType.Colon, ":", lLine, lColumn));
                        LexTextUntilSemicolon(lTokens);
                        break;
                    case '"':
                        lTokens.Add(LexString());
                        break;
                    default:
                        if (char.IsLetter(lChar) || lChar == '_')
                        {
                            lTokens.Add(LexWord());
                            break;
                        }
                        throw Error($"Unexpected character '{lChar}'", lLine, lColumn);
                }
            }
        }

        // --- Sub-lexers ---

        // TEXT mode: raw text (markup brackets included) until the unescaped ';'.
        // Emits a Text token then the Semicolon token. '\;' becomes a literal ';'.
        private void LexTextUntilSemicolon(List<Token> pTokens)
        {
            int lLine = _line, lColumn = _column;
            StringBuilder lSb = new StringBuilder();

            while (_idx < _src.Length)
            {
                char lChar = _src[_idx];

                // Verify if the text containt a \; and if after that there is text
                if (lChar == '\\' && _idx + 1 < _src.Length && _src[_idx + 1] == ';')
                {
                    // add ; to the returned string, and advance two time to skip "\;" and read the next
                    lSb.Append(';');
                    AdvanceChar();
                    AdvanceChar();
                    continue;
                }

                // Terminate the text analysis
                if (lChar == ';')
                {
                    pTokens.Add(new Token(TokenType.Text, lSb.ToString(), lLine, lColumn));
                    int lSemiLine = _line, lSemiColumn = _column;
                    AdvanceChar();
                    pTokens.Add(new Token(TokenType.Semicolon, ";", lSemiLine, lSemiColumn));
                    return;
                }

                lSb.Append(lChar);
                AdvanceChar();
            }

            throw Error("Line text without terminating ';'", lLine, lColumn);
        }

        private Token LexString()
        {
            int lLine = _line, lColumn = _column;
            AdvanceChar(); // opening quote

            StringBuilder lSb = new StringBuilder();
            while (_idx < _src.Length && _src[_idx] != '"')
            {
                lSb.Append(_src[_idx]);
                AdvanceChar();
            }

            if (_idx >= _src.Length)
                throw Error("Unterminated string literal", lLine, lColumn);

            AdvanceChar(); // closing quote
            return new Token(TokenType.String, lSb.ToString(), lLine, lColumn);
        }

        private Token LexWord()
        {
            int lLine = _line, lColumn = _column;
            int lStart = _idx;

            while (_idx < _src.Length && (char.IsLetterOrDigit(_src[_idx]) || _src[_idx] == '_'))
                AdvanceChar();

            string lWord = _src.Substring(lStart, _idx - lStart);

            // Keyword lookup. Token.Value keeps the word so the parser can recover it
            // when a keyword turns out to be a speaker name ("jump : ...;").
            TokenType lType = lWord switch
            {
                "node"   => TokenType.KeywordNode,
                "jump"   => TokenType.KeywordJump,
                "choice" => TokenType.KeywordChoice,
                "end"    => TokenType.KeywordEnd,
                _        => TokenType.Identifier,
            };

            return new Token(lType, lWord, lLine, lColumn);
        }

        // --- Low-level helpers ---

        private void SkipWhitespaceAndComments()
        {
            while (_idx < _src.Length)
            {
                char lChar = _src[_idx];
                if (char.IsWhiteSpace(lChar))
                {
                    AdvanceChar();
                    continue;
                }
                if (lChar == '/' && _idx + 1 < _src.Length && _src[_idx + 1] == '/')
                {
                    while (_idx < _src.Length && _src[_idx] != '\n')
                        AdvanceChar();
                    continue;
                }
                return;
            }
        }

        private void AdvanceChar()
        {
            if (_src[_idx] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _idx++;
        }

        private FormatException Error(string pMessage, int pLine, int pColumn)
        {
            return new FormatException($"{pMessage} (line {pLine}, column {pColumn}).");
        }
    }
}
