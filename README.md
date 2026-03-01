# DimaDB

## Repository Layout

* **DimaDB**
  Main project containing the lexer, parser, AST definitions, AST printer, error handling, runtime primitives, storage engine, query planner, query executor, CLI, and REPL.

* **DimaDB.SourceGenerator**
  Roslyn source generator that produces AST boilerplate from annotations.

* **DimaDB.Tests**
  Comprehensive unit tests for the query planner, query executor, and other components.

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
| **4** | Execution error                                                                |


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

select_item = "*" | identifier , "." , "*" | select_item_expression , [ alias ] ;

select_item_expression = identifier , "." , identifier | identifier | expression ;

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

---

## DimaDB Storage Engine

A heap-file storage engine with **slotted pages**, **overflow pages**, **free space management**, and **forwarding pointers**, supporting fixed and variable-length records, persistence, and CRUD operations.

### Quick Start

```csharp
using DimaDB.Core.Storage;
using DimaDB.Core.Storage.Types;

// Create engine
var engine = new StorageEngine("./data");

// Define schema
var columns = new[]
{
    new ColumnDefinition("id", ColumnType.Int, false),
    new ColumnDefinition("name", ColumnType.Text, true)
};

// Create table
engine.CreateTable("users", columns);

// Open table
var table = engine.OpenTable("users");

// Insert
var rid = table.Insert(new object?[] { 1, "Alice" });

// Get
var record = table.Get(rid);
Console.WriteLine($"Name: {record![1]}");

// Update
table.Update(rid, new object?[] { 1, "Bob" });

// Delete
table.Delete(rid);

// Scan
foreach (var (recordId, values) in table.Scan())
{
    Console.WriteLine($"{recordId}: {string.Join(", ", values)}");
}

// Cleanup
engine.Flush();
engine.Dispose();
```

### Features

- **Slotted Heap Pages:** 4096-byte pages with slot directory for variable-length records
- **Free Space Map:** Tracks available space on each page for efficient record placement
- **Forwarding Pointers:** Enable record relocation without RID changes (for future compaction)
- **Overflow Pages:** Automatic storage of TEXT values > 250 bytes across multiple pages
- **CRUD API:** Insert, Get, Update, Delete with RID-based access
- **Variable-Length Records:** UTF-8 TEXT with inline or overflow storage
- **Persistence:** Page writes to `.db` files
- **Schema Metadata:** JSON-based schema persistence for restart recovery
- **Buffer Pool:** LRU in-memory page caching (100 pages default)
- **Data Types:** INT, BOOL, TEXT
- **Null Support:** Nullable columns with null-bit markers

### Design Highlights

#### Slotted Page Layout

Each 4096-byte heap page contains:
- **Header:** PageId, NumSlots, FreeSpaceOffset (12 bytes)
- **Slot Directory:** Grows downward from offset 12 (each slot is 16 bytes)
- **Record Payload:** Grows upward from FreeSpaceOffset

**Slot Directory Entry (16 bytes):**
- PageId (4 bytes) - For forwarding pointers: target page
- SlotId (2 bytes) - For forwarding pointers: target slot
- Offset (4 bytes) - Record offset in page (or 0 if forwarding)
- Length (4 bytes) - Record length, or special marker (tombstone/forwarding)

**Slot Entry Markers:**
- `Length = 0xFFFFFFFF` → **Tombstone** (deleted record, space not reclaimed)
- `Length = 0xFFFFFFFE` → **Forwarding pointer** (record relocated to another page)
- `Length > 0` → Normal record (valid length in bytes)

#### Overflow Page Layout

For TEXT values > 250 bytes, data is stored in **overflow pages** linked together via redirecting pointers.

**Overflow Page Structure (4096 bytes):**
- **OverflowPageHeader:**
- **Overflow Data Payload** Grows downward from offset 12

**OverflowPageHeader Fields:**
- **PageId (4 bytes):** Page identifier in overflow storage
- **NextPageId (4 bytes):** Link to next overflow page (0 = end of chain)
- **DataLength (4 bytes):** Total original data length (stored on first page only)

**How Overflow Retrieval Works:**
1. Read heap record → get `OverflowPointer(FirstPageId, Length)`
2. Load `FirstPageId` overflow page
3. Read 4084 bytes of data from offset 12
4. Follow `NextPageId` chain until `NextPageId = 0`
5. Concatenate all data chunks, trim to original `Length`

**Overflow Data Size:**
- Header: 12 bytes
- Available for data: 4096 - 12 = **4084 bytes per page**
- For 15KB: Need ceil(15000 / 4084) = **4 pages** (48B header + 15KB data)

#### Free Space Management

The **Free Space Map (FSM)** tracks available space on each page:

**How it works:**
1. **Insert:** Query FSM to find a page with sufficient space; O(1) lookup instead of scanning all pages
2. **Delete:** Update FSM when a record is deleted, freeing space on the page
3. **Update:** Check if updated record fits; if not, delete from old page + insert on different page
4. **Optimization:** FSM enables efficient space allocation without full table scans

#### Forwarding Pointers

**Forwarding pointers** enable record relocation without changing the RID:

**How it works:**
1. **Original slot:** Contains forwarding pointer marker (0xFFFFFFFE)
2. **PageId & SlotId fields:** Store the new location
3. **Transparent to users:** RID remains stable; read operation transparently follows forward
4. **Use case:** Page compaction can move records without invalidating RIDs

#### Overflow Management

TEXT values are stored based on size:

| Size | Storage | Behavior |
|------|---------|----------|
| ≤ 250 bytes | Inline | Stored directly in heap page |
| > 250 bytes | Overflow | Stored in chain of overflow pages; redirecting pointer in heap |

#### RID Semantics

Records are uniquely identified by `(PageId, SlotId)`:
- **Stable:** Across in-place updates (record stays on same page)
- **Forwarding:** Can be relocated via forwarding pointers without RID change
- **Tombstone:** Deleted records marked as tombstones
- **Overflow:** Overflow pointers embedded in records allow TEXT relocation

#### Serialization

Records are serialized to binary:

[Null bits (1 byte per column)] + [Fixed-length fields|Variable-length fields(1 byte - overflow marker, if Inline Length + data else OverflowPointer)]

#### Persistence Model

1. **Schema:** JSON updates to `schemas.json`
2. **Heap Data:** page writes to `<TableName>.db`
3. **Overflow Data:** page writes to `<TableName>_overflow.db`
4. **Recovery:** Restart loads schemas; pages loaded on-demand from disk

### API Reference

#### StorageEngine

// Lifecycle
var engine = new StorageEngine(dataDir: "./data");

// Schema management
void CreateTable(string tableName, ColumnDefinition[] columns);
void DropTable(string tableName);
bool TableExists(string tableName);
IEnumerable<string> GetTableNames();

// Table access
TableHandle OpenTable(string tableName);

// Durability
void Flush();
void Dispose();

#### TableHandle

// CRUD
RecordId Insert(object?[] values);
object?[]? Get(RecordId rid);
bool Update(RecordId rid, object?[] newValues);
bool Delete(RecordId rid);

// Scan
IEnumerable<(RecordId, object?[])> Scan();

// Schema
Schema Schema { get; }

### Testing

Run the test suite:

dotnet test DimaDB.Core.Tests

#### Test Coverage

**Basic CRUD Tests:**
- Create table with schema
- Insert and retrieve records
- Update existing records
- Delete records
- Scan all records
- Null value handling
- All data types (INT, BOOL, TEXT)

**Persistence Tests:**
- Data survives engine restart
- Schemas persisted in `schemas.json`

**Free Space Management Tests:**
- FSM tracks available space correctly
- Records placed in pages with sufficient space
- Deleted space is reclaimed in FSM
- Page selection optimized via FSM
- FSM consistency after updates

**Heap Page Tests:**
- Slotted page structure integrity
- Slot directory growth and management
- Record payload serialization/deserialization
- Page boundary conditions
- Tombstone markers (0xFFFFFFFF)

**Overflow Page Tests:**
- Overflow page header (12 bytes) correct
- Overflow page chain linking via NextPageId
- Data split across multiple overflow pages
- DataLength field consistency
- Overflow data retrieval and reconstruction

**Forwarding Pointer Tests:**
- Forwarding marker (0xFFFFFFFE) correctly set
- Read operation transparently follows forwarding pointer
- RID remains stable after relocation
- Forward chains (pointer to pointer) handled correctly
- Forwarding pointers with overflow records

**Overflow TEXT Tests:**
- Small TEXT stored inline (≤ 250 bytes)
- Large TEXT stored in overflow pages (> 250 bytes)
- Very large TEXT spans multiple overflow pages (10KB+)
- Mixed inline and overflow columns in same record
- Overflow persists across restart
- Update: overflow → inline
- Scan with mixed records

**Storage Manager Tests:**
- Page I/O and buffering
- Buffer pool LRU eviction
- Dirty page tracking and persistence

**Edge Cases:**
- Empty strings
- Special UTF-8 characters (Unicode)
- Multiple pages and fragmentation
- Page-full behavior
- FSM consistency after updates
- Forwarding chains and cycles prevention
- Overflow chain corruption detection

### Demo

Run the interactive storage engine demo:

dotnet test DimaDB.Core.Tests --filter Demo_HeapAndOverflowPageIntegration --logger "console;verbosity=detailed"

Output includes:
- Table creation
- Mixed document sizes (small + large + very large)
- Retrieval and scanning
- Size change updates (small→large, large→small)
- Persistence and restart verification
- File structure inspection

### Implementation Details

#### Overflow Page Header Structure

Each overflow page header is **12 bytes**:

| Field | Size | Purpose |
|-------|------|---------|
| PageId | 4 | Overflow page identifier |
| NextPageId | 4 | Link to next page in chain (0 = end) |
| DataLength | 4 | Original data length (for validation) |

#### OverflowPointer Format

Stored inline in heap records (8 bytes total):

| Field | Size | Purpose |
|-------|------|---------|
| FirstPageId | 4 | ID of first overflow page in chain |
| Length | 4 | Total TEXT data length |

#### Free Space Map Configuration

- **Track:** Available space on each page in memory
- **Update:** On insert, delete, update operations
- **Query:** O(1) to find page with required space
- **Storage:** In-memory only (rebuilt on engine restart)

#### Overflow Configuration

- **MaxInlineTextBytes:** 250 bytes
- **OverflowPageDataSize:** 4084 bytes per page (4096 - 12-byte header)
- **OverflowPageHeader:** PageId (4) + NextPageId (4) + DataLength (4) = 12 bytes

#### Performance

- **Insert (small):** O(1) amortized with FSM
- **Insert (large):** O(M) where M = # overflow pages
- **Get (small):** O(1); O(M) for overflow; O(F) for forwarding chains (F = forwarding hops)
- **Update (in-place):** O(1); O(M) if size changes significantly
- **Delete:** O(1) mark; O(M) to free overflow pages
- **Scan:** O(N) + overflow I/O + forwarding hops

---

## Query Execution Pipeline

DimaDB uses a three-stage query execution model:

SQL Input 
    ↓ 
Lexer (Tokenization) 
    ↓ 
Parser (AST Generation) 
    ↓ 
QueryPlanner (Plan Compilation) ← Converts AST to optimized execution plan 
    ↓ 
QueryExecutor (Plan Execution) ← Interprets plan and produces results 
    ↓ 
StorageEngine (Data Operations) 
    ↓ 
Output

**Plan Nodes:**

| Node | Purpose | Example |
|------|---------|---------|
| `TableScan` | Scan all records from a table | `TableScan("users", "u")` |
| `Filter` | Apply WHERE predicate | `Filter(source, id > 5)` |
| `Project` | Select and compute columns | `Project(source, [id, name])` |
| `Limit` | Restrict result rows | `Limit(source, 10)` |

**Operator Precedence:**

Limit (outermost) 
└─ Project 
└─ Filter
└─ TableScan 