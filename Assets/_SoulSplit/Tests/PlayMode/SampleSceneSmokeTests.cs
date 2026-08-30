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
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SoulSplit.Tests
{
    public class SampleSceneSmokeTests
    {
        private bool _hadMaterializationPreference;
        private bool _materializationPreference;

        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            _hadMaterializationPreference = GameplaySettings.HasMaterializationPreference;
            _materializationPreference = GameplaySettings.MaterializeAtSoulPosition;
            GameplaySettings.MaterializeAtSoulPosition = true;
            SceneManager.LoadScene("SampleScene");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreGameplaySettings()
        {
            if (_hadMaterializationPreference)
                GameplaySettings.MaterializeAtSoulPosition = _materializationPreference;
            else
                GameplaySettings.ResetMaterializationPreference();
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
        public IEnumerator SoulSurge_ActivatesOnlyWhenChargedAndAppliesBothFormBonuses()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            PlayerController body = Object.FindAnyObjectByType<PlayerController>();
            SoulController soul = Object.FindAnyObjectByType<SoulController>(FindObjectsInactive.Include);
            Assert.That(manager, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(soul, Is.Not.Null);
            Assert.That(manager.TryActivateUltimate(), Is.False, "Bos ultimate etkinlesmemelidir.");

            manager.AddUltimateCharge(1000f);
            Assert.That(manager.UltimateReady, Is.True);
            Assert.That(manager.TryActivateUltimate(), Is.True);
            yield return null;

            Assert.That(manager.IsUltimateActive, Is.True);
            Assert.That(manager.UltimateChargeNormalized, Is.Zero.Within(0.001f));
            Assert.That(body.MovementSpeedMultiplier, Is.GreaterThan(1f));
            Assert.That(soul.MovementSpeedMultiplier, Is.GreaterThan(1f));
            Assert.That(body.GetComponent<MeleeAttack>().DamageMultiplier, Is.GreaterThan(1f));
            Assert.That(soul.GetComponent<MeleeAttack>().DamageMultiplier, Is.GreaterThan(1f));

            MethodInfo endUltimate = typeof(SoulSwitchManager).GetMethod(
                "EndUltimate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(endUltimate, Is.Not.Null);
            endUltimate.Invoke(manager, null);

            Assert.That(manager.IsUltimateActive, Is.False);
            Assert.That(body.MovementSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(soul.MovementSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(body.GetComponent<MeleeAttack>().DamageMultiplier, Is.EqualTo(1f));
            Assert.That(soul.GetComponent<MeleeAttack>().DamageMultiplier, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator GameplayHud_CreatesReadableSoulSurgeMeter()
        {
            yield return null;

            GameObject ultimateMeter = GameObject.Find("UltimateMeter_BG");
            Assert.That(ultimateMeter, Is.Not.Null);
            Assert.That(ultimateMeter.GetComponent<Image>(), Is.Not.Null);

            Text stateText = ultimateMeter.GetComponentInChildren<Text>();
            Assert.That(stateText, Is.Not.Null);
            Assert.That(stateText.text, Does.Contain("SOUL SURGE"));
        }

        [UnityTest]
        public IEnumerator SuccessfulBodyAttack_FillsSoulSurgeCharge()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            GameObject body = GameObject.Find("Player");
            GameObject targetObject = GameObject.Find("PhysicalEnemy_Guardian");
            Assert.That(manager, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(targetObject, Is.Not.Null);

            MeleeAttack attack = body.GetComponent<MeleeAttack>();
            Health targetHealth = targetObject.GetComponent<Health>();
            Assert.That(attack, Is.Not.Null);
            Assert.That(targetHealth, Is.Not.Null);

            targetHealth.ResetHealth();
            targetObject.transform.position = body.transform.position + Vector3.right * 0.7f;
            Physics2D.SyncTransforms();

            MethodInfo performAttack = typeof(MeleeAttack).GetMethod(
                "PerformAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(performAttack, Is.Not.Null);
            performAttack.Invoke(attack, new object[] { AttackTier.Light });
            yield return null;

            Assert.That(attack.LastAttackConnected, Is.True);
            Assert.That(manager.UltimateChargeNormalized, Is.GreaterThan(0f),
                "Basarili fiziksel vurus Soul Surge gostergesini doldurmalidir.");
        }

        [UnityTest]
        public IEnumerator AttackPressedDuringCooldown_IsBufferedAndExecuted()
        {
            MeleeAttack attack = GameObject.Find("Player")?.GetComponent<MeleeAttack>();
            Assert.That(attack, Is.Not.Null);

            int triggerCount = 0;
            AttackTier lastTier = AttackTier.Light;
            attack.OnAttackTriggered += tier =>
            {
                triggerCount++;
                lastTier = tier;
            };

            Assert.That(attack.RequestAttack(AttackTier.Light), Is.True);
            yield return new WaitForSeconds(0.22f);
            Assert.That(attack.RequestAttack(AttackTier.Heavy), Is.True);
            Assert.That(attack.HasBufferedAttack, Is.True,
                "Cooldown sirasinda gelen ikinci saldiri kaybolmamalidir.");

            yield return new WaitForSeconds(0.2f);

            Assert.That(triggerCount, Is.EqualTo(2));
            Assert.That(lastTier, Is.EqualTo(AttackTier.Heavy));
            Assert.That(attack.HasBufferedAttack, Is.False);
        }

        [UnityTest]
        public IEnumerator SoulStep_DashesAndProtectsPlayerDuringItsWindow()
        {
            GameObject player = GameObject.Find("Player");
            PlayerController controller = player?.GetComponent<PlayerController>();
            Health health = player?.GetComponent<Health>();
            Rigidbody2D body = player?.GetComponent<Rigidbody2D>();
            CapsuleCollider2D capsule = player?.GetComponent<CapsuleCollider2D>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(health, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(capsule, Is.Not.Null);

            int healthBefore = health.Current;
            float standingHeight = capsule.size.y;
            Assert.That(controller.RequestDodge(), Is.True);
            yield return new WaitForFixedUpdate();

            Assert.That(controller.IsDodging, Is.True);
            Assert.That(controller.State, Is.EqualTo(PlayerState.Dashing));
            Assert.That(controller.IsCrouching, Is.True);
            Assert.That(capsule.size.y, Is.LessThan(standingHeight),
                "Takla sirasinda fiziksel profil alcak gecitlere sigacak kadar kuculmelidir.");
            Assert.That(Mathf.Abs(body.linearVelocity.x), Is.GreaterThan(15f));
            Assert.That(health.IsInvincible, Is.True);
            Assert.That(health.TryTakeDamage(1, health.VulnerableTo == DamageRealm.Spiritual
                ? DamageType.Spiritual
                : DamageType.Physical), Is.EqualTo(HitResult.Ignored));
            Assert.That(health.Current, Is.EqualTo(healthBefore));
        }

        [UnityTest]
        public IEnumerator ConsecutiveHits_BuildFlowAndIncreaseSoulSurgeReward()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            Assert.That(manager, Is.Not.Null);

            MethodInfo registerHit = typeof(SoulSwitchManager).GetMethod(
                "HandleHitConfirmed", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(registerHit, Is.Not.Null);

            registerHit.Invoke(manager, new object[] { AttackTier.Light, HitResult.Damaged });
            registerHit.Invoke(manager, new object[] { AttackTier.Light, HitResult.Damaged });
            yield return null;

            Assert.That(manager.ComboCount, Is.EqualTo(2));
            Assert.That(manager.ComboTimeNormalized, Is.GreaterThan(0.9f));
            Assert.That(manager.UltimateChargeNormalized, Is.GreaterThan(0.28f),
                "Ikinci zincir vurusunun Soul Surge odulu bonuslu olmalidir.");
        }

        [UnityTest]
        public IEnumerator PauseMenu_AutoBootstrapsAndRestoresTimeScale()
        {
            yield return null;

            PauseMenuUI pauseMenu = Object.FindAnyObjectByType<PauseMenuUI>();
            Assert.That(pauseMenu, Is.Not.Null, "Oyun sahnesi duraklatma menusunu otomatik kurmalidir.");
            Assert.That(Object.FindAnyObjectByType<GameAudioFeedback>(), Is.Not.Null,
                "Oyun sahnesi ses geri bildirim sistemini otomatik kurmalidir.");
            Assert.That(Object.FindAnyObjectByType<CheckpointToastUI>(), Is.Not.Null,
                "Oyun sahnesi checkpoint bildirimini otomatik kurmalidir.");

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
        public IEnumerator PlayerDeath_FreezesBodyAndOffersRecoveryChoices()
        {
            GameObject player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            Health health = player.GetComponent<Health>();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            PlayerController controller = player.GetComponent<PlayerController>();
            Assert.That(health, Is.Not.Null);

            health.Kill();
            yield return new WaitForSecondsRealtime(1.05f);

            DeathScreenUI deathScreen = Object.FindAnyObjectByType<DeathScreenUI>();
            Assert.That(deathScreen, Is.Not.Null);
            Assert.That(deathScreen.IsOpen, Is.True);
            Assert.That(body.simulated, Is.False, "Olu beden fizik nedeniyle dusmeye devam etmemelidir.");
            Assert.That(controller.enabled, Is.False);
            Assert.That(TimeScaleController.IsPaused, Is.True);
            Assert.That(GameObject.Find("RetryCheckpointButton"), Is.Not.Null);
            Assert.That(GameObject.Find("DeathNewGameButton"), Is.Not.Null);
            Assert.That(GameObject.Find("DeathMainMenuButton"), Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject?.name,
                Is.EqualTo("RetryCheckpointButton"));

            deathScreen.StartNewGame();
            Assert.That(deathScreen.IsConfirmationOpen, Is.True,
                "Kaydi silecek yeni oyun eylemi onay istemelidir.");
            Assert.That(EventSystem.current.currentSelectedGameObject?.name,
                Is.EqualTo("CancelDeathNewGameButton"));
            deathScreen.CancelNewGame();

            deathScreen.ReturnToMainMenu();
            yield return null;
            Assert.That(TimeScaleController.IsPaused, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }

        [UnityTest]
        public IEnumerator ManualSoulReturn_AlwaysKeepsBodyAtOriginalPosition()
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

            Vector2 originalPosition = body.transform.position;
            separate.Invoke(manager, null);
            yield return null;

            GameObject soul = GameObject.Find("Soul");
            Vector2 destination = new Vector2(2f, 4f);
            soul.transform.position = destination;
            Physics2D.SyncTransforms();

            returnToBody.Invoke(manager, new object[] { false, true });
            yield return new WaitForFixedUpdate();

            Assert.That(Mathf.Abs(body.transform.position.x - originalPosition.x), Is.LessThan(0.05f));
            Assert.That(Vector2.Distance(body.transform.position, destination), Is.GreaterThan(1f));
            Assert.That(manager.IsSoulActive, Is.False);
        }

        [UnityTest]
        public IEnumerator SoulForm_DoesNotShowMaterializationPreview()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            Assert.That(manager, Is.Not.Null);
            MethodInfo separate = typeof(SoulSwitchManager).GetMethod(
                "SeparateSoul", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(separate, Is.Not.Null);
            separate.Invoke(manager, null);
            yield return null;

            SoulMaterializationPreview preview = manager.GetComponent<SoulMaterializationPreview>();
            Assert.That(preview == null || !preview.IsVisible, Is.True,
                "Ruh formunda mavi bedenlesme ovali gosterilmemelidir.");
        }

        [UnityTest]
        public IEnumerator MaterializationPreference_CannotBeEnabled()
        {
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            GameObject body = GameObject.Find("Player");
            Assert.That(manager, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            GameplaySettings.MaterializeAtSoulPosition = true;
            Assert.That(GameplaySettings.MaterializeAtSoulPosition, Is.False);
            yield return null;
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

            Assert.That(Mathf.Abs(body.transform.position.x - originalPosition.x), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator Settings_DoNotOfferHitStopOrSoulTeleportOptions()
        {
            PauseMenuUI pauseMenu = Object.FindAnyObjectByType<PauseMenuUI>();
            SoulSwitchManager manager = Object.FindAnyObjectByType<SoulSwitchManager>();
            GameObject body = GameObject.Find("Player");
            Assert.That(pauseMenu, Is.Not.Null);
            Assert.That(manager, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            pauseMenu.Open();
            pauseMenu.OpenSettings();
            SettingsPanelUI settingsPanel = Object.FindAnyObjectByType<SettingsPanelUI>();
            Assert.That(settingsPanel, Is.Not.Null);
            Assert.That(settingsPanel.IsOpen, Is.True);
            Assert.That(GameObject.Find("MaterializeAtSoulToggle"), Is.Null);
            Assert.That(GameObject.Find("HitStopToggle"), Is.Null);
            settingsPanel.Close();
            pauseMenu.Close();
            Assert.That(GameplaySettings.MaterializeAtSoulPosition, Is.False);
            Assert.That(GameplaySettings.HitStopEnabled, Is.False);

            MethodInfo separate = typeof(SoulSwitchManager).GetMethod(
                "SeparateSoul", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo returnToBody = typeof(SoulSwitchManager).GetMethod(
                "ReturnToBody", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector2 originalPosition = body.transform.position;
            separate.Invoke(manager, null);
            yield return null;

            Vector2 soulPosition = originalPosition + new Vector2(6f, 3f);
            GameObject.Find("Soul").transform.position = soulPosition;
            returnToBody.Invoke(manager, new object[] { false, true });
            yield return null;

            Assert.That(Mathf.Abs(body.transform.position.x - originalPosition.x), Is.LessThan(0.05f));
            Assert.That(Vector2.Distance(body.transform.position, soulPosition),
                Is.GreaterThan(4f), "Ayar kapaliyken beden ruhun yanina isinlanmamalidir.");
        }

        [UnityTest]
        public IEnumerator SoulReturn_IgnoresBlockedSoulPositionAndKeepsBodySafe()
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
            Vector2 originalPosition = body.transform.position;
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

            Assert.That(Mathf.Abs(body.transform.position.x - originalPosition.x), Is.LessThan(0.05f));
            Assert.That(Vector2.Distance(body.transform.position, blockedDestination), Is.GreaterThan(1f));
            Assert.That(overlaps.Any(hit => hit != null && !hit.isTrigger), Is.False,
                "Beden birakildigi guvenli konumda kalmalidir.");
        }

        [UnityTest]
        public IEnumerator EnemyAttack_ShowsWarningAndIsNotCancelledByPlayerHit()
        {
            EnemyBase enemy = GameObject.Find("PhysicalEnemy_Guardian")?.GetComponent<EnemyBase>();
            Assert.That(enemy, Is.Not.Null);
            Health health = enemy.GetComponent<Health>();
            health.ResetHealth();

            MethodInfo beginAttack = typeof(EnemyBase).GetMethod(
                "BeginAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(beginAttack, Is.Not.Null);
            beginAttack.Invoke(enemy, null);

            Assert.That(enemy.State, Is.EqualTo(EnemyState.Attack));
            float waitUntilWarning = Mathf.Max(0f, enemy.AttackWindupDuration - 0.5f) + 0.02f;
            if (waitUntilWarning > 0f) yield return new WaitForSeconds(waitUntilWarning);
            Assert.That(enemy.IsAttackWarningVisible, Is.True);

            DamageType type = health.VulnerableTo == DamageRealm.Spiritual
                ? DamageType.Spiritual
                : DamageType.Physical;
            health.TryTakeDamage(1, type, Vector2.right);
            yield return null;

            Assert.That(enemy.State, Is.EqualTo(EnemyState.Attack),
                "Oyuncu vurusu dusmanin hazirladigi saldiriyi iptal etmemelidir.");
            Assert.That(enemy.IsAttackWarningVisible, Is.True);

            yield return new WaitForSeconds(0.52f);
            Assert.That(enemy.IsAttackWarningVisible, Is.False,
                "Unlem hasar karesinde kaybolmalidir.");
        }

        [UnityTest]
        public IEnumerator MainKillZone_CoversStartingAreaAndMostOfLevel()
        {
            GameObject killZone = GameObject.Find("KillZone_Chasm");
            Assert.That(killZone, Is.Not.Null);
            BoxCollider2D box = killZone.GetComponent<BoxCollider2D>();
            Assert.That(box, Is.Not.Null);
            Assert.That(box.bounds.min.x, Is.LessThan(-10f));
            Assert.That(box.bounds.max.x, Is.GreaterThan(200f));
            Assert.That(box.bounds.max.y, Is.LessThan(-5f));
            yield return null;
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
