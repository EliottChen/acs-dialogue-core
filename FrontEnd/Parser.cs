using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ACSDlg.Core
{
    /// <summary>
    /// Consumes the token stream and builds the <see cref="DialogueGraph"/>.
    /// Recognizes each instruction by its head token (node / jump / choice / end, otherwise
    /// spoken line), instantiates the matching <see cref="IDialogueContent"/>, and triggers
    /// markup parsing of each line (producing <see cref="ParsedLine"/>).
    /// Validates the structure: '#start' present, no duplicate node, every jump and choice
    /// target resolves to an existing node.
    /// Throws <see cref="FormatException"/> on the first problem (v0).
    /// </summary>
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public Parser(List<Token> pTokens)
        {
            _tokens = pTokens;
        }

        public DialogueGraph Parse()
        {
            string? lStartId = null;
            Dictionary<string, DialogueNode> lNodes = new Dictionary<string, DialogueNode>();

            while (Peek().Type != TokenType.EndOfFile)
            {
                Token lHead = Peek();

                if (lHead.Type == TokenType.Hash)
                {
                    Next();
                    Token lDirective = Expect(TokenType.Identifier, "directive name after '#'");
                    if (!lDirective.Value.Equals("start", StringComparison.OrdinalIgnoreCase))
                        throw Error($"Unknown directive '#{lDirective.Value}'", lDirective);
                    if (lStartId != null)
                        throw Error("Duplicate '#start' directive", lDirective);

                    lStartId = Expect(TokenType.Identifier, "node id after '#start'").Value;
                    continue;
                }

                if (lHead.Type == TokenType.KeywordNode)
                {
                    DialogueNode lNode = ParseNodeDeclaration();
                    if (lNodes.ContainsKey(lNode.Id))
                        throw Error($"Duplicate node '{lNode.Id}'", lHead);
                    lNodes.Add(lNode.Id, lNode);
                    continue;
                }

                throw Error($"Expected 'node' or '#start', got {Describe(lHead)}", lHead);
            }

            if (lStartId == null)
                throw new FormatException("Missing '#start' directive.");
            if (!lNodes.ContainsKey(lStartId))
                throw new FormatException($"'#start {lStartId}' points to a node that does not exist.");

            ValidateTargets(lNodes);

            return new DialogueGraph(lStartId, lNodes);
        }

        // --- Node level ---

        private List<IDialogueContent> ParseContentUntilEndBrace(string pOwnerName)
        {
            List<IDialogueContent> lDialogueContent = new List<IDialogueContent>();

            while (true)
            {
                Token lHead = Peek();

                if (lHead.Type == TokenType.RBrace)
                {
                    Next();
                    return lDialogueContent;
                }

                if (lHead.Type == TokenType.EndOfFile)
                {
                    throw Error($"Unterminated block in {pOwnerName} (missing '}}')", lHead);
                }

                lDialogueContent.Add(ParseContent());
            }
        }

        private DialogueNode ParseNodeDeclaration()
        {
            Next(); // 'node'
            Token lName = Expect(TokenType.Identifier, "node name");
            Expect(TokenType.LBrace, "'{' after node name");

            DialogueNode lNode = new DialogueNode(lName.Value);
            lNode.Contents.AddRange(ParseContentUntilEndBrace($"node id : {lNode.Id}"));
            return lNode;
        }

        // Dispatch on the head token. A keyword followed by ':' is a speaker line, not an
        // instruction ("jump : ...;" speaks, "jump Cible;" jumps) — the colon disambiguates.
        private IDialogueContent ParseContent()
        {
            Token lHead = Peek();

            if (lHead.Type == TokenType.KeywordEnd && PeekNext().Type == TokenType.Semicolon)
            {
                Next();
                Next();
                return new EndContent();
            }

            if (lHead.Type == TokenType.KeywordJump && PeekNext().Type == TokenType.Identifier)
            {
                Next();
                Token lTarget = Next();
                Expect(TokenType.Semicolon, "';' after jump target");
                return new JumpContent(lTarget.Value);
            }

            if (lHead.Type == TokenType.KeywordChoice && PeekNext().Type == TokenType.String)
                return ParseChoiceGroup();

            return ParseLine();
        }

        // Consecutive 'choice' statements form one menu (a single ChoiceContent).
        private ChoiceContent ParseChoiceGroup()
        {
            ChoiceContent lChoice = new ChoiceContent();

            while (Peek().Type == TokenType.KeywordChoice && PeekNext().Type == TokenType.String)
            {
                Next(); // 'choice'
                Token lChoiceName = Expect(TokenType.String, "choice label in quotes");

                Token lAfterLabel = Peek();
                switch (lAfterLabel.Type) {
                    case TokenType.LBrace: // block form: choice "Label" { ... } — falls through on block end
                        Next(); // consume '{'
                        List<IDialogueContent> lBlock = ParseContentUntilEndBrace($"choice \"{lChoiceName.Value}\"");
                        lChoice.Options.Add(new ChoiceOption(lChoiceName.Value,  lBlock));
                        break;
                    case TokenType.KeywordJump: // jump form: choice "Label" jump Target ;
                        Next(); // consume 'jump'
                        Token lTarget = Expect(TokenType.Identifier, "jump target after 'jump'");
                        Expect(TokenType.Semicolon, "';' after choice's jump target");
                        lChoice.Options.Add(new ChoiceOption(lChoiceName.Value, lTarget.Value));
                        break;
                    default:
                        throw Error($"Expected 'jump <target>;' or '{{ ... }}' after choice label, got {Describe(lAfterLabel)}", lAfterLabel);
                }
            }

            return lChoice;
        }

        // Spoken line: (words)* ':' Text ';'. An empty speaker = anonymous narration.
        private LineContent ParseLine()
        {
            StringBuilder lSpeaker = new StringBuilder();

            while (Peek().Type != TokenType.Colon)
            {
                Token lWord = Peek();
                bool lIsWord = lWord.Type == TokenType.Identifier
                            || lWord.Type == TokenType.KeywordNode
                            || lWord.Type == TokenType.KeywordJump
                            || lWord.Type == TokenType.KeywordChoice
                            || lWord.Type == TokenType.KeywordEnd;
                if (!lIsWord)
                    throw Error($"Expected a line, an instruction or '}}', got {Describe(lWord)}", lWord);

                if (lSpeaker.Length > 0)
                    lSpeaker.Append(' ');
                lSpeaker.Append(lWord.Value);
                Next();
            }

            Next(); // ':'
            Token lText = Expect(TokenType.Text, "line text after ':'");
            Expect(TokenType.Semicolon, "';' after line text");

            string lSpeakerName = lSpeaker.ToString().Trim();
            string lRawText = lText.Value.Trim();
            ParsedLine lLine;
            try
            {
                lLine = MarkupParser.Parse(lRawText);
            }
            catch (FormatException lEx)
            {
                throw Error(lEx.Message.TrimEnd('.'), lText);
            }

            return new LineContent(lSpeakerName.Length == 0 ? null : lSpeakerName, lLine, lRawText);
        }

        // --- Validation ---

        // Every jump and choice target must resolve to a declared node (fail at parse time,
        // not in-game). Unreachable nodes are allowed — they are just unused.
        private static void ValidateTargets(Dictionary<string, DialogueNode> pNodes)
        {
            foreach (DialogueNode lNode in pNodes.Values)
                ValidateContents(lNode.Contents, pNodes, lNode.Id);
        }

        // Recursive: inline blocks contain the same content kinds, including nested choices.
        private static void ValidateContents(List<IDialogueContent> pContents, Dictionary<string, DialogueNode> pNodes, string pNodeId)
        {
            foreach (IDialogueContent lContent in pContents)
            {
                if (lContent is JumpContent lJump && !pNodes.ContainsKey(lJump.TargetId))
                    throw new FormatException($"Node '{pNodeId}' jumps to unknown node '{lJump.TargetId}'.");

                if (lContent is ChoiceContent lChoice)
                {
                    foreach (ChoiceOption lOption in lChoice.Options)
                    {
                        if (lOption.InlineContent != null)
                            ValidateContents(lOption.InlineContent, pNodes, pNodeId);   // descend dans le bloc
                        else if (lOption.TargetId == null || !pNodes.ContainsKey(lOption.TargetId))
                            throw new FormatException($"Choice '{lOption.Text}' in node '{pNodeId}' jumps to unknown node '{lOption.TargetId}'.");
                    }
                }
            }
        }

        // --- Token stream helpers ---

        private Token Peek() => _tokens[_pos];
        private Token PeekNext() => _pos + 1 < _tokens.Count ? _tokens[_pos + 1] : _tokens[_tokens.Count - 1];
        /// <summary> Get the actual token and then advance the head </summary>
        /// <returns> The actual token </returns>
        private Token Next() => _tokens[_pos++];

        private Token Expect(TokenType pType, string pWhat)
        {
            Token lToken = Peek();
            if (lToken.Type != pType)
                throw Error($"Expected {pWhat}, got {Describe(lToken)}", lToken);
            return Next();
        }

        private static string Describe(Token pToken)
        {
            return pToken.Type == TokenType.EndOfFile ? "end of file" : $"'{pToken.Value}'";
        }

        private static FormatException Error(string pMessage, Token pAt)
        {
            return new FormatException($"{pMessage} (line {pAt.Line}, column {pAt.Column}).");
        }
    }
}
