using UnityEngine;
using UnityEngine.UI;
using System;
using Game.Core.Application.Navigation;
using Game.Core.Application.UI;

public abstract class Popup : MonoBehaviour
{ 
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] protected Button _backButton;

    protected UIContext Context { get; private set; }
    public PopupRoute Route { get; private set; }

    public void Initialize(
        UIContext context,
        PopupRoute route,
        Action backCallback)
    {
        Context = context;
        Route = route;

        if (_backButton != null)
        {
            _backButton.onClick.RemoveAllListeners();
            _backButton.onClick.AddListener(() => backCallback?.Invoke());
        }

        OnInitialize();
        Open();
    }

    protected virtual void OnInitialize()
    {
    }

    public virtual void Open()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        gameObject.SetActive(true);
    }

    public virtual void Close(Action callback = null)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        callback?.Invoke();
        Destroy(gameObject);
    }
}
