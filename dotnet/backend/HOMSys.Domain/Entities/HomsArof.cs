namespace HOMSys.Domain.Entities;

/// <summary>
/// Row-for-row mirror of HOMS's A/R open file F:\HOMS\RAM\AROF4HO.DBF (user 2026-11-13: "migrate the arof4ho as whole
/// to SQL"). Key = the DBF record number, so a changed 1,000-record block (<see cref="HomsArofBlock"/>) is replaced by
/// RecNo range. Every DBF field is kept under its own name; blank numerics are 0, blank dates null, text trimmed.
/// Loaded once in full, then kept current by LegacyMasterWatcher (new rows at the end of the file, plus a periodic
/// block-fingerprint compare for rows HOMS edits in place: DOCBAL, re-applied RFCs/CNs). HOMSys never writes it back.
/// DOCID: 1 invoice, 6 RFC (REFID 1 / REFNO = invoice no.), 2 OR, 61 DM, 62 CM, 33/34/52 others.
/// </summary>
public class HomsArofRow
{
    public int RecNo { get; set; }
    /// <summary>The DBF deletion flag ("*").</summary>
    public bool Deleted { get; set; }

    public string AdjId { get; set; } = string.Empty;          // ADJ_ID C12
    public string PaymentId { get; set; } = string.Empty;      // PAYMENTID C20
    public DateOnly? TrDate { get; set; }                       // TRDATE
    public int WhseNo { get; set; }                             // WHSENO N2
    public int EmpNo { get; set; }                              // EMPNO N4
    public string CSman { get; set; } = string.Empty;           // CSMAN C4
    public int Salesman { get; set; }                           // SALESMAN N4
    public int TCode { get; set; }                              // TCODE N2
    public int AcctNo { get; set; }                             // ACCTNO N5
    public string CKey { get; set; } = string.Empty;            // CKEY C5
    public string CustName { get; set; } = string.Empty;        // CUSTNAME C30
    public int Term { get; set; }                               // TERM N2
    public int DocId { get; set; }                              // DOCID N2
    public DateOnly? DocDate { get; set; }                      // DOCDATE
    public string DocType { get; set; } = string.Empty;         // DOCTYPE C10
    public int DocNo { get; set; }                              // DOCNO N8
    public int RefId { get; set; }                              // REFID N2
    public int RefNo { get; set; }                              // REFNO N8
    public DateOnly? RefDate { get; set; }                      // REFDATE
    public string RefType { get; set; } = string.Empty;         // REFTYPE C10
    public int DcrNo { get; set; }                              // DCRNO N8
    public DateOnly? DcrDate { get; set; }                      // DCRDATE
    public bool BadCqf { get; set; }                            // BADCQF L
    public bool BadChq { get; set; }                            // BADCHQ L
    public decimal DrAmt { get; set; }                          // DRAMT N12.2
    public decimal CrAmt { get; set; }                          // CRAMT N12.2
    public string Bank { get; set; } = string.Empty;            // BANK C10
    public string Location { get; set; } = string.Empty;        // LOCATION C15
    public string CheckNo { get; set; } = string.Empty;         // CHECKNO C15
    public DateOnly? CheckDate { get; set; }                    // CHECKDATE
    public string Remarks { get; set; } = string.Empty;         // REMARKS C10
    public int RefId2 { get; set; }                             // REFID2 N2
    public int RefNo2 { get; set; }                             // REFNO2 N8
    public DateOnly? RefDate2 { get; set; }                     // REFDATE2
    public string RefType2 { get; set; } = string.Empty;        // REFTYPE2 C10
    public int TrType { get; set; }                             // TRTYPE N2
    public string DssNo { get; set; } = string.Empty;           // DSSNO C8
    public string Reference { get; set; } = string.Empty;       // REFERENCE C40
    public string DusCKey { get; set; } = string.Empty;         // DUSCKEY C5
    public string DCusName { get; set; } = string.Empty;        // DCUSNAME C35
    public string UserName { get; set; } = string.Empty;        // USERNAME C20
    public decimal DocBal { get; set; }                         // DOCBAL N12.2
    public long BatchNo { get; set; }                           // BATCHNO N10
    public bool Transfer { get; set; }                          // TRANSFER L
    public int SplitNo { get; set; }                            // SPLITNO N6
    public decimal OrigAmt { get; set; }                        // ORIGAMT N12.2
    public DateOnly? SysDate { get; set; }                      // SYSDATE
    public bool Process { get; set; }                           // PROCESS L
    public string Expln1 { get; set; } = string.Empty;          // EXPLN1 C75
    public string Expln2 { get; set; } = string.Empty;          // EXPLN2 C75
    public string Expln3 { get; set; } = string.Empty;          // EXPLN3 C75
    public string BCode { get; set; } = string.Empty;           // BCODE C2
    public string SName { get; set; } = string.Empty;           // SNAME C35
    public bool ChkRcvd { get; set; }                           // CHK_RCVD L
    public string CustKey { get; set; } = string.Empty;         // CUSTKEY C7
    public string Sman4Soa { get; set; } = string.Empty;        // SMAN4SOA C6
    public string PayGroup1 { get; set; } = string.Empty;       // PAYGROUP1 C22
    public string PayGroup2 { get; set; } = string.Empty;       // PAYGROUP2 C22
    public DateOnly? PurgDate { get; set; }                     // PURGDATE
    public DateOnly? HoPosted { get; set; }                     // HOPOSTED
    public int RsrNo { get; set; }                              // RSRNO N8
    public DateOnly? RsrDate { get; set; }                      // RSRDATE
    public int CrsrNo { get; set; }                             // CRSRNO N8
    public DateOnly? CrsrDate { get; set; }                     // CRSRDATE
    public string ArofId { get; set; } = string.Empty;          // AROFID C12 (not unique: ~100k blank, ~800 repeated)
    public string PoNum { get; set; } = string.Empty;           // PONUM C15
    public DateOnly? PoDate { get; set; }                       // PODATE
}

/// <summary>
/// Fingerprint of one block of <see cref="HomsArofFile.BlockSize"/> records of AROF4HO.DBF (block n = RecNo
/// n*BlockSize+1 .. (n+1)*BlockSize): BLAKE2b-128 hex of the block's raw record bytes as stored in the file, deletion
/// flags included. The watcher re-reads the file, compares, and sends only blocks whose fingerprint moved.
/// </summary>
public class HomsArofBlock
{
    public int BlockNo { get; set; }
    public int FirstRecNo { get; set; }
    public int RecordCount { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The one AROF4HO.DBF the mirror reflects (Id 1): the DBF header as last applied, so the watcher can tell new rows
/// (RecordCount grew), a rewrite (count shrank / purge + PACK) or a structure change (FieldList differs).
/// </summary>
public class HomsArofFile
{
    public const int SingletonId = 1;
    public const int DefaultBlockSize = 1000;

    public int Id { get; set; } = SingletonId;
    public string Path { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int RecordLength { get; set; }
    public int HeaderLength { get; set; }
    public int BlockSize { get; set; } = DefaultBlockSize;
    /// <summary>"NAME:TypeLen.Dec,..." in file order — a MODIFY STRUCTURE shows up here.</summary>
    public string FieldList { get; set; } = string.Empty;
    public long FileSize { get; set; }
    /// <summary>The file's last-write time when it was read (UTC).</summary>
    public DateTime? FileWrittenAt { get; set; }
    /// <summary>Last time every block was compared (full read).</summary>
    public DateTime? FullScanAt { get; set; }
    /// <summary>Last time any block was applied.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
