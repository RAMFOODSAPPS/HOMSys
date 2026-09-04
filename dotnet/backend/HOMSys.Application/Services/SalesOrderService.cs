using System.Security.Claims;
using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

/// <summary>
/// Sales order encoding. Reproduces the save path of the legacy VFP form
/// \\itworks-pc\source\bms\a11102.SCX (cmdSave.Click).
///
/// See legacy/vfp/ANALYSIS.md. Deliberately NOT reproduced: order limits
/// (chklimit/maxorder), AR aging, blockinv/TIN gates, blocked-SKU removal,
/// GetMax (dead in the legacy form since 2024-05-18), pricing/discounts.
/// </summary>
public class SalesOrderService(
    ISalesOrderRepository orderRepo,
    ICustomerRepository customerRepo,
    IProductRepository productRepo,
    IPoLogRepository poLogRepo,
    IDocClassRepository docClassRepo,
    ICustomerIdentifierMapRepository identifierMapRepo,
    IUnitOfWork uow,
    IHttpContextAccessor http,
    PriceCalculationService priceCalc)
{
    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    private string? CurrentBranch =>
        http.HttpContext?.User?.FindFirstValue("branch");

    public async Task<IEnumerable<SalesOrderDto>> GetAllAsync()
    {
        var orders = await orderRepo.GetAllAsync();
        if (!string.IsNullOrEmpty(CurrentBranch))
            orders = orders.Where(o => o.Branch == CurrentBranch);
        var result = new List<SalesOrderDto>();
        foreach (var order in orders)
            result.Add(await MapToDtoAsync(order));
        return result;
    }

    public async Task<SalesOrderDto?> GetByIdAsync(int soId)
    {
        var order = await orderRepo.GetByIdAsync(soId);
        if (order is null) return null;
        if (!string.IsNullOrEmpty(CurrentBranch) && order.Branch != CurrentBranch) return null;
        return await MapToDtoAsync(order);
    }

    /// <summary>Sum of PricePerCase * QtyCs * 1.12 across lines — same formula as the encode grid's running total.</summary>
    private async Task<decimal> ComputeEstAmtAsync(SalesOrder o)
    {
        var total = 0m;
        foreach (var line in o.Lines)
        {
            var quote = await priceCalc.GetQuoteAsync(line.CProdNo, o.CustKey);
            total += (quote.PricePerCase ?? 0m) * line.QtyCs * 1.12m;
        }
        return Math.Round(total, 2);
    }

    public async Task<CustomerLookupDto?> LookupCustomerAsync(string custKey)
    {
        var c = await customerRepo.GetByCustKeyAsync(custKey.Trim());
        if (c is null) return null;

        // Delivery address overrides the mailing address as ship-to when present,
        // matching the legacy addnew procedure.
        var hasDel = !string.IsNullOrWhiteSpace(c.DelAddrLn1) ||
                     !string.IsNullOrWhiteSpace(c.DelAddrLn2);

        return new CustomerLookupDto
        {
            CustKey = c.CustKey,
            CusName = c.CusName,
            CKey = c.CKey,
            WhseNo = c.WhseNo,
            CustWhse = c.CustWhse,
            Term = c.Term,
            TermDays = c.TermDays,
            Salesman = c.Salesman,
            CsMan = c.CsMan,
            ShipToLn1 = hasDel ? c.DelAddrLn1 : c.AddrLn1,
            ShipToLn2 = hasDel ? c.DelAddrLn2 : c.AddrLn2,
            DelArea = c.DelArea,
            VatId = c.VatId,
            Tpc = c.Tpc,
            Offshore = c.Offshore,
            ExBranch = c.ExBranch,
            CCode = ResolveCCode(c),
            IsCash = c.Term == 0
        };
    }

    /// <summary>
    /// Legacy addnew: when IEffDate is set and the order date is on or after it,
    /// use CCode; otherwise fall back to OldCCode.
    /// </summary>
    private static int ResolveCCode(Customer c)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (c.IEffDate is null) return c.OldCCode != 0 ? c.OldCCode : c.CCode;
        return today >= c.IEffDate.Value ? c.CCode : c.OldCCode;
    }

    public async Task<ProductLookupDto?> LookupProductAsync(string cProdNo)
    {
        var p = await productRepo.GetByCProdNoAsync(cProdNo.Trim());
        return p is null ? null : ToLookup(p);
    }

    private static ProductLookupDto ToLookup(Product p) => new()
    {
        CProdNo = p.CProdNo,
        ProdNo = p.ProdNo,
        ProdDesc = p.ProdDesc,
        PackSize = p.PackSize,
        Pieces = p.Pieces,
        QtyPerPc = p.QtyPerPc,
        Um = p.Um,
        PriceList = p.PriceList,
        TaxRate = p.TaxRate,
        Supplier = p.Supplier
    };

    /// <summary>Typeahead for the Customer Key field — matches on key or name.</summary>
    public Task<IEnumerable<CustomerSuggestionDto>> SearchCustomersAsync(string term) =>
        customerRepo.SearchAsync((term ?? string.Empty).Trim());

    /// <summary>Typeahead for the Prodno field — matches on code or description.</summary>
    public async Task<IEnumerable<ProductSuggestionDto>> SearchProductsAsync(string term) =>
        (await productRepo.SearchAsync((term ?? string.Empty).Trim()))
            .Select(p => new ProductSuggestionDto { CProdNo = p.CProdNo, ProdDesc = p.ProdDesc, PackSize = p.PackSize, Pieces = p.Pieces });

    /// <summary>Document Classification combo options (docclass.DBF).</summary>
    public async Task<IEnumerable<DocClassDto>> GetDocClassesAsync() =>
        (await docClassRepo.GetAllAsync())
            .Select(d => new DocClassDto { Code = d.Code, Description = d.Description });

    /// <summary>
    /// Known Customer Identifier -> CustKey mappings for the "Import by
    /// Customer Name" mapping dialog, pre-filled with the customer name so
    /// the encoder can confirm at a glance.
    /// </summary>
    public async Task<IEnumerable<CustomerIdentifierMapDto>> GetCustomerIdentifierMapsAsync(IEnumerable<string> identifiers)
    {
        var maps = await identifierMapRepo.GetByIdentifiersAsync(identifiers);
        if (maps.Count == 0) return [];

        var customers = await customerRepo.GetByCustKeysAsync(maps.Select(m => m.CustKey));
        return maps.Select(m => new CustomerIdentifierMapDto
        {
            Identifier = m.Identifier,
            CustKey = m.CustKey,
            CusName = customers.TryGetValue(m.CustKey, out var c) ? c.CusName : string.Empty
        });
    }

    /// <summary>Persists the encoder's Identifier -> CustKey choices from the mapping dialog for reuse on future imports.</summary>
    public async Task SaveCustomerIdentifierMapsAsync(IEnumerable<SaveCustomerIdentifierMapDto> mappings) =>
        await identifierMapRepo.UpsertRangeAsync(
            mappings
                .Where(m => !string.IsNullOrWhiteSpace(m.Identifier) && !string.IsNullOrWhiteSpace(m.CustKey))
                .Select(m => (m.Identifier.Trim(), m.CustKey.Trim())),
            CurrentUser);

    /// <summary>
    /// PO duplicate check. WARNING ONLY.
    ///
    /// The legacy txtPonum.Valid shows messageb(...,48,...) — an OK-only box —
    /// then a bare RETURN, which in VFP yields .T., so the entry is accepted.
    /// Duplicate PO numbers are legal. Never turn this into a hard failure.
    /// </summary>
    public async Task<PoCheckDto> CheckPoNumberAsync(string poNum)
    {
        poNum = (poNum ?? string.Empty).Trim();
        var result = new PoCheckDto { PoNum = poNum };

        if (string.IsNullOrEmpty(poNum)) return result;

        var previous = await poLogRepo.GetLatestAsync(poNum);
        if (previous is null) return result;

        result.AlreadyEncoded = true;
        result.PreviousSoNo = previous.SoNo;
        result.PreviousOrderDate = previous.OrderDate;
        result.PreviousCustKey = previous.CustKey;
        result.Message = $"PO# {poNum} has already been encoded.";
        return result;
    }

    /// <summary>
    /// Import file-hash check. HARD BLOCK — unlike CheckPoNumberAsync, the
    /// client must stop the import wizard when AlreadyProcessed is true.
    /// "Processed" means a real, saved SalesOrder carries this hash — draft
    /// tabs closed without saving never set it, so they don't count.
    /// </summary>
    public async Task<FileImportCheckResultDto> CheckFileImportedAsync(string fileHash)
    {
        var result = new FileImportCheckResultDto();
        if (string.IsNullOrWhiteSpace(fileHash)) return result;

        var existing = await orderRepo.FindByFileHashAsync(fileHash.Trim());
        if (existing is null) return result;

        result.AlreadyProcessed = true;
        result.FirstProcessedAt = existing.CreatedAt;
        result.FirstProcessedBy = existing.CreatedBy;
        return result;
    }

    /// <summary>
    /// Fallback Customer+PO duplicate check, run when the file hash didn't
    /// match (e.g. the file was re-saved/re-exported). WARNING ONLY.
    /// </summary>
    public async Task<RowDuplicateCheckResultDto> CheckRowDuplicatesAsync(IEnumerable<ImportCheckRowDto> rows)
    {
        var input = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.CustKey) && !string.IsNullOrWhiteSpace(r.PoNum))
            .Select(r => (CustKey: r.CustKey.Trim(), PoNum: r.PoNum.Trim()))
            .Distinct()
            .ToList();

        var result = new RowDuplicateCheckResultDto();
        if (input.Count == 0) return result;

        var existing = await orderRepo.FindByPoNumsAsync(input.Select(r => r.PoNum));
        var existingPairs = existing.Select(o => (o.CustKey, o.PoNum)).ToHashSet();

        result.DuplicateRows = input
            .Where(existingPairs.Contains)
            .Select(r => new ImportCheckRowDto { CustKey = r.CustKey, PoNum = r.PoNum })
            .ToList();
        return result;
    }

    /// <summary>
    /// Early PO-Number-only hard block, run right after Next in the import
    /// wizard, before column mapping/customer resolution. Catches a batch
    /// that's already been saved as real Sales Orders even when the file's
    /// bytes changed (e.g. a renamed worksheet tab defeats the file-hash
    /// check in CheckFileImportedAsync). Matches on PO Number alone since
    /// the "Import by Customer Name" flow doesn't know CustKey yet here.
    /// </summary>
    public async Task<PoImportCheckResultDto> CheckPoNumbersImportedAsync(IEnumerable<string> poNums)
    {
        var input = poNums
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct()
            .ToList();

        var result = new PoImportCheckResultDto();
        if (input.Count == 0) return result;

        var existing = await orderRepo.FindByPoNumsAsync(input);
        result.Matches = existing.Select(o => new PoImportMatchDto
        {
            PoNum = o.PoNum,
            CustKey = o.CustKey,
            CusName = o.CusName,
            OrderDate = o.OrderDate,
            EncodedBy = o.CreatedBy
        }).ToList();
        return result;
    }

    /// <summary>
    /// Legacy: "dele for empty(cprodno)" — lines without a product are dropped,
    /// not rejected. Validates every remaining product number exists.
    /// </summary>
    private async Task<(List<CreateSalesOrderLineDto> lines, Dictionary<string, Product> products, string? error)>
        ResolveLinesAndProducts(IEnumerable<CreateSalesOrderLineDto>? rawLines)
    {
        var lines = (rawLines ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.CProdNo))
            .ToList();

        if (lines.Count == 0)
            return (lines, new Dictionary<string, Product>(), "No record to save. Enter at least one product.");

        var products = await productRepo.GetByCProdNosAsync(
            lines.Select(l => l.CProdNo.Trim()));

        var missing = lines
            .Select(l => l.CProdNo.Trim())
            .Where(c => !products.ContainsKey(c))
            .Distinct()
            .ToList();

        if (missing.Count > 0)
            return (lines, products, $"Unknown product number(s): {string.Join(", ", missing)}.");

        return (lines, products, null);
    }

    /// <summary>
    /// Denormalises customer fields onto the order header. Shared by create and
    /// update — excludes creation-only fields (SoNo, UserName, SysDate,
    /// CreatedAt/By, SoTymStart/End/Elapsed, ExpectDel).
    /// </summary>
    private static void ApplyCustomerToHeader(SalesOrder order, Customer customer)
    {
        var hasDel = !string.IsNullOrWhiteSpace(customer.DelAddrLn1) ||
                     !string.IsNullOrWhiteSpace(customer.DelAddrLn2);

        order.CustKey = customer.CustKey;
        order.CusName = customer.CusName;
        order.CKey = customer.CKey;

        order.CCode = ResolveCCode(customer);
        order.WhseNo = customer.WhseNo;
        order.CustWhse = customer.CustWhse;

        order.ShipToLn1 = hasDel ? customer.DelAddrLn1 : customer.AddrLn1;
        order.ShipToLn2 = hasDel ? customer.DelAddrLn2 : customer.AddrLn2;
        order.DelArea = customer.DelArea;

        order.Term = customer.Term;
        order.TermDays = customer.TermDays;
        order.Salesman = customer.Salesman;
        order.CsMan = customer.CsMan;

        order.Tpc = customer.Tpc;
        order.Offshore = customer.Offshore;
        order.ExBranch = customer.ExBranch;
        order.VatId = customer.VatId;
    }

    private static List<SalesOrderLine> BuildLines(
        List<CreateSalesOrderLineDto> lines, Dictionary<string, Product> products)
    {
        var built = new List<SalesOrderLine>();
        var lineNo = 1;

        foreach (var l in lines)
        {
            var p = products[l.CProdNo.Trim()];
            var (qtyCs, qtyPc) = NormaliseQuantity(l.QtyCs, l.QtyPc, p.Pieces);

            built.Add(new SalesOrderLine
            {
                LineNo = lineNo++,
                DocNo = null,

                CProdNo = p.CProdNo,
                ProdNo = p.ProdNo,

                // Denormalised from Product, exactly as the legacy save does.
                // oowkdet.PRODDESC is C(50) while prod4win.PRODDESC is C(75).
                ProdDesc = Truncate(p.ProdDesc, 50),
                PackSize = p.PackSize,
                Pieces = p.Pieces,
                QtyPerPc = p.QtyPerPc,
                Um = p.Um,
                Supplier = p.Supplier,
                CSupplier = ToChar2(p.Supplier),
                PriceList = p.PriceList,
                TaxRate = p.TaxRate,

                QtyCs = qtyCs,
                QtyPc = qtyPc,
                FreeGoods = l.FreeGoods
            });
        }

        return built;
    }

    public async Task<(SalesOrderDto? order, string? error)> CreateAsync(CreateSalesOrderDto dto)
    {
        if (string.IsNullOrEmpty(CurrentBranch))
            return (null, "Your account has no branch assigned; contact an administrator.");

        var custKey = (dto.CustKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(custKey))
            return (null, "Customer key is required.");

        var customer = await customerRepo.GetByCustKeyAsync(custKey);
        if (customer is null)
            return (null, $"Customer {custKey} not found.");

        var (lines, products, linesError) = await ResolveLinesAndProducts(dto.Lines);
        if (linesError is not null)
            return (null, linesError);

        var now = DateTime.UtcNow;

        // Legacy uses sysparam.transdate as the order date. sysparam is not
        // imported into HOMSys, so the server date is used instead. If BMS
        // transdate ever diverges from the calendar date this needs revisiting.
        var orderDate = DateOnly.FromDateTime(DateTime.Now);

        var order = new SalesOrder
        {
            SoNo = null,   // filled by the Python bridge
            DocNo = null,

            OrderDate = orderDate,

            PoNum = (dto.PoNum ?? string.Empty).Trim(),
            PoDate = dto.PoDate,
            CancelDate = dto.CancelDate,
            InvRem = Truncate(dto.InvRem, 100),
            Remarks = Truncate(dto.Remarks, 60),

            ExpectDel = false,   // legacy sets this false explicitly on save

            UserName = CurrentUser,
            Branch = CurrentBranch,
            SysDate = orderDate,

            SoTymStart = dto.SoTymStart,
            SoTymEnd = now,
            SoElapsed = FormatElapsed(dto.SoTymStart, now),

            SourceFileHash = string.IsNullOrWhiteSpace(dto.SourceFileHash) ? null : dto.SourceFileHash.Trim(),
            SourceFileName = string.IsNullOrWhiteSpace(dto.SourceFileName) ? null : dto.SourceFileName.Trim(),

            CreatedAt = now,
            CreatedBy = CurrentUser
        };

        ApplyCustomerToHeader(order, customer);

        // O.R. details only apply to cash customers.
        if (customer.Term == 0)
        {
            order.OrNo = dto.OrNo;
            order.ChkDate = dto.ChkDate;
            order.OrAmt = dto.OrAmt;
        }

        order.DocClass = dto.DocClass;

        foreach (var line in BuildLines(lines, products))
            order.Lines.Add(line);

        await uow.BeginTransactionAsync();
        try
        {
            var created = await orderRepo.CreateAsync(order);

            // Legacy checkponum2: log the PO number against this order.
            if (!string.IsNullOrEmpty(created.PoNum))
            {
                await poLogRepo.AddAsync(new PoLog
                {
                    PoNum = created.PoNum,
                    PoDate = created.PoDate,
                    SoNo = null,          // bridge fills this alongside SalesOrder.SoNo
                    SoId = created.SoId,
                    OrderDate = created.OrderDate,
                    CustKey = created.CustKey,
                    CusName = created.CusName,
                    IsSeeded = false,
                    CreatedAt = now
                });
            }

            await uow.CommitAsync();

            var saved = await orderRepo.GetByIdAsync(created.SoId);
            return (MapToDto(saved!), null);
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    public async Task<(SalesOrderDto? order, string? error)> UpdateAsync(int soId, CreateSalesOrderDto dto)
    {
        var order = await orderRepo.GetForUpdateAsync(soId);
        if (order is null || (!string.IsNullOrEmpty(CurrentBranch) && order.Branch != CurrentBranch))
            return (null, $"Sales order {soId} not found.");

        if (order.InvNo is not null)
            return (null, $"Sales order {soId} has already been invoiced (INV# {order.InvNo}) and cannot be edited.");

        if (order.WorkflowStatus == "Processed")
            return (null, $"Sales order {soId} has been Processed and cannot be edited.");

        if (order.IsLocked)
            return (null, $"Sales order {soId} is locked — it was pushed to BMS as SO# {order.SoNo}. Deallocate it in BMS first.");

        var custKey = (dto.CustKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(custKey))
            return (null, "Customer key is required.");

        var customer = await customerRepo.GetByCustKeyAsync(custKey);
        if (customer is null)
            return (null, $"Customer {custKey} not found.");

        var (lines, products, linesError) = await ResolveLinesAndProducts(dto.Lines);
        if (linesError is not null)
            return (null, linesError);

        var originalPoNum = order.PoNum;

        ApplyCustomerToHeader(order, customer);

        order.PoNum = (dto.PoNum ?? string.Empty).Trim();
        order.PoDate = dto.PoDate;
        order.CancelDate = dto.CancelDate;
        order.InvRem = Truncate(dto.InvRem, 100);
        order.Remarks = Truncate(dto.Remarks, 60);

        if (customer.Term == 0)
        {
            order.OrNo = dto.OrNo;
            order.ChkDate = dto.ChkDate;
            order.OrAmt = dto.OrAmt;
        }
        else
        {
            order.OrNo = null;
            order.ChkDate = null;
            order.OrAmt = null;
        }

        order.DocClass = dto.DocClass;

        order.Lines.Clear();
        foreach (var line in BuildLines(lines, products))
            order.Lines.Add(line);

        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = CurrentUser;

        // Order was already pushed to BMS (deallocated, then edited again) — the
        // live oowkhdr/oowkdet record needs this edit pushed back. Re-lock until
        // BMS confirms the resync landed.
        if (order.SoNo is not null)
        {
            order.NeedsResync = true;
            order.ResyncFailed = false;
            order.IsLocked = true;
            order.WorkflowStatus = "Downloaded";
        }

        await uow.BeginTransactionAsync();
        try
        {
            // Legacy checkponum2: only log when the PO number actually changed.
            if (!string.IsNullOrEmpty(order.PoNum) && order.PoNum != originalPoNum)
            {
                await poLogRepo.AddAsync(new PoLog
                {
                    PoNum = order.PoNum,
                    PoDate = order.PoDate,
                    SoNo = null,
                    SoId = order.SoId,
                    OrderDate = order.OrderDate,
                    CustKey = order.CustKey,
                    CusName = order.CusName,
                    IsSeeded = false,
                    CreatedAt = order.UpdatedAt.Value
                });
            }

            await orderRepo.SaveChangesAsync();
            await uow.CommitAsync();

            var saved = await orderRepo.GetByIdAsync(soId);
            return (MapToDto(saved!), null);
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Quantity normalisation, matching legacy hconvert.prg:
    ///
    ///     mval  = qtypc + (qtycs * pieces)
    ///     qtycs = int(mval / pieces)
    ///     qtypc = mval - int(mval/pieces) * pieces
    ///
    /// i.e. plain divmod. Loose pieces roll up into cases.
    ///
    /// The legacy code divides by zero when Pieces = 0; guard instead and leave
    /// the quantities as entered.
    /// </summary>
    public static (int QtyCs, int QtyPc) NormaliseQuantity(int qtyCs, int qtyPc, int pieces)
    {
        if (pieces <= 0) return (qtyCs, qtyPc);

        var total = qtyPc + (qtyCs * pieces);
        return (total / pieces, total % pieces);
    }

    /// <summary>
    /// Legacy: csupplier = str(prod4win.supplier, 2) — a 2-character field.
    /// Supplier codes are N(2) at source so this is normally exact; if a value
    /// ever exceeds two digits keep the low-order digits rather than the high.
    /// </summary>
    private static string ToChar2(int supplier)
    {
        var s = supplier.ToString();
        return s.Length <= 2 ? s : s[^2..];
    }

    private static string FormatElapsed(DateTime? start, DateTime end)
    {
        if (start is null) return string.Empty;
        var span = end - start.Value;
        return span < TimeSpan.Zero
            ? string.Empty
            : $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    private async Task<SalesOrderDto> MapToDtoAsync(SalesOrder o)
    {
        var dto = MapToDto(o);
        dto.EstAmt = await ComputeEstAmtAsync(o);
        return dto;
    }

    /// <summary>
    /// OOS status per CProdNo from the bridge-synced snapshot (OosSyncLine).
    /// No rows at all means the order hasn't synced yet -- leave the DTO's
    /// Allocated fields null (unknown, not "fully OOS"). Once an order has
    /// synced, a line with no matching row is fully OOS (StkFlag 2): the
    /// bridge posts a full overwrite of everything still in oowkdet, so a
    /// missing CProdNo means it was already deleted as a full stockout.
    /// </summary>
    private static void ApplyOosStatus(SalesOrder o, IReadOnlyList<SalesOrderLineDto> lineDtos)
    {
        if (o.OosSyncLines.Count == 0) return;

        var synced = o.OosSyncLines.ToDictionary(l => l.CProdNo);
        foreach (var dto in lineDtos)
        {
            if (synced.TryGetValue(dto.CProdNo, out var oos))
            {
                dto.AllocatedQtyCs = oos.AllocatedQtyCs;
                dto.AllocatedQtyPc = oos.AllocatedQtyPc;
                dto.StkFlag = oos.StkFlag;
                dto.InvNetAmt = oos.NetAmt;
            }
            else
            {
                dto.AllocatedQtyCs = 0;
                dto.AllocatedQtyPc = 0;
                dto.StkFlag = 2;
                dto.InvNetAmt = 0;
            }
        }
    }

    private static SalesOrderDto MapToDto(SalesOrder o)
    {
        var dto = BuildDto(o);
        ApplyOosStatus(o, dto.Lines);
        return dto;
    }

    private static SalesOrderDto BuildDto(SalesOrder o) => new()
    {
        SoId = o.SoId,
        SoNo = o.SoNo,
        CustKey = o.CustKey,
        CusName = o.CusName,
        OrderDate = o.OrderDate,
        PoNum = o.PoNum,
        PoDate = o.PoDate,
        CancelDate = o.CancelDate,
        InvRem = o.InvRem,
        Remarks = o.Remarks,
        ShipToLn1 = o.ShipToLn1,
        ShipToLn2 = o.ShipToLn2,
        Term = o.Term,
        Salesman = o.Salesman,
        CsMan = o.CsMan,
        OrNo = o.OrNo,
        ChkDate = o.ChkDate,
        OrAmt = o.OrAmt,
        DocClass = o.DocClass,
        InvNo = o.InvNo,
        InvDate = o.InvDate,
        InvAmt = o.InvAmt,
        IsLocked = o.IsLocked,
        NeedsResync = o.NeedsResync,
        ResyncFailed = o.ResyncFailed,
        WorkflowStatus = o.WorkflowStatus,
        CreatedAt = o.CreatedAt,
        CreatedBy = o.CreatedBy,
        Lines = o.Lines.OrderBy(l => l.LineNo).Select(l => new SalesOrderLineDto
        {
            Id = l.Id,
            LineNo = l.LineNo,
            CProdNo = l.CProdNo,
            ProdNo = l.ProdNo,
            ProdDesc = l.ProdDesc,
            PackSize = l.PackSize,
            QtyCs = l.QtyCs,
            QtyPc = l.QtyPc,
            Pieces = l.Pieces,
            Um = l.Um,
            PriceList = l.PriceList,
            TaxRate = l.TaxRate,
            FreeGoods = l.FreeGoods
        }).ToList()
    };
}
