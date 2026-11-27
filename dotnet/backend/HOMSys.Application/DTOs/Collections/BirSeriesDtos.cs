namespace HOMSys.Application.DTOs.Collections;

/// <summary>Watcher payload for one HOMS B1 file (F:\HOMS\RAM\ARCHIVES\B103{MMYY}.DBF: invoice no. → BIR series).
/// Reset = first sync of the file (drop what HOMSys held for it). Removed = invoices no longer in the file.</summary>
public record BirSeriesSyncRequest(string SourceFile, bool Reset, List<BirSeriesSyncItem>? Rows, List<BirSeriesKey>? Removed);

public record BirSeriesKey(int DocId, int DocNo);

public record BirSeriesSyncItem(int WhseNo, int DocId, int DocNo, DateOnly? DocDate, int BirFrom, int BirTo);
