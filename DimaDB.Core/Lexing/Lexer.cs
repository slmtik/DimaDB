using DimaDB.Core.ErrorHandling;

namespace DimaDB.Core.Lexing;

public class Lexer(ErrorReporter? errorReporter) : ILexer
{
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

    private List<Token> _tokens = null!;
    private string _source = null!;
    private int _startPosition = 0;
    private int _currentPosition = 0;
    private int _line = 1;

    private bool IsAtEnd => _currentPosition >= _source.Length;
    private char Peek => IsAtEnd ? '\0' : _source[_currentPosition];
    private char PeekNext => _currentPosition + 1 >= _source.Length ? '\0' : _source[_currentPosition + 1];

    public Lexer() : this(null) { }

    public IList<Token> Tokenize(string source)
    {
        _source = source;
        _tokens = [];
        _startPosition = 0;
        _currentPosition = 0;
        _line = 1;

        while (!IsAtEnd)
        {
            _startPosition = _currentPosition;
            NextToken();
        }

        _tokens.Add(new Token(TokenType.EoF, _currentPosition, _source.Length, _line));

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
        _tokens.Add(new Token(type, _startPosition, _currentPosition - _startPosition, _line));
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
        while (!IsAtEnd)
        {
            if (Peek == '\n')
            {
                _line++;
            }

            if (Peek == '\'')
            {
                if (PeekNext != '\'')
                {
                    break;
                }

                Advance(); // Consume the escaped '
            }

            Advance();
        }

        if (IsAtEnd)
        {
            errorReporter?.Report(new LexerException(_line, _currentPosition, "Unterminated string"));
            return;
        }

        Advance(); // Consume the closing '

        AddToken(TokenType.StringLiteral);
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

        AddToken(TokenType.NumberLiteral);
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

        AddToken(TokenType.Identifier);

        if (IsAtEnd)
        {
            errorReporter?.Report(new LexerException(_line, _currentPosition, "Unterminated quoted identifier"));
        }
    }
}
