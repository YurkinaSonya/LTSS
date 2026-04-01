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
    public abstract Enums.PopupType PopupType { get; }

    public void Initialize(
        UIContext context,
        PopupRoute route,
        Action backCallback)
    {
        EnsureInfrastructure();
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
        EnsureInfrastructure();

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
        EnsureInfrastructure();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        callback?.Invoke();
        Destroy(gameObject);
    }

    private void EnsureInfrastructure()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        var rectTransform = transform as RectTransform;

        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
