using DimaDB.Storage.Page.Overflow;
using DimaDB.Storage.Types;
using System.Text;

namespace DimaDB.Storage.Serialization;

public class RecordSerializer(Schema schema, OverflowManager? overflowManager = null)
{
    private readonly Schema _schema = schema;
    private readonly OverflowManager? _overflowManager = overflowManager;

    public byte[] Serialize(object?[] values)
    {
        if (values.Length != _schema.Columns.Length)
            throw new ArgumentException("Value count mismatch");

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        for (int i = 0; i < _schema.Columns.Length; i++)
        {
            var column = _schema.Columns[i];
            var value = values[i];

            if (value is null)
            {
                if (!column.AllowNull)
                    throw new InvalidOperationException($"Column {column.Name} does not allow null");
                WriteBit(bw, false);
                continue;
            }

            WriteBit(bw, true);

            switch (column.Type)
            {
                case ColumnType.Int:
                    bw.Write(Convert.ToInt32(value));
                    break;
                case ColumnType.Bool:
                    bw.Write(Convert.ToBoolean(value));
                    break;
                case ColumnType.Text:
                    SerializeText(bw, value.ToString() ?? "");
                    break;
                default:
                    throw new NotSupportedException($"Type {column.Type} not supported");
            }
        }

        return ms.ToArray();
    }

    public object?[] Deserialize(byte[] data)
    {
        var result = new object?[_schema.Columns.Length];
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        for (int i = 0; i < _schema.Columns.Length; i++)
        {
            var column = _schema.Columns[i];
            bool isNotNull = ReadBit(br);

            if (!isNotNull)
            {
                result[i] = null;
                continue;
            }

            result[i] = column.Type switch
            {
                ColumnType.Int => br.ReadInt32(),
                ColumnType.Bool => br.ReadBoolean(),
                ColumnType.Text => DeserializeText(br),
                _ => throw new NotSupportedException($"Type {column.Type} not supported")
            };
        }

        return result;
    }

    private void SerializeText(BinaryWriter bw, string str)
    {
        var textBytes = Encoding.UTF8.GetBytes(str);

        if (textBytes.Length <= OverflowManager.MaxInlineTextBytes || _overflowManager == null)
        {
            bw.Write((byte)0);
            bw.Write(textBytes.Length);
            bw.Write(textBytes);
        }
        else
        {
            var pointer = _overflowManager.StoreOverflow(textBytes);
            bw.Write((byte)1);
            var pointerBytes = new byte[OverflowPointer.Size];
            pointer.Write(pointerBytes);
            bw.Write(pointerBytes);
        }
    }

    private string DeserializeText(BinaryReader br)
    {
        byte marker = br.ReadByte();

        if (marker == 0)
        {
            int length = br.ReadInt32();
            byte[] buffer = br.ReadBytes(length);
            return Encoding.UTF8.GetString(buffer);
        }
        else if (marker == 1)
        {
            var pointerBytes = br.ReadBytes(OverflowPointer.Size);
            var pointer = OverflowPointer.Read(pointerBytes);

            if (_overflowManager == null)
            {
                throw new InvalidOperationException("OverflowManager not available for overflow resolution");
            }

            byte[] data = _overflowManager.RetrieveOverflow(pointer);
            return Encoding.UTF8.GetString(data);
        }
        else
        {
            throw new InvalidOperationException($"Invalid text marker: {marker}");
        }
    }

    private static void WriteBit(BinaryWriter bw, bool value)
    {
        bw.Write(value ? (byte)1 : (byte)0);
    }

    private static bool ReadBit(BinaryReader br) => br.ReadByte() != 0;
}