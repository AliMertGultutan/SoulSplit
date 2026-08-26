using UnityEngine;

namespace SoulSplit.Core
{
    /// <summary>
    /// Vurus izi (slash arc) efekti — silahin yorungesini gosteren, kodla
    /// uretilen yay bicimli mesh. ParticleFX ile ayni desen: prefab
    /// gerektirmez, her cagri kendi objesini olusturur ve omru bitince
    /// kendini yok eder.
    ///
    /// NEDEN MESH, PARCACIK DEGIL: Parcacik izi noktali/dagilmis okunur;
    /// kilic savurmasi ise SUREKLI bir yay. Ring-segment mesh + uzunluk
    /// boyunca alfa gradyani, klasik "slash" gorunumunu verir — kuyruk
    /// sonuk, uc parlak.
    ///
    /// Sprite'in kendi hareket cizgileri zaten var; bu onlarin YERINE degil,
    /// USTUNE biner ve savurmayi daha okunur kilar.
    /// </summary>
    public static class SlashFX
    {
        private static Material _cachedMaterial;

        /// <param name="center">Yayin merkezi (genelde saldiranin govdesi).</param>
        /// <param name="facing">+1 saga, -1 sola. Yay buna gore aynalanir.</param>
        /// <param name="radius">Merkezden yayin ortasina mesafe.</param>
        /// <param name="thickness">Yayin kalinligi (dunya birimi).</param>
        /// <param name="startAngleDeg">Baslangic acisi (derece, 0 = saga dogru yatay).</param>
        /// <param name="sweepDeg">Tarama acisi. Negatif = saat yonu.</param>
        /// <param name="duration">Sonme suresi.</param>
        public static void Arc(
            Vector3 center, int facing, Color color,
            float radius = 1.15f, float thickness = 0.38f,
            float startAngleDeg = 110f, float sweepDeg = -140f,
            float duration = 0.16f, int sortingOrder = 70)
        {
            var go = new GameObject("FX_Slash");
            go.transform.position = center;

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = BuildArcMesh(facing, radius, thickness, startAngleDeg, sweepDeg, color);

            renderer.sharedMaterial = GetMaterial();
            renderer.sortingOrder = sortingOrder;
            // 2D sahnede isiktan/golgeden etkilenmesin; kendi isigini yayiyor gibi.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            var fader = go.AddComponent<SlashFader>();
            fader.Init(duration, radius);
        }

        /// <summary>
        /// Hafif saldiri icin kisayol.
        ///
        /// Merkez ONE kaydiriliyor (facing * 0.3): yay govdenin ETRAFINDA degil
        /// ONUNDE savrulsun. Merkezi karakterin uzerine koymak yayin bas/govde
        /// hizasindan gecmesine ve silueti kapatmasina yol aciyordu.
        /// </summary>
        public static void Light(Vector3 center, int facing, Color color)
        {
            Arc(center + new Vector3(facing * 0.3f, 0.1f, 0f), facing, color,
                radius: 1.15f, thickness: 0.20f,
                startAngleDeg: 95f, sweepDeg: -120f, duration: 0.14f);
        }

        /// <summary>Agir saldiri icin kisayol: daha genis, daha kalin, daha uzun sure.</summary>
        public static void Heavy(Vector3 center, int facing, Color color)
        {
            Arc(center + new Vector3(facing * 0.38f, 0.1f, 0f), facing, color,
                radius: 1.5f, thickness: 0.30f,
                startAngleDeg: 115f, sweepDeg: -155f, duration: 0.2f);
        }

        /// <summary>
        /// Ring-segment (halka dilimi) mesh uretir. Alfa, yay boyunca
        /// kuyruktan uca dogru artar — hareket yonunu gozle okutur.
        /// </summary>
        private static Mesh BuildArcMesh(
            int facing, float radius, float thickness,
            float startAngleDeg, float sweepDeg, Color color)
        {
            const int segments = 24;
            var vertices = new Vector3[(segments + 1) * 2];
            var colors = new Color[(segments + 1) * 2];
            var uvs = new Vector2[(segments + 1) * 2];
            var triangles = new int[segments * 6];

            float half = thickness * 0.5f;
            int sign = facing >= 0 ? 1 : -1;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = (startAngleDeg + sweepDeg * t) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

                // Yatayda aynalama; dikey aynen kalir.
                var dir = new Vector3(cos * sign, sin, 0f);

                vertices[i * 2] = dir * (radius - half);
                vertices[i * 2 + 1] = dir * (radius + half);

                // Kuyruk seffaf -> uc parlak. Ucta hafif bir dusus, sivri bitis icin.
                float alpha = Mathf.Pow(t, 1.6f) * Mathf.Clamp01((1f - t) * 6f + 0.25f);
                var c = color;
                c.a = color.a * alpha;
                colors[i * 2] = c;
                colors[i * 2 + 1] = c;

                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2, tri = i * 6;
                triangles[tri]     = v;
                triangles[tri + 1] = v + 1;
                triangles[tri + 2] = v + 2;
                triangles[tri + 3] = v + 1;
                triangles[tri + 4] = v + 3;
                triangles[tri + 5] = v + 2;
            }

            var mesh = new Mesh { name = "SlashArc" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GetMaterial()
        {
            if (_cachedMaterial != null) return _cachedMaterial;

            // ParticleFX ile ayni gerekce: projenin geri kalani URP kullaniyor,
            // Built-in "Sprites/Default" build'de shader stripping ile kaybolabilir.
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _cachedMaterial = new Material(shader);
            // Sprite shader'lari _MainTex bekler; duz beyaz doku verilince
            // gorunumu tamamen vertex renkleri belirler.
            var white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
            _cachedMaterial.mainTexture = white;
            return _cachedMaterial;
        }

        /// <summary>Yayi soldurup objeyi yok eden kucuk yardimci.</summary>
        private class SlashFader : MonoBehaviour
        {
            private float _duration;
            private float _elapsed;
            private float _baseRadius;
            private Mesh _mesh;
            private Color[] _baseColors;
            private Color[] _workColors;

            public void Init(float duration, float baseRadius)
            {
                _duration = Mathf.Max(0.01f, duration);
                _baseRadius = baseRadius;
                _mesh = GetComponent<MeshFilter>().mesh;   // ornek kopya
                _baseColors = _mesh.colors;
                _workColors = new Color[_baseColors.Length];
            }

            private void Update()
            {
                // Hit-stop sirasinda da akmali; CameraFollow/HitFlash ile ayni kural.
                _elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);

                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                // Hizli sonme + hafif genisleme: savurmanin "dagilmasi".
                float fade = 1f - t * t;
                for (int i = 0; i < _baseColors.Length; i++)
                {
                    var c = _baseColors[i];
                    c.a *= fade;
                    _workColors[i] = c;
                }
                _mesh.colors = _workColors;

                float grow = 1f + t * 0.12f;
                transform.localScale = new Vector3(grow, grow, 1f);
            }

            private void OnDestroy()
            {
                if (_mesh != null) Destroy(_mesh);
            }
        }
    }
}
