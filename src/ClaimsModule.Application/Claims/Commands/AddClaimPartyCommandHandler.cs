using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands;

public sealed class AddClaimPartyCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<AddClaimPartyCommand, ClaimPartyDto>
{
    public async Task<ClaimPartyDto> Handle(AddClaimPartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims.Include(c => c.Parties)
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Claim), request.ClaimId);

        var party = claim.AddParty(request.PartyType, request.PartyRole, request.Name, request.ContactEmail, request.ContactPhone, currentUser.UserName);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<ClaimPartyDto>(party);
    }
}
