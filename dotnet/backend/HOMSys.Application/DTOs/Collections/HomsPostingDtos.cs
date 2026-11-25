using System.Text.Json;

namespace HOMSys.Application.DTOs.Collections;

/// <summary>A posting handed to the HO watcher: append Lines to ARCHIVES\{DetailTable}{MonthKey} and Header to
/// {Table}{MonthKey}. Header / Lines are field name → value (null = blank). DocNo is null for a JV not yet numbered.</summary>
/// Register = what DCSR writes besides GH/GD for an RPDC / JV (pdc4w / deposits rows, pd / DP files, desk); {} = none.</summary>
public record HomsPostingJobDto(int Id, string Kind, string Table, string DetailTable, string MonthKey, int DocId, int? DocNo,
    JsonElement Header, JsonElement Lines, JsonElement Deductions, JsonElement Register);

public record HomsPostingDocNoDto(int DocNo);

/// <summary>A JV DCSR already made for the check (pdc4w GATRNO / GATRDATE), found before HOMSys wrote its own.</summary>
public record HomsJvByDcsrDto(int No, string? Date);

/// <summary>RetryLater = not written, try again (e.g. HOMS hasn't created the month file yet); Error = failed;
/// AlreadyPresent = the document was already in the file, nothing written.
/// DeductionsWritten = its ordeduc2 rows are in HOMS (written now; for a DEDUCTIONS posting, or already there).
/// JvByDcsr = a JV: DCSR had already JV'd the check, nothing was written. Note = what a REGISTER run added.</summary>
public record CompleteHomsPostingDto(string? File, bool AlreadyPresent, string? Error, bool RetryLater, bool DeductionsWritten = false,
    HomsJvByDcsrDto? JvByDcsr = null, string? Note = null);

/// <summary>Kind OR / RPDC / JV; Status Pending / Processing / Done / Held / Failed / Cancelled.</summary>
public record HomsPostingDto(int Id, string Kind, string Status, int? DocNo, string? File, string? Message, DateTime CreatedAt, DateTime? CompletedAt);
