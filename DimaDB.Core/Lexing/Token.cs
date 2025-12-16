namespace DimaDB.Core.Lexing;

public record Token(TokenType TokenType, int Start, int Length, int Line);
