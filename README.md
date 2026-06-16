# The Forest Guardian

*Game Virtual Reality (VR) berbasis Unity di mana Anda berperan sebagai penjaga hutan untuk merawat pohon, memadamkan kebakaran, dan menjaga keseimbangan ekosistem.*

[![Unity Version](https://img.shields.io/badge/Unity-6000.3.5f1-lightgrey.svg)](https://unity.com)
[![XR Interaction Toolkit](https://img.shields.io/badge/XR%20Interaction-3.3.1-blue.svg)](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/index.html)

---

## Daftar Isi
1. [Tentang Proyek](#tentang-proyek)
2. [Prasyarat & Instalasi](#prasyarat--instalasi)
3. [Penggunaan](#penggunaan)
4. [Struktur Proyek](#struktur-proyek)
5. [Kontribusi](#kontribusi)
6. [Lisensi & Kontak](#lisensi--kontak)

---

## Tentang Proyek

**The Forest Guardian** adalah simulasi imersif dalam Virtual Reality yang dirancang untuk memberikan edukasi mengenai pelestarian lingkungan. Pemain dapat memilih antara **Story Mode** (dengan misi interaktif terstruktur) atau **Sandbox Mode**.

Dalam permainan, Anda dapat melakukan berbagai tindakan:
- 🔍 **Scanning Pohon:** Menggunakan *Scanner* untuk mengidentifikasi pohon yang sehat atau berpenyakit (sakit/buruk).
- 🪓 **Penebangan & Perawatan:** Memotong pohon berpenyakit dengan kapak, kemudian menggali tanah dengan sekop untuk menanam pohon baru.
- 🧯 **Pemadaman Api:** Mengambil *Fire Hose* untuk memadamkan kebakaran hutan yang merusak ekosistem.
- 📈 **Manajemen Statistik:** Memastikan kelangsungan ekosistem dengan memulihkan indikator status hutan.

Teknologi Utama yang Digunakan:
- **Unity 6000.3.5f1**
- **XR Interaction Toolkit (XRI)** versi 3.3.1 untuk mekanik VR standar.
- **Universal Render Pipeline (URP)** / **High Definition Render Pipeline (HDRP)** untuk pencahayaan dan grafis.
- **DOTween** untuk animasi UI dan objek.
- **TextMeshPro** untuk kebutuhan tipografi HUD dan indikator.

---

## Prasyarat & Instalasi

### Prasyarat
- Unity Hub terinstal.
- Editor Unity versi **6000.3.5f1**.
- Headset VR yang kompatibel dengan OpenXR atau Oculus (mis. Meta Quest 2/3) beserta kabel link atau koneksi nirkabel.

### Cara Instalasi

1. **Clone repository ini** ke mesin lokal Anda:
   ```bash
   git clone <URL_REPOSITORY>
   cd "ProjekIMK/The Forest Guardian"
   ```

2. **Buka dengan Unity Hub:**
   - Buka Unity Hub > klik **Add** > pilih folder `The Forest Guardian`.
   - Pastikan editor versi `6000.3.5f1` telah terpasang.

3. **Instalasi Dependencies Tambahan:**
   Ketika proyek dibuka pertama kali, Unity akan mengunduh otomatis package berikut sesuai dengan file `manifest.json`:
   - XR Interaction Toolkit (3.3.1)
   - OpenXR Plugin (1.16.1)
   - Oculus XR Plugin (4.5.4)
   - Animation Rigging, DOTween, dll.

4. **Konfigurasi Lingkungan:**
   - Pergi ke `Edit > Project Settings > XR Plug-in Management`. Pastikan OpenXR (atau Oculus, sesuai perangkat) sudah tercentang pada tab PC/Android.

---

## Penggunaan

1. Buka adegan utama (Main Scene) yang terletak di direktori `Assets/Scenes/`.
2. Tekan tombol **Play** di Unity Editor.
3. Anda dapat bermain menggunakan perangkat VR fisik, atau menggunakan **XR Interaction Simulator** yang sudah disediakan untuk uji coba menggunakan mouse dan keyboard jika Headset VR tidak terhubung.
4. Di *Story Mode*, ikuti panduan layar untuk:
   - Berpindah (*Move*) dan Melihat (*Look*)
   - Mengakses Inventori
   - *Summon* & *Grab Scanner* untuk memindai pohon
   - *Summon Fire Hose* untuk memadamkan api.

---

## Struktur Proyek

```text
The Forest Guardian/
├── Assets/
│   ├── Plugins/                 # Library pihak ketiga (DOTween, dll.)
│   ├── ProceduralTerrainPainter/# Sistem untuk mewarnai dan menghasilkan medan secara dinamis
│   ├── Scenes/                  # Adegan/Scene Unity dan Script utama
│   │   ├── Script/              # Logika gameplay utama (GameManager, QuestManager, PlayerMovement)
│   ├── TextMesh Pro/            # Aset teks dan UI
│   ├── WaterWorks/              # Sistem dan efek air
│   └── XR/ & XRI/               # Konfigurasi XR Interaction Toolkit
├── Packages/                    # Konfigurasi dependensi project (manifest.json)
└── ProjectSettings/             # Pengaturan global proyek (grafis, input, XR)
```

**File Penting:**
- `GameManager.cs` — Kelas Singleton yang mengelola status permainan dan referensi terrain.
- `QuestManager.cs` — Mengatur alur *Story Mode* dan *Sandbox*, serta tahapan objektif (Scanning, Penebangan, Penanaman, Pemadaman).
- `TreeManager.cs` & `TreeChop.cs` — Logika terkait siklus hidup pohon dan interaksi pemotongan pohon.

---

## Kontribusi

Kami sangat mengapresiasi kontribusi dari developer lain! Jika Anda ingin membantu pengembangan proyek ini:

1. **Fork** repository ini.
2. Buat branch fitur baru (`git checkout -b feature/NamaFitur`).
3. Lakukan commit perubahan Anda secara spesifik dan rapi (`git commit -m "Add: mekanik baru untuk penyiraman tanaman"`).
4. **Push** ke branch yang telah dibuat (`git push origin feature/NamaFitur`).
5. Buka **Pull Request** ke branch utama dan berikan deskripsi fitur atau *bugfix* yang Anda kerjakan.

---

## Lisensi & Kontak

**Lisensi:** Proyek ini dilindungi oleh hak cipta yang berlaku. Silakan hubungi pengelola repositori untuk informasi lebih lanjut mengenai lisensi penggunaan.

**Kontak:**
Jika Anda menemukan *bug* atau memiliki saran, jangan ragu untuk membuka [Issue](../../issues) di repositori ini.