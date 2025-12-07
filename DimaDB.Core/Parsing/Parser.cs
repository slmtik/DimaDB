using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Interfaces;
using DimaDB.Core.Lexing;
using System.Collections.Immutable;

namespace DimaDB.Core.Parsing;

public class Parser(ErrorReporter? errorReporter) : IParser
{
    private readonly List<Token> _tokens = [];
    private int _current = 0;

    private bool IsAtEnd => Peek.TokenType == TokenType.EoF;
    private Token Peek => _tokens[_current];
    private Token Previous => _tokens[_current - 1];

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

        throw new ParserException(Peek, message);
    }

    public IList<Statement> Parse(IList<Token> tokens)
    {
        _tokens.Clear();
        _current = 0;
        _tokens.AddRange(tokens);

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

        throw new ParserException(Peek, "Expect statement");
    }

    private Statement.CreateTable CreateTableStatementRule()
    {
        var tableRef = TableIdentirfierRule();

        Consume(TokenType.LeftParenthesis, "Expect '(' before column definitions");

        var columnDefinitions = ImmutableArray.CreateBuilder<Expression.ColumnDefinition>();
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
        return new Identifier(tableToken.Lexeme, tableToken.Quoted);
    }

    private Expression.ColumnDefinition ColumnDefinitionRule()
    {
        var identifierToken = Consume(TokenType.Identifier, "Expect column identifier in the column definition");
        var columnIdentifier = new Identifier(identifierToken.Lexeme, identifierToken.Quoted);

        var type = TypeNameRule();

        return new Expression.ColumnDefinition(columnIdentifier, type);
    }

    private TypeName TypeNameRule()
    {
        if (Match(TokenType.Int, TokenType.BigInt, TokenType.Text))
        {
            return new TypeName(Previous.Lexeme.ToUpper());
        }

        throw new ParserException(Peek, "Unsupported type");
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
            return new Expression.NumberLiteral((double)Previous.Literal!);
        }

        if (Match(TokenType.StringLiteral))
        {
            return new Expression.StringLiteral((string)Previous.Literal!);
        }

        throw new ParserException(IsAtEnd ? Previous : Peek, "Expect value expression");
    }

    private Statement.Select SelectStatementRule()
    {
        var selectClause = SelectClauseRule();

        Expression.FromClause? fromClause = null;

        if (!Check(TokenType.Semicolon) && Consume(TokenType.From, "Expect 'FROM' after SELECT clause") is not null)
        {
            fromClause = FromClauseRule();
        }

        Expression? whereClause = null;

        if (Match(TokenType.Where))
        {
            whereClause = WhereClauseRule();
        }

        long? limit = null;
        if (Match(TokenType.Limit))
        {
            var limitToken = Consume(TokenType.NumberLiteral, "Expect number after LIMIT");
            var limitValue = (double)limitToken.Literal!;

            if (limitValue < 0 || limitValue != Math.Floor(limitValue))
            {
                throw new ParserException(limitToken, "LIMIT must be a non-negative integer");
            }

            limit = (long)limitValue;
        }

        Consume(TokenType.Semicolon, "Expect ';' after SELECT statement");

        return new Statement.Select(selectClause, fromClause, whereClause, limit);
    }

    private ImmutableArray<Expression.SelectItem> SelectClauseRule()
    {
        var selectClause = ImmutableArray.CreateBuilder<Expression.SelectItem>();
        do
        {
            selectClause.Add(SelectItemRule());
        } while (Match(TokenType.Comma));

        return selectClause.ToImmutable();
    }

    private Expression.SelectItem SelectItemRule()
    {
        var expression = SelectItemExpressionRule();
        var alias = AliasRule();
        return new Expression.SelectItem(expression, alias);
    }

    private Expression SelectItemExpressionRule()
    {
        if (Match(TokenType.Star))
        {
            return new Expression.Star();
        }

        if (Check(TokenType.Identifier))
        {
            var firstToken = Advance();
            var firstIdentifier = new Identifier(firstToken.Lexeme, firstToken.Quoted);

            if (Match(TokenType.Dot))
            {
                if (Match(TokenType.Star))
                {
                    return new Expression.QualifiedStar(firstIdentifier);
                }

                var secondToken = Consume(TokenType.Identifier, "Expect identifier after .");
                var secondIdentifier = new Identifier(secondToken.Lexeme, secondToken.Quoted);

                return new Expression.ColumnReference(firstIdentifier, secondIdentifier);
            }
                
            return new Expression.ColumnReference(null, firstIdentifier);
        }
            
        return ExpressionRule();
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

            return new Identifier(aliasToken.Lexeme, aliasToken.Quoted);
        }

        return null;
    }

    private Expression.FromClause FromClauseRule()
    {
        var tableRef = TableReferenceRule();
        return new Expression.FromClause(tableRef);
    }

    private Expression.TableReference TableReferenceRule()
    {
        var tableToken = Consume(TokenType.Identifier, "Expect table name");
        var tableIdentifier = new Identifier(tableToken.Lexeme, tableToken.Quoted);
        var alias = AliasRule();

        return new Expression.TableReference(tableIdentifier, alias);
    }

    private Expression WhereClauseRule()
    {
        return ExpressionRule();
    }

    private Expression ExpressionRule() => OrRule();

    private Expression OrRule()
    {
        Expression expression = AndRule();

        while (Match(TokenType.Or))
        {
            Token operatorToken = Previous;
            Expression right = AndRule();
            expression = new Expression.BinaryOperation(expression, operatorToken, right);
        }

        return expression;
    }

    private Expression AndRule()
    {
        Expression expression = EqualityRule();

        while (Match(TokenType.And))
        {
            Token operatorToken = Previous;
            Expression right = EqualityRule();
            expression = new Expression.BinaryOperation(expression, operatorToken, right);
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
            expression = new Expression.BinaryOperation(expression, operatorToken, right);
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
            expression = new Expression.BinaryOperation(expression, operatorToken, right);
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
            expression = new Expression.BinaryOperation(expression, operatorToken, right);
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
            expression = new Expression.BinaryOperation(expression, operatorToken, right);
        }

        return expression;
    }

    private Expression UnaryRule()
    {
        if (Match(TokenType.Minus, TokenType.Not))
        {
            Token operatorToken = Previous;
            Expression right = PrimaryRule();
            return new Expression.UnaryOperation(operatorToken, right);
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
            return new Expression.NumberLiteral((double)Previous.Literal!);
        }

        if (Match(TokenType.StringLiteral))
        {
            return new Expression.StringLiteral((string)Previous.Literal!);
        }

        if (Match(TokenType.LeftParenthesis))
        {
            Expression expr = ExpressionRule();
            Consume(TokenType.RightParenthesis, "Expect ')' after expression");
            return new Expression.Parenthesized(expr);
        }

        if (Match(TokenType.Identifier))
        {
            var identifier = new Identifier(Previous.Lexeme, Previous.Quoted);

            if (Match(TokenType.Dot))
            {
                var columnToken = Consume(TokenType.Identifier, "Expect column name after '.'");
                var columnIdentifier = new Identifier(columnToken.Lexeme, columnToken.Quoted);

                return new Expression.ColumnReference(identifier, columnIdentifier);
            }

            return new Expression.ColumnReference(null, identifier);
        }

        throw new ParserException(IsAtEnd ? Previous : Peek, "Expect expression");
    }
}
