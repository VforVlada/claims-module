using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Entities;

public sealed class Claim : AggregateRoot
{
    private readonly List<ClaimParty> _parties = [];
    private readonly List<ClaimRiskObject> _riskObjects = [];
    private readonly List<ClaimReserveComponent> _reserveComponents = [];
    private readonly List<ClaimDocument> _documents = [];

    private Claim() { }

    public ClaimNumber ClaimNumber { get; private set; } = null!;

    public Guid? PolicyId { get; private set; }

    public Policy? Policy { get; private set; }

    public ClaimType ClaimType { get; private set; }

    public ClaimStatus Status { get; private set; }

    public string AssignedHandler { get; private set; } = string.Empty;

    /// <summary>Set when the $10M aggregate reserve cap is exceeded (BR-R-07) — requires manager sign-off.</summary>
    public bool RequiresManagerOverride { get; private set; }

    /// <summary>Set by the SLA monitoring job when a Draft/Open claim goes untouched for 48h+; cleared by any activity on the claim.</summary>
    public bool IsSlaBreached { get; private set; }

    public DateTimeOffset? SlaBreachedAt { get; private set; }

    public LossEvent LossEvent { get; private set; } = null!;

    public IReadOnlyCollection<ClaimParty> Parties => _parties.AsReadOnly();

    public IReadOnlyCollection<ClaimRiskObject> RiskObjects => _riskObjects.AsReadOnly();

    public IReadOnlyCollection<ClaimReserveComponent> ReserveComponents => _reserveComponents.AsReadOnly();

    public IReadOnlyCollection<ClaimDocument> Documents => _documents.AsReadOnly();

    public static Claim Create(
        Guid organizationEntityId,
        ClaimNumber claimNumber,
        Guid? policyId,
        ClaimType claimType,
        string assignedHandler,
        DateTimeOffset lossDate,
        string lossDescription,
        string lossLocation,
        Guid causeOfLossCodeId,
        string createdBy)
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            OrganizationEntityId = organizationEntityId,
            ClaimNumber = claimNumber,
            PolicyId = policyId,
            ClaimType = claimType,
            Status = ClaimStatus.Draft,
            AssignedHandler = assignedHandler
        };

        claim.LossEvent = new LossEvent
        {
            Id = Guid.NewGuid(),
            OrganizationEntityId = organizationEntityId,
            ClaimId = claim.Id,
            LossDate = lossDate,
            Description = lossDescription,
            Location = lossLocation,
            CauseOfLossCodeId = causeOfLossCodeId
        };

        claim.AddDomainEvent(new ClaimCreatedEvent(claim.Id, claim.ClaimNumber, createdBy));
        return claim;
    }

    public ClaimParty AddParty(PartyType partyType, PartyRole partyRole, string name, string? email, string? phone, string addedBy)
    {
        ClearSlaBreach();
        var party = new ClaimParty
        {
            Id = Guid.NewGuid(),
            OrganizationEntityId = OrganizationEntityId,
            ClaimId = Id,
            PartyType = partyType,
            PartyRole = partyRole,
            Name = name,
            ContactEmail = email,
            ContactPhone = phone
        };

        _parties.Add(party);
        AddDomainEvent(new PartyAddedEvent(Id, party.Id, partyRole, addedBy));
        return party;
    }

    public ClaimRiskObject AddRiskObject(AssetType assetType, string description, string? identifier)
    {
        var riskObject = new ClaimRiskObject
        {
            Id = Guid.NewGuid(),
            OrganizationEntityId = OrganizationEntityId,
            ClaimId = Id,
            AssetType = assetType,
            Description = description,
            Identifier = identifier
        };

        _riskObjects.Add(riskObject);
        return riskObject;
    }

    public ClaimDocument AddDocument(string fileName, string sanitizedFileName, string contentType, long sizeBytes, string blobPath, string uploadedBy, DocumentType documentType = DocumentType.Other)
    {
        ClearSlaBreach();
        var document = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            OrganizationEntityId = OrganizationEntityId,
            ClaimId = Id,
            FileName = fileName,
            SanitizedFileName = sanitizedFileName,
            ContentType = contentType,
            DocumentType = documentType,
            SizeBytes = sizeBytes,
            BlobPath = blobPath,
            UploadedBy = uploadedBy
        };

        _documents.Add(document);
        AddDomainEvent(new DocumentUploadedEvent(Id, document.Id, fileName, uploadedBy));
        return document;
    }

    public ClaimReserveComponent OpenReserve(ReserveComponentType componentType, Money amount, ApprovalTier tier, string requestedBy, string? reason = null)
    {
        if (Status is ClaimStatus.Closed or ClaimStatus.Withdrawn)
        {
            throw new InvalidReserveOperationException($"Cannot open a reserve on a claim in '{Status}' status.");
        }

        ClearSlaBreach();
        var component = ClaimReserveComponent.Open(OrganizationEntityId, Id, componentType, amount, tier, requestedBy, reason);
        _reserveComponents.Add(component);
        return component;
    }

    /// <summary>
    /// Applies a status transition already validated against the seeded ClaimStatusTransitions
    /// table by the caller (IClaimStatusTransitionValidator, application layer). Only the
    /// in-memory invariants are enforced here: no same-status "transition" (WF-01), and BR-C-03
    /// (>=1 Claimant before leaving Draft).
    /// </summary>
    public void TransitionTo(ClaimStatus newStatus, string changedBy)
    {
        if (newStatus == Status)
        {
            throw new ClaimInvariantViolationException($"Claim is already in status {Status}.");
        }

        // BR-C-06: the seeded ClaimStatusTransitions table has no Draft → Closed row, but this rule
        // holds whatever the table says — a claim must at least be opened before it is closed.
        if (Status == ClaimStatus.Draft && newStatus == ClaimStatus.Closed)
        {
            throw new ClaimInvariantViolationException("A draft claim cannot be closed directly; open it first.");
        }

        if (Status == ClaimStatus.Draft && !_parties.Any(p => p.PartyRole == PartyRole.Claimant))
        {
            throw new ClaimInvariantViolationException("At least one Claimant party is required before a claim can leave Draft status.");
        }

        var oldStatus = Status;
        Status = newStatus;
        ClearSlaBreach();

        // Reserve lines follow the claim: inactive while it is closed or withdrawn.
        foreach (var component in _reserveComponents)
        {
            if (newStatus is ClaimStatus.Closed or ClaimStatus.Withdrawn)
            {
                component.Close();
            }
            else if (newStatus == ClaimStatus.Reopened)
            {
                component.Reopen();
            }
        }
        AddDomainEvent(new ClaimStatusChangedEvent(Id, oldStatus, newStatus, changedBy));
    }

    /// <summary>Records a non-blocking warning so it lands in the audit trail alongside the change that caused it.</summary>
    public void RaiseWarning(string code, string message, string raisedBy) =>
        AddDomainEvent(new ClaimWarningRaisedEvent(Id, code, message, raisedBy));

    public void FlagManagerOverrideRequired() => RequiresManagerOverride = true;

    /// <summary>Idempotent: re-flagging an already-breached claim changes nothing (the SLA job runs every 15 minutes).</summary>
    public bool FlagSlaBreach(DateTimeOffset detectedAt)
    {
        if (IsSlaBreached)
        {
            return false;
        }

        IsSlaBreached = true;
        SlaBreachedAt = detectedAt;
        return true;
    }

    private void ClearSlaBreach()
    {
        IsSlaBreached = false;
        SlaBreachedAt = null;
    }
}
