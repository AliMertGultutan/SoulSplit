#if UNITY_EDITOR
using System.IO;
using System.Text;
using SoulSplit.Enemies;
using SoulSplit.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulSplit.EditorTools
{
    /// <summary>
    /// Oynanis sahnesindeki her kok nesneyi kategori bazli bir prefab varligina
    /// donusturur ve sahnedeki nesneyi o prefaba baglar. Tekrar calistirilabilir.
    /// </summary>
    public static class GameplayPrefabLibraryBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootFolder = "Assets/_SoulSplit/Prefabs";

        [InitializeOnLoadMethod]
        private static void BuildOnceWhenScriptsReload()
        {
            // Bu proje icin istenen prefab kutuphanesi henuz uretilmediyse,
            // acik Editor derlemeyi bitirdikten sonra bir kez kur. Player prefabi
            // tamamlanma isaretidir; sonraki domain reload'larda yeniden calismaz.
            if (File.Exists(RootFolder + "/Characters/Player.prefab")) return;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                try
                {
                    BuildAllGameplayPrefabs();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("SoulSplit/Build/Build All Gameplay Prefabs")]
        public static void BuildAllGameplayPrefabs()
        {
            Scene current = SceneManager.GetActiveScene();
            if (current.isDirty)
                throw new System.InvalidOperationException("Acik sahnede kaydedilmemis degisiklik var. Once sahneyi kaydedin.");

            EnsureFolder(RootFolder + "/Characters");
            EnsureFolder(RootFolder + "/Environment");
            EnsureFolder(RootFolder + "/Systems");
            EnsureFolder(RootFolder + "/UI");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ConfigurePlayer(scene);

            int created = 0;
            int alreadyLinked = 0;
            StringBuilder report = new StringBuilder();
            GameObject[] roots = scene.GetRootGameObjects();

            foreach (GameObject root in roots)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                {
                    alreadyLinked++;
                    continue;
                }

                string category = ResolveCategory(root);
                string fileName = Sanitize(root.name) + ".prefab";
                string path = RootFolder + "/" + category + "/" + fileName;
                bool success;
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction, out success);
                if (!success)
                    throw new System.InvalidOperationException("Prefab olusturulamadi: " + root.name);

                created++;
                report.AppendLine(path);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GameplayPrefabLibraryBuilder] {created} yeni kok prefab baglandi, " +
                      $"{alreadyLinked} mevcut prefab korundu.\n{report}");
        }

        private static void ConfigurePlayer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                PlayerController controller = root.GetComponentInChildren<PlayerController>(true);
                if (controller == null) continue;

                if (controller.GetComponent<NoHitKillRecovery>() == null)
                    controller.gameObject.AddComponent<NoHitKillRecovery>();

                SerializedObject serialized = new SerializedObject(controller);
                SetFloat(serialized, "maxSpeed", 11.5f);
                SetFloat(serialized, "groundAcceleration", 135f);
                SetFloat(serialized, "groundDeceleration", 155f);
                SetFloat(serialized, "airAcceleration", 78f);
                SetFloat(serialized, "airDeceleration", 42f);
                SetFloat(serialized, "apexHangVelocityThreshold", 1.35f);
                SetFloat(serialized, "apexGravityMultiplier", 0.42f);
                SetFloat(serialized, "apexHorizontalAccelerationMultiplier", 1.18f);
                SetFloat(serialized, "cornerCorrectionDistance", 0.18f);
                SetFloat(serialized, "cornerCheckDistance", 0.12f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            throw new System.InvalidOperationException("SampleScene icinde PlayerController bulunamadi.");
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new System.InvalidOperationException("PlayerController alani bulunamadi: " + propertyName);
            property.floatValue = value;
        }

        private static string ResolveCategory(GameObject root)
        {
            if (root.GetComponentInChildren<PlayerController>(true) != null ||
                root.GetComponentInChildren<SoulController>(true) != null ||
                root.GetComponentInChildren<EnemyBase>(true) != null)
                return "Characters";
            if (root.GetComponentInChildren<Canvas>(true) != null || root.GetComponent<RectTransform>() != null)
                return "UI";
            if (root.GetComponentInChildren<Collider2D>(true) != null ||
                root.GetComponentInChildren<SpriteRenderer>(true) != null)
                return "Environment";
            return "Systems";
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "Unnamed" : value.Trim();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
