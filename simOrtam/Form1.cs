using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Deneme3
{
    public partial class Form1 : Form
    {
        List<Araba> arabalar = new List<Araba>();
        Random rnd = new Random();
        Timer arabaHareketTimer = new Timer();
        Timer timerA = new Timer();
        Timer timerB = new Timer();
        Timer timerC = new Timer();
        Timer timerD = new Timer();
        Timer trafikAzaltmaTimer = new Timer(); // Yeşil ışık yanan yöndeki trafiği azaltmak için
        List<Label> trafikIsiklari = new List<Label>();

        // Trafik yoğunluğu göstergeleri
        List<TrafikYogunlugu> trafikYogunluklari = new List<TrafikYogunlugu>();
        int seciliYogunlukIndex = -1; // Seçili olan yoğunluk göstergesi

        // Ölçeklendirme değişkenleri
        private float olcekX = 1.0f;
        private float olcekY = 1.0f;
        private const int referansGenislik = 1280;
        private const int referansYukseklik = 960;

        // Serial Monitor için
        private Panel pnlSerialMonitor;
        private ListBox lstSerialMessages;
        private Queue<string> serialMessages = new Queue<string>();
        private const int MAX_MESSAGES = 15; // Maksimum mesaj sayısı
        private bool showSerialMonitor = true; // Monitörü gösterme/gizleme durumu

        // İstek-yanıt protokolü için
        private int aktifKavsak = 0; // Aktif olan kavşak
        private bool veriBekleniyor = false; // Veri gönderdikten sonra yanıt bekleniyor mu?
        private DateTime sonVeriGondermeZamani;
        private const int VERI_GONDERME_ZAMAN_ASIMI = 2000; // 2 saniye

        // Potansiyometre kontrolü için
        private Button btnPotAktif;  // Potansiyometre aktifleştirme butonu
        private Label lblPotDurum;  // Potansiyometre durum etiketi
        private bool potAktif = false;  // Potansiyometre aktif mi

        public Form1()
        {
            InitializeComponent();
            this.Width = 960;
            this.Height = 960;
            this.DoubleBuffered = true;

            // Ölçeklendirme faktörlerini hesapla
            HesaplaOlcek();

            // Form yeniden boyutlandırıldığında olayı ekle
            this.Resize += Form1_Resize;

            // Serial Monitor panelini oluştur
            CreateSerialMonitorPanel();

            InitTrafikIsiklari(); // Burası burada kalmalı
            InitTrafikYogunluklari(); // Trafik yoğunluğu göstergelerini oluştur

            // Trafik yoğunluğu değerlerini rastgele belirle
            RandomTrafikYogunlugu();

            // Form'a tıklama olayı ekleyin
            this.MouseClick += Form1_MouseClick;

            // Potansiyometre kontrollerini oluştur
            InitPotansiyometreKontrolu();
        }

        private void InitPotansiyometreKontrolu()
        {
            // Potansiyometre kontrolü için buton oluştur
            btnPotAktif = new Button();
            btnPotAktif.Text = "Potansiyometre Aktif Et";
            btnPotAktif.Size = new Size(150, 30);
            btnPotAktif.Location = new Point(10, cmbPorts.Bottom + 10);
            btnPotAktif.Click += BtnPotAktif_Click;
            this.Controls.Add(btnPotAktif);

            // Potansiyometre durum etiketi
            lblPotDurum = new Label();
            lblPotDurum.Text = "Potansiyometre: Devre Dışı";
            lblPotDurum.AutoSize = true;
            lblPotDurum.Location = new Point(btnPotAktif.Right + 10, btnPotAktif.Top + 5);
            lblPotDurum.ForeColor = Color.Red;
            this.Controls.Add(lblPotDurum);
        }

        private void BtnPotAktif_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show("Önce port bağlantısını açın!");
                return;
            }

            if (seciliYogunlukIndex == -1)
            {
                MessageBox.Show("Önce bir trafik yoğunluğu göstergesine tıklayın!");
                return;
            }

            if (!potAktif)
            {
                // Potansiyometreyi aktifleştir
                int kavsak = (seciliYogunlukIndex / 4) + 1;  // Kavşak numarası (1-4)
                int yon = seciliYogunlukIndex % 4;          // Yön (0-3)

                Task.Run(() => {
                    try
                    {
                        // P,kavşak,yön# formatında komut gönder
                        string komut = $"P,{kavsak},{yon}#";
                        serialPort1.Write(komut);

                        this.BeginInvoke(new Action(() => {
                            AddSerialMessage(komut, true);
                            btnPotAktif.Text = "Potansiyometre Kapat";
                            lblPotDurum.Text = $"Potansiyometre: Kavşak {kavsak}, Yön {GetYonAdi(yon)}";
                            lblPotDurum.ForeColor = Color.Green;
                            potAktif = true;
                            lblStatus.Text = $"Potansiyometre kavşak {kavsak}, {GetYonAdi(yon)} yönüne bağlandı";
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() => {
                            AddSerialMessage("HATA: " + ex.Message, false);
                        }));
                    }
                });
            }
            else
            {
                // Potansiyometreyi devre dışı bırak
                Task.Run(() => {
                    try
                    {
                        // PX# komutu gönder
                        string komut = "PX#";
                        serialPort1.Write(komut);

                        this.BeginInvoke(new Action(() => {
                            AddSerialMessage(komut, true);
                            btnPotAktif.Text = "Potansiyometre Aktif Et";
                            lblPotDurum.Text = "Potansiyometre: Devre Dışı";
                            lblPotDurum.ForeColor = Color.Red;
                            potAktif = false;
                            lblStatus.Text = "Potansiyometre devre dışı bırakıldı";
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() => {
                            AddSerialMessage("HATA: " + ex.Message, false);
                        }));
                    }
                });
            }
        }

        private void CreateSerialMonitorPanel()
        {
            // Panel oluştur
            pnlSerialMonitor = new Panel();
            pnlSerialMonitor.Size = new Size(300, 200);
            pnlSerialMonitor.Location = new Point(this.ClientSize.Width - 310, 10); // Sağ üst köşe
            pnlSerialMonitor.BackColor = Color.FromArgb(100, 0, 0, 0); // Yarı saydam siyah
            pnlSerialMonitor.BorderStyle = BorderStyle.FixedSingle;

            // Panel başlığı
            Label lblTitle = new Label();
            lblTitle.Text = "Serial Monitor (S tuşu ile gizle/göster)";
            lblTitle.Size = new Size(pnlSerialMonitor.Width, 20);
            lblTitle.Location = new Point(0, 0);
            lblTitle.BackColor = Color.FromArgb(150, 0, 0, 0);
            lblTitle.ForeColor = Color.White;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;

            // ListBox oluştur
            lstSerialMessages = new ListBox();
            lstSerialMessages.Size = new Size(pnlSerialMonitor.Width - 10, pnlSerialMonitor.Height - 30);
            lstSerialMessages.Location = new Point(5, 25);
            lstSerialMessages.BackColor = Color.Black;
            lstSerialMessages.ForeColor = Color.Lime;
            lstSerialMessages.BorderStyle = BorderStyle.None;
            lstSerialMessages.Font = new Font("Consolas", 8);
            lstSerialMessages.IntegralHeight = false;

            // Panel'e ekle
            pnlSerialMonitor.Controls.Add(lblTitle);
            pnlSerialMonitor.Controls.Add(lstSerialMessages);

            // Form'a ekle
            this.Controls.Add(pnlSerialMonitor);
            pnlSerialMonitor.BringToFront();

            // Klavye olayını ekle
            this.KeyPress += Form1_KeyPress;
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            // 'S' tuşuna basıldığında serial monitörü gizle/göster
            if (e.KeyChar == 's' || e.KeyChar == 'S')
            {
                showSerialMonitor = !showSerialMonitor;
                pnlSerialMonitor.Visible = showSerialMonitor;
            }
        }

        private void AddSerialMessage(string message, bool isSent = false)
        {
            string timeStamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string prefix = isSent ? "→: " : "←: ";
            string fullMessage = $"[{timeStamp}] {prefix}{message}";

            // Kuyruğa yeni mesaj ekle
            serialMessages.Enqueue(fullMessage);

            // Maksimum mesaj sayısını aşıyorsa, eski mesajları çıkar
            while (serialMessages.Count > MAX_MESSAGES)
            {
                serialMessages.Dequeue();
            }

            // ListBox'ı güncelle
            UpdateSerialMonitor();
        }

        private void UpdateSerialMonitor()
        {
            // UI thread kontrolü
            if (lstSerialMessages.InvokeRequired)
            {
                lstSerialMessages.Invoke(new Action(UpdateSerialMonitor));
                return;
            }

            // ListBox'ı temizle ve mesajları ekle
            lstSerialMessages.Items.Clear();
            foreach (string message in serialMessages)
            {
                lstSerialMessages.Items.Add(message);
            }

            // Son eklenen mesaja kaydır
            if (lstSerialMessages.Items.Count > 0)
            {
                lstSerialMessages.SelectedIndex = lstSerialMessages.Items.Count - 1;
            }
        }

        private void HesaplaOlcek()
        {
            olcekX = (float)this.ClientSize.Width / referansGenislik;
            olcekY = (float)this.ClientSize.Height / referansYukseklik;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            // Form yeniden boyutlandırıldığında ölçeği güncelle
            HesaplaOlcek();

            // Trafik ışıkları ve yoğunluk göstergelerini yeniden konumlandır
            YenidenKonumlandir();

            // Formu yeniden çiz
            this.Invalidate();

            // Serial Monitor panelini güncelle
            pnlSerialMonitor.Location = new Point(this.ClientSize.Width - 310, 10);
        }

        private void YenidenKonumlandir()
        {
            // Trafik ışıklarını temizle ve yeniden oluştur
            foreach (var isik in trafikIsiklari)
            {
                if (this.Controls.Contains(isik))
                    this.Controls.Remove(isik);
            }
            trafikIsiklari.Clear();
            InitTrafikIsiklari();

            // Trafik yoğunluğu göstergelerini temizle ve yeniden oluştur
            trafikYogunluklari.Clear();
            InitTrafikYogunluklari();
        }

        private void RandomTrafikYogunlugu()
        {
            foreach (var yogunluk in trafikYogunluklari)
            {
                yogunluk.DegerGuncelle(rnd.Next(0, 101));
            }
            this.Invalidate(); // Formu yeniden çiz
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            // Kullanıcı tıkladığında en yakın trafik yoğunluğu göstergesini bul
            seciliYogunlukIndex = -1;
            int enYakinMesafe = int.MaxValue;

            for (int i = 0; i < trafikYogunluklari.Count; i++)
            {
                var yogunluk = trafikYogunluklari[i];
                int mesafe = (int)Math.Sqrt(
                    Math.Pow(e.X - yogunluk.X - yogunluk.Genislik / 2, 2) +
                    Math.Pow(e.Y - yogunluk.Y - yogunluk.Yukseklik / 2, 2)
                );

                if (mesafe < enYakinMesafe && mesafe < 50 * Math.Max(olcekX, olcekY)) // 50 piksel içindeyse, ölçeklenmiş
                {
                    enYakinMesafe = mesafe;
                    seciliYogunlukIndex = i;
                }
            }

            if (seciliYogunlukIndex != -1)
            {
                int kavsak = (seciliYogunlukIndex / 4) + 1;
                int yon = seciliYogunlukIndex % 4;
                lblStatus.Text = $"{kavsak} no'lu kavşağın {GetYonAdi(yon)} yönü seçildi";
            }
            else
            {
                lblStatus.Text = "Gösterge seçilmedi";
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            cmbPorts.Items.AddRange(ports);

            // SerialPort veri alma olayını ekle
            serialPort1.DataReceived += SerialPort1_DataReceived;

            // Timer interval değerini ayarla - 1 saniye yap
            timer1.Interval = 1000;

            // Trafik yoğunluğunu azaltma timer'ını başlat (1 saniyede bir çalışacak)
            trafikAzaltmaTimer.Interval = 1000; // 1 saniye
            trafikAzaltmaTimer.Tick += TrafikAzaltmaTimer_Tick;
            trafikAzaltmaTimer.Start();
        }

        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // Port açıksa ve veri varsa oku
                if (serialPort1.IsOpen && serialPort1.BytesToRead > 0)
                {
                    string veri = serialPort1.ReadLine().Trim();

                    // UI thread'e geç ve veriyi işle
                    this.BeginInvoke(new Action(() => {
                        // Gelen veriyi serial monitorda göster
                        AddSerialMessage(veri, false);

                        // Eğer gelen veri "Y," ile başlıyorsa (Yeşil ışık komutuysa)
                        if (veri.StartsWith("Y,"))
                        {
                            string[] parcalar = veri.Split(new char[] { ',', '#' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parcalar.Length >= 3)
                            {
                                int kavsak = int.Parse(parcalar[1]);
                                int yesilYon = int.Parse(parcalar[2]);

                                // Trafik ışıklarını güncelle
                                GuncelleTrafikIsiklari(kavsak, yesilYon);

                                // Arduino'ya şimdi yanıt olarak ilgili kavşağın yoğunluk değerlerini gönder
                                GonderKavsakYogunlukVerileri(kavsak);

                                // Veri gönderildi, artık bekleme durumundan çık
                                veriBekleniyor = false;
                            }
                        }
                        // YENİ EKLENDİ - Skor verilerini işleme
                        else if (veri.StartsWith("S,"))
                        {
                            string[] parcalar = veri.Split(new char[] { ',', '#' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parcalar.Length >= 5) // S, Kavşak, 4 yön skoru
                            {
                                try
                                {
                                    int kavsak = int.Parse(parcalar[1]);
                                    float[] skorlar = new float[4];

                                    for (int i = 0; i < 4; i++)
                                    {
                                        if (float.TryParse(parcalar[i + 2], out float skor))
                                        {
                                            skorlar[i] = skor;
                                        }
                                    }

                                    // Kavşak indeksi 1'den başladığı için diziye dönüştürürken 1 çıkarıyoruz
                                    kavsak--;

                                    if (kavsak >= 0 && kavsak < 4)
                                    {
                                        for (int yon = 0; yon < 4; yon++)
                                        {
                                            int index = kavsak * 4 + yon;
                                            if (index < trafikYogunluklari.Count)
                                            {
                                                // Skoru trafikYogunluklari içine kaydedelim
                                                trafikYogunluklari[index].SkorGuncelle(skorlar[yon]);
                                            }
                                        }

                                        // Formu yeniden çizerek skorları göster
                                        this.Invalidate();

                                        // Debug bilgisi
                                        AddSerialMessage($"Kavşak {kavsak + 1} için skorlar güncellendi", false);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AddSerialMessage("HATA (Skor işleme): " + ex.Message, false);
                                }
                            }
                        }
                        // Potansiyometreden gelen değeri işle - arduino # karakteri ile sonlanmış değer gönderir
                        else if (potAktif && int.TryParse(veri.Replace("#", ""), out int potDegeri))
                        {
                            // 0-1023 aralığını 0-100 aralığına dönüştür
                            int yogunlukDegeri = (int)((potDegeri / 1023.0) * 100);

                            // Eğer seçili bir yoğunluk göstergesi varsa
                            if (seciliYogunlukIndex != -1)
                            {
                                trafikYogunluklari[seciliYogunlukIndex].DegerGuncelle(yogunlukDegeri);
                                this.Invalidate(); // Formu yeniden çiz

                                // Durumu güncelle
                                int kavsak = (seciliYogunlukIndex / 4) + 1;
                                int yon = seciliYogunlukIndex % 4;
                                lblStatus.Text = $"Kavşak {kavsak}, {GetYonAdi(yon)} yoğunluğu: %{yogunlukDegeri} (potansiyometre)";
                            }
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                // Hata oluşursa UI thread'de göster
                this.BeginInvoke(new Action(() => {
                    AddSerialMessage("HATA: " + ex.Message, false);
                }));
            }
        }

        private void TrafikAzaltmaTimer_Tick(object sender, EventArgs e)
        {
            // Her kavşak için yeşil ışığın yandığı yönün trafik yoğunluğunu azalt
            for (int kavsak = 0; kavsak < 4; kavsak++)
            {
                int baslangicIndeks = kavsak * 4;

                // Yeşil yanan ışığı bul
                for (int yon = 0; yon < 4; yon++)
                {
                    int isikIndeks = baslangicIndeks + yon;
                    int yogunlukIndeks = kavsak * 4 + yon;

                    if (isikIndeks < trafikIsiklari.Count &&
                        yogunlukIndeks < trafikYogunluklari.Count &&
                        trafikIsiklari[isikIndeks].BackColor == Color.Green)
                    {
                        // Potansiyometre ile kontrol edilmiyor ise yoğunluğu azalt
                        if (!(potAktif && yogunlukIndeks == seciliYogunlukIndex))
                        {
                            // Yeşil ışık yanan yönün yoğunluğunu %1 azalt
                            int mevcutYogunluk = trafikYogunluklari[yogunlukIndeks].Deger;
                            int yeniYogunluk = Math.Max(0, mevcutYogunluk - 1); // Minimum 0 olabilir

                            // Yoğunluğu güncelle
                            trafikYogunluklari[yogunlukIndeks].DegerGuncelle(yeniYogunluk);

                            // Eğer yoğunluk sıfır olduysa ve port açıksa, seri olarak interrupt gönder
                            if (yeniYogunluk == 0 && serialPort1.IsOpen && !veriBekleniyor)
                            {
                                Task.Run(() => {
                                    try
                                    {
                                        // R,kavşak# formatında interrupt gönder
                                        string interruptKomut = $"R,{kavsak + 1}#";
                                        serialPort1.Write(interruptKomut);

                                        // UI thread'de Serial Monitor'a bilgi ekle ve durum güncelle
                                        this.BeginInvoke(new Action(() => {
                                            AddSerialMessage(interruptKomut, true);
                                            lblStatus.Text = $"Kavşak {kavsak + 1} yoğunluğu sıfırlandı, yeniden hesaplama yapılıyor";
                                            veriBekleniyor = true;
                                            sonVeriGondermeZamani = DateTime.Now;
                                        }));
                                    }
                                    catch (Exception ex)
                                    {
                                        this.BeginInvoke(new Action(() => {
                                            AddSerialMessage("HATA: " + ex.Message, false);
                                        }));
                                    }
                                });
                            }
                        }
                        break; // Bu kavşakta yeşil ışığı bulduk, sonraki kavşağa geç
                    }
                }
            }

            // Form'u yeniden çiz (trafik yoğunluklarını güncellemek için)
            this.Invalidate();

            // Zaman aşımı kontrolü - eğer veri gönderildi ama yanıt gelmezse
            if (veriBekleniyor && (DateTime.Now - sonVeriGondermeZamani).TotalMilliseconds > VERI_GONDERME_ZAMAN_ASIMI)
            {
                veriBekleniyor = false;
                lblStatus.Text = "Veri gönderme zaman aşımına uğradı. Tekrar deneyin.";
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Bu boş kalabilir
        }

        private void lblA_Click(object sender, EventArgs e)
        {
            // Bu boş kalabilir
        }

        private void lblStatus_Click(object sender, EventArgs e)
        {
            // Bu boş kalabilir
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
                timer1.Stop();
                lblStatus.Text = "Bağlı Değil";
                lblStatus.ForeColor = Color.Red;
                btnConnect.Text = "Bağlan";

                // Potansiyometreyi de kapat
                if (potAktif)
                {
                    potAktif = false;
                    btnPotAktif.Text = "Potansiyometre Aktif Et";
                    lblPotDurum.Text = "Potansiyometre: Devre Dışı";
                    lblPotDurum.ForeColor = Color.Red;
                }

                // Serial Monitor'e bilgi ekle
                AddSerialMessage("Port kapatıldı: " + serialPort1.PortName, false);
            }
            else if (cmbPorts.SelectedItem != null)
            {
                serialPort1.PortName = cmbPorts.SelectedItem.ToString();
                serialPort1.BaudRate = 9600;
                try
                {
                    serialPort1.Open();
                    timer1.Start();
                    lblStatus.Text = "BAĞLI";
                    lblStatus.ForeColor = Color.Green;
                    btnConnect.Text = "Bağlantıyı Kes";

                    // Serial Monitor'e bilgi ekle
                    AddSerialMessage("Port açıldı: " + serialPort1.PortName + ", Baud: " + serialPort1.BaudRate, false);

                    // Tüm kavşakları arka planda aktifleştir
                    Task.Run(() => {
                        for (int i = 1; i <= 4; i++)
                        {
                            try
                            {
                                string komut = $"R,{i}#";
                                serialPort1.Write(komut);

                                // UI thread'de Serial Monitor'a ekle
                                int kavsak = i; // Closure için kopya alıyoruz
                                this.BeginInvoke(new Action(() => {
                                    AddSerialMessage(komut, true);
                                    lblStatus.Text = $"Kavşak {kavsak} için veri gönderildi.";
                                }));

                                // Her komut arasında kısa bir bekleme (UI thread'i bloke etmeyen şekilde)
                                System.Threading.Thread.Sleep(200);
                            }
                            catch (Exception ex)
                            {
                                // UI thread'de hatayı göster
                                this.BeginInvoke(new Action(() => {
                                    AddSerialMessage("HATA: " + ex.Message, false);
                                }));
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Bağlanılamadı: " + ex.Message);

                    // Serial Monitor'e hata ekle
                    AddSerialMessage("HATA: " + ex.Message, false);
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // 1 saniyede bir Arduino'yu kontrol et
            if (serialPort1.IsOpen && !veriBekleniyor)
            {
                // Sıradaki kavşak için yeniden hesaplama isteği gönder
                aktifKavsak = (aktifKavsak % 4) + 1; // 1-4 arası döngü
                GonderYenidenHesaplamaIstegi(aktifKavsak);
            }
        }

        // Yeniden hesaplama isteği gönderen fonksiyon
        private void GonderYenidenHesaplamaIstegi(int kavsak)
        {
            if (serialPort1.IsOpen && !veriBekleniyor)
            {
                Task.Run(() => {
                    try
                    {
                        string komut = $"R,{kavsak}#";
                        serialPort1.Write(komut);

                        // UI thread'de bilgi güncelle
                        this.BeginInvoke(new Action(() => {
                            AddSerialMessage(komut, true);
                            lblStatus.Text = $"Kavşak {kavsak} için yeniden hesaplama istendi.";
                            veriBekleniyor = true;
                            sonVeriGondermeZamani = DateTime.Now;
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() => {
                            AddSerialMessage("HATA: " + ex.Message, false);
                        }));
                    }
                });
            }
        }

        // Belli bir kavşağın trafik yoğunluk verilerini göndermek için yeni fonksiyon
        private void GonderKavsakYogunlukVerileri(int kavsak)
        {
            if (!serialPort1.IsOpen) return;

            Task.Run(() => {
                try
                {
                    // Arduino 1'den başladığı için kavşak -1 yapalım
                    int kavsakIndeks = kavsak - 1;

                    // Seçilen kavşağın 4 yönünün verilerini gönder
                    for (int yon = 0; yon < 4; yon++)
                    {
                        int yogunlukIndeks = kavsakIndeks * 4 + yon;
                        if (yogunlukIndeks < trafikYogunluklari.Count)
                        {
                            // Potansiyometre ile kontrol edilmeyen yönleri güncelle
                            if (!(potAktif && yogunlukIndeks == seciliYogunlukIndex))
                            {
                                int yogunluk = trafikYogunluklari[yogunlukIndeks].Deger;
                                string veri = $"{kavsak},{yon},{yogunluk}#";
                                serialPort1.Write(veri);

                                // UI thread'de göster
                                int y = yon; // Closure için kopya
                                this.BeginInvoke(new Action(() => {
                                    AddSerialMessage(veri, true);
                                }));

                                // Verilerin işlenmesi için küçük bir bekleme ekleyin
                                System.Threading.Thread.Sleep(50);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.BeginInvoke(new Action(() => {
                        AddSerialMessage("HATA: " + ex.Message, false);
                    }));
                }
            });
        }

        // Trafik yoğunluğunu güncelleyen fonksiyon
        private void GuncelleTrafikYogunlugu(int kavsak, int yon, int yeniDeger)
        {
            // Kavşak indeksi 1'den, dizi indeksi 0'dan başladığı için 1 çıkarıyoruz
            kavsak--;

            if (kavsak >= 0 && kavsak < 4 && yon >= 0 && yon < 4)
            {
                // Her kavşak için 4 yön var, dolayısıyla toplam indeksi hesapla
                int yogunlukIndeksi = kavsak * 4 + yon;

                if (yogunlukIndeksi >= 0 && yogunlukIndeksi < trafikYogunluklari.Count)
                {
                    // Potansiyometre ile kontrol edilmiyorsa güncelle
                    if (!(potAktif && yogunlukIndeksi == seciliYogunlukIndex))
                    {
                        // Yoğunluk değerini güncelle
                        trafikYogunluklari[yogunlukIndeksi].DegerGuncelle(yeniDeger);

                        // Serial Monitor'a bilgi ekle
                        AddSerialMessage($"Kavşak {kavsak + 1}, {GetYonAdi(yon)} yoğunluğu: %{yeniDeger} güncellendi.", false);

                        // Formu yeniden çiz
                        this.Invalidate();
                    }
                }
            }
        }

        // Trafik ışıklarını güncelleyen fonksiyon
        private void GuncelleTrafikIsiklari(int kavsak, int yesilYon)
        {
            // Kavşak indeksi 1'den, dizi indeksi 0'dan başladığı için 1 çıkarıyoruz
            kavsak--;

            if (kavsak >= 0 && kavsak < 4 && yesilYon >= 0 && yesilYon < 4)
            {
                // Her kavşak için 4 ışık var (Kuzey, Doğu, Güney, Batı)
                int baslangicIndeks = kavsak * 4;

                // Önce tüm ışıkları kırmızı yap
                for (int i = 0; i < 4; i++)
                {
                    trafikIsiklari[baslangicIndeks + i].BackColor = Color.DarkRed;
                }

                // Sonra yeşil olan ışığı yeşil yap
                trafikIsiklari[baslangicIndeks + yesilYon].BackColor = Color.Green;

                // Serial Monitor'a bilgi ekle
                AddSerialMessage($"Kavşak {kavsak + 1}, Yön: {GetYonAdi(yesilYon)} yeşil yapıldı.", false);

                // Formu yeniden çiz
                this.Invalidate();
            }
        }

        private string GetYonAdi(int yon)
        {
            switch (yon)
            {
                case 0: return "Kuzey";
                case 1: return "Doğu";
                case 2: return "Güney";
                case 3: return "Batı";
                default: return "Bilinmeyen";
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.Close();
                }
                catch { }
            }
        }

        private void InitTrafikIsiklari()
        {
            for (int i = 0; i < 16; i++)
            {
                var isik = new Label();
                isik.Size = new Size((int)(14 * olcekX), (int)(14 * olcekY));
                isik.BackColor = Color.DarkRed;
                isik.BorderStyle = BorderStyle.FixedSingle;
                isik.Visible = true;
                isik.BringToFront();
                trafikIsiklari.Add(isik);
                this.Controls.Add(isik);
            }
        }

        private void InitTrafikYogunluklari()
        {
            int formWidth = this.ClientSize.Width;
            int formHeight = this.ClientSize.Height;

            float aralik = 300 * olcekX;  // Ölçeklenmiş aralık
            int merkezX = formWidth / 2;
            int merkezY = formHeight / 2;

            Point[] merkezler = {
                new Point(merkezX - (int)aralik, merkezY - (int)aralik), // Sol üst kavşak
                new Point(merkezX + (int)aralik, merkezY - (int)aralik), // Sağ üst kavşak
                new Point(merkezX - (int)aralik, merkezY + (int)aralik), // Sol alt kavşak
                new Point(merkezX + (int)aralik, merkezY + (int)aralik)  // Sağ alt kavşak
            };

            // Her kavşak için dört yön (Kuzey, Doğu, Güney, Batı)
            int yogunlukIndex = 0;
            for (int i = 0; i < merkezler.Length; i++)
            {
                int mx = merkezler[i].X;
                int my = merkezler[i].Y;

                // Ölçeklenmiş boyutlar
                int yatayGenislik = (int)(60 * olcekX);
                int yatayYukseklik = (int)(40 * olcekY);
                int dikeyGenislik = (int)(40 * olcekX);
                int dikeyYukseklik = (int)(60 * olcekY);
                int mesafe = (int)(150 * olcekY);
                int yatayMesafe = (int)(110 * olcekX);

                // Kuzey yönü
                trafikYogunluklari.Add(new TrafikYogunlugu(mx - yatayGenislik / 2, my - mesafe, yatayGenislik, yatayYukseklik, 0));

                // Doğu yönü
                trafikYogunluklari.Add(new TrafikYogunlugu(mx + yatayMesafe, my - dikeyYukseklik / 2, dikeyGenislik, dikeyYukseklik, 0));

                // Güney yönü
                trafikYogunluklari.Add(new TrafikYogunlugu(mx - yatayGenislik / 2, my + yatayMesafe, yatayGenislik, yatayYukseklik, 0));

                // Batı yönü
                trafikYogunluklari.Add(new TrafikYogunlugu(mx - mesafe, my - dikeyYukseklik / 2, dikeyGenislik, dikeyYukseklik, 0));
            }
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            // Kalemi ölçekle
            Pen yol = new Pen(Color.Gray, (int)(60 * Math.Min(olcekX, olcekY)));
            Pen serit = new Pen(Color.White, (int)(5 * Math.Min(olcekX, olcekY)));
            serit.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            int formWidth = this.ClientSize.Width;
            int formHeight = this.ClientSize.Height;

            // Ölçeklenmiş aralık
            float aralik = 300 * olcekX;
            int merkezX = formWidth / 2;
            int merkezY = formHeight / 2;

            Point[] merkezler = {
                new Point(merkezX - (int)aralik, merkezY - (int)aralik),
                new Point(merkezX + (int)aralik, merkezY - (int)aralik),
                new Point(merkezX - (int)aralik, merkezY + (int)aralik),
                new Point(merkezX + (int)aralik, merkezY + (int)aralik)
            };

            int isikIndex = 0;
            for (int i = 0; i < merkezler.Length; i++)
            {
                int mx = merkezler[i].X;
                int my = merkezler[i].Y;

                // Ölçeklenmiş mesafeler
                int kavsakYaricap = (int)(80 * olcekX);
                int isikMesafe = (int)(95 * olcekY);

                // Kavşak çizgileri
                e.Graphics.DrawLine(yol, mx - kavsakYaricap, my, mx + kavsakYaricap, my);
                e.Graphics.DrawLine(yol, mx, my - kavsakYaricap, mx, my + kavsakYaricap);
                e.Graphics.DrawLine(serit, mx - kavsakYaricap, my, mx + kavsakYaricap, my);
                e.Graphics.DrawLine(serit, mx, my - kavsakYaricap, mx, my + kavsakYaricap);

                // Trafik ışıkları (N, E, S, W)
                if (isikIndex + 3 < trafikIsiklari.Count)
                {
                    int isikBoyut = (int)(7 * Math.Min(olcekX, olcekY));

                    trafikIsiklari[isikIndex++].Location = new Point(mx - isikBoyut, my - isikMesafe);
                    trafikIsiklari[isikIndex++].Location = new Point(mx + kavsakYaricap + isikBoyut, my - isikBoyut);
                    trafikIsiklari[isikIndex++].Location = new Point(mx - isikBoyut, my + kavsakYaricap + isikBoyut);
                    trafikIsiklari[isikIndex++].Location = new Point(mx - isikMesafe, my - isikBoyut);
                }
            }

            // Dış yollar - ölçeklenmiş
            e.Graphics.DrawLine(yol, merkezler[0].X + (int)(80 * olcekX), merkezler[0].Y, merkezler[1].X - (int)(80 * olcekX), merkezler[1].Y);
            e.Graphics.DrawLine(yol, merkezler[2].X + (int)(80 * olcekX), merkezler[2].Y, merkezler[3].X - (int)(80 * olcekX), merkezler[3].Y);
            e.Graphics.DrawLine(yol, merkezler[0].X, merkezler[0].Y + (int)(80 * olcekY), merkezler[2].X, merkezler[2].Y - (int)(80 * olcekY));
            e.Graphics.DrawLine(yol, merkezler[1].X, merkezler[1].Y + (int)(80 * olcekY), merkezler[3].X, merkezler[3].Y - (int)(80 * olcekY));

            e.Graphics.DrawLine(serit, merkezler[0].X + (int)(80 * olcekX), merkezler[0].Y, merkezler[1].X - (int)(80 * olcekX), merkezler[1].Y);
            e.Graphics.DrawLine(serit, merkezler[2].X + (int)(80 * olcekX), merkezler[2].Y, merkezler[3].X - (int)(80 * olcekX), merkezler[3].Y);
            e.Graphics.DrawLine(serit, merkezler[0].X, merkezler[0].Y + (int)(80 * olcekY), merkezler[2].X, merkezler[2].Y - (int)(80 * olcekY));
            e.Graphics.DrawLine(serit, merkezler[1].X, merkezler[1].Y + (int)(80 * olcekY), merkezler[3].X, merkezler[3].Y - (int)(80 * olcekY));

            // Uzun yollar - Formun kenarlarına kadar
            e.Graphics.DrawLine(yol, 0, merkezY - (int)aralik, formWidth, merkezY - (int)aralik);
            e.Graphics.DrawLine(yol, 0, merkezY + (int)aralik, formWidth, merkezY + (int)aralik);
            e.Graphics.DrawLine(yol, merkezX - (int)aralik, 0, merkezX - (int)aralik, formHeight);
            e.Graphics.DrawLine(yol, merkezX + (int)aralik, 0, merkezX + (int)aralik, formHeight);

            e.Graphics.DrawLine(serit, 0, merkezY - (int)aralik, formWidth, merkezY - (int)aralik);
            e.Graphics.DrawLine(serit, 0, merkezY + (int)aralik, formWidth, merkezY + (int)aralik);
            e.Graphics.DrawLine(serit, merkezX - (int)aralik, 0, merkezX - (int)aralik, formHeight);
            e.Graphics.DrawLine(serit, merkezX + (int)aralik, 0, merkezX + (int)aralik, formHeight);

            // Trafik yoğunluğu göstergelerini çiz
            foreach (var yogunluk in trafikYogunluklari)
            {
                yogunluk.Ciz(e.Graphics);
            }
        }
    }

    public class Araba
    {
        public Panel kutu;
        public string yon;
        public bool hareketEdebilir;

        public Araba(Panel panel, string yon)
        {
            this.kutu = panel;
            this.yon = yon;
            this.hareketEdebilir = false;
        }
    }

    public class TrafikYogunlugu
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Genislik { get; private set; }
        public int Yukseklik { get; private set; }
        public int Deger { get; private set; } // 0-100 arası bir değer (yüzde)
        public float Skor { get; private set; } = 0; // Hesaplanan skor değeri - YENİ EKLENDİ

        public TrafikYogunlugu(int x, int y, int genislik, int yukseklik, int deger)
        {
            X = x;
            Y = y;
            Genislik = genislik;
            Yukseklik = yukseklik;
            Deger = Math.Max(0, Math.Min(100, deger)); // 0-100 arasında sınırla
        }

        public void DegerGuncelle(int yeniDeger)
        {
            Deger = Math.Max(0, Math.Min(100, yeniDeger)); // 0-100 arasında sınırla
        }

        // YENİ EKLENDİ - Skor değerini güncellemek için
        public void SkorGuncelle(float yeniSkor)
        {
            Skor = yeniSkor;
        }

        public void Ciz(Graphics g)
        {
            // Çerçeve çiz
            g.DrawRectangle(Pens.Black, X, Y, Genislik, Yukseklik);

            // Doluluk oranına göre içini doldur
            int doluYukseklik = (int)(Yukseklik * (Deger / 100.0));
            int doluGenislik = (int)(Genislik * (Deger / 100.0));

            // Yatay mı dikey mi kontrol et
            if (Genislik > Yukseklik) // Yatay gösterge
            {
                Rectangle doluRect = new Rectangle(X, Y, doluGenislik, Yukseklik);

                // Yoğunluğa göre renk belirle (Yeşil -> Sarı -> Kırmızı)
                Color dolguRengi;
                if (Deger < 30)
                    dolguRengi = Color.Green;
                else if (Deger < 70)
                    dolguRengi = Color.Yellow;
                else
                    dolguRengi = Color.Red;

                g.FillRectangle(new SolidBrush(dolguRengi), doluRect);

                // YENİ EKLENDİ - Skor gösterimi (yatay gösterge için)
                if (Skor > 0)
                {
                    string skorText = $"S:{Skor:F1}";
                    StringFormat skorSf = new StringFormat();
                    skorSf.Alignment = StringAlignment.Center;
                    g.DrawString(skorText, new Font("Arial", 7, FontStyle.Regular), Brushes.Blue,
                                new PointF(X + Genislik / 2, Y - 15), skorSf);
                }
            }
            else // Dikey gösterge
            {
                // Dikey gösterge aşağıdan yukarı dolsun
                Rectangle doluRect = new Rectangle(X, Y + Yukseklik - doluYukseklik, Genislik, doluYukseklik);

                // Yoğunluğa göre renk belirle (Yeşil -> Sarı -> Kırmızı)
                Color dolguRengi;
                if (Deger < 30)
                    dolguRengi = Color.Green;
                else if (Deger < 70)
                    dolguRengi = Color.Yellow;
                else
                    dolguRengi = Color.Red;

                g.FillRectangle(new SolidBrush(dolguRengi), doluRect);

                // YENİ EKLENDİ - Skor gösterimi (dikey gösterge için)
                if (Skor > 0)
                {
                    string skorText = $"S:{Skor:F1}";
                    StringFormat skorSf = new StringFormat();
                    skorSf.Alignment = StringAlignment.Far;
                    g.DrawString(skorText, new Font("Arial", 7, FontStyle.Regular), Brushes.Blue,
                                new PointF(X - 5, Y + Yukseklik / 2 - 15), skorSf);
                }
            }

            // Yüzde değerini göster
            string yuzde = $"%{Deger}";
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            g.DrawString(yuzde, new Font("Arial", 8, FontStyle.Bold), Brushes.Black, new RectangleF(X, Y, Genislik, Yukseklik), sf);
        }
    }
}