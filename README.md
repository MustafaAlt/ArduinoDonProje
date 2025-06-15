# ArduinoDonemProje
Akıllı Trafik Yönetim Sistemi
Bu proje, Arduino tabanlı akıllı trafik ışığı kontrol sistemi için geliştirilmiş bir Windows Forms uygulamasıdır. Sistem, 4 kavşaktan oluşan bir trafik ağında gerçek zamanlı trafik yoğunluğu izleme ve adaptif trafik ışığı kontrolü sağlar.Projede C# form ile bir trafik ortamı simüle edilip. Rastegele olarak belirlenen Trafik yoğunlukları arduino üzerine gönderilmiştir.Serş port üzerinden aktarılan verilere bakarak karar veren arduino bir merkez sunucu gibi çalışmaktadır. Yeşil yanacak yönün kararını belirleyip her kavşağa ayrı olacak şekilde bilgilendirmektedir. Karar verme süreci Skor değerleri ile oluşmaktadır. 
🚦 Özellikler
Ana Özellikler

4 Kavşak Yönetimi: Her kavşakta 4 yön (Kuzey, Doğu, Güney, Batı) için trafik kontrolü
Gerçek Zamanlı Trafik Yoğunluğu İzleme: Her yön için %0-100 arası yoğunluk gösterimi
Arduino ile Seri Haberleşme: RS232/USB üzerinden Arduino kontrolü
Adaptif Trafik Işığı Kontrolü: Trafik yoğunluğuna göre otomatik ışık ayarlama
Potansiyometre Entegrasyonu: Manuel trafik yoğunluğu kontrolü
Grafik Arayüz: Kavşakları ve yolları görsel olarak gösteren interaktif harita

🛠️ Teknoloji Stack
Ana Teknolojiler

C# .NET Framework: Ana programlama dili ve platform
Windows Forms: Kullanıcı arayüzü framework'ü
System.IO.Ports: Arduino ile seri haberleşme
GDI+: Grafik çizim ve görselleştirme


![image](https://github.com/user-attachments/assets/d40554ad-a8aa-43a9-897a-d0794b99158d)
![image](https://github.com/user-attachments/assets/755fc825-236d-4953-b143-e435d44d5696)

