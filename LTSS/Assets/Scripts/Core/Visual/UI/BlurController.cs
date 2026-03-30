using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class BlurController : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private CustomPassVolume _sceneBlur;

    public void Initialize()
    {
        SetBlur(false);
    }

    public void SetBlur(bool isActive)
    {
        if (_sceneBlur != null)
        {
            if (_sceneBlur.targetCamera == null)
            {
                _sceneBlur.targetCamera = Camera.main;
            }

            _sceneBlur.gameObject.SetActive(isActive);
        }

        if (_canvas == null)
        {
            return;
        }

        if (isActive)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = Camera.main;
            _canvas.planeDistance = 1f;
        }
        else
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = null;
        }
    }
}
