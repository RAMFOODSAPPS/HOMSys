namespace HOMSys.Domain.Entities;

/// <summary>CR = Collection Receipt, PR = Provisional Receipt, AR = Acknowledgement Receipt.</summary>
public enum ReceiptType { CR, PR, AR }

public enum PaymentMode { Cash, Check, PostDatedCheck, BankTransfer }

/// <summary>Where the money is. Immature/Matured/NearMaturity are derived from CheckDate, never stored.</summary>
/// <summary>Draft = requirements incomplete (e.g. no receipt soft copy yet); Collected = complete (CR);
/// Pending = complete PDC — the check is on hand, not yet collected; Deposited = its bank match is approved by the verifier.</summary>
public enum PaymentStatus { Collected, Deposited, Draft, Pending }

/// <summary>Finance verification track, independent of PaymentStatus.</summary>
public enum VerificationStatus { Draft, ForVerification, Returned, Verified, Voided }

public enum PdcOutcome { Succeeded, Bounced }

/// <summary>Follow-up of a bounced PDC, derived from its LOD / settlement / legal fields, never stored.</summary>
public static class DishonorStates
{
    public const string ForLod = "For LOD", LodIssued = "LOD Issued", AwaitingSettlement = "Awaiting Settlement",
        Overdue = "Overdue", ForLegal = "For Legal Action", Settled = "Settled";
}

/// <summary>RfcBoRtv: returns / bad orders / RTV a chain deducts on its Remittance Advice (beyond RFCs already posted in BMS).</summary>
public enum DeductionType { Ewt2307, Discount, Penalty, Other, RfcBoRtv }
