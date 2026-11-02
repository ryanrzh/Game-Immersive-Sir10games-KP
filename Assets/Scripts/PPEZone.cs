using System.Collections.Generic;
using System.Linq;

namespace Game.APD
{
    // Bagian tubuh / jenis perlindungan yang ditangani sebuah APD.
    // Pakai [Flags] supaya satu APD boleh punya lebih dari satu zona
    // (contoh: helm + face shield bisa ditandai Head | Eye).
    // Di Inspector tampil sebagai dropdown multi-pilihan.
    [System.Flags]
    public enum PPEZone
    {
        None = 0,                // belum ditentukan
        Head = 1 << 0,           // Kepala (helm)
        Eye = 1 << 1,            // Mata dan wajah (kacamata, goggles, face shield)
        Respiratory = 1 << 2,    // Pernapasan (masker, CO mask)
        Ear = 1 << 3,            // Telinga (ear plug, ear muff)
        Body = 1 << 4,           // Badan (rompi, baju, suit)
        Hand = 1 << 5,           // Tangan (sarung tangan)
        Foot = 1 << 6,           // Kaki (sepatu, foot strap)
        FallProtection = 1 << 7, // Pelindung jatuh (harness + lanyard)
        Other = 1 << 8           // Lainnya (alat bantu seperti senter)
    }

    public static class PPEZoneExtensions
    {
        // Urutan tampil di UI: kepala sampai kaki, lalu pelindung jatuh dan lainnya.
        public static readonly PPEZone[] DisplayOrder =
        {
            PPEZone.Head, PPEZone.Eye, PPEZone.Respiratory, PPEZone.Ear,
            PPEZone.Body, PPEZone.Hand, PPEZone.Foot,
            PPEZone.FallProtection, PPEZone.Other
        };

        // Nama satu zona untuk ditampilkan ke pemain.
        public static string DisplayName(this PPEZone zone)
        {
            switch (zone)
            {
                case PPEZone.Head: return "Kepala";
                case PPEZone.Eye: return "Mata";
                case PPEZone.Respiratory: return "Pernapasan";
                case PPEZone.Ear: return "Telinga";
                case PPEZone.Body: return "Badan";
                case PPEZone.Hand: return "Tangan";
                case PPEZone.Foot: return "Kaki";
                case PPEZone.FallProtection: return "Pelindung Jatuh";
                case PPEZone.Other: return "Lainnya";
                default: return "Belum ditentukan";
            }
        }

        // Nama gabungan kalau zona lebih dari satu, contoh: "Kepala, Mata".
        public static string ToLabel(this PPEZone zone)
        {
            if (zone == PPEZone.None) return PPEZone.None.DisplayName();
            var names = new List<string>();
            foreach (var z in DisplayOrder)
                if ((zone & z) != 0) names.Add(z.DisplayName());
            return string.Join(", ", names.ToArray());
        }

        // Ambil APD yang menangani zona tertentu dari sebuah daftar.
        // Contoh: facility.EquipmentChoices.FilterByZone(PPEZone.Head)
        public static List<PersonalProtectiveEquipment> FilterByZone(
            this IEnumerable<PersonalProtectiveEquipment> list, PPEZone zone)
        {
            return list.Where(e => e != null && e.IsForZone(zone)).ToList();
        }
    }
}
