# Animasyon & Combat Cilası — Çalışma Notu

Bu dosya, oyuncu ile birlikte kararlaştırılan "sade" format: tek dosyada
kısa bir yön (bible) + değişiklik günlüğü (changelog) + test notları.
Ayrı ayrı 7 belge yerine burası tutulur.

## Yön (kısa)

> **GÜNCELLEME 2026-08-25:** Aşağıdaki ilk madde artık GEÇERSİZ. Proje
> sprite-sheet + Animator Controller mimarisine geçti (bkz. turlar 4, 8, 9).
> Eski hali kayıt için bırakıldı: *"prosedürel animasyon kullanıyor —
> Animator Controller / Blend Tree / iskelet yok. Bu değişmeyecek."*

- **Animasyon mimarisi (güncel):** Oyuncu (beden + ruh), fiziksel muhafızlar
  ve hayalet düşmanlar **CraftPix sprite-sheet + AnimatorController**
  kullanıyor. Klipler ve controller'lar
  [SpriteAnimationBuilder.cs](Assets/_SoulSplit/Editor/SpriteAnimationBuilder.cs)
  ile üretiliyor. Prosedürel animatörler (`PlayerProceduralAnimator`,
  `EnemyProceduralAnimator`) **silinmedi, devre dışı** — geri dönülebilir.
- **Animator sadece `m_Sprite`'ı sürer.** Transform'a dokunmaz. Bu yüzden
  transform tabanlı efektler (süzülme drift'i, eğilme squash'ı) Animator ile
  çakışmadan üst üste binebiliyor. Yeni görsel katman eklerken bu ayrım
  korunmalı.
- Vuruş zamanlaması kuralı: **hasar, görsel "impact frame"de inmeli**,
  tuşa basış anında değil. Ek nüans (tur 12'de öğrenildi): oyuncunun kendi
  saldırısında hasarın 1 kare ÖNDE olması tepkiselliği artırır; **düşman
  saldırısında ise görsel hasardan ÖNCE gelmeli**, yoksa haksız hissettirir.
- **Test yöntemi uyarısı:** `EditorApplication.Step()` ile kare-kare
  ilerlerken `WaitForSecondsRealtime` çözülmez (gerçek saat ilerlemez).
  `HitStop` ve `HitFlash` bunu kullandığı için ölçümlerde "donmuş /
  tetiklenmiyor / renk kaybolmuş" gibi **yanlış alarmlar** üretir. Renk veya
  zamanlama ölçerken `HitStopHost` geçici kapatılmalı. Bu tuzağa 3 kez düşüldü.
- **Assembly adı:** Script'ler `Assembly-CSharp` değil **`SoulSplit.Runtime`**
  içinde derleniyor (asmdef var). Reflection'da bu ad kullanılmalı.
- Ses katmanı şu an yok (proje genelinde hiç `AudioClip` yok) — bu bilinen
  bir boşluk, ayrı bir kararla ele alınacak (lisanslı kaynak gerekiyor).

## Değişiklik Günlüğü

### 2026-08-24 — Oyuncu saldırı hitbox/animasyon zaman uyuşmazlığı düzeltildi

**Sorun (Gözlemlendi):** [MeleeAttack.cs](Assets/_SoulSplit/Scripts/Combat/MeleeAttack.cs)
hasarı `AttackPressedThisFrame` anında uyguluyordu, ama görsel hazırlık+vuruş
animasyonu (`PlayerProceduralAnimator`/`SoulController`'daki
`attackAnticipationDuration + attackStrikeDuration` ≈ 0.125s) aynı anda
başlıyordu — yani hasar, kılıç henüz havadayken iniyordu.

**Değişiklik (Implemented):** `MeleeAttack.cs`'e `impactDelay` (varsayılan
0.125s, `[SerializeField]`) ve `_pendingImpactTimer` eklendi. `OnAttackTriggered`
hâlâ tuşa basılır basılmaz ateşleniyor (animasyon hazırlığı gecikmeden
başlasın diye), ama gerçek hasar uygulaması (`PerformAttack()`) artık
`impactDelay` süresi dolana kadar erteleniyor.

- Dosya: [Assets/_SoulSplit/Scripts/Combat/MeleeAttack.cs](Assets/_SoulSplit/Scripts/Combat/MeleeAttack.cs)
- Geri alma: `impactDelay` alanını 0 yapmak eski (anlık) davranışa döner;
  tam geri almak için commit'i revert etmek yeterli.
- Kapsam: Hem beden hem ruh formu aynı `MeleeAttack` script'ini paylaştığı
  için düzeltme otomatik olarak ikisine de uygulandı.

**Test (Tested):** Play Mode'da, oyuncuyu bir düşmanın menziline yerleştirip
`_pendingImpactTimer`'ı gerçek koddaki değerle (0.125s) tetikledim:
- Tetiklendiği an: can `3/3` (hasar inmedi ✅)
- Gerçek `Update()` döngüsü birkaç frame ilerledikten sonra: can `2/3`,
  `_pendingImpactTimer=-1` (hasar tam zamanında indi ✅)
- Konsolda hata yok.

**Bilinen sınır:** Gerçek klavye/fare girişiyle uçtan uca (buton
basışından ekrandaki vuruş anına kadar) henüz elle oynanarak
doğrulanmadı — yukarıdaki test kodun mantığını gerçek Unity döngüsünde
doğruluyor, ama görsel "hissi" bir insan tarafından teyit edilmedi.

---

### 2026-08-24 — Şiddet kademeli vuruş tepkisi

**Sorun (Gözlemlendi):** Hasar miktarından bağımsız olarak her "Damaged"
vuruşu aynı knockback, aynı ezilme (squash), aynı hit-stop/kamera sarsıntısı
ve aynı sersemleme süresini oynatıyordu — hafif ve ağır vuruş arasında
hiç görsel/hissi fark yoktu.

**Değişiklik (Implemented):**
- `Health.cs`: `OnHit` event'ine uygulanan hasar miktarını taşıyan bir
  `int amount` parametresi eklendi (`Action<HitResult, DamageType, Vector2, int>`).
  Çevresel ölümde (`Kill()`) `amount = maxHealth` gönderiliyor (en ağır
  kabul edilsin diye).
- `PlayerHitReaction.cs`: `heavyDamageThreshold` (varsayılan 2) eklendi;
  eşiği geçen hasarlarda ayrı `heavyKnockback*`/`heavyHitSquashAmount`
  alanları kullanılıyor (varsayılan: hafif 6/3 hız, ağır 10/5 hız;
  hafif ezilme 0.32, ağır 0.5).
- `EnemyBase.cs`: aynı eşik deseni; ağır vuruşta `knockbackForce` ve
  `hurtDuration`, `heavyKnockbackMultiplier`/`heavyHurtDurationMultiplier`
  ile çarpılıyor (varsayılan sırasıyla ×1.6 / ×1.5).
- `HitFlash.cs`: ağır vuruşta ayrı `heavyHitStopDuration`/`heavyShakeMagnitude`/
  `heavyShakeDuration` kullanılıyor (varsayılan hafif 0.035s/0.06/0.14s,
  ağır 0.06s/0.1/0.18s) — "Damaged" ve "Killed" arasında artık üçüncü
  bir ara kademe var.
- Zincirleme güncelleme: `WorldHealthBar.cs`, `PlayerHealthUI.cs` yeni
  event imzasına uyacak şekilde güncellendi (davranışları değişmedi).

- Dosyalar: [Health.cs](Assets/_SoulSplit/Scripts/Combat/Health.cs),
  [PlayerHitReaction.cs](Assets/_SoulSplit/Scripts/Player/PlayerHitReaction.cs),
  [EnemyBase.cs](Assets/_SoulSplit/Scripts/Enemies/EnemyBase.cs),
  [HitFlash.cs](Assets/_SoulSplit/Scripts/Combat/HitFlash.cs),
  [WorldHealthBar.cs](Assets/_SoulSplit/Scripts/UI/WorldHealthBar.cs),
  [PlayerHealthUI.cs](Assets/_SoulSplit/Scripts/UI/PlayerHealthUI.cs)
- Geri alma: her `heavy*` alanını kendi hafif karşılığıyla aynı değere
  çekmek kademeyi etkisiz kılar; tam geri almak için commit revert.
- Bilinen sınır: Şu an oyundaki tüm saldırılar `damage=1` gönderiyor
  (MeleeAttack, EnemyBase), yani "ağır" kademe fiilen henüz hiçbir
  saldırı tarafından tetiklenmiyor — bir "ağır saldırı" eklendiğinde
  (damage≥2) otomatik devreye girecek şekilde hazır bekliyor.

**Test (Tested):** Play Mode'da, `Health.TryTakeDamage`'i gerçek
production kodu üzerinden hem `amount=1` (hafif) hem `amount=2-3` (ağır,
öldürmeyecek şekilde tam candan başlatılarak) ile tetikledim:

| | Hafif | Ağır |
|---|---|---|
| Oyuncu knockback | (-6.00, 3.00) | (-10.00, 5.00) ✅ |
| Oyuncu hit-squash | 0.32 | 0.5 ✅ |
| Kamera sarsıntı (mag/süre) | 0.06 / 0.14 | 0.1 / 0.18 ✅ |
| Düşman knockback | -3.96 | -6.33 ✅ |
| Düşman sersemleme süresi | 0.25 | 0.375 ✅ |

Tüm değerler `[SerializeField]` config'leriyle birebir eşleşti. Konsolda
hata yok. (Not: ilk test turunda derleme+play-mode geçişi çakışınca
`HitStop` singleton'ı geçici olarak `null` kaldı — düzeltme değil, test
ortamı artefaktıydı; temiz bir play-mode oturumunda tekrarlanınca
sorun kalmadı.)

**Bilinen sınır:** Gerçek bir "ağır saldırı" (damage≥2 üreten bir saldırı
türü) henüz oyunda yok, bu yüzden bu kademe gerçek oynanışta henüz hiç
tetiklenmiyor — sadece altyapı hazır. Ghost düşmanların knockback'i hâlâ
sadece X ekseninde (Y bileşeni yok); bu ayrı, henüz işlenmemiş bir madde.

---

### 2026-08-24 — Ağır saldırı türü eklendi (oyuncu)

**Sorun (Gözlemlendi):** Şiddet kademeli vuruş tepkisi sistemi hazırdı
ama oyunda `damage≥2` üreten hiçbir saldırı yoktu — yani sistem hiç
fiilen tetiklenmiyordu.

**Değişiklik (Implemented):**
- Yeni input action: `HeavyAttack` (Klavye **K**, Fare orta tuş, Gamepad
  buttonEast). [SoulSplitControls.inputactions](Assets/_SoulSplit/Input/SoulSplitControls.inputactions),
  [PlayerInputHandler.cs](Assets/_SoulSplit/Scripts/Player/PlayerInputHandler.cs).
- `MeleeAttack.cs`: yeni `AttackTier` enum'u (Light/Heavy). Ağır saldırı
  için ayrı alanlar eklendi: `heavyDamage=2` (**bilerek** önceki
  oturumdaki `heavyDamageThreshold=2` ile aynı — ağır saldırı gerçekten
  ağır vuruş tepkisini tetiklesin diye), `heavyHitboxSize` (1.7×1.5,
  hafiften büyük), `heavyCooldown=0.75s` (hafiften ~2x uzun — büyük
  vuruşun bedeli budur), `heavyImpactDelay=0.21s`, ayrı `heavyImpactColor`.
  Aynı anda iki tuşa basılırsa ağır öncelikli.
- `PlayerProceduralAnimator.cs` ve `SoulController.cs`: ağır saldırı için
  ayrı bir zamanlama seti (hazırlık 0.16s / vuruş 0.05s / tutma 0.14s /
  toparlanma 0.28s, açılar ve lunge mesafesi de büyütüldü) — hafifin
  hız-ölçeklenmiş kopyası değil, kendi zamanlama eğrisi var (yavaş
  hazırlık + hâlâ keskin vuruş + uzun toparlanma kontrastı korundu).
  `OnAttackTriggered` artık `AttackTier` taşıyor; her iki animatör de
  tetiklenen türe göre doğru zamanlama setini kullanıyor.
- Beden ve ruh formu aynı `PlayerInputHandler`'ı paylaştığı için ağır
  saldırı otomatik olarak ikisinde de çalışıyor.

- Dosyalar: [MeleeAttack.cs](Assets/_SoulSplit/Scripts/Combat/MeleeAttack.cs),
  [PlayerInputHandler.cs](Assets/_SoulSplit/Scripts/Player/PlayerInputHandler.cs),
  [PlayerProceduralAnimator.cs](Assets/_SoulSplit/Scripts/Player/PlayerProceduralAnimator.cs),
  [SoulController.cs](Assets/_SoulSplit/Scripts/Player/SoulController.cs),
  [SoulSplitControls.inputactions](Assets/_SoulSplit/Input/SoulSplitControls.inputactions)
- Geri alma: `heavyDamage`'ı 1 yapmak şiddet kademesini fiilen devre dışı
  bırakır (saldırı hâlâ var ama artık "hafif" sayılır); tam geri almak
  için commit revert.
- Kapsam: Sadece oyuncu. Düşmanlara ağır saldırı eklenmedi (ayrı bir
  karar gerektirir — bkz. aşağıdaki adaylar).

**Test (Tested):** Play Mode'da, gerçek `Update()`/event zinciri
üzerinden (girdiyi taklit etmek yerine `BeginAttack`'i tetikleyen aynı
kod yolunu kullanarak):
- Ağır saldırı tetiklendiğinde: `PlayerProceduralAnimator._attackTier=Heavy`,
  `_attackTimer=0.63` (=`heavyAttackTotalDuration`, tam eşleşme) ✅,
  hasar henüz inmedi (doğru — gecikmeli) ✅
- Gecikme dolduktan sonra: düşman canı `3→1` (`heavyDamage=2` ile tam
  eşleşme) ✅
- Hafif saldırı (regresyon kontrolü): düşman canı `3→2` (`damage=1`,
  değişmedi) ✅
- Konsolda hata yok; `PlayerInputHandler` yeni `HeavyAttack` action'ını
  hatasız buldu (aksi halde `throwIfNotFound` patlardı).

**Bilinen sınır:** Gerçek klavye/fare ile uçtan uca (K tuşuna basıp
ekranda büyük vuruşu görmek) henüz insan tarafından oynanarak
doğrulanmadı. Ağır vuruşun knockback büyüklüğü bu turda gözlemlenemedi
(çok fazla frame geçtiği için düşman AI'si normale dönüp hızı ezdi) —
ama aynı knockback formülü bir önceki oturumda ayrıca, izole şekilde
doğrulanmıştı.

---

### 2026-08-25 — Sahne aydınlatması, post-processing ve saldırı-zamanlama kod tekrarı

**Sorun (Gözlemlendi):** Kullanıcı "grafikler çok kötü, oyun akıcı değil,
animasyonlar aşırı kötü" diye bildirdi. İnceleme kök nedeni ortaya
çıkardı: `Assets/Scenes/SampleScene.unity`'deki TÜM SpriteRenderer'lar
URP'nin 2D-lit materyalini (`Sprite-Lit-Default`) kullanıyordu ama
sahnede tek bir `Light2D` yoktu — yani karakter/düşman/zemin sanatı
(kalitesi aslında iyi) neredeyse simsiyah render ediliyordu. Ayrıca tek
post-processing volume'ü (`SoulFormProfile.asset`) hem tamamen boştu
(bloom/vignette/color grade yok) hem de `weight=0` idi (override eklense
bile hiç etkili olmayacaktı). Animasyon/hit-feel kodu incelendiğinde
zaten sağlam olduğu görüldü (squash-stretch, hit-stop, kamera sarsıntısı,
impact-frame zamanlaması) — asıl sorun görünürlüktü.

**Değişiklik (Implemented):**
- Sahneye `SceneLighting` altında 1 global `Light2D` (soğuk gri-mavi,
  intensity 0.4) + 4 nokta `Light2D` aksan ışığı (soğuk mavi, "soul"
  temalı, intensity 2, arka plan karolarının merkezlerine hizalı)
  eklendi. Hepsi `Default` sorting layer'ı hedefliyor (projede tek
  sorting layer bu).
- `SoulFormProfile.asset`'e Bloom (threshold 0.95, intensity 0.45,
  soğuk mavi tint), Vignette (yakın-siyah, intensity 0.3) ve Color
  Adjustments (contrast +8, saturation -8, post-exposure +0.1) override'ları
  eklendi; `SoulFormVolume.weight` 0'dan 1'e çekildi (aksi halde hiçbir
  override etkili olmuyordu — bu sessizce başarısız olan bir hataydı).
- Main Camera'nın `UniversalAdditionalCameraData.antialiasing`'i
  None'dan FXAA'ya çekildi — kamera look-ahead/shake hareketinde sprite
  kenar titremesini azaltır.
- `PlayerProceduralAnimator.cs` ve `SoulController.cs`'in birebir
  kopyaladığı 4 fazlı (hazırlık/vuruş/tutma/toparlanma) saldırı-overlay
  eğri matematiği yeni bir dosyaya (`AttackOverlayAnimator.cs`, `struct
  AttackOverlayTimings` + `static class AttackOverlayAnimator`) taşındı.
  Her iki bileşen de kendi [SerializeField] hafif/ağır süre-açı-mesafe
  değerlerini korudu (body/soul formu hâlâ farklı hissetmeli) — sadece
  hesaplama tek yerde.

- Dosyalar: [Assets/Scenes/SampleScene.unity](Assets/Scenes/SampleScene.unity),
  [Assets/_SoulSplit/Art/Profiles/SoulFormProfile.asset](Assets/_SoulSplit/Art/Profiles/SoulFormProfile.asset),
  Main Camera (`UniversalAdditionalCameraData`),
  [AttackOverlayAnimator.cs](Assets/_SoulSplit/Scripts/Player/AttackOverlayAnimator.cs) (yeni),
  [PlayerProceduralAnimator.cs](Assets/_SoulSplit/Scripts/Player/PlayerProceduralAnimator.cs),
  [SoulController.cs](Assets/_SoulSplit/Scripts/Player/SoulController.cs)
- Geri alma: `Lighting_Global`'ı `intensity=0` yapmak veya `SceneLighting`
  objesini pasifleştirmek eski (karanlık) görünüme döner; volume
  `weight=0` yapmak post-processing'i etkisiz kılar. Kod tarafı: tam
  geri almak için commit revert (davranış değişmedi, sadece dosya
  organizasyonu).
- Kapsam: Sprite-sheet/Animator Controller'a geçilmedi (proje kuralı
  gereği), ses eklenmedi (bilinen, ayrı kararla ele alınacak boşluk),
  DoF/kromatik aberasyon/film grain eklenmedi (bilinçli olarak — 2D
  aksiyon oyununda okunabilirliği bozar).

**Test (Tested):** Play Mode'da:
- Işıklandırma öncesi/sonrası Game View ekran görüntüsü karşılaştırıldı —
  öncesinde karakter/platformlar ayırt edilemiyordu, sonrasında net
  görünür ve atmosferik.
- Konsolda hata/uyarı yok (`read_console`, hem düzenleme hem play mode
  sırasında kontrol edildi).
- Kod tekrarı temizliği: hem `PlayerProceduralAnimator` hem `SoulController`
  üzerinde reflection ile hafif ve ağır saldırı tetiklendi (`BeginAttack`/
  `HandleAttackTriggered`), `_attackTimer`/`_attackTier` beklenen
  `TotalDuration` değerleriyle (hafif 0.365s, ağır 0.630s — refactor
  ÖNCESİ elle hesaplanan degerlerle birebir ayni) eslesti; regresyon yok.

**Bilinen sınır:** Aksan ışıklarının pozisyonları arka plan sanatındaki
ışıltı noktalarına Scene View'da gözle hizalandı, piksel-hassas değil —
gerekirse ince ayar sonradan yapılabilir. Kullanıcının "grafikler/
animasyonlar artık istediği düzeyde mi" sorusu öznel bir görsel karar;
otomatik olarak "tamam" sayılmadı, kullanıcı onayı bekleniyor.

---

### 2026-08-25 (devam) — Oyuncu ışığı, eğilme görseli, parkur geçişleri

**Sorun (Gözlemlendi):** Kullanıcı üç şey istedi: (1) karakter ilerlerken
üzerinde hafif aydınlık olsun, (2) eğilme tuşu eklensin — bazı yerler
geçilmiyor, (3) hem fiziksel hem ruh formunda birer düşman olsun, zemin
tek düzde olmasın. İnceleme ortaya çıkardı ki (2) ve (3) zaten büyük
ölçüde koddaydı: `PlayerController.HandleCrouch()` S/Down ile eğilmeyi
zaten uyguluyordu (sadece görsel geri bildirimi yoktu, o yüzden "yok"
sanılmış olabilir) ve sahnede zaten `PhysicalEnemy_Guardian` (x=102),
`PhysicalEnemy_Guardian_Final` (x=148), `GhostEnemy_Serpent` (x=120),
`GhostEnemy_MaskSwarm` (x=138) mevcuttu — ama hepsi başlangıç
noktasından (x=0) çok uzakta, ve başlangıç bölgesindeki platform
sıçramaları (Platform_A→B→C, sonra duvar sekmesi bacası) muhtemelen
oyuncuyu daha o bölgede tıkıyordu; yani "düşman yok" hissi aslında
"oraya kadar ulaşamıyorum" hissiydi.

**Değişiklik (Implemented):**
- Player'ın `Visual` çocuğuna ve `Soul` objesine birer takip eden
  `Light2D` (Point, sırasıyla sıcak-nötr ve soğuk-mavi ton, intensity
  ~1.1-1.3, outer radius 4.5) eklendi — karakter artık aksan ışıklarından
  uzakken de görünür.
- `PlayerProceduralAnimator.cs`'e `crouchSquashAmount`/`crouchTransitionSpeed`
  eklendi; `controller.IsCrouching` okunup mevcut squash/stretch
  katmanına (`_crouchAmount`) ekleniyor — artık eğilme GÖRÜNÜR (collider
  zaten küçülüyordu, ama sprite aynı kalıyordu).
- Başlangıç bölgesine `Ceiling_CrouchTunnel` (x=6-9, tavan y≈1.85)
  eklendi — sadece eğilerek geçilebilen, ayakta durarak geçilemeyen bir
  düşük tünel; `Physics2D.OverlapBox` ile ayakta/eğik collider
  boyutlarıyla test edilip doğrulandı (ayakta blocked=true, eğik
  blocked=false).
- Platform_A→B ve B→C arasına birer ara basamak (`Platform_Step_AB`,
  `Platform_Step_BC`) eklendi — 3 birimlik dikey sıçramaları ikiye
  bölüyor, tek zıplamayla teorik sınıra (~2.95 birim) dayanan riskli
  sıçramaları rahatlatıyor.
- Duvar sekmesi bacasına (Wall_Left/Wall_Right arası, y 7.5→15.5) orta
  yükseklikte bir dinlenme çıkıntısı (`Platform_ChimneyRest`, y=11.3)
  eklendi — 8 birimlik kesintisiz duvar-sekmesi zincirini ikiye bölüyor.
- Başlangıç zeminine küçük bir basamak (`Ground_Step1`, x=1.3-3.7,
  yükseklik 1) eklendi — zemin artık başlangıçta bile tek düz çizgi
  değil.

- Dosyalar: [PlayerProceduralAnimator.cs](Assets/_SoulSplit/Scripts/Player/PlayerProceduralAnimator.cs),
  [Assets/Scenes/SampleScene.unity](Assets/Scenes/SampleScene.unity)
  (yeni GameObject'ler: `Lighting_PlayerGlow`, `Lighting_SoulGlow`,
  `Ceiling_CrouchTunnel`, `Platform_Step_AB`, `Platform_Step_BC`,
  `Platform_ChimneyRest`, `Ground_Step1`)
- Geri alma: yeni GameObject'leri sahneden silmek veya pasifleştirmek
  yeterli; `crouchSquashAmount`'ı 0 yapmak eğilme görselini kapatır
  (collider davranışı etkilenmez).
- Kapsam: Mevcut `PhysicalEnemy_Guardian*`/`GhostEnemy_*` düşmanları
  TAŞINMADI/kopyalanmadı — "her iki formda da düşman olsun" isteği zaten
  sahnede karşılanıyordu, asıl eksik oraya ulaşılamamasıydı.

**Test (Tested):** Play Mode'da, `Physics2D.OverlapBox` ile eğilme
tünelinin ayakta collider boyutuyla (0.9×1.75) bloklandığı, eğik
collider boyutuyla (0.9×0.94) bloklanmadığı doğrulandı. Sahne ekran
görüntüleriyle oyuncu ışığının hareket boyunca karakteri aydınlattığı,
yeni basamak/dinlenme platformlarının doğru konumlarda render edildiği
teyit edildi. Konsolda yeni hata/uyarı yok.

**Bilinen sınır:** Yeni platform/basamakların tam "hissi" (jump timing,
zamanlama) klavye ile uçtan uca insan tarafından oynanarak
doğrulanmadı — sadece geometri/collider matematiği ve statik
OverlapBox testleriyle doğrulandı. Kullanıcının gerçek oynanışta hâlâ
zorlandığı bir nokta olursa (özellikle duvar sekmesi bacası, beceri
gerektiren bir mekanik olmaya devam ediyor) ayrı bir turda ele alınmalı.

---

### 2026-08-25 (3) — Piksel yoğunluğu tutarsızlığı + sprite-sheet altyapısı

**YÖN DEĞİŞİKLİĞİ:** Kullanıcı, prosedürel animasyonun yetersiz olduğuna
karar verdi ("animasyonlar rezalet") ve gerçek kare animasyonuna geçilmesini
istedi. Bu, dosyanın başındaki "Animator Controller / Blend Tree yok, bu
değişmeyecek" kuralını **bilinçli olarak geçersiz kılar**. Prosedürel
sistem henüz kaldırılmadı — sprite sheet gelene kadar duruyor.

**Sorun 1 (Gözlemlendi):** Kullanıcı "çoğu yerde tutarsızlık var" dedi.
Ölçüm, kaynağı net gösterdi: sahnedeki sprite'ların **ekrandaki piksel
yoğunluğu** 5 kat aralıkta değişiyordu (kamera ortho 8 → 67.5 ekran-px/birim):

| Varlık | Eski PPU | Ekran ölçeği |
|---|---|---|
| Player | 100 | 0.68x |
| Düşmanlar | 200 | **0.34x** |
| Tile_StoneEdge | 241.7 | **0.28x** |
| Background_Ruins | 48 | **1.41x** |

Yani düşmanın "sanat pikseli" oyuncunun yarısı kadardı — aynı sahnede iki
farklı piksel dili. Üstüne filtre de karışıktı: **Player `Point` (sert/
testere kenar), diğer her şey `Bilinear`**.

**ÖNCEKİ RAPORDAKİ HATA:** 2026-08-25 tarihli denetim raporunda bu PPU
farkları "kasıtlı, doğru" diye geçilmişti. Yanlıştı — orada yapılan test
"dünya boyutu tutarlı mı" idi, ama doğru test **ekrandaki piksel
yoğunluğu**. Bu tur düzeltildi.

**Değişiklik (Implemented):**
- 4 kaynak PNG, alan-ortalamalı (box filter, alfa-ağırlıklı premultiplied
  toplama — şeffaf kenarlarda koyu hale oluşmasın diye) küçültmeyle yeniden
  örneklendi; PPU'lar 100'e çekildi. **Dünya boyutları birebir korundu**,
  yani çarpışma/yerleşim/oynanış hiç etkilenmedi:
  - `Enemy_StoneGuardian` 270x440 → 135x220 (dünya 1.350x2.200, aynı)
  - `Ghost_Serpent` 511x220 → 256x110 (dünya 2.560x1.100)
  - `Ghost_MaskSwarm` 295x300 → 148x150 (dünya 1.480x1.500)
  - `Tile_StoneEdge` 2132x290 → 882x120 (dünya 8.820x1.200)
- `Player_Physical` ve `Player_Soul` filtresi `Point` → `Bilinear`.
  Gerekçe: hiçbir şey tam sayı ölçekte render edilmiyor (~0.68x küçültme);
  Point bu durumda harekette titreme üretir. Pixel Perfect Camera eklemek
  alternatifti ama o da `orthographicSize`'i ele geçirip
  `CameraFollow.PunchZoom`'u bozardı — bilerek seçilmedi.
- `Background_Ruins` **kasıtlı olarak 48 PPU'da bırakıldı**: karakterlere
  göre ~2:1 oranı zaten atmosferik derinlik için meşru bir teknik ve PPU'yu
  50'ye çekmek arka planın üst kenarında kaplama boşluğu riski doğuruyordu
  (mevcut kaplama y=24.13'te bitiyor, kamera y=24'e kadar görüyor — çok dar
  bir pay; bu ayrıca bilinen bir kırılganlık).
- **Yeni araç:** [SpriteAnimationBuilder.cs](Assets/_SoulSplit/Editor/SpriteAnimationBuilder.cs)
  — dilimlenmiş bir sprite klasöründen `<Durum>_<sayı>` isimlendirmesine göre
  otomatik `AnimationClip`'ler + tam kurulu `AnimatorController` üretir
  (Speed/IsGrounded/Attack/Hurt/Death parametreleri, doğru exit-time
  kuralları ile).

**Yedek:** Orijinal (küçültülmemiş) PNG'ler `ArtOriginals/` klasöründe —
Assets dışında, Unity'ye ikinci kopya olarak import olmasın diye.

**Test (Tested):**
- Küçültme sonrası 8 varlığın tamamı ölçüldü: arka plan hariç hepsi
  100 PPU / 0.68x / Bilinear'da birleşti; dünya boyutları hedeflenen
  değerlerle birebir eşleşti.
- Play Mode'da oyuncu taş muhafızın yanına taşınıp yakın plan ekran
  görüntüsü alındı — küçültme detayı bozmadı, ikisi artık aynı piksel
  yoğunluğunda okunuyor.
- `SpriteAnimationBuilder` sahte 13 karelik bir set üzerinde gerçek kod
  yolundan çalıştırıldı ve çıktısı denetlendi:
  - `Walk_10`, `Walk_2`'den SONRA sıralandı (0.250s) — alfabetik sıralama
    tuzağı doğru şekilde aşıldı ✅
  - Idle/Walk/Run döngülü, Attack/Hurt/Death tek sefer ✅
  - 5 parametre doğru tiplerle, varsayılan durum Idle ✅
  - Locomotion geçişleri `hasExitTime=False` (anında tepki), Attack/Hurt
    çıkışları `hasExitTime=True, exitTime=1.0` (animasyon kesilmiyor) ✅
  - Test varlıkları sonrasında silindi.

**Bilinen sınır:** Sprite sheet henüz projede yok — kullanıcı indirecek.
Builder gerçek bir sheet üzerinde henüz çalıştırılmadı, sadece sentetik
kare setiyle doğrulandı. Ayrıca prosedürel animasyon bileşenleri hâlâ
sahnede duruyor; sprite-sheet'e geçilirken `PlayerProceduralAnimator`
objeden kaldırılmalı, yoksa iki sistem aynı transform'u çekiştirir.

---

### 2026-08-25 (4) — Oyuncu sprite-sheet animasyonuna geçirildi

**Kaynak:** CraftPix "Free Knight Character Sprites" (Knight_1 varyantı).
Lisans dosyası `PlayerSheets/CraftPix_License.txt` olarak varlık klasörüne
kopyalandı. Kullanılmayan animasyonlar (Attack2/3, RunAttack, Defend,
Protect, Jump) `PlayerSheets_Extra/` klasörüne ayrıldı — builder öksüz
state üretmesin diye.

**Ölçüm bulguları (dilimleme öncesi):**
- Tüm kareler 128×128, yatay şerit. Walk 8, Run 7, Attack1 5, Death 6,
  Hurt 2, Idle 4 kare.
- Karakterin GERÇEK içeriği kare içinde sadece **43×64 px**. Yani art,
  mevcut boyalı sanattan (~180px/1.8 birim) belirgin daha düşük
  çözünürlüklü — bu, kullanıcının seçtiği "hazır sprite sheet" yolunun
  kabul edilmiş bedeli.
- **Ayaklar her karede tam y=0'da** (kare tabanı) → dikey pivot güvenli.
- Gövde yatayda **x≈34'te** merkezleniyor, kare merkezinde (64) DEĞİL.
  Düz "Center" pivot kullanılsaydı karakter sola kayar ve her yön
  değişiminde (flipX) yana zıplardı.

**Değişiklik (Implemented):**
- 6 sheet, `ISpriteEditorDataProvider` ile 128px ızgarada dilimlendi;
  sprite'lar `<Durum>_<sıra>` olarak adlandırıldı (builder'ın beklediği
  desen). Toplam 32 sprite.
- **PPU = 64/1.80 = 35.56** → karakter mevcut oyuncuyla aynı 1.80 birim
  boyunda. **Pivot = (34/128, 32/128)** → gövde merkezi.
- Filtre `Point` seçildi: bu gerçek düşük çözünürlüklü pixel art, önceki
  turda boyalı sanata uygulanan `Bilinear` kararı burada geçerli değil.
- `SpriteAnimationBuilder` gerçek veri üzerinde çalıştırıldı →
  `Assets/_SoulSplit/Animation/Player/` altında 6 klip + `PlayerAnimator.controller`.
- **Yeni köprü:** [PlayerAnimatorBridge.cs](Assets/_SoulSplit/Scripts/Player/PlayerAnimatorBridge.cs)
  — `PlayerController` (hız/zemin), `MeleeAttack.OnAttackTriggered` ve
  `Health.OnHit/OnDeath` olaylarını Animator parametrelerine bağlar.
  Hiçbir transform'a dokunmaz.
- `Player/Visual` üzerinde: `PlayerProceduralAnimator` **devre dışı
  bırakıldı** (silinmedi — geri dönülebilir), transform sıfırlandı,
  `Animator` + `PlayerAnimatorBridge` eklendi.
- **Attack klibi yeniden zamanlandı** (kritik): builder'ın ürettiği düzgün
  12fps dağılımında vuruş karesi 0.333s'ye düşüyordu, ama `impactDelay`
  0.125s idi — yani hasar kılıç havadayken inecekti. Bu, projenin daha
  önce özellikle düzelttiği hatanın aynısı. Klip, dosyadaki "yavaş
  hazırlık → ani vuruş → UZUN tutma" ilkesine göre elle zamanlandı:
  `0.000/0.050/0.100/0.1333` hazırlık kareleri, `0.1667` vuruş, kare
  0.350'ye kadar ASILI kalıyor (**167ms impact frame hold**).
- `impactDelay` 0.125 → **0.1667**, `heavyImpactDelay` 0.21 → 0.1667
  (ağır saldırı şu an aynı klibi oynatıyor).

**Test (Tested):** Play Mode'da, gerçek kod yollarından:
- Locomotion zinciri: `Idle` → `Walk` (Speed 0.49) → `Run` (Speed 0.91),
  koşu kareleri düzgün döngüde (Run_1→3→5→0) ✅
- Ayak hizası: oyuncu merkezi y=0.90, ayaklar y=0.00, zemin üstü y=0.00 —
  tam oturuyor, yüzme/batma yok ✅
- Saldırı, `MeleeAttack.BeginAttack` üzerinden tetiklenip kare kare
  izlendi: hasar **kare 9 (0.150s)**, vuruş sprite'ı **kare 10 (0.167s)** —
  1 karelik (16ms) fark, istenen yönde (tepkisellik) ✅
- Düşmana gerçek saldırı: can 3 → 2, HitFlash + parçacık + can barı
  hepsi tetiklendi ✅
- Konsol hatasız.

**Bilinen sınırlar:**
- **Ruh formu ve düşmanlar hâlâ prosedürel animasyonda.** Sadece bedenin
  oyuncusu sprite-sheet'e geçti. Ruh formunda oynarken görsel dil değişiyor.
- Şövalye sanatı, boyalı çevre/düşman sanatından daha düşük çözünürlüklü —
  pikselleri belirgin daha iri. Bu, "hazır paket" seçiminin kaçınılmaz
  sonucu; tek gerçek çözümü tüm karakterleri aynı paketten almak
  (Knight_2/Knight_3 varyantları düşman için kullanılabilir).
- Jump/Fall state'i yok (Jump.png Extra klasöründe bekliyor); havadayken
  Idle/Walk oynuyor. `IsGrounded` parametresi hazır, sadece state+geçiş
  eklenmeli.
- Ağır saldırının kendi klibi yok (Attack2/Attack3 Extra'da hazır bekliyor).
- Attack klibinin elle zamanlaması, `SpriteAnimationBuilder` yeniden
  çalıştırılırsa **kaybolur** (builder düzgün dağılım üretir). Yeniden
  çalıştırılırsa bu retiming tekrar uygulanmalı.

---

### 2026-08-25 (5) — Fiziksel düşmanlar da sprite-sheet'e geçirildi + TAM DENETİM

**Değişiklik (Implemented):**
- CraftPix Knight_2 (mor varyant) → `GuardianSheets/`, 6 animasyon dilimlendi.
- **PPU = 64/2.20 = 29.09** seçildi. Gerekçe: muhafızın `CapsuleCollider2D`'si
  1.05×2.20; oyuncuyla aynı PPU (35.56) kullanılsaydı sprite 1.80 kalıp
  kutudan kısa görünürdü. **Oynanışı değiştirmemek** için collider'a sprite
  uyduruldu, tersi değil. Bedeli: muhafızın pikselleri oyuncununkinden %22
  iri (önceki 2 kat farka göre kabul edilebilir).
- Pivot oyuncuyla aynı: `(34/128, 32/128)`.
- [EnemyAnimatorBridge.cs](Assets/_SoulSplit/Scripts/Enemies/EnemyAnimatorBridge.cs)
  yazıldı; `EnemyBase.OnAttackTriggered` / `Health.OnHit,OnDeath` →
  Animator, ayrıca flipX. `referenceSpeed` her düşmanın kendi
  `chaseSpeed`'ine (3.6) eşitlendi.
- Guardian Attack klibi elle zamanlandı: `EnemyBase` hasarı `attackWindup`
  (0.35s) SONUNDA uyguladığı için vuruş karesi **0.340s**'ye konuldu,
  sonrasında 0.200s asılı kalıyor.
- Her iki `PhysicalEnemy_Guardian*` üzerinde `EnemyProceduralAnimator`
  devre dışı bırakıldı, `Animator` + `EnemyAnimatorBridge` eklendi.

**Test (Tested):** Play Mode'da gerçek AI/kod yollarından:
- Muhafız zinciri: `Chase→Walk` (Speed 0.34) → `Run` (0.83) → `Attack` ✅
- Muhafız hasarı **tam `Attack_4` vuruş karesinde** indi, oyuncu canı 5→4 ✅
- `Hurt`: `Walk → Hurt_0 → Hurt_1 → Idle` ✅
- `Death`: `Death` state + alpha solma (0.44) birlikte çalışıyor ✅
- Oyuncu flipX: sağa facing=1/flipX=False, sola facing=-1/flipX=True ✅
- Hayaletler dokunulmadı, prosedürel sistemde çalışıyor ✅
- Konsol hatasız.

**TEST YÖNTEMİ UYARISI:** `EditorApplication.Step()` ile kare-kare
adımlamada `HitStop` (WaitForSecondsRealtime kullanıyor) çözülmüyor, çünkü
gerçek saat ilerlemiyor. Bu, ölçümlerde "animasyon donmuş / Hurt
tetiklenmiyor" gibi **yanlış alarmlar** üretti. Hasar zamanlaması ölçerken
`HitStopHost` geçici kapatılmalı — aksi halde yanlış sonuç okunur.

**[GİDERİLDİ — bkz. 2026-08-25 (6)]** Aşağıdaki regresyon bir sonraki
turda çözüldü.

**!!! BİLİNEN REGRESYON (bu turda oluştu):**
Oyuncunun ve iki muhafızın TÜM parçacık efektleri kayboldu; çünkü hepsi
prosedürel animatörlerin içinde yaşıyordu ve o bileşenler devre dışı
bırakıldı:
- `PlayerProceduralAnimator`: zıplama tozu (satır 229), duvar-sekme
  darbesi (237), koşu ayak tozu (369), duvar-kayma tozu (404), iniş
  tozu (447)
- `EnemyProceduralAnimator`: ölüm patlaması (138), hasar kıvılcımı (163)
Ayrıca oyuncunun **eğilme (crouch) görsel geri bildirimi** de aynı sebeple
kayboldu (bu turda eklenen `crouchSquashAmount` prosedürel animatörde).
`MeleeAttack`'in vuruş parçacıkları etkilenmedi (saldıran tarafta).
→ Çözüm: parçacık/toz mantığını Animator'dan bağımsız ayrı bir bileşene
çıkarmak (ör. `PlayerFootstepFX`), böylece iki sistemden de bağımsız çalışır.

---

### 2026-08-25 (6) — Parçacık regresyonu giderildi

**Sorun:** Bir önceki turda prosedürel animatörler devre dışı bırakılınca,
İÇLERİNDE yaşayan tüm toz/parçacık efektleri sessizce kayboldu.

**Kilit içgörü:** Sprite-sheet Animator'ı yalnızca `m_Sprite` özelliğini
sürüyor, transform'a hiç dokunmuyor. Dolayısıyla parçacık mantığı ayrı bir
bileşene çıkarılırsa **her iki animasyon sisteminden de bağımsız** çalışır.

**Değişiklik (Implemented):**
- [PlayerFootstepFX.cs](Assets/_SoulSplit/Scripts/Player/PlayerFootstepFX.cs)
  — zıplama, iniş, koşu ayak basışı, duvar kayması, duvar sekmesi.
- [EnemyImpactFX.cs](Assets/_SoulSplit/Scripts/Enemies/EnemyImpactFX.cs)
  — hasar kıvılcımı, ölüm patlaması (yer/hava fiziği ayrımıyla).
- Eski bileşenlerdeki **ayarlı renk/süre değerleri korunarak** aktarıldı
  (`dustColor`, `runDustCooldown`, `wallSlideDustInterval`,
  `deathParticleColor`, `hurtSparkColor`, `locomotion`).
- **Hayaletlere EKLENMEDİ**: onların `EnemyProceduralAnimator`'ı hâlâ açık
  ve kendi parçacıklarını üretiyor; eklenseydi çift parçacık olurdu.
- İyileştirme: ayak tozu artık Animator'ın gerçek döngü fazına (0.0 ve 0.5
  noktaları = iki ayak basışı) senkronize; eski prosedürel stride fazından
  daha doğru.

**Çözülen gerçek hata — iniş tozu bir karelik boşluk:**
İlk denemede iniş tozu hiç çıkmadı. Kare kare izleyince sebep görüldü:

| kare | y | vy | grounded |
|---|---|---|---|
| 18 | 1.10 | **-27.77** | false |
| 19 | 0.90 | **0.00** | false ← çarpışma hızı sıfırladı, ama hâlâ "havada" |
| 20 | 0.90 | 0.00 | **true** |

`IsGrounded` (FixedUpdate'te OverlapBox ile hesaplanıyor), fizik çarpışmayı
çözdükten BİR KARE sonra true oluyor. O ara karede anlık hız yazılırsa iyi
değer sıfırla eziliyor. Çözüm: anlık hız yerine **düşüşteki en yüksek hız**
(`Mathf.Min`) saklanıyor, yerdeyken sıfırlanıyor.

**Test (Tested):** Play Mode'da, `FX_Burst` objeleri sayılarak:
- Zıplama tozu ✅ · İniş tozu ✅ (düzeltmeden sonra) · Koşu ayak tozu ✅
  (7 toz, animasyonla senkron) · Duvar kayması ✅ (4 toz) · Duvar sekmesi ✅
- Düşman hasar kıvılcımı ✅ · Düşman ölüm patlaması ✅
- Konsol hatasız.

**Bilinen sınır (bir sonraki turda giderildi, bkz. 7):** Eğilme görsel
geri bildirimi yok.

---

### 2026-08-25 (7) — Eğilme (crouch) animasyonu eklendi

**Sorun:** `PlayerController` eğilmeyi zaten uyguluyordu (collider kısalıyor,
hız düşüyor) ama sprite-sheet'e geçince görsel geri bildirim kalmamıştı.
Seviyede sadece eğilerek geçilebilen bir tünel olduğu için bu gerçek bir
oynanabilirlik eksiğiydi.

**Ölçüm:** Knight setinde **gerçek bir eğilme karesi yok**. En yakını
`Protect` (siper alma) pozu, o da 60px — ayaktaki 64px'e göre neredeyse tam
boy. Tünel açıklığı ise sadece **1.25 birim** (`Ceiling_CrouchTunnel`
tabanı y=1.25, zemin y=0); ayaktaki karakter 1.8 birim. Yani tek başına
sprite değişimi yetmez, gerçekten kısalması gerekiyordu.

**Değişiklik (Implemented):**
- `Protect.png` → `PlayerSheets/Crouch.png` olarak kopyalandı, oyuncuyla
  aynı PPU (35.56) ve pivot ile `Crouch_0` olarak dilimlendi.
- `Crouch.anim` (tek kare, döngülü — pozu tutar) elle oluşturuldu.
- `PlayerAnimator.controller`'a `IsCrouching` (Bool) parametresi,
  `Crouch` state'i ve geçişler eklendi. **Builder yeniden çalıştırılmadı**
  — çalıştırılsaydı Attack klibinin elle zamanlaması silinirdi.
- Geçişler bilinçli olarak `Idle/Walk/Run → Crouch` şeklinde kuruldu,
  **AnyState kullanılmadı**: AnyState olsaydı eğilme, Attack/Hurt/Death
  animasyonlarını ortasından keserdi.
- [PlayerCrouchVisual.cs](Assets/_SoulSplit/Scripts/Player/PlayerCrouchVisual.cs)
  — görseli collider yüksekliğine uyduran ayrı bileşen.
  `crouchScaleY = (64 × crouchColliderHeightMultiplier) / 60 = 0.587`,
  yani sabit yazılmadı, gerçek collider oranından türetildi.
- `PlayerAnimatorBridge`'e `IsCrouching` yazımı eklendi.

**Pivot matematiği (kritik):** Pivot karakterin dikey MERKEZİNDE olduğu
için düz ölçekleme ayakları yerden kaldırırdı. Telafi:
`localPos.y = feetOffset × (1 − scale)`. Ölçülen sonuç: eğik haldeyken
görsel ayak dünya-Y = **0.005** (zemin y=0.00) — hizalama doğru.

**Neden ayrı bileşen:** `PlayerAnimatorBridge` bilinçli olarak transform'a
dokunmuyor (sözleşmesi bu), sprite-sheet Animator'ı da sadece `m_Sprite`
sürüyor. Dolayısıyla `PlayerCrouchVisual` transform'un tek sahibi — çakışma yok.

**Test (Tested):** Play Mode'da:
- Ayakta: `Idle`, scaleY=1.000 · Eğik: `Crouch`/`Crouch_0`, scaleY=0.587,
  localY=−0.372 · Kalkınca `Idle`, scaleY=1.000 ✅
- Görsel ayak hizası eğikken y=0.005 (zeminde) ✅
- **Tünel testi:** ayakta yürüyünce x=5.55'te tıkandı; eğilerek yürüyünce
  x=15.77'ye kadar geçti → tünel yalnızca eğilerek geçilebiliyor ✅
- Konsol hatasız.

**Bilinen sınır:** Eğilirken sprite dikeyde %59'a sıkıştırıldığı için pixel
art bir miktar deforme oluyor. Gerçek çözümü, sete uygun çizilmiş bir
crouch karesi olurdu; pakette yok.

---

### 2026-08-25 (8) — Zıplama + ruh formu animasyonları

## Zıplama

**Ölçüm önce:** Jump.png 6 kare, ama kareler arası konum **tutarsız**:

| kare | y aralığı | gövde merkezi X |
|---|---|---|
| 0 | 0..56 | 44 |
| 1 | **17**..80 | 41 |
| 2 | **17**..80 | 30 |
| 3 | 6..70 | 38 |
| 4 | 0..57 | 48 |
| 5 | 0..56 | **61** |

Idle referansı: y 0..63, merkez 33. Sabit pivotla **kare 5** kullanılsaydı
karakter yatayda ~0.76 birim sağa sıçrardı. Kare 0 ise kalkış çökmesi —
oyunun zıplaması anlık olduğu için görseli geciktirirdi.

**Değişiklik:** Sadece tutarlı **kare 1-2-3** kullanıldı (merkezler 41/30/38,
pivota yakın). `Jump.anim` döngüsüz — son kare (havada uzanmış poz) iniş
olana kadar asılı kalıyor. Geçişler `Idle/Walk/Run/Crouch → Jump`
(`IsGrounded` false), `Jump → Idle` (true). **AnyState kullanılmadı** ki
Attack/Hurt/Death zıplarken kesilmesin.

**Test:** Tam zıplamada (JumpHeld) `Jump_1→Jump_2→Jump_3` yükselirken
oynadı, y=3.35'e çıktı, kare 3 havada asılı kaldı, inişte `Idle`'a döndü ✅
Kısa "tap jump"ta (JumpHeld false) `lowJumpMultiplier` devreye girip
havada kalma 0.2s'ye düştü — bu doğru davranış, animasyon da ona uydu ✅

## Ruh formu

**Tasarım kararı:** Ruh, oyuncunun kendi bedeninden çıkan ruhu. Bu yüzden
**aynı şövalye sprite'ları** camgöbeği tonlu ve yarı saydam olarak
kullanıldı — yeni sanat gerekmedi, stil tutarlı kaldı.

**Yürüme/koşma bilinçli olarak EKLENMEDİ:** ruh süzülüyor, yürümüyor.
Duvarlardan geçen bir hayalette yürüme döngüsü yanlış okunurdu. Hareket
hissini `SoulController`'ın kendi drift (`Mathf.Sin`) salınımı veriyor —
o transform'a, Animator ise `m_Sprite`'a yazdığı için ikisi çakışmadan
üst üste biniyor.

- `SoulSheets/` → Idle + Attack + Hurt, oyuncuyla aynı PPU/pivot.
- [SoulAnimatorBridge.cs](Assets/_SoulSplit/Scripts/Player/SoulAnimatorBridge.cs)
  — ruhta `Health` yok, `Health.FindOn` üzerinden `DamageRelay` ile bedenin
  can havuzuna bağlanıyor.
- `SoulController`'a `useAttackRotationOverlay` bayrağı eklendi ve **false**
  yapıldı; yoksa kendi saldırı rotasyonu kare animasyonuyla üst üste binerdi.
  Bayrak false iken `_attackTimer` yine de işliyor (saldırı durumu normal
  süresinde bitsin diye).
- Ruh `impactDelay` 0.125 → 0.1667 (klip zamanlamasıyla hizalı).
- Renk `(0.30, 0.85, 0.95, 0.55)` + ruh ışığı camgöbeği/1.6. İlk denenen
  mavi ton yetersizdi — şövalye zaten beyaz/gümüş ve sahne mavi ışıklı
  olduğu için beden ile ruh ayırt edilemiyordu.

**Test:** Gerçek form değiştirme (SoulSwitch girdisi) ile:
kareler ilerledi (`Idle_2→Idle_0→Idle_2→Idle_3→Idle_1`) **ve** drift
salınımı sürdü (localY −0.09→0.06→0.11→−0.03→−0.12) ✅
Saldırıda `Attack_0→_1→_3→_4`, **rotZ=0.0** (çift efekt yok) ✅
Konsol hatasız.

**Not:** İlk testte ruh donuk görünmüştü — objeyi `SetActive` ile zorla
açtığım içindi; gerçek `SoulSwitchManager` akışıyla test edilince sorun
yoktu. Bu, "gerçek kod yolundan test et" kuralının bir örneği daha.

---

### 2026-08-25 (9) — Hayalet düşmanlar sprite-sheet'e geçirildi

**Tasarım kararı (önemli):** Ruh formunda şövalye sprite'ı kullanmak
tematik olarak doğruydu (oyuncunun kendi ruhu). Ama hayalet DÜŞMANLAR
için aynısını yapmak yanlış olurdu — oyunda beş ayrı şövalye olurdu
(oyuncu, ruh, muhafız, iki hayalet) ve düşman çeşitliliği yok olurdu.
Bu yüzden ayrı bir paket kullanıldı:
[CraftPix Free Ghost Pixel Art Sprite Sheets](https://craftpix.net/freebies/free-ghost-pixel-art-sprite-sheets/)
— aynı 128px kare yüksekliği (dilimleme hattı birebir çalıştı), aynı
pixel-art ailesi, ama *yūrei* silueti (uzun saç, savrulan kaftan).

**Atama:** Onre → `GhostEnemy_Serpent` (pakette `Flight.png` var, süzülen
düşman için ideal — "Walk" state'i olarak kullanıldı). Yurei → `GhostEnemy_MaskSwarm`.

**Ölçüm bulgusu:** Bu paketin içeriği kare merkezinde DEĞİL
(Onre x≈63, Yurei x≈58; şövalyelerde 34'tü). Şövalye pivotu körlemesine
kullanılsaydı hayaletler yana kayardı. Her pakete kendi pivotu hesaplandı:
Onre `(0.492, 0.270)`, Yurei `(0.453, 0.301)`. PPU 35.56 (oyuncuyla aynı
piksel yoğunluğu) → içerik 70px = 1.97 birim boy.

**Sahne düzeltmeleri (gerekliydi):** Eski `Ghost_Serpent` sprite'ı YATAYDI
(2.56×1.10) ve collider ona göre kaydırılmıştı. Yeni sprite dikey humanoid
olduğu için:
- `GhostEnemy_Serpent` collider offset `(0.72, 0.08)` → `(0,0)`, r=0.6
- Her iki can barı → `(0, 1.15, 0)`

**Kod düzeltmesi:** `EnemyAnimatorBridge`'de yorumda "uçan düşmanlarda
toplam hızı kullan" yazıyordu ama kod sadece `Velocity.x` okuyordu —
yorum ile kod çelişiyordu. `useTotalVelocity` bayrağı eklendi ve
hayaletlerde açıldı (süzülen düşman dikeyde de hareket eder).

**Saldırı zamanlaması — bulunan gerçek sorun:** İlk ölçümde hasar
`Attack_2`'de indi, vuruş karesi `Attack_3` **2 kare sonra** geldi. Yani
oyuncu darbeyi görmeden yiyordu. Sebep: `AnyState→Attack` geçiş süresi +
tetikleme gecikmesi (~33ms). **Düşman saldırısında görsel hasardan ÖNCE
gelmeli** (oyuncunun kendi saldırısında tam tersi istenir — orada
tepkisellik için hasar 1 kare önde). Vuruş kareleri 0.340 → 0.290'a
çekildi. Doğrulama: vuruş karesi ve hasar artık **aynı karede** (37).

**Test (Tested):** Gerçek AI/form-değiştirme akışından:
- Uykuda (oyuncu bedende): alpha 0.35, animasyon yine de işliyor ✅
- Ruh formuna geçince uyanıyor: alpha 0.35→0.45→0.87→0.99 ✅
  (`GhostEnemy.UpdateDormantLook` alpha'yı sürüyor, Animator ise
  `m_Sprite`'ı — çakışma yok)
- AI zinciri `Patrol→Chase→Attack→Patrol→Chase`, animasyon takip etti ✅
- flipX doğru, `useTotalVelocity` ile Speed düzgün ✅
- Parçacık efektleri `EnemyImpactFX`'e devredildi (locomotion=Floating,
  havada dağılan ölüm) ✅
- Konsol temiz (tek uyarı benim `HitStopHost`'u ölçüm için kapatmamdan;
  sahnede aktif kaldığı doğrulandı).

---

### 2026-08-25 (10) — Muhafızlar renk tonuyla ayrıştırıldı

**Sorun:** Oyuncu (Knight_1, gümüş) ile muhafız (Knight_2, mor) ekranda
neredeyse aynı görünüyordu — paketin mor varyantı o boyutta okunmuyor ve
sahnenin mavi ışığı ikisini de aynı tona çekiyordu. Savaş okunabilirliği
için gerçek bir sorundu.

**Değişiklik:** `SpriteRenderer.color` üzerinden ton verildi:
- `PhysicalEnemy_Guardian` → `(0.92, 0.46, 0.24)` bronz/kor
- `PhysicalEnemy_Guardian_Final` → `(0.82, 0.24, 0.22)` koyu kızıl
  (boss olduğu renkten okunsun diye ayrıca ayrıştırıldı)

İlk denenen `(1.00, 0.52, 0.42)` fazla somon/pembeye kaçıyordu ve "taş
muhafız" olarak okunmuyordu; daha turuncu/topraksı tona çekildi.

**HitFlash uyumu doğrulandı:** `HitFlash._baseColor`'ı `Awake`'te okuduğu
için sahnede ayarlanan ton korunuyor. Reflection ile teyit edildi:
`_baseColor` = bronz/kızıl (beyaz değil), yani vuruş flaşından sonra
doğru tona dönüyor.

**TEST YÖNTEMİ — üçüncü kez aynı tuzak:** Ölçümde renk "beyazda kalmış"
göründü ve regresyon sandım. Sebep yine `WaitForSecondsRealtime`:
`EditorApplication.Step()` ile kare kare ilerlerken gerçek saat
ilerlemediği için flaş coroutine'i hiç bitmiyor. `_routine != null`
kontrolüyle doğrulandı. **Renk/zamanlama ölçerken bu kural unutulmamalı.**

---

### 2026-08-25 (11) — Vuruş izi (slash) VFX'i eklendi

**Önce kontrol:** Şövalye sprite'ının saldırı karelerinde zaten ince
hareket çizgileri çizili. Karşılaştırmalı test yapıldı (`showSlashTrail`
kapalı vs açık) — sprite'ın kendi çizgileri ince ve zayıf kalıyor; yeni
yay onların YERİNE değil ÜSTÜNE binip savurmayı okunur kılıyor.

**Değişiklik:** [SlashFX.cs](Assets/_SoulSplit/Scripts/Core/SlashFX.cs)
— `ParticleFX` ile aynı desen (prefab yok, kodla üretilir, ömrü bitince
kendini yok eder). Ring-segment mesh + uzunluk boyunca alfa gradyanı
(kuyruk sönük, uç parlak) ile klasik "slash" görünümü.

**Neden mesh, parçacık değil:** Parçacık izi noktalı/dağılmış okunur;
kılıç savurması ise sürekli bir yay. Mesh bunu doğru veriyor.

- `MeleeAttack`'e `showSlashTrail` + `slashColor`/`heavySlashColor` eklendi.
- Yay **isabetten bağımsız** çıkıyor: havaya savurunca da görünmeli,
  yoksa oyuncu saldırının çıktığını göremez.
- Hafif: yarıçap 1.15, kalınlık 0.20, 120° tarama. Ağır: 1.5 / 0.30 / 155°.
- Renkler forma göre: beden sıcak (krem/amber), ruh soğuk (camgöbeği).
- Sönme `Time.unscaledDeltaTime` kullanıyor — hit-stop sırasında da akar
  (`CameraFollow`/`HitFlash` ile aynı kural).

**İki tur ayar gerekti:**
1. İlk sürüm fazla kalın/opaktı ve karakterin silueti üstünü kapatıyordu.
2. Merkez oyuncunun tam üstündeydi; yay gövdenin ETRAFINDA dönüyordu.
   `facing * 0.3` kadar öne kaydırıldı → artık gövdenin ÖNÜNDE savruluyor.

**SÜREÇ HATASI (kayda değer):** Ayar değişikliğinden sonra `refresh_unity`
çağırmadan doğrudan Play Mode'a girdim; Unity eski assembly ile çalıştı ve
ekranda hiçbir şey değişmedi. Görsele bakıp "ayar yetmedi" diye körlemesine
tekrar ayar yapacaktım — bunun yerine **mesh geometrisini ölçtüm**
(kalınlık 0.320 çıktı, hedef 0.20 idi) ve derlenmediğini anladım.
Kural: Editor odakta değilken kod düzenlemesinden sonra Play'e girmeden
önce mutlaka `refresh_unity(compile: request)`.

**Test (Tested):**
- Mesh üretimi: 50 vertex, 48 üçgen, `isVisible=true`, sortingOrder 70,
  URP unlit shader ✅
- Derleme sonrası ölçüm: hafif kalınlık **0.200** (hedef 0.20), konum
  oyuncudan `+0.30` önde ✅
- Ağır: iç 1.35 / dış 1.65 / kalınlık 0.30 — hafiften belirgin geniş ✅
- Görsel: yay karakterin önünde temiz hilal olarak okunuyor, silueti
  kapatmıyor ✅
- Konsol hatasız.

**Kapsam:** Bu turda sadece `MeleeAttack` (beden + ruh). Düşmanlar bir
sonraki turda eklendi.

---

### 2026-08-25 (12) — Vuruş izi düşman saldırılarına da eklendi

**Tasarım nüansı:** Düşman saldırıları zaten `attackWindup` ile telegraf
ediliyor; oraya oyuncuyla aynı güçte iz koymak görsel gürültü yaratırdı.
Bu yüzden düşman izleri **kasten daha sönük** (alfa 0.38–0.46, oyuncu
0.55) ve her düşman **kendi kimlik rengini** kullanıyor.

- `EnemyBase`'e `showSlashTrail` / `slashColor` / `slashRadius` /
  `slashThickness` / `slashSweep` eklendi. `slashRadius` 0 bırakılırsa
  `attackRange`'ten türetiliyor (yeni düşman eklendiğinde elle ayar
  gerekmesin diye).
- `ApplyAttackDamage` içinde, **isabetten bağımsız** tetikleniyor —
  oyuncu kaçmayı başardığında da savurmanın nereden geçtiğini görmeli.

| Düşman | Yarıçap | Kalınlık | Alfa | Ton |
|---|---|---|---|---|
| Guardian | 1.45 | 0.26 | 0.42 | bronz |
| Guardian_Final | 1.45 | 0.26 | 0.46 | kızıl (boss) |
| Ghost_Serpent | 1.15 | 0.18 | 0.38 | soğuk/soluk |
| Ghost_MaskSwarm | 1.10 | 0.18 | 0.38 | soğuk/soluk |

Muhafızlar iri gövdeli olduğu için daha geniş/kalın; hayaletler pençe
saldırdığı için ince ve soluk.

**Test (Tested):** Gerçek AI saldırılarından (elle tetikleme değil):
- Muhafız: kalınlık 0.26, maxAlpha 0.34 ✅
- Hayalet: kalınlık 0.18, maxAlpha 0.31 ✅
- İkisi de görsel olarak doğrulandı; oyuncununkinden belirgin sönük.
- Konsol temiz (tek uyarı `HitStopHost`'u ölçüm için kapatmamdan;
  sahnede aktif kaldığı ayrıca doğrulandı).

---

### 2026-08-25 (13) — Başlangıç bölgesine ilk düşman eklendi

**Sorun:** İlk düşman x=102'deydi; oyuncu spawn'dan (x=0) sonra uzun süre
hiç savaşla karşılaşmıyordu.

**Yerleştirme — geometri kısıtı belirledi:** Başlangıç bölgesi ölçüldü:

| Bölge | x aralığı | Not |
|---|---|---|
| Ground_Step1 | 1.30–3.70 | 1 birim basamak |
| açık zemin | 3.70–6.00 | dar; üstünde Platform_A tabanı **y=2.20** |
| Ceiling_CrouchTunnel | 6.00–9.00 | açıklık sadece **1.25** birim |
| açık zemin | 9.00–33.00 | tavan y=3.80, geniş |

Muhafız **2.20 birim** boyunda:
- x≈4.8'e konamaz — Platform_A tabanı tam 2.20'de, sıkışırdı.
- Tünele hiç giremez (1.25 < 2.20).

**Bulunan gerçek hata:** İlk denemede x=10.5'e konuldu. Play Mode'da
düşman 9.60'a yürüyüp **takıldı** — devriye menzili (2.5) onu tünelin
içine sokuyor, gövdesi tünel tavanına çarpıyordu. Konum x=13'e alındı;
devriye aralığı 10.5–15.5 olup tünelden (biter 9.0) tamamen uzaklaştı.
Aralıkta zemin seviyesinde başka engel olmadığı ayrıca collider taramasıyla
doğrulandı.

**Değişiklik:** `PhysicalEnemy_Guardian` **kopyalandı** (sıfırdan kurmak
yerine — Animator, EnemyAnimatorBridge, EnemyImpactFX, HitFlash,
WorldHealthBar bağlantıları hazır gelsin diye). Kopyada `switchManager` ve
`bodyTransform` referanslarının korunduğu doğrulandı.

`PhysicalEnemy_Guardian_Intro` @ (13, 1.1) — öğretici düşman olarak
zayıflatıldı:
- can 3 → **2**
- algı yarıçapı 7 → **6** (daha geç fark etsin)
- devriye menzili 4 → **2.5** (postundan uzaklaşmasın)
- saldırı cooldown → **1.6s** (daha yavaş saldırsın)

Rengi bilerek bronz bırakıldı: aynı düşman tipi = aynı renk kimliği.

**Test (Tested):** Play Mode'da gerçek AI'dan:
- Collider alt kenarı 0.005 (zeminde, batmıyor/yüzmüyor) ✅
- Serbest devriye: x 11.89–15.51 (3.62 birim), Walk animasyonu ✅
- Oyuncu menzile girince Attack'e geçti, vuruş izi çıktı ✅
- Konsol temiz (tek uyarı ölçüm için kapattığım `HitStopHost`'tan).

---

### 2026-08-25 (14) — Başlangıç koridoru geçilemiyordu: `Ground_Step1` kaldırıldı

**Sorun:** Bir önceki turda eklenen intro düşmanına (x=13) ulaşılamıyordu.

**Ölçüm (spekülasyon değil, Play Mode'da girdi simülasyonu):**

| Deneme | En uzak x |
|---|---|
| Sadece sağa yürü | **0.85** — takılı |
| Sağa + eğil | **0.85** — takılı |
| Sağa + zıpla | 37.5 ama **y=20.3** (platformlara tırmanıp zeminden kopuyor) |

**Kök neden — iki katmanlı:**
1. `Ground_Step1` (x 1.3–3.7, yükseklik 1.0) yürüyerek geçilmiyordu.
   Unity 2D'de **otomatik basamak çıkma yok**; test edildi: 1.0, 0.4, 0.3
   ve hatta **0.2** birimlik basamak bile yürüyen karakteri durduruyor.
2. Basamağı zıplayarak geçen oyuncu **x=4.05, y=1.75'te sıkışıyordu** —
   basamağın sağ kenarında dengede kalıp `Platform_A`'nın sol yüzüne
   yaslanıyor. Sebep geometri: basamak üstü y=1.0, `Platform_A` tabanı
   y=2.2 → arada **1.2** birim, oyuncu ise **1.8** boyunda. Sığmıyor.

**Neden alçaltmak çözmedi:** `Platform_A` tabanı 2.2'de asılı olduğu için
basamak en fazla `2.2 − 1.8 = 0.4` olabilirdi; ama 0.2 bile yürümeyi
kestiği için bu aralıkta çalışan bir yükseklik YOK. Başlangıç koridoru
basamağı kaldırmıyor.

**Değişiklik:** `Ground_Step1` silindi. Doğrulama: basamak kapalıyken
oyuncu **zıplamadan**, yalnızca yürüyüp eğilerek x=12.48'e ulaştı
(düşman devriyesi 10.5–15.5).

Geri istenirse: `x=2.5, y=0.5, boyut 2.4×1.0, layer=Ground, Tile_Stone
tiled`. Ama aynı sorun geri gelir.

**Kaybedilen:** "zemin tek düz olmasın" isteği bu koridorda karşılanamıyor.
Zemin seviyesinde yükseklik çeşitliliği ancak **x>9.5** bölgesinde mümkün
(orada tavan y=3.8+, bol boşluk var) — orada bir basamak yine zıplama
gerektirir ama sıkışma yaratmaz.

**Ayrıca öğrenilen:** Seviyenin ana yolu **zemin**. `Ground_Floor`
(x −5..33) ve `Floor_Room2` (x 33..48) kesintisiz; platform zinciri
(A→B→C→baca→ExitTop y=16) yukarı giden ayrı bir rota. İntro düşmanının
zeminde olması doğru; sorun yalnızca erişimdi.

**DOĞRULAMA TAMAMLANDI (proje yeniden açıldıktan sonra):**
Temiz bir Play Mode oturumunda, spawn'dan (x=0) başlayarak **yalnızca
sağa yürüme + eğilme** girdisiyle (ziplama YOK):
- İlk temas kare 126'da, oyuncu x=8.04'te gerçekleşti
- Düşman `Patrol` → `Attack` durumuna geçti
- Ayrı bir turda düşman oyuncuyu kovalayıp öldürdü (can respawn'a düştü),
  yani karşılaşma uçtan uca işliyor
- Konsol temiz (tek uyarı ölçüm için kapattığım `HitStopHost`'tan; sahnede
  aktif kaldığı ayrıca doğrulandı)

**Yan gözlem:** Düşman kovalarken tünel ağzına (x≈9.0) kadar geliyor ama
2.2 birim boyu tünele (1.25 açıklık) girmesini engelliyor — orada duruyor.
`loseTargetRadius=10` olduğu için oyuncu yeterince uzaklaşırsa devriyeye
dönüyor. Şimdilik sorun değil, ama oyuncu tünelde güvenli bölge bulabilir.

**NOT — assembly adı değişti:** Proje yeniden açıldığında script'ler artık
`Assembly-CSharp` yerine **`SoulSplit.Runtime`** assembly'sinde derleniyor
(asmdef eklenmiş). Reflection ile tip çözerken
`System.Type.GetType("SoulSplit.X.Y, SoulSplit.Runtime")` kullanılmalı;
eski ad `null` döner ve "Type cannot be null" hatası verir.

---

### 2026-08-25 (15) — Tünel kamp sorunu: düşmana "tasma" davranışı eklendi

**Sorun:** Oyuncu eğilme tüneline (x 6–9, açıklık 1.25) saklandığında,
2.2 birim boyundaki muhafız içeri giremiyor ama **tünel ağzında süresiz
kamp ediyordu** (ölçüldü: 5 sn sonra hâlâ `Chase`, x=9.57, mesafe 5.95).

**Neden `loseTargetRadius` yetmedi:** O parametre yalnızca hedef
UZAKLAŞINCA devreye giriyor. Oyuncu YAKIN ama ULAŞILAMAZ olduğunda
(mesafe 5.95 < 10) düşman sonsuza kadar kovalamaya devam ediyordu.
Değeri düşürmek de çözüm değildi: `loseTargetRadius` mutlaka
`detectionRadius`'tan büyük olmalı, yoksa algı sınırında titreme olur.

**Neden tüneli taşımadım:** `Ceiling_CrouchTunnel` görsel olarak
`Platform_A`'nın (x 4.5–9.5) altında duruyor, tavan kütlesini ondan
alıyor. Tek başına kaydırılsa boşlukta asılı bir levha olurdu. Ayrıca
mesafe tek başına çözmüyordu: bir kez çekilen düşman, tünel nerede olursa
olsun ağzına kadar geliyordu.

**Değişiklik — `EnemyBase.maxChaseDistanceFromPost` (opt-in, varsayılan 0):**
Kovalarken doğum noktasından bu mesafeyi aşarsa hedefi bırakıp postuna
döner. Varsayılan **0 = sınırsız (eski davranış)**, böylece mevcut
düşmanlar etkilenmedi — doğrulandı: diğer 4 düşman hâlâ 0.

`PhysicalEnemy_Guardian_Intro`: post x=13 → **16**, tasma **6.0**.
Böylece kovalamada en fazla x=10.0'a inebiliyor; tünel ağzına (9.0)
**ulaşamıyor**. Devriye 2.5 olduğu için bu, devriyenin ötesinde 3.5
birimlik gerçek kovalama alanı bırakıyor.

**İki tur düzeltme gerekti (ikisi de ölçümle yakalandı):**

1. **Titreme:** Tek eşikle düşman sınırda `Chase↔Patrol` arasında
   **420 karede 100+ kez** durum değiştirdi (sınır dışı → Patrol → Patrol
   onu içeri itiyor → yeniden Chase). Çözüm: histerezis — bırakma eşiği
   ile yeniden hedeflenme eşiği ayrıldı.
2. **Yavaş salınım:** Histerezis 0.5×tasma iken düşman postun 3 birim
   yakınına dönüyor, orada saklı oyuncuyu (mesafe 5.5 < algı 6) tekrar
   algılayıp geri geliyordu → 10–13 arası sonsuz gidip gelme (5 değişim).
   Çözüm: eşik **0.25×tasma**'ya daraltıldı; düşman artık saklı oyuncunun
   algı yarıçapından çıkacak kadar geri dönüyor.

**Test (Tested):**

| | Önce | 1. tur | Son |
|---|---|---|---|
| Durum değişimi (8 sn, oyuncu tünelde) | 100+ | 5 | **1** |
| Düşmanın ulaştığı en sol nokta | 9.57 (ağızda) | 9.90 | **9.93** |
| Bitiş durumu | `Chase` (kamp) | `Patrol` (salınım) | **`Patrol`, post 16.0'a döndü** |

- Normal karşılaşma bozulmadı: spawn'dan yürüyüp eğilerek kare 183'te
  `Attack` (öncesi kare 182 — fark yok).
- Diğer 4 düşmanın tasma değeri 0 (etkilenmediler) ✅
- Konsol temiz (tek uyarı ölçüm için kapattığım `HitStopHost`'tan;
  sahnede aktif kaldığı doğrulandı).

**Bilinen sınır:** Tasma pozisyona dayalı; düşman postundan uzaktayken
oyuncu yanına gelirse de vazgeçer. Bu senaryoda sorun değil (post ile
tünel arası kısa), ama daha uzun kovalama isteyen bir düşmanda değer
yükseltilmeli.

---

### 2026-08-25 (16) — Zemin çeşitliliği x>22 bölgesine eklendi

**Bağlam:** `Ground_Step1` başlangıç koridorunda kaldırılmıştı (bkz. tur 14)
çünkü `Platform_A` orada 2.2'de asılı ve çalışan bir basamak yüksekliği
yoktu. Çeşitlilik, tavanın yüksek olduğu bölgeye taşındı.

**Yerleştirme kısıtı:** İlk düşünülen x>9.5 idi, ama intro muhafızın
**tasma menzili x=10–22** (post 16 ± tasma 6). Oraya basamak koymak onun
devriyesini de bloklardı — tünelin yaptığının aynısı. Bu yüzden merdiven
**x=23**'ten başlatıldı; düşmanın ulaşabildiği en sağ nokta 22.0, çakışma yok.

**Eklenen (merdiven: çık–çık–in):**

| Obje | x aralığı | Üst y |
|---|---|---|
| `Ground_Rise1` | 23.0–26.0 | 1.0 |
| `Ground_Rise2` | 26.0–29.0 | 2.0 |
| `Ground_Rise3` | 29.0–31.0 | 1.0 |

Bloklar y=−0.5'ten başlıyor (zemine gömülü) ki dikiş görünmesin.
Tavan orada y=7.5 (chimney duvarları) → oyuncu `Rise2` üstünde başı 3.8'de,
sıkışma riski yok. Bu, `Ground_Step1`'i öldüren `Platform_A` sorununun
tekrarlanmadığını garanti ediyor.

**Görsel tutarlılık:** İlk sürümde bloklar düz/çıplak durdu; mevcut tüm
platformların `TopEdge` çocuğu (sprite `Tile_StoneEdge`, yükseklik 1.2,
üst kenarı ebeveynin üstüyle hizalı, `sortingOrder=1`) varmış. Aynı desen
üçüne de uygulandı — artık kaya dokulu üst kenarla çevredeki araziyle
aynı dilde.

**Test (Tested):**
- Oyuncu x=21'den sağa yürüyüp zıplayarak: kare 70'te merdivende (y=1.90),
  kare 140'ta `Floor_Room2`'de (x=33.22), kare 210'da x=41.05 ✅
- x=41.05'te duruyor — orası `SoulIntro_Wall` (ruh formu gerektiren duvar),
  beklenen ilerleme kilidi, merdivenle ilgisi yok ✅
- Düşman devriyesi etkilenmedi: x 14.34–18.52 serbest ✅
- Konsol temiz.

---

### 2026-08-25 (17) — Düşmanlara ağır saldırı eklendi

**Bağlam:** Oyuncunun "şiddet kademesi" tepki sistemi (2026-08-24'te
eklenmişti: `heavyDamageThreshold=2`, büyük knockback, güçlü hit-stop,
şiddetli kamera sarsıntısı) **ilk oturumdan beri uykudaydı** — çünkü
oyundaki hiçbir düşman saldırısı `damage=1`'den fazlasını göndermiyordu.
Bu tur o boşluğu kapattı.

**Mekanik (`EnemyBase`, opt-in):**
- `enableHeavyAttack` (varsayılan **false** — mevcut davranış korunur)
- `heavyAttackChance` — her saldırı denemesinde ağır olma olasılığı
- `heavyAttackDamage = 2` — oyuncunun `heavyDamageThreshold` değeriyle
  **kasten aynı**; eşleşmezse ağır tepki hiç tetiklenmez
- `heavyAttackWindup = 0.7s` (hafif 0.35s) — **adil telgraf**: uzun
  hazırlık, "ağır geliyor" demek. Kademe saldırının BAŞINDA seçiliyor ki
  hazırlık süresi bilgi taşısın.
- `heavyAttackCooldown = 2.4s`, `heavyAttackHitboxSize` daha geniş

**Olay imzası değişti:** `EnemyBase.OnAttackTriggered` artık
`Action<AttackTier>` (oyuncudaki `MeleeAttack` ile aynı enum ve desen).
`EnemyProceduralAnimator` ve `EnemyAnimatorBridge` buna göre güncellendi.

**Animasyon:** Her düşman kendi paketinin `Attack_2` setini `HeavyAttack`
olarak aldı (Knight_2 muhafız için, Onre/Yurei hayaletler için).
Vuruş karesi **tahmin edilmedi, ölçüldü** (kare içeriğinin en geniş
olduğu kare): Muhafız kare 2 (genişlik 88), Onre kare 2, Yurei kare 3.
Klipler vuruş 0.650s'ye gelecek şekilde elle zamanlandı (hasar 0.70s'de —
düşman saldırısında görsel hasardan ÖNCE gelmeli kuralı).

**Dayanıklılık:** `EnemyAnimatorBridge`, `HeavyAttack` parametresinin
varlığını kontrol edip yoksa hafif klibe düşüyor. Böylece HeavyAttack
klibi eklenmemiş bir düşman eklense de Animator uyarısı basmadan çalışır.

**Görsel:** Ağır vuruşta slash izi de büyüyor — yarıçap ×1.35,
kalınlık ×1.5, tarama ×1.3, alfa ×1.6, süre 0.15→0.22s.

**Sahne ayarları:**

| Düşman | Ağır | Olasılık |
|---|---|---|
| `..._Intro` | **kapalı** | — (ilk karşılaşma adil kalsın) |
| `..._Guardian` | açık | %30 |
| `..._Guardian_Final` | açık | **%45** (boss) |
| `GhostEnemy_Serpent` | açık | %30 |
| `GhostEnemy_MaskSwarm` | açık | %30 |

**Test (Tested):** Gerçek AI dövüşünde 900 kare:
- 3 hafif vuruş (hasar 1, sprite `Attack_4`) + 1 ağır vuruş
  (**hasar 2, sprite `HeavyAttack_2`** — ölçülen vuruş karesi, tam hasar
  anında) ✅
- **Oyuncunun ağır tepkisi doğrulandı:** knockback hafifte vx=−5.94,
  ağırda **vx=−10.00, vy=1.86** — yani uykudaki sistem artık çalışıyor ✅
- Ayarlar sahneye doğru kaydedildi (ölçüm için geçici yaptığım %100
  olasılık play-mode'da kaldı, sahneye sızmadı) ✅
- Konsol temiz (uyarılar `HitStopHost`'u ölçüm için kapatmamdan;
  sahnede aktif kaldığı doğrulandı).

**Bilinen sınır:** Ağır saldırı seçimi tamamen rastgele (`heavyAttackChance`).
Mesafeye/cana göre karar veren bir mantık daha okunur olurdu ama bu turda
kapsam dışı bırakıldı.

---

*Sıradaki adaylar (henüz Planlandı aşamasında, işlenmedi): hayalet dikey
knockback'i, ses katmanı (blocked — lisanslı kaynak bekleniyor).*
