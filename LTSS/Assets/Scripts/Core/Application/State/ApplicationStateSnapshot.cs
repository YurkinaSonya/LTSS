using System;
using System.Collections.Generic;
using Game.Core.Application.Navigation;
using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class ApplicationStateSnapshot
    {
        private readonly List<PopupRoute> _popupStack;

        public static ApplicationStateSnapshot Default =>
            new ApplicationStateSnapshot(
                AppStateId.None,
                ScreenId.None,
                GameFlowStage.None,
                GameFlowPhase.None,
                false,
                string.Empty,
                string.Empty,
                null);

        public AppStateId AppStateId { get; }
        public ScreenId CurrentScreen { get; }
        public GameFlowStage GameFlowStage { get; }
        public GameFlowPhase GameFlowPhase { get; }
        public bool IsBusy { get; }
        public string StatusMessage { get; }
        public string LastError { get; }
        public int PopupCount => _popupStack.Count;
        public IReadOnlyList<PopupRoute> PopupStack => _popupStack;

        public ApplicationStateSnapshot(
            AppStateId appStateId,
            ScreenId currentScreen,
            GameFlowStage gameFlowStage,
            GameFlowPhase gameFlowPhase,
            bool isBusy,
            string statusMessage,
            string lastError,
            IEnumerable<PopupRoute> popupStack)
        {
            AppStateId = appStateId;
            CurrentScreen = currentScreen;
            GameFlowStage = gameFlowStage;
            GameFlowPhase = gameFlowPhase;
            IsBusy = isBusy;
            StatusMessage = statusMessage ?? string.Empty;
            LastError = lastError ?? string.Empty;
            _popupStack = popupStack != null
                ? new List<PopupRoute>(popupStack)
                : new List<PopupRoute>();
        }

        public ApplicationStateSnapshot With(
            AppStateId? appStateId = null,
            ScreenId? currentScreen = null,
            GameFlowStage? gameFlowStage = null,
            GameFlowPhase? gameFlowPhase = null,
            bool? isBusy = null,
            string statusMessage = null,
            string lastError = null,
            IEnumerable<PopupRoute> popupStack = null)
        {
            return new ApplicationStateSnapshot(
                appStateId ?? AppStateId,
                currentScreen ?? CurrentScreen,
                gameFlowStage ?? GameFlowStage,
                gameFlowPhase ?? GameFlowPhase,
                isBusy ?? IsBusy,
                statusMessage ?? StatusMessage,
                lastError ?? LastError,
                popupStack ?? _popupStack);
        }

        public ApplicationStateSnapshot PushPopup(PopupRoute route)
        {
            if (route == null)
            {
                return this;
            }

            var nextStack = new List<PopupRoute>(_popupStack)
            {
                route
            };

            return With(popupStack: nextStack);
        }

        public ApplicationStateSnapshot PopPopup()
        {
            if (_popupStack.Count == 0)
            {
                return this;
            }

            var nextStack = new List<PopupRoute>(_popupStack);
            nextStack.RemoveAt(nextStack.Count - 1);

            return With(popupStack: nextStack);
        }

        public ApplicationStateSnapshot ClearPopups()
        {
            if (_popupStack.Count == 0)
            {
                return this;
            }

            return With(popupStack: Array.Empty<PopupRoute>());
        }
    }
}
