using System;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    public interface IRunApiClient
    {
        void GetCurrent(
            string bearerToken,
            Action<ApiResponse<RunInfoDto>> onCompleted = null);

        void GetBootstrap(
            string runId,
            string bearerToken,
            Action<ApiResponse<BootstrapResponseDto>> onCompleted = null);
    }
}
