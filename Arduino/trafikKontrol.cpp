/*
  TrafikKontrol.cpp - Akıllı trafik ışıkları kontrol kütüphanesi
  Bu kütüphane, Arduino ile akıllı trafik ışıkları simülasyonu için
  trafik yoğunluğu algılama ve karar verme fonksiyonları içerir.
*/

#include "Arduino.h"
#include "TrafikKontrol.h"

TrafikKontrol::TrafikKontrol() {
  _minimumYogunlukEsigi = 5;
  _potKavsak = -1;
  _potYon = -1;
  _potAktif = false;
  _kontrolSikligi = 3;
  
  // Yeni algoritma parametreleri
  _donguSuresi = 3000;  // 3 saniye (milisaniye cinsinden)
  _yogunlukAgirligi = 1.0;  // α
  _beklemeSuresiAgirligi = 0.5; // β
  _maxBeklemeSuresi = 60000; // 60 saniye
}

void TrafikKontrol::begin() {
  // Tüm kavşakları sıfırla
  for (int i = 0; i < 4; i++) {
    for (int j = 0; j < 4; j++) {
      _kavsaklar[i].deger[j] = 0;
      _kavsaklar[i].beklemeSuresi[j] = 0;
      _kavsaklar[i].skor[j] = 0.0;
    }
    _kavsaklar[i].yesilIsikYonu = -1; // Başlangıçta hiçbir yön yeşil değil
    _kavsaklar[i].kontrolAktif = true;
    _kavsaklar[i].kontrolSayaci = 0;
  }
}

void TrafikKontrol::trafikYogunluguGuncelle(int kavsak, int yon, int yogunluk) {
  // Geçerli parametre kontrolü
  if (kavsak < 0 || kavsak > 3 || yon < 0 || yon > 3) {
    return;
  }
  
  // Potansiyometre ile seçili değilse güncelle
  if (!(_potAktif && kavsak == _potKavsak && yon == _potYon)) {
    // Yoğunluğu 0-100 arasında sınırla
    _kavsaklar[kavsak].deger[yon] = constrain(yogunluk, 0, 100);
    
    // Yeni veri geldi, kontrol aktif olsun
    _kavsaklar[kavsak].kontrolAktif = true;
    _kavsaklar[kavsak].kontrolSayaci = 0;
  }
}

// Eski algoritma - sadece yoğunluğa bakarak karar verir
int TrafikKontrol::enYogunYonuBul(int kavsak) {
  // Geçerli parametre kontrolü
  if (kavsak < 0 || kavsak > 3) {
    return -1;
  }
  
  int enYogunYon = 0;
  int enYuksekYogunluk = _kavsaklar[kavsak].deger[0];
  
  // En yoğun yönü bul
  for (int yon = 1; yon < 4; yon++) {
    if (_kavsaklar[kavsak].deger[yon] > enYuksekYogunluk) {
      enYogunYon = yon;
      enYuksekYogunluk = _kavsaklar[kavsak].deger[yon];
    }
  }
  
  // Minimum yoğunluk eşiği kontrolü
  if (enYuksekYogunluk < _minimumYogunlukEsigi) {
    // Eğer tüm yönler çok düşük yoğunluktaysa en son yeşil yananın devam etmesini sağla
    if (_kavsaklar[kavsak].yesilIsikYonu >= 0) {
      return _kavsaklar[kavsak].yesilIsikYonu;
    }
  }
  
  return enYogunYon;
}

// Yeni algoritma - skor hesaplayarak karar verir
float TrafikKontrol::skorHesapla(int kavsak, int yon) {
  // Geçerli parametre kontrolü
  if (kavsak < 0 || kavsak > 3 || yon < 0 || yon > 3) {
    return -1.0;
  }
  
  // Yoğunluk zaten 0-100 arasında 
  float yogunluk = (float)_kavsaklar[kavsak].deger[yon];
  
  // Bekleme süresini saniyeye çevir ve normalize et (0-100 arası)
  float beklemeSuresi = (float)_kavsaklar[kavsak].beklemeSuresi[yon] / 1000.0;  // ms -> s
  float normalizedBeklemeSuresi = (beklemeSuresi / (_maxBeklemeSuresi / 1000.0)) * 100.0;
  normalizedBeklemeSuresi = constrain(normalizedBeklemeSuresi, 0.0, 100.0);
  
  // Skor = α * yoğunluk + β * beklemeSüresi
  float skor = _yogunlukAgirligi * yogunluk + _beklemeSuresiAgirligi * normalizedBeklemeSuresi;
  
  // Skoru kaydet
  _kavsaklar[kavsak].skor[yon] = skor;
  
  return skor;
}

int TrafikKontrol::enYuksekSkorluYonuBul(int kavsak) {
  // Geçerli parametre kontrolü
  if (kavsak < 0 || kavsak > 3) {
    return -1;
  }
  
  // Her yön için skor hesapla
  for (int yon = 0; yon < 4; yon++) {
    skorHesapla(kavsak, yon);
  }
  
  // En yüksek skorlu yönü bul
  int enYuksekSkorYonu = 0;
  float enYuksekSkor = _kavsaklar[kavsak].skor[0];
  
  for (int yon = 1; yon < 4; yon++) {
    if (_kavsaklar[kavsak].skor[yon] > enYuksekSkor) {
      enYuksekSkorYonu = yon;
      enYuksekSkor = _kavsaklar[kavsak].skor[yon];
    }
  }
  
  return enYuksekSkorYonu;
}

void TrafikKontrol::beklemeSureleriniGuncelle(int kavsak, int yesilYon) {
  // Geçerli parametre kontrolü
  if (kavsak < 0 || kavsak > 3 || yesilYon < 0 || yesilYon > 3) {
    return;
  }
  
  // Yeşil olan yönün bekleme süresini sıfırla
  _kavsaklar[kavsak].beklemeSuresi[yesilYon] = 0;
  
  // Diğer yönlerin bekleme sürelerini artır
  for (int yon = 0; yon < 4; yon++) {
    if (yon != yesilYon) {
      _kavsaklar[kavsak].beklemeSuresi[yon] += _donguSuresi;
    }
  }
}

bool TrafikKontrol::kavsagiGuncelle(int kavsak) {
  // Geçerli parametre kontrolü
  if (kavsak < 0 || kavsak > 3) {
    return false;
  }
  
  // Bu kavşak için kontrol edilmeli mi veya zorunlu kontrol zamanı mı?
  if (_kavsaklar[kavsak].kontrolAktif || _kavsaklar[kavsak].kontrolSayaci >= _kontrolSikligi) {
    // Yeni algoritma kullan - en yüksek skorlu yönü bul
    int yeniYesilYon = enYuksekSkorluYonuBul(kavsak);
    
    // Yeşil ışık değişmiş mi veya zorunlu kontrol zamanı mı?
    if (yeniYesilYon != _kavsaklar[kavsak].yesilIsikYonu || _kavsaklar[kavsak].kontrolSayaci >= _kontrolSikligi) {
      // Yeşil ışık yönünü güncelle
      _kavsaklar[kavsak].yesilIsikYonu = yeniYesilYon;
      _kavsaklar[kavsak].kontrolSayaci = 0;
      _kavsaklar[kavsak].kontrolAktif = false;
      
      // Bekleme sürelerini güncelle
      beklemeSureleriniGuncelle(kavsak, yeniYesilYon);
      
      return true; // Değişiklik oldu
    } else {
      // Değişiklik olmadı, sayacı artır
      _kavsaklar[kavsak].kontrolSayaci++;
    }
  } else {
    // Kontrol edilmesine gerek yok, sadece sayacı artır
    _kavsaklar[kavsak].kontrolSayaci++;
  }
  
  return false; // Değişiklik olmadı
}

int TrafikKontrol::yesilIsikYonunuGetir(int kavsak) {
  // Geçerli parametre kontrolü
  if (kavsak < 0 || kavsak > 3) {
    return -1;
  }
  
  return _kavsaklar[kavsak].yesilIsikYonu;
}

int TrafikKontrol::potansiyometreOku(int pin) {
  // Potansiyometre aktif ve seçili kavşak/yön geçerli mi?
  if (_potAktif && _potKavsak >= 0 && _potKavsak < 4 && _potYon >= 0 && _potYon < 4) {
    // Analog değeri oku (0-1023)
    int deger = analogRead(pin);
    
    // 0-100 aralığına dönüştür
    int yogunluk = map(deger, 0, 1023, 0, 100);
    
    // Yoğunluğu güncelle
    _kavsaklar[_potKavsak].deger[_potYon] = yogunluk;
    
    // Kontrolü aktifleştir
    _kavsaklar[_potKavsak].kontrolAktif = true;
    _kavsaklar[_potKavsak].kontrolSayaci = 0;
    
    return deger; // potDegeri değerini döndür
  }
  
  return -1; // Potansiyometre aktif değil veya geçersiz ayarlar
}

int TrafikKontrol::toplamYogunlukGetir() {
  int toplam = 0;
  
  // Tüm kavşaklar ve yönler için yoğunlukları topla
  for (int i = 0; i < 4; i++) {
    for (int j = 0; j < 4; j++) {
      toplam += _kavsaklar[i].deger[j];
    }
  }
  
  return toplam;
}

void TrafikKontrol::potansiyometreKontrolAyarla(int kavsak, int yon) {
  // Geçerli parametre kontrolü
  if (kavsak < 0 || kavsak > 3 || yon < 0 || yon > 3) {
    return;
  }
  
  _potKavsak = kavsak;
  _potYon = yon;
  _potAktif = true;
}

void TrafikKontrol::potansiyometreKontrolKapat() {
  _potAktif = false;
  _potKavsak = -1;
  _potYon = -1;
}

bool TrafikKontrol::potansiyometreKontrolDurumu() {
  return _potAktif;
}

void TrafikKontrol::parametreleriAyarla(float yogunlukAgirligi, float beklemeSuresiAgirligi) {
  _yogunlukAgirligi = yogunlukAgirligi;
  _beklemeSuresiAgirligi = beklemeSuresiAgirligi;
}