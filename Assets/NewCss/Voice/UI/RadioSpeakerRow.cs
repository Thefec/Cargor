using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Telsiz HUD'unda tek bir satır: durum metni ("{isim} konuşuyor" / "{isim} (Sessize Alındı)" /
/// çıplak isim) + nabız atan seviye çubuğu. <see cref="RadioHudController"/> tarafından havuzlanır
/// (Instantiate/Destroy YOK — CreateSlot desenindeki gibi bir kez kurulur, sonra Assign/Release ile
/// yeniden kullanılır).
///
/// PREFAB OVERRIDE DESENİ (RadioVoiceSpeakerSlot.BuildAudioGraphIfNeeded ile BİREBİR aynı):
/// <see cref="EnsureBuilt"/> HER İKİ yolda da (koddan yeni obje / Resources/Voice/RadioHUD prefab'ından
/// Instantiate) çağrılır — GetComponent bulursa (tasarımcı prefab'a elle koymuşsa) ONU kullanır,
/// bulamazsa koddan ekler. Çocuk objeler ("Label", "LevelBar/LevelFill") aynı mantıkla transform.Find
/// ile aranır. Bu yüzden RadioHudController hiçbir zaman "prefab mı kod mu" ayrımı yapmak ZORUNDA
/// DEĞİL — tek bir EnsureBuilt çağrısı her iki senaryoda da doğru referansları doldurur.
///
/// TIKLAMA = MUTE (yalnızca uzak konuşmacı satırları — kendi mikrofon satırı <see cref="SetClickable"/>
/// ile kapalı bırakılır, kendini mute etmek anlamsız). Metin zaten mute durumunu ("Sessize Alındı"
/// varyantı + soluk renk) taşıdığı için AYRI bir mute ikonu YOK — hem daha az glyph/font riski
/// (emoji TMP fallback fontunda kutu gösterebilir) hem tek bir okunabilir kaynak.
/// </summary>
public sealed class RadioSpeakerRow : MonoBehaviour
{
    private static readonly Color ActiveTextColor = Color.white;
    private static readonly Color MutedTextColor = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color LevelFillColor = new Color(0.35f, 1f, 0.45f, 0.9f);

    private CanvasGroup _canvasGroup;
    private TextMeshProUGUI _label;
    private Image _levelFill;
    private Button _button;

    private Action<RadioSpeakerRow> _onClicked;

    /// <summary>null = havuzda boşta. RadioHudController Assign/Release üzerinden değiştirir.</summary>
    public ulong? SpeakerId { get; private set; }

    /// <summary>
    /// Bu satırın görsel ağacını kurar/bağlar — bkz. sınıf yorumu §Prefab override deseni.
    /// RadioHudController.CreateRow HER İKİ yolda da (prefab var/yok) bunu çağırır.
    /// </summary>
    public void EnsureBuilt(float preferredHeight)
    {
        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.flexibleWidth = 1f;

        if (GetComponent<Image>() == null)
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _button = GetComponent<Button>();
        if (_button == null)
        {
            _button = gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None; // radyo filtresi zaten "sanatsal" — highlight/pressed tint gerekmiyor, editor-isleri'nde istenirse eklenir
        }
        _button.onClick.AddListener(HandleClicked);

        var labelTr = transform.Find("Label");
        if (labelTr == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(8f, 3f);
            labelRt.offsetMax = new Vector2(-8f, 0f);

            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 18f;
            _label.color = ActiveTextColor;
            _label.alignment = TextAlignmentOptions.MidlineLeft;
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Ellipsis;
        }
        else
        {
            _label = labelTr.GetComponent<TextMeshProUGUI>();
        }

        var levelBarTr = transform.Find("LevelBar");
        if (levelBarTr == null)
        {
            // Seviye çubuğu — satırın alt kenarında ince bir dolum şeridi.
            var levelBgGo = new GameObject("LevelBar", typeof(RectTransform));
            levelBgGo.transform.SetParent(transform, false);
            var levelBgRt = (RectTransform)levelBgGo.transform;
            levelBgRt.anchorMin = new Vector2(0f, 0f);
            levelBgRt.anchorMax = new Vector2(1f, 0f);
            levelBgRt.pivot = new Vector2(0f, 0f);
            levelBgRt.offsetMin = new Vector2(0f, 0f);
            levelBgRt.offsetMax = new Vector2(0f, 3f);
            var levelBg = levelBgGo.AddComponent<Image>();
            levelBg.color = new Color(1f, 1f, 1f, 0.12f);

            var levelFillGo = new GameObject("LevelFill", typeof(RectTransform));
            levelFillGo.transform.SetParent(levelBgGo.transform, false);
            var levelFillRt = (RectTransform)levelFillGo.transform;
            levelFillRt.anchorMin = Vector2.zero;
            levelFillRt.anchorMax = Vector2.one;
            levelFillRt.offsetMin = Vector2.zero;
            levelFillRt.offsetMax = Vector2.zero;

            _levelFill = levelFillGo.AddComponent<Image>();
            _levelFill.color = LevelFillColor;
            _levelFill.type = Image.Type.Filled;
            _levelFill.fillMethod = Image.FillMethod.Horizontal;
            _levelFill.fillAmount = 0f;
        }
        else
        {
            var levelFillTr = levelBarTr.Find("LevelFill");
            _levelFill = levelFillTr != null ? levelFillTr.GetComponent<Image>() : null;
        }
    }

    /// <summary>Kendi mikrofon satırı tıklanamaz bırakılır (kendini mute etmek anlamsız).</summary>
    public void SetClickable(bool clickable, Action<RadioSpeakerRow> onClicked)
    {
        _onClicked = onClicked;
        if (_button != null) _button.interactable = clickable;
    }

    private void HandleClicked() => _onClicked?.Invoke(this);

    public void Assign(ulong? speakerId) => SpeakerId = speakerId;

    public void SetLabel(string text, bool dimmed)
    {
        if (_label != null)
        {
            _label.text = text;
            _label.color = dimmed ? MutedTextColor : ActiveTextColor;
        }
    }

    public void SetLevel01(float level01)
    {
        if (_levelFill != null) _levelFill.fillAmount = Mathf.Clamp01(level01);
    }

    public void SetAlpha(float alpha)
    {
        if (_canvasGroup != null) _canvasGroup.alpha = alpha;
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);
}
