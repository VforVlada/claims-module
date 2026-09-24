namespace ClaimsModule.Domain.Enums;

public enum ClaimStatus
{
    Draft = 0,
    Open = 1,
    UnderInvestigation = 2,
    PendingPayment = 3,
    Closed = 4,

    /// <summary>Previously closed claim re-activated (FRS Appendix A.1) — reachable only from Closed.</summary>
    Reopened = 5,

    /// <summary>Claimant withdrew the claim (FRS Appendix A.1) — terminal, like Closed.</summary>
    Withdrawn = 6
}
