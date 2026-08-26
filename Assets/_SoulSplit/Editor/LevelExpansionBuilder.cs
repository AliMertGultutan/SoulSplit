using System;
using System.Collections.Generic;
using SoulSplit.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulSplit.Editor
{
    /// <summary>
    /// Mevcut bolumun sonuna ikinci bir perde ekler. Uretilen kok her calistirmada
    /// bastan kuruldugu icin seviye tasarimi guvenle tekrar duzenlenebilir.
    /// </summary>
    public static class LevelExpansionBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootName = "LevelExpansion_Act2";
        private const string ReinforcementRootName = "EnemyReinforcements_FullLevel";
        private const string StonePath = "Assets/_SoulSplit/Art/Environment/Tile_Stone.png";
        private const string EdgePath = "Assets/_SoulSplit/Art/Environment/Tile_StoneEdge.png";
        private const string GuardianPath = "Assets/_SoulSplit/Prefabs/Enemies/PhysicalEnemy_Guardian.prefab";
        private const string FinalGuardianPath = "Assets/_SoulSplit/Prefabs/Enemies/PhysicalEnemy_Guardian_Final.prefab";
        private const string SerpentPath = "Assets/_SoulSplit/Prefabs/Enemies/GhostEnemy_Serpent.prefab";
        private const string MaskPath = "Assets/_SoulSplit/Prefabs/Enemies/GhostEnemy_MaskSwarm.prefab";
        private const float EdgeHeight = 1.2f;

        private readonly struct PlatformSpec
        {
            public readonly string Name;
            public readonly Vector2 Position;
            public readonly Vector2 Size;
            public readonly bool Underhang;

            public PlatformSpec(string name, float x, float y, float width, float height, bool underhang = false)
            {
                Name = name;
                Position = new Vector2(x, y);
                Size = new Vector2(width, height);
                Underhang = underhang;
            }
        }

        private readonly struct EnemySpec
        {
            public readonly string Name;
            public readonly string PrefabPath;
            public readonly Vector2 Position;

            public EnemySpec(string name, string prefabPath, float x, float y)
            {
                Name = name;
                PrefabPath = prefabPath;
                Position = new Vector2(x, y);
            }
        }

        private static readonly PlatformSpec[] Platforms =
        {
            // Gecis avlusu: once genis zemin, sonra ruh formuyla asilabilen muhurlu kapi.
            new("Act2_Floor_Entry", 166f, -1f, 20f, 2f),
            new("Act2_Entry_WestLedge", 161.5f, 2.4f, 3.6f, 0.65f, true),
            new("Act2_Entry_CenterLedge", 167f, 4.25f, 3.4f, 0.65f, true),
            new("Act2_Entry_EastLedge", 172.4f, 2.55f, 3.4f, 0.65f, true),
            new("Act2_SoulGate", 176f, 4f, 1.15f, 8f),

            // Karma arena: fiziksel ve ruhani dusmanlar ayni dovus alaninda.
            new("Act2_Floor_MixedArena", 184f, -1f, 16f, 2f),
            new("Act2_Mixed_WestLedge", 180.2f, 2.35f, 3.3f, 0.65f, true),
            new("Act2_Mixed_CenterLedge", 185.5f, 4.25f, 3.6f, 0.65f, true),
            new("Act2_Mixed_EastLedge", 190.2f, 2.45f, 2.8f, 0.65f, true),

            // Kirik kopru: araliklar cift ziplama icin okunakli ve affedicidir.
            new("Act2_Chasm_Pillar1", 194.5f, -0.2f, 3.2f, 2.8f),
            new("Act2_Chasm_Pillar2", 199.5f, 1.05f, 3f, 2.5f),
            new("Act2_Chasm_Pillar3", 204.3f, 0.05f, 3.2f, 2.7f),

            // Son avlu: genis merkez ve iki kanat, kalabalik savasta kacis rotasi verir.
            new("Act2_Floor_FinalArena", 216f, -1f, 20f, 2f),
            new("Act2_Final_WestLedge", 211.5f, 2.35f, 3.6f, 0.65f, true),
            new("Act2_Final_CenterLedge", 216.5f, 4.3f, 4f, 0.7f, true),
            new("Act2_Final_EastLedge", 221.3f, 2.45f, 3.4f, 0.65f, true),
        };

        private static readonly EnemySpec[] Enemies =
        {
            new("Act2_Guardian_Entry", GuardianPath, 163.8f, 1.1f),
            new("Act2_Serpent_Entry", SerpentPath, 170f, 5.4f),
            new("Act2_Guardian_Mixed", GuardianPath, 182.5f, 1.1f),
            new("Act2_MaskSwarm_Mixed", MaskPath, 188f, 4.9f),
            new("Act2_Serpent_Chasm", SerpentPath, 200f, 6.2f),
            new("Act2_MaskSwarm_Final", MaskPath, 212.5f, 5.1f),
            new("Act2_Guardian_FinalWest", GuardianPath, 216.2f, 1.1f),
            new("Act2_Guardian_Final", FinalGuardianPath, 221f, 1.1f),
        };

        private static readonly EnemySpec[] FullLevelReinforcements =
        {
            // Erken oyun: saldiri egitiminden sonra baslar, hareket ogretimini bolmez.
            new("Reinforcement_Guardian_Early", GuardianPath, 21.8f, 1.1f),
            new("Reinforcement_Serpent_Chimney", SerpentPath, 28.8f, 17.2f),

            // Ruh girisi ve ikinci oda.
            new("Reinforcement_Guardian_Room2", GuardianPath, 37f, 1.1f),
            new("Reinforcement_Mask_Room2", MaskPath, 46.2f, 5.4f),

            // Kirik zemin bolgesi: havada baski ve inis noktasinda yakin dovus.
            new("Reinforcement_Guardian_ChasmEntry", GuardianPath, 52.5f, 1.1f),
            new("Reinforcement_Serpent_Chasm", SerpentPath, 65.5f, 5.2f),
            new("Reinforcement_Guardian_ChasmExit", GuardianPath, 80f, 1.1f),

            // Gec oyun arenalari: mevcut dusmanlarla karma gruplar olusturur.
            new("Reinforcement_Mask_Room4", MaskPath, 96f, 5.1f),
            new("Reinforcement_Guardian_Room5", GuardianPath, 113.5f, 1.1f),
            new("Reinforcement_Serpent_Room5Exit", SerpentPath, 128f, 5.3f),
        };

        [MenuItem("SoulSplit/Level/Rebuild Act 2 Expansion")]
        public static void RebuildFromMenu() => Rebuild(false);

        public static void RebuildFromCommandLine() => Rebuild(true);

        private static void Rebuild(bool openScene)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (openScene || scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Sprite stone = AssetDatabase.LoadAssetAtPath<Sprite>(StonePath);
            Sprite edge = AssetDatabase.LoadAssetAtPath<Sprite>(EdgePath);
            if (stone == null || edge == null)
                throw new InvalidOperationException("Act 2 platform sprite'lari yuklenemedi.");

            Material material = FindSceneObject("Ground_Floor")?.GetComponent<SpriteRenderer>()?.sharedMaterial;
            GameObject oldRoot = FindSceneObject(RootName);
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);

            GameObject root = new GameObject(RootName);
            foreach (PlatformSpec spec in Platforms)
                CreatePlatform(root.transform, spec, stone, edge, material);

            CreateCheckpoint(root.transform, "Act2_Checkpoint_Entry", new Vector2(159.2f, 1.25f));
            CreateCheckpoint(root.transform, "Act2_Checkpoint_MixedArena", new Vector2(179f, 1.25f));
            CreateCheckpoint(root.transform, "Act2_Checkpoint_FinalArena", new Vector2(208.5f, 1.25f));
            CreateKillZone(root.transform, "Act2_Chasm_KillZone", new Vector2(199.5f, -6.5f), new Vector2(15f, 5f));

            foreach (EnemySpec spec in Enemies)
                CreateEnemy(root.transform, spec);

            GameObject oldReinforcementRoot = FindSceneObject(ReinforcementRootName);
            if (oldReinforcementRoot != null) UnityEngine.Object.DestroyImmediate(oldReinforcementRoot);
            GameObject reinforcementRoot = new GameObject(ReinforcementRootName);
            foreach (EnemySpec spec in FullLevelReinforcements)
                CreateEnemy(reinforcementRoot.transform, spec);

            CloneBackdrop(root.transform, "Background_Ruins_x150", "Act2_Background_x198", 198f);
            CloneBackdrop(root.transform, "Background_Ruins_x150", "Act2_Background_x230", 230f);

            GameObject winTrigger = FindSceneObject("WinTrigger");
            if (winTrigger == null)
                throw new InvalidOperationException("WinTrigger bulunamadi; yeni bitis noktasi ayarlanamadi.");
            winTrigger.transform.position = new Vector3(224.2f, 2.2f, winTrigger.transform.position.z);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Seviye genisletmesi kuruldu: {Platforms.Length} platform, " +
                      $"{Enemies.Length + FullLevelReinforcements.Length} yeni dusman, 3 kontrol noktasi.");
        }

        private static void CreatePlatform(Transform parent, PlatformSpec spec, Sprite stone, Sprite edge, Material material)
        {
            GameObject platform = new GameObject(spec.Name);
            platform.layer = 8;
            platform.transform.SetParent(parent, false);
            platform.transform.position = new Vector3(spec.Position.x, spec.Position.y, 0f);

            SpriteRenderer body = platform.AddComponent<SpriteRenderer>();
            body.sprite = stone;
            body.drawMode = SpriteDrawMode.Tiled;
            body.size = spec.Size;
            if (material != null) body.sharedMaterial = material;

            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            collider.size = spec.Size;

            GameObject topEdge = new GameObject("TopEdge");
            topEdge.layer = 8;
            topEdge.transform.SetParent(platform.transform, false);
            topEdge.transform.localPosition = new Vector3(0f, spec.Size.y * 0.5f - EdgeHeight * 0.5f, -0.02f);
            SpriteRenderer edgeRenderer = topEdge.AddComponent<SpriteRenderer>();
            edgeRenderer.sprite = edge;
            edgeRenderer.drawMode = SpriteDrawMode.Tiled;
            edgeRenderer.size = new Vector2(spec.Size.x, EdgeHeight);
            edgeRenderer.sortingOrder = 1;
            if (material != null) edgeRenderer.sharedMaterial = material;

            if (!spec.Underhang) return;
            GameObject underhang = new GameObject("Underhang");
            underhang.layer = 8;
            underhang.transform.SetParent(platform.transform, false);
            underhang.transform.localPosition = new Vector3(0f, -spec.Size.y * 0.5f - 0.35f, 0.02f);
            SpriteRenderer underhangRenderer = underhang.AddComponent<SpriteRenderer>();
            underhangRenderer.sprite = stone;
            underhangRenderer.drawMode = SpriteDrawMode.Tiled;
            underhangRenderer.size = new Vector2(spec.Size.x * 0.58f, 0.9f);
            underhangRenderer.sortingOrder = -1;
            underhangRenderer.color = new Color(0.72f, 0.77f, 0.8f, 1f);
            if (material != null) underhangRenderer.sharedMaterial = material;
        }

        private static void CreateCheckpoint(Transform parent, string name, Vector2 position)
        {
            GameObject checkpoint = new GameObject(name);
            checkpoint.transform.SetParent(parent, false);
            checkpoint.transform.position = position;
            BoxCollider2D collider = checkpoint.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 2.5f);
            collider.isTrigger = true;
            checkpoint.AddComponent<CheckpointTrigger>();
        }

        private static void CreateKillZone(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.position = position;
            BoxCollider2D collider = zone.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = true;
            zone.AddComponent<KillZone>();
        }

        private static void CreateEnemy(Transform parent, EnemySpec spec)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null) throw new InvalidOperationException($"Dusman prefab'i yuklenemedi: {spec.PrefabPath}");
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            enemy.name = spec.Name;
            enemy.transform.position = new Vector3(spec.Position.x, spec.Position.y, 0f);
        }

        private static void CloneBackdrop(Transform parent, string sourceName, string cloneName, float x)
        {
            GameObject source = FindSceneObject(sourceName);
            if (source == null) return;
            GameObject clone = UnityEngine.Object.Instantiate(source, parent);
            clone.name = cloneName;
            clone.transform.position = new Vector3(x, source.transform.position.y, source.transform.position.z);
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == objectName && candidate.scene.path == ScenePath)
                    return candidate;
            }
            return null;
        }
    }
}
