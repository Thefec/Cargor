using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NewCss.Audio;

public class UIButtonSoundManager : MonoBehaviour
{
    [Header("Sound Settings")]
    public AudioClip buttonClickSound;
    public AudioClip buttonHoverSound;

    [Range(0f, 1f)]
    public float clickVolume = 1f;
    [Range(0f, 1f)]
    public float hoverVolume = 0.5f;

    private AudioSource audioSource;

    void Awake()
    {
        // AudioSource olu�tur
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D ses
        AudioRouting.Route(audioSource, AudioCategory.SFX);
    }

    void Start()
    {
        // Sahnedeki t�m butonlar� bul
        Button[] allButtons = FindObjectsOfType<Button>(true);

        foreach (Button button in allButtons)
        {
            // T�klama sesi ekle
            button.onClick.AddListener(() => PlayClickSound());

            // Hover efekti ekle (opsiyonel)
            AddHoverSound(button.gameObject);
        }
    }

    void PlayClickSound()
    {
        if (buttonClickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonClickSound, clickVolume);
        }
    }

    void PlayHoverSound()
    {
        if (buttonHoverSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonHoverSound, hoverVolume);
        }
    }

    void AddHoverSound(GameObject buttonObject)
    {
        EventTrigger trigger = buttonObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = buttonObject.AddComponent<EventTrigger>();
        }

        // Hover event ekle
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) => { PlayHoverSound(); });
        trigger.triggers.Add(entry);
    }
}