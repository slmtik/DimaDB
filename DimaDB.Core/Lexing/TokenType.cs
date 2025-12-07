namespace DimaDB.Core.Lexing;

public enum TokenType
{
    // Keywords
    Select, 
    From, 
    Where, 
    Create, 
    Table, 
    Insert, 
    Into,
    Values, 
    Limit, 
    And, 
    Or, 
    Null,
    Not,
    False, 
    True,
    As,

    //Types
    Int, 
    BigInt,
    Text,

    // Literals
    Identifier,
    NumberLiteral,
    StringLiteral,

    // Operators
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    Plus,
    Minus,
    Slash,
    Concat,

    // Separators
    LeftParenthesis,
    RightParenthesis,
    Comma,
    Semicolon,
    Star,
    Dot,

    EoF
}
