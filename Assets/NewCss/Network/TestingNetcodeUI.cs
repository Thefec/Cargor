using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class TestingNetcodeUI : MonoBehaviour
{
    [SerializeField] private Button StartHostButton;
    [SerializeField] private Button StartClientButton;

    private void Awake()
    {
        StartHostButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
            Hide();
        });
        StartClientButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
            Hide();
        });
        
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}