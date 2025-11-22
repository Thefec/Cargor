using UnityEngine;
using System.Collections;

public class ButtonSlideAnimation : MonoBehaviour
{
    [Header("Buton Ayarlarý")]
    [SerializeField] private RectTransform[] buttons; // 5 butonu buraya sürükle

    [Header("Animasyon Ayarlarý")]
    [SerializeField] private float slideDistance = 1000f; // Saðdan ne kadar uzaktan baþlayacak
    [SerializeField] private float slideDuration = 0.5f; // Her butonun kayma süresi
    [SerializeField] private float delayBetweenButtons = 0.1f; // Butonlar arasý gecikme
    [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Otomatik Baþlat")]
    [SerializeField] private bool playOnStart = true;

    private Vector2[] originalPositions;

    void Start()
    {
        // Orijinal pozisyonlarý kaydet
        originalPositions = new Vector2[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            originalPositions[i] = buttons[i].anchoredPosition;

            // Butonlarý baþlangýçta saðda gizle
            buttons[i].anchoredPosition = new Vector2(
                originalPositions[i].x + slideDistance,
                originalPositions[i].y
            );
        }

        if (playOnStart)
        {
            PlayAnimation();
        }
    }

    public void PlayAnimation()
    {
        StartCoroutine(AnimateButtons());
    }

    private IEnumerator AnimateButtons()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            // Her buton için animasyon baþlat
            StartCoroutine(SlideButton(buttons[i], originalPositions[i]));

            // Bir sonraki buton için bekle
            yield return new WaitForSeconds(delayBetweenButtons);
        }
    }

    private IEnumerator SlideButton(RectTransform button, Vector2 targetPosition)
    {
        Vector2 startPosition = button.anchoredPosition;
        float elapsedTime = 0f;

        while (elapsedTime < slideDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / slideDuration;

            // Animation curve uygula
            float curveValue = slideCurve.Evaluate(t);

            button.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, curveValue);

            yield return null;
        }

        // Tam pozisyona ayarla
        button.anchoredPosition = targetPosition;
    }

    // Ýsteðe baðlý: Geri kaydýrma animasyonu
    public void PlayReverseAnimation()
    {
        StartCoroutine(ReverseAnimateButtons());
    }

    private IEnumerator ReverseAnimateButtons()
    {
        // Aþaðýdan yukarý doðru geri kaydýr
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            Vector2 hidePosition = new Vector2(
                originalPositions[i].x + slideDistance,
                originalPositions[i].y
            );

            StartCoroutine(SlideButton(buttons[i], hidePosition));
            yield return new WaitForSeconds(delayBetweenButtons);
        }
    }
}