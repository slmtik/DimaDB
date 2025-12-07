# DimaDB

## Repository Layout

* **DimaDB.Core**
  Core library containing the lexer, parser, AST definitions, error handling, and runtime primitives.

* **DimaDB.Cli**
  Command-line client that executes SQL scripts or starts an interactive REPL.

* **DimaDB.Repl**
  REPL wrapper used by the CLI for interactive sessions.

* **DimaDB.SourceGenerator**
  Roslyn source generator that produces AST boilerplate from annotations.

* **DimaDB.Core.Tests**
  xUnit test project covering lexer and parser behavior.

---

## Requirements

* .NET 8 SDK
* C# 12 (LangVersion 12.0)
* Visual Studio 2022, Rider, or VS Code (optional)

---

## Build

From the repository root:

```bash
dotnet build
```

---

## CLI

Run a single SQL query from the command line:

```bash
dotnet run --project DimaDB.Cli --query "<SQL>"
```

The CLI accepts SQL commands terminated with a semicolon (`;`).

---

## REPL

Start the interactive REPL:

```bash
dotnet run --project DimaDB.Cli
```

### REPL Commands

* Enter SQL statements terminated with `;`
* Type `exit` to quit the REPL

### Example

```sql
CREATE TABLE users (id INT, name TEXT);
INSERT INTO users VALUES (1, 'Bob');
SELECT id, name FROM users WHERE id = 1;
```

---

## AST Debug Mode

Both the CLI and REPL support an **AST debug mode**, which prints the parsed Abstract Syntax Tree.

### Usage Examples

**Run a single query with AST output:**

```bash
dotnet run --project DimaDB.Cli --query "SELECT id, name FROM users WHERE id = 1;" --ast-debug
```

**Start the REPL with AST debug enabled:**

```bash
dotnet run --project DimaDB.Cli --ast-debug
```

---

## Error Codes

The CLI and REPL use the following process exit codes to indicate failure types:

| Code  | Description                                                                    |
| ----- | ------------------------------------------------------------------------------ |
| **1** | Invalid arguments passed to the program (e.g. unknown options, missing values) |
| **2** | Lexer error (invalid or unexpected tokens in the SQL input)                    |
| **3** | Parser error (syntactically invalid SQL)                                       |

---

## Testing

Run the unit tests using the .NET test runner:

```bash
dotnet test
```

---

## EBNF Grammar

```bash
program = { ";" } , { statement , { ";" } } ;

statement = select_statement | create_table_statement | insert_into_statement ;

create_table_statement = "CREATE" , "TABLE" , table_identifier , "(" , column_definition , { "," , column_definition } , ")" , ";" ;

column_definition = identifier , type_name ;

type_name = "INT" | "BIGINT" | "TEXT" ;

insert_into_statement = "INSERT" , "INTO" , table_identifier , [ "VALUES" ] , "(" , value_expression , { "," , value_expression } , ")" , ";" ;

value_expression = boolean_literal | null_literal | number_literal | string_literal ;

select_statement = "SELECT" , select_clause , [ "FROM" , from_clause ] , [ "WHERE" , where_clause ] , [ "LIMIT" , number_literal ] , ";" ;

select_clause = select_item , { "," , select_item } ;

select_item = select_item_expression , [ alias ] ;

select_item_expression = "*" | identifier , "." , "*" | identifier , "." , identifier | identifier | expression ;

alias = [ "AS" ] , identifier ;

from_clause = table_reference ;

table_reference = identifier , [ alias ] ;

where_clause = expression ;

expression = or_expression ;

or_expression = and_expression , { "OR" , and_expression } ;

and_expression = equality_expression , { "AND" , equality_expression } ;

equality_expression = comparison_expression , { ( "=" | "!=" ) , comparison_expression } ;

comparison_expression = term_expression , { ( ">" | ">=" | "<" | "<=" ) , term_expression } ;

term_expression = factor_expression , { ( "+" | "-" | "||" ) , factor_expression } ;

factor_expression = unary_expression , { ( "*" | "/" ) , unary_expression } ;

unary_expression = ( "-" | "NOT" ) , primary_expression | primary_expression ;

primary_expression = boolean_literal | null_literal | number_literal | string_literal | "(" , expression , ")" | identifier , "." , identifier | identifier ;

table_identifier = identifier ;

identifier = ? quoted or unquoted identifier ? ;

boolean_literal = "TRUE" | "FALSE" ;

null_literal = "NULL" ;

number_literal = ? numeric literal ? ;

string_literal = ? string literal ? ;

```
