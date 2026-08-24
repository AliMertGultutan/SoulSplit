using UnityEngine;

namespace SoulSplit.Combat
{
    /// <summary>
    /// Ruh objesine gelen hasari BEDENIN can havuzuna yonlendirir.
    ///
    /// TASARIM KARARI — Ortak can havuzu:
    /// Ruh ve beden icin ayri can havuzu yerine TEK havuz secildi.
    /// Ayri havuzlar riski boler: bedenin dusmesi tek basina oyunu bitirmez,
    /// oyuncu "ruhla idare ederim" diye dusunur. Tek havuzda ise ruh
    /// formundayken oyuncu AYNI ANDA iki yerden vurulabilir — geride kalan
    /// beden fiziksel dusmanlara, ruhun kendisi hayaletlere acik. Ayni kaynagi
    /// iki cepheden harcamak, raporda "risk yonetimi problemi" diye
    /// tanimlanan gerilimi dogrudan uretir.
    /// </summary>
    public class DamageRelay : MonoBehaviour
    {
        [Header("Referans")]
        [Tooltip("Hasarin yonlendirilecegi asil can havuzu (bedenin Health'i).")]
        [SerializeField] private Health targetHealth;

        public Health Target => targetHealth;
    }
}
