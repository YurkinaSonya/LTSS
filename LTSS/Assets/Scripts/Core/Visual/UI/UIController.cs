using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Core;
using Game.Core.Application;
using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.Networking;
using Game.Core.Application.Periods;
using Game.Core.Application.Session;
using Game.Core.Application.State;
using Game.Core.Application.UI;
using Zenject;

public class UIController : MonoBehaviour, IInitializable, IDisposable
{
    private const string ScreenResourcePath = "UI/Screens";
    private const int MainCanvasSortingOrder = 0;

    public ScreenController CurrentScreen => _currentScreen;

    [Header ("UI components")]
    [SerializeField] private ScreenView[] _views;

    [Header ("other controllers")]
    [SerializeField] private PopupController popupController;

    private readonly Dictionary<ScreenId, ScreenView> _viewsByType =
        new Dictionary<ScreenId, ScreenView>();
    private readonly Dictionary<ScreenId, Type> _runtimeScreenTypes =
        new Dictionary<ScreenId, Type>();

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
        ISessionCoordinator sessionCoordinator,
        ISessionFlowCoordinator sessionFlowCoordinator,
        IPeriodGameplayService periodGameplayService,
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
            sessionCoordinator,
            sessionFlowCoordinator,
            periodGameplayService,
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

        EnsureCanvasInfrastructure();
        EnsureEventSystem();
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
        _runtimeScreenTypes.Clear();

        RegisterViews(_views);

        var resourcePrefabs = Resources.LoadAll<GameObject>(ScreenResourcePath);

        if (resourcePrefabs == null)
        {
            return;
        }

        foreach (var prefab in resourcePrefabs)
        {
            if (prefab == null)
            {
                continue;
            }

            var view = prefab.GetComponent<ScreenView>();

            if (view == null)
            {
                continue;
            }

            RegisterView(view);
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || !typeof(ScreenView).IsAssignableFrom(type))
                {
                    continue;
                }

                var attribute = Attribute.GetCustomAttribute(type, typeof(ScreenDefinitionAttribute))
                    as ScreenDefinitionAttribute;

                if (attribute == null
                    || _viewsByType.ContainsKey(attribute.ScreenId)
                    || _runtimeScreenTypes.ContainsKey(attribute.ScreenId))
                {
                    continue;
                }

                _runtimeScreenTypes.Add(attribute.ScreenId, type);
            }
        }

        _appLogger?.Info($"UI screen registry built. Registered screens: {_viewsByType.Count}.");
    }

    private void RegisterViews(ScreenView[] views)
    {
        if (views == null)
        {
            return;
        }

        foreach (var view in views)
        {
            RegisterView(view);
        }
    }

    private void RegisterView(ScreenView view)
    {
        if (view == null)
        {
            return;
        }

        if (_viewsByType.ContainsKey(view.Type))
        {
            _appLogger?.Warning($"Duplicate screen registration detected for '{view.Type}'.");
            return;
        }

        _viewsByType.Add(view.Type, view);
    }

    private void EnsureCanvasInfrastructure()
    {
        var rectTransform = transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localPosition = Vector3.zero;
        }

        var canvas = GetComponent<Canvas>();

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = MainCanvasSortingOrder;
            canvas.pixelPerfect = false;
        }

        var scaler = GetComponent<CanvasScaler>();

        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        var projectContext = GetComponentInParent<ProjectContext>();

        if (projectContext != null)
        {
            eventSystemObject.transform.SetParent(projectContext.transform, false);
        }

        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
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

        if (_viewsByType.TryGetValue(screenId, out var view) && view != null)
        {
            _currentView = Instantiate(view, transform);
        }
        else if (_runtimeScreenTypes.TryGetValue(screenId, out var runtimeType) && runtimeType != null)
        {
            var screenObject = new GameObject($"{screenId}Screen", typeof(RectTransform), typeof(CanvasGroup));
            screenObject.transform.SetParent(transform, false);
            _currentView = screenObject.AddComponent(runtimeType) as ScreenView;
        }

        if (_currentView == null)
        {
            _appLogger?.Warning($"No screen view is registered for '{screenId}'.");
            return;
        }
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
