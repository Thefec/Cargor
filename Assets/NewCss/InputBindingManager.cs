using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyun içi tüm tuş atamalarını merkezi olarak yöneten static manager.
/// PlayerPrefs'e kaydeder, tüm scriptler buradan okur.
/// </summary>
public static class InputBindingManager
{
    #region Enums

    public enum GameAction
    {
        Pickup,     // Eşya Al (default: Sol Tık)
        Drop,       // Eşya Bırak (default: Sağ Tık)
        Interact,   // Etkileşim (default: E)
        Sprint,     // Koşma (default: Left Shift)
        Zoom,       // Yakınlaştır (default: Z)
        MapView,    // Harita (default: X)
        Reveal,     // İnceleme (default: C)
        MoveUp,     // Yukarı Git (default: W)
        MoveDown,   // Aşağı Git (default: S)
        MoveLeft,   // Sola Git (default: A)
        MoveRight   // Sağa Git (default: D)
    }

    #endregion

    #region Binding Class

    public class Binding
    {
        public bool IsMouse;
        public int MouseButton; // 0=left, 1=right, 2=middle
        public KeyCode Key;

        public Binding(KeyCode key)
        {
            IsMouse = false;
            Key = key;
            MouseButton = -1;
        }

        public Binding(int mouseButton)
        {
            IsMouse = true;
            MouseButton = mouseButton;
            Key = KeyCode.None;
        }
    }

    #endregion

    #region Defaults

    private static readonly Dictionary<GameAction, Binding> Defaults = new()
    {
        { GameAction.Pickup,   new Binding(0) },                    // Sol Tık
        { GameAction.Drop,     new Binding(1) },                    // Sağ Tık
        { GameAction.Interact, new Binding(KeyCode.E) },            // E
        { GameAction.Sprint,   new Binding(KeyCode.LeftShift) },    // Left Shift
        { GameAction.Zoom,     new Binding(KeyCode.Z) },            // Z
        { GameAction.MapView,  new Binding(KeyCode.X) },            // X
        { GameAction.Reveal,   new Binding(KeyCode.C) },            // C
        { GameAction.MoveUp, new Binding(KeyCode.W) },              // W
        { GameAction.MoveDown, new Binding(KeyCode.S) },            // S
        { GameAction.MoveLeft, new Binding(KeyCode.A) },            // A
        { GameAction.MoveRight, new Binding(KeyCode.D) }            // D
    };

    #endregion

    #region State

    private static Dictionary<GameAction, Binding> _current;
    private static bool _initialized;

    #endregion

    #region Initialization

    public static void Initialize()
    {
        if (_initialized) return;

        _current = new Dictionary<GameAction, Binding>();

        foreach (var kvp in Defaults)
        {
            string prefKey = $"KeyBind_{kvp.Key}";

            if (PlayerPrefs.HasKey(prefKey + "_IsMouse"))
            {
                bool isMouse = PlayerPrefs.GetInt(prefKey + "_IsMouse") == 1;

                if (isMouse)
                    _current[kvp.Key] = new Binding(PlayerPrefs.GetInt(prefKey + "_Mouse"));
                else
                    _current[kvp.Key] = new Binding((KeyCode)PlayerPrefs.GetInt(prefKey + "_Key"));
            }
            else
            {
                _current[kvp.Key] = new Binding(
                    kvp.Value.IsMouse ? (KeyCode)(-1) : kvp.Value.Key
                );

                if (kvp.Value.IsMouse)
                    _current[kvp.Key] = new Binding(kvp.Value.MouseButton);
                else
                    _current[kvp.Key] = new Binding(kvp.Value.Key);
            }
        }

        _initialized = true;
        Debug.Log("[InputBindingManager] Tüm tuş atamaları yüklendi.");
    }

    #endregion

    #region Input Queries

    /// <summary>
    /// Tuşa basıldığı an (tek frame)
    /// </summary>
    public static bool GetActionDown(GameAction action)
    {
        EnsureInitialized();
        var binding = _current[action];

        if (binding.IsMouse)
            return Input.GetMouseButtonDown(binding.MouseButton);

        return Input.GetKeyDown(binding.Key);
    }

    /// <summary>
    /// Tuş basılı tutulurken (sürekli)
    /// </summary>
    public static bool GetAction(GameAction action)
    {
        EnsureInitialized();
        var binding = _current[action];

        if (binding.IsMouse)
            return Input.GetMouseButton(binding.MouseButton);

        return Input.GetKey(binding.Key);
    }

    /// <summary>
    /// Tuş bırakıldığı an (tek frame)
    /// </summary>
    public static bool GetActionUp(GameAction action)
    {
        EnsureInitialized();
        var binding = _current[action];

        if (binding.IsMouse)
            return Input.GetMouseButtonUp(binding.MouseButton);

        return Input.GetKeyUp(binding.Key);
    }

    #endregion

    #region Binding Management

    public static Binding GetBinding(GameAction action)
    {
        EnsureInitialized();
        return _current[action];
    }

    public static void SetBinding(GameAction action, KeyCode key)
    {
        EnsureInitialized();
        _current[action] = new Binding(key);
        SaveBinding(action);
        Debug.Log($"[InputBindingManager] {action} → {FormatKeyName(key)}");
    }

    public static void SetBinding(GameAction action, int mouseButton)
    {
        EnsureInitialized();
        _current[action] = new Binding(mouseButton);
        SaveBinding(action);
        Debug.Log($"[InputBindingManager] {action} → Mouse {mouseButton}");
    }

    public static void ResetToDefaults()
    {
        EnsureInitialized();

        foreach (var kvp in Defaults)
        {
            if (kvp.Value.IsMouse)
                _current[kvp.Key] = new Binding(kvp.Value.MouseButton);
            else
                _current[kvp.Key] = new Binding(kvp.Value.Key);

            SaveBinding(kvp.Key);
        }

        Debug.Log("[InputBindingManager] Tüm tuş atamaları varsayılana sıfırlandı.");
    }

    public static void ResetBinding(GameAction action)
    {
        EnsureInitialized();
        var def = Defaults[action];

        if (def.IsMouse)
            _current[action] = new Binding(def.MouseButton);
        else
            _current[action] = new Binding(def.Key);

        SaveBinding(action);
    }

    public static bool IsKeyBound(KeyCode key, GameAction? excludeAction = null)
    {
        EnsureInitialized();
        foreach (var kvp in _current)
        {
            if (excludeAction.HasValue && kvp.Key == excludeAction.Value) continue;
            if (!kvp.Value.IsMouse && kvp.Value.Key == key) return true;
        }
        return false;
    }

    public static bool IsMouseBound(int mouseButton, GameAction? excludeAction = null)
    {
        EnsureInitialized();
        foreach (var kvp in _current)
        {
            if (excludeAction.HasValue && kvp.Key == excludeAction.Value) continue;
            if (kvp.Value.IsMouse && kvp.Value.MouseButton == mouseButton) return true;
        }
        return false;
    }

    #endregion

    #region Display Names

    public static string GetBindingDisplayName(GameAction action)
    {
        EnsureInitialized();
        var binding = _current[action];

        if (binding.IsMouse)
        {
            return binding.MouseButton switch
            {
                0 => "Sol Tık",
                1 => "Sağ Tık",
                2 => "Orta Tık",
                _ => $"Mouse {binding.MouseButton}"
            };
        }

        return FormatKeyName(binding.Key);
    }

    public static string GetActionDisplayName(GameAction action)
    {
        return action switch
        {
            GameAction.Pickup   => "Eşya Al",
            GameAction.Drop     => "Eşya Bırak",
            GameAction.Interact => "Etkileşim",
            GameAction.Sprint   => "Koşma",
            GameAction.Zoom     => "Yakınlaştır",
            GameAction.MapView  => "Harita",
            GameAction.Reveal   => "İnceleme",
            GameAction.MoveUp => "Yukarı Git",
            GameAction.MoveDown => "Aşağı Git",
            GameAction.MoveLeft => "Sola Git",
            GameAction.MoveRight => "Sağa Git",
            _ => action.ToString()
        };
    }

    private static string FormatKeyName(KeyCode key)
    {
        return key switch
        {
            KeyCode.LeftShift    => "L-Shift",
            KeyCode.RightShift   => "R-Shift",
            KeyCode.LeftControl  => "L-Ctrl",
            KeyCode.RightControl => "R-Ctrl",
            KeyCode.LeftAlt      => "L-Alt",
            KeyCode.RightAlt     => "R-Alt",
            KeyCode.Space        => "Space",
            KeyCode.Return       => "Enter",
            KeyCode.Escape       => "Esc",
            KeyCode.Tab          => "Tab",
            KeyCode.BackQuote    => "`",
            KeyCode.Mouse0       => "Sol Tık",
            KeyCode.Mouse1       => "Sağ Tık",
            KeyCode.Mouse2       => "Orta Tık",
            _ => key.ToString()
        };
    }

    #endregion

    #region Persistence

    private static void SaveBinding(GameAction action)
    {
        string prefKey = $"KeyBind_{action}";
        var binding = _current[action];

        PlayerPrefs.SetInt(prefKey + "_IsMouse", binding.IsMouse ? 1 : 0);

        if (binding.IsMouse)
            PlayerPrefs.SetInt(prefKey + "_Mouse", binding.MouseButton);
        else
            PlayerPrefs.SetInt(prefKey + "_Key", (int)binding.Key);

        PlayerPrefs.Save();
    }

    private static void EnsureInitialized()
    {
        if (!_initialized) Initialize();
    }

    #endregion
}
