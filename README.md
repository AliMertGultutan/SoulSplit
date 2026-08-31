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
| Soul Surge ultimate | Q | Sağ omuz tuşu |
| Ruh Adımı / kaçınma | Left Shift | Sol omuz tuşu |
| Duraklat / devam et | Esc | Start button |

Klavye kontrolleri ana menü veya duraklatma menüsündeki `Ayarlar > Klavye Tuşlarını Ayarla`
ekranından değiştirilebilir. Atamalar otomatik kaydedilir; çakışan tuşlar kabul edilmez.
Oyun içi yönlendirmeler de varsayılan tuşlar yerine oyuncunun etkin atamalarını gösterir.

## Oyun kuralları

- Beden fiziksel, ruh ise ruhani hasar verir.
- Ruhani düşmanlar bedene de saldırır; ruh dışarıdaysa beden veya ruhtan kendilerine daha yakın olanı hedefler.
- Ruh dışarıdayken enerji mesafeye bağlı olarak daha hızlı tükenir.
- Başarılı vuruşlar ve öldürmeler Soul Surge göstergesini doldurur. Ultimate aktifken ruh enerjisi tükenmez, hareket hızlanır ve saldırı hasarı artar.
- Cooldown bitmeden hemen önce basılan hafif veya ağır saldırı kısa süreliğine tamponlanır; erken basılan komut kaybolmadan sıradaki saldırıya dönüşür.
- Art arda başarılı vuruşlar `AKIŞ` zincirini yükseltir ve Soul Surge kazanımını kademeli olarak artırır; zincir HUD üzerinde açık metinle gösterilir.
- Beden ve ruh ortak can kullanır; beden savunmasız bırakılırsa oyuncu ölebilir.
- Beden formundaki `Ruh Adımı`, kısa bir yatay kaçınma ve dokunulmazlık penceresi sağlar; havada yere değene kadar yalnızca bir kez kullanılabilir.
- Ölüm anında karakter fiziği ve kontrolü durur. Ölüm ekranından son checkpoint'e dönülebilir, onay vererek yeni oyun başlatılabilir veya ana menüye çıkılabilir.
- Checkpoint tetikleri yeniden doğuş konumunu günceller.
- Ulaşılan en ileri checkpoint otomatik kaydedilir; ana menüden oyuna devam edilebilir veya onay vererek yeni oyun başlatılabilir.
- Ana menüden ve duraklatma menüsünden ortak Ayarlar ekranına ulaşılabilir.
- Ana ses, kamera efekti yoğunluğu, vuruş donması, oyun ipuçları ve tam ekran tercihleri otomatik kaydedilir.
- Duraklatma menüsündeki ruh dönüşü ayarıyla bedenin ruhun yanında mı yoksa bırakıldığı yerde mi kalacağı seçilebilir; tercih sonraki açılışlarda korunur.
- Ruh formundayken bedenin döneceği yer dünya üzerinde önizlenir; güvenli nokta, engelli nokta ve bedenin yerinde kalması renk ile birlikte açık metinle belirtilir.
- Sıçrama, saldırı, hasar, sekme ve ruh geçişleri çalışma anında üretilen kısa seslerle geri bildirim verir; harici ses varlığı gerekmez.

## Geliştirme

- Kod: `Assets/_SoulSplit/Scripts`
- Oyun içi sanat: `Assets/_SoulSplit/Art`
- Kaynak sanat: `ArtOriginals`
- Örnek sprite-sheet kodları: `Samples~/SpriteSheetReference`
- Animasyon çalışma notları: `ANIMATION_POLISH.md`

Unity tarafından üretilen `Library`, `Temp`, `Logs` ve IDE proje dosyaları repoya eklenmez. Yerel ekran görüntüleri `LocalCaptures`, kurtarma sahneleri `Recovery` altında tutulur ve Git tarafından izlenmez.

