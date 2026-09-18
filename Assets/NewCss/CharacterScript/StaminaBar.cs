using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StaminaBar : MonoBehaviour
{
    public Transform loadingBarImage;
    public float TargetAmount = 100.0f;
    public float Speed = 30;

    private float CurrentAmount;
    private Image _fillImage; // Optimizasyon teftisi 2026-09-18: her frame GetComponent yerine Awake'de onbellek

    void Awake()
    {
        _fillImage = loadingBarImage != null ? loadingBarImage.GetComponent<Image>() : null;
    }

    void Start()
    {
        CurrentAmount = TargetAmount;
        if (_fillImage != null) _fillImage.fillAmount = 1f;
    }

    void Update()
    {
        if (CurrentAmount < TargetAmount)
        {
            CurrentAmount += Speed * Time.deltaTime;
            if (_fillImage != null) _fillImage.fillAmount = CurrentAmount / TargetAmount;
        }
    }
}
