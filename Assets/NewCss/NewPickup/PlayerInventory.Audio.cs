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

    private void UpdateAudioVolume()
    {
        if (_audioSource == null) return;

        float finalVolume = inventorySoundVolume;

        if (_settingsManager != null)
        {
            finalVolume *= _settingsManager.GetSFXVolume() * _settingsManager.GetMasterVolume();
        }

        _audioSource.volume = finalVolume;
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