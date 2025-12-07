using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Interfaces;

namespace DimaDB.Core.Lexing;

public class Lexer(ErrorReporter? errorReporter) : ILexer
{
    private readonly List<Token> _tokens = [];
    private readonly Dictionary<string, TokenType> _keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        {"SELECT", TokenType.Select},
        {"FROM", TokenType.From},
        {"WHERE", TokenType.Where},
        {"CREATE", TokenType.Create},
        {"TABLE", TokenType.Table},
        {"INSERT", TokenType.Insert},
        {"INTO", TokenType.Into},
        {"VALUES", TokenType.Values},
        {"LIMIT", TokenType.Limit},
        {"AND", TokenType.And},
        {"OR", TokenType.Or},
        {"NULL", TokenType.Null},
        {"NOT", TokenType.Not},
        {"FALSE", TokenType.False},
        {"TRUE", TokenType.True},
        {"AS", TokenType.As},
        {"INT", TokenType.Int },
        {"BIGINT", TokenType.BigInt },
        {"TEXT", TokenType.Text },
    };

    private string _source = "";
    private int _startPosition = 0;
    private int _currentPosition = 0;
    private int _line = 1;

    private bool IsAtEnd => _currentPosition >= _source.Length;
    private char Peek => IsAtEnd ? '\0' : _source[_currentPosition];
    private char PeekNext => _currentPosition + 1 >= _source.Length ? '\0' : _source[_currentPosition + 1];

    public IList<Token> Tokenize(string source)
    {
        _source = source;
        _tokens.Clear();
        _startPosition = 0;
        _currentPosition = 0;
        _line = 1;

        while (!IsAtEnd)
        {
            _startPosition = _currentPosition;
            NextToken();
        }

        _tokens.Add(new Token(TokenType.EoF, "", null, _line, _currentPosition, false));

        return _tokens;
    }

    private void NextToken()
    {
        char c = Advance();
        switch (c)
        {
            case '(': 
                AddToken(TokenType.LeftParenthesis); 
                break;
            case ')': 
                AddToken(TokenType.RightParenthesis); 
                break;
            case ',': 
                AddToken(TokenType.Comma); 
                break;
            case '.':
                AddToken(TokenType.Dot);
                break;
            case ';': 
                AddToken(TokenType.Semicolon); 
                break;
            case '*': 
                AddToken(TokenType.Star); 
                break;
            case '=': 
                AddToken(TokenType.Equal); 
                break;
            case '+':
                AddToken(TokenType.Plus); 
                break;
            case '-':
                if (Match('-'))
                {
                    // It's a comment, skip to the end of the line
                    while (Peek != '\n' && !IsAtEnd)
                    {
                        Advance();
                    }
                }
                else
                {
                    AddToken(TokenType.Minus);
                }
                break;
            case '/':
                AddToken(TokenType.Slash);
                break;
            case '>': 
                AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater); 
                break;
            case '<': 
                AddToken(Match('=') ? TokenType.LessEqual : Match('>') ? TokenType.NotEqual : TokenType.Less); 
                break;
            case ' ' or '\r' or '\t':
                // Ignore whitespace
                break;
            case '\n': 
                _line++;
                break;
            case '\'': 
                AddStringToken(); 
                break;
            case var _ when IsDigit(c): 
                AddNumberToken(); 
                break;
            case var _ when IsAlpha(c):
                AddIdentifierToken();
                break;
            case '"':
                AddQuoteIdentifierToken();
                break;
            default: 
                if (c == '|' && Match('|'))
                {
                    AddToken(TokenType.Concat);
                    break;
                }

                errorReporter?.Report(new LexerException(_line, _currentPosition, $"Unexpected character: {c}"));
                break;
        }
    }

    private char Advance() => _source[_currentPosition++];

    private void AddToken(TokenType type)
    {
        AddToken(type, null);
    }

    private void AddToken(TokenType type, bool quoted)
    {
        AddToken(type, null, quoted);
    }

    private void AddToken(TokenType type, object? literal)
    {
        AddToken(type, literal, false);
    }

    private void AddToken(TokenType type, object? literal, bool quoted)
    {
        if (quoted)
        {
            _tokens.Add(new Token(type, _source[(_startPosition + 1)..(_currentPosition - 1)], literal, _line, _startPosition, quoted));
        }
        else
        {
            _tokens.Add(new Token(type, _source[_startPosition.._currentPosition], literal, _line, _startPosition, quoted));
        }
    }

    private bool Match(char expected)
    {
        if (IsAtEnd)
        {
            return false;
        }

        if (Peek != expected)
        {
            return false;
        }

        Advance();

        return true;
    }

    private void AddStringToken()
    {
        while (!IsAtEnd && Peek != '\'')
        {
            if (Peek == '\n')
            {
                _line++;
            }

            Advance();
        }

        if (IsAtEnd)
        {
            errorReporter?.Report(new LexerException(_line, _currentPosition, "Unterminated string"));
            return;
        }

        Advance(); // Consume the closing '

        AddToken(TokenType.StringLiteral, _source[(_startPosition + 1)..(_currentPosition - 1)]);
    }
    
    private static bool IsDigit(char c) => char.IsAsciiDigit(c);

    private void AddNumberToken()
    {
        while (IsDigit(Peek))
        {
            Advance();
        }

        if (Peek == '.' && IsDigit(PeekNext))
        {
            Advance(); // Consume the '.'

            while (IsDigit(Peek))
            {
                Advance();
            }
        }

        AddToken(TokenType.NumberLiteral, double.Parse(_source[_startPosition.._currentPosition]));
    }

    private static bool IsAlpha(char c) => char.IsLetter(c) || c == '_';

    private static bool IsAlphaNumeric(char c) => IsAlpha(c) || IsDigit(c);

    private void AddIdentifierToken()
    {
        while (IsAlphaNumeric(Peek))
        {
            Advance();
        }

        if (!_keywords.TryGetValue(_source[_startPosition.._currentPosition], out var tokenType))
        {
            tokenType = TokenType.Identifier;
        }

        AddToken(tokenType);
    }

    private void AddQuoteIdentifierToken()
    {
        while (!IsAtEnd && Peek != '"')
        {
            Advance();
        }

        Advance(); // Consume the closing "

        AddToken(TokenType.Identifier, true);

        if (IsAtEnd)
        {
            errorReporter?.Report(new LexerException(_line, _currentPosition, "Unterminated quoted identifier"));
        }
    }
}
