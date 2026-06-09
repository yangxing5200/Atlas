using Atlas.Models.Requests;
using Atlas.Models.Responses;

namespace Atlas.Services.Abstractions;

public interface IUserAssignmentService
{
    Task<OperationResult> AssignStoresAsync(AssignStoresRequest request);

    Task<OperationResult> AssignRolesAsync(AssignRolesRequest request);
}
