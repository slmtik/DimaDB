using DimaDB.Core.Storage;
using DimaDB.Core.Storage.Page.Overflow;
using DimaDB.Core.Storage.Types;
using Xunit.Abstractions;

namespace DimaDB.Core.Tests.Storage;

public class StorageEngineDemo(ITestOutputHelper output)
{
    [Fact]
    public void Demo_HeapAndOverflowPageIntegration()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"demo_{Guid.NewGuid()}");
        var engine = new StorageEngine(testDir);

        output.WriteLine("=== DimaDB Storage Engine Demo ===\n");

        output.WriteLine("1. Creating 'documents' table with TEXT columns...");
        var columns = new[]
        {
            new ColumnDefinition("doc_id", ColumnType.Int, false),
            new ColumnDefinition("title", ColumnType.Text, false),
            new ColumnDefinition("content", ColumnType.Text, true)
        };

        engine.CreateTable("documents", columns);
        output.WriteLine("   ✓ Table created\n");

        output.WriteLine("2. Inserting documents (small + large TEXT)...");
        var table = engine.OpenTable("documents");

        var rid1 = table.Insert([1, "Short", "Brief content"]);
        output.WriteLine($"   ✓ Small doc: {rid1} (TEXT inline)");

        var largeContent = new string('x', 5000);
        var rid2 = table.Insert([2, "Large Document", largeContent]);
        output.WriteLine($"   ✓ Large doc: {rid2} (TEXT overflowed)");

        var veryLargeContent = new string('x', 15000);
        var rid3 = table.Insert([3, "Huge Document", veryLargeContent]);
        output.WriteLine($"   ✓ Huge doc: {rid3} (TEXT multi-page overflow)\n");

        output.WriteLine("3. Retrieving documents...");
        var doc1 = table.Get(rid1);
        var doc2 = table.Get(rid2);
        var doc3 = table.Get(rid3);

        output.WriteLine($"   Doc 1: {doc1![1]} ({((string)doc1![2]!).Length} bytes)");
        output.WriteLine($"   Doc 2: {doc2![1]} ({((string)doc2![2]!).Length} bytes)");
        output.WriteLine($"   Doc 3: {doc3![1]} ({((string)doc3![2]!).Length} bytes)\n");

        output.WriteLine("4. Scanning all documents...");
        var scanned = table.Scan().ToList();
        foreach (var (rid, values) in scanned)
        {
            var title = values[1];
            var contentLen = ((string)values[2]!).Length;
            var storageType = contentLen > OverflowManager.MaxInlineTextBytes ? "OVERFLOW" : "INLINE";
            output.WriteLine($"   {rid}: {title} ({contentLen}B, {storageType})");
        }
        output.WriteLine($"   Total: {scanned.Count} documents\n");

        output.WriteLine("5. Updating document (large → small)...");
        var doc2Before = table.Get(rid2);
        output.WriteLine($"   Before update: {string.Join(" | ", doc2Before!.Select(v => v?.ToString() ?? "NULL"))}");
        output.WriteLine($"   ✓ Updated: {table.Update(rid2, [2, "Large Document", "Updated!"])}");
        var doc2After = table.Get(rid2);
        output.WriteLine($"   After update: {string.Join(" | ", doc2After!.Select(v => v?.ToString() ?? "NULL"))}\n");

        output.WriteLine("6. Updating document (small → large)...");
        var doc1Before = table.Get(rid1);
        output.WriteLine($"   Before update: {string.Join(" | ", doc1Before!.Select(v => v?.ToString() ?? "NULL"))}");
        output.WriteLine($"   ✓ Updated: {table.Update(rid1, [1, "Short", "Updated!"])}");
        var doc1After = table.Get(rid1);
        output.WriteLine($"   After update: {string.Join(" | ", doc1After!.Select(v => v?.ToString() ?? "NULL"))}\n");

        output.WriteLine("7. Testing persistence...");
        engine.Flush();
        engine.Dispose();
        output.WriteLine("   ✓ Engine disposed and flushed\n");

        output.WriteLine("8. Restarting engine...");
        var engine2 = new StorageEngine(testDir);
        var table2 = engine2.OpenTable("documents");

        var restored = table2.Get(rid3);
        output.WriteLine($"   ✓ Retrieved after restart: {restored![1]}");
        output.WriteLine($"   ✓ Content size: {((string)restored![2]!).Length} bytes\n");

        output.WriteLine("9. File structure:");
        var files = Directory.GetFiles(testDir);
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            output.WriteLine($"   {Path.GetFileName(file)}: {info.Length} bytes");
        }

        engine2.Dispose();
        Directory.Delete(testDir, true);

        output.WriteLine("\n=== Demo Complete ===");
    }
}