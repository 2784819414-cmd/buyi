using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Procedural 2.5D door that rotates around a hinge pivot and follows CampusPlacedObject rotation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CampusDoor3D : MonoBehaviour, ICampusInteractable
    {
        private const string GeneratedMeshName = "CampusDoor3D_GeneratedMesh";

        public Transform DoorPivot;
        public MeshFilter DoorMeshFilter;
        public MeshRenderer DoorMeshRenderer;
        public Material DoorFaceMaterial;
        public Material DoorTopMaterial;
        public Collider2D DoorCollider;
        public Collider2D InteractionCollider;
        public CampusPlacedObject PlacedObject;
        public bool StartsOpen;
        public float OpenAngle = 90f;
        public float AnimationDuration = 0.22f;
        public float PanelWidth = 0.94f;
        public float LowerFaceBaseExposure = 0.18f;
        public float LowerFaceTopExposure = 0.075f;
        public float UpperFaceBaseExposure = 0.12f;
        public float UpperFaceTopExposure = 0.055f;
        public float PanelDepth = 0.42f;
        public float TopDepth = -0.015f;
        public float EndBevel = 0.055f;
        public float HingeWidth = 0.06f;
        public bool OpenClockwise;

        [SerializeField]
        private bool isOpen;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> faceTriangles = new List<int>();
        private readonly List<int> topTriangles = new List<int>();
        private Coroutine animationRoutine;
        private Mesh generatedMesh;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            CacheReferences();
            RebuildDoorMesh();

            if (Application.isPlaying)
            {
                SetOpenImmediate(StartsOpen);
            }
        }

        private void OnEnable()
        {
            CacheReferences();
            RebuildDoorMesh();

            if (!Application.isPlaying)
            {
                SetOpenImmediate(StartsOpen);
            }
        }

        private void Reset()
        {
            CacheReferences();
            RebuildDoorMesh();
            SetOpenImmediate(StartsOpen);
        }

        private void OnValidate()
        {
            PanelWidth = Mathf.Max(0.1f, PanelWidth);
            LowerFaceBaseExposure = Mathf.Max(0.02f, LowerFaceBaseExposure);
            LowerFaceTopExposure = Mathf.Clamp(LowerFaceTopExposure, 0.02f, LowerFaceBaseExposure);
            UpperFaceBaseExposure = Mathf.Max(0.02f, UpperFaceBaseExposure);
            UpperFaceTopExposure = Mathf.Clamp(UpperFaceTopExposure, 0.02f, UpperFaceBaseExposure);
            PanelDepth = Mathf.Max(0.02f, PanelDepth);
            EndBevel = Mathf.Clamp(EndBevel, 0f, PanelWidth * 0.45f);
            HingeWidth = Mathf.Clamp(HingeWidth, 0.01f, PanelWidth * 0.2f);
            AnimationDuration = Mathf.Max(0f, AnimationDuration);

            CacheReferences();
            RebuildDoorMesh();

            if (!Application.isPlaying)
            {
                SetOpenImmediate(StartsOpen);
            }
        }

        private void OnDestroy()
        {
            if (generatedMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedMesh);
            }
            else
            {
                DestroyImmediate(generatedMesh);
            }

            generatedMesh = null;
        }

        public void Interact(GameObject actor)
        {
            ToggleOpen();
        }

        public void ToggleOpen()
        {
            SetOpen(!isOpen);
        }

        public void Open()
        {
            SetOpen(true);
        }

        public void Close()
        {
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            CacheReferences();
            if (!Application.isPlaying || AnimationDuration <= 0f)
            {
                SetOpenImmediate(open);
                return;
            }

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimateDoor(open));
        }

        public void SetOpenImmediate(bool open)
        {
            if (animationRoutine != null && Application.isPlaying)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            isOpen = open;
            ApplyVisualState(open ? GetSignedOpenAngle() : 0f);
            ApplyBlockingState(open, true);
        }

        public void RebuildDoorMesh()
        {
            if (DoorMeshFilter == null)
            {
                return;
            }

            Mesh mesh = GetOrCreateMesh();
            BuildDoorLeafMesh(mesh);
            DoorMeshFilter.sharedMesh = mesh;
            ApplyMaterials();
            ApplyColliderDefaults();
            ApplyVisualState(isOpen ? GetSignedOpenAngle() : 0f);
        }

        private IEnumerator AnimateDoor(bool open)
        {
            isOpen = open;
            ApplyBlockingState(open, false);

            float startAngle = GetCurrentSwingAngle();
            float targetAngle = open ? GetSignedOpenAngle() : 0f;
            float elapsed = 0f;
            while (elapsed < AnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / AnimationDuration);
                t = t * t * (3f - 2f * t);
                ApplyVisualState(Mathf.Lerp(startAngle, targetAngle, t));
                yield return null;
            }

            ApplyVisualState(targetAngle);
            ApplyBlockingState(open, true);
            animationRoutine = null;
        }

        private void ApplyVisualState(float swingAngle)
        {
            if (DoorPivot == null)
            {
                return;
            }

            DoorPivot.localPosition = GetClosedPivotLocalPosition();
            DoorPivot.localRotation = Quaternion.Euler(0f, 0f, GetClosedAngle() + swingAngle);
        }

        private void ApplyBlockingState(bool open, bool finalState)
        {
            bool blocks = !open && finalState;
            if (DoorCollider != null)
            {
                DoorCollider.enabled = blocks;
            }

            if (InteractionCollider != null)
            {
                InteractionCollider.enabled = true;
                InteractionCollider.isTrigger = true;
            }

            if (PlacedObject != null)
            {
                PlacedObject.BlocksMovement = blocks;
                PlacedObject.BlocksSight = blocks;
                PlacedObject.IsInteractable = true;
            }
        }

        private void CacheReferences()
        {
            if (DoorPivot == null)
            {
                DoorPivot = CampusObjectNames.FindDirectChild(transform, CampusObjectNames.DoorPivot, CampusObjectNames.LegacyDoorPivot);
            }

            if (DoorPivot == null)
            {
                DoorPivot = CreateChild(transform, CampusObjectNames.DoorPivot);
            }

            Transform visual = DoorPivot != null
                ? CampusObjectNames.FindDirectChild(DoorPivot, CampusObjectNames.DoorVisual, CampusObjectNames.LegacyDoorVisual)
                : CampusObjectNames.FindDirectChild(transform, CampusObjectNames.DoorVisual, CampusObjectNames.LegacyDoorVisual);

            if (visual == null && DoorPivot != null)
            {
                visual = CreateChild(DoorPivot, CampusObjectNames.DoorVisual);
            }

            if (DoorMeshFilter == null && visual != null)
            {
                DoorMeshFilter = visual.GetComponent<MeshFilter>();
            }

            if (DoorMeshFilter == null && visual != null)
            {
                DoorMeshFilter = visual.gameObject.AddComponent<MeshFilter>();
            }

            if (DoorMeshRenderer == null && visual != null)
            {
                DoorMeshRenderer = visual.GetComponent<MeshRenderer>();
            }

            if (DoorMeshRenderer == null && visual != null)
            {
                DoorMeshRenderer = visual.gameObject.AddComponent<MeshRenderer>();
            }

            if (DoorCollider == null)
            {
                Transform colliderTransform = DoorPivot != null
                    ? CampusObjectNames.FindDirectChild(DoorPivot, CampusObjectNames.DoorCollider, CampusObjectNames.LegacyDoorCollider)
                    : CampusObjectNames.FindDirectChild(transform, CampusObjectNames.DoorCollider, CampusObjectNames.LegacyDoorCollider);
                if (colliderTransform == null && DoorPivot != null)
                {
                    colliderTransform = CreateChild(DoorPivot, CampusObjectNames.DoorCollider);
                }

                DoorCollider = colliderTransform != null ? colliderTransform.GetComponent<Collider2D>() : null;
                if (DoorCollider == null && colliderTransform != null)
                {
                    DoorCollider = colliderTransform.gameObject.AddComponent<BoxCollider2D>();
                }
            }

            if (InteractionCollider == null)
            {
                Collider2D[] colliders = GetComponents<Collider2D>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null && colliders[i].isTrigger)
                    {
                        InteractionCollider = colliders[i];
                        break;
                    }
                }
            }

            if (InteractionCollider == null)
            {
                BoxCollider2D trigger = gameObject.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                InteractionCollider = trigger;
            }

            if (PlacedObject == null)
            {
                PlacedObject = GetComponent<CampusPlacedObject>();
            }

            if (PlacedObject == null)
            {
                PlacedObject = gameObject.AddComponent<CampusPlacedObject>();
            }
        }

        private static Transform CreateChild(Transform parent, string childName)
        {
            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            return childObject.transform;
        }

        private Mesh GetOrCreateMesh()
        {
            if (generatedMesh != null)
            {
                return generatedMesh;
            }

            Mesh existing = DoorMeshFilter != null ? DoorMeshFilter.sharedMesh : null;
            if (existing != null && existing.name == GeneratedMeshName)
            {
                generatedMesh = existing;
                return generatedMesh;
            }

            generatedMesh = new Mesh
            {
                name = GeneratedMeshName,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            return generatedMesh;
        }

        private void BuildDoorLeafMesh(Mesh mesh)
        {
            vertices.Clear();
            uvs.Clear();
            faceTriangles.Clear();
            topTriangles.Clear();

            float width = Mathf.Max(0.1f, PanelWidth);
            float lowerBase = Mathf.Max(0.02f, LowerFaceBaseExposure);
            float lowerTop = Mathf.Clamp(LowerFaceTopExposure, 0.02f, lowerBase);
            float upperBase = Mathf.Max(0.02f, UpperFaceBaseExposure);
            float upperTop = Mathf.Clamp(UpperFaceTopExposure, 0.02f, upperBase);
            float bevel = Mathf.Clamp(EndBevel, 0f, width * 0.45f);
            float hingeWidth = Mathf.Clamp(HingeWidth, 0.01f, width * 0.2f);
            float topZ = TopDepth;
            float baseZ = PanelDepth;

            Vector3 bSW = new Vector3(0f, -lowerBase, baseZ);
            Vector3 bSE = new Vector3(width, -lowerBase, baseZ);
            Vector3 bNE = new Vector3(width, upperBase, baseZ);
            Vector3 bNW = new Vector3(0f, upperBase, baseZ);
            Vector3 tSW = new Vector3(bevel, -lowerTop, topZ);
            Vector3 tSE = new Vector3(width - bevel, -lowerTop, topZ);
            Vector3 tNE = new Vector3(width - bevel, upperTop, topZ);
            Vector3 tNW = new Vector3(bevel, upperTop, topZ);

            AddQuad(tSW, tSE, tNE, tNW, topTriangles);
            AddQuad(bSE, bSW, tSW, tSE, faceTriangles);
            AddQuad(bNW, bNE, tNE, tNW, faceTriangles, 0.08f, 0f, 0.72f, 1f);
            AddQuad(bSW, bNW, tNW, tSW, faceTriangles, 0f, 0f, 0.08f, 1f);
            AddQuad(bNE, bSE, tSE, tNE, faceTriangles, 0.92f, 0f, 1f, 1f);

            AddHingeGeometry(lowerBase, lowerTop, upperBase, upperTop, baseZ, topZ, hingeWidth);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(faceTriangles, 0);
            mesh.SetTriangles(topTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void AddHingeGeometry(float lowerBase, float lowerTop, float upperBase, float upperTop, float baseZ, float topZ, float hingeWidth)
        {
            float hingeMinX = -hingeWidth;
            float hingeMaxX = 0f;
            float hingeTopInset = hingeWidth * 0.2f;

            Vector3 bSW = new Vector3(hingeMinX, -lowerBase, baseZ);
            Vector3 bSE = new Vector3(hingeMaxX, -lowerBase, baseZ);
            Vector3 bNE = new Vector3(hingeMaxX, upperBase, baseZ);
            Vector3 bNW = new Vector3(hingeMinX, upperBase, baseZ);
            Vector3 tSW = new Vector3(hingeMinX + hingeTopInset, -lowerTop, topZ);
            Vector3 tSE = new Vector3(hingeMaxX, -lowerTop, topZ);
            Vector3 tNE = new Vector3(hingeMaxX, upperTop, topZ);
            Vector3 tNW = new Vector3(hingeMinX + hingeTopInset, upperTop, topZ);

            AddQuad(tSW, tSE, tNE, tNW, topTriangles, 0f, 0f, 0.12f, 1f);
            AddQuad(bSE, bSW, tSW, tSE, faceTriangles, 0f, 0f, 0.12f, 1f);
            AddQuad(bNW, bNE, tNE, tNW, faceTriangles, 0f, 0f, 0.12f, 1f);
            AddQuad(bSW, bNW, tNW, tSW, faceTriangles, 0f, 0f, 0.12f, 1f);
        }

        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> triangles)
        {
            AddQuad(a, b, c, d, triangles, 0f, 0f, 1f, 1f);
        }

        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> triangles, float uMin, float vMin, float uMax, float vMax)
        {
            int index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            uvs.Add(new Vector2(uMin, vMin));
            uvs.Add(new Vector2(uMax, vMin));
            uvs.Add(new Vector2(uMax, vMax));
            uvs.Add(new Vector2(uMin, vMax));

            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        private void ApplyMaterials()
        {
            if (DoorMeshRenderer == null)
            {
                return;
            }

            Material face = DoorFaceMaterial != null ? DoorFaceMaterial : ResolveExistingMaterial(0);
            Material top = DoorTopMaterial != null ? DoorTopMaterial : ResolveExistingMaterial(1);
            DoorMeshRenderer.sharedMaterials = new[]
            {
                face,
                top != null ? top : face
            };
            DoorMeshRenderer.sortingLayerID = SortingLayer.NameToID("Default");
            DoorMeshRenderer.sortingOrder = 300;
        }

        private Material ResolveExistingMaterial(int index)
        {
            if (DoorMeshRenderer == null)
            {
                return null;
            }

            Material[] materials = DoorMeshRenderer.sharedMaterials;
            return materials != null && index >= 0 && index < materials.Length ? materials[index] : null;
        }

        private void ApplyColliderDefaults()
        {
            if (DoorCollider is BoxCollider2D blocker)
            {
                blocker.isTrigger = false;
                blocker.offset = new Vector2(Mathf.Max(0.1f, PanelWidth) * 0.5f, GetPanelCenterY());
                blocker.size = new Vector2(Mathf.Max(0.1f, PanelWidth), GetBaseThickness());
            }

            if (InteractionCollider is BoxCollider2D trigger)
            {
                trigger.isTrigger = true;
                trigger.offset = new Vector2(0f, GetPanelCenterY());
                float triggerSize = Mathf.Max(1f, PanelWidth + 0.45f);
                trigger.size = new Vector2(triggerSize, triggerSize);
            }
        }

        private float GetCurrentSwingAngle()
        {
            if (DoorPivot == null)
            {
                return 0f;
            }

            return Mathf.DeltaAngle(GetClosedAngle(), DoorPivot.localEulerAngles.z);
        }

        private float GetClosedAngle()
        {
            return ResolvePlacementRotation90() * 90f;
        }

        private Vector3 GetClosedPivotLocalPosition()
        {
            float halfWidth = Mathf.Max(0.1f, PanelWidth) * 0.5f;
            float angle = GetClosedAngle() * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Cos(angle) * halfWidth, -Mathf.Sin(angle) * halfWidth, 0f);
        }

        private int ResolvePlacementRotation90()
        {
            if (PlacedObject != null)
            {
                return PlacedObject.ResolveAllowedRotation90(PlacedObject.Rotation90);
            }

            return CampusPlacedObject.NormalizeRotation90(Mathf.RoundToInt(transform.localEulerAngles.z / 90f));
        }

        private float GetSignedOpenAngle()
        {
            float magnitude = Mathf.Abs(OpenAngle);
            return OpenClockwise ? -magnitude : magnitude;
        }

        private float GetPanelCenterY()
        {
            return (Mathf.Max(0.02f, UpperFaceBaseExposure) - Mathf.Max(0.02f, LowerFaceBaseExposure)) * 0.5f;
        }

        private float GetBaseThickness()
        {
            return Mathf.Max(0.02f, LowerFaceBaseExposure) + Mathf.Max(0.02f, UpperFaceBaseExposure);
        }
    }
}
