namespace HOMSys.Domain.Entities;

/// <summary>
/// Live oowkdet snapshot for one SO line, as last read by the Python bridge
/// (salesorder_bridge.py's recover_one -> sync_oos, fired on every existing
/// Forward-to-Invoice / printinvoice2 call from invoice.SCX -- no separate
/// FoxPro call site). One row per (SoId, CProdNo) still present in oowkdet
/// at sync time. A SalesOrderLine with no matching row here, once the order
/// has synced at least once, means that CProdNo was already gone from
/// oowkdet (deleted as a full stockout) -- see SalesOrderService.MapToDto.
/// </summary>
public class OosSyncLine
{
    public int Id { get; set; }

    public int SoId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    public string CProdNo { get; set; } = string.Empty;
    public int AllocatedQtyCs { get; set; }
    public int AllocatedQtyPc { get; set; }
    public int? StkFlag { get; set; }

    /// <summary>oowkdet.NETAMT — VAT-inclusive net amount for this line, as last
    /// read by the same bridge sync as the quantities above.</summary>
    public decimal? NetAmt { get; set; }

    public DateTime SyncedAt { get; set; }
}
