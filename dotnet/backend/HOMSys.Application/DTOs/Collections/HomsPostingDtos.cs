using System.Text.Json;

namespace HOMSys.Application.DTOs.Collections;

/// <summary>A posting handed to the HO watcher: append Lines to ARCHIVES\{DetailTable}{MonthKey} and Header to
/// {Table}{MonthKey}. Header / Lines are field name → value (null = blank). DocNo is null for a JV not yet numbered.</summary>
public record HomsPostingJobDto(int Id, string Kind, string Table, string DetailTable, string MonthKey, int DocId, int? DocNo,
    JsonElement Header, JsonElement Lines);

public record HomsPostingDocNoDto(int DocNo);

/// <summary>RetryLater = not written, try again (e.g. HOMS hasn't created the month file yet); Error = failed;
/// AlreadyPresent = the document was already in the file, nothing written.</summary>
public record CompleteHomsPostingDto(string? File, bool AlreadyPresent, string? Error, bool RetryLater);

/// <summary>Kind OR / RPDC / JV; Status Pending / Processing / Done / Held / Failed / Cancelled.</summary>
public record HomsPostingDto(int Id, string Kind, string Status, int? DocNo, string? File, string? Message, DateTime CreatedAt, DateTime? CompletedAt);
