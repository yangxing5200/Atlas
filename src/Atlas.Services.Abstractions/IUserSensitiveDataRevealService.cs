using Atlas.Models.Requests;
using Atlas.Models.Responses;

namespace Atlas.Services.Abstractions;

public interface IUserSensitiveDataRevealService
{
    Task<RevealSensitiveFieldsResponse> RevealUserFieldsAsync(
        long userId,
        RevealSensitiveFieldsRequest request,
        CancellationToken ct = default);

    Task<RevealSensitiveFieldsResponse> RevealLoginLogFieldsAsync(
        long loginLogId,
        RevealSensitiveFieldsRequest request,
        CancellationToken ct = default);
}
