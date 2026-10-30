namespace HOMSys.Application.DTOs.Collections;

/// <summary>Watcher payload for one OH03/OD03 month. Reset = first sync of the file (drop what HOMSys held for it).</summary>
public record BmsCollectionSyncRequest(string SourceFile, bool Reset, List<BmsDocSyncItem>? Docs, List<BmsDocKey>? Removed);

public record BmsDocKey(int DocId, int DocNo);

public record BmsDocSyncItem(
    int DocId, int DocNo, DateOnly? DocDate, int DcrNo, DateOnly? DcrDate, string? UserName, string? Payor, int TCode,
    DateOnly? Posted, string? StatDesc, int DeptCode, List<BmsLineSyncItem>? Lines);

public record BmsLineSyncItem(
    int WhseNo, string? CustKey, string? CKey, int RefId, int RefNo, DateOnly? RefDate, decimal DrAmt, decimal CrAmt, int AcctNo,
    string? DraweeBank, string? CheckNo, DateOnly? CheckDate, bool BadCheck, string? DepBank, string? PaymentId,
    string? RecBank, string? Reference, int TrType);

/// <summary>The BMS posting a HOMSys collection was found in.</summary>
public record BmsPostingDto(int DocNo, DateOnly? DocDate, DateOnly? Posted, decimal Received, decimal Ewt, decimal ArApplied,
    bool Bounced, string LinkMethod);
