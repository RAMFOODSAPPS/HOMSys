using System.Text;

namespace HOMSys.Infrastructure.Data.Dbf;

/// <summary>
/// Minimal reader for Visual FoxPro DBF files.
///
/// Only what the reference-data import needs: header, field descriptors, and
/// records with C / N / D / L / I / T fields. Memo (M) fields are NOT supported —
/// none of the tables we import have an .FPT, verified during staging.
///
/// Production DBFs are frequently held open by BMS, so the file is opened with
/// FileShare.ReadWrite. A plain File.OpenRead throws "being used by another
/// process" on a live share.
/// </summary>
public sealed class DbfReader : IDisposable
{
    private readonly FileStream _fs;
    private readonly Encoding _encoding;

    public IReadOnlyList<DbfField> Fields { get; }
    public int RecordCount { get; }
    public int RecordLength { get; }
    public int HeaderLength { get; }

    static DbfReader()
    {
        // DBF text is codepage 1252, which .NET Core does not register by default.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public DbfReader(string path)
    {
        _fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        _encoding = Encoding.GetEncoding(1252);

        var header = new byte[32];
        ReadExact(header, 32);

        RecordCount = BitConverter.ToInt32(header, 4);
        HeaderLength = BitConverter.ToInt16(header, 8);
        RecordLength = BitConverter.ToInt16(header, 10);

        var fields = new List<DbfField>();
        var offset = 1;   // byte 0 of each record is the deleted flag
        var descriptor = new byte[32];

        while (_fs.Position < HeaderLength - 1)
        {
            ReadExact(descriptor, 32);
            if (descriptor[0] == 0x0D) break;   // header terminator

            var name = _encoding.GetString(descriptor, 0, 11).TrimEnd('\0').Trim();
            if (name.Length == 0) break;

            var field = new DbfField(
                name,
                (char)descriptor[11],
                descriptor[16],
                descriptor[17],
                offset);

            fields.Add(field);
            offset += field.Length;
        }

        Fields = fields;
    }

    /// <summary>Non-deleted records, streamed.</summary>
    public IEnumerable<DbfRecord> Records()
    {
        var buffer = new byte[RecordLength];

        for (var i = 0; i < RecordCount; i++)
        {
            _fs.Seek(HeaderLength + ((long)i * RecordLength), SeekOrigin.Begin);

            var read = 0;
            while (read < RecordLength)
            {
                var n = _fs.Read(buffer, read, RecordLength - read);
                if (n <= 0) break;
                read += n;
            }
            if (read < RecordLength) yield break;   // truncated tail

            if (buffer[0] == 0x2A) continue;        // deleted

            yield return new DbfRecord(buffer, Fields, _encoding) { RecNo = i + 1 };
        }
    }

    private void ReadExact(byte[] buffer, int count)
    {
        var read = 0;
        while (read < count)
        {
            var n = _fs.Read(buffer, read, count - read);
            if (n <= 0) throw new EndOfStreamException("Unexpected end of DBF header.");
            read += n;
        }
    }

    public void Dispose() => _fs.Dispose();
}

public sealed record DbfField(string Name, char Type, int Length, int Decimals, int Offset);

public sealed class DbfRecord(byte[] buffer, IReadOnlyList<DbfField> fields, Encoding encoding)
{
    /// <summary>1-based physical record position, matching VFP's RECNO() — stable
    /// identity for a DBF row across syncs, used to diff without relying on
    /// natural keys that can legitimately repeat (e.g. duplicate EFF_DATE rows).</summary>
    public int RecNo { get; init; }

    private DbfField? Find(string name) =>
        fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    public string GetString(string name)
    {
        var f = Find(name);
        if (f is null) return string.Empty;
        return encoding.GetString(buffer, f.Offset, f.Length).Trim();
    }

    public int GetInt(string name)
    {
        var raw = GetString(name);
        return int.TryParse(raw, out var v) ? v : 0;
    }

    public decimal GetDecimal(string name)
    {
        var raw = GetString(name);
        return decimal.TryParse(raw, out var v) ? v : 0m;
    }

    /// <summary>Same as GetDecimal, but returns null for a blank field instead of 0.</summary>
    public decimal? GetDecimalOrNull(string name)
    {
        var raw = GetString(name);
        if (raw.Length == 0) return null;
        return decimal.TryParse(raw, out var v) ? v : null;
    }

    /// <summary>DBF date fields are 8 chars, YYYYMMDD. Blank means null.</summary>
    public DateOnly? GetDate(string name)
    {
        var raw = GetString(name);
        if (raw.Length != 8) return null;

        return int.TryParse(raw[..4], out var y) &&
               int.TryParse(raw.Substring(4, 2), out var m) &&
               int.TryParse(raw.Substring(6, 2), out var d) &&
               y is >= 1 and <= 9999 && m is >= 1 and <= 12 && d is >= 1 and <= 31
            ? new DateOnly(y, m, d)
            : null;
    }

    public bool GetBool(string name)
    {
        var raw = GetString(name);
        return raw.Length > 0 && (raw[0] is 'T' or 't' or 'Y' or 'y');
    }
}
