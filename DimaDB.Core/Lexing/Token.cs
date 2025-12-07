namespace DimaDB.Core.Lexing;

public record Token(TokenType TokenType, string Lexeme, object? Literal, int Line, int Position, bool Quoted);
