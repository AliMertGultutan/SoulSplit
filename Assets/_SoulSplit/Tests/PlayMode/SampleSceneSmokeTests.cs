using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SoulSplit.Combat;
using SoulSplit.Core;
using SoulSplit.Enemies;
using SoulSplit.Player;
using SoulSplit.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SoulSplit.Tests
{
    public class SampleSceneSmokeTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FormSwitch_TransfersAttackInputToActiveForm()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            GameObject body = GameObject.Find("Player");
            Assert.That(manager, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            MeleeAttack bodyAttack = body.GetComponent<MeleeAttack>();
            Assert.That(bodyAttack, Is.Not.Null);
            Assert.That(bodyAttack.AcceptsInput, Is.True);

            MethodInfo separate = typeof(SoulSwitchManager).GetMethod(
                "SeparateSoul", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(separate, Is.Not.Null);
            separate.Invoke(manager, null);
            yield return null;

            GameObject soul = GameObject.Find("Soul");
            Assert.That(soul, Is.Not.Null);
            MeleeAttack soulAttack = soul.GetComponent<MeleeAttack>();
            Assert.That(bodyAttack.AcceptsInput, Is.False);
            Assert.That(soulAttack.AcceptsInput, Is.True);

            manager.ForceReturnToBody();
            yield return null;

            Assert.That(bodyAttack.AcceptsInput, Is.True);
        }

        [UnityTest]
        public IEnumerator PauseMenu_AutoBootstrapsAndRestoresTimeScale()
        {
            yield return null;

            PauseMenuUI pauseMenu = Object.FindAnyObjectByType<PauseMenuUI>();
            Assert.That(pauseMenu, Is.Not.Null, "Oyun sahnesi duraklatma menusunu otomatik kurmalidir.");
            Assert.That(Object.FindAnyObjectByType<GameAudioFeedback>(), Is.Not.Null,
                "Oyun sahnesi ses geri bildirim sistemini otomatik kurmalidir.");

            pauseMenu.Open();
            Assert.That(pauseMenu.IsOpen, Is.True);
            Assert.That(TimeScaleController.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            pauseMenu.Close();
            Assert.That(pauseMenu.IsOpen, Is.False);
            Assert.That(TimeScaleController.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ManualSoulReturn_MaterializesBodyAtSoulPosition()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            GameObject body = GameObject.Find("Player");
            Assert.That(manager, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            MethodInfo separate = typeof(SoulSwitchManager).GetMethod(
                "SeparateSoul", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo returnToBody = typeof(SoulSwitchManager).GetMethod(
                "ReturnToBody", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(separate, Is.Not.Null);
            Assert.That(returnToBody, Is.Not.Null);

            separate.Invoke(manager, null);
            yield return null;

            GameObject soul = GameObject.Find("Soul");
            Vector2 destination = new Vector2(2f, 4f);
            soul.transform.position = destination;
            Physics2D.SyncTransforms();

            returnToBody.Invoke(manager, new object[] { false, true });
            yield return new WaitForFixedUpdate();

            Assert.That(Vector2.Distance(body.transform.position, destination), Is.LessThan(0.05f));
            Assert.That(manager.IsSoulActive, Is.False);
        }

        [UnityTest]
        public IEnumerator SystemReturn_KeepsBodyAtItsOriginalPosition()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            GameObject body = GameObject.Find("Player");
            Assert.That(manager, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            MethodInfo separate = typeof(SoulSwitchManager).GetMethod(
                "SeparateSoul", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector2 originalPosition = body.transform.position;
            separate.Invoke(manager, null);
            yield return null;

            GameObject.Find("Soul").transform.position = originalPosition + new Vector2(5f, 4f);
            manager.ForceReturnToBody();
            yield return null;

            Assert.That(Vector2.Distance(body.transform.position, originalPosition), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator SoulReturn_InsidePlatformFindsNearbyClearPosition()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            GameObject body = GameObject.Find("Player");
            GameObject platform = GameObject.Find("Platform_A");
            Assert.That(manager, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(platform, Is.Not.Null);

            MethodInfo separate = typeof(SoulSwitchManager).GetMethod(
                "SeparateSoul", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo returnToBody = typeof(SoulSwitchManager).GetMethod(
                "ReturnToBody", BindingFlags.Instance | BindingFlags.NonPublic);
            separate.Invoke(manager, null);
            yield return null;

            Vector2 blockedDestination = platform.transform.position;
            GameObject.Find("Soul").transform.position = blockedDestination;
            Physics2D.SyncTransforms();
            returnToBody.Invoke(manager, new object[] { false, true });
            yield return new WaitForFixedUpdate();

            CapsuleCollider2D capsule = body.GetComponent<CapsuleCollider2D>();
            Collider2D[] overlaps = Physics2D.OverlapCapsuleAll(
                (Vector2)body.transform.position + capsule.offset,
                capsule.size, capsule.direction, 0f, 1 << 8);

            Assert.That(Vector2.Distance(body.transform.position, blockedDestination), Is.LessThanOrEqualTo(2.6f));
            Assert.That(overlaps.Any(hit => hit != null && !hit.isTrigger), Is.False,
                "Beden ruhun yakininda fakat platform collider'inin disinda olmalidir.");
        }

        [UnityTest]
        public IEnumerator GhostAttack_DamagesPhysicalEnemyCaughtInHitbox()
        {
            GameObject ghostObject = GameObject.Find("GhostEnemy_Serpent");
            GameObject physicalObject = GameObject.Find("PhysicalEnemy_Guardian");
            Assert.That(ghostObject, Is.Not.Null);
            Assert.That(physicalObject, Is.Not.Null);

            GhostEnemy ghost = ghostObject.GetComponent<GhostEnemy>();
            Health physicalHealth = physicalObject.GetComponent<Health>();
            Assert.That(ghost, Is.Not.Null);
            Assert.That(physicalHealth, Is.Not.Null);

            physicalHealth.ResetHealth();
            int healthBefore = physicalHealth.Current;
            ghostObject.transform.position = physicalObject.transform.position + Vector3.left * 0.5f;
            Physics2D.SyncTransforms();

            MethodInfo applyAttackDamage = typeof(EnemyBase).GetMethod(
                "ApplyAttackDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyAttackDamage, Is.Not.Null);
            applyAttackDamage.Invoke(ghost, null);
            yield return null;

            Assert.That(physicalHealth.Current, Is.LessThan(healthBefore),
                "Ruhani dusmanin saldirisi fiziksel dusmana hasar vermelidir.");
        }

        [UnityTest]
        public IEnumerator GhostEnemy_TargetsAndDamagesPhysicalHero()
        {
            GameObject ghostObject = GameObject.Find("GhostEnemy_Serpent");
            GameObject body = GameObject.Find("Player");
            Assert.That(ghostObject, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            GhostEnemy ghost = ghostObject.GetComponent<GhostEnemy>();
            Health bodyHealth = body.GetComponent<Health>();
            Assert.That(ghost, Is.Not.Null);
            Assert.That(bodyHealth, Is.Not.Null);

            MethodInfo findTarget = typeof(GhostEnemy).GetMethod(
                "FindTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(findTarget, Is.Not.Null);
            Assert.That(findTarget.Invoke(ghost, null), Is.EqualTo(body.transform),
                "Ruh bedende olsa bile ruhani dusman fiziksel kahramani hedeflemeli.");

            bodyHealth.ResetHealth();
            int healthBefore = bodyHealth.Current;
            ghostObject.transform.position = body.transform.position + Vector3.left * 0.5f;
            Physics2D.SyncTransforms();

            MethodInfo applyAttackDamage = typeof(EnemyBase).GetMethod(
                "ApplyAttackDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            applyAttackDamage.Invoke(ghost, null);
            yield return null;

            Assert.That(bodyHealth.Current, Is.LessThan(healthBefore),
                "Ruhani dusmanin saldirisi fiziksel kahramana hasar vermeli.");
        }

        [UnityTest]
        public IEnumerator Scene_HasExactlyOneEnabledGlobalLight()
        {
            yield return null;

            int globalLightCount = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include)
                .Count(light => light.enabled && light.lightType == Light2D.LightType.Global);

            Assert.That(globalLightCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExpandedLevel_HasPlayableRouteAndAdditionalEnemies()
        {
            yield return null;

            GameObject expansion = GameObject.Find("LevelExpansion_Act2");
            GameObject soulGate = GameObject.Find("Act2_SoulGate");
            GameObject finalFloor = GameObject.Find("Act2_Floor_FinalArena");
            GameObject winTrigger = GameObject.Find("WinTrigger");

            Assert.That(expansion, Is.Not.Null, "Act 2 kok nesnesi sahnede bulunmali.");
            Assert.That(soulGate, Is.Not.Null);
            Assert.That(soulGate.GetComponent<BoxCollider2D>(), Is.Not.Null);
            Assert.That(finalFloor, Is.Not.Null);
            Assert.That(finalFloor.GetComponent<BoxCollider2D>().enabled, Is.True);
            Assert.That(winTrigger, Is.Not.Null);
            Assert.That(winTrigger.transform.position.x, Is.GreaterThanOrEqualTo(224f));

            int enemyCount = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude).Length;
            Assert.That(enemyCount, Is.GreaterThanOrEqualTo(23),
                "Baslangictan finale kadar sahnede en az 23 aktif dusman olmali.");

            GameObject reinforcements = GameObject.Find("EnemyReinforcements_FullLevel");
            Assert.That(reinforcements, Is.Not.Null);
            Assert.That(reinforcements.transform.childCount, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator ExpandedLevel_ChasmHasSafeProgressionAndKillZone()
        {
            yield return null;

            Transform first = GameObject.Find("Act2_Chasm_Pillar1")?.transform;
            Transform second = GameObject.Find("Act2_Chasm_Pillar2")?.transform;
            Transform third = GameObject.Find("Act2_Chasm_Pillar3")?.transform;
            GameObject killZone = GameObject.Find("Act2_Chasm_KillZone");

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(third, Is.Not.Null);
            Assert.That(Vector2.Distance(first.position, second.position), Is.LessThan(5.5f));
            Assert.That(Vector2.Distance(second.position, third.position), Is.LessThan(5.5f));
            Assert.That(killZone, Is.Not.Null);
            Assert.That(killZone.GetComponent<Collider2D>().isTrigger, Is.True);
            Assert.That(killZone.GetComponent<KillZone>(), Is.Not.Null);
        }
    }
}
