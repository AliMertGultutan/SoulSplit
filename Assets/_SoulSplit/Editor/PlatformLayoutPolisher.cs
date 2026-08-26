using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulSplit.Editor
{
    /// <summary>
    /// SampleScene platform rotasini okunakli, tekrar uretilebilir bir duzene getirir.
    /// Mevcut oynanis nesnelerini korur; yalnizca zeminleri bicimlendirir ve gec oyun
    /// odalarindaki uretilmis platform kumesini yeniden kurar.
    /// </summary>
    public static class PlatformLayoutPolisher
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string StonePath = "Assets/_SoulSplit/Art/Environment/Tile_Stone.png";
        private const string EdgePath = "Assets/_SoulSplit/Art/Environment/Tile_StoneEdge.png";
        private const string GeneratedRootName = "PlatformLayout_Polished";
        private const float EdgeHeight = 1.2f;

        private sealed class PlatformSpec
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

        private static readonly PlatformSpec[] ExistingLayout =
        {
            new("Ground_Floor", 14f, -1f, 38f, 2f),
            new("Platform_A", 6.7f, 2.55f, 4.4f, 0.7f),
            new("Platform_Step_AB", 10f, 4.15f, 1.9f, 0.55f),
            new("Platform_B", 13.1f, 5.65f, 4.3f, 0.7f),
            new("Platform_Step_BC", 16.25f, 7.15f, 1.9f, 0.55f),
            new("Platform_C", 19.4f, 8.55f, 4.5f, 0.7f),
            new("Platform_Landing", 22.85f, 8.15f, 2.4f, 0.65f),
            new("Platform_ChimneyRest", 27f, 11.25f, 1.8f, 0.45f),
            new("Platform_ExitTop", 28f, 16f, 4.2f, 0.65f),
            new("Ground_Rise1", 24.35f, 0.2f, 2.8f, 1.4f),
            new("Ground_Rise2", 27.25f, 0.65f, 2.8f, 2.3f),
            new("Ground_Rise3", 30f, 0.2f, 2.2f, 1.4f),
            new("Wall_Left", 25.5f, 11.5f, 1f, 8f),
            new("Wall_Right", 28.5f, 11.5f, 1f, 8f),

            new("Floor_Room2", 40.5f, -1f, 15f, 2f),
            new("SoulIntro_Ledge", 37.8f, 2.8f, 3.8f, 0.65f),
            new("SoulIntro_NookFloor", 45f, 2.9f, 3.2f, 0.65f),

            new("Floor_Room3_Before", 53f, -1f, 10f, 2f),
            new("Chasm_Step1", 61f, -0.45f, 2.8f, 2.5f),
            new("Chasm_Step2", 66.25f, 0.15f, 2.3f, 2.7f),
            new("Chasm_Step3", 71.6f, -0.25f, 3f, 2.3f),
            new("Floor_Room3_After", 81f, -1f, 10f, 2f),

            new("Floor_Room4", 100f, -1f, 22f, 2f),
            new("Floor_Room5", 119f, -1f, 20f, 2f),
            new("Floor_Room6", 141f, -1f, 30f, 2f),
        };

        private static readonly PlatformSpec[] GeneratedLayout =
        {
            // Muhafiz odasi: merkezdeki dovus alanini bos birakip iki kanada cikis verir.
            new("Arena4_WestLedge", 96.1f, 2.35f, 3.8f, 0.7f, true),
            new("Arena4_EastLedge", 107.1f, 3.15f, 3.4f, 0.7f, true),

            // Ruh dusmani odasi: yukari-asagi dolasimi destekleyen yumusak bir yay.
            new("Arena5_WestLedge", 114.3f, 2.2f, 3.6f, 0.65f, true),
            new("Arena5_CenterLedge", 119.7f, 4.15f, 3.2f, 0.65f, true),
            new("Arena5_EastLedge", 125.1f, 2.55f, 3.6f, 0.65f, true),

            // Final yaklasimi: maske surusune dikey alan, final muhafiza acik zemin.
            new("Arena6_WestLedge", 132.7f, 2.35f, 3.6f, 0.7f, true),
            new("Arena6_CenterLedge", 138f, 4.25f, 4.1f, 0.7f, true),
            new("Arena6_EastLedge", 143.1f, 2.45f, 3f, 0.65f, true),
        };

        [MenuItem("SoulSplit/Level/Rebuild Polished Platform Layout")]
        public static void RebuildFromMenu()
        {
            Rebuild(openScene: false);
        }

        // -executeMethod SoulSplit.Editor.PlatformLayoutPolisher.RebuildFromCommandLine
        public static void RebuildFromCommandLine()
        {
            Rebuild(openScene: true);
        }

        private static void Rebuild(bool openScene)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (openScene || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Sprite stone = AssetDatabase.LoadAssetAtPath<Sprite>(StonePath);
            Sprite edge = AssetDatabase.LoadAssetAtPath<Sprite>(EdgePath);
            if (stone == null || edge == null)
            {
                throw new System.InvalidOperationException("Platform sprite'lari yuklenemedi.");
            }

            Material platformMaterial = FindSceneObject("Ground_Floor")?.GetComponent<SpriteRenderer>()?.sharedMaterial;

            foreach (PlatformSpec spec in ExistingLayout)
            {
                GameObject platform = FindSceneObject(spec.Name);
                if (platform == null)
                {
                    Debug.LogWarning($"Platform bulunamadi: {spec.Name}");
                    continue;
                }

                ConfigurePlatform(platform, spec, stone, edge, platformMaterial);
            }

            GameObject generatedRoot = FindSceneObject(GeneratedRootName);
            if (generatedRoot == null)
            {
                generatedRoot = new GameObject(GeneratedRootName);
            }

            var oldChildren = new List<GameObject>();
            foreach (Transform child in generatedRoot.transform)
            {
                oldChildren.Add(child.gameObject);
            }
            foreach (GameObject child in oldChildren)
            {
                Object.DestroyImmediate(child);
            }

            foreach (PlatformSpec spec in GeneratedLayout)
            {
                GameObject platform = new GameObject(spec.Name);
                platform.transform.SetParent(generatedRoot.transform, false);
                ConfigurePlatform(platform, spec, stone, edge, platformMaterial);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Platform duzeni yenilendi: {ExistingLayout.Length} mevcut, {GeneratedLayout.Length} yeni platform.");
        }

        private static void ConfigurePlatform(
            GameObject platform,
            PlatformSpec spec,
            Sprite stone,
            Sprite edge,
            Material material)
        {
            platform.layer = 8;
            platform.transform.position = new Vector3(spec.Position.x, spec.Position.y, 0f);
            platform.transform.rotation = Quaternion.identity;
            platform.transform.localScale = Vector3.one;

            SpriteRenderer body = GetOrAdd<SpriteRenderer>(platform);
            body.sprite = stone;
            body.drawMode = SpriteDrawMode.Tiled;
            body.size = spec.Size;
            body.sortingOrder = 0;
            body.color = Color.white;
            if (material != null) body.sharedMaterial = material;

            BoxCollider2D collider = GetOrAdd<BoxCollider2D>(platform);
            collider.size = spec.Size;
            collider.offset = Vector2.zero;
            collider.isTrigger = false;

            Transform edgeTransform = platform.transform.Find("TopEdge");
            GameObject edgeObject = edgeTransform != null ? edgeTransform.gameObject : new GameObject("TopEdge");
            edgeObject.layer = platform.layer;
            edgeObject.transform.SetParent(platform.transform, false);
            edgeObject.transform.localPosition = new Vector3(0f, spec.Size.y * 0.5f - EdgeHeight * 0.5f, -0.02f);
            edgeObject.transform.localRotation = Quaternion.identity;
            edgeObject.transform.localScale = Vector3.one;

            SpriteRenderer edgeRenderer = GetOrAdd<SpriteRenderer>(edgeObject);
            edgeRenderer.sprite = edge;
            edgeRenderer.drawMode = SpriteDrawMode.Tiled;
            edgeRenderer.size = new Vector2(spec.Size.x, EdgeHeight);
            edgeRenderer.sortingOrder = 1;
            edgeRenderer.color = Color.white;
            if (material != null) edgeRenderer.sharedMaterial = material;

            if (!spec.Underhang) return;

            Transform underhangTransform = platform.transform.Find("Underhang");
            GameObject underhang = underhangTransform != null ? underhangTransform.gameObject : new GameObject("Underhang");
            underhang.layer = platform.layer;
            underhang.transform.SetParent(platform.transform, false);
            underhang.transform.localPosition = new Vector3(0f, -spec.Size.y * 0.5f - 0.35f, 0.02f);
            underhang.transform.localRotation = Quaternion.identity;
            underhang.transform.localScale = Vector3.one;

            SpriteRenderer underhangRenderer = GetOrAdd<SpriteRenderer>(underhang);
            underhangRenderer.sprite = stone;
            underhangRenderer.drawMode = SpriteDrawMode.Tiled;
            underhangRenderer.size = new Vector2(spec.Size.x * 0.58f, 0.9f);
            underhangRenderer.sortingOrder = -1;
            underhangRenderer.color = new Color(0.72f, 0.77f, 0.8f, 1f);
            if (material != null) underhangRenderer.sharedMaterial = material;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == objectName && candidate.scene.path == ScenePath)
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
