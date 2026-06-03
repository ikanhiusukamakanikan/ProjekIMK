using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public interface IEndScreenButtonOwner
{
    void HandleEndScreenButtonHovered(string actionId, bool isHovered);
    void HandleEndScreenButtonClicked(string actionId);
}

[System.Serializable]
public class EndScreenButton
{
    public GameObject buttonObject;
    public GameObject hoverObject;
}

public class EndScreenButtonRuntime
{
    private readonly EndScreenButton button;
    private readonly List<Graphic> hoverGraphics = new();
    private readonly List<Renderer> hoverRenderers = new();
    private readonly List<Color> graphicBaseColors = new();
    private readonly List<Color> rendererBaseColors = new();
    private readonly float visibleAlpha;
    private readonly float hiddenAlpha;
    private readonly Vector3 baseScale;
    private CanvasGroup hoverCanvasGroup;
    private Sequence hoverTween;
    private Sequence clickTween;

    public EndScreenButtonRuntime(EndScreenButton button, float visibleAlpha, float hiddenAlpha)
    {
        this.button = button;
        this.visibleAlpha = visibleAlpha;
        this.hiddenAlpha = hiddenAlpha;
        baseScale = button.buttonObject.transform.localScale;
        CacheHoverVisuals();
    }

    public void PlayHover(bool isHovered, float duration, Ease ease)
    {
        if (isHovered)
        {
            SoundManager.PlaySound(SoundType.Hover);
        }

        if (button.hoverObject == null)
        {
            return;
        }

        hoverTween?.Kill();
        button.hoverObject.SetActive(true);

        float targetAlpha = isHovered ? visibleAlpha : hiddenAlpha;
        hoverTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(button.hoverObject);

        if (hoverCanvasGroup != null)
        {
            hoverTween.Join(hoverCanvasGroup.DOFade(targetAlpha, Mathf.Max(0.01f, duration)));
        }

        for (int i = 0; i < hoverGraphics.Count; i++)
        {
            Color targetColor = graphicBaseColors[i];
            targetColor.a = targetAlpha;
            hoverTween.Join(hoverGraphics[i].DOColor(targetColor, Mathf.Max(0.01f, duration)));
        }

        for (int i = 0; i < hoverRenderers.Count; i++)
        {
            Color targetColor = rendererBaseColors[i];
            targetColor.a = targetAlpha;
            hoverTween.Join(hoverRenderers[i].material.DOColor(targetColor, Mathf.Max(0.01f, duration)));
        }

        hoverTween
            .SetEase(ease)
            .OnComplete(() =>
            {
                if (!isHovered)
                {
                    button.hoverObject.SetActive(false);
                }

                hoverTween = null;
            });
    }

    public void PlayClick(
        float clickScale,
        float shrinkDuration,
        float returnDuration,
        Ease shrinkEase,
        Ease returnEase,
        Color darkColor,
        TweenCallback onComplete
    )
    {
        SoundManager.PlaySound(SoundType.Click);
        clickTween?.Kill();
        hoverTween?.Kill();

        if (button.hoverObject != null)
        {
            button.hoverObject.SetActive(true);
        }

        clickTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(button.buttonObject)
            .Append(button.buttonObject.transform.DOScale(baseScale * clickScale, Mathf.Max(0.01f, shrinkDuration)).SetEase(shrinkEase))
            .Join(CreateHoverDarkTween(darkColor, shrinkDuration))
            .Append(button.buttonObject.transform.DOScale(baseScale, Mathf.Max(0.01f, returnDuration)).SetEase(returnEase))
            .OnComplete(() =>
            {
                clickTween = null;
                onComplete?.Invoke();
            });
    }

    public void HideHoverImmediate()
    {
        if (button.hoverObject == null)
        {
            return;
        }

        if (hoverCanvasGroup != null)
        {
            hoverCanvasGroup.alpha = hiddenAlpha;
        }

        for (int i = 0; i < hoverGraphics.Count; i++)
        {
            Color color = graphicBaseColors[i];
            color.a = hiddenAlpha;
            hoverGraphics[i].color = color;
        }

        for (int i = 0; i < hoverRenderers.Count; i++)
        {
            Color color = rendererBaseColors[i];
            color.a = hiddenAlpha;
            hoverRenderers[i].material.color = color;
        }

        button.hoverObject.SetActive(false);
    }

    public void Kill()
    {
        hoverTween?.Kill();
        clickTween?.Kill();
    }

    private void CacheHoverVisuals()
    {
        if (button.hoverObject == null)
        {
            return;
        }

        hoverCanvasGroup = button.hoverObject.GetComponent<CanvasGroup>();
        hoverGraphics.AddRange(button.hoverObject.GetComponentsInChildren<Graphic>(true));
        hoverRenderers.AddRange(button.hoverObject.GetComponentsInChildren<Renderer>(true));

        for (int i = 0; i < hoverGraphics.Count; i++)
        {
            graphicBaseColors.Add(hoverGraphics[i].color);
        }

        for (int i = 0; i < hoverRenderers.Count; i++)
        {
            rendererBaseColors.Add(hoverRenderers[i].material.color);
        }
    }

    private Tween CreateHoverDarkTween(Color darkColor, float duration)
    {
        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        if (hoverCanvasGroup != null)
        {
            sequence.Join(hoverCanvasGroup.DOFade(visibleAlpha, Mathf.Max(0.01f, duration)));
        }

        for (int i = 0; i < hoverGraphics.Count; i++)
        {
            sequence.Join(hoverGraphics[i].DOColor(darkColor, Mathf.Max(0.01f, duration)));
        }

        for (int i = 0; i < hoverRenderers.Count; i++)
        {
            sequence.Join(hoverRenderers[i].material.DOColor(darkColor, Mathf.Max(0.01f, duration)));
        }

        return sequence;
    }
}

public class EndScreenButtonTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private IEndScreenButtonOwner owner;
    private string actionId;
    private Button uiButton;

    public void Setup(IEndScreenButtonOwner owner, string actionId)
    {
        this.owner = owner;
        this.actionId = actionId;
        uiButton = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.HandleEndScreenButtonHovered(actionId, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HandleEndScreenButtonHovered(actionId, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (uiButton != null)
        {
            return;
        }

        owner?.HandleEndScreenButtonClicked(actionId);
    }

    void OnMouseEnter()
    {
        owner?.HandleEndScreenButtonHovered(actionId, true);
    }

    void OnMouseExit()
    {
        owner?.HandleEndScreenButtonHovered(actionId, false);
    }

    void OnMouseDown()
    {
        if (uiButton != null)
        {
            return;
        }

        owner?.HandleEndScreenButtonClicked(actionId);
    }
}
