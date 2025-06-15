#include <TimerOne.h>
#include "TrafikKontrol.h"

// Kütüphaneyi kullanma
TrafikKontrol trafikKontrol;

String inputString = "";      // Gelen verileri tutacak string
boolean stringComplete = false;  // String'in tamamen gelip gelmediğini kontrol etmek için

// Timer zaman aralığı (ms)
const unsigned long TIMER_SURESI = 3000; // 3 saniye

// Son trafik kontrolü zamanı
unsigned long sonKontrolZamani = 0;

// Şu anki aktif kavşak
int aktifKavsak = 0;

// Veri bekleme bayrağı - sadece belirli bir kavşak için veri işleme
boolean istemBekleniyor = false;
int istemBeklenenKavsak = -1;

// Potansiyometre için
const int POT_PIN = A0;  // Potansiyometrenin bağlı olduğu pin
unsigned long sonPotOkumaZamani = 0;
const unsigned long POT_OKUMA_ARALIGI = 100; // 100ms'de bir oku

void setup() {
  // 9600 baud rate ile seri haberleşmeyi başlat
  Serial.begin(9600);
  
  // Potansiyometre pini girişe ayarla
  pinMode(POT_PIN, INPUT);
  
  // Gelen veri string'ini temizle
  inputString.reserve(200);
  
  // TrafikKontrol kütüphanesini başlat
  trafikKontrol.begin();
  
  // Algoritma ağırlık parametrelerini ayarla (α = 1.0, β = 0.5)
  trafikKontrol.parametreleriAyarla(1.0, 0.5);
  
  // Timer1'i başlat
  Timer1.initialize(TIMER_SURESI * 1000); // mikrosaniye cinsinden
  Timer1.attachInterrupt(timerCallback);
  
  sonKontrolZamani = millis();
  sonPotOkumaZamani = millis();
}

// Timer callback fonksiyonu
void timerCallback() {
  // Eğer bir istem bekliyorsak ve ilgili kavşak için
  if (istemBekleniyor && istemBeklenenKavsak >= 0 && istemBeklenenKavsak < 4) {
    // Kütüphane fonksiyonunu kullanarak kavşağı güncelle
    trafikKontrol.kavsagiGuncelle(istemBeklenenKavsak);
    
    // Yeşil yanan yönü al
    int yesilYon = trafikKontrol.yesilIsikYonunuGetir(istemBeklenenKavsak);
    
    // C#'a bilgi gönder (Kavşak ve yeşil ışık yönü)
    Serial.print("Y,");
    Serial.print(istemBeklenenKavsak + 1); // Kavşak numarası (1'den başlıyor)
    Serial.print(",");
    Serial.print(yesilYon); // Yeşil ışık yönü (0-3)
    Serial.println("#");
    
    // İstek işlendi, bayrakları temizle
    istemBekleniyor = false;
    istemBeklenenKavsak = -1;
    return;
  }
  
  // Normal zamanlayıcı çalışması - otomatik kontrol
  // Yeni algoritma kullanarak kavşağı güncelle
  bool degisiklikOldu = trafikKontrol.kavsagiGuncelle(aktifKavsak);
  
  // Eğer yeşil ışık değiştiyse bilgi gönder
  if (degisiklikOldu) {
    // Yeşil yanan yönü al
    int yesilYon = trafikKontrol.yesilIsikYonunuGetir(aktifKavsak);
    
    // C#'a bilgi gönder (Kavşak ve yeşil ışık yönü)
    Serial.print("Y,");
    Serial.print(aktifKavsak + 1); // Kavşak numarası (1'den başlıyor)
    Serial.print(",");
    Serial.print(yesilYon); // Yeşil ışık yönü (0-3)
    Serial.println("#");
    
    // Debug bilgisi - hangi yönün neden seçildiğini anlatır
    Serial.print("Debug: Kavşak ");
    Serial.print(aktifKavsak + 1);
    Serial.print(", Yön ");
    Serial.print(yesilYon);
    Serial.print(" seçildi. Skor: ");
    
    // Skorları yazdır (yoğunluk ve bekleme süresi etkileri görmek için)
    for (int yon = 0; yon < 4; yon++) {
      float skor = trafikKontrol.skorHesapla(aktifKavsak, yon);
      Serial.print("Yön ");
      Serial.print(yon);
      Serial.print("=");
      Serial.print(skor, 2);
      Serial.print(" ");
    }
    Serial.println("#");
  }
  
  // Sıradaki kavşağa geç
  aktifKavsak = (aktifKavsak + 1) % 4;
}

void loop() {
  // Seri haberleşmeden gelen veriyi işle
  if (stringComplete) {
    // Veriyi işle
    parseTrafficData(inputString);
    
    // Yeni veri almak için değişkenleri sıfırla
    inputString = "";
    stringComplete = false;
  }
  
  // Potansiyometre kontrolü - belirli aralıklarla oku
  if (trafikKontrol.potansiyometreKontrolDurumu() && millis() - sonPotOkumaZamani >= POT_OKUMA_ARALIGI) {
    // Potansiyometre değerini oku ve 0-1023 arası değer döndür
    int potDegeri = trafikKontrol.potansiyometreOku(POT_PIN);
    
    // Potansiyometre değerini C#'a gönder
    Serial.print(potDegeri);
    Serial.println("#");
    
    // Zamanlayıcıyı güncelle
    sonPotOkumaZamani = millis();
    
    // Hızlı tepki için timerCallback'i hemen tetikle
    timerCallback();
  }
}

// Seri porttan veri geldiğinde çağrılır
void serialEvent() {
  while (Serial.available()) {
    char inChar = (char)Serial.read();
    
    // # karakteri veri paketinin sonunu belirtir
    if (inChar == '#') {
      stringComplete = true;
      break;
    } else {
      // Veriyi biriktir
      inputString += inChar;
    }
  }
}

// C# tarafından gelen veri formatlarını işle
void parseTrafficData(String data) {
  // Parametre ayarlama komutu: "A,1.0,0.5#" (Yoğunluk, BeklemeSüresi ağırlıkları)
  if (data.startsWith("A,")) {
    int firstComma = data.indexOf(',');
    int secondComma = data.indexOf(',', firstComma + 1);
    
    if (firstComma != -1 && secondComma != -1) {
      // String'ten float'a dönüştür
      float yogunlukAgirligi = data.substring(firstComma + 1, secondComma).toFloat();
      float beklemeSuresiAgirligi = data.substring(secondComma + 1).toFloat();
      
      // Kütüphane fonksiyonunu kullanarak parametreleri ayarla
      trafikKontrol.parametreleriAyarla(yogunlukAgirligi, beklemeSuresiAgirligi);
      
      // Onay mesajını gönder
      Serial.print("Parametreler ayarlandı: α=");
      Serial.print(yogunlukAgirligi);
      Serial.print(", β=");
      Serial.print(beklemeSuresiAgirligi);
      Serial.println("#");
    }
    return;
  }
  
  // Potansiyometre seçim komutu: "P,1,2#" (1. kavşağın 2. yönü)
  if (data.startsWith("P,")) {
    int firstComma = data.indexOf(',');
    int secondComma = data.indexOf(',', firstComma + 1);
    
    if (firstComma != -1 && secondComma != -1) {
      int kavsak = data.substring(firstComma + 1, secondComma).toInt();
      int yon = data.substring(secondComma + 1).toInt();
      
      if (kavsak >= 1 && kavsak <= 4 && yon >= 0 && yon <= 3) {
        // Kütüphane fonksiyonunu kullanarak potansiyometre kontrolü ayarla
        trafikKontrol.potansiyometreKontrolAyarla(kavsak - 1, yon); // 0-tabanlı indeks
        
        // Onay mesajını gönder
        Serial.print("Secili kavşak: ");
        Serial.print(kavsak);
        Serial.print(", yön: ");
        Serial.print(yon);
        Serial.println("#");
      }
    }
    return;
  }
  
  // Potansiyometreyi devre dışı bırakma komutu: "PX#"
  if (data.startsWith("PX")) {
    // Kütüphane fonksiyonuyla potansiyometre kontrolünü kapat
    trafikKontrol.potansiyometreKontrolKapat();
    Serial.println("Potansiyometre devre dışı bırakıldı#");
    return;
  }
  
  // C#'tan gelen interrupt sinyali ("R,1#" gibi, 1 kavşak numarası)
  if (data.startsWith("R,")) {
    int kavsak = data.substring(2).toInt();
    if (kavsak >= 1 && kavsak <= 4) {
      // Bu kavşak için trafik kontrolünü aktifleştir
      istemBekleniyor = true;  // İstem bekleniyor bayrağını set et
      istemBeklenenKavsak = kavsak - 1;  // 0-tabanlı dizin için -1
    }
    return;
  }
  
  // Normal trafik yoğunluk verisi ("Kavşak,Yön,Değer")
  int firstComma = data.indexOf(',');
  if (firstComma == -1) return;
  
  int secondComma = data.indexOf(',', firstComma + 1);
  if (secondComma == -1) return;
  
  // Kavşak, yön ve değeri çıkart
  int kavsak = data.substring(0, firstComma).toInt();
  int yon = data.substring(firstComma + 1, secondComma).toInt();
  int deger = data.substring(secondComma + 1).toInt();
  
  // Dizi indeksleri 0'dan başlar, kavşak numarası 1'den başlıyor
  if (kavsak >= 1 && kavsak <= 4 && yon >= 0 && yon <= 3) {
    // Kütüphane fonksiyonunu kullanarak trafik yoğunluğunu güncelle
    trafikKontrol.trafikYogunluguGuncelle(kavsak - 1, yon, deger);
    
    // Eğer bu beklediğimiz kavşak için son veri ise işlem yap
    if (istemBekleniyor && istemBeklenenKavsak == kavsak - 1) {
      // Tüm yönlerin verisi gelince timer'ı tetikle
      timerCallback();
    }
  }
}
