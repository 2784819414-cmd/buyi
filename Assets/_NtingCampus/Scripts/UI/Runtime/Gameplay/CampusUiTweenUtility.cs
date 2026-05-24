using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public static class CampusUiTweenUtility
    {
        private static readonly Vector2 PanelOpenSlideOffset = new Vector2(0f, -14f);
        private static readonly Vector2 PanelCloseSlideOffset = new Vector2(0f, -10f);

        public static Tween TweenFloat(
            DOGetter<float> getter,
            DOSetter<float> setter,
            float to,
            float duration,
            Ease ease = Ease.OutCubic,
            bool ignoreTimeScale = true)
        {
            if (getter == null || setter == null)
            {
                return null;
            }

            if (duration <= 0f)
            {
                setter(to);
                return null;
            }

            return DOTween.To(getter, setter, to, duration)
                .SetEase(ease)
                .SetUpdate(ignoreTimeScale);
        }

        public static Tween FadeCanvasGroup(CanvasGroup canvasGroup, float to, float duration, Ease ease = Ease.OutCubic)
        {
            if (canvasGroup == null)
            {
                return null;
            }

            return canvasGroup.DOFade(to, duration)
                .SetEase(ease)
                .SetUpdate(true);
        }

        public static Tween ScaleRect(RectTransform rectTransform, float to, float duration, Ease ease = Ease.OutBack)
        {
            if (rectTransform == null)
            {
                return null;
            }

            return rectTransform.DOScale(Vector3.one * to, duration)
                .SetEase(ease)
                .SetUpdate(true);
        }

        public static Tween PunchScale(RectTransform rectTransform, float punchScale = 0.04f, float duration = 0.22f)
        {
            if (rectTransform == null)
            {
                return null;
            }

            return rectTransform.DOPunchScale(
                    new Vector3(punchScale, punchScale, 0f),
                    duration,
                    8,
                    0.8f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        public static Tween PulseText(Text text, Color pulseColor, float duration = 0.16f)
        {
            if (text == null)
            {
                return null;
            }

            return text.DOColor(pulseColor, duration)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        public static Sequence OpenPanel(CanvasGroup canvasGroup, RectTransform panel, float duration = 0.24f, float startScale = 0.96f)
        {
            if (duration <= 0f)
            {
                ApplyPanelRestState(canvasGroup, panel, true);
                return null;
            }

            CampusUiTweenState state = CapturePanelState(panel);
            if (canvasGroup == null && panel == null)
            {
                return null;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                sequence.Join(canvasGroup.DOFade(1f, duration * 0.85f).SetEase(Ease.OutCubic));
            }

            if (panel != null)
            {
                Vector2 targetPosition = state.RestAnchoredPosition;
                Vector3 targetScale = state.RestLocalScale;
                panel.anchoredPosition = targetPosition + PanelOpenSlideOffset;
                panel.localScale = targetScale * Mathf.Max(0f, startScale);
                sequence.Join(panel.DOAnchorPos(targetPosition, duration).SetEase(Ease.OutCubic));
                sequence.Join(panel.DOScale(targetScale, duration).SetEase(Ease.OutBack));
            }

            sequence.OnComplete(() => ApplyPanelRestState(canvasGroup, panel, true));

            return sequence;
        }

        public static Sequence ClosePanel(CanvasGroup canvasGroup, RectTransform panel, float duration = 0.18f, float endScale = 0.96f)
        {
            if (duration <= 0f)
            {
                ApplyPanelRestState(canvasGroup, panel, false);
                return null;
            }

            CampusUiTweenState state = CapturePanelState(panel);
            if (canvasGroup == null && panel == null)
            {
                return null;
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true);

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                sequence.Join(canvasGroup.DOFade(0f, duration * 0.9f).SetEase(Ease.InCubic));
            }

            if (panel != null)
            {
                Vector2 targetPosition = state.RestAnchoredPosition + PanelCloseSlideOffset;
                Vector3 targetScale = state.RestLocalScale * Mathf.Max(0f, endScale);
                sequence.Join(panel.DOAnchorPos(targetPosition, duration).SetEase(Ease.InCubic));
                sequence.Join(panel.DOScale(targetScale, duration).SetEase(Ease.InQuad));
            }

            sequence.OnComplete(() => ApplyPanelRestState(canvasGroup, panel, false));

            return sequence;
        }

        private static CampusUiTweenState CapturePanelState(RectTransform panel)
        {
            if (panel == null)
            {
                return null;
            }

            CampusUiTweenState state = panel.GetComponent<CampusUiTweenState>();
            if (state == null)
            {
                state = panel.gameObject.AddComponent<CampusUiTweenState>();
            }

            state.Capture(panel);
            return state;
        }

        private static void ApplyPanelRestState(CanvasGroup canvasGroup, RectTransform panel, bool visible)
        {
            if (panel != null)
            {
                CampusUiTweenState state = CapturePanelState(panel);
                if (state != null)
                {
                    panel.anchoredPosition = state.RestAnchoredPosition;
                    panel.localScale = state.RestLocalScale;
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }

    }

    [DisallowMultipleComponent]
    public sealed class CampusUiTweenState : MonoBehaviour
    {
        [SerializeField] private Vector2 restAnchoredPosition;
        [SerializeField] private Vector3 restLocalScale = Vector3.one;
        [SerializeField] private bool hasCaptured;

        public bool HasCaptured => hasCaptured;

        public Vector2 RestAnchoredPosition => restAnchoredPosition;

        public Vector3 RestLocalScale => restLocalScale;

        public void Capture(RectTransform panel)
        {
            if (hasCaptured || panel == null)
            {
                return;
            }

            restAnchoredPosition = panel.anchoredPosition;
            restLocalScale = panel.localScale;
            hasCaptured = true;
        }
    }
}
