using System.Collections;
using UnityEngine;

namespace NewCss.UIScripts
{
    /// <summary>
    /// World-space canvas'ların event camera'sını (worldCamera) çalışma zamanında yerel kameraya bağlar.
    ///
    /// SORUN: Sahnedeki world-space canvas'ların Event Camera'sı atanmamış (m_Camera = None). Bir
    /// world-space canvas'ta GraphicRaycaster varsa ve event camera yoksa, EventSystem her frame
    /// projeksiyon yapamaz ve "Screen position out of view frustum (screen pos -nan(ind), -nan(ind))"
    /// uyarısını durmadan basar. Netcode oyununda oyuncu kamerası runtime'da spawn olduğundan sahnede
    /// edit-time referansı verilemez; özellikle CLIENT tarafında join anında Camera.main henüz hazır
    /// olmadığı için spam client'ta ortaya çıkar.
    ///
    /// ÇÖZÜM: Kamera hazır olur olmaz worldCamera'sı boş olan tüm world-space canvas'lara bağla.
    /// Müşteri gibi runtime spawn olan canvas'lar için hafif periyodik tarama yapılır.
    ///
    /// Kurulum GEREKMEZ: [RuntimeInitializeOnLoadMethod] ile kendini başlatır, sahneye eklemeye gerek yok.
    /// Yalnızca worldCamera'sı null olan canvas'lara dokunur (elle atanmış kameraları bozmaz).
    /// </summary>
    public class WorldSpaceCanvasCameraBinder : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[WorldSpaceCanvasCameraBinder]");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<WorldSpaceCanvasCameraBinder>();
        }

        private void Start()
        {
            StartCoroutine(BindLoop());
        }

        private IEnumerator BindLoop()
        {
            var wait = new WaitForSeconds(ScanInterval);

            while (true)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    BindWorldSpaceCanvases(cam);
                }

                yield return wait;
            }
        }

        private void BindWorldSpaceCanvases(Camera cam)
        {
            // Aktif sahnedeki tüm canvas'lar.
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            foreach (var canvas in canvases)
            {
                if (canvas == null) continue;
                if (canvas.renderMode != RenderMode.WorldSpace) continue;

                // Yalnızca event camera'sı boş olanlara dokun; elle atanmışı koru.
                if (canvas.worldCamera == null)
                {
                    canvas.worldCamera = cam;
                }
            }
        }
    }
}
