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

    void Start()
    {
        CurrentAmount = TargetAmount;
        loadingBarImage.GetComponent<Image>().fillAmount = 1f;
    }

    void Update()
    {
        if (CurrentAmount < TargetAmount)
        {
            CurrentAmount += Speed * Time.deltaTime;
            loadingBarImage.GetComponent<Image>().fillAmount = CurrentAmount / TargetAmount;
        }
    }
}
