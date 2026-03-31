using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Core.Application;
using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.Networking;
using Game.Core.Application.State;
using Game.Core.Application.UI;
using Zenject;

public class UIController : MonoBehaviour, IInitializable, IDisposable
{
    public ScreenController CurrentScreen => _currentScreen;

    [Header ("UI components")]
    [SerializeField] private ScreenView[] _views;

    [Header ("other controllers")]
    [SerializeField] private PopupController popupController;

    private readonly Dictionary<ScreenId, ScreenView> _viewsByType =
        new Dictionary<ScreenId, ScreenView>();

    private ScreenController _currentScreen;
    private ScreenView _currentView;
    private ScreenId _currentScreenId = ScreenId.None;
    private IApplicationStateStore _stateStore;
    private IPopupNavigationService _popupNavigation;
    private IUserActionLogger _userActionLogger;
    private IAppLogger _appLogger;
    private UIContext _uiContext;
    private bool _isInitialized;

    [Inject]
    public void Construct(
        IApplicationStateStore stateStore,
        IPopupNavigationService popupNavigation,
        IApplicationNavigationService navigation,
        IGameSessionService gameSessionService,
        IUserActionLogger userActionLogger,
        IAppLogger appLogger,
        IEventAggregator eventAggregator,
        IApiClient apiClient)
    {
        _stateStore = stateStore;
        _popupNavigation = popupNavigation;
        _userActionLogger = userActionLogger;
        _appLogger = appLogger;
        _uiContext = new UIContext(
            navigation,
            popupNavigation,
            gameSessionService,
            userActionLogger,
            appLogger,
            eventAggregator,
            stateStore,
            apiClient);
    }

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        BuildScreenRegistry();
        popupController?.Initialize(_uiContext, _popupNavigation, _appLogger);

        if (_stateStore != null)
        {
            _stateStore.StateChanged += ApplyState;
            ApplyState(_stateStore.Current);
        }

        _isInitialized = true;
    }

    public void Dispose()
    {
        if (_stateStore != null)
        {
            _stateStore.StateChanged -= ApplyState;
        }

        Clear();
    }

    private void BuildScreenRegistry()
    {
        _viewsByType.Clear();

        if (_views == null)
        {
            return;
        }

        foreach (var view in _views)
        {
            if (view == null)
            {
                continue;
            }

            if (_viewsByType.ContainsKey(view.Type))
            {
                _appLogger?.Warning($"Duplicate screen registration detected for '{view.Type}'.");
                continue;
            }

            _viewsByType.Add(view.Type, view);
        }
    }

    private void ApplyState(ApplicationStateSnapshot state)
    {
        ApplyScreen(state.CurrentScreen);
        popupController?.ApplyPopupStack(state.PopupStack);
    }

    private void ApplyScreen(ScreenId screenId)
    {
        if (_currentScreen != null && screenId == _currentScreenId)
        {
            return;
        }

        Clear();

        if (screenId == ScreenId.None)
        {
            return;
        }

        if (!_viewsByType.TryGetValue(screenId, out var view) || view == null)
        {
            _appLogger?.Warning($"No screen view is registered for '{screenId}'.");
            return;
        }

        _currentView = Instantiate(view, transform);
        _currentScreen = _currentView.Construct(_uiContext);
        _currentScreen.Open();
        _currentScreenId = screenId;

        _userActionLogger?.Log(UserActionType.ScreenShown, screenId.ToString());
    }

    public void Clear()
    {
        if (_currentScreen != null)
        {
            _currentScreen.Close();
        }

        _currentScreen = null;
        _currentView = null;
        _currentScreenId = ScreenId.None;
    }
}
