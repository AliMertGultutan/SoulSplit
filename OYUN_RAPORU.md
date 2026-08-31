# SoulSplit — Yapım Raporu ve Soru-Cevap Rehberi

> Bu belge, oyunun nasıl geliştirildiğini anlatan öğretici rapordur. Kod veya Unity
> hakkında soru sorulduğunda kısa cevap bölümünden başlanabilir; ayrıntı gerektiğinde
> ilgili teknik bölüme geçilebilir.

## 1. Oyunun kısa tanımı

**SoulSplit**, 2D aksiyon-platform türünde bir Unity oyunudur. Oyuncu aynı can
havuzunu paylaşan fiziksel beden ve ruh formu arasında geçiş yapar. Fiziksel beden
platformlarda koşar, zıplar, duvarlara tutunur ve fiziksel saldırı yapar. Ruh formu
yerçekimsiz şekilde sekiz yönde süzülür ve ruhani hedeflere saldırır.

Oyunun temel tasarım sorusu şudur: “Ruhla ilerlerken geride bıraktığım bedeni ne
kadar süre güvenli tutabilirim?” Ruh enerjisi zamanla azalır; bedenden uzaklaşıldıkça
azalma hızı artar. Bu nedenle form değiştirme yalnızca görsel bir özellik değil,
oyuncunun risk ve zamanlama kararıdır.

## 2. Teknik kimlik

| Konu | Projedeki karşılığı |
|---|---|
| Unity sürümü | `6000.5.2f1` |
| Ana sahne | `Assets/Scenes/SampleScene.unity` |
| Kod dili | C# |
| Mimari | Bileşen tabanlı Unity mimarisi + olay (event) tabanlı iletişim |
| Fizik | `Rigidbody2D`, `CapsuleCollider2D`, `Physics2D` sorguları |
| Girdi | Unity Input System (`SoulSplitControls.inputactions`) |
| Görsel boru hattı | URP 2D, Sprite Renderer, parçacık efektleri |
| Arayüz | Unity UGUI (`Canvas`, `Text`, `Slider`, `Toggle`, `Button`) |
| Kayıt | `PlayerPrefs` üzerinden ayarlar ve checkpoint |
| Test | EditMode ve PlayMode Unity Test Framework |

Kullanılan başlıca paketler Input System, Universal Render Pipeline 2D, 2D Sprite/
Tilemap araçları, UGUI ve Unity Test Framework’tür. Önceki taşınabilirlik hatasına
sebep olan harici Git paket bağımlılığı artık `Packages/manifest.json` içinde yoktur.

## 3. Kod mimarisi: sorumluluklar nasıl ayrıldı?

Her script tek bir ana sorumluluğa sahiptir. Böylece hareketi değiştirmek için ses
kodunu, ses kodunu değiştirmek için düşman yapay zekâsını bozmak gerekmez.

| Sistem | Ana scriptler | Sorumluluk |
|---|---|---|
| Girdi | `PlayerInputHandler` | Klavye, fare ve gamepad girdilerini tek noktadan okumak |
| Beden hareketi | `PlayerController` | Koşma, zıplama, duvar, crouch ve takla |
| Ruh hareketi | `SoulController` | Yerçekimsiz 8 yönlü hareket |
| Form geçişi | `SoulSwitchManager` | Ruh enerjisi, kamera hedefi, beden/ruh aktivasyonu |
| Yakın dövüş | `MeleeAttack` | Saldırı zamanlaması, hitbox ve hasar çağrısı |
| Can | `Health` | Hasar, yanlış form vuruşu, i-frame, ölüm ve iyileşme |
| Düşman iskeleti | `EnemyBase` | Ortak durum makinesi, algılama, saldırı ve ölüm |
| Düşman türleri | `PhysicalEnemy`, `GhostEnemy` | Yerde devriye veya duvarlardan geçen süzülme |
| Ses | `GameAudioFeedback`, `EnemyAudioFeedback` | Olayları ses kliplerine bağlamak |
| Kayıt/ölüm | `CheckpointTrigger`, `ProgressionSave`, `PlayerDeathHandler` | İlerleme ve yeniden başlama |
| Bölüm sonu | `WinTrigger` | Tüm düşmanlar ölü mü kontrolü |
| Arayüz | `PlayerHealthUI`, `SoulMeterUI`, vb. | HUD, menüler, ipuçları ve ekranlar |

### Olay akışı

```text
Input System -> PlayerInputHandler
             -> PlayerController / SoulController
             -> Rigidbody2D + Physics2D

MeleeAttack -> Health.TryTakeDamage()
            -> OnHit / OnDeath / OnHealed event'leri
            -> UI, animasyon, kamera, parçacık ve ses
```

Girdi `Update` içinde okunur, fiziksel hareket `FixedUpdate` içinde uygulanır. Bu
ayrım, farklı kare hızlarında kontrolün tutarlı kalmasına yardım eder.

## 4. Unity sahnesi ve prefab düzeni

Oynanabilir dünya prefab merkezlidir. Kaynak prefablar şu klasörlerde tutulur:

- `Assets/_SoulSplit/Prefabs/Characters/` — oyuncu ve hazır düşman yerleşimleri
- `Assets/_SoulSplit/Prefabs/Enemies/` — fiziksel ve ruhani düşman prefabları
- `Assets/_SoulSplit/Prefabs/Environment/` — zemin, platform, duvar, tünel,
  checkpoint, kill zone ve bölüm sonu tetikleyicileri
- `Assets/_SoulSplit/Prefabs/Systems/` — kamera, ışık, ruh bağı ve oyun sistemleri
- `Assets/_SoulSplit/Prefabs/UI/` — oyun içi Canvas ve HUD

Harita `SampleScene` içinde bu prefabların örneklerinin yerleştirilmesiyle oluşur.
Bir platform veya düşman prefabı değiştirilirse o prefabı kullanan örnekler aynı
temel değişikliği alır. Seviye tasarımcısı Scene penceresinde örneğin konumunu,
ölçeğini ve izin verilen override alanlarını değiştirebilir. Yeni bir nesne türü için
önce prefab oluşturmak daha güvenli yöntemdir.

Kullanılan önemli fizik katmanları:

| Katman | Amaç |
|---|---|
| `Ground` | Zemin, duvar ve tavan kontrolleri |
| `Soul` | Ruhun fizik etkileşimi |
| `Body` | Oyuncunun fiziksel bedeni |
| `PhysicalEnemy` | Yerde hareket eden düşmanlar |
| `GhostEnemy` | Ruhani/duvarlardan geçen düşmanlar |

Ruhun duvardan geçmesi için her karede özel bir “duvardan geç” hilesi yazılmadı;
`Soul` ile `Ground` arasındaki Physics 2D katman çarpışma matrisi kullanıldı.

## 5. Oyuncu hareket algoritmaları

### 5.1 Akıcı yatay hareket

Karakterin yatay hızı doğrudan hedef hıza atanmıyor. `Mathf.MoveTowards` ile hedefe
ivmelenerek yaklaşılır:

```text
hedefHiz = yatayGirdi * maksimumHiz
oran = yerdeyse yerIvmesi / yerYavaslamasi
       havadaysa havaIvmesi / havaYavaslamasi
anlikHiz = MoveTowards(anlikHiz, hedefHiz, oran * fixedDeltaTime)
```

Mevcut temel değerler maksimum 11.5 birim/s, yerde 135 ivme ve 155 yavaşlama,
havada 78 ivme ve 42 yavaşlamadır. Oyun hızlı tepki verirken havada bir miktar
momentum korunur.

### 5.2 Coyote Time ve Jump Buffer

Oyuncu platformdan ayrıldıktan sonra `0.12` saniye daha zıplayabilir. Bu Coyote
Time’dır. Oyuncu yere inmeden önce zıplama tuşuna basarsa istek `0.15` saniye tutulur;
zemine değdiğinde tüketilir. Bu da Jump Buffer’dır.

```text
zemin temas ederse coyoteSayaci = 0.12
zıplama tuşuna basılırsa jumpBufferSayaci = 0.15
her kare iki sayaç deltaTime kadar azalır
uygun anda zıplama varsa ilgili sayaç sıfırlanır
```

### 5.3 Değişken zıplama yüksekliği

Zıplama tuşu erken bırakılırsa yukarı yönlü hız `jumpCutMultiplier = 0.45` ile
çarpılır. Tuş basılı tutulursa tam zıplama, erken bırakılırsa kısa zıplama yapılır.

### 5.4 Apex Hang, yerçekimi ve terminal hız

Zıplamanın tepe noktasında dikey hız `1.35` birim/s’nin altına indiğinde yerçekimi
`0.42` katsayısına düşürülür; yatay ivme `1.18` katsayısıyla güçlenir. Düşüşte
yerçekimi `1.8` katına, tuş bırakılmış kısa zıplamada `3.2` katına çıkar. Düşme hızı
`26` birim/s ile sınırlandırılır:

```text
eğer dikeyHiz < -maksimumDüşmeHızı:
    dikeyHiz = -maksimumDüşmeHızı
```

Bu değerler havada gereğinden fazla asılı kalmayı önlerken tepe noktasında yön
değiştirmek için okunabilir bir pencere bırakır.

### 5.5 Corner Correction

Karakter yükselirken tavanın yalnızca bir köşesine takılırsa iki kısa yukarı rayı
kontrol edilir. Sadece bir taraf kapalıysa karakter `0.18` birim açık tarafa
kaydırılır ve yukarı momentumu korunur. İki taraf da kapalıysa veya karakter
düşüyorsa düzeltme yapılmaz.

### 5.6 Duvar kayması ve duvar zıplaması

`OverlapBox` ile sağ ve sol duvar kontrol edilir. Oyuncu duvara doğru basıyor ve
düşüyorsa düşme hızı `3.5` birim/s ile sınırlandırılarak duvarda kayar. Duvar
zıplamasında duvarın tersi yönünde yaklaşık `(12, 16)` hız verilir. Temas karesi
kaçırılmış olsa bile `0.09` saniyelik wall-coyote penceresi vardır.

### 5.7 Shift ile takla / Ruh Adımı

Shift, aktif bedende kısa süreli yatay bir kaçınma başlatır: hız `18` birim/s, süre
`0.16` saniye, cooldown `0.75` saniye ve dokunulmazlık `0.20` saniyedir. Collider
yüksekliği crouch profiline düşer.

Takla başlarken `Body` ile `PhysicalEnemy` ve `GhostEnemy` katmanları arasındaki
çarpışma geçici kapatılır; süre bitince önceki çarpışma matrisi geri yüklenir. Bu
yüzden Shift sırasında düşmanın içinden geçilebilir. Görsel dönüşü
`PlayerProceduralAnimator`, `DodgeProgressNormalized` değeriyle tam tur oynatır.

### 5.8 Squash and Stretch

`PlayerProceduralAnimator` hareket hızına göre ölçek uygular. Yere basma ve inişte
Y ekseni sıkışır, havada Y ekseni uzar; X ekseni yaklaşık ters oranda ayarlanarak
hacim hissi korunur. Takla ve hasar tepkileri de bu görsel geri bildirime bağlıdır.

## 6. Girdi sistemi ve alternatif tuşlar

`SoulSplitControls.inputactions` içindeki `Player` action map:

| Eylem | Varsayılan klavye/fare | Gamepad |
|---|---|---|
| Move | WASD ve oklar | Sol analog |
| Jump | Space **ve W** | Güney düğmesi |
| Attack | Sol tık ve J | Batı düğmesi |
| HeavyAttack | Orta tık ve K | Doğu düğmesi |
| SoulSwitch | Sağ tık ve E | Kuzey düğmesi |
| Ultimate | Q | Sağ omuz |
| Dodge | Left Shift | Sol omuz |

W hem yukarı hareket bileşimi hem de ayrı Jump binding’i olarak kayıtlıdır. Bu
nedenle Space veya W kabul edilir. `PlayerInputHandler` basılma, basılı tutulma ve
bırakılma durumlarını ayrı ayrı okur; değişken zıplama bu ayrım üzerine kuruludur.

## 7. Ruh formu ve Soul Surge

`SoulSwitchManager` iki hareket scriptini birbirine karıştırmadan yönetir:

1. Bedende `body` aktif, `soul` pasiftir.
2. Form değiştirmede yeterli enerji kontrol edilir.
3. Beden bulunduğu yerde dondurulur; ruh aynı konumda etkinleştirilir.
4. Kamera ruhu takip eder ve aktif saldırı bileşeni ruhunki olur.
5. Ruh enerjisi zamanla azalır.
6. Beden–ruh mesafesi arttıkça tüketim 1–4 kat aralığında yükselir.
7. Enerji biterse ruh zorunlu olarak bedene döner ve kısa kilit uygulanır.

Ruh kapatıldığında oyuncu **her zaman bıraktığı fiziksel bedene döner**. Ruhun
konumunda beden oluşturma seçeneği devre dışıdır; mavi oval/bedenleşme önizlemesi
ve ona ait gereksiz fizik taraması varsayılan akışta çalışmaz. Ruh aktifken bedeni
gösteren bağ çizgisi (`SoulTether`) ayrı bir sistemdir.

Doğru hedefe başarılı vuruşlar combo ve ultimate enerjisi kazandırır. Hafif vuruş
14, ağır vuruş 24, öldürme ek olarak 12 puandır. Combo penceresi `2.25` saniyedir.
Enerji 100’e ulaştığında Q ile Soul Surge açılır; 7 saniye sürer, hareketi `1.25`
katına, hasarı `2` katına çıkarır ve ruh enerjisi tüketimini durdurur.

## 8. Savaş ve hasar sistemi

`MeleeAttack`, saldırı tuşunda hazırlık olayını yayınlar; gerçek hasar
`impactDelay` sonrasında uygulanır. Hitbox, `ContactFilter2D` ve layer mask ile
`Physics2D.OverlapBox` üzerinden taranır.

```text
Saldırı isteği -> cooldown uygunsa başlat, değilse attack buffer'a al
              -> hazırlık/impact gecikmesi
              -> OverlapBox hitbox taraması
              -> Health.TryTakeDamage(amount, damageType)
              -> Deflected / Ignored / Damaged / Killed
```

`Health.vulnerableTo` hedefin savunma alanını belirtir: `Physical`, `Spiritual` veya
`Both`. Yanlış formdan gelen saldırı `Deflected` olur, hasar uygulanmaz. Gerçek
hasardan sonra varsayılan `0.6` saniyelik i-frame vardır.

### Hasar sonrası %20 can yenileme

`NoHitKillRecovery` oyuncunun gerçek hasar olayını dinler. Oyuncu hasar aldıktan
sonra yeni hasar almadan doğru bir saldırıyla düşman öldürürse, maksimum canın
`%20`si `HealPercent(0.20f)` ile geri verilir. Yeni hasar bayrağı yeniden kurar;
can doluysa ek can üretilmez. Çukura düşme gibi `Health.Kill()` çevresel ölümdür ve
savaş vuruşu sayılmaz.

## 9. Düşman yapay zekâsı

`EnemyBase` sonlu durum makinesi kullanır:

```text
Patrol -> hedef algılanırsa Chase
Chase  -> saldırı menziline girerse Attack
Chase  -> hedef uzaklaşırsa Patrol
Attack -> hazırlık + vuruş + cooldown
Herhangi bir durum -> can sıfırsa Dead
```

`PhysicalEnemy` zeminde devriye gezer; önündeki zemin ve duvarı raycast ile kontrol
eder, uçurumdan yürümez. `GhostEnemy` yerçekimsiz süzülür ve katman matrisi sayesinde
duvarlardan geçer; ruh dışarıdaysa beden ile ruh arasından daha yakın olanı hedef
seçebilir.

Düşman saldırıya karar verdiği anda üzerinde büyük sarı `!` görünür. Hasara `0.25`
saniye kala renk kırmızıya döner ve ölçek titreşir. Oyuncunun vuruşu düşmanın saldırı
zamanlayıcısını iptal etmez; bu, vuruş donmasının bize vurmayı istemeden engellemesini
önler. Düşman devriye/dolaşma sesleri özellikle kaldırılmıştır; saldırı, hasar ve
ölüm gibi önemli anlar seslendirilir.

## 10. Ölüm, checkpoint ve bölüm bitişi

`KillZone` trigger collider’dır. Oyuncu veya ruhla ilişkili can havuzu alana girerse
`Health.Kill()` çağrılır; düşmek formdan bağımsız olarak öldürür.

`CheckpointTrigger`, sahne adı, checkpoint adı ve konumu `ProgressionSave` üzerinden
`PlayerPrefs`e yazar. Kayıt yalnızca ileri yönde güncellenir. Devam seçildiğinde
`PlayerDeathHandler`, aynı sahnedeki son konuma döner.

`WinTrigger` oyuncu alana girdiğinde veya alanda kaldığında kontrol eder.
`AllEnemiesDefeated()` aktif ve pasif bütün `EnemyBase` nesnelerini tarar ve her
birinin `Health.IsDead` değerini kontrol eder. Zamanlayıcı veya geri sayım yoktur;
son düşman gerçekten öldüyse ve oyuncu bitiş alanındaysa kazanma ekranı açılır.

## 11. Ses sistemi

Sesler olay tabanlı `GameAudioFeedback` ve `EnemyAudioFeedback` bileşenlerine ayrılmıştır.
`Assets/Resources/SoulSplitAudio` klasöründe şu anda **43 `.ogg` dosyası** bulunur:

- 13 oyuncu eylemi için temel klipler,
- her eylem için `1` ve `2` varyantları,
- fiziksel ve ruhani düşman saldırı/hasar/ölüm klipleri.

Oyuncu olayları zıplama, duvar zıplaması, hafif/ağır saldırı, hasar, sekme, ölüm,
ruhtan çıkış, bedene dönüş, Soul Surge, checkpoint, Ruh Adımı ve iyileşmedir.

İki varyantlı bir eylemde seçim rastgeledir:

```text
clipler = [Action1, Action2]
oynatılacak = clipler[Random.Range(0, clipler.Length)]
AudioSource.PlayOneShot(oynatılacak)
```

Klip bulunamazsa temel `Action` klibi, o da yoksa prosedürel kısa ses yedeği kullanılır.
Kenney kaynakları CC0 lisanslıdır; lisans notu
`Assets/Resources/SoulSplitAudio/Kenney_LICENSE.txt` içindedir. Düşman dolaşma
sesleri kaldırıldığı için ses tasarımı önemli oyun olaylarına odaklanır.

## 12. Animasyon, kamera ve arayüz

- `PlayerProceduralAnimator`: hız, zıplama, iniş squash/stretch ve takla dönüşü.
- `EnemyProceduralAnimator`: düşman hızına ve durumuna bağlı hareket.
- `HitFlash`, `PlayerHitReaction`, `EnemyImpactFX`: hasar rengi, knockback ve etki.
- `ParticleFX`, `SlashFX`: parçacıklar ve saldırı yayları.
- `CameraFollow`: aktif beden veya ruhu izleme, sarsıntı ve zoom punch.
- `PlayerHealthUI`, `SoulMeterUI`, `WorldHealthBar`: can, ruh enerjisi ve düşman HUD’ı.

Hit-stop ayarı artık oynanış seçeneği değildir. `HitStop.Trigger` düşman saldırılarının
zamanını dondurmamak için mevcut durmayı temizler; ayarlar ekranında aç/kapat seçeneği
bulunmaz. Ayarlar; ana ses, kamera efekt yoğunluğu, ipuçları, tam ekran ve yeniden
tuş atamayı içerir ve `PlayerPrefs`e kaydedilir.

## 13. Test yaklaşımı

EditMode ve PlayMode testlerinde özellikle şu senaryolar kapsanır: `Health` hasar ve
i-frame kuralları, checkpoint’in ileri yönde kaydı, Space/W zıplama binding’i,
takla collider’ı ve cooldown’ı, bütün düşmanlar ölmeden bölümün bitmemesi, ünlem
uyarısı, kill zone sınırları ve menü akışı. Projede toplam 44 `[Test]` veya
`[UnityTest]` işaretli test metodu bulunur. Son doğrulama için Unity Editor’da Play
Mode’da fizik, ses ve prefab görselleri de kontrol edilmelidir.

## 14. Olası sorulara kısa cevaplar

### “Neden hareket `Update` yerine `FixedUpdate` içinde uygulanıyor?”

`Rigidbody2D` fiziği sabit zaman adımında daha tutarlı çalışır. Girdi her görüntü
karesinde okunup tamponlanır, fizik adımında tüketilir.

### “Coyote Time ile Jump Buffer arasındaki fark nedir?”

Coyote Time zeminden ayrıldıktan sonraki kısa toleranstır; Jump Buffer yere basmadan
önceki zıplama isteğini saklar.

### “Ruh neden kendi konumunda bedene dönüşmüyor?”

Dönüş her zaman bırakılan fiziksel bedene yapılır. Böylece ruh formu sınırsız
ışınlanmaya dönüşmez ve bedenin korunması oyunun riskini oluşturur.

### “Fiziksel düşman ruh saldırısından neden etkilenmiyor?”

`DamageType` ile hedefin `vulnerableTo` değeri karşılaştırılır. Yanlış boyut
`Deflected` sonucu verir.

### “Saldırı neden tuşa bastığım anda hasar vermiyor?”

Hazırlık ve impact gecikmesi görsel savurma ile hasar karesini eşleştirir.

### “Ünlem ne zaman görünür?”

Düşman Attack durumuna girdiğinde görünür; hasara `0.25` saniye kala kırmızı ve
titreşimli olur.

### “Shift düşmanın içinden nasıl geçiyor?”

Taklada collider küçülür, i-frame verilir ve Body–Enemy katman çarpışmaları geçici
olarak kapatılır; süre bitince eski matris geri yüklenir.

### “Bölüm neden geri sayımla bitmiyor?”

Bölüm sonu sahnedeki aktif ve pasif tüm düşmanların `Health.IsDead` değerini tarar.
Zamanlayıcı veya tahmin kullanılmaz.

### “%20 can yenileme ne zaman çalışır?”

Oyuncu hasar aldıktan sonra başka hasar almadan bir düşmanı öldürürse maksimum canın
%20’si geri verilir.

### “Mavi ruh çevresi neden görünmüyor?”

Bedenleşme önizlemesi `showMaterializationPreview = false` durumundadır ve ayar
olarak sunulmaz. Ruhun dönüşü sabit fiziksel bedende gerçekleşir.

### “Map kodla mı yapılıyor?”

Oynanış kuralları kodla, zemin ve platform yerleşimi Unity Scene penceresinde prefab
örnekleriyle yapılır. Harita yalnızca kodla üretilmez.

### “Haritada özgürce nasıl değişiklik yaparım?”

`SampleScene`i açıp Hierarchy’de prefab örneğini seçin; Inspector’dan konum ve ölçeği
değiştirin. Kalıcı değişiklik için prefabı Prefab Mode’da düzenleyin. Yeni türler için
`Assets/_SoulSplit/Prefabs/Environment` altında prefab oluşturun.

### “Başka bilgisayarda proje neden açılmıyordu?”

Eski Git paket bağımlılığı diğer bilgisayarda `git.exe` bulunmadığı için çözülemiyordu.
Bağımlılık kaldırıldı; yeni cihazda yine de `6000.5.2f1` sürümüyle açıp paket ve asset
import işleminin bitmesini beklemek gerekir.

### “Seslerde neden iki varyant var?”

Aynı eylemin her tekrarda aynı örneği çalmasını önlemek için `Action1` ve `Action2`
arasında rastgele seçim yapılır.

## 15. Sunumda kullanılabilecek 30 saniyelik özet

“SoulSplit, Unity 6000.5.2f1 ile geliştirilmiş 2D aksiyon-platform oyunudur.
Oyuncunun fiziksel bedeni ve ruhu aynı can havuzunu paylaşır; `SoulSwitchManager`
form geçişini ve enerjiyi, `PlayerController` hızlı platform hareketini yönetir.
Akıcı hareket için coyote time, jump buffer, değişken zıplama, apex hang, terminal
velocity, corner correction, duvar zıplaması ve takla sırasında çarpışma geçişi
kullanılmıştır. Savaşta `MeleeAttack` hitbox’ı `Physics2D` ile tarar, `Health` hasar
türünü ve i-frame’i çözer. Düşmanlar Patrol–Chase–Attack–Dead durum makinesindedir ve
saldırıdan önce ünlemle telgraf verir. Bölüm bitişi sayaçla değil, bütün düşmanların
gerçekten ölü olup olmadığı taranarak belirlenir. Sesler olay tabanlıdır; önemli
eylemlerde iki varyant rastgele seçilir.”

## 16. Teknik sözlük

- **Prefab:** Bir nesnenin tekrar kullanılabilir Unity şablonu.
- **Rigidbody2D:** 2D fizik simülasyonuna katılan bileşen.
- **Collider2D:** Fiziksel şekil ve çarpışma sınırı.
- **Layer mask:** Physics2D sorgusunun hangi katmanları tarayacağını belirleyen bit maskesi.
- **FSM:** Bir nesnenin sınırlı durumlar ve geçişlerle yönetilmesi.
- **i-frame:** Hasar sonrası kısa dokunulmazlık.
- **Hitbox:** Saldırının etkili olduğu fiziksel sorgu alanı.
- **Coyote Time:** Zeminden ayrıldıktan sonraki kısa zıplama toleransı.
- **Jump Buffer:** Erken basılan zıplama isteğinin kısa süre saklanması.
- **Apex Hang:** Zıplama tepesinde yerçekiminin geçici azaltılması.
- **Terminal Velocity:** Düşme hızının üst sınırı.
- **Squash and Stretch:** Hız ve darbeyi ölçek değişimiyle hissettiren animasyon tekniği.
- **Event:** Bir olay olduğunda abone olan sistemlere haber veren C# mekanizması.
