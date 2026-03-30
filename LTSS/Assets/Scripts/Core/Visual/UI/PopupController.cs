using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.UI;

public class PopupController : MonoBehaviour
{  
    [SerializeField] private CanvasGroup _popupCanvas;
    [SerializeField] protected Button _backBGButton;
    [SerializeField] private List<PopupSlotData> _popups;

    private readonly List<Popup> _popupLayouts = new List<Popup>();
    private readonly Dictionary<Enums.PopupType, Popup> _popupRegistry =
        new Dictionary<Enums.PopupType, Popup>();

    private UIContext _uiContext;
    private IPopupNavigationService _popupNavigation;
    private IAppLogger _logger;
    private bool _isInitialized;


    public void Initialize(
        UIContext uiContext,
        IPopupNavigationService popupNavigation,
        IAppLogger logger)
    {
        _uiContext = uiContext;
        _popupNavigation = popupNavigation;
        _logger = logger;

        BuildRegistry();

        if (_backBGButton != null)
        {
            _backBGButton.onClick.RemoveAllListeners();
            _backBGButton.onClick.AddListener(() =>
            {
                _popupNavigation?.Pop("background_back");
            });
        }

        HidePopupContainer();
        _isInitialized = true;
    }

    public void ApplyPopupStack(IReadOnlyList<PopupRoute> popupStack)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (AreStacksEquivalent(popupStack))
        {
            return;
        }

        RebuildPopupStack(popupStack);
    }

    public void ShowPopupContainer()
    {
        if (_popupCanvas == null)
        {
            return;
        }

        _popupCanvas.alpha = 1;
        _popupCanvas.interactable = true;
        _popupCanvas.blocksRaycasts = true;
    }

    public void HidePopupContainer()
    {
        if (_popupCanvas == null)
        {
            return;
        }

        _popupCanvas.alpha = 0;
        _popupCanvas.interactable = false;
        _popupCanvas.blocksRaycasts = false;
    }

    private void BuildRegistry()
    {
        _popupRegistry.Clear();

        if (_popups == null)
        {
            return;
        }

        foreach (var popup in _popups)
        {
            if (popup == null || popup.Popup == null)
            {
                continue;
            }

            if (_popupRegistry.ContainsKey(popup.PopupType))
            {
                continue;
            }

            _popupRegistry.Add(popup.PopupType, popup.Popup);
        }
    }

    private bool AreStacksEquivalent(IReadOnlyList<PopupRoute> popupStack)
    {
        var desiredCount = popupStack?.Count ?? 0;

        if (_popupLayouts.Count != desiredCount)
        {
            return false;
        }

        for (var i = 0; i < desiredCount; i++)
        {
            var existingPopup = _popupLayouts[i];

            if (existingPopup == null || !existingPopup.Route.IsEquivalentTo(popupStack[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void RebuildPopupStack(IReadOnlyList<PopupRoute> popupStack)
    {
        CloseAllCurrentPopups();

        if (popupStack == null || popupStack.Count == 0)
        {
            HidePopupContainer();
            return;
        }

        ShowPopupContainer();

        foreach (var popupRoute in popupStack)
        {
            if (!_popupRegistry.TryGetValue(popupRoute.Type, out var popupPrefab) || popupPrefab == null)
            {
                _logger?.Warning($"Popup prefab is not registered for '{popupRoute.Type}'.");
                continue;
            }

            var popupParent = _popupCanvas != null
                ? _popupCanvas.gameObject.transform
                : transform;

            var popup = Object.Instantiate(popupPrefab, popupParent);
            popup.Initialize(_uiContext, popupRoute, () => _popupNavigation?.Pop("popup_back"));
            _popupLayouts.Add(popup);
        }

        if (_popupLayouts.Count == 0)
        {
            HidePopupContainer();
        }
    }

    private void CloseAllCurrentPopups()
    {
        foreach (var popup in _popupLayouts)
        {
            if (popup == null)
            {
                continue;
            }

            popup.Close();
        }

        _popupLayouts.Clear();
    }
}


[System.Serializable] 
public class PopupSlotData 
{
    public Popup Popup;
    public Enums.PopupType PopupType; 
}
