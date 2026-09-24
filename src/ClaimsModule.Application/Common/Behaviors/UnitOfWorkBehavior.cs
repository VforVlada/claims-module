using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Common.Behaviors;

/// <summary>
/// Wraps command handlers in a transaction so a handler's own IApplicationDbContext.SaveChangesAsync
/// call (and any raw-SQL round trip, e.g. IClaimNumberGenerator) share one atomic unit — this
/// behavior does not call SaveChanges itself, so handlers get server-generated values (ids,
/// audit timestamps, row versions) back before building their response DTO. Queries (requests
/// that are not ICommand) pass straight through untouched.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommand)
        {
            return await next();
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next();
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return response;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
