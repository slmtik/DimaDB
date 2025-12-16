using DimaDB.Core.AST;
using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Lexing;
using System.Collections.Immutable;
using System.Globalization;

namespace DimaDB.Core.Parsing;

public class Parser(ErrorReporter? errorReporter) : IParser
{
    private IList<Token> _tokens = null!;
    private string _source = null!;
    private int _current = 0;

    private bool IsAtEnd => Peek.TokenType == TokenType.EoF;
    private Token Peek => _tokens[_current];
    private Token Previous => _tokens[_current - 1];

    public Parser() : this(null) { }

    public IList<Statement> Parse(string source, IList<Token> tokens)
    {
        _source = source;
        _current = 0;
        _tokens = tokens;

        var statements = new List<Statement>();

        while (!IsAtEnd)
        {
            while (Match(TokenType.Semicolon)) { }

            if (IsAtEnd)
                break;

            try
            {
                statements.Add(StatementRule());

            }
            catch (ParserException ex)
            {
                errorReporter?.Report(ex);
                Synchronize();
            }
        }

        return statements;
    }

    private void Synchronize()
    {
        Advance();

        while (!IsAtEnd)
        {
            if (Previous.TokenType == TokenType.Semicolon)
            {
                return;
            }

            switch (Peek.TokenType)
            {
                case TokenType.Create:
                case TokenType.Insert:
                case TokenType.Select:
                    return;
            }

            Advance();
        }
    }

    private bool Match(params TokenType[] types)
    {
        foreach (TokenType type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    private bool Check(TokenType type) => !IsAtEnd && Peek.TokenType == type;

    private Token Advance()
    {
        if (!IsAtEnd)
        {
            _current++;
        }

        return Previous;
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type))
        {
            return Advance();
        }

        throw new ParserException(_source, Peek, message);
    }

    private BinaryOperator MapBinaryOperator(Token token)
    {
        return token.TokenType switch
        {
            TokenType.Plus => BinaryOperator.Add,
            TokenType.Minus => BinaryOperator.Subtract,
            TokenType.Star => BinaryOperator.Multiply,
            TokenType.Slash => BinaryOperator.Divide,
            TokenType.Equal => BinaryOperator.Equal,
            TokenType.NotEqual => BinaryOperator.NotEqual,
            TokenType.Less => BinaryOperator.Less,
            TokenType.LessEqual => BinaryOperator.LessEqual,
            TokenType.Greater => BinaryOperator.Greater,
            TokenType.GreaterEqual => BinaryOperator.GreaterEqual,
            TokenType.And => BinaryOperator.And,
            TokenType.Or => BinaryOperator.Or,
            _ => throw new ParserException(_source, Previous, "Invalid binary operator")
        };
    }

    private UnaryOperator MapUnaryOperator(Token token)
    {
        return token.TokenType switch
        {
            TokenType.Minus => UnaryOperator.Negate,
            TokenType.Not => UnaryOperator.Not,
            _ => throw new ParserException(_source, Previous, "Invalid unary operator")
        };
    }

    private Identifier ParseIdentifier(Token identifierToken)
    {
        var identifierSpan = _source.AsSpan(identifierToken.Start, identifierToken.Length);
        if (identifierSpan[0] == '"')
        {
            return new(identifierSpan[1..^1].ToString(), true);
        }
        return new(identifierSpan.ToString(), false);
    }

    private Statement StatementRule()
    { 

        if (Match(TokenType.Select))
        {
            return SelectStatementRule();
        }

        if (Match(TokenType.Create) && Match(TokenType.Table))
        {
            return CreateTableStatementRule();
        }

        if (Match(TokenType.Insert) && Match(TokenType.Into))
        {
            return InsertIntoStatementRule();
        }

        throw new ParserException(_source, Peek, "Expect statement");
    }

    private Statement.CreateTable CreateTableStatementRule()
    {
        var tableRef = TableIdentirfierRule();

        Consume(TokenType.LeftParenthesis, "Expect '(' before column definitions");

        var columnDefinitions = ImmutableArray.CreateBuilder<Component.ColumnDefinition>();
        do
        {
            columnDefinitions.Add(ColumnDefinitionRule());
        } while (Match(TokenType.Comma));

        Consume(TokenType.RightParenthesis, "Expect ')' after column definitions");

        Consume(TokenType.Semicolon, "Expect ';' after CREATE TABLE statement");

        return new Statement.CreateTable(tableRef, columnDefinitions.ToImmutable());
    }

    private Identifier TableIdentirfierRule()
    {
        var tableToken = Consume(TokenType.Identifier, "Expect table name");
        return ParseIdentifier(tableToken);
    }

    private Component.ColumnDefinition ColumnDefinitionRule()
    {
        var identifierToken = Consume(TokenType.Identifier, "Expect column identifier in the column definition");
        var columnIdentifier = ParseIdentifier(identifierToken);

        var type = TypeNameRule();

        return new Component.ColumnDefinition(columnIdentifier, type);
    }

    private TypeName TypeNameRule()
    {
        if (Match(TokenType.Int, TokenType.BigInt, TokenType.Text))
        {
            return new TypeName(_source.AsSpan(Previous.Start, Previous.Length).ToString().ToUpper());
        }

        throw new ParserException(_source, Peek, "Unsupported type");
    }

    private Statement.InsertInto InsertIntoStatementRule()
    {
        var tableRef = TableIdentirfierRule();

        Match(TokenType.Values);

        Consume(TokenType.LeftParenthesis, "Expect '(' before values");

        var values = ImmutableArray.CreateBuilder<Expression>();
        do
        {
            values.Add(ValueExpressionRule());
        } while (Match(TokenType.Comma));

        Consume(TokenType.RightParenthesis, "Expect ')' after values");

        Consume(TokenType.Semicolon, "Expect ';' after INSERT INTO statement");

        return new Statement.InsertInto(tableRef, values.ToImmutable());
    }

    private Expression ValueExpressionRule()
    {
        if (Match(TokenType.False, TokenType.True))
        {
            return new Expression.BooleanLiteral(Previous.TokenType == TokenType.True);
        }

        if (Match(TokenType.Null))
        {
            return new Expression.NullLiteral();
        }

        if (Match(TokenType.NumberLiteral))
        {
            return ParseNumberLiteral();
        }

        if (Match(TokenType.StringLiteral))
        {
            return ParseStringLiteral();
        }

        throw new ParserException(_source, IsAtEnd ? Previous : Peek, "Expect value expression");
    }

    private Statement.Select SelectStatementRule()
    {
        var selectClause = SelectClauseRule();

        Clause.FromClause? fromClause = null;

        if (!Check(TokenType.Semicolon) && Consume(TokenType.From, "Expect 'FROM' after SELECT clause") is not null)
        {
            fromClause = FromClauseRule();
        }

        Clause.WhereClause? whereClause = null;

        if (Match(TokenType.Where))
        {
            whereClause = WhereClauseRule();
        }

        long? limit = null;
        if (Match(TokenType.Limit))
        {
            var limitToken = Consume(TokenType.NumberLiteral, "Expect number after LIMIT");
            var limitValue = double.Parse(_source.AsSpan(limitToken.Start, limitToken.Length));

            if (limitValue < 0 || limitValue != Math.Floor(limitValue))
            {
                throw new ParserException(_source, limitToken, "LIMIT must be a non-negative integer");
            }

            limit = (long)limitValue;
        }

        Consume(TokenType.Semicolon, "Expect ';' after SELECT statement");

        return new Statement.Select(selectClause, fromClause, whereClause, limit);
    }

    private ImmutableArray<Component.SelectItem> SelectClauseRule()
    {
        var selectClause = ImmutableArray.CreateBuilder<Component.SelectItem>();
        do
        {
            selectClause.Add(SelectItemRule());
        } while (Match(TokenType.Comma));

        return selectClause.ToImmutable();
    }

    private Component.SelectItem SelectItemRule()
    {
        if (Match(TokenType.Star))
        {
            return new Component.Star();
        }

        if (Check(TokenType.Identifier))
        {
            var firstToken = Advance();
            var firstIdentifier = ParseIdentifier(firstToken);

            if (Match(TokenType.Dot))
            {
                if (Match(TokenType.Star))
                {
                    return new Component.QualifiedStar(firstIdentifier);
                }

                var secondToken = Consume(TokenType.Identifier, "Expect identifier after .");
                var secondIdentifier = ParseIdentifier(secondToken);
                var qualifiedColumnReference = new Expression.ColumnReference(firstIdentifier, secondIdentifier);

                return new Component.ExpressionItem(qualifiedColumnReference, AliasRule());
            }

            var columnReference = new Expression.ColumnReference(null, firstIdentifier);
            return new Component.ExpressionItem(columnReference, AliasRule());
        }

        return new Component.ExpressionItem(ExpressionRule(), AliasRule());
    }

    private Identifier? AliasRule()
    {
        if (Check(TokenType.As) || Check(TokenType.Identifier))
        {
            Token aliasToken;
            if (Match(TokenType.As))
            {
                aliasToken = Consume(TokenType.Identifier, "Expect identifier after AS");
            }
            else
            {
                aliasToken = Advance();
            }

            return ParseIdentifier(aliasToken);
        }

        return null;
    }

    private Clause.FromClause FromClauseRule()
    {
        var tableRef = TableReferenceRule();
        return new Clause.FromClause(tableRef);
    }

    private Component.TableReference TableReferenceRule()
    {
        var tableToken = Consume(TokenType.Identifier, "Expect table name");
        var tableIdentifier = ParseIdentifier(tableToken);
        var alias = AliasRule();

        return new Component.TableReference(tableIdentifier, alias);
    }

    private Clause.WhereClause WhereClauseRule()
    {
        return new Clause.WhereClause(ExpressionRule());
    }

    private Expression ExpressionRule() => OrRule();

    private Expression OrRule()
    {
        Expression expression = AndRule();

        while (Match(TokenType.Or))
        {
            Expression right = AndRule();
            expression = new Expression.BinaryOperation(expression, BinaryOperator.Or, right);
        }

        return expression;
    }

    private Expression AndRule()
    {
        Expression expression = EqualityRule();

        while (Match(TokenType.And))
        {
            Expression right = EqualityRule();
            expression = new Expression.BinaryOperation(expression, BinaryOperator.And, right);
        }

        return expression;
    }

    private Expression EqualityRule()
    {
        Expression expression = ComparisonRule();

        while (Match(TokenType.Equal, TokenType.NotEqual))
        {
            Token operatorToken = Previous;
            Expression right = ComparisonRule();
            expression = new Expression.BinaryOperation(expression, MapBinaryOperator(operatorToken), right);
        }

        return expression;
    }

    private Expression ComparisonRule()
    {
        Expression expression = TermRule();

        while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
        {
            Token operatorToken = Previous;
            Expression right = TermRule();
            expression = new Expression.BinaryOperation(expression, MapBinaryOperator(operatorToken), right);
        }

        return expression;
    }

    private Expression TermRule()
    {
        Expression expression = FactorRule();

        while (Match(TokenType.Plus, TokenType.Minus, TokenType.Concat))
        {
            Token operatorToken = Previous;
            Expression right = FactorRule();
            expression = new Expression.BinaryOperation(expression, MapBinaryOperator(operatorToken), right);
        }

        return expression;
    }

    private Expression FactorRule()
    {
        Expression expression = UnaryRule();

        while (Match(TokenType.Star, TokenType.Slash))
        {
            Token operatorToken = Previous;
            Expression right = UnaryRule();
            expression = new Expression.BinaryOperation(expression, MapBinaryOperator(operatorToken), right);
        }

        return expression;
    }

    private Expression UnaryRule()
    {
        if (Match(TokenType.Minus, TokenType.Not))
        {
            Token operatorToken = Previous;
            Expression right = PrimaryRule();
            return new Expression.UnaryOperation(MapUnaryOperator(operatorToken), right);
        }

        return PrimaryRule();
    }

    private Expression PrimaryRule()
    {
        if (Match(TokenType.False, TokenType.True))
        {
            return new Expression.BooleanLiteral(Previous.TokenType == TokenType.True);
        }

        if (Match(TokenType.Null))
        {
            return new Expression.NullLiteral();
        }

        if (Match(TokenType.NumberLiteral))
        {
            return ParseNumberLiteral();
        }

        if (Match(TokenType.StringLiteral))
        {
            return ParseStringLiteral();
        }

        if (Match(TokenType.LeftParenthesis))
        {
            Expression expr = ExpressionRule();
            Consume(TokenType.RightParenthesis, "Expect ')' after expression");
            return new Expression.Parenthesized(expr);
        }

        if (Match(TokenType.Identifier))
        {
            var identifier = ParseIdentifier(Previous);

            if (Match(TokenType.Dot))
            {
                var columnToken = Consume(TokenType.Identifier, "Expect column name after '.'");
                var columnIdentifier = ParseIdentifier(columnToken);

                return new Expression.ColumnReference(identifier, columnIdentifier);
            }

            return new Expression.ColumnReference(null, identifier);
        }

        throw new ParserException(_source, IsAtEnd ? Previous : Peek, "Expect expression");
    }

    private Expression.NumberLiteral ParseNumberLiteral()
    {
        var numberSpan = _source.AsSpan(Previous.Start, Previous.Length);
        if (double.TryParse(numberSpan, NumberStyles.Float,  CultureInfo.InvariantCulture, out double doubleValue))
        {
            return new(doubleValue);
        }

        throw new ParserException(_source, Previous, "Invalid decimal literal");
    }

    private Expression.StringLiteral ParseStringLiteral()
    {
        return new Expression.StringLiteral(_source.AsSpan(Previous.Start + 1, Previous.Length - 2).ToString().Replace("''", "'"));
    }
}
