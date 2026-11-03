using UnityEngine;

namespace Game.APD
{
    using System.Collections.Generic;
    using Unity.Android.Gradle.Manifest;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class InfoBahayaMenuUI : MonoBehaviour
    {
        public UIDocument uiDocument;

        private VisualElement mapContainer;
        private VisualElement popupPanel;
        private Label popupTitle;
        private Label popupDesc;
        private Label popupApd;

        private Button currentActivePoint = null;

        // Struktur Data Titik Bahaya
        public class HazardPoint
        {
            public string Id;
            public Vector2 PositionPercent; // Menggunakan persentase (X=kiri, Y=atas) dari 0 s/d 100
            public string Title;
            public string Description;
            public string ApdInfo;
        }

        private List<HazardPoint> hazardDataList = new List<HazardPoint>();

        private void Start()
        {
            var data = APDGameManager.Instance.PlantSelectedData;

            var root = uiDocument.rootVisualElement;

            var btnBack = root.Q<Button>("btn-back");
            btnBack.RegisterCallback<ClickEvent>(ev =>
            {
                GameManager.LoadScene("PlantMenu", false);
            });

            mapContainer = root.Q<VisualElement>("map-container");
            mapContainer.style.backgroundImage = new StyleBackground(data.VectorImage);

            popupPanel = root.Q<VisualElement>("popup-panel");
            popupTitle = root.Q<Label>("popup-title");
            popupDesc = root.Q<Label>("popup-desc");
            popupApd = root.Q<Label>("popup-apd");

            root.Q<Button>("btn-tutup-popup")?.RegisterCallback<ClickEvent>(evt => ClosePopup());

            // 1. Siapkan Database Dummy (Anda bisa ganti tarik data dari API/JSON)
            SetupHazardData();

            // 2. Spawn Titik-Titik ke Layar
            //SpawnPoints(Data);
            SpawnPoints(data);
        }

        private void SetupHazardData()
        {
            hazardDataList.Add(new HazardPoint
            {
                Id = "point_1",
                PositionPercent = new Vector2(15f, 80f), // X: 15%, Y: 80%
                Title = "Tumpukan Material",
                Description = "Bahaya tertimbun atau terpeleset pada area material curah.",
                ApdInfo = "APD: Safety helmet, safety shoes, masker debu"
            });

            hazardDataList.Add(new HazardPoint
            {
                Id = "point_tangga",
                PositionPercent = new Vector2(78f, 58f), // Posisi tangga di gambar
                Title = "Tangga transfer house",
                Description = "Pekerja bisa jatuh dari ketinggian atau terpeleset di tangga.",
                ApdInfo = "APD: full body harness with double lanyard, safety helmet, safety shoes"
            });

            // Tambahkan titik lainnya sesuai Vector2 yang diinginkan...
            hazardDataList.Add(new HazardPoint
            {
                Id = "point_2",
                PositionPercent = new Vector2(25f, 60f),
                Title = "Conveyor Belt",
                Description = "Bahaya terjepit mesin berputar.",
                ApdInfo = "APD: Safety helmet, sarung tangan, kacamata safety"
            });
        }

        private void SpawnPoints(Plant plantData)
        {
            //foreach (var data in hazardDataList)
            //{
            //    // Buat elemen Button baru
            //    Button btnPoint = new Button();
            //    btnPoint.name = data.Id;
            //    btnPoint.text = "?";

            //    // Tambahkan class Tailwind ke tombol baru
            //    string[] classes = {
            //    "absolute", "w-10", "h-10", "rounded-full", "bg-slate-900",
            //    "text-white", "font-bold", "text-lg", "border-2", "border-slate-400",
            //    "cursor-pointer", "justify-center", "items-center"
            //};
            //    foreach (var c in classes) btnPoint.AddToClassList(c);

            //    // Set Posisi Dinamis (Menggunakan tipe persentase agar responsif)
            //    btnPoint.style.left = new Length(data.PositionPercent.x, LengthUnit.Percent);
            //    btnPoint.style.top = new Length(data.PositionPercent.y, LengthUnit.Percent);

            //    // Daftarkan event klik (Capture data local untuk lambda)
            //    HazardPoint localData = data;
            //    btnPoint.clicked += () => OnPointClicked(btnPoint, localData);

            //    // Masukkan ke dalam map-container (Ditaruh di index 0 agar tidak menutupi popup)
            //    mapContainer.Insert(0, btnPoint);
            //}

            foreach (var data in plantData.Facilities)
            {
                // Buat elemen Button baru
                Button btnPoint = new Button();
                btnPoint.name = data.Id;
                btnPoint.text = "?";

                // Tambahkan class Tailwind ke tombol baru
                string[] classes = {
                "absolute", "w-10", "h-10", "rounded-full", "bg-slate-900",
                "text-white", "font-bold", "text-lg", "border-2", "border-slate-400",
                "cursor-pointer", "justify-center", "items-center"
            };
                foreach (var c in classes) btnPoint.AddToClassList(c);

                // Set Posisi Dinamis (Menggunakan tipe persentase agar responsif)
                btnPoint.style.left = new Length(data.PositionPoint.x * 100f, LengthUnit.Percent);
                btnPoint.style.top = new Length(data.PositionPoint.y * 100f, LengthUnit.Percent);

                btnPoint.clicked += () => OnPointClicked(btnPoint, data);

                // Masukkan ke dalam map-container (Ditaruh di index 0 agar tidak menutupi popup)
                mapContainer.Insert(0, btnPoint);
            }
        }

        private void OnPointClicked(Button clickedBtn, Facility data)
        {
            if (currentActivePoint != null) ResetPointStyle(currentActivePoint);

            HighlightPointStyle(clickedBtn);
            currentActivePoint = clickedBtn;

            // Update isi popup dari object data
            popupTitle.text = data.Name;
            popupDesc.text = data.Description;
            
            string apdList = string.Empty;
            for (int i = 0; i < data.Hazards.Length; i++)
            {
                for (int j = 0; j < data.Hazards[i].RequiredEquipment.Length; j++)
                {
                    apdList += string.Join(", ", data.Hazards[i].RequiredEquipment[j].Name);
                }
            }

            popupApd.text = apdList;

            popupPanel.style.display = DisplayStyle.Flex;

            popupPanel.schedule.Execute(() =>
            {
                SetPopupPosition(clickedBtn);
            }).StartingIn(10);
        }

        private void SetPopupPosition(Button clickedBtn)
        {
            // 1. Ambil koordinat tombol yang diklik
            float btnX = clickedBtn.resolvedStyle.left;
            float btnY = clickedBtn.resolvedStyle.top;

            // 2. Ambil ukuran dimensi Layar (Container) dan Pop-up
            float containerWidth = mapContainer.resolvedStyle.width;
            float containerHeight = mapContainer.resolvedStyle.height; // Ambil tinggi container

            float popupWidth = popupPanel.resolvedStyle.width;
            float popupHeight = popupPanel.resolvedStyle.height; // Ambil tinggi popup

            float offsetX = 60f;  // Jarak dari titik ke pop-up (horizontal)
            float offsetY = 50f;  // Jarak dari titik ke pop-up (vertikal)
            float padding = 20f;  // Batas aman minimum dari pinggir layar (agar tidak nempel banget)

            // ==========================================
            // 3. LOGIKA POSISI X (KIRI / KANAN)
            // ==========================================
            float targetX;

            // Cek apakah jika ditaruh di KANAN akan nabrak batas kanan layar?
            if (btnX + offsetX + popupWidth > containerWidth - padding)
            {
                // Mentok kanan -> pindahkan ke KIRI tombol
                targetX = btnX - popupWidth - 20f;
            }
            else
            {
                // Aman -> taruh di KANAN tombol
                targetX = btnX + offsetX;
            }

            // Cek darurat: Pastikan tidak tembus batas KIRI layar (kalo layarnya sempit)
            if (targetX < padding) targetX = padding;

            popupPanel.style.left = targetX;

            // ==========================================
            // 4. LOGIKA POSISI Y (ATAS / BAWAH)
            // ==========================================
            float targetY = btnY - offsetY; // Posisi ideal awal (sedikit ke atas dari tombol)

            // Cek apakah nabrak batas ATAS layar?
            if (targetY < padding)
            {
                targetY = padding; // Tahan di batas atas
            }
            // Cek apakah nabrak batas BAWAH layar?
            else if (targetY + popupHeight > containerHeight - padding)
            {
                // Tahan posisi Y agar bagian bawah popup sejajar dengan batas bawah layar
                targetY = containerHeight - popupHeight - padding;
            }

            popupPanel.style.top = targetY;
        }

        private void ClosePopup()
        {
            popupPanel.style.display = DisplayStyle.None;
            if (currentActivePoint != null)
            {
                ResetPointStyle(currentActivePoint);
                currentActivePoint = null;
            }
        }

        // Fungsi Styling Tetap Sama
        private void HighlightPointStyle(Button btn)
        {
            btn.RemoveFromClassList("w-10");
            btn.RemoveFromClassList("h-10");
            btn.RemoveFromClassList("border-slate-400");
            btn.RemoveFromClassList("border-2");

            btn.AddToClassList("w-12");
            btn.AddToClassList("h-12");
            btn.AddToClassList("border-white");
            btn.AddToClassList("border-4");
            btn.AddToClassList("shadow-lg");
            btn.AddToClassList("text-xl");
        }

        private void ResetPointStyle(Button btn)
        {
            btn.RemoveFromClassList("w-12");
            btn.RemoveFromClassList("h-12");
            btn.RemoveFromClassList("border-white");
            btn.RemoveFromClassList("border-4");
            btn.RemoveFromClassList("shadow-lg");
            btn.RemoveFromClassList("text-xl");

            btn.AddToClassList("w-10");
            btn.AddToClassList("h-10");
            btn.AddToClassList("border-slate-400");
            btn.AddToClassList("border-2");
        }
    }
}