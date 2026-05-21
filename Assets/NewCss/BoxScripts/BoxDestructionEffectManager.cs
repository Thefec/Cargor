using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Kutu kırılma efekt yöneticisi — R.E.P.O tarzı tatmin edici parçalanma.
    /// Singleton pattern ile çalışır, her sahnede bir adet bulunmalı.
    /// 
    /// Özellikler:
    /// - Object Pool sistemi (GC ve performans optimizasyonu)
    /// - Karton parça dağılımı (büyük kenarlar + orta parçalar + küçük kırıntılar)
    /// - Hafif toz partikülleri (az miktarda, R.E.P.O tarzı)
    /// - URP/Lit shader uyumlu fade-out animasyonu
    /// - Akıllı fizik: gravity aktif kalır, kinematic sadece fade-out'ta devreye girer
    /// 
    /// Kullanım:
    /// BoxDestructionEffectManager.Instance.PlayEffect(boxType, position);
    /// </summary>
    public class BoxDestructionEffectManager : MonoBehaviour
    {
        #region Singleton

        public static BoxDestructionEffectManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializePool();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("=== PARÇA AYARLARI ===")]
        [SerializeField, Tooltip("Toplam parça sayısı (büyük + orta + küçük)")]
        private int fragmentCount = 6;

        [SerializeField, Tooltip("Patlama kuvveti (m/s)")]
        private float explosionForce = 4f;

        [SerializeField, Tooltip("Parçaların toplam yaşam süresi (saniye)")]
        private float fragmentLifetime = 1.5f;

        [SerializeField, Tooltip("En küçük parça boyutu")]
        private float minFragmentScale = 0.08f;

        [SerializeField, Tooltip("En büyük parça boyutu")]
        private float maxFragmentScale = 0.2f;

        [Header("=== TOZ PARTİKÜL AYARLARI ===")]
        [SerializeField, Tooltip("Toz partikülleri eklensin mi?")]
        private bool addDustParticles = true;

        [SerializeField, Tooltip("Toz partikül sayısı")]
        private int dustParticleCount = 5;

        [Header("=== KUTU RENKLERİ ===")]
        [SerializeField] private Color redBoxColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color yellowBoxColor = new Color(0.9f, 0.8f, 0.2f);
        [SerializeField] private Color blueBoxColor = new Color(0.2f, 0.4f, 0.8f);
        [SerializeField] private Color cardboardTint = new Color(0.8f, 0.7f, 0.5f);

        #endregion

        #region Private Fields

        private Mesh _cubeMesh;
        private readonly Queue<GameObject> _fragmentPool = new Queue<GameObject>();
        private const int POOL_SIZE = 30;

        // Shader property ID'leri — her frame string lookup yerine cached ID kullan
        private static readonly int ShaderSurface = Shader.PropertyToID("_Surface");
        private static readonly int ShaderBlend = Shader.PropertyToID("_Blend");
        private static readonly int ShaderSrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int ShaderDstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int ShaderZWrite = Shader.PropertyToID("_ZWrite");
        private static readonly int ShaderSmoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int ShaderBaseColor = Shader.PropertyToID("_BaseColor");

        #endregion

        #region Initialization

        private void InitializePool()
        {
            // Sadece küp mesh — basit kare parçalar
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);

            // Pool'u önceden doldur
            for (int i = 0; i < POOL_SIZE; i++)
            {
                var fragment = CreateFragment();
                fragment.SetActive(false);
                _fragmentPool.Enqueue(fragment);
            }
        }

        /// <summary>
        /// Tek bir küp fragment oluşturur (pool için).
        /// </summary>
        private GameObject CreateFragment()
        {
            var fragment = new GameObject("BoxFragment");
            fragment.transform.SetParent(transform);

            // ─── Mesh (sadece küp) ───
            var meshFilter = fragment.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = _cubeMesh;

            // ─── Renderer ───
            var meshRenderer = fragment.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                // URP yoksa fallback
                shader = Shader.Find("Standard");
            }
            var mat = new Material(shader);
            mat.SetFloat(ShaderSmoothness, 0.1f); // Karton yüzey — mat görünüm
            // URP Lit shader'da başlangıç rengini gri yap (beyaz parlama önlenir)
            mat.SetColor(ShaderBaseColor, new Color(0.5f, 0.5f, 0.5f, 1f));
            // Emission'ı kapat (parlama kaynağı olabilir)
            mat.DisableKeyword("_EMISSION");
            meshRenderer.material = mat;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ─── Physics ───
            var rb = fragment.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.linearDamping = 1f;
            rb.angularDamping = 1.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.useGravity = true;

            // ─── Collider ───
            var collider = fragment.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.8f;

            // Diğer objelerle gereksiz etkileşimi azalt
            fragment.layer = LayerMask.NameToLayer("Ignore Raycast");

            return fragment;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Kutu tipine göre kırılma efekti oynatır.
        /// ClientRpc tarafından her client'ta çağrılır.
        /// </summary>
        /// <param name="boxType">Kutu tipi (renk belirler)</param>
        /// <param name="position">Dünya pozisyonu</param>
        public void PlayEffect(BoxInfo.BoxType boxType, Vector3 position)
        {
            // Null/destroyed kontrolü
            if (this == null || !gameObject.activeInHierarchy) return;

            Color fragmentColor = GetColorForBoxType(boxType);
            StartCoroutine(SpawnFragmentsCoroutine(position, fragmentColor));
        }

        /// <summary>
        /// Özel renk ile kırılma efekti oynatır.
        /// </summary>
        public void PlayEffect(Vector3 position, Color color)
        {
            if (this == null || !gameObject.activeInHierarchy) return;
            StartCoroutine(SpawnFragmentsCoroutine(position, color));
        }

        /// <summary>
        /// Herhangi bir objenin parçalanma efektini oynatır.
        /// Objenin rengini otomatik algılar:
        /// 1. BoxInfo varsa kutu rengini kullanır
        /// 2. Yoksa Renderer materyalinden renk çeker
        /// 3. Hiçbiri yoksa varsayılan karton rengi kullanır
        /// </summary>
        /// <param name="position">Dünya pozisyonu</param>
        /// <param name="sourceObject">Rengi algılanacak kaynak obje</param>
        public void PlayEffect(Vector3 position, GameObject sourceObject)
        {
            if (this == null || !gameObject.activeInHierarchy) return;

            Color fragmentColor = GetColorFromObject(sourceObject);
            StartCoroutine(SpawnFragmentsCoroutine(position, fragmentColor));
        }

        /// <summary>
        /// Objenin rengini otomatik algılar.
        /// BoxInfo → Renderer → Fallback karton rengi sırasıyla kontrol eder.
        /// </summary>
        private Color GetColorFromObject(GameObject obj)
        {
            if (obj == null) return cardboardTint;

            // 1. BoxInfo varsa kutu rengini kullan
            var boxInfo = obj.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                return GetColorForBoxType(boxInfo.boxType);
            }

            // 2. Renderer'dan materyalin _BaseColor veya color değerini al
            var renderer = obj.GetComponentInChildren<MeshRenderer>();
            if (renderer == null) renderer = obj.GetComponentInChildren<MeshRenderer>();
            
            if (renderer != null && renderer.sharedMaterial != null)
            {
                var mat = renderer.sharedMaterial;

                // URP Lit shader: _BaseColor
                if (mat.HasProperty(ShaderBaseColor))
                {
                    Color matColor = mat.GetColor(ShaderBaseColor);
                    matColor.a = 1f; // Alpha'yı 1'e sabitle
                    return matColor;
                }

                // Standard shader: _Color
                if (mat.HasProperty("_Color"))
                {
                    Color matColor = mat.color;
                    matColor.a = 1f;
                    return matColor;
                }
            }

            // 3. SkinnedMeshRenderer kontrolü
            var skinnedRenderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedRenderer != null && skinnedRenderer.sharedMaterial != null)
            {
                var mat = skinnedRenderer.sharedMaterial;

                if (mat.HasProperty(ShaderBaseColor))
                {
                    Color matColor = mat.GetColor(ShaderBaseColor);
                    matColor.a = 1f;
                    return matColor;
                }
            }

            // 4. Fallback: karton rengi
            return cardboardTint;
        }

        #endregion

        #region Fragment Spawning

        /// <summary>
        /// Ana fragment spawn coroutine'i.
        /// Parçaları 3 gruba ayırır: büyük kenarlar, orta parçalar, küçük kırıntılar.
        /// R.E.P.O tarzı: az ama tatmin edici parçalanma.
        /// </summary>
        private IEnumerator SpawnFragmentsCoroutine(Vector3 position, Color color)
        {
            var fragments = new List<GameObject>();

            // Tüm parçalar küp — farklı boyutlarda
            for (int i = 0; i < fragmentCount; i++)
            {
                float scale = Random.Range(minFragmentScale, maxFragmentScale);
                bool tint = i % 3 == 0;
                SpawnFragment(fragments, position, color, scale, scale * 1.2f, tint);
            }

            // Toz partikülleri
            if (addDustParticles)
            {
                SpawnDustParticles(position, color);
            }



            // ─── FADE-OUT VE TEMİZLİK ────────────────────────────
            yield return StartCoroutine(FadeOutFragments(fragments));
        }

        /// <summary>
        /// Tek bir fragment'i pool'dan alıp sahneye yerleştirir.
        /// </summary>
        private void SpawnFragment(List<GameObject> fragments, Vector3 position, Color baseColor,
            float minScale, float maxScale, bool useCardboardTint)
        {
            var fragment = GetOrCreateFragment();
            if (fragment == null) return;

            fragment.SetActive(true);

            // Boyut (non-uniform = daha gerçekçi parça şekilleri)
            float scaleX = Random.Range(minScale, maxScale);
            float scaleY = Random.Range(minScale * 0.5f, maxScale * 0.8f);
            float scaleZ = Random.Range(minScale, maxScale);
            fragment.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

            // Pozisyon (kutu merkezi etrafında rastgele dağılım)
            var randomOffset = new Vector3(
                Random.Range(-0.25f, 0.25f),
                Random.Range(-0.1f, 0.3f),
                Random.Range(-0.25f, 0.25f)
            );
            fragment.transform.position = position + randomOffset;

            // Rastgele rotasyon
            fragment.transform.rotation = Random.rotation;

            // ─── Renk (bazı parçalar karton tonu alır — çeşitlilik) ───
            var renderer = fragment.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
            {
                Color finalColor = useCardboardTint
                    ? Color.Lerp(baseColor, cardboardTint, 0.4f)
                    : baseColor;

                // Hafif renk varyasyonu — 1.0 sınırını aşmaz (HDR parlama önlenir)
                float variation = Random.Range(0.7f, 1.0f);
                finalColor = new Color(
                    Mathf.Clamp01(finalColor.r * variation),
                    Mathf.Clamp01(finalColor.g * variation),
                    Mathf.Clamp01(finalColor.b * variation),
                    1f
                );
                // URP Lit shader _BaseColor kullanır, .color (_Color) DEĞİL!
                renderer.material.SetColor(ShaderBaseColor, finalColor);
            }

            // ─── Collider (aktif başlar, 0.5s sonra kapanır) ───
            var col = fragment.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.enabled = true;
                col.size = Vector3.one * 0.8f;
            }

            // ─── Fizik (patlama kuvveti + rastgele yuvarlanma) ───
            var rb = fragment.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true; // Gravity HER ZAMAN aktif — havada donmaz
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Gerçekçi patlama yönü (çoğunlukla yukarı ve dışa)
                Vector3 explosionDir = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(0.5f, 1.5f),
                    Random.Range(-1f, 1f)
                ).normalized;

                float force = explosionForce * Random.Range(0.6f, 1.4f);
                rb.AddForce(explosionDir * force, ForceMode.VelocityChange);

                // Rastgele yuvarlanma — karton parça gibi dönsün
                rb.AddTorque(Random.insideUnitSphere * force * 3f, ForceMode.VelocityChange);
            }

            fragments.Add(fragment);

            // Collider'ı 0.5s sonra kapat (diğer objelerle gereksiz çarpışma önlenir)
            // NOT: Rigidbody + gravity AKTİF KALIR — parçalar havadaysa yere düşer
            StartCoroutine(DisableColliderAfterDelay(fragment, 0.5f));
        }

        /// <summary>
        /// Collider'ı belirli süre sonra devre dışı bırakır.
        /// DİKKAT: Sadece collider kapanır, Rigidbody + gravity aktif kalır.
        /// Bu sayede havadaki parçalar yere düşmeye devam eder.
        /// </summary>
        private IEnumerator DisableColliderAfterDelay(GameObject fragment, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (fragment == null || !fragment.activeInHierarchy) yield break;

            var col = fragment.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.enabled = false;
            }

            // !! Rigidbody'ye DOKUNMA — gravity aktif kalsın !!
            // Kinematic sadece FadeOutFragments'ta devreye girer
        }

        /// <summary>
        /// Fragment'leri fade-out ile yok eder.
        /// Lifetime'ın %70'inde kinematic yapılır (o zamana parçalar yere inmiş olur).
        /// Kalan %30'da alpha azalır ve scale küçülür.
        /// </summary>
        private IEnumerator FadeOutFragments(List<GameObject> fragments)
        {
            // ─── BEKLEME: Parçaların yere inmesini bekle ───
            yield return new WaitForSeconds(fragmentLifetime * 0.7f);

            // ─── KİNEMATİK YAP: Artık fizik gerekmez ───
            // Bu noktada parçalar yere inmiş olmalı
            foreach (var fragment in fragments)
            {
                if (fragment == null || !fragment.activeInHierarchy) continue;

                // Kinematic yap — artık fizik simülasyonu durur
                var rb = fragment.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }

                // Materyali transparent moda geçir (alpha fade için zorunlu)
                var renderer = fragment.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    SetMaterialTransparent(renderer.material);
                }
            }

            // ─── FADE-OUT ANİMASYONU ──────────────────────────────
            float fadeTime = fragmentLifetime * 0.3f;
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / fadeTime);

                foreach (var fragment in fragments)
                {
                    if (fragment == null || !fragment.activeInHierarchy) continue;

                    var renderer = fragment.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.material != null)
                    {
                        Color c = renderer.material.GetColor(ShaderBaseColor);
                        c.a = alpha;
                        renderer.material.SetColor(ShaderBaseColor, c);
                    }

                    // Scale küçült — yok olma hissi
                    fragment.transform.localScale *= 0.97f;
                }

                yield return null;
            }

            // ─── POOL'A GERİ DÖN ─────────────────────────────────
            foreach (var fragment in fragments)
            {
                if (fragment != null)
                {
                    ReturnFragmentToPool(fragment);
                }
            }
        }

        /// <summary>
        /// Hafif toz bulutu efekti. URP shader öncelikli.
        /// </summary>
        private void SpawnDustParticles(Vector3 position, Color color)
        {
            var dustGO = new GameObject("DustEffect");
            dustGO.transform.position = position;

            var ps = dustGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 1.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);

            Color dustColor = Color.Lerp(color, cardboardTint, 0.6f);
            dustColor.a = 0.3f;
            main.startColor = dustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = dustParticleCount;
            main.gravityModifier = 0.2f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, dustParticleCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(dustColor, 0f), new GradientColorKey(dustColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.3f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var particleRenderer = dustGO.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particleShader == null)
                    particleShader = Shader.Find("Particles/Standard Unlit");
                if (particleShader != null)
                    particleRenderer.material = new Material(particleShader);
            }

            ps.Play();
            Destroy(dustGO, main.startLifetime.constantMax + 0.5f);
        }

        #endregion

        #region Pool Management

        /// <summary>
        /// Pool'dan fragment alır veya yenisini oluşturur.
        /// </summary>
        private GameObject GetOrCreateFragment()
        {
            if (_fragmentPool.Count > 0)
            {
                return _fragmentPool.Dequeue();
            }

            // Pool boşsa yeni oluştur
            if (_cubeMesh == null) return null;
            return CreateFragment();
        }

        /// <summary>
        /// Fragment'i pool'a geri döndürür (deaktive eder, sıfırlar).
        /// </summary>
        private void ReturnFragmentToPool(GameObject fragment)
        {
            if (fragment == null) return;

            fragment.SetActive(false);
            fragment.transform.localScale = Vector3.one * maxFragmentScale;

            // Rigidbody sıfırla
            var rb = fragment.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Collider sıfırla
            var col = fragment.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.enabled = true;
            }

            // Materyal sıfırla: opaque moda geri dön, alpha = 1
            var renderer = fragment.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
            {
                SetMaterialOpaque(renderer.material);
                Color c = renderer.material.GetColor(ShaderBaseColor);
                c.a = 1f;
                renderer.material.SetColor(ShaderBaseColor, c);
            }

            _fragmentPool.Enqueue(fragment);
        }

        #endregion

        #region Shader Helpers

        /// <summary>
        /// URP/Lit materyalini Transparent moduna geçirir.
        /// Fade-out sırasında alpha değişikliğinin görünmesi için zorunlu.
        /// </summary>
        private void SetMaterialTransparent(Material mat)
        {
            if (mat == null) return;

            mat.SetFloat(ShaderSurface, 1); // 0=Opaque, 1=Transparent
            mat.SetFloat(ShaderBlend, 0);   // 0=Alpha blend
            mat.SetInt(ShaderSrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt(ShaderDstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt(ShaderZWrite, 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        /// <summary>
        /// URP/Lit materyalini Opaque moduna geri döndürür.
        /// Pool'a dönerken çağrılır — bir sonraki kullanım için hazırlanır.
        /// </summary>
        private void SetMaterialOpaque(Material mat)
        {
            if (mat == null) return;

            mat.SetFloat(ShaderSurface, 0);
            mat.SetInt(ShaderSrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt(ShaderDstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt(ShaderZWrite, 1);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        #endregion

        #region Color Mapping

        /// <summary>
        /// BoxType enum'dan renk döndürür.
        /// </summary>
        private Color GetColorForBoxType(BoxInfo.BoxType boxType)
        {
            return boxType switch
            {
                BoxInfo.BoxType.Red => redBoxColor,
                BoxInfo.BoxType.Yellow => yellowBoxColor,
                BoxInfo.BoxType.Blue => blueBoxColor,
                _ => cardboardTint
            };
        }

        #endregion
    }
}
