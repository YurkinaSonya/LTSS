using System.Collections.Generic;
using Game.Core.Application.Logging;
using Game.Core.Application.State;

namespace Game.Core.Application.Navigation
{
    public sealed class PopupNavigationService : IPopupNavigationService
    {
        private readonly IApplicationStateStore _stateStore;
        private readonly IUserActionLogger _userActionLogger;

        public PopupNavigationService(
            IApplicationStateStore stateStore,
            IUserActionLogger userActionLogger)
        {
            _stateStore = stateStore;
            _userActionLogger = userActionLogger;
        }

        public void Push(Enums.PopupType type, string source = null)
        {
            Push(new PopupRoute(type, source));
        }

        public void Push(PopupRoute route)
        {
            if (route == null)
            {
                return;
            }

            _stateStore.SetState(state => state.PushPopup(route));
            _userActionLogger.Log(
                UserActionType.PopupOpened,
                route.Type.ToString(),
                BuildMetadata(route));
        }

        public void Pop(string source = null)
        {
            var currentState = _stateStore.Current;

            if (currentState.PopupCount == 0)
            {
                return;
            }

            var topPopup = currentState.PopupStack[currentState.PopupCount - 1];

            if (topPopup != null
                && topPopup.Type == Enums.PopupType.InterPeriodBlock
                && !string.Equals(source, "inter_period_block_continue", System.StringComparison.Ordinal))
            {
                return;
            }

            _stateStore.SetState(state => state.PopPopup());

            var metadata = BuildMetadata(topPopup);

            if (!string.IsNullOrEmpty(source))
            {
                metadata["trigger"] = source;
            }

            _userActionLogger.Log(
                UserActionType.PopupClosed,
                topPopup.Type.ToString(),
                metadata);
        }

        public void Clear(string source = null)
        {
            var currentCount = _stateStore.Current.PopupCount;

            if (currentCount == 0)
            {
                return;
            }

            _stateStore.SetState(state => state.ClearPopups());

            var metadata = new Dictionary<string, string>
            {
                { "count", currentCount.ToString() }
            };

            if (!string.IsNullOrEmpty(source))
            {
                metadata["trigger"] = source;
            }

            _userActionLogger.Log(
                UserActionType.PopupClosed,
                "clear_all",
                metadata);
        }

        private static Dictionary<string, string> BuildMetadata(PopupRoute route)
        {
            var metadata = new Dictionary<string, string>
            {
                { "popup", route.Type.ToString() }
            };

            if (!string.IsNullOrEmpty(route.Source))
            {
                metadata["source"] = route.Source;
            }

            return metadata;
        }
    }
}
