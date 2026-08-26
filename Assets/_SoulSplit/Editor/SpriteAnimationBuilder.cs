using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SoulSplit.EditorTools
{
    /// <summary>
    /// Dilimlenmis bir sprite sheet klasorunden OTOMATIK olarak AnimationClip'ler
    /// ve tam kurulu bir AnimatorController uretir.
    ///
    /// Beklenen sprite isimlendirmesi: &lt;Durum&gt;_&lt;sira&gt;
    ///   ornek: Idle_0, Idle_1, Walk_0, Walk_1, Run_0, Attack_0, Hurt_0, Death_0
    /// Sprite Editor'de dilimledikten sonra kareleri bu sekilde yeniden
    /// adlandirmak yeterli — gerisi burada halloluyor.
    ///
    /// Sondaki sayi SAYISAL olarak siralanir (Walk_10, Walk_2'den SONRA gelir);
    /// duz alfabetik siralama kare sirasini bozardi.
    /// </summary>
    public static class SpriteAnimationBuilder
    {
        /// <summary>Dongu halinde oynayacak durumlar. Digerleri tek sefer oynar.</summary>
        private static readonly HashSet<string> DefaultLooping =
            new HashSet<string> { "Idle", "Walk", "Run" };

        /// <summary>Sprite adinin sonundaki "_12" gibi sira numarasini yakalar.</summary>
        private static readonly Regex TrailingIndex = new Regex(@"^(?<state>.+?)[_\-\s]?(?<index>\d+)$");

        /// <param name="spriteFolder">Dilimlenmis PNG'lerin bulundugu klasor (Assets/... ile baslar).</param>
        /// <param name="outputFolder">Uretilen .anim ve .controller dosyalarinin yazilacagi klasor.</param>
        /// <param name="controllerName">Uretilecek AnimatorController'in adi (uzantisiz).</param>
        /// <param name="frameRate">Klip kare hizi. Pixel-art'ta 10-12 genelde daha "keskin" durur.</param>
        public static string Build(
            string spriteFolder,
            string outputFolder,
            string controllerName = "PlayerAnimator",
            float frameRate = 12f)
        {
            var log = new StringBuilder();

            if (!AssetDatabase.IsValidFolder(spriteFolder))
                return $"HATA: klasor bulunamadi -> {spriteFolder}";

            EnsureFolder(outputFolder);

            // --- 1) Klasordeki tum Sprite alt-varliklarini topla ---
            var sprites = new List<Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { spriteFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is Sprite s) sprites.Add(s);
                }
            }

            if (sprites.Count == 0)
                return $"HATA: {spriteFolder} altinda hic Sprite yok. " +
                       "PNG'yi Sprite Mode: Multiple yapip Sprite Editor'de dilimlediniz mi?";

            // --- 2) Isim onekine gore grupla (Walk_0, Walk_1 -> "Walk") ---
            var groups = new Dictionary<string, List<(int index, Sprite sprite)>>();
            var ungrouped = new List<string>();

            foreach (Sprite s in sprites)
            {
                Match m = TrailingIndex.Match(s.name);
                if (!m.Success) { ungrouped.Add(s.name); continue; }

                string state = m.Groups["state"].Value;
                int index = int.Parse(m.Groups["index"].Value);

                if (!groups.TryGetValue(state, out var list))
                {
                    list = new List<(int, Sprite)>();
                    groups[state] = list;
                }
                list.Add((index, s));
            }

            if (ungrouped.Count > 0)
                log.AppendLine($"UYARI: sira numarasi olmayan {ungrouped.Count} sprite atlandi " +
                               $"(ornek: {string.Join(", ", ungrouped.Take(3))})");

            if (groups.Count == 0)
                return "HATA: hicbir sprite '<Durum>_<sayi>' desenine uymuyor. " +
                       "Sprite Editor'de kareleri Idle_0, Walk_0... seklinde adlandirin.";

            // --- 3) Her grup icin AnimationClip uret ---
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var kv in groups)
            {
                string state = kv.Key;
                // Sayisal siralama: Walk_10, Walk_2'den sonra gelmeli.
                var ordered = kv.Value.OrderBy(t => t.index).Select(t => t.sprite).ToArray();

                var clip = new AnimationClip { frameRate = frameRate };

                var binding = new EditorCurveBinding
                {
                    type = typeof(SpriteRenderer),
                    path = "",              // Animator ile ayni GameObject'te
                    propertyName = "m_Sprite"
                };

                var keys = new ObjectReferenceKeyframe[ordered.Length];
                for (int i = 0; i < ordered.Length; i++)
                {
                    keys[i] = new ObjectReferenceKeyframe
                    {
                        time = i / frameRate,
                        value = ordered[i]
                    };
                }
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = DefaultLooping.Contains(state);
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                string clipPath = $"{outputFolder}/{state}.anim";
                AssetDatabase.CreateAsset(clip, clipPath);
                clips[state] = clip;

                log.AppendLine($"klip: {state} ({ordered.Length} kare, " +
                               $"{(settings.loopTime ? "dongulu" : "tek sefer")})");
            }

            // --- 4) AnimatorController kur ---
            string controllerPath = $"{outputFolder}/{controllerName}.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();

            foreach (var kv in clips)
            {
                AnimatorState st = sm.AddState(kv.Key);
                st.motion = kv.Value;
                states[kv.Key] = st;
            }

            if (states.TryGetValue("Idle", out AnimatorState idle))
                sm.defaultState = idle;

            // Locomotion gecisleri: hasExitTime = FALSE olmali, yoksa yon
            // degistirince animasyon bitene kadar takilir (klasik "gec tepki" hatasi).
            AddTransition(states, "Idle", "Walk", t =>
            {
                t.hasExitTime = false; t.duration = 0.05f;
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }, log);

            AddTransition(states, "Walk", "Idle", t =>
            {
                t.hasExitTime = false; t.duration = 0.05f;
                t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }, log);

            AddTransition(states, "Walk", "Run", t =>
            {
                t.hasExitTime = false; t.duration = 0.08f;
                t.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
            }, log);

            AddTransition(states, "Run", "Walk", t =>
            {
                t.hasExitTime = false; t.duration = 0.08f;
                t.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");
            }, log);

            // Attack / Hurt / Death: Any State'ten trigger ile girilir; boylece
            // hangi durumda olursak olalim tetiklenebilir. Cikis ise hasExitTime
            // = TRUE — animasyon yarida kesilmesin.
            SetupOneShot(sm, states, "Attack", "Attack", log);
            SetupOneShot(sm, states, "Hurt", "Hurt", log);

            if (states.TryGetValue("Death", out AnimatorState death))
            {
                AnimatorStateTransition any = sm.AddAnyStateTransition(death);
                any.hasExitTime = false;
                any.duration = 0.02f;
                any.canTransitionToSelf = false;
                any.AddCondition(AnimatorConditionMode.If, 0f, "Death");
                log.AppendLine("gecis: AnyState -> Death (Death trigger, geri donus yok)");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"\nTAMAM -> {controllerPath}");
            log.AppendLine($"Bulunan durumlar: {string.Join(", ", states.Keys.OrderBy(k => k))}");
            return log.ToString();
        }

        /// <summary>Any State -> durum (trigger ile) ve durum -> Idle (animasyon bitince) ciftini kurar.</summary>
        private static void SetupOneShot(
            AnimatorStateMachine sm,
            Dictionary<string, AnimatorState> states,
            string stateName,
            string triggerName,
            StringBuilder log)
        {
            if (!states.TryGetValue(stateName, out AnimatorState state)) return;

            AnimatorStateTransition any = sm.AddAnyStateTransition(state);
            any.hasExitTime = false;
            any.duration = 0.02f;
            any.canTransitionToSelf = false;   // saldiri kendini yeniden tetikleyip takilmasin
            any.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

            if (states.TryGetValue("Idle", out AnimatorState idle))
            {
                AnimatorStateTransition back = state.AddTransition(idle);
                back.hasExitTime = true;   // animasyon TAMAMLANSIN
                back.exitTime = 1f;
                back.duration = 0.05f;
            }

            log.AppendLine($"gecis: AnyState -> {stateName} ({triggerName}) -> Idle (exitTime 1.0)");
        }

        private static void AddTransition(
            Dictionary<string, AnimatorState> states,
            string from,
            string to,
            System.Action<AnimatorStateTransition> configure,
            StringBuilder log)
        {
            if (!states.TryGetValue(from, out AnimatorState a) ||
                !states.TryGetValue(to, out AnimatorState b))
            {
                log.AppendLine($"atlandi: {from} -> {to} (durumlardan biri yok)");
                return;
            }

            configure(a.AddTransition(b));
            log.AppendLine($"gecis: {from} -> {to}");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];                 // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
