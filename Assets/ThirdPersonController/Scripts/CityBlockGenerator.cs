using UnityEngine;
using UnityEngine.ProBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TheGlitch
{
    public class CityBlock_ZonedGenerator : MonoBehaviour
    {
        [Header("Overall")]
        public float StreetX = 0f;
        public float StreetHalfWidthA = 5.5f;   // Zone A
        public float StreetHalfWidthB = 3.0f;   // Zone B (canyon)
        public float StreetHalfWidthC = 7.0f;   // Zone C (plaza)
        public float StreetHalfWidthD = 6.0f;   // Zone D (forecourt)

        [Header("Z Segments")]
        public float Z0 = -70f;   // start
        public float Z1 = -45f;   // end Zone A
        public float Z2 = -15f;   // end Zone B
        public float Z3 = 15f;   // end Zone C
        public float Z4 = 45f;   // end Zone D (target near)

        [Header("Wall / Building")]
        public float WallThickness = 1.0f;
        public float WallHeightB = 14f;
        public float WallHeightA = 8f;
        public float WallHeightC = 12f;
        public float WallHeightD = 16f;

        [Header("Materials (URP Lit)")]
        public Color ZoneAColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        public Color ZoneBColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        public Color ZoneCColor = new Color(0.50f, 0.48f, 0.40f, 1f);
        public Color ZoneDColor = new Color(0.38f, 0.42f, 0.50f, 1f);

        public Color WallColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        public Color LandmarkColor = new Color(0.70f, 0.70f, 0.70f, 1f);

        private Material _matA, _matB, _matC, _matD, _matWall, _matLandmark;

#if UNITY_EDITOR
        [ContextMenu("Generate Zoned City Block")]
        public void Generate()
        {
            PrepareMaterials();
            ClearChildren();

            Transform root = transform;
            Transform geo = CreateGroup("Geo", root);
            Transform zones = CreateGroup("Zones", root);
            Transform landmarks = CreateGroup("Landmarks", root);
            Transform markers = CreateGroup("Markers", root);

            // ===== Zone floors (the BIG readability win) =====
            CreateZoneFloor(zones, "Zone_A_Buffer", Z0, Z1, StreetHalfWidthA, _matA);
            CreateZoneFloor(zones, "Zone_B_Canyon", Z1, Z2, StreetHalfWidthB, _matB);
            CreateZoneFloor(zones, "Zone_C_Plaza", Z2, Z3, StreetHalfWidthC, _matC);
            CreateZoneFloor(zones, "Zone_D_Forecourt", Z3, Z4, StreetHalfWidthD, _matD);

            // ===== Continuous street walls (make it feel like ¡°rooms¡±) =====
            // Zone A walls (lower)
            CreateStreetWalls(geo, "Walls_A", Z0, Z1, StreetHalfWidthA, WallHeightA, _matWall);
            // Zone B walls (taller, canyon feel)
            CreateStreetWalls(geo, "Walls_B", Z1, Z2, StreetHalfWidthB, WallHeightB, _matWall);
            // Zone C plaza has partial walls (open it up)
            CreateStreetWalls(geo, "Walls_C", Z2, Z3, StreetHalfWidthC, WallHeightC, _matWall, broken: true);
            // Zone D walls (highest, target pressure)
            CreateStreetWalls(geo, "Walls_D", Z3, Z4, StreetHalfWidthD, WallHeightD, _matWall);

            // ===== Landmarks (one per zone) =====
            // A: small arch-ish marker
            CreateLandmark(landmarks, "LM_A_Arch", new Vector3(0, 3.5f, (Z0 + Z1) * 0.5f), new Vector3(10f, 7f, 1.2f));
            // B: tall chimney/tower on one side
            CreateLandmark(landmarks, "LM_B_Tower", new Vector3(-8f, 10f, (Z1 + Z2) * 0.5f), new Vector3(3f, 20f, 3f));
            // C: billboard frame near plaza edge
            CreateLandmark(landmarks, "LM_C_BillboardFrame", new Vector3(10f, 6f, Z2 + 6f), new Vector3(1.2f, 12f, 8f));
            // D: target silhouette (big)
            CreateLandmark(landmarks, "LM_D_TargetSilhouette", new Vector3(0f, 18f, Z4 - 6f), new Vector3(34f, 36f, 16f));

            // ===== Markers =====
            CreateMarker(markers, "M_Start", new Vector3(0, 0, Z0 + 2f));
            CreateMarker(markers, "M_ZoneA_End", new Vector3(0, 0, Z1));
            CreateMarker(markers, "M_ZoneB_End", new Vector3(0, 0, Z2));
            CreateMarker(markers, "M_ZoneC_End", new Vector3(0, 0, Z3));
            CreateMarker(markers, "M_TargetApproach", new Vector3(0, 0, Z4 - 10f));

            Debug.Log("Zoned city block generated. You should clearly see A/B/C/D regions now.");
        }

        // ---------------- Zone floor ----------------
        void CreateZoneFloor(Transform parent, string name, float zStart, float zEnd, float halfWidth, Material mat)
        {
            float len = Mathf.Abs(zEnd - zStart);
            float zMid = (zStart + zEnd) * 0.5f;

            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(halfWidth * 2f, 0.25f, len));
            var go = pb.gameObject;
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(StreetX, -0.125f, zMid);

            pb.ToMesh();
            pb.Refresh();

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        // ---------------- Street walls (continuous) ----------------
        void CreateStreetWalls(Transform parent, string groupName, float zStart, float zEnd, float halfWidth, float height, Material mat, bool broken = false)
        {
            Transform g = CreateGroup(groupName, parent);
            float len = Mathf.Abs(zEnd - zStart);
            float zMid = (zStart + zEnd) * 0.5f;

            // If broken (plaza), make 2 segments with a gap in middle
            if (broken)
            {
                float segLen = len * 0.4f;
                float gapLen = len - segLen * 2f;

                float leftZ1 = zStart + segLen * 0.5f;
                float leftZ2 = zEnd - segLen * 0.5f;

                // Left wall segments
                CreateWall(g, "Wall_L_1", -halfWidth - WallThickness * 0.5f, leftZ1, segLen, height, mat);
                CreateWall(g, "Wall_L_2", -halfWidth - WallThickness * 0.5f, leftZ2, segLen, height, mat);

                // Right wall segments
                CreateWall(g, "Wall_R_1", +halfWidth + WallThickness * 0.5f, leftZ1, segLen, height, mat);
                CreateWall(g, "Wall_R_2", +halfWidth + WallThickness * 0.5f, leftZ2, segLen, height, mat);

                return;
            }

            // Continuous walls
            CreateWall(g, "Wall_L", -halfWidth - WallThickness * 0.5f, zMid, len, height, mat);
            CreateWall(g, "Wall_R", +halfWidth + WallThickness * 0.5f, zMid, len, height, mat);
        }

        void CreateWall(Transform parent, string name, float x, float z, float len, float height, Material mat)
        {
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(WallThickness, height, len));
            var go = pb.gameObject;
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, height * 0.5f, z);

            pb.ToMesh();
            pb.Refresh();

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        // ---------------- Landmarks ----------------
        void CreateLandmark(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            var go = pb.gameObject;
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;

            pb.ToMesh();
            pb.Refresh();

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = _matLandmark;
        }

        // ---------------- Materials ----------------
        void PrepareMaterials()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
            {
                Debug.LogError("URP Lit shader not found. Are you in URP?");
                return;
            }

            _matA = new Material(s) { color = ZoneAColor };
            _matB = new Material(s) { color = ZoneBColor };
            _matC = new Material(s) { color = ZoneCColor };
            _matD = new Material(s) { color = ZoneDColor };

            _matWall = new Material(s) { color = WallColor };
            _matLandmark = new Material(s) { color = LandmarkColor };
        }

        // ---------------- Utils ----------------
        Transform CreateGroup(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }

        void CreateMarker(Transform parent, string name, Vector3 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }
#endif
    }
}
