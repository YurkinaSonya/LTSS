using System;
using System.Collections.Generic;

namespace Game.Core.Application.Session
{
    public interface ISessionFlowCoordinator
    {
        SessionFlowRuntimeState Current { get; }
        event Action<SessionFlowRuntimeState> Changed;

        void StartOrResume();
        void CompleteActiveStep();
        void ApplyPermanentIncomeLoss();
        void SkipActiveStep();
        void SubmitActiveSurvey(IReadOnlyDictionary<string, string> answers);
        void ClearRuntime();
    }
}
