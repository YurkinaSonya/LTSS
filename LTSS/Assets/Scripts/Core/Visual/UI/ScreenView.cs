using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core.Application;
using Game.Core.Application.UI;

public class ScreenView : MonoBehaviour
{
    public ScreenId Type => _type;

    [SerializeField] private ScreenId _type;
    [SerializeField] private CanvasGroup _canvasGroup;

    private float _alpha;


    public virtual ScreenController Construct(UIContext context)
    {
        return new ScreenController(this, context);
    }

    public void DestroyScreenView()
    {
        Destroy(gameObject);
    }

    public void CloseScreen(Action callback = null)
    {
        StopAllCoroutines();

        _alpha = 0;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
        callback?.Invoke();
    }

    public void OpenScreen(Action callback = null)
    {
        StopAllCoroutines();

        _alpha = 1;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        gameObject.SetActive(true);
        callback?.Invoke();
    }
}
