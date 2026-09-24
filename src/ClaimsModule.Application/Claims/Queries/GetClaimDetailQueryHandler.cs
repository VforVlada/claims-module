using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ClaimsModule.Application.Claims;

namespace ClaimsModule.Application.Claims.Queries;

public sealed class GetClaimDetailQueryHandler(IApplicationDbContext context, IMapper mapper)
    : IRequestHandler<GetClaimDetailQuery, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(GetClaimDetailQuery request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims.AsNoTracking().IncludeFullGraph()
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Claim), request.ClaimId);

        return mapper.Map<ClaimDetailDto>(claim);
    }
}
