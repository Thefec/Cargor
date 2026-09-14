using NewCss;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;



public partial class PlayerInventory : NetworkBehaviour
{
    #region Audio System

    private enum SoundType
    {
        Pickup,
        Drop,
        PlaceOnTable,
        PlaceOnShelf,
        TakeFromTable,
        TakeFromShelf
    }

    private void PlaySound(SoundType soundType)
    {
        var clip = GetAudioClip(soundType);
        if (clip == null || _audioSource == null) return;

        UpdateAudioVolume();
        _audioSource.PlayOneShot(clip);

        if (IsOwner)
        {
            PlayInventorySoundServerRpc((int)soundType);
        }
    }

    private AudioClip GetAudioClip(SoundType soundType)
    {
        return soundType switch
        {
            SoundType.Pickup => pickupSound,
            SoundType.Drop => dropSound,
            SoundType.PlaceOnTable => placeOnTableSound ?? dropSound,
            SoundType.PlaceOnShelf => placeOnShelfSound ?? dropSound,
            SoundType.TakeFromTable => takeFromTableSound ?? pickupSound,
            SoundType.TakeFromShelf => takeFromShelfSound ?? pickupSound,
            _ => null
        };
    }

    /// <summary>
    /// DÜZELTME (QA, çifte ölçekleme): SFX/Master kısılması artık AudioRouting ile SFX mixer
    /// grubuna yönlendirilen _audioSource üzerinden CargorMixer'da uygulanıyor (bkz.
    /// UnifiedSettingsManager.ApplyMixerCategoryVolume). Burada AYRICA
    /// GetSFXVolume()*GetMasterVolume() çarpılırsa ses iki kez kısılır — kaynak volume'u
    /// SADECE tasarım değerini (inventorySoundVolume) taşır.
    /// </summary>
    private void UpdateAudioVolume()
    {
        if (_audioSource == null) return;

        _audioSource.volume = inventorySoundVolume;
    }

    [ServerRpc]
    private void PlayInventorySoundServerRpc(int soundTypeIndex)
    {
        PlayInventorySoundClientRpc(soundTypeIndex);
    }

    [ClientRpc]
    private void PlayInventorySoundClientRpc(int soundTypeIndex)
    {
        if (IsOwner || _audioSource == null) return;

        var clip = GetAudioClip((SoundType)soundTypeIndex);
        if (clip != null)
        {
            UpdateAudioVolume();
            _audioSource.PlayOneShot(clip);
        }
    }

    #endregion
}