namespace DimaDB.Lexing;

public record Token(TokenType TokenType, int Start, int Length, int Line);
