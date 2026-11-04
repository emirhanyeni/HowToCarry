// Assets/Editor/EdiRoomBuilder.cs
// Menü: Tools > Edi > Create Final Edi Room
// Tek tıkla "Final_Edi_Room" objesini sahnede kurar, materyalleri ve prefab'ı Assets altına kaydeder.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class EdiRoomBuilder
{
    const string RootName = "Final_Edi_Room";
    const string MatFolder = "Assets/Generated/EdiRoom/Materials";
    const string PrefabFolder = "Assets/Prefabs";
    const string PrefabPath = PrefabFolder + "/" + RootName + ".prefab";

    // Oda ölçüleri (metre)
    const float RoomSize = 10f;
    const float WallHeight = 3f;
    const float WallThick = 0.2f;
    const float Half = RoomSize * 0.5f;

    [MenuItem("Tools/Edi/Create Final Edi Room")]
    public static void CreateRoom()
    {
        // Sahnede eski kopya varsa kaldır
        var old = GameObject.Find(RootName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        EnsureFolder(MatFolder);
        EnsureFolder(PrefabFolder);

        // ---------------------------------------------------- Materyaller
        Material matWall    = GetMat("Mat_Wall",     new Color(0.52f, 0.54f, 0.56f), 0.1f);
        Material matFloor   = GetMat("Mat_WoodFloor", new Color(0.45f, 0.30f, 0.18f), 0.25f);
        Material matCeiling = GetMat("Mat_Ceiling",  new Color(0.70f, 0.70f, 0.68f), 0.05f);
        Material matBedBase = GetMat("Mat_BedBase",  new Color(0.35f, 0.22f, 0.12f), 0.1f);
        Material matMattress= GetMat("Mat_Mattress", new Color(0.35f, 0.50f, 0.78f), 0.05f);
        Material matBlanket = GetMat("Mat_Blanket",  new Color(0.80f, 0.35f, 0.25f), 0.05f);
        Material matPillow  = GetMat("Mat_Pillow",   new Color(0.95f, 0.95f, 0.92f), 0.05f);
        Material matWood    = GetMat("Mat_DeskWood", new Color(0.55f, 0.38f, 0.22f), 0.2f);
        Material matBlack   = GetMat("Mat_Black",    new Color(0.04f, 0.04f, 0.05f), 0.5f);
        Material matScreen  = GetMat("Mat_Screen",   new Color(0.05f, 0.10f, 0.14f), 0.8f);
        Material matChair   = GetMat("Mat_Chair",    new Color(0.15f, 0.15f, 0.18f), 0.1f);
        Material matBox     = GetMat("Mat_PizzaBox", new Color(0.82f, 0.68f, 0.45f), 0.0f);
        Material matBoxRed  = GetMat("Mat_PizzaBoxRed", new Color(0.75f, 0.22f, 0.18f), 0.0f);
        Material matMirror  = GetMat("Mat_Mirror",   new Color(0.75f, 0.88f, 0.95f), 0.97f, 0.9f);
        Material matFrame   = GetMat("Mat_MirrorFrame", new Color(0.25f, 0.17f, 0.10f), 0.2f);

        // ---------------------------------------------------- Kök
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);

        // ---------------------------------------------------- Oda kabuğu
        var shell = Group(root.transform, "Room_Shell");
        Make(shell, "Floor", PrimitiveType.Cube, new Vector3(0, -0.05f, 0), new Vector3(RoomSize, 0.1f, RoomSize), matFloor);
        Make(shell, "Ceiling", PrimitiveType.Cube, new Vector3(0, WallHeight + 0.05f, 0),
             new Vector3(RoomSize + WallThick * 2, 0.1f, RoomSize + WallThick * 2), matCeiling);

        float wy = WallHeight * 0.5f;
        float wl = RoomSize + WallThick * 2f; // köşeler kapansın
        Make(shell, "Wall_North", PrimitiveType.Cube, new Vector3(0, wy, Half + WallThick * .5f), new Vector3(wl, WallHeight, WallThick), matWall);
        Make(shell, "Wall_South", PrimitiveType.Cube, new Vector3(0, wy, -Half - WallThick * .5f), new Vector3(wl, WallHeight, WallThick), matWall);
        Make(shell, "Wall_East",  PrimitiveType.Cube, new Vector3(Half + WallThick * .5f, wy, 0), new Vector3(WallThick, WallHeight, wl), matWall);
        Make(shell, "Wall_West",  PrimitiveType.Cube, new Vector3(-Half - WallThick * .5f, wy, 0), new Vector3(WallThick, WallHeight, wl), matWall);

        // ---------------------------------------------------- Işık
        var lightGO = new GameObject("Room_PointLight");
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.localPosition = new Vector3(0, WallHeight - 0.4f, 0);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.86f, 0.65f);
        light.range = 11f;
        light.intensity = GraphicsSettings.currentRenderPipeline != null ? 12f : 0.9f; // SRP'de birim farklı
        light.shadows = LightShadows.Soft;

        // ---------------------------------------------------- Dağınık yatak (güney-batı köşe)
        var bed = Group(root.transform, "Messy_Bed");
        Vector3 bc = new Vector3(-3.7f, 0f, -3.6f);
        Make(bed, "Bed_Frame",   PrimitiveType.Cube, bc + new Vector3(0, 0.2f, 0),        new Vector3(2.0f, 0.4f, 2.3f), matBedBase);
        Make(bed, "Bed_Headboard", PrimitiveType.Cube, bc + new Vector3(0, 0.6f, -1.2f),  new Vector3(2.0f, 1.0f, 0.1f), matBedBase);
        Make(bed, "Bed_Mattress", PrimitiveType.Cube, bc + new Vector3(0, 0.5f, 0.0f),    new Vector3(1.9f, 0.2f, 2.2f), matMattress);
        Make(bed, "Bed_Pillow_1", PrimitiveType.Cube, bc + new Vector3(-0.4f, 0.68f, -0.85f), new Vector3(0.7f, 0.16f, 0.42f), matPillow, new Vector3(0, 8f, 0));
        Make(bed, "Bed_Pillow_2", PrimitiveType.Cube, bc + new Vector3(0.45f, 0.70f, -0.75f), new Vector3(0.6f, 0.14f, 0.40f), matPillow, new Vector3(4f, -22f, 6f));
        Make(bed, "Bed_Blanket_Crumpled", PrimitiveType.Cube, bc + new Vector3(0.1f, 0.68f, 0.45f), new Vector3(1.7f, 0.14f, 1.1f), matBlanket, new Vector3(3f, 17f, -4f));
        Make(bed, "Bed_Blanket_Fallen", PrimitiveType.Cube, bc + new Vector3(1.15f, 0.12f, 0.9f), new Vector3(0.8f, 0.08f, 0.7f), matBlanket, new Vector3(0, -30f, 0));

        // ---------------------------------------------------- Bilgisayar masası (doğu duvarı)
        var desk = Group(root.transform, "Computer_Desk");
        Vector3 dc = new Vector3(Half - 0.45f, 0f, -1.4f);
        float deskH = 0.75f;
        Make(desk, "Desk_Top", PrimitiveType.Cube, dc + new Vector3(0, deskH, 0), new Vector3(0.8f, 0.06f, 1.8f), matWood);
        foreach (var sx in new[] { -0.35f, 0.35f })
            foreach (var sz in new[] { -0.8f, 0.8f })
                Make(desk, "Desk_Leg", PrimitiveType.Cube, dc + new Vector3(sx, deskH * 0.5f, sz), new Vector3(0.06f, deskH, 0.06f), matWood);
        // Monitör (ince siyah küpler)
        Make(desk, "Monitor_Stand", PrimitiveType.Cube, dc + new Vector3(0.1f, deskH + 0.12f, 0), new Vector3(0.12f, 0.2f, 0.06f), matBlack);
        Make(desk, "Monitor_Base",  PrimitiveType.Cube, dc + new Vector3(0.1f, deskH + 0.035f, 0), new Vector3(0.25f, 0.015f, 0.3f), matBlack);
        Make(desk, "Monitor_Body",  PrimitiveType.Cube, dc + new Vector3(0.1f, deskH + 0.45f, 0), new Vector3(0.04f, 0.38f, 0.7f), matBlack);
        Make(desk, "Monitor_Screen", PrimitiveType.Cube, dc + new Vector3(0.075f, deskH + 0.45f, 0), new Vector3(0.01f, 0.33f, 0.64f), matScreen);
        Make(desk, "Keyboard", PrimitiveType.Cube, dc + new Vector3(-0.2f, deskH + 0.04f, 0), new Vector3(0.18f, 0.02f, 0.5f), matBlack);
        Make(desk, "Mouse", PrimitiveType.Cube, dc + new Vector3(-0.2f, deskH + 0.04f, 0.45f), new Vector3(0.07f, 0.03f, 0.1f), matBlack);
        Make(desk, "PC_Tower", PrimitiveType.Cube, dc + new Vector3(0.05f, 0.25f, 0.6f), new Vector3(0.22f, 0.5f, 0.45f), matBlack);
        // Sandalye
        Make(desk, "Chair_Seat", PrimitiveType.Cube, dc + new Vector3(-0.85f, 0.45f, 0.1f), new Vector3(0.5f, 0.07f, 0.5f), matChair, new Vector3(0, -12f, 0));
        Make(desk, "Chair_Back", PrimitiveType.Cube, dc + new Vector3(-1.08f, 0.8f, 0.1f), new Vector3(0.06f, 0.7f, 0.5f), matChair, new Vector3(0, -12f, 0));
        Make(desk, "Chair_Post", PrimitiveType.Cube, dc + new Vector3(-0.85f, 0.22f, 0.1f), new Vector3(0.08f, 0.44f, 0.08f), matChair);

        // ---------------------------------------------------- Pizza kutuları (yerde)
        var junk = Group(root.transform, "Pizza_Boxes");
        Make(junk, "PizzaBox_1", PrimitiveType.Cube, new Vector3(1.8f, 0.03f, -3.2f), new Vector3(0.4f, 0.05f, 0.4f), matBox, new Vector3(0, 20f, 0));
        Make(junk, "PizzaBox_2", PrimitiveType.Cube, new Vector3(1.85f, 0.08f, -3.15f), new Vector3(0.4f, 0.05f, 0.4f), matBoxRed, new Vector3(0, -8f, 0));
        Make(junk, "PizzaBox_3", PrimitiveType.Cube, new Vector3(-1.2f, 0.03f, -2.0f), new Vector3(0.4f, 0.05f, 0.4f), matBox, new Vector3(0, 63f, 0));
        Make(junk, "PizzaBox_Open_Base", PrimitiveType.Cube, new Vector3(3.0f, 0.03f, 2.4f), new Vector3(0.4f, 0.05f, 0.4f), matBox, new Vector3(0, -35f, 0));
        Make(junk, "PizzaBox_Open_Lid", PrimitiveType.Cube, new Vector3(3.15f, 0.22f, 2.6f), new Vector3(0.4f, 0.03f, 0.4f), matBoxRed, new Vector3(-68f, -35f, 0));

        // ---------------------------------------------------- Ayna (KRİTİK) – kuzey duvarı
        float mirrorZ = Half - 0.04f;
        var mirror = Make(root.transform, "FinalMirror", PrimitiveType.Cube,
                          new Vector3(0, 1.55f, mirrorZ), new Vector3(2.0f, 2.6f, 0.06f), matMirror);
        // Collider'ı oda tarafına doğru kalınlaştır ki oyuncu yaklaşınca/ışın atınca yakalansın (dünyada ~0.45 m)
        var bc2 = mirror.GetComponent<BoxCollider>();
        if (bc2 == null) bc2 = mirror.AddComponent<BoxCollider>();
        bc2.size = new Vector3(1f, 1f, 8f);
        bc2.center = new Vector3(0f, 0f, -3.5f);

        var frame = Make(root.transform, "FinalMirror_Frame", PrimitiveType.Cube,
                         new Vector3(0, 1.55f, mirrorZ + 0.02f), new Vector3(2.2f, 2.8f, 0.05f), matFrame);
        UnityEngine.Object.DestroyImmediate(frame.GetComponent<BoxCollider>()); // sadece görsel

        AttachMirrorEnding(mirror);

        // ---------------------------------------------------- Işınlanma noktaları (KRİTİK)
        Vector3 mirrorPos = new Vector3(0, 0, mirrorZ);
        var player = new GameObject("PlayerFinalSpawnPoint");
        player.transform.SetParent(root.transform, false);
        player.transform.localPosition = new Vector3(-0.8f, 0.05f, -0.6f);
        var companion = new GameObject("CompanionFinalSpawnPoint");
        companion.transform.SetParent(root.transform, false);
        companion.transform.localPosition = new Vector3(0.8f, 0.05f, -0.6f);
        FaceTarget(player.transform, mirrorPos);
        FaceTarget(companion.transform, mirrorPos);

        // Küçük gizmo ikonları (sahnede kolay görülsün)
        SetIcon(player, 3);
        SetIcon(companion, 4);

        // ---------------------------------------------------- Prefab
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[EdiRoomBuilder] '{RootName}' oluşturuldu. Prefab: {PrefabPath}");
    }

    // ================================================================ Yardımcılar
    static void AttachMirrorEnding(GameObject mirror)
    {
        Type t = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in asm.GetTypes())
            {
                if (type.Name == "MirrorEnding" && typeof(Component).IsAssignableFrom(type)) { t = type; break; }
            }
            if (t != null) break;
        }
        if (t != null) mirror.AddComponent(t);
        else Debug.LogWarning("[EdiRoomBuilder] 'MirrorEnding' script'i bulunamadı (derleme hatası olabilir). Aynaya elle ekleyin.");
    }

    static void FaceTarget(Transform tr, Vector3 localTarget)
    {
        Vector3 dir = localTarget - tr.localPosition;
        dir.y = 0f;
        tr.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up); // forward -> ayna
    }

    static GameObject Make(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    static Material GetMat(string name, Color color, float smoothness, float metallic = 0f)
    {
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    static void SetIcon(GameObject go, int idx)
    {
        // 0-7: Unity'nin etiket ikonları (Mavi/Yeşil vb.)
        var tex = EditorGUIUtility.IconContent("sv_label_" + idx).image as Texture2D;
        if (tex != null) EditorGUIUtility.SetIconForObject(go, tex);
    }
}
