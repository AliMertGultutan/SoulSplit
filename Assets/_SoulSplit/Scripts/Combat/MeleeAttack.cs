using UnityEngine;
using SoulSplit.Player;
using SoulSplit.Core;

namespace SoulSplit.Combat
{
    /// <summary>
    /// Yakin dovus vurusu. Hem beden hem ruh ayni scripti kullanir;
    /// aralarindaki tek fark Inspector'daki damageType degeri.
    ///
    /// Hedef bulma Layer + ContactFilter2D ile yapilir, if-else zinciri yok.
    /// Filtre HER IKI dusman katmanini da kapsar; boylece yanlis formla
    /// vurulan dusman hasar almaz ama "sekme" geri bildirimi alabilir.
    /// </summary>
    public class MeleeAttack : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private PlayerInputHandler input;
        [Tooltip("Vurusun yonunu belirleyen kaynak. Bos birakilirsa bu obje kullanilir.")]
        [SerializeField] private Transform originTransform;

        [Header("Hasar")]
        [Tooltip("Bu saldirinin boyutu. Beden = Physical, ruh = Spiritual.")]
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private int damage = 1;

        [Header("Vurus Alani")]
        [Tooltip("Vurus kutusunun bakis yonundeki kaymasi.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.9f, 0f);
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.4f, 1.3f);
        [Tooltip("Her iki dusman katmanini da isaretle; yanlis form geri bildirimi buna bagli.")]
        [SerializeField] private LayerMask targetLayers;

        [Header("Tempo")]
        [SerializeField] private float cooldown = 0.35f;

        [Header("Carpisma Efekti")]
        [Tooltip("Isabet aninda cikan parcaciklarin rengi. Fiziksel = sicak, ruhani = soguk secilmeli.")]
        [SerializeField] private Color impactColor = new Color(1f, 0.6f, 0.25f);

        [Header("Hata Ayiklama")]
        [SerializeField] private bool drawGizmos = true;

        private float _nextAttackTime;
        private readonly Collider2D[] _results = new Collider2D[8];
        private ContactFilter2D _filter;
        private int _facing = 1;

        /// <summary>Son vurusta en az bir dogru hedefe isabet edildi mi? Efektler icin.</summary>
        public bool LastAttackConnected { get; private set; }
        /// <summary>Son vurusta yanlis formda bir dusmana carpildi mi? Sekme efekti icin.</summary>
        public bool LastAttackDeflected { get; private set; }
        /// <summary>Vurus tetiklenince (isabetten bagimsiz) calisir. Animasyon bunu dinler.</summary>
        public event System.Action OnAttackTriggered;

        private void Awake()
        {
            if (originTransform == null) originTransform = transform;

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true
            };
        }

        private void Update()
        {
            UpdateFacing();

            if (!input.AttackPressedThisFrame) return;
            if (Time.time < _nextAttackTime) return;

            _nextAttackTime = Time.time + cooldown;
            PerformAttack();
            OnAttackTriggered?.Invoke();
        }

        /// <summary>Bakis yonunu aktif kontrolcuden okur.</summary>
        private void UpdateFacing()
        {
            if (originTransform.TryGetComponent(out PlayerController body))
            {
                _facing = body.FacingDirection;
            }
            else if (originTransform.TryGetComponent(out SoulController soul))
            {
                _facing = soul.FacingDirection;
            }
        }

        private void PerformAttack()
        {
            LastAttackConnected = false;
            LastAttackDeflected = false;

            Vector2 center = (Vector2)originTransform.position
                             + new Vector2(hitboxOffset.x * _facing, hitboxOffset.y);

            int count = Physics2D.OverlapBox(center, hitboxSize, 0f, _filter, _results);
            for (int i = 0; i < count; i++)
            {
                Health target = Health.FindOn(_results[i]);
                if (target == null) continue;

                Vector2 hitDirection = new Vector2(_facing, 0.15f).normalized;
                HitResult result = target.TryTakeDamage(damage, damageType, hitDirection);
                if (result == HitResult.Damaged || result == HitResult.Killed)
                {
                    LastAttackConnected = true;
                    Vector2 hitPoint = _results[i].bounds.center;
                    ParticleFX.Impact(hitPoint, impactColor, new Vector2(_facing, 0.2f));
                }
                else if (result == HitResult.Deflected)
                {
                    LastAttackDeflected = true;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Transform origin = originTransform != null ? originTransform : transform;
            Vector2 center = (Vector2)origin.position
                             + new Vector2(hitboxOffset.x * _facing, hitboxOffset.y);

            Gizmos.color = damageType == DamageType.Physical
                ? new Color(1f, 0.55f, 0.2f, 0.8f)
                : new Color(0.4f, 0.85f, 1f, 0.8f);
            Gizmos.DrawWireCube(center, hitboxSize);
        }
    }
}
