// =====================================================================================
//  IndustrialCharacterGenerator.cs
//  Generator karakter flat-vector bergaya korporat/industri untuk game pelatihan APD.
//
//  CARA PAKAI
//  1. Buat GameObject kosong -> Add Component "IndustrialCharacterGenerator".
//  2. Atur tubuh (gender, tinggi, bahu, lingkar badan, warna kulit, rambut) di Inspector.
//  3. Isi list "equipment" dengan id part (lihat daftar id di bawah) atau panggil lewat kode:
//         gen.Equip("hardhat");
//         gen.Equip("vest", new Color(1f, 0.45f, 0.05f));
//         gen.Play("Walk");  gen.PlayOverlay("Wave");  gen.SetExpression(IndExpression.Happy);
//  4. Kamera 2D orthographic. SELURUH karakter = 1 GameObject + 1 SkinnedMeshRenderer + 1 mesh
//     (vertex color). Tulang (bone) hanya Transform kosong, jadi tetap bisa dianimasikan / diakses
//     lewat GetBone(), tetapi draw call & batch per karakter hanya 1.
//
//  ID PART BAWAAN (per PPEZone)
//    Head : hardhat, hardhat_strap          Eye  : safety_glasses, goggles
//    Respiratory : dust_mask, half_respirator
//    Ear  : ear_plug, ear_muff              Body : coverall, vest, shirt_short, shirt_long, pants_work
//    Hand : gloves                          Foot : safety_shoes, safety_boots
//    FallProtection : harness               Other: id_card
//
//  ID OTOMATIS dari registry (tidak perlu hafal / ketik string):
//    - Inspector: field part id & custom hair id berupa dropdown (dikelompokkan per zone) yang dibaca dari registry.
//    - Kode: konstanta IndPartIds.Hardhat, IndHairIds.xxx, IndAnimNames.Walk dibuat otomatis ke Assets/IndPartIds.cs
//      (menu Tools > Industrial Character > Generate Id Constants; juga jalan sendiri tiap script selesai dikompilasi).
//    - Part/rambut/animasi buatan sendiri: taruh pendaftarannya di method static tanpa parameter bertanda
//      [IndAutoRegister] -> terdaftar otomatis (juga di Editor), langsung muncul di dropdown & konstanta.
//
//  MENAMBAH PART BARU  : IndPartLibrary.Register(new IndPartDef { ... build = ctx => {...} });
//  MENAMBAH ANIMASI    : IndAnimLibrary.Register(new IndLambdaAnim("Nama", durasi, loop, (t, pose, rig) => {...}));
//  MENAMBAH GAYA RAMBUT: IndHairLibrary.Register("NamaGaya", ctx => {...}); lalu isi body.customHairId.
//
//  CATATAN
//  - Enum PPEZone dipakai dari proyek kamu (tanpa namespace). Kalau PPEZone ada di dalam
//    namespace, tambahkan "using NamespaceKamu;" di bawah.
//  - Tampak depan saja. Sisi "Left/Right" = kiri/kanan LAYAR (Left = x negatif).
//  - Proporsi tubuh: ~7 kepala, gaya flat-vector ramah (bahu miring, lengan & kaki lurus-tebal). 1 unit = 1 meter.
//  - Urutan gambar antar part = urutan segitiga di dalam 1 mesh (diurutkan menurut IndLayer), bukan sortingOrder.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Game;
using Game.APD;

// ------------------------------------------------------------------------------------
//  ENUM
// ------------------------------------------------------------------------------------
public enum IndGender { Male, Female }
public enum IndHairStyle { Bald, Buzz, Short, SidePart, Curly, Long, Ponytail, Bun, Hijab }
public enum IndFacialHair { None, Stubble, Mustache, Goatee, Beard }
public enum IndExpression { Neutral, Happy, Worried, Surprised }
// Average = kaki lebih pendek, badan panjang (orang kiri di referensi). Slim = kaki panjang, lebih ramping (orang tengah/kanan).
public enum IndBuild { Average, Slim }

// Tandai field string agar tampil sebagai dropdown id yang dibaca otomatis dari registry.
public sealed class IndPartIdAttribute : PropertyAttribute { }
public sealed class IndHairIdAttribute : PropertyAttribute { }

// Tandai method static tanpa parameter yang berisi IndPartLibrary.Register / IndHairLibrary.Register / IndAnimLibrary.Register.
// Method dipanggil otomatis saat registry pertama kali dipakai (Editor maupun runtime).
[AttributeUsage(AttributeTargets.Method)]
public sealed class IndAutoRegisterAttribute : Attribute { }

public static class IndAutoRegistrar
{
    static bool done;
    public static void Run()
    {
        if (done) return;
        done = true;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            string n = asm.GetName().Name;
            if (n.StartsWith("System") || n.StartsWith("mscorlib") || n.StartsWith("Unity") || n.StartsWith("Mono.")
                || n.StartsWith("nunit") || n.StartsWith("netstandard") || n.StartsWith("Microsoft")) continue;
            Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                MethodInfo[] ms;
                try { ms = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { continue; }
                foreach (var m in ms)
                {
                    if (m.GetParameters().Length != 0 || !m.IsDefined(typeof(IndAutoRegisterAttribute), false)) continue;
                    try { m.Invoke(null, null); } catch (Exception e) { Debug.LogException(e); }
                }
            }
        }
    }
}

public enum IndBone
{
    Root, Hips, Torso, Neck, Head,
    UpperArmL, ForeArmL, HandL, UpperArmR, ForeArmR, HandR,
    ThighL, ShinL, FootL, ThighR, ShinR, FootR,
    Eyes
}

public static class IndBoneUtil
{
    public const int Count = 18;

    // Indeks bone induk (urutan sama dengan enum IndBone).
    public static readonly int[] Parent = { 0, 0, 1, 2, 3, 2, 5, 6, 2, 8, 9, 1, 11, 12, 1, 14, 15, 4 };

    // Pasangan sisi kanan dari bone sisi kiri.
    public static IndBone Right(IndBone left)
    {
        switch (left)
        {
            case IndBone.UpperArmL: return IndBone.UpperArmR;
            case IndBone.ForeArmL: return IndBone.ForeArmR;
            case IndBone.HandL: return IndBone.HandR;
            case IndBone.ThighL: return IndBone.ThighR;
            case IndBone.ShinL: return IndBone.ShinR;
            case IndBone.FootL: return IndBone.FootR;
            default: return left;
        }
    }
}

// Urutan gambar (sortingOrder relatif). Makin besar = makin di depan.
public static class IndLayer
{
    public const int FootSkin = 9, Leg = 10, Boot = 11, PantsLeg = 12, PantsPocket = 13, Shoe = 14, HarnessLeg = 16;
    public const int Torso = 20, PantsWaist = 21, Shirt = 23, HairBack = 24, Neck = 25, Collar = 26, Vest = 27, Harness = 28, Badge = 29;
    public const int UpperArm = 30, Sleeve = 31, ForeArm = 32, ForeSleeve = 33, Hand = 34, Glove = 35;
    public const int HijabBack = 36, Ear = 39, Head = 40, Beard = 41, Face = 42, Mask = 44, Eyewear = 46, HairFront = 48;
    public const int MuffBand = 50, MuffCup = 51, Helmet = 52;
}

public static class IndColor
{
    public static Color C(int r, int g, int b, int a = 255) { return new Color(r / 255f, g / 255f, b / 255f, a / 255f); }
    public static Color Shade(Color c, float k) { return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a); }
    public static Color Alpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }
}

// ------------------------------------------------------------------------------------
//  PROFIL LEBAR (lebar setengah badan sebagai fungsi posisi) - dasar bentuk organik
// ------------------------------------------------------------------------------------
public class IndProfile
{
    readonly float[] ys, ws;

    // pasangan (y, setengahLebar) ascending pada y. yScale/wScale = skala satuan.
    public IndProfile(float yScale, float wScale, params float[] yw)
    {
        int n = yw.Length / 2;
        ys = new float[n]; ws = new float[n];
        for (int i = 0; i < n; i++) { ys[i] = yw[i * 2] * yScale; ws[i] = yw[i * 2 + 1] * wScale; }
    }

    float Slope(int i)
    {
        int a = Mathf.Max(i - 1, 0), b = Mathf.Min(i + 1, ys.Length - 1);
        return (ws[b] - ws[a]) / (ys[b] - ys[a]);
    }

    // Interpolasi Hermite (Catmull-Rom) agar kontur halus.
    public float Eval(float y)
    {
        int n = ys.Length;
        if (y <= ys[0]) return ws[0];
        if (y >= ys[n - 1]) return ws[n - 1];
        int i = 0;
        while (i < n - 2 && y > ys[i + 1]) i++;
        float h = ys[i + 1] - ys[i];
        float t = (y - ys[i]) / h, t2 = t * t, t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * ws[i] + (t3 - 2 * t2 + t) * h * Slope(i)
             + (-2 * t3 + 3 * t2) * ws[i + 1] + (t3 - t2) * h * Slope(i + 1);
    }
}

// ------------------------------------------------------------------------------------
//  MESH BUILDER - kumpulan bentuk flat -> 1 mesh (vertex color)
// ------------------------------------------------------------------------------------
public class IndMeshBuilder
{
    readonly List<Vector3> _v = new List<Vector3>();
    readonly List<Color32> _c = new List<Color32>();
    readonly List<int> _t = new List<int>();

    public bool IsEmpty { get { return _t.Count == 0; } }
    public List<Vector3> Vertices { get { return _v; } }
    public List<Color32> Colors { get { return _c; } }
    public List<int> Triangles { get { return _t; } }

    int V(float x, float y, Color col) { _v.Add(new Vector3(x, y, 0f)); _c.Add(col); return _v.Count - 1; }
    void Tri(int a, int b, int c) { _t.Add(a); _t.Add(b); _t.Add(c); }

    // Strip horizontal antara dua fungsi x(y) -> bentuk dasar tubuh/pakaian.
    public void Strip(float y0, float y1, int steps, Func<float, float> xl, Func<float, float> xr, Color col)
    {
        steps = Mathf.Max(1, steps);
        int start = _v.Count;
        for (int i = 0; i <= steps; i++)
        {
            float y = Mathf.Lerp(y0, y1, i / (float)steps);
            V(xl(y), y, col); V(xr(y), y, col);
        }
        for (int i = 0; i < steps; i++)
        {
            int a = start + i * 2;
            Tri(a, a + 2, a + 1); Tri(a + 1, a + 2, a + 3);
        }
    }

    // Tabung simetris dari profil (y mutlak). Dipakai kepala, torso, leher, pakaian torso.
    public void Tube(IndProfile p, float cx, float y0, float y1, float pad, Color col, int steps = 16)
    {
        Strip(y0, y1, steps, y => cx - (p.Eval(y) + pad), y => cx + (p.Eval(y) + pad), col);
    }

    // Anggota badan: profil berdasarkan fraksi panjang f (0 = sendi, 1 = ujung), menggantung ke bawah (-y).
    public void Limb(IndProfile p, float cx, float len, float f0, float f1, float pad, Color col, int steps = 10)
    {
        Strip(-f0 * len, -f1 * len, steps,
            y => cx - (p.Eval(-y / len) + pad),
            y => cx + (p.Eval(-y / len) + pad), col);
    }

    // Elips / sektor elips (a0..a1 derajat), difan dari titik pusat.
    public void Ellipse(float cx, float cy, float rx, float ry, Color col, int seg = 24, float rotDeg = 0f, float a0 = 0f, float a1 = 360f)
    {
        int c0 = V(cx, cy, col);
        float rot = rotDeg * Mathf.Deg2Rad, cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
        int prev = -1;
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
            float lx = Mathf.Cos(a) * rx, ly = Mathf.Sin(a) * ry;
            int id = V(cx + lx * cr - ly * sr, cy + lx * sr + ly * cr, col);
            if (prev >= 0) Tri(c0, prev, id);
            prev = id;
        }
    }

    // Poligon cembung / bintang (difan dari titik berat).
    public void Poly(IList<Vector2> pts, Color col)
    {
        if (pts.Count < 3) return;
        Vector2 ctr = Vector2.zero;
        for (int i = 0; i < pts.Count; i++) ctr += pts[i];
        ctr /= pts.Count;
        int c0 = V(ctr.x, ctr.y, col);
        int first = -1, prev = -1;
        for (int i = 0; i < pts.Count; i++)
        {
            int id = V(pts[i].x, pts[i].y, col);
            if (first < 0) first = id;
            if (prev >= 0) Tri(c0, prev, id);
            prev = id;
        }
        Tri(c0, prev, first);
    }

    public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col) { Poly(new[] { a, b, c, d }, col); }

    public void RoundRect(float cx, float cy, float w, float h, float r, Color col, int seg = 5)
    {
        r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
        float hx = w * 0.5f - r, hy = h * 0.5f - r;
        var pts = new List<Vector2>();
        Corner(pts, cx + hx, cy + hy, r, 0f, seg);
        Corner(pts, cx - hx, cy + hy, r, 90f, seg);
        Corner(pts, cx - hx, cy - hy, r, 180f, seg);
        Corner(pts, cx + hx, cy - hy, r, 270f, seg);
        Poly(pts, col);
    }

    static void Corner(List<Vector2> pts, float x, float y, float r, float startDeg, int seg)
    {
        for (int i = 0; i <= seg; i++)
        {
            float a = (startDeg + 90f * i / seg) * Mathf.Deg2Rad;
            pts.Add(new Vector2(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r));
        }
    }

    // Garis tebal dengan banyak titik (opsional meruncing di ujung).
    public void Polyline(IList<Vector2> p, float thick, Color col, bool taper = false)
    {
        int n = p.Count;
        if (n < 2) return;
        int start = _v.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 dir = i == 0 ? p[1] - p[0] : (i == n - 1 ? p[n - 1] - p[n - 2] : p[i + 1] - p[i - 1]);
            dir.Normalize();
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            float tt = i / (float)(n - 1);
            float th = thick * (taper ? Mathf.Lerp(0.25f, 1f, Mathf.Sin(tt * Mathf.PI)) : 1f) * 0.5f;
            V(p[i].x + nrm.x * th, p[i].y + nrm.y * th, col);
            V(p[i].x - nrm.x * th, p[i].y - nrm.y * th, col);
        }
        for (int i = 0; i < n - 1; i++)
        {
            int a = start + i * 2;
            Tri(a, a + 2, a + 1); Tri(a + 1, a + 2, a + 3);
        }
    }

    public void Line(Vector2 a, Vector2 b, float thick, Color col) { Polyline(new[] { a, b }, thick, col); }

    public static List<Vector2> ArcPoints(float cx, float cy, float rx, float ry, float a0, float a1, int n)
    {
        var l = new List<Vector2>();
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
            l.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
        }
        return l;
    }

    public Mesh ToMesh(string name)
    {
        var m = new Mesh { name = name };
        m.SetVertices(_v);
        m.SetColors(_c);
        m.SetTriangles(_t, 0);
        m.RecalculateBounds();
        return m;
    }
}

// ------------------------------------------------------------------------------------
//  RIG - dimensi anatomi + transform bone
// ------------------------------------------------------------------------------------
public class IndRig
{
    public Transform[] t = new Transform[IndBoneUtil.Count];
    public Vector3[] restPos = new Vector3[IndBoneUtil.Count];
    public float[] restRot = new float[IndBoneUtil.Count];

    public float H;                    // tinggi 1 kepala (satuan dasar proporsi)
    public float hipY, thighLen, shinLen, ankleH, upperArmLen, foreArmLen, handLen;
    public float shoulderX, shoulderY, hipX, neckY, headY;
    public IndProfile torso, neck, head, upperArm, foreArm, hand, thigh, shin;

    public Transform Get(IndBone b) { return t[(int)b]; }
    public float TorsoHalf(float y) { return torso.Eval(y); }
    public float HeadHalf(float y) { return head.Eval(y); }

    public const float EyeY = 0.53f;   // tinggi mata di dalam kepala (satuan H)

    public float TK;                   // peregangan vertikal torso (dipakai saat bake mesh, bone tidak di-scale)
    public float legSplay;             // sudut kaki terbuka (derajat) pada pose istirahat
    public IndProfile shinSkin;        // betis telanjang (lebih ramping dari celana)

    // Proporsi diukur dari gambar referensi flat-vector (H = tinggi tengkorak, dagu ke ubun-ubun).
    //   Average: selangkangan 2,67 H, pangkal leher 3,26 H di atas sendi pinggul, total ~7,2 H
    //   Slim   : selangkangan 3,50 H, pangkal leher 2,90 H di atas sendi pinggul, total ~7,7 H
    public void Setup(IndBodyConfig b)
    {
        bool f = b.gender == IndGender.Female;
        bool slim = b.build == IndBuild.Slim;
        float hipH = (slim ? 3.5f : 2.67f) * b.legLength + 0.10f;       // tinggi sendi pinggul (satuan H)
        float collarH = slim ? 2.90f : 3.26f;                           // pangkal leher di atas sendi pinggul (satuan H)
        TK = collarH / 2.62f;
        legSplay = 10f;
        H = b.height / (hipH + 2.55f * TK + 1.22f);
        hipY = hipH * H;
        ankleH = 0.28f * H;
        float legLen = (hipY - ankleH) / Mathf.Cos(legSplay * Mathf.Deg2Rad);
        thighLen = legLen * 0.505f; shinLen = legLen - thighLen;
        upperArmLen = 1.25f * H; foreArmLen = 1.1f * H; handLen = 0.7f * H;
        neckY = 2.55f * TK * H; headY = 0.22f * H; shoulderY = (collarH - 0.46f) * H;

        // Torso (koordinat y belum diregangkan, 0 = selangkangan .. 2,62 = pangkal leher). Hampir lurus, tanpa pinggang.
        float[] ty = { 0f, 0.3f, 0.7f, 1.05f, 1.45f, 1.85f, 2.15f, 2.35f, 2.48f, 2.57f, 2.62f };
        float[] tw = f
            ? new[] { 0.62f, 0.66f, 0.60f, 0.54f, 0.58f, 0.64f, 0.68f, 0.68f, 0.62f, 0.42f, 0.25f }
            : new[] { 0.74f, 0.75f, 0.73f, 0.72f, 0.76f, 0.80f, 0.84f, 0.84f, 0.78f, 0.52f, 0.30f };
        var yw = new float[ty.Length * 2];
        for (int i = 0; i < ty.Length; i++)
        {
            float k = Mathf.Lerp(b.girth, b.shoulderWidth, Mathf.SmoothStep(0f, 1f, (ty[i] - 1.2f) / 1.0f));
            yw[i * 2] = ty[i]; yw[i * 2 + 1] = tw[i] * k;
        }
        torso = new IndProfile(H, H, yw);

        head = new IndProfile(H, H * (f ? 0.95f : 1f),
            0f, 0.10f, 0.05f, 0.19f, 0.16f, 0.285f, 0.33f, 0.345f, 0.52f, 0.37f,
            0.74f, 0.372f, 0.90f, 0.34f, 0.96f, 0.28f, 0.99f, 0.19f, 1.0f, 0f);
        neck = new IndProfile(H, H * (f ? 0.85f : 1f), -0.3f, 0.20f, 0.2f, 0.185f, 0.5f, 0.18f);

        float a = (f ? 0.88f : 1f) * b.armThickness;
        upperArm = new IndProfile(1f, H * a, 0f, 0.27f, 0.3f, 0.27f, 0.7f, 0.255f, 1f, 0.245f);
        foreArm = new IndProfile(1f, H * a, 0f, 0.235f, 0.25f, 0.235f, 0.7f, 0.19f, 1f, 0.16f);
        hand = new IndProfile(1f, H * a, 0f, 0.15f, 0.12f, 0.17f, 0.45f, 0.19f, 0.8f, 0.17f, 0.95f, 0.11f, 1f, 0.05f);

        float tl = Mathf.Lerp(1f, b.girth, 0.5f);
        thigh = new IndProfile(1f, H * tl, 0f, 0.40f, 0.5f, 0.34f, 1f, 0.30f);
        shin = new IndProfile(1f, H * tl, 0f, 0.30f, 0.5f, 0.29f, 1f, 0.28f);
        shinSkin = new IndProfile(1f, H * tl, 0f, 0.27f, 0.4f, 0.22f, 1f, 0.17f);
        hipX = 0.40f * H * tl;
        shoulderX = (f ? 0.72f : 0.88f) * H * b.shoulderWidth;
    }
}

// ------------------------------------------------------------------------------------
//  KONFIGURASI
// ------------------------------------------------------------------------------------
[Serializable]
public class IndBodyConfig
{
    public IndGender gender = IndGender.Male;
    public IndBuild build = IndBuild.Average;
    [Range(1.4f, 2.0f)] public float height = 1.75f;
    [Range(0.85f, 1.2f)] public float shoulderWidth = 1f;
    [Range(0.8f, 1.5f)] public float girth = 1f;          // lingkar badan / perut
    [Range(0.92f, 1.08f)] public float legLength = 1f;
    [Range(0.8f, 1.3f)] public float armThickness = 1f;
    public Color skin = new Color(0.86f, 0.66f, 0.50f);
    public Color eye = new Color(0.25f, 0.16f, 0.10f);
    public IndHairStyle hairStyle = IndHairStyle.Short;
    [IndHairId] public string customHairId = "";                       // isi untuk memakai gaya rambut buatan sendiri
    public Color hair = new Color(0.10f, 0.08f, 0.07f);    // untuk Hijab = warna kain
    public IndFacialHair facialHair = IndFacialHair.None;
}

[Serializable]
public class IndEquipEntry
{
    [IndPartId] public string partId;
    public Color color;     // alpha 0 = pakai warna bawaan part
    public Color accent;    // alpha 0 = pakai warna aksen bawaan part
}

// ------------------------------------------------------------------------------------
//  PART (APD / PAKAIAN) - modular lewat registry
// ------------------------------------------------------------------------------------
public delegate void IndPartBuild(IndPartContext c);

public class IndPartDef
{
    public string id, displayName;
    public PPEZone zone;
    public string slot;                                  // slot sama = saling menggantikan
    public string[] replaces = new string[0];            // slot lain yang ikut dilepas
    public Color defaultMain = Color.white, defaultAccent = Color.gray;
    public bool coversHairTop;                           // true = rambut atas disembunyikan (helm)
    public IndPartBuild build;
}

public class IndPartContext
{
    public IndRig rig;
    public IndBodyConfig body;
    public Color main, accent;
    public Action<IndBone, int, Action<IndMeshBuilder>> addAction;

    public float H { get { return rig.H; } }

    // Tambah satu mesh ke bone. draw dijalankan langsung.
    public void Add(IndBone bone, int order, Action<IndMeshBuilder> draw) { addAction(bone, order, draw); }

    // Tambah mesh ke bone kiri & kanan. side = -1 (kiri layar) / +1 (kanan layar).
    public void AddSides(IndBone leftBone, int order, Action<IndMeshBuilder, int> draw)
    {
        Add(leftBone, order, mb => draw(mb, -1));
        Add(IndBoneUtil.Right(leftBone), order, mb => draw(mb, +1));
    }
}

public static class IndPartLibrary
{
    static readonly Dictionary<string, IndPartDef> map = new Dictionary<string, IndPartDef>();
    static bool init;

    static void EnsureInit() { if (init) return; init = true; IndBuiltInParts.RegisterAll(); IndAutoRegistrar.Run(); }

    public static void Register(IndPartDef d) { EnsureInit(); map[d.id] = d; }
    public static IndPartDef Get(string id)
    {
        EnsureInit();
        IndPartDef d; map.TryGetValue(id ?? "", out d); return d;
    }
    public static IEnumerable<IndPartDef> All() { EnsureInit(); return map.Values; }
    public static List<IndPartDef> ByZone(PPEZone z)
    {
        EnsureInit();
        var l = new List<IndPartDef>();
        foreach (var d in map.Values) if (d.zone == z) l.Add(d);
        return l;
    }
}

// ------------------------------------------------------------------------------------
//  RAMBUT - modular lewat registry
// ------------------------------------------------------------------------------------
public class IndHairContext
{
    public IndRig rig; public Color color; public bool headgear;
    public Action<int, Action<IndMeshBuilder>> addAction;
    public float H { get { return rig.H; } }
    // Tambah mesh ke bone Head pada layer tertentu.
    public void Add(int order, Action<IndMeshBuilder> draw) { addAction(order, draw); }
}

public static class IndHairLibrary
{
    static readonly Dictionary<string, Action<IndHairContext>> map = new Dictionary<string, Action<IndHairContext>>();
    static bool init;
    static void EnsureInit() { if (init) return; init = true; IndBuiltInHair.RegisterAll(); IndAutoRegistrar.Run(); }
    public static void Register(string id, Action<IndHairContext> build) { EnsureInit(); map[id] = build; }
    public static IEnumerable<string> Ids() { EnsureInit(); return map.Keys; }
    public static Action<IndHairContext> Get(string id)
    {
        EnsureInit();
        Action<IndHairContext> a; map.TryGetValue(id ?? "", out a); return a;
    }
}

// ------------------------------------------------------------------------------------
//  POSE & ANIMASI - modular lewat registry
// ------------------------------------------------------------------------------------
public class IndPose
{
    public Vector2[] pos = new Vector2[IndBoneUtil.Count];
    public float[] rot = new float[IndBoneUtil.Count];
    public Vector2[] scl = new Vector2[IndBoneUtil.Count];
    public bool[] has = new bool[IndBoneUtil.Count];

    public IndPose() { Reset(); }

    public void Reset()
    {
        for (int i = 0; i < IndBoneUtil.Count; i++) { pos[i] = Vector2.zero; rot[i] = 0f; scl[i] = Vector2.one; has[i] = false; }
    }
    public void Pos(IndBone b, float x, float y) { pos[(int)b] = new Vector2(x, y); has[(int)b] = true; }
    public void Rot(IndBone b, float deg) { rot[(int)b] = deg; has[(int)b] = true; }
    public void Scl(IndBone b, float x, float y) { scl[(int)b] = new Vector2(x, y); has[(int)b] = true; }

    public static void Blend(IndPose a, IndPose b, float t, IndPose r)
    {
        for (int i = 0; i < IndBoneUtil.Count; i++)
        {
            r.pos[i] = Vector2.Lerp(a.pos[i], b.pos[i], t);
            r.rot[i] = Mathf.Lerp(a.rot[i], b.rot[i], t);
            r.scl[i] = Vector2.Lerp(a.scl[i], b.scl[i], t);
            r.has[i] = a.has[i] || b.has[i];
        }
    }

    // Timpa hanya bone yang disentuh pose o (untuk animasi overlay).
    public void Overlay(IndPose o, float w)
    {
        for (int i = 0; i < IndBoneUtil.Count; i++)
        {
            if (!o.has[i]) continue;
            pos[i] = Vector2.Lerp(pos[i], o.pos[i], w);
            rot[i] = Mathf.Lerp(rot[i], o.rot[i], w);
            scl[i] = Vector2.Lerp(scl[i], o.scl[i], w);
        }
    }
}

public abstract class IndAnimation
{
    public abstract string Name { get; }
    public virtual float Duration { get { return 1f; } }      // detik
    public virtual bool Loop { get { return true; } }
    // t = 0..1 (waktu ternormalisasi). Tulis hasil ke pose; posisi dalam satuan dunia (pakai rig.H).
    public abstract void Evaluate(float t, IndPose pose, IndRig rig);
}

public class IndLambdaAnim : IndAnimation
{
    readonly string _name; readonly float _dur; readonly bool _loop; readonly Action<float, IndPose, IndRig> _fn;
    public IndLambdaAnim(string name, float duration, bool loop, Action<float, IndPose, IndRig> fn)
    { _name = name; _dur = duration; _loop = loop; _fn = fn; }
    public override string Name { get { return _name; } }
    public override float Duration { get { return _dur; } }
    public override bool Loop { get { return _loop; } }
    public override void Evaluate(float t, IndPose pose, IndRig rig) { _fn(t, pose, rig); }
}

public static class IndAnimLibrary
{
    static readonly Dictionary<string, IndAnimation> map = new Dictionary<string, IndAnimation>();
    static bool init;
    static void EnsureInit() { if (init) return; init = true; IndBuiltInAnims.RegisterAll(); IndAutoRegistrar.Run(); }
    public static void Register(IndAnimation a) { EnsureInit(); map[a.Name] = a; }
    public static IndAnimation Get(string name)
    {
        EnsureInit();
        IndAnimation a; map.TryGetValue(name ?? "", out a); return a;
    }
    public static IEnumerable<string> Names() { EnsureInit(); return map.Keys; }
}

// ------------------------------------------------------------------------------------
//  ANIMASI BAWAAN
//  Karena tampak depan, ayunan maju/mundur dibuat dengan foreshortening (scale Y)
//  dan angkat kaki dengan menekuk (memendekkan) shin.
// ------------------------------------------------------------------------------------
public static class IndBuiltInAnims
{
    const float TAU = Mathf.PI * 2f;

    static float Env(float t) { return Mathf.SmoothStep(0f, 1f, t / 0.2f) * Mathf.SmoothStep(0f, 1f, (1f - t) / 0.2f); }

    public static void RegisterAll()
    {
        IndAnimLibrary.Register(new IndLambdaAnim("Idle", 6f, true, Idle));
        IndAnimLibrary.Register(new IndLambdaAnim("Walk", 0.9f, true, (t, p, r) => Gait(t, p, r, 0.55f, 0.05f, 0.6f, 0f)));
        IndAnimLibrary.Register(new IndLambdaAnim("Run", 0.5f, true, (t, p, r) => Gait(t, p, r, 0.85f, 0.10f, 0.9f, 0.25f)));
        IndAnimLibrary.Register(new IndLambdaAnim("Wave", 1.6f, false, Wave));      // overlay: lambai tangan kanan
        IndAnimLibrary.Register(new IndLambdaAnim("Cheer", 1.4f, false, Cheer));    // overlay: angkat kedua tangan
        IndAnimLibrary.Register(new IndLambdaAnim("Nod", 0.9f, false, Nod));        // overlay: mengangguk (benar)
        IndAnimLibrary.Register(new IndLambdaAnim("Shake", 0.9f, false, Shake));    // overlay: menggeleng (salah)
    }

    // Idle hidup: napas, geser berat badan, kepala menoleh pelan, lengan sedikit goyang.
    static void Idle(float t, IndPose p, IndRig r)
    {
        float H = r.H;
        float sway = Mathf.Sin(t * TAU);                 // geser berat badan (1 siklus / 6 dtk)
        float br = Mathf.Sin(t * TAU * 2f);              // napas (2 siklus / 6 dtk)
        float look = Mathf.Sin(t * TAU + 1.3f);
        p.Rot(IndBone.Torso, -sway * 1.3f);
        p.Scl(IndBone.Torso, 1f, 1f + 0.007f * br);
        p.Rot(IndBone.Head, sway * 1.6f + look * 1.0f);
        p.Pos(IndBone.Head, 0f, 0.004f * H * br);
        float arm = Mathf.Sin(t * TAU * 2f + 0.5f);
        p.Rot(IndBone.UpperArmL, arm * 1.0f - sway * 0.8f);
        p.Rot(IndBone.UpperArmR, -arm * 1.0f - sway * 0.8f);
        p.Rot(IndBone.ForeArmL, Mathf.Sin(t * TAU * 2f + 1.0f) * 1.5f);
        p.Rot(IndBone.ForeArmR, -Mathf.Sin(t * TAU * 2f + 1.0f) * 1.5f);
    }

    // Gaya berjalan/berlari tampak depan. Tangan & kaki dikompensasi scale-nya di ApplyPose (tidak gepeng).
    static void Gait(float t, IndPose p, IndRig r, float legAmp, float bob, float armAmp, float elbow)
    {
        float H = r.H, phi = t * TAU;
        float bs = Mathf.Sin(phi);
        p.Pos(IndBone.Hips, bs * 0.02f * H, -Mathf.Abs(bs) * bob * H);
        p.Rot(IndBone.Torso, bs * 2.0f);
        p.Rot(IndBone.Head, -bs * 1.6f);
        for (int s = -1; s <= 1; s += 2)
        {
            float ph = phi + (s < 0 ? 0f : Mathf.PI);
            float sw = Mathf.Sin(ph), fw = Mathf.Max(0f, Mathf.Cos(ph));
            IndBone th = s < 0 ? IndBone.ThighL : IndBone.ThighR;
            IndBone sh = s < 0 ? IndBone.ShinL : IndBone.ShinR;
            IndBone ua = s < 0 ? IndBone.UpperArmL : IndBone.UpperArmR;
            IndBone fa = s < 0 ? IndBone.ForeArmL : IndBone.ForeArmR;
            p.Scl(th, 1f, Mathf.Cos(sw * legAmp * 0.8f) - 0.08f * fw);
            p.Scl(sh, 1f, 1f - 0.26f * fw);
            p.Rot(th, s * 1.5f * fw);                                   // lutut sedikit keluar saat diangkat
            float armSw = -sw, armFw = Mathf.Max(0f, -Mathf.Cos(ph));
            p.Scl(ua, 1f, Mathf.Cos(armSw * armAmp * 0.8f));
            p.Rot(ua, s * 1.5f);
            p.Scl(fa, 1f, 1f - elbow * 0.6f - 0.14f * armFw);
            p.Rot(fa, -s * (6f * armFw + elbow * 55f));                 // siku menekuk ke dalam
        }
    }

    static void Wave(float t, IndPose p, IndRig r)
    {
        float e = Env(t);
        p.Rot(IndBone.UpperArmR, 125f * e);
        p.Rot(IndBone.ForeArmR, 15f * e + Mathf.Sin(t * TAU * 3f) * 20f * e);
    }

    static void Cheer(float t, IndPose p, IndRig r)
    {
        float e = Env(t);
        p.Rot(IndBone.UpperArmL, -155f * e); p.Rot(IndBone.UpperArmR, 155f * e);
        p.Rot(IndBone.ForeArmL, -10f * e); p.Rot(IndBone.ForeArmR, 10f * e);
        p.Pos(IndBone.Hips, 0f, Mathf.Abs(Mathf.Sin(t * TAU * 2f)) * 0.10f * r.H * e);
    }

    static void Nod(float t, IndPose p, IndRig r)
    {
        float s = Mathf.Sin(t * TAU * 2f) * Env(t);
        p.Pos(IndBone.Head, 0f, -0.045f * r.H * Mathf.Max(0f, s));
        p.Scl(IndBone.Head, 1f, 1f - 0.04f * Mathf.Max(0f, s));
    }

    static void Shake(float t, IndPose p, IndRig r)
    {
        float s = Mathf.Sin(t * TAU * 3f) * Env(t);
        p.Pos(IndBone.Head, 0.05f * r.H * s, 0f);
        p.Scl(IndBone.Head, 1f - 0.04f * Mathf.Abs(s), 1f);
        p.Rot(IndBone.Head, -4f * s);
    }
}

// ------------------------------------------------------------------------------------
//  RAMBUT BAWAAN
// ------------------------------------------------------------------------------------
public static class IndBuiltInHair
{
    public static void RegisterAll()
    {
        IndHairLibrary.Register("Bald", c => { });
        IndHairLibrary.Register("Buzz", c =>
        {
            float H = c.H; Color col = IndColor.Shade(c.color, 0.95f);
            if (!c.headgear) c.Add(IndLayer.HairFront, mb => Cap(mb, c.rig, 0.80f, 0.012f, col));
            c.Add(IndLayer.HairFront, mb => Sides(mb, c.rig, 0.60f, 0.80f, 0.012f * H, col));
        });
        IndHairLibrary.Register("Short", c =>
        {
            float H = c.H; Color col = c.color;
            c.Add(IndLayer.HairFront, mb =>
            {
                if (!c.headgear)
                {
                    Cap(mb, c.rig, 0.76f, 0.03f, col);
                    mb.Ellipse(0f, 0.99f * H, 0.35f * H, 0.10f * H, col);
                    mb.Poly(new[] { new Vector2(-0.36f * H, 0.80f * H), new Vector2(-0.30f * H, 0.92f * H), new Vector2(0.30f * H, 0.92f * H),
                                    new Vector2(0.36f * H, 0.80f * H), new Vector2(0.12f * H, 0.77f * H), new Vector2(-0.12f * H, 0.80f * H) }, col);
                }
                Sides(mb, c.rig, 0.55f, 0.80f, 0.03f * H, col);
            });
        });
        IndHairLibrary.Register("SidePart", c =>
        {
            float H = c.H; Color col = c.color;
            c.Add(IndLayer.HairFront, mb =>
            {
                if (!c.headgear)
                {
                    Cap(mb, c.rig, 0.74f, 0.035f, col);
                    mb.Poly(new[] { new Vector2(-0.37f * H, 0.82f * H), new Vector2(-0.20f * H, 0.95f * H), new Vector2(0.36f * H, 0.92f * H),
                                    new Vector2(0.37f * H, 0.80f * H), new Vector2(0.08f * H, 0.84f * H), new Vector2(-0.10f * H, 0.76f * H) }, col);
                    mb.Line(new Vector2(-0.14f * H, 1.0f * H), new Vector2(-0.07f * H, 0.88f * H), 0.012f * H, IndColor.Shade(col, 0.55f));
                }
                Sides(mb, c.rig, 0.55f, 0.80f, 0.03f * H, col);
            });
        });
        IndHairLibrary.Register("Curly", c =>
        {
            float H = c.H; Color col = c.color;
            c.Add(IndLayer.HairFront, mb =>
            {
                if (!c.headgear) Cap(mb, c.rig, 0.78f, 0.02f, col);
                for (int a = 0; a <= 180; a += 20)
                {
                    bool side = a <= 30 || a >= 150;
                    if (c.headgear && !side) continue;
                    float rad = a * Mathf.Deg2Rad;
                    mb.Ellipse(Mathf.Cos(rad) * 0.44f * H, 0.60f * H + Mathf.Sin(rad) * 0.46f * H, 0.12f * H, 0.12f * H, col, 14);
                }
            });
        });
        IndHairLibrary.Register("Long", c =>
        {
            float H = c.H; IndRig r = c.rig; Color col = c.color;
            c.Add(IndLayer.HairBack, mb =>
            {
                Func<float, float> w = y => y >= 0.15f * H
                    ? r.HeadHalf(y) + 0.06f * H
                    : Mathf.Lerp(0.42f * H, r.HeadHalf(0.15f * H) + 0.06f * H, (y + 0.5f * H) / (0.65f * H));
                mb.Strip(0.95f * H, -0.5f * H, 24, y => -w(y), y => w(y), IndColor.Shade(col, 0.9f));
            });
            c.Add(IndLayer.HairFront, mb =>
            {
                if (!c.headgear)
                {
                    Cap(mb, r, 0.76f, 0.03f, col);
                    mb.Poly(new[] { new Vector2(-0.37f * H, 0.78f * H), new Vector2(-0.28f * H, 0.93f * H), new Vector2(0.28f * H, 0.93f * H),
                                    new Vector2(0.37f * H, 0.78f * H), new Vector2(0.14f * H, 0.80f * H), new Vector2(-0.14f * H, 0.78f * H) }, col);
                }
                // helai samping yang membingkai wajah
                mb.Strip(0.78f * H, 0.10f * H, 14, y => -(r.HeadHalf(y) + 0.045f * H), y => -(r.HeadHalf(y) - 0.075f * H), col);
                mb.Strip(0.78f * H, 0.10f * H, 14, y => r.HeadHalf(y) - 0.075f * H, y => r.HeadHalf(y) + 0.045f * H, col);
            });
        });
        IndHairLibrary.Register("Ponytail", c =>
        {
            float H = c.H; Color col = c.color;
            c.Add(IndLayer.HairFront, mb =>
            {
                if (!c.headgear)
                {
                    Cap(mb, c.rig, 0.76f, 0.03f, col);
                    mb.Poly(new[] { new Vector2(-0.36f * H, 0.80f * H), new Vector2(-0.30f * H, 0.92f * H), new Vector2(0.30f * H, 0.92f * H),
                                    new Vector2(0.36f * H, 0.80f * H), new Vector2(0.12f * H, 0.77f * H), new Vector2(-0.12f * H, 0.80f * H) }, col);
                }
                Sides(mb, c.rig, 0.55f, 0.80f, 0.03f * H, col);
                var tail = new List<Vector2> {
                    new Vector2(0.32f * H, 0.62f * H), new Vector2(0.44f * H, 0.40f * H), new Vector2(0.47f * H, 0.12f * H),
                    new Vector2(0.42f * H, -0.18f * H), new Vector2(0.36f * H, -0.40f * H) };
                mb.Polyline(tail, 0.17f * H, col, true);
                mb.Ellipse(0.33f * H, 0.60f * H, 0.05f * H, 0.05f * H, IndColor.Shade(col, 0.5f), 10);
            });
        });
        IndHairLibrary.Register("Bun", c =>
        {
            float H = c.H; Color col = c.color;
            c.Add(IndLayer.HairFront, mb =>
            {
                if (!c.headgear)
                {
                    Cap(mb, c.rig, 0.76f, 0.03f, col);
                    mb.Poly(new[] { new Vector2(-0.36f * H, 0.80f * H), new Vector2(-0.30f * H, 0.92f * H), new Vector2(0.30f * H, 0.92f * H),
                                    new Vector2(0.36f * H, 0.80f * H), new Vector2(0.12f * H, 0.77f * H), new Vector2(-0.12f * H, 0.80f * H) }, col);
                    mb.Ellipse(0f, 1.09f * H, 0.16f * H, 0.14f * H, col);
                }
                Sides(mb, c.rig, 0.55f, 0.80f, 0.03f * H, col);
            });
        });
        IndHairLibrary.Register("Hijab", c =>
        {
            float H = c.H; IndRig r = c.rig; Color col = c.color; Color dark = IndColor.Shade(col, 0.85f);
            // kain menjuntai di belakang kepala, menutup leher dan bahu
            c.Add(IndLayer.HijabBack, mb =>
            {
                Func<float, float> w = y => y >= 0.2f * H
                    ? r.HeadHalf(y) + 0.07f * H
                    : Mathf.Lerp(0.62f * H, r.HeadHalf(0.2f * H) + 0.07f * H, (y + 0.55f * H) / (0.75f * H));
                mb.Strip(0.93f * H, -0.55f * H, 24, y => -w(y), y => w(y), dark);
                mb.Ellipse(0f, 0.93f * H, r.HeadHalf(0.93f * H) + 0.07f * H, 0.08f * H, dark, 16, 0f, 0f, 180f);
            });
            // bingkai wajah
            c.Add(IndLayer.HairFront, mb =>
            {
                Cap(mb, r, 0.80f, 0.05f, col);
                mb.Strip(0.82f * H, 0.12f * H, 14, y => -(r.HeadHalf(y) + 0.05f * H), y => -(r.HeadHalf(y) - 0.045f * H), col);
                mb.Strip(0.82f * H, 0.12f * H, 14, y => r.HeadHalf(y) - 0.045f * H, y => r.HeadHalf(y) + 0.05f * H, col);
                mb.Polyline(IndMeshBuilder.ArcPoints(0f, 0.15f * H, r.HeadHalf(0.15f * H) + 0.01f * H, 0.17f * H, 190f, 350f, 12), 0.07f * H, col);
            });
        });
    }

    // Tutup kepala dari y0 (x H) sampai atas. padFrac = tebal rambut dalam satuan H.
    static void Cap(IndMeshBuilder mb, IndRig r, float y0, float padFrac, Color col)
    {
        float H = r.H, pad = padFrac * H;
        mb.Tube(r.head, 0f, y0 * H, 0.93f * H, pad, col, 14);
        mb.Ellipse(0f, 0.93f * H, r.HeadHalf(0.93f * H) + pad, 0.075f * H + pad, col, 16, 0f, 0f, 180f);
    }

    // Rambut di sisi kepala (cambang / sisa rambut di bawah helm).
    static void Sides(IndMeshBuilder mb, IndRig r, float y0, float y1, float pad, Color col)
    {
        float H = r.H;
        mb.Strip(y0 * H, y1 * H, 8, y => -(r.HeadHalf(y) + pad), y => -(r.HeadHalf(y) - 0.055f * H), col);
        mb.Strip(y0 * H, y1 * H, 8, y => r.HeadHalf(y) - 0.055f * H, y => r.HeadHalf(y) + pad, col);
    }
}

// ------------------------------------------------------------------------------------
//  PART BAWAAN (APD + PAKAIAN)
// ------------------------------------------------------------------------------------
public static class IndBuiltInParts
{
    static void Reg(string id, string name, PPEZone zone, string slot, Color main, Color accent, IndPartBuild build,
                    string[] replaces = null, bool coversHair = false)
    {
        IndPartLibrary.Register(new IndPartDef
        {
            id = id, displayName = name, zone = zone, slot = slot,
            defaultMain = main, defaultAccent = accent, build = build,
            replaces = replaces ?? new string[0], coversHairTop = coversHair
        });
    }

    public static void RegisterAll()
    {
        // ---- Kepala
        Reg("hardhat", "Helm Safety", PPEZone.Head, "head", IndColor.C(250, 200, 30), IndColor.C(60, 60, 60), c => Hardhat(c, false), null, true);
        Reg("hardhat_strap", "Helm Safety + Tali Dagu", PPEZone.Head, "head", IndColor.C(250, 200, 30), IndColor.C(40, 40, 40), c => Hardhat(c, true), null, true);
        // ---- Mata
        Reg("safety_glasses", "Kacamata Safety", PPEZone.Eye, "eye", IndColor.C(190, 235, 255, 90), IndColor.C(50, 50, 55), Glasses);
        Reg("goggles", "Goggles", PPEZone.Eye, "eye", IndColor.C(190, 235, 255, 90), IndColor.C(28, 40, 92), Goggles);
        // ---- Pernapasan
        Reg("dust_mask", "Masker Debu", PPEZone.Respiratory, "face", IndColor.C(225, 238, 245), IndColor.C(150, 160, 165), DustMask);
        Reg("half_respirator", "Respirator Half-Face", PPEZone.Respiratory, "face", IndColor.C(80, 88, 98), IndColor.C(240, 190, 30), Respirator);
        // ---- Telinga
        Reg("ear_plug", "Ear Plug", PPEZone.Ear, "ear", IndColor.C(255, 140, 0), IndColor.C(255, 200, 120), EarPlug);
        Reg("ear_muff", "Ear Muff", PPEZone.Ear, "ear", IndColor.C(45, 48, 55), IndColor.C(230, 190, 40), EarMuff);
        // ---- Badan
        Reg("shirt_short", "Kaos / Polo Lengan Pendek", PPEZone.Body, "inner", IndColor.C(60, 110, 170), IndColor.C(235, 235, 235), c => Shirt(c, false), new[] { "outfit" });
        Reg("shirt_long", "Kemeja Lengan Panjang", PPEZone.Body, "inner", IndColor.C(70, 120, 175), IndColor.C(235, 235, 235), c => Shirt(c, true), new[] { "outfit" });
        Reg("pants_work", "Celana Kerja", PPEZone.Body, "legs", IndColor.C(45, 55, 75), IndColor.C(30, 30, 30), Pants, new[] { "outfit" });
        Reg("coverall", "Wearpack / Coverall", PPEZone.Body, "outfit", IndColor.C(96, 118, 160), IndColor.C(178, 186, 202), Coverall, new[] { "inner", "legs" });
        Reg("vest", "Rompi Safety Reflektif", PPEZone.Body, "vest", IndColor.C(255, 115, 15), IndColor.C(215, 222, 228), Vest);
        // ---- Tangan
        Reg("gloves", "Sarung Tangan", PPEZone.Hand, "hands", IndColor.C(34, 37, 54), IndColor.C(62, 68, 92), Gloves);
        // ---- Kaki
        Reg("safety_shoes", "Safety Shoes", PPEZone.Foot, "feet", IndColor.C(36, 41, 64), IndColor.C(150, 150, 155), c => SafetyBoot(c, 0.10f));
        Reg("safety_boots", "Safety Boots", PPEZone.Foot, "feet", IndColor.C(36, 41, 64), IndColor.C(150, 150, 155), c => SafetyBoot(c, 0.30f));
        Reg("shoes_casual", "Sepatu Kasual", PPEZone.Foot, "feet", IndColor.C(30, 33, 75), IndColor.C(30, 33, 75), c => Shoes(c, false));
        // ---- Pelindung jatuh
        Reg("harness", "Full Body Harness", PPEZone.FallProtection, "harness", IndColor.C(250, 198, 40), IndColor.C(34, 44, 94), Harness);
        // ---- Lainnya
        Reg("id_card", "ID Card + Lanyard", PPEZone.Other, "badge", IndColor.C(240, 240, 240), IndColor.C(200, 40, 40), IdCard);
    }

    // =============================== KEPALA ===============================
    static void Hardhat(IndPartContext c, bool strap)
    {
        float H = c.H; Color m = c.main, dark = IndColor.Shade(m, 0.78f), shadow = IndColor.Shade(m, 0.88f), light = IndColor.Shade(m, 1.15f);
        c.Add(IndBone.Head, IndLayer.Helmet, mb =>
        {
            mb.Ellipse(0f, 0.80f * H, 0.585f * H, 0.075f * H, dark, 30);                                  // brim lebar (sisi bawah)
            mb.Ellipse(0f, 0.82f * H, 0.50f * H, 0.47f * H, m, 36, 0f, 0f, 180f);                          // kubah
            mb.RoundRect(0f, 0.835f * H, 0.99f * H, 0.055f * H, 0.025f * H, shadow);                       // pita gelap di dasar kubah
            mb.RoundRect(0f, 1.04f * H, 0.115f * H, 0.45f * H, 0.055f * H, light);                          // rib tengah
            for (int s = -1; s <= 1; s += 2)                                                               // rib samping
                mb.Polyline(new[] { new Vector2(s * 0.34f * H, 0.86f * H), new Vector2(s * 0.31f * H, 1.06f * H), new Vector2(s * 0.17f * H, 1.23f * H) }, 0.04f * H, light, true);
            mb.Polyline(IndMeshBuilder.ArcPoints(0f, 0.82f * H, 0.44f * H, 0.41f * H, 118f, 152f, 6), 0.03f * H, IndColor.Shade(m, 1.25f), true);   // kilau
        });
        if (strap)
            c.Add(IndBone.Head, IndLayer.Helmet + 1, mb =>
            {
                for (int s = -1; s <= 1; s += 2)
                    mb.Polyline(new[] { new Vector2(s * 0.40f * H, 0.78f * H), new Vector2(s * 0.35f * H, 0.45f * H), new Vector2(s * 0.30f * H, 0.12f * H) }, 0.022f * H, c.accent);
                mb.Polyline(IndMeshBuilder.ArcPoints(0f, 0.12f * H, 0.30f * H, 0.14f * H, 200f, 340f, 10), 0.03f * H, c.accent);
            });
    }

    // =============================== MATA ===============================
    static void Glasses(IndPartContext c)
    {
        float H = c.H;
        c.Add(IndBone.Head, IndLayer.Eyewear, mb =>
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float cx = s * 0.15f * H, cy = 0.53f * H;
                mb.RoundRect(cx, cy, 0.215f * H, 0.125f * H, 0.045f * H, c.accent);
                mb.RoundRect(cx, cy, 0.19f * H, 0.10f * H, 0.035f * H, c.main);
                mb.Line(new Vector2(s * 0.255f * H, 0.55f * H), new Vector2(s * 0.385f * H, 0.54f * H), 0.014f * H, c.accent);
            }
            mb.Line(new Vector2(-0.045f * H, 0.55f * H), new Vector2(0.045f * H, 0.55f * H), 0.014f * H, c.accent);
        });
    }

    // Goggles: lensa bening lebar 1 panel + bingkai & tali navy (terlihat jelas dari depan).
    static void Goggles(IndPartContext c)
    {
        float H = c.H; Color hi = IndColor.C(255, 255, 255, 170);
        c.Add(IndBone.Head, IndLayer.Eyewear, mb =>
        {
            for (int s = -1; s <= 1; s += 2)
                mb.Line(new Vector2(s * 0.36f * H, 0.54f * H), new Vector2(s * 0.46f * H, 0.56f * H), 0.06f * H, c.accent);   // tali
            mb.RoundRect(0f, 0.53f * H, 0.80f * H, 0.25f * H, 0.085f * H, c.accent);                                   // bingkai
            mb.RoundRect(0f, 0.53f * H, 0.72f * H, 0.185f * H, 0.065f * H, c.main);                                    // lensa
            mb.Line(new Vector2(-0.26f * H, 0.595f * H), new Vector2(-0.17f * H, 0.47f * H), 0.022f * H, hi);           // kilau
            mb.Line(new Vector2(-0.19f * H, 0.595f * H), new Vector2(-0.14f * H, 0.52f * H), 0.012f * H, hi);
        });
    }

    // =============================== PERNAPASAN ===============================
    static void DustMask(IndPartContext c)
    {
        float H = c.H; Color pleat = IndColor.Shade(c.main, 0.85f);
        c.Add(IndBone.Head, IndLayer.Mask, mb =>
        {
            for (int s = -1; s <= 1; s += 2)
                mb.Line(new Vector2(s * 0.26f * H, 0.30f * H), new Vector2(s * 0.385f * H, 0.48f * H), 0.012f * H, c.accent);   // tali telinga
            mb.Poly(new[] { new Vector2(-0.22f * H, 0.40f * H), new Vector2(0.22f * H, 0.40f * H), new Vector2(0.27f * H, 0.27f * H),
                            new Vector2(0.20f * H, 0.08f * H), new Vector2(-0.20f * H, 0.08f * H), new Vector2(-0.27f * H, 0.27f * H) }, c.main);
            for (int i = 0; i < 3; i++)
            {
                float y = (0.32f - i * 0.075f) * H;
                mb.Line(new Vector2(-0.23f * H, y), new Vector2(0.23f * H, y), 0.01f * H, pleat);
            }
        });
    }

    static void Respirator(IndPartContext c)
    {
        float H = c.H; Color rim = IndColor.Shade(c.main, 0.75f);
        c.Add(IndBone.Head, IndLayer.Mask, mb =>
        {
            for (int s = -1; s <= 1; s += 2)
                mb.Line(new Vector2(s * 0.28f * H, 0.30f * H), new Vector2(s * 0.395f * H, 0.55f * H), 0.03f * H, IndColor.Shade(c.main, 0.6f));
            mb.Poly(new[] { new Vector2(-0.19f * H, 0.42f * H), new Vector2(0.19f * H, 0.42f * H), new Vector2(0.28f * H, 0.28f * H),
                            new Vector2(0.22f * H, 0.07f * H), new Vector2(0f, 0.02f * H), new Vector2(-0.22f * H, 0.07f * H), new Vector2(-0.28f * H, 0.28f * H) }, c.main);
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Ellipse(s * 0.19f * H, 0.17f * H, 0.095f * H, 0.095f * H, rim, 20);
                mb.Ellipse(s * 0.19f * H, 0.17f * H, 0.075f * H, 0.075f * H, c.accent, 20);
            }
            mb.RoundRect(0f, 0.15f * H, 0.07f * H, 0.07f * H, 0.02f * H, rim);
        });
    }

    // =============================== TELINGA ===============================
    static void EarPlug(IndPartContext c)
    {
        float H = c.H;
        c.Add(IndBone.Head, IndLayer.Ear + 2, mb =>
        {
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Ellipse(s * 0.372f * H, 0.47f * H, 0.038f * H, 0.038f * H, c.main, 14);
                mb.Ellipse(s * 0.368f * H, 0.48f * H, 0.014f * H, 0.014f * H, c.accent, 8);
            }
        });
    }

    static void EarMuff(IndPartContext c)
    {
        float H = c.H;
        c.Add(IndBone.Head, IndLayer.MuffBand, mb =>
            mb.Polyline(IndMeshBuilder.ArcPoints(0f, 0.62f * H, 0.45f * H, 0.50f * H, 180f, 0f, 22), 0.05f * H, c.accent));
        c.Add(IndBone.Head, IndLayer.MuffCup, mb =>
        {
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Ellipse(s * 0.425f * H, 0.52f * H, 0.095f * H, 0.135f * H, IndColor.Shade(c.main, 0.8f), 20);
                mb.Ellipse(s * 0.435f * H, 0.52f * H, 0.07f * H, 0.11f * H, c.main, 20);
            }
        });
    }

    // =============================== BADAN ===============================
    static void ShirtTorso(IndPartContext c, Color m, float hem)
    {
        float H = c.H; IndRig r = c.rig;
        c.Add(IndBone.Torso, IndLayer.Shirt, mb =>
        {
            mb.Strip(hem * H, 2.60f * H, 26, y => -(r.TorsoHalf(y) + 0.03f * H), y => r.TorsoHalf(y) + 0.03f * H, m);
            mb.Strip(hem * H, (hem + 0.06f) * H, 2, y => -(r.TorsoHalf(y) + 0.03f * H), y => r.TorsoHalf(y) + 0.03f * H, IndColor.Shade(m, 0.88f));
        });
    }

    static void Collar(IndPartContext c, Color trim)
    {
        float H = c.H;
        c.Add(IndBone.Torso, IndLayer.Collar, mb =>
        {
            mb.Poly(new[] { new Vector2(-0.27f * H, 2.64f * H), new Vector2(-0.04f * H, 2.64f * H), new Vector2(-0.12f * H, 2.38f * H) }, trim);
            mb.Poly(new[] { new Vector2(0.27f * H, 2.64f * H), new Vector2(0.04f * H, 2.64f * H), new Vector2(0.12f * H, 2.38f * H) }, trim);
            mb.Line(new Vector2(0f, 2.40f * H), new Vector2(0f, 1.95f * H), 0.012f * H, IndColor.Shade(trim, 0.6f));
        });
    }

    static void Sleeves(IndPartContext c, Color m, Color trim, bool longSleeve)
    {
        float H = c.H; IndRig r = c.rig;
        c.AddSides(IndBone.UpperArmL, IndLayer.Sleeve, (mb, s) =>
        {
            float w = r.upperArm.Eval(0f) + 0.04f * H;
            mb.Ellipse(0f, 0f, w, w, m, 20);
            float f1 = longSleeve ? 1.0f : 0.40f;
            mb.Limb(r.upperArm, 0f, r.upperArmLen, -0.02f, f1, 0.04f * H, m, 10);
            if (!longSleeve) mb.Limb(r.upperArm, 0f, r.upperArmLen, f1 - 0.07f, f1, 0.05f * H, trim, 2);
            else mb.Ellipse(0f, -r.upperArmLen, r.upperArm.Eval(1f) + 0.04f * H, r.upperArm.Eval(1f) + 0.04f * H, m, 16);
        });
        if (longSleeve)
            c.AddSides(IndBone.ForeArmL, IndLayer.ForeSleeve, (mb, s) =>
            {
                mb.Limb(r.foreArm, 0f, r.foreArmLen, 0f, 0.93f, 0.04f * H, m, 10);
                mb.Limb(r.foreArm, 0f, r.foreArmLen, 0.88f, 0.98f, 0.05f * H, trim, 2);
            });
    }

    static void Shirt(IndPartContext c, bool longSleeve)
    {
        ShirtTorso(c, c.main, 0.85f);
        Collar(c, c.accent);
        Sleeves(c, c.main, c.accent, longSleeve);
        float H = c.H;
        if (longSleeve)
            c.Add(IndBone.Torso, IndLayer.Shirt + 1, mb =>
            {   // saku dada
                mb.RoundRect(-0.36f * H, 1.78f * H, 0.24f * H, 0.26f * H, 0.03f * H, IndColor.Shade(c.main, 0.9f));
                mb.Line(new Vector2(-0.46f * H, 1.88f * H), new Vector2(-0.26f * H, 1.88f * H), 0.012f * H, IndColor.Shade(c.main, 0.72f));
            });
    }

    // roll = ujung celana digulung (manset lebih terang & lebih lebar), pergelangan kaki terlihat seperti referensi.
    static void PantsLegs(IndPartContext c, Color m, Color cuff, bool roll, float hemUp = 0f)
    {
        float H = c.H; IndRig r = c.rig;
        c.AddSides(IndBone.ThighL, IndLayer.PantsLeg, (mb, s) => mb.Limb(r.thigh, 0f, r.thighLen, -0.1f, 1.0f, 0.02f * H, m, 10));
        c.AddSides(IndBone.ShinL, IndLayer.PantsLeg, (mb, s) =>
        {
            float end = roll ? 1f - 0.16f * H / r.shinLen : 1f - hemUp / r.shinLen;
            float w0 = r.shin.Eval(0f) + 0.02f * H;
            mb.Ellipse(0f, 0f, w0, w0, m, 16);
            mb.Limb(r.shin, 0f, r.shinLen, 0f, end, 0.02f * H, m, 10);
            if (roll)
            {
                float cs = end - 0.27f * H / r.shinLen;
                mb.Limb(r.shin, 0f, r.shinLen, cs, end, 0.075f * H, cuff, 3);
                float w = r.shin.Eval(cs) + 0.075f * H;
                mb.Line(new Vector2(-w, -cs * r.shinLen), new Vector2(w, -cs * r.shinLen), 0.008f * H, IndColor.Shade(cuff, 0.6f));
            }
            else mb.Limb(r.shin, 0f, r.shinLen, end - 0.06f * H / r.shinLen, end, 0.035f * H, cuff, 2);
        });
    }

    static void Pants(IndPartContext c)
    {
        float H = c.H; IndRig r = c.rig;
        c.Add(IndBone.Torso, IndLayer.PantsWaist, mb =>
        {
            mb.Strip(0f, 1.05f * H, 12, y => -(r.TorsoHalf(y) + 0.015f * H), y => r.TorsoHalf(y) + 0.015f * H, c.main);
            mb.Strip(0.93f * H, 1.03f * H, 2, y => -(r.TorsoHalf(y) + 0.02f * H), y => r.TorsoHalf(y) + 0.02f * H, c.accent);   // ikat pinggang
            mb.RoundRect(0f, 0.98f * H, 0.10f * H, 0.08f * H, 0.01f * H, IndColor.C(190, 190, 195));
        });
        PantsLegs(c, c.main, Color.Lerp(c.main, IndColor.C(165, 175, 215), 0.45f), true);
    }

    // Wearpack seperti referensi: kerah terbuka + kaos dalam gelap, plaket kancing, saku dada bertutup,
    // saku cargo di paha, manset, strip reflektif di lengan & kaki.
    static void Coverall(IndPartContext c)
    {
        float H = c.H; IndRig r = c.rig; Color m = c.main, ref1 = c.accent;
        Color shade = IndColor.Shade(m, 0.82f), deep = IndColor.Shade(m, 0.66f), hi = IndColor.Shade(m, 1.10f);
        Color under = IndColor.C(26, 29, 42), metal = IndColor.C(205, 210, 216);
        float k = 1f / r.TK;     // kompensasi peregangan vertikal torso supaya saku/kancing tidak lonjong
        Func<float, float> half = y => r.TorsoHalf(y) + 0.03f * H;
        c.Add(IndBone.Torso, IndLayer.Shirt, mb =>
        {
            mb.Strip(0f, 2.60f * H, 28, y => -half(y), y => half(y), m);
            mb.Strip(0.80f * H, 0.85f * H, 2, y => -half(y), y => half(y), shade);                       // jahitan pinggang
            mb.Strip(0.05f * H, 2.30f * H, 12, y => -0.03f * H, y => 0.03f * H, shade);                  // plaket tengah / resleting
            for (int i = 0; i < 4; i++) mb.Ellipse(0f, (2.05f - i * 0.27f) * H, 0.02f * H, 0.02f * H * k, deep, 8);    // kancing
            for (int s = -1; s <= 1; s += 2)
            {
                float cx = s * 0.60f * H;
                mb.RoundRect(cx, 1.40f * H, 0.26f * H, 0.28f * H * k, 0.02f * H, shade);                 // saku dada (di luar tali harness)
                mb.RoundRect(cx, 1.53f * H, 0.26f * H, 0.10f * H * k, 0.015f * H, deep);                 // tutup saku
                mb.Ellipse(cx, 1.53f * H, 0.018f * H, 0.018f * H * k, metal, 8);                         // kancing saku
            }
        });
        c.Add(IndBone.Torso, IndLayer.Collar, mb =>
        {
            mb.Poly(new[] { new Vector2(-0.15f * H, 2.62f * H), new Vector2(0.15f * H, 2.62f * H), new Vector2(0f, 2.18f * H) }, under);   // kaos dalam (V leher)
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Poly(new[] { new Vector2(s * 0.07f * H, 2.65f * H), new Vector2(s * 0.37f * H, 2.60f * H), new Vector2(s * 0.17f * H, 2.20f * H) }, hi);
                mb.Line(new Vector2(s * 0.07f * H, 2.65f * H), new Vector2(s * 0.17f * H, 2.20f * H), 0.012f * H, deep);
                mb.Line(new Vector2(s * 0.37f * H, 2.60f * H), new Vector2(s * 0.17f * H, 2.20f * H), 0.012f * H, deep);
            }
        });
        Sleeves(c, m, shade, true);
        c.AddSides(IndBone.UpperArmL, IndLayer.Sleeve + 1, (mb, s) => mb.Limb(r.upperArm, 0f, r.upperArmLen, 0.54f, 0.67f, 0.045f * H, ref1, 2));
        c.AddSides(IndBone.ForeArmL, IndLayer.ForeSleeve + 1, (mb, s) => mb.Limb(r.foreArm, 0f, r.foreArmLen, 0.30f, 0.44f, 0.045f * H, ref1, 2));
        PantsLegs(c, m, shade, false, 0.24f * H);
        c.AddSides(IndBone.ThighL, IndLayer.PantsPocket, (mb, s) =>
        {   // saku cargo di sisi luar paha
            float py = -0.42f * r.thighLen;
            mb.RoundRect(s * 0.15f * H, py, 0.30f * H, 0.46f * H, 0.02f * H, shade);
            mb.RoundRect(s * 0.15f * H, py + 0.18f * H, 0.30f * H, 0.10f * H, 0.015f * H, deep);
            mb.Ellipse(s * 0.15f * H, py + 0.18f * H, 0.018f * H, 0.018f * H, metal, 8);
        });
        c.AddSides(IndBone.ShinL, IndLayer.PantsPocket, (mb, s) => mb.Limb(r.shin, 0f, r.shinLen, 0.55f, 0.73f, 0.035f * H, ref1, 2));
    }

    static void Vest(IndPartContext c)
    {
        float H = c.H; IndRig r = c.rig;
        Func<float, float> opening = y => y > 2.0f * H ? Mathf.Lerp(0.02f * H, 0.26f * H, (y - 2.0f * H) / (0.62f * H)) : 0.02f * H;
        c.Add(IndBone.Torso, IndLayer.Vest, mb =>
        {
            for (int s = -1; s <= 1; s += 2)
            {
                int sg = s;
                Func<float, float> outer = y => r.TorsoHalf(y) + 0.045f * H;
                Func<float, float> a = y => sg < 0 ? -outer(y) : opening(y);
                Func<float, float> b = y => sg < 0 ? -opening(y) : outer(y);
                mb.Strip(0.75f * H, 2.60f * H, 24, a, b, c.main);
                // strip reflektif horizontal & vertikal
                mb.Strip(1.22f * H, 1.34f * H, 2, a, b, c.accent);
                mb.Strip(0.75f * H, 2.50f * H, 12, y => sg < 0 ? -0.40f * H : 0.30f * H, y => sg < 0 ? -0.30f * H : 0.40f * H, c.accent);
            }
        });
    }

    // =============================== TANGAN ===============================
    static void Gloves(IndPartContext c)
    {
        float H = c.H; IndRig r = c.rig; Color seam = Color.Lerp(c.main, IndColor.C(255, 255, 255), 0.16f);
        c.AddSides(IndBone.HandL, IndLayer.Glove, (mb, s) =>
        {
            mb.Limb(r.hand, 0f, r.handLen, -0.12f, 1.0f, 0.035f * H, c.main, 10);
            mb.Ellipse(-s * 0.18f * H, -0.28f * H, 0.08f * H, 0.125f * H, c.main, 14, -s * 15f);                  // jempol
            mb.Limb(r.hand, 0f, r.handLen, -0.16f, 0.14f, 0.06f * H, c.accent, 2);                                // manset
            for (int i = -1; i <= 1; i++)                                                                          // garis pemisah jari
                mb.Line(new Vector2(i * 0.085f * H, -0.40f * H), new Vector2(i * 0.095f * H, -0.64f * H), 0.012f * H, seam);
            mb.Line(new Vector2(-0.12f * H, -0.33f * H), new Vector2(0.12f * H, -0.33f * H), 0.012f * H, seam);    // buku jari
        });
    }

    // =============================== KAKI ===============================
    // Siluet kaki/sepatu tampak depan menghadap serong keluar (s = sisi layar: -1 kiri, +1 kanan).
    public static Vector2[] FootShape(int s, float H)
    {
        // sepatu pipih meruncing ke arah luar (u = jarak keluar dari sumbu kaki), seperti referensi
        float[,] u =
        {
            { -0.17f, -0.28f }, { -0.17f, -0.06f }, { 0.25f, 0.06f }, { 0.62f, -0.25f }, { 0.62f, -0.28f }
        };
        var l = new Vector2[5];
        for (int i = 0; i < 5; i++) l[i] = new Vector2(s * u[i, 0] * H, u[i, 1] * H);
        return l;
    }

    static void Shoes(IndPartContext c, bool boot)
    {
        float H = c.H; IndRig r = c.rig; Color sole = IndColor.Shade(c.main, 0.6f);
        c.AddSides(IndBone.FootL, IndLayer.Shoe, (mb, s) =>
        {
            mb.Poly(FootShape(s, H), c.main);                                                                  // bagian atas
            mb.Quad(new Vector2(s * -0.17f * H, -0.25f * H), new Vector2(s * 0.63f * H, -0.25f * H),
                    new Vector2(s * 0.63f * H, -0.28f * H), new Vector2(s * -0.17f * H, -0.28f * H), sole);    // sol
            mb.Poly(new[] { new Vector2(s * 0.40f * H, -0.066f * H), new Vector2(s * 0.62f * H, -0.25f * H),
                            new Vector2(s * 0.30f * H, -0.25f * H), new Vector2(s * 0.30f * H, -0.02f * H) }, c.accent);   // toe cap
        });
        if (boot)
            c.AddSides(IndBone.ShinL, IndLayer.Shoe + 1, (mb, s) =>
            {
                mb.Limb(r.shin, 0f, r.shinLen, 0.60f, 1.0f, 0.045f * H, c.main, 6);
                mb.Limb(r.shin, 0f, r.shinLen, 0.58f, 0.64f, 0.05f * H, IndColor.Shade(c.main, 0.7f), 2);
            });
    }

    // Sepatu/boot safety: telapak tebal, toe cap, tali, kerah empuk. shaftTop = tinggi batang di atas pergelangan (satuan H).
    // Layer di bawah celana (PantsLeg) supaya ujung celana menimpa bagian atas boot seperti referensi.
    static void SafetyBoot(IndPartContext c, float shaftTop)
    {
        float H = c.H; Color m = c.main, sole = IndColor.Shade(m, 0.45f);
        Color hi = Color.Lerp(m, IndColor.C(255, 255, 255), 0.22f), lace = Color.Lerp(m, IndColor.C(255, 255, 255), 0.45f);
        c.AddSides(IndBone.FootL, IndLayer.Boot, (mb, s) =>
        {
            float top = shaftTop * H;
            mb.RoundRect(0f, (top - 0.10f * H) * 0.5f, 0.52f * H, top + 0.10f * H, 0.05f * H, m);                            // batang boot
            mb.Poly(new[] { new Vector2(s * -0.26f * H, -0.28f * H), new Vector2(s * -0.26f * H, 0.02f * H), new Vector2(s * 0.30f * H, 0.02f * H),
                            new Vector2(s * 0.60f * H, -0.12f * H), new Vector2(s * 0.67f * H, -0.22f * H), new Vector2(s * 0.63f * H, -0.28f * H) }, m);   // telapak & punggung kaki
            mb.Poly(new[] { new Vector2(s * 0.42f * H, -0.04f * H), new Vector2(s * 0.62f * H, -0.14f * H), new Vector2(s * 0.68f * H, -0.20f * H), new Vector2(s * 0.38f * H, -0.20f * H) }, hi);   // toe cap
            mb.Quad(new Vector2(s * -0.27f * H, -0.20f * H), new Vector2(s * 0.69f * H, -0.20f * H),
                    new Vector2(s * 0.69f * H, -0.285f * H), new Vector2(s * -0.27f * H, -0.285f * H), sole);                 // sol tebal
            if (top > 0.2f * H)
                mb.RoundRect(0f, top - 0.04f * H, 0.52f * H, 0.08f * H, 0.03f * H, hi);                                       // kerah empuk
            for (float y = top - 0.12f * H; y > 0.0f; y -= 0.07f * H)                                                         // tali
                mb.Line(new Vector2(-0.14f * H, y), new Vector2(0.14f * H, y), 0.018f * H, lace);
        });
    }

    // =============================== PELINDUNG JATUH ===============================
    // Full body harness seperti referensi: tali bahu kuning berbingkai navy + bantalan krem, tali dada & gesper,
    // sabuk pinggang, tali paha diagonal dengan gesper logam.
    static void Harness(IndPartContext c)
    {
        float H = c.H; IndRig r = c.rig; float k = 1f / r.TK;
        Color web = c.main, navy = c.accent, navyDeep = IndColor.Shade(c.accent, 0.6f);
        Color metal = IndColor.C(205, 210, 216), pad = IndColor.C(244, 230, 185);
        c.Add(IndBone.Torso, IndLayer.Harness, mb =>
        {
            for (int s = -1; s <= 1; s += 2)
            {
                int sg = s;
                Func<float, float> cx = y => sg * Mathf.Lerp(0.37f * H, 0.47f * H, (y - 0.95f * H) / (1.6f * H));
                mb.Strip(0.80f * H, 2.58f * H, 22, y => cx(y) - 0.115f * H, y => cx(y) + 0.115f * H, navy);     // tepi navy
                mb.Strip(0.80f * H, 2.58f * H, 22, y => cx(y) - 0.085f * H, y => cx(y) + 0.085f * H, web);      // tali kuning
                mb.RoundRect(cx(1.98f * H), 1.98f * H, 0.21f * H, 0.23f * H * k, 0.025f * H, navy);              // bantalan
                mb.RoundRect(cx(1.98f * H), 1.98f * H, 0.17f * H, 0.19f * H * k, 0.02f * H, pad);
            }
            mb.Strip(1.74f * H, 1.74f * H + 0.10f * H * k, 2, y => -0.43f * H, y => 0.43f * H, navy);            // tali dada
            mb.RoundRect(0f, 1.79f * H, 0.22f * H, 0.15f * H * k, 0.02f * H, metal);
            mb.RoundRect(0f, 1.79f * H, 0.13f * H, 0.07f * H * k, 0.01f * H, navyDeep);
            Func<float, float> hw = y => r.TorsoHalf(y) + 0.035f * H;
            mb.Strip(0.76f * H, 0.76f * H + 0.25f * H * k, 2, y => -hw(y), y => hw(y), navy);                    // sabuk pinggang
            mb.Strip(0.78f * H, 0.78f * H + 0.21f * H * k, 2, y => -hw(y), y => hw(y), web);
            mb.RoundRect(0f, 0.88f * H, 0.34f * H, 0.25f * H * k, 0.03f * H, metal);                             // gesper pinggang
            mb.RoundRect(0f, 0.88f * H, 0.26f * H, 0.18f * H * k, 0.02f * H, navyDeep);
            for (int s = -1; s <= 1; s += 2)
            {                                                                                                     // tali paha diagonal
                mb.Polyline(new[] { new Vector2(s * 0.70f * H, 0.78f * H), new Vector2(s * 0.22f * H, 0.08f * H) }, 0.17f * H, navy);
                mb.Polyline(new[] { new Vector2(s * 0.70f * H, 0.78f * H), new Vector2(s * 0.22f * H, 0.08f * H) }, 0.12f * H, web);
                mb.RoundRect(s * 0.46f * H, 0.44f * H, 0.13f * H, 0.11f * H * k, 0.02f * H, metal);
            }
        });
        c.AddSides(IndBone.ThighL, IndLayer.HarnessLeg, (mb, s) =>
        {
            mb.Limb(r.thigh, 0f, r.thighLen, 0.14f, 0.25f, 0.04f * H, navy, 2);
            mb.Limb(r.thigh, 0f, r.thighLen, 0.15f, 0.24f, 0.03f * H, web, 2);
            mb.RoundRect(s * 0.20f * H, -0.195f * r.thighLen, 0.12f * H, 0.11f * H, 0.02f * H, metal);
        });
    }

    // =============================== LAINNYA ===============================
    static void IdCard(IndPartContext c)
    {
        float H = c.H;
        c.Add(IndBone.Torso, IndLayer.Badge, mb =>
        {
            mb.Polyline(new[] { new Vector2(-0.19f * H, 2.62f * H), new Vector2(0f, 1.78f * H), new Vector2(0.19f * H, 2.62f * H) }, 0.03f * H, c.accent);
            mb.RoundRect(0f, 1.60f * H, 0.30f * H, 0.42f * H, 0.03f * H, c.main);
            mb.Strip(1.74f * H, 1.80f * H, 2, y => -0.15f * H, y => 0.15f * H, c.accent);
            mb.Ellipse(0f, 1.62f * H, 0.07f * H, 0.07f * H, IndColor.C(120, 140, 160), 14);
            mb.Line(new Vector2(-0.10f * H, 1.46f * H), new Vector2(0.10f * H, 1.46f * H), 0.012f * H, IndColor.C(120, 120, 120));
        });
    }
}

// ------------------------------------------------------------------------------------
//  KOMPONEN UTAMA
// ------------------------------------------------------------------------------------
[DisallowMultipleComponent]
public class IndustrialCharacterGenerator : MonoBehaviour
{
    const string RigName = "__CharacterRig";

    [Header("Tubuh")]
    public IndBodyConfig body = new IndBodyConfig();
    public IndExpression expression = IndExpression.Neutral;

    [Header("Pakaian & APD")]
    [Tooltip("Pakai kaos + celana bawaan bila tidak ada pakaian/wearpack yang dipasang.")]
    public bool defaultClothing = true;
    public Color defaultShirtColor = new Color(0.82f, 0.84f, 0.88f);
    public Color defaultPantsColor = new Color(0.18f, 0.22f, 0.32f);
    [Tooltip("Pasang sepatu kasual bila slot kaki kosong.")]
    public bool defaultShoes = true;
    public Color defaultShoeColor = new Color(0.12f, 0.13f, 0.30f);
    public List<IndEquipEntry> equipment = new List<IndEquipEntry>();

    [Header("Render")]
    public string sortingLayerName = "Default";
    [Tooltip("sortingOrder renderer karakter. Seluruh karakter = 1 renderer, jadi cukup beri nilai berbeda antar karakter (mis. selisih 1).")]
    public int sortingOrderBase = 0;
    [Tooltip("Kosongkan = pakai Sprites/Default (mendukung vertex color).")]
    public Material overrideMaterial;

    [Header("Animasi")]
    public string startAnimation = "Idle";
    public float animationSpeed = 1f;
    [Tooltip("Mata berkedip otomatis.")]
    public bool autoBlink = true;

    // ---------- state runtime ----------
    IndRig _rig;
    GameObject _rigRoot;
    SkinnedMeshRenderer _smr;
    Mesh _mesh;
    Matrix4x4[] _restMat, _bind;
    static Material _sharedMat;

    class Piece { public IndBone bone; public int order, seq; public IndMeshBuilder mb; }
    readonly List<Piece> _bodyPieces = new List<Piece>();
    readonly List<Piece> _facePieces = new List<Piece>();
    readonly List<Piece> _equipPieces = new List<Piece>();
    int _seq;

    struct Joint { public int child, parent; public float radius, maxW; public bool both; }
    Joint[] _joints; Vector3[] _jointPos, _jointAxis;
    readonly Vector2[] _cum = new Vector2[IndBoneUtil.Count];
    float _blinkNext = 2f, _blinkT = -1f;

    IndAnimation _base, _fadeFrom, _overlay;
    float _baseTime, _fadeFromTime, _fadeT = 1f, _fadeDur = 0.15f, _ovT;
    readonly IndPose _pr = new IndPose(), _pa = new IndPose(), _po = new IndPose();

    public IndRig Rig { get { EnsureBuilt(); return _rig; } }

    // ---------- siklus hidup ----------
    void Awake() { Rebuild(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Rebuild ditunda agar tidak mengubah hierarki di dalam OnValidate.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (!gameObject.scene.IsValid()) return;      // abaikan prefab asset
            Rebuild();
        };
    }
#endif

    void LateUpdate()
    {
        if (_rig == null) return;
        TickAnimation(Time.deltaTime);
    }

    void EnsureBuilt() { if (_rig == null) Rebuild(); }

    [ContextMenu("Rebuild Character")]
    public void Rebuild()
    {
        ClearRig();
        BuildRig();
        BuildBody();
        BuildFace();
        BuildEquipment();
        Bake();
        _base = null; _overlay = null; _fadeFrom = null; _fadeT = 1f;
        Play(string.IsNullOrEmpty(startAnimation) ? "Idle" : startAnimation, 0f);
        if (Application.isPlaying) TickAnimation(0f);
    }

    [ContextMenu("Log Part Ids")]
    void LogPartIds()
    {
        foreach (var d in IndPartLibrary.All()) Debug.Log(d.zone + " | " + d.id + " | " + d.displayName + " | slot=" + d.slot);
    }

    // =====================================================================
    //  API PUBLIK - equipment
    // =====================================================================
    public void Equip(string partId) { Equip(partId, Color.clear, Color.clear); }

    // color/accent dengan alpha 0 = pakai warna bawaan part.
    public void Equip(string partId, Color color) { Equip(partId, color, Color.clear); }

    public void Equip(string partId, Color color, Color accent)
    {
        var def = IndPartLibrary.Get(partId);
        if (def == null) { Debug.LogWarning("Part tidak dikenal: " + partId); return; }
        equipment.RemoveAll(e =>
        {
            var o = IndPartLibrary.Get(e.partId);
            return o != null && Conflicts(def, o);
        });
        equipment.Add(new IndEquipEntry { partId = partId, color = color, accent = accent });
        RebuildEquipmentOnly();
    }

    public void Unequip(string partId)
    {
        if (equipment.RemoveAll(e => e.partId == partId) > 0) RebuildEquipmentOnly();
    }

    public void UnequipSlot(string slot)
    {
        if (equipment.RemoveAll(e => { var d = IndPartLibrary.Get(e.partId); return d != null && d.slot == slot; }) > 0) RebuildEquipmentOnly();
    }

    public void UnequipZone(PPEZone zone)
    {
        if (equipment.RemoveAll(e => { var d = IndPartLibrary.Get(e.partId); return d != null && d.zone == zone; }) > 0) RebuildEquipmentOnly();
    }

    public void ClearEquipment() { equipment.Clear(); RebuildEquipmentOnly(); }

    public bool IsEquipped(string partId) { return equipment.Exists(e => e.partId == partId); }

    public List<string> GetEquippedInZone(PPEZone zone)
    {
        var l = new List<string>();
        foreach (var e in equipment) { var d = IndPartLibrary.Get(e.partId); if (d != null && d.zone == zone) l.Add(e.partId); }
        return l;
    }

    // =====================================================================
    //  API PUBLIK - penampilan & animasi
    // =====================================================================
    public void SetExpression(IndExpression e) { expression = e; if (_rig != null) { BuildFace(); Bake(); } }
    public void SetSkinColor(Color c) { body.skin = c; Rebuild(); }
    public void SetHair(IndHairStyle style, Color color) { body.hairStyle = style; body.hair = color; Rebuild(); }

    public void Play(string name, float fade = 0.15f)
    {
        var a = IndAnimLibrary.Get(name);
        if (a == null) { Debug.LogWarning("Animasi tidak dikenal: " + name); return; }
        if (_base == a) return;
        _fadeFrom = _base; _fadeFromTime = _baseTime;
        _fadeT = (_fadeFrom == null || fade <= 0f) ? 1f : 0f;
        _fadeDur = Mathf.Max(0.0001f, fade);
        _base = a; _baseTime = 0f;
    }

    // Animasi sekali jalan yang menimpa bone tertentu saja (mis. Wave, Cheer, Nod, Shake).
    public void PlayOverlay(string name)
    {
        var a = IndAnimLibrary.Get(name);
        if (a == null) { Debug.LogWarning("Animasi tidak dikenal: " + name); return; }
        _overlay = a; _ovT = 0f;
    }

    public Transform GetBone(IndBone b) { EnsureBuilt(); return _rig.Get(b); }

    // =====================================================================
    //  ANIMASI - tick
    // =====================================================================
    void TickAnimation(float rawDt)
    {
        float dt = rawDt * animationSpeed;
        if (_base != null)
        {
            _baseTime += dt / Mathf.Max(0.01f, _base.Duration);
            _baseTime = _base.Loop ? _baseTime % 1f : Mathf.Min(1f, _baseTime);
        }
        _pr.Reset();
        if (_base != null) _base.Evaluate(_baseTime, _pr, _rig);

        if (_fadeFrom != null && _fadeT < 1f)
        {
            _fadeT += rawDt / _fadeDur;
            _fadeFromTime += dt / Mathf.Max(0.01f, _fadeFrom.Duration);
            _fadeFromTime = _fadeFrom.Loop ? _fadeFromTime % 1f : Mathf.Min(1f, _fadeFromTime);
            _pa.Reset();
            _fadeFrom.Evaluate(_fadeFromTime, _pa, _rig);
            IndPose.Blend(_pa, _pr, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_fadeT)), _pr);
        }

        if (_overlay != null)
        {
            _ovT += dt / Mathf.Max(0.01f, _overlay.Duration);
            if (_ovT >= 1f) _overlay = null;
            else
            {
                _po.Reset();
                _overlay.Evaluate(_ovT, _po, _rig);
                _pr.Overlay(_po, Mathf.Clamp01(Mathf.Min(_ovT, 1f - _ovT) / 0.08f));
            }
        }
        float bl = UpdateBlink(rawDt);
        if (bl > 0f) _pr.Scl(IndBone.Eyes, 1f, Mathf.Lerp(1f, 0.08f, bl));
        ApplyPose(_pr);
    }

    float UpdateBlink(float dt)
    {
        if (!autoBlink) return 0f;
        if (_blinkT < 0f)
        {
            _blinkNext -= dt;
            if (_blinkNext <= 0f) _blinkT = 0f;
            return 0f;
        }
        _blinkT += dt;
        const float dur = 0.16f;
        if (_blinkT >= dur) { _blinkT = -1f; _blinkNext = UnityEngine.Random.Range(2.2f, 5.5f); return 0f; }
        return Mathf.Sin(_blinkT / dur * Mathf.PI);
    }

    static bool KeepShape(int i)
    {
        return i == (int)IndBone.HandL || i == (int)IndBone.HandR || i == (int)IndBone.FootL || i == (int)IndBone.FootR;
    }

    void ApplyPose(IndPose p)
    {
        for (int i = 0; i < IndBoneUtil.Count; i++)
        {
            Transform tr = _rig.t[i];
            if (tr == null) continue;
            Vector2 sc = p.scl[i];
            if (i > 0 && KeepShape(i))
            {   // tangan & kaki tidak ikut gepeng saat lengan/kaki induknya di-scale
                Vector2 pc = _cum[IndBoneUtil.Parent[i]];
                sc.x /= Mathf.Max(0.25f, pc.x); sc.y /= Mathf.Max(0.25f, pc.y);
            }
            tr.localPosition = _rig.restPos[i] + new Vector3(p.pos[i].x, p.pos[i].y, 0f);
            tr.localRotation = Quaternion.Euler(0f, 0f, _rig.restRot[i] + p.rot[i]);
            tr.localScale = new Vector3(sc.x, sc.y, 1f);
            _cum[i] = i == 0 ? Vector2.one : Vector2.Scale(_cum[IndBoneUtil.Parent[i]], sc);
        }
    }

    // =====================================================================
    //  BUILD - rig
    // =====================================================================
    void ClearRig()
    {
        // hapus rig lama (termasuk sisa dari sesi editor sebelumnya)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var ch = transform.GetChild(i);
            if (ch.name == RigName) KillObj(ch.gameObject);
        }
        _rigRoot = null; _rig = null; _smr = null;
        _bodyPieces.Clear(); _facePieces.Clear(); _equipPieces.Clear(); _seq = 0;
    }

    void OnDestroy() { KillObj(_mesh); }

    static void KillObj(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

    Transform MakeBone(IndRig r, IndBone id, Transform parent, Vector3 pos, float restRot = 0f)
    {
        var go = new GameObject(id.ToString());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, restRot);
        go.hideFlags = HideFlags.DontSaveInEditor;
        r.t[(int)id] = go.transform; r.restPos[(int)id] = pos; r.restRot[(int)id] = restRot;
        return go.transform;
    }

    void BuildRig()
    {
        var r = new IndRig();
        r.Setup(body);

        _rigRoot = new GameObject(RigName);
        _rigRoot.transform.SetParent(transform, false);
        _rigRoot.hideFlags = HideFlags.DontSaveInEditor;     // objek hasil generate tidak ikut tersimpan di scene
        Transform root = _rigRoot.transform;
        r.t[(int)IndBone.Root] = root;

        Transform hips = MakeBone(r, IndBone.Hips, root, new Vector3(0f, r.hipY, 0f));
        Transform torso = MakeBone(r, IndBone.Torso, hips, Vector3.zero);
        Transform neck = MakeBone(r, IndBone.Neck, torso, new Vector3(0f, r.neckY, 0f));
        Transform head = MakeBone(r, IndBone.Head, neck, new Vector3(0f, r.headY, 0f));
        MakeBone(r, IndBone.Eyes, head, new Vector3(0f, IndRig.EyeY * r.H, 0f));

        for (int s = -1; s <= 1; s += 2)
        {
            bool left = s < 0;
            // pose berdiri tegap: lengan agak menjauh dari badan, siku menekuk, tangan di sekitar pinggul; kaki terbuka
            Transform ua = MakeBone(r, left ? IndBone.UpperArmL : IndBone.UpperArmR, torso, new Vector3(s * r.shoulderX, r.shoulderY, 0f), s * 8f);
            Transform fa = MakeBone(r, left ? IndBone.ForeArmL : IndBone.ForeArmR, ua, new Vector3(0f, -r.upperArmLen, 0f), -s * 18f);
            MakeBone(r, left ? IndBone.HandL : IndBone.HandR, fa, new Vector3(0f, -r.foreArmLen, 0f));

            Transform th = MakeBone(r, left ? IndBone.ThighL : IndBone.ThighR, hips, new Vector3(s * r.hipX, 0f, 0f), s * r.legSplay);
            Transform sh = MakeBone(r, left ? IndBone.ShinL : IndBone.ShinR, th, new Vector3(0f, -r.thighLen, 0f), 0f);
            MakeBone(r, left ? IndBone.FootL : IndBone.FootR, sh, new Vector3(0f, -r.shinLen, 0f), -s * r.legSplay);   // telapak tetap rata di lantai
        }
        _rig = r;
        SetupSkin();
    }

    static Joint J(IndBone child, IndBone parent, float radius, float maxW, bool both)
    {
        return new Joint { child = (int)child, parent = (int)parent, radius = radius, maxW = maxW, both = both };
    }

    // Matriks pose istirahat, bind pose, daftar sendi (untuk bobot halus), dan SkinnedMeshRenderer.
    void SetupSkin()
    {
        int n = IndBoneUtil.Count;
        _restMat = new Matrix4x4[n]; _bind = new Matrix4x4[n];
        _restMat[0] = Matrix4x4.identity;
        for (int i = 1; i < n; i++)
            _restMat[i] = _restMat[IndBoneUtil.Parent[i]] * Matrix4x4.TRS(_rig.restPos[i], Quaternion.Euler(0f, 0f, _rig.restRot[i]), Vector3.one);
        for (int i = 0; i < n; i++) _bind[i] = _restMat[i].inverse;

        float H = _rig.H;
        _joints = new[]
        {
            J(IndBone.Neck, IndBone.Torso, 0.25f * H, 0.5f, false),
            J(IndBone.Head, IndBone.Neck, 0.22f * H, 0.5f, true),
            J(IndBone.UpperArmL, IndBone.Torso, 0.32f * H, 0.4f, false), J(IndBone.UpperArmR, IndBone.Torso, 0.32f * H, 0.4f, false),
            J(IndBone.ForeArmL, IndBone.UpperArmL, 0.30f * H, 0.5f, true), J(IndBone.ForeArmR, IndBone.UpperArmR, 0.30f * H, 0.5f, true),
            J(IndBone.HandL, IndBone.ForeArmL, 0.20f * H, 0.5f, true), J(IndBone.HandR, IndBone.ForeArmR, 0.20f * H, 0.5f, true),
            J(IndBone.ThighL, IndBone.Hips, 0.35f * H, 0.4f, false), J(IndBone.ThighR, IndBone.Hips, 0.35f * H, 0.4f, false),
            J(IndBone.ShinL, IndBone.ThighL, 0.40f * H, 0.5f, true), J(IndBone.ShinR, IndBone.ThighR, 0.40f * H, 0.5f, true),
            J(IndBone.FootL, IndBone.ShinL, 0.16f * H, 0.5f, true), J(IndBone.FootR, IndBone.ShinR, 0.16f * H, 0.5f, true),
        };
        _jointPos = new Vector3[_joints.Length]; _jointAxis = new Vector3[_joints.Length];
        for (int j = 0; j < _joints.Length; j++)
        {
            _jointPos[j] = _restMat[_joints[j].child].MultiplyPoint3x4(Vector3.zero);
            _jointAxis[j] = _restMat[_joints[j].child].MultiplyVector(Vector3.up).normalized;
        }

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "IndCharacterMesh" };
            _mesh.hideFlags = HideFlags.DontSaveInEditor;
            _mesh.MarkDynamic();
        }

        _smr = _rigRoot.AddComponent<SkinnedMeshRenderer>();
        _smr.sharedMaterial = overrideMaterial != null ? overrideMaterial : SharedMaterial();
        _smr.sortingLayerName = sortingLayerName;
        _smr.sortingOrder = sortingOrderBase;
        _smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _smr.receiveShadows = false;
        _smr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _smr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        _smr.quality = SkinQuality.Bone2;
        _smr.updateWhenOffscreen = false;
        _smr.bones = _rig.t;
        _smr.rootBone = _rigRoot.transform;
        _smr.localBounds = new Bounds(new Vector3(0f, body.height * 0.5f, 0f), new Vector3(body.height * 1.4f, body.height * 1.3f, 1f));
    }

    // =====================================================================
    //  BUILD - mesh tunggal (skinned)
    // =====================================================================
    static Material SharedMaterial()
    {
        if (_sharedMat == null)
        {
            var sh = Shader.Find("Sprites/Default");
            _sharedMat = new Material(sh) { name = "IndVectorMat", hideFlags = HideFlags.HideAndDontSave };
        }
        return _sharedMat;
    }

    void AddPiece(List<Piece> list, IndBone bone, int order, IndMeshBuilder mb)
    {
        if (mb.IsEmpty) return;
        list.Add(new Piece { bone = bone, order = order, seq = _seq++, mb = mb });
    }

    // Bobot tulang per vertex: milik 1 bone, tetapi dekat sendi dicampur dengan bone sebelahnya
    // supaya siku/lutut/leher/pergelangan melengkung mulus tanpa celah (tidak tembus pandang).
    BoneWeight WeightFor(int owner, Vector3 p)
    {
        var bw = new BoneWeight { boneIndex0 = owner, weight0 = 1f };
        float best = 0f; int other = -1;
        for (int j = 0; j < _joints.Length; j++)
        {
            Joint jt = _joints[j];
            int o;
            if (owner == jt.child) o = jt.parent;
            else if (owner == jt.parent && jt.both) o = jt.child;
            else continue;
            float d = Vector3.Dot(p - _jointPos[j], _jointAxis[j]);
            float t = 1f - Mathf.Abs(d) / jt.radius;
            if (t <= 0f) continue;
            float w = jt.maxW * t * t * (3f - 2f * t);
            if (w > best) { best = w; other = o; }
        }
        if (other >= 0) { bw.weight0 = 1f - best; bw.boneIndex1 = other; bw.weight1 = best; }
        return bw;
    }

    // Gabungkan semua bentuk (badan + wajah + pakaian/APD + rambut) menjadi 1 mesh.
    // Urutan gambar = urutan segitiga, diurutkan menurut layer (IndLayer).
    void Bake()
    {
        if (_rig == null || _smr == null) return;
        var all = new List<Piece>(_bodyPieces.Count + _facePieces.Count + _equipPieces.Count);
        all.AddRange(_bodyPieces); all.AddRange(_facePieces); all.AddRange(_equipPieces);
        all.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.seq.CompareTo(b.seq));

        var verts = new List<Vector3>(); var cols = new List<Color32>();
        var tris = new List<int>(); var bws = new List<BoneWeight>();
        foreach (var pc in all)
        {
            int bi = (int)pc.bone;
            Matrix4x4 m = _restMat[bi];
            int baseIdx = verts.Count;
            var v = pc.mb.Vertices; var c = pc.mb.Colors; var t = pc.mb.Triangles;
            for (int i = 0; i < v.Count; i++)
            {
                Vector3 lp = v[i];
                if (bi == (int)IndBone.Torso) lp.y *= _rig.TK;      // badan lebih panjang tanpa men-scale bone
                Vector3 p = m.MultiplyPoint3x4(lp);
                verts.Add(p); cols.Add(c[i]); bws.Add(WeightFor(bi, p));
            }
            for (int i = 0; i < t.Count; i++) tris.Add(baseIdx + t[i]);
        }

        _mesh.Clear();
        _mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.SetVertices(verts);
        _mesh.SetColors(cols);
        _mesh.SetTriangles(tris, 0);
        _mesh.boneWeights = bws.ToArray();
        _mesh.bindposes = _bind;
        _mesh.bounds = new Bounds(new Vector3(0f, body.height * 0.5f, 0f), new Vector3(body.height * 1.4f, body.height * 1.3f, 1f));
        _smr.sharedMesh = _mesh;
    }

    // Badan dasar (kulit): tidak dibongkar saat ganti pakaian.
    void BuildBody()
    {
        IndRig r = _rig; float H = r.H;
        Color skin = body.skin, shade = IndColor.Shade(skin, 0.86f);
        _bodyPieces.Clear();

        // Torso
        var mb = new IndMeshBuilder();
        mb.Strip(0f, 2.62f * H, 28, y => -r.TorsoHalf(y), y => r.TorsoHalf(y), skin);
        AddPiece(_bodyPieces, IndBone.Torso, IndLayer.Torso, mb);

        // Leher: menyelam dalam ke torso & ke kepala (tidak ada celah), + bayangan dagu
        mb = new IndMeshBuilder();
        mb.Tube(r.neck, 0f, -0.30f * H, 0.50f * H, 0f, skin, 8);
        mb.Ellipse(0f, -0.04f * H, 0.25f * H, 0.17f * H, skin, 20);
        mb.Ellipse(0f, 0.20f * H, 0.19f * H, 0.08f * H, shade, 16);
        AddPiece(_bodyPieces, IndBone.Neck, IndLayer.Neck, mb);

        // Kepala + telinga
        mb = new IndMeshBuilder();
        mb.Tube(r.head, 0f, 0f, H, 0f, skin, 22);
        AddPiece(_bodyPieces, IndBone.Head, IndLayer.Head, mb);
        mb = new IndMeshBuilder();
        for (int s = -1; s <= 1; s += 2)
        {
            float ex = s * (r.HeadHalf(0.5f * H) + 0.005f * H);
            mb.Ellipse(ex, 0.50f * H, 0.055f * H, 0.10f * H, skin, 14);
            mb.Ellipse(ex + s * 0.004f * H, 0.50f * H, 0.03f * H, 0.065f * H, shade, 12);
        }
        AddPiece(_bodyPieces, IndBone.Head, IndLayer.Ear, mb);

        for (int s = -1; s <= 1; s += 2)
        {
            bool left = s < 0;

            // Lengan atas: bahu & siku membulat
            mb = new IndMeshBuilder();
            float wTop = r.upperArm.Eval(0f), wElb = r.upperArm.Eval(1f);
            mb.Limb(r.upperArm, 0f, r.upperArmLen, 0f, 1f, 0f, skin, 10);
            mb.Ellipse(0f, 0f, wTop, wTop, skin, 20);
            mb.Ellipse(0f, -r.upperArmLen, wElb, wElb, skin, 16);
            AddPiece(_bodyPieces, left ? IndBone.UpperArmL : IndBone.UpperArmR, IndLayer.UpperArm, mb);

            // Lengan bawah (ujung atas dibulatkan agar siku tidak berlubang saat menekuk)
            mb = new IndMeshBuilder();
            float wFa = r.foreArm.Eval(0f);
            mb.Limb(r.foreArm, 0f, r.foreArmLen, 0f, 1f, 0f, skin, 10);
            mb.Ellipse(0f, 0f, wFa, wFa, skin, 16);
            AddPiece(_bodyPieces, left ? IndBone.ForeArmL : IndBone.ForeArmR, IndLayer.ForeArm, mb);

            // Tangan: kepalan santai + jempol + garis jari
            mb = new IndMeshBuilder();
            mb.Limb(r.hand, 0f, r.handLen, -0.1f, 1f, 0f, skin, 10);
            mb.Ellipse(-s * 0.17f * H, -0.28f * H, 0.07f * H, 0.11f * H, skin, 14, -s * 15f);                       // jempol
            mb.Line(new Vector2(-0.13f * H, -0.42f * H), new Vector2(0.13f * H, -0.44f * H), 0.012f * H, shade);    // garis jari
            AddPiece(_bodyPieces, left ? IndBone.HandL : IndBone.HandR, IndLayer.Hand, mb);

            // Kaki
            mb = new IndMeshBuilder();
            float wk = r.thigh.Eval(1f);
            mb.Limb(r.thigh, 0f, r.thighLen, -0.1f, 1f, 0f, skin, 10);
            mb.Ellipse(0f, -r.thighLen, wk, wk, skin, 16);
            AddPiece(_bodyPieces, left ? IndBone.ThighL : IndBone.ThighR, IndLayer.Leg, mb);

            mb = new IndMeshBuilder();
            float ws = r.shinSkin.Eval(0f);
            mb.Limb(r.shinSkin, 0f, r.shinLen, 0f, 1f, 0f, skin, 10);
            mb.Ellipse(0f, 0f, ws, ws, skin, 16);
            AddPiece(_bodyPieces, left ? IndBone.ShinL : IndBone.ShinR, IndLayer.Leg, mb);

            mb = new IndMeshBuilder();
            mb.Poly(IndBuiltInParts.FootShape(s, H), skin);        // kaki telanjang (tertutup sepatu bila dipakai)
            AddPiece(_bodyPieces, left ? IndBone.FootL : IndBone.FootR, IndLayer.FootSkin, mb);
        }
    }

    // Wajah gaya flat ramah: mata titik + highlight, alis tipis, senyum kecil, pipi merona.
    // Mata ada di bone sendiri (Eyes) agar bisa berkedip. Dibangun ulang saat ganti ekspresi.
    void BuildFace()
    {
        _facePieces.Clear();
        IndRig r = _rig; float H = r.H;
        bool fem = body.gender == IndGender.Female;
        Color skinShade = IndColor.Shade(body.skin, 0.80f);
        Color dark = IndColor.C(30, 22, 20), white = IndColor.C(250, 250, 250);
        Color eyeCol = IndColor.Shade(body.eye, 0.55f);
        Color lip = fem ? IndColor.C(190, 85, 95) : IndColor.Shade(body.skin, 0.62f);
        Color blush = Color.Lerp(body.skin, IndColor.C(235, 105, 115), fem ? 0.38f : 0.30f);
        Color brow = IndColor.Shade(body.hair, 0.9f);
        if (body.hairStyle == IndHairStyle.Hijab && string.IsNullOrEmpty(body.customHairId)) brow = IndColor.C(45, 35, 30);

        // jenggot / kumis (di belakang wajah)
        var beard = new IndMeshBuilder();
        DrawFacialHair(beard, r, IndColor.Shade(body.hair, 0.95f));
        AddPiece(_facePieces, IndBone.Head, IndLayer.Beard, beard);

        // alis, pipi, hidung, mulut (bone Head)
        var mb = new IndMeshBuilder();
        for (int s = -1; s <= 1; s += 2)
            mb.Ellipse(s * 0.255f * H, 0.33f * H, 0.075f * H, 0.048f * H, blush, 16);
        for (int s = -1; s <= 1; s += 2)
        {
            float cx = s * 0.15f * H;
            float by = 0.655f * H, inner = 0f, outer = 0f, lift = 0f;
            switch (expression)
            {
                case IndExpression.Happy: lift = 0.012f * H; break;
                case IndExpression.Surprised: lift = 0.04f * H; break;
                case IndExpression.Worried: inner = 0.028f * H; outer = -0.006f * H; break;
            }
            var bp = new List<Vector2> {
                new Vector2(cx - s * 0.075f * H, by + lift + inner),
                new Vector2(cx, by + lift + 0.012f * H + (inner + outer) * 0.4f),
                new Vector2(cx + s * 0.085f * H, by + lift + outer) };
            mb.Polyline(bp, (fem ? 0.020f : 0.026f) * H, brow, true);
        }
        mb.Polyline(IndMeshBuilder.ArcPoints(0f, 0.375f * H, 0.04f * H, 0.02f * H, 200f, 340f, 8), 0.012f * H, skinShade);
        float lt = (fem ? 0.020f : 0.016f) * H;
        switch (expression)
        {
            case IndExpression.Happy:
                mb.Polyline(IndMeshBuilder.ArcPoints(0f, 0.245f * H, 0.095f * H, 0.065f * H, 205f, 335f, 10), lt * 1.2f, lip, true);
                break;
            case IndExpression.Worried:
                mb.Polyline(IndMeshBuilder.ArcPoints(0f, 0.12f * H, 0.075f * H, 0.045f * H, 30f, 150f, 10), lt, lip, true);
                break;
            case IndExpression.Surprised:
                mb.Ellipse(0f, 0.18f * H, 0.045f * H, 0.055f * H, lip, 14);
                mb.Ellipse(0f, 0.18f * H, 0.032f * H, 0.042f * H, IndColor.C(70, 25, 30), 14);
                break;
            default:    // netral = senyum tipis (tidak datar / menyeramkan)
                mb.Polyline(IndMeshBuilder.ArcPoints(0f, 0.235f * H, 0.07f * H, 0.035f * H, 215f, 325f, 10), lt, lip, true);
                break;
        }
        AddPiece(_facePieces, IndBone.Head, IndLayer.Face, mb);

        // mata (bone Eyes, titik pusat = tengah mata)
        var eyes = new IndMeshBuilder();
        for (int s = -1; s <= 1; s += 2)
        {
            float cx = s * 0.15f * H;
            float ry = expression == IndExpression.Surprised ? 0.050f * H : (expression == IndExpression.Happy ? 0.030f * H : 0.036f * H);
            eyes.Ellipse(cx, 0f, 0.026f * H, ry, eyeCol, 16);
            eyes.Ellipse(cx + 0.008f * H, ry * 0.35f, 0.008f * H, 0.008f * H, white, 8);
            if (fem) eyes.Line(new Vector2(cx + s * 0.038f * H, 0.012f * H), new Vector2(cx + s * 0.062f * H, 0.034f * H), 0.012f * H, dark);
        }
        AddPiece(_facePieces, IndBone.Eyes, IndLayer.Face + 1, eyes);
    }

    void DrawFacialHair(IndMeshBuilder mb, IndRig r, Color col)
    {
        float H = r.H;
        if (body.facialHair == IndFacialHair.None) return;
        if (body.facialHair == IndFacialHair.Stubble)
        {
            mb.Strip(0f, 0.34f * H, 10, y => -(r.HeadHalf(y) + 0.003f * H), y => r.HeadHalf(y) + 0.003f * H, IndColor.Alpha(col, 0.28f));
            return;
        }
        Action mustache = () =>
            mb.Polyline(new[] { new Vector2(-0.11f * H, 0.250f * H), new Vector2(-0.04f * H, 0.272f * H), new Vector2(0f, 0.262f * H),
                                new Vector2(0.04f * H, 0.272f * H), new Vector2(0.11f * H, 0.250f * H) }, 0.045f * H, col, true);
        if (body.facialHair == IndFacialHair.Beard)
        {
            mb.Strip(0f, 0.32f * H, 14, y => -(r.HeadHalf(y) + 0.008f * H), y => r.HeadHalf(y) + 0.008f * H, col);
            mb.Strip(0.32f * H, 0.52f * H, 6, y => -(r.HeadHalf(y) + 0.008f * H), y => -(r.HeadHalf(y) - 0.07f * H), col);
            mb.Strip(0.32f * H, 0.52f * H, 6, y => r.HeadHalf(y) - 0.07f * H, y => r.HeadHalf(y) + 0.008f * H, col);
        }
        if (body.facialHair == IndFacialHair.Goatee) mb.Ellipse(0f, 0.08f * H, 0.075f * H, 0.095f * H, col, 14);
        mustache();
    }

    // =====================================================================
    //  BUILD - pakaian, APD, rambut
    // =====================================================================
    static bool Conflicts(IndPartDef a, IndPartDef b)
    {
        return a.slot == b.slot || Array.IndexOf(a.replaces, b.slot) >= 0 || Array.IndexOf(b.replaces, a.slot) >= 0;
    }

    class Resolved { public IndPartDef def; public Color main, accent; }

    // Daftar part efektif: konflik slot diselesaikan (yang belakangan menang) + pakaian bawaan.
    List<Resolved> Resolve()
    {
        var list = new List<Resolved>();
        foreach (var e in equipment)
        {
            var d = IndPartLibrary.Get(e.partId);
            if (d == null) { Debug.LogWarning("Part tidak dikenal: " + e.partId); continue; }
            list.RemoveAll(x => Conflicts(d, x.def));
            list.Add(new Resolved
            {
                def = d,
                main = e.color.a > 0.01f ? e.color : d.defaultMain,
                accent = e.accent.a > 0.01f ? e.accent : d.defaultAccent
            });
        }
        if (defaultClothing)
        {
            if (!list.Exists(x => x.def.slot == "inner" || x.def.slot == "outfit"))
            {
                var d = IndPartLibrary.Get("shirt_short");
                list.Insert(0, new Resolved { def = d, main = defaultShirtColor, accent = IndColor.Shade(defaultShirtColor, 0.85f) });
            }
            if (!list.Exists(x => x.def.slot == "legs" || x.def.slot == "outfit"))
            {
                var d = IndPartLibrary.Get("pants_work");
                list.Insert(0, new Resolved { def = d, main = defaultPantsColor, accent = IndColor.C(30, 30, 30) });
            }
        }
        if (defaultShoes && !list.Exists(x => x.def.slot == "feet"))
        {
            var d = IndPartLibrary.Get("shoes_casual");
            if (d != null) list.Insert(0, new Resolved { def = d, main = defaultShoeColor, accent = defaultShoeColor });
        }
        return list;
    }

    void RebuildEquipmentOnly()
    {
        EnsureBuilt();
        BuildEquipment();
        Bake();
    }

    void BuildEquipment()
    {
        _equipPieces.Clear();
        var list = Resolve();

        bool headgear = false;
        foreach (var x in list) if (x.def.coversHairTop) headgear = true;
        BuildHair(headgear);

        foreach (var x in list)
        {
            var def = x.def;
            var ctx = new IndPartContext { rig = _rig, body = body, main = x.main, accent = x.accent };
            ctx.addAction = (bone, order, draw) =>
            {
                var mb = new IndMeshBuilder();
                draw(mb);
                AddPiece(_equipPieces, bone, order, mb);
            };
            def.build(ctx);
        }
    }

    void BuildHair(bool headgear)
    {
        string id = string.IsNullOrEmpty(body.customHairId) ? body.hairStyle.ToString() : body.customHairId;
        var build = IndHairLibrary.Get(id);
        if (build == null) { Debug.LogWarning("Gaya rambut tidak dikenal: " + id); return; }
        var ctx = new IndHairContext { rig = _rig, color = body.hair, headgear = headgear };
        ctx.addAction = (order, draw) =>
        {
            var mb = new IndMeshBuilder();
            draw(mb);
            AddPiece(_equipPieces, IndBone.Head, order, mb);
        };
        build(ctx);
    }
}

// ------------------------------------------------------------------------------------
//  EDITOR: dropdown id otomatis + generator konstanta id
// ------------------------------------------------------------------------------------
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(IndPartIdAttribute))]
public class IndPartIdDrawer : PropertyDrawer
{
    string[] _ids, _labels; int _count = -1;

    void Build()
    {
        var defs = new List<IndPartDef>(IndPartLibrary.All());
        if (_ids != null && defs.Count == _count) return;
        _count = defs.Count;
        defs.Sort((a, b) =>
        {
            int z = ((int)a.zone).CompareTo((int)b.zone);
            return z != 0 ? z : string.Compare(a.id, b.id, StringComparison.Ordinal);
        });
        _ids = new string[defs.Count + 1]; _labels = new string[defs.Count + 1];
        _ids[0] = ""; _labels[0] = "(kosong)";
        for (int i = 0; i < defs.Count; i++)
        {
            _ids[i + 1] = defs[i].id;
            _labels[i + 1] = defs[i].zone + "/" + defs[i].displayName + "  [" + defs[i].id + "]";
        }
    }

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        if (prop.propertyType != SerializedPropertyType.String) { EditorGUI.PropertyField(pos, prop, label); return; }
        Build();
        string cur = prop.stringValue ?? "";
        int idx = Array.IndexOf(_ids, cur);
        var labels = _labels;
        if (idx < 0)
        {
            labels = new string[_labels.Length + 1];
            Array.Copy(_labels, labels, _labels.Length);
            labels[labels.Length - 1] = "(tidak dikenal) " + cur;
            idx = labels.Length - 1;
        }
        EditorGUI.BeginProperty(pos, label, prop);
        EditorGUI.BeginChangeCheck();
        int n = EditorGUI.Popup(pos, label.text, idx, labels);
        if (EditorGUI.EndChangeCheck() && n < _ids.Length) prop.stringValue = _ids[n];
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(IndHairIdAttribute))]
public class IndHairIdDrawer : PropertyDrawer
{
    string[] _ids, _labels; int _count = -1;

    void Build()
    {
        var ids = new List<string>(IndHairLibrary.Ids());
        if (_ids != null && ids.Count == _count) return;
        _count = ids.Count;
        ids.Sort(StringComparer.Ordinal);
        _ids = new string[ids.Count + 1]; _labels = new string[ids.Count + 1];
        _ids[0] = ""; _labels[0] = "(pakai hairStyle)";
        for (int i = 0; i < ids.Count; i++) { _ids[i + 1] = ids[i]; _labels[i + 1] = ids[i]; }
    }

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        if (prop.propertyType != SerializedPropertyType.String) { EditorGUI.PropertyField(pos, prop, label); return; }
        Build();
        string cur = prop.stringValue ?? "";
        int idx = Array.IndexOf(_ids, cur);
        var labels = _labels;
        if (idx < 0)
        {
            labels = new string[_labels.Length + 1];
            Array.Copy(_labels, labels, _labels.Length);
            labels[labels.Length - 1] = "(tidak dikenal) " + cur;
            idx = labels.Length - 1;
        }
        EditorGUI.BeginProperty(pos, label, prop);
        EditorGUI.BeginChangeCheck();
        int n = EditorGUI.Popup(pos, label.text, idx, labels);
        if (EditorGUI.EndChangeCheck() && n < _ids.Length) prop.stringValue = _ids[n];
        EditorGUI.EndProperty();
    }
}

// Membuat Assets/IndPartIds.cs berisi konstanta dari registry:
//   IndPartIds.Hardhat, IndHairIds.Curly, IndAnimNames.Walk  ->  gen.Equip(IndPartIds.Hardhat);
public static class IndIdConstantsGenerator
{
    const string FileName = "IndPartIds.cs";

    [InitializeOnLoadMethod]
    static void Auto() { EditorApplication.delayCall += () => Write(false); }

    [MenuItem("Tools/Industrial Character/Generate Id Constants")]
    static void Menu() { Write(true); }

    static string Ident(string id, HashSet<string> used)
    {
        var sb = new StringBuilder();
        bool up = true;
        foreach (char ch in id)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(up ? char.ToUpperInvariant(ch) : ch); up = false; }
            else up = true;
        }
        string name = sb.Length == 0 ? "Id" : sb.ToString();
        if (char.IsDigit(name[0])) name = "_" + name;
        string unique = name; int k = 2;
        while (!used.Add(unique)) unique = name + (k++);
        return unique;
    }

    static string Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated> Dibuat otomatis oleh IndustrialCharacterGenerator dari registry. Jangan diedit manual.");
        sb.AppendLine("public static class IndPartIds");
        sb.AppendLine("{");
        var defs = new List<IndPartDef>(IndPartLibrary.All());
        defs.Sort((a, b) =>
        {
            int z = ((int)a.zone).CompareTo((int)b.zone);
            return z != 0 ? z : string.Compare(a.id, b.id, StringComparison.Ordinal);
        });
        var used = new HashSet<string>();
        foreach (var d in defs)
            sb.AppendLine("    public const string " + Ident(d.id, used) + " = \"" + d.id + "\";   // " + d.zone + " - " + d.displayName);
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class IndHairIds");
        sb.AppendLine("{");
        var hair = new List<string>(IndHairLibrary.Ids()); hair.Sort(StringComparer.Ordinal);
        used = new HashSet<string>();
        foreach (var h in hair) sb.AppendLine("    public const string " + Ident(h, used) + " = \"" + h + "\";");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class IndAnimNames");
        sb.AppendLine("{");
        var an = new List<string>(IndAnimLibrary.Names()); an.Sort(StringComparer.Ordinal);
        used = new HashSet<string>();
        foreach (var a in an) sb.AppendLine("    public const string " + Ident(a, used) + " = \"" + a + "\";");
        sb.AppendLine("}");
        return sb.ToString();
    }

    static void Write(bool force)
    {
        if (!force && (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)) return;
        string folder = "Scripts";
        string path = Path.Combine(Application.dataPath, folder, FileName);
        string text = Generate();
        if (File.Exists(path) && File.ReadAllText(path) == text)
        {
            if (force) Debug.Log("IndPartIds.cs sudah up-to-date.");
            return;
        }
        File.WriteAllText(path, text);
        AssetDatabase.Refresh();
        Debug.Log("IndPartIds.cs diperbarui dari registry.");
    }
}
#endif
