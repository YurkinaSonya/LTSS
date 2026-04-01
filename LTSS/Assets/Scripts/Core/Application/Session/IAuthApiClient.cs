using System;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    public interface IAuthApiClient
    {
        void Login(
            LoginRequestDto request,
            Action<ApiResponse<LoginResponseDto>> onCompleted = null);
    }
}
