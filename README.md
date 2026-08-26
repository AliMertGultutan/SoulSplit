# SoulSplit

SoulSplit, oyuncunun fiziksel bedeni ile ruh formu arasında geçiş yaptığı 2D bir Unity aksiyon-platform prototipidir. İki form aynı can havuzunu paylaşır; fiziksel ve ruhani düşmanlar yalnızca doğru hasar türünden etkilenir.

## Gereksinimler

- Unity `6000.5.2f1`
- Universal Render Pipeline 2D
- Unity Input System

Projeyi Unity Hub üzerinden açın. Başlangıç sahnesi `Assets/_SoulSplit/Scenes/MainMenu.unity`, oynanış sahnesi `Assets/Scenes/SampleScene.unity` dosyasıdır; ikisi de Build Settings'e eklenmiştir.

## Kontroller

| Eylem | Klavye / Fare | Gamepad |
| --- | --- | --- |
| Hareket | WASD veya ok tuşları | Sol analog |
| Zıplama | Space | South button |
| Hafif saldırı | Sol tık veya J | West button |
| Ağır saldırı | Orta tık veya K | East button |
| Ruh değişimi | Sağ tık veya E | North button |
| Duraklat / devam et | Esc | Start button |

## Oyun kuralları

- Beden fiziksel, ruh ise ruhani hasar verir.
- Ruhani düşmanlar bedene de saldırır; ruh dışarıdaysa beden veya ruhtan kendilerine daha yakın olanı hedefler.
- Ruh dışarıdayken enerji mesafeye bağlı olarak daha hızlı tükenir.
- Beden ve ruh ortak can kullanır; beden savunmasız bırakılırsa oyuncu ölebilir.
- Checkpoint tetikleri yeniden doğuş konumunu günceller.
- Ulaşılan en ileri checkpoint otomatik kaydedilir; ana menüden oyuna devam edilebilir veya onay vererek yeni oyun başlatılabilir.
- Duraklatma menüsünden oyuna dönülebilir, bölüm yeniden başlatılabilir, ana menüye dönülebilir ve ana ses seviyesi ayarlanabilir.
- Duraklatma menüsündeki ruh dönüşü ayarıyla bedenin ruhun yanında mı yoksa bırakıldığı yerde mi kalacağı seçilebilir; tercih sonraki açılışlarda korunur.
- Sıçrama, saldırı, hasar, sekme ve ruh geçişleri çalışma anında üretilen kısa seslerle geri bildirim verir; harici ses varlığı gerekmez.

## Geliştirme

- Kod: `Assets/_SoulSplit/Scripts`
- Oyun içi sanat: `Assets/_SoulSplit/Art`
- Kaynak sanat: `ArtOriginals`
- Örnek sprite-sheet kodları: `Samples~/SpriteSheetReference`
- Animasyon çalışma notları: `ANIMATION_POLISH.md`

Unity tarafından üretilen `Library`, `Temp`, `Logs` ve IDE proje dosyaları repoya eklenmez. Yerel ekran görüntüleri `LocalCaptures`, kurtarma sahneleri `Recovery` altında tutulur ve Git tarafından izlenmez.

## Doğrulama

Unity Test Runner'da EditMode ve PlayMode testlerini çalıştırın. Dağıtım öncesinde ayrıca MainMenu → SampleScene geçişini, iki formun saldırı ayrımını, checkpoint/respawn döngüsünü ve kazanma ekranını Play Mode'da kontrol edin.

## Üçüncü taraf varlıklar

CraftPix kaynaklı sprite paketlerinin lisans metinleri ilgili sanat klasörlerinde tutulur. Yeni dış kaynak eklerken lisans dosyasını varlıkla aynı klasöre koyun ve kaynağı `THIRD_PARTY_NOTICES.md` içinde kaydedin.
