using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using NewCss.Audio;

/// <summary>
/// Projedeki TÜM sesleri (SFX + müzik) tek pencerede listeler, tıklayınca çalar. Salt okunur —
/// hiçbir asset'i değiştirmez. Kaynaklar:
///   - SFX: Assets/Resources/Audio/SfxLibrary.asset (Assets/Editor/SfxLibrarySetup.cs kurar) —
///     6 sahne SFX'i + 2 Stinger (Bankrupt/Victory) + MoneyEarned/CorrectItem BİLİNÇLİ boş
///     (plan §3.2, henüz seçilmedi).
///   - Müzik: MusicLibrary.GetTracks (Assets/NewCss/Audio/Music/MusicLibrary.cs) ile aynı
///     Resources/Music/{Gameplay/Main,Busy,Closing,Tension,Menu} klasörleri — kod DEĞİŞMEDEN
///     buraya da yeni parça eklendiğinde otomatik görünür.
///
/// Önizleme çalma UnityEditor.AudioUtil (internal) reflection ile — Unity'nin kendi Project
/// penceresi/AudioClip Inspector'ının kullandığı standart yol, sürümler arası kararlı.
/// </summary>
public class SesEnvanteriWindow : EditorWindow
{
    private const string LibraryPath = "Assets/Resources/Audio/SfxLibrary.asset";
    private Vector2 _scroll;
    private AudioClip _playing;

    [MenuItem("Tools/Cargor/Audio/Ses Envanterini Ac")]
    public static void Ac()
    {
        var pencere = GetWindow<SesEnvanteriWindow>("Ses Envanteri");
        pencere.minSize = new Vector2(420, 300);
    }

    private void OnDisable()
    {
        // Pencere kapanınca çalan varsa durdur — arkada sonsuz döngüde sesin kalmasını önler.
        StopPreview();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("SFX — SfxLibrary.asset", EditorStyles.boldLabel);
        DrawSfxSection();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Muzik — Resources/Music", EditorStyles.boldLabel);
        DrawMusicSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawSfxSection()
    {
        var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        if (library == null)
        {
            EditorGUILayout.HelpBox(
                $"{LibraryPath} bulunamadi. Once Tools > Cargor > Audio > " +
                "SFX Kutuphanesini Kur veya Guncelle calistir.", MessageType.Warning);
            return;
        }

        foreach (var entry in library.Entries.OrderBy(e => e.id.ToString()))
        {
            DrawClipRow(entry.id.ToString(), entry.clip);
        }
    }

    private void DrawMusicSection()
    {
        foreach (MusicFamily family in Enum.GetValues(typeof(MusicFamily)))
        {
            var clips = MusicLibrary.GetTracks(family);
            if (clips.Length == 0)
            {
                EditorGUILayout.LabelField($"{family} — bos (Resources/Music altinda dosya yok)");
                continue;
            }

            EditorGUILayout.LabelField(family.ToString(), EditorStyles.miniBoldLabel);
            foreach (var clip in clips)
            {
                DrawClipRow("  " + (clip != null ? clip.name : "?"), clip);
            }
        }
    }

    private void DrawClipRow(string label, AudioClip clip)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(label, GUILayout.Width(220));

        if (clip == null)
        {
            EditorGUILayout.LabelField("BOS SLOT (klip atanmamis)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            return;
        }

        EditorGUILayout.LabelField($"{clip.length:0.0}s", GUILayout.Width(45));

        bool isPlaying = _playing == clip && IsPreviewClipPlaying();
        string buttonLabel = isPlaying ? "Durdur" : "Cal";
        if (GUILayout.Button(buttonLabel, GUILayout.Width(60)))
        {
            if (isPlaying) StopPreview();
            else PlayPreview(clip);
        }

        if (GUILayout.Button("Sec", GUILayout.Width(45)))
        {
            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
        }

        EditorGUILayout.EndHorizontal();
    }

    #region AudioUtil reflection (Unity internal — Project penceresinin kullandigi standart yol)

    private static MethodInfo _playMethod;
    private static MethodInfo _stopAllMethod;
    private static MethodInfo _isPlayingMethod;

    private static Type AudioUtilType =>
        typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

    private void PlayPreview(AudioClip clip)
    {
        StopPreview();
        try
        {
            _playMethod ??= AudioUtilType?.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            _playMethod?.Invoke(null, new object[] { clip, 0, false });
            _playing = clip;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SesEnvanteri] Onizleme calinamadi ({clip.name}): {e.Message}. " +
                              "Clip'i Project penceresinde secip oradan da calabilirsin.");
        }
    }

    private void StopPreview()
    {
        try
        {
            _stopAllMethod ??= AudioUtilType?.GetMethod(
                "StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
            _stopAllMethod?.Invoke(null, null);
        }
        catch { /* onizleme durdurma en kotu ihtimalle sessizce basarisiz olur */ }
        _playing = null;
    }

    private bool IsPreviewClipPlaying()
    {
        try
        {
            _isPlayingMethod ??= AudioUtilType?.GetMethod(
                "IsPreviewClipPlaying", BindingFlags.Static | BindingFlags.Public);
            if (_isPlayingMethod == null) return _playing != null;
            return (bool)_isPlayingMethod.Invoke(null, null);
        }
        catch
        {
            return _playing != null;
        }
    }

    #endregion
}
