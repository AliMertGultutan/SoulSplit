using SoulSplit.Core;
using UnityEngine;

namespace SoulSplit.Player
{
    /// <summary>
    /// Ruh aktifken bedenin geri donecegi yeri dunyada gosterir. Renk durumun
    /// hizli okunmasini saglar; metin de bilgiyi yalnizca renge bagli birakmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoulMaterializationPreview : MonoBehaviour
    {
        private const int PointCount = 40;
        private const float RefreshInterval = 0.08f;

        private static readonly Color SafeColor = new Color(0.35f, 0.92f, 1f, 0.9f);
        private static readonly Color UnsafeColor = new Color(1f, 0.3f, 0.28f, 0.95f);
        private static readonly Color BodyColor = new Color(1f, 0.72f, 0.3f, 0.9f);

        private SoulSwitchManager _manager;
        private GameObject _visualRoot;
        private LineRenderer _outline;
        private TextMesh _label;
        private Material _lineMaterial;
        private float _refreshTimer;
        private Vector2 _shapeCenter;
        private Vector2 _shapeSize;
        private Color _baseColor;

        public bool IsVisible => _visualRoot != null && _visualRoot.activeSelf;
        public bool IsSafe { get; private set; }
        public Vector2 PreviewPosition { get; private set; }
        public string StatusText => _label != null ? _label.text : string.Empty;

        private void Awake()
        {
            _manager = GetComponent<SoulSwitchManager>();
            BuildVisuals();
        }

        private void LateUpdate()
        {
            if (_manager == null || !_manager.IsSoulActive)
            {
                SetVisible(false);
                return;
            }

            _refreshTimer -= Time.unscaledDeltaTime;
            // Beden yercekimiyle hareket edebilir; "yerinde kal" ayarinda
            // ucuz olan beden takibini kare kare yaparak isareti kaydirmayiz.
            if (!GameplaySettings.MaterializeAtSoulPosition || _refreshTimer <= 0f)
            {
                _refreshTimer = RefreshInterval;
                RefreshPreview();
            }

            if (!IsVisible) return;
            float pulse = 0.86f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.09f;
            Color pulseColor = _baseColor;
            pulseColor.a *= pulse;
            _outline.startColor = pulseColor;
            _outline.endColor = pulseColor;
            _label.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b,
                Mathf.Clamp01(pulse + 0.08f));
        }

        private void RefreshPreview()
        {
            if (!_manager.TryGetMaterializationPreview(
                    out Vector2 position, out bool isSafe, out bool remainsAtBody))
            {
                SetVisible(false);
                return;
            }

            PreviewPosition = position;
            IsSafe = isSafe;
            _baseColor = remainsAtBody ? BodyColor : isSafe ? SafeColor : UnsafeColor;
            _label.text = remainsAtBody
                ? "BEDEN BURADA KALIR"
                : isSafe ? "BEDENLEŞME NOKTASI" : "GÜVENLİ NOKTA YOK";

            if (!_manager.TryGetBodyPreviewShape(position, out _shapeCenter, out _shapeSize))
            {
                _shapeCenter = position;
                _shapeSize = new Vector2(0.8f, 1.6f);
            }

            DrawOutline();
            _label.transform.position = new Vector3(
                _shapeCenter.x, _shapeCenter.y + _shapeSize.y * 0.65f + 0.2f, 0f);
            SetVisible(true);
        }

        private void BuildVisuals()
        {
            _visualRoot = new GameObject("SoulMaterializationPreview_Visual");
            _visualRoot.transform.SetParent(transform, false);

            GameObject outlineObject = new GameObject("BodyOutline");
            outlineObject.transform.SetParent(_visualRoot.transform, false);
            _outline = outlineObject.AddComponent<LineRenderer>();
            _outline.useWorldSpace = true;
            _outline.loop = true;
            _outline.positionCount = PointCount;
            _outline.widthMultiplier = 0.055f;
            _outline.numCornerVertices = 3;
            _outline.numCapVertices = 3;
            _outline.sortingOrder = 60;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _lineMaterial = new Material(shader) { name = "Soul Materialization Preview (Runtime)" };
                _outline.material = _lineMaterial;
            }

            GameObject labelObject = new GameObject("StatusLabel");
            labelObject.transform.SetParent(_visualRoot.transform, false);
            _label = labelObject.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 56;
            _label.characterSize = 0.05f;
            _label.fontStyle = FontStyle.Bold;
            _label.text = string.Empty;
            MeshRenderer renderer = _label.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 61;

            SetVisible(false);
        }

        private void DrawOutline()
        {
            float radiusX = Mathf.Max(0.15f, _shapeSize.x * 0.5f);
            float radiusY = Mathf.Max(0.25f, _shapeSize.y * 0.5f);
            for (int i = 0; i < PointCount; i++)
            {
                float angle = i * Mathf.PI * 2f / PointCount;
                _outline.SetPosition(i, new Vector3(
                    _shapeCenter.x + Mathf.Cos(angle) * radiusX,
                    _shapeCenter.y + Mathf.Sin(angle) * radiusY,
                    0f));
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visualRoot != null && _visualRoot.activeSelf != visible)
                _visualRoot.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }
    }
}
