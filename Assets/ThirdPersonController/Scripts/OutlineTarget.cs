using UnityEngine;

namespace TheGlitch
{
    public class OutlineTarget : MonoBehaviour
    {
        public Material OutlineMaterial;
        public float Thickness = 0.015f;

        private GameObject _outlineObj;
        private Renderer _outlineRenderer;
        private Material _runtimeMat;

        private void Awake()
        {
            BuildOutline();
            SetOutlined(false);
        }

        private void BuildOutline()
        {
            var srcFilter = GetComponentInChildren<MeshFilter>();
            var srcRenderer = GetComponentInChildren<MeshRenderer>();

            if (srcFilter == null || srcRenderer == null || OutlineMaterial == null)
                return;

            _outlineObj = new GameObject("OutlineHull");
            _outlineObj.transform.SetParent(srcRenderer.transform, false);

            var mf = _outlineObj.AddComponent<MeshFilter>();
            mf.sharedMesh = srcFilter.sharedMesh;

            _outlineRenderer = _outlineObj.AddComponent<MeshRenderer>();

            // 运行时实例化一份材质，避免改到全局
            _runtimeMat = new Material(OutlineMaterial);
            _runtimeMat.SetFloat("_Thickness", Thickness);

            _outlineRenderer.sharedMaterial = _runtimeMat;

            // 描边只要壳，不要投影
            _outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outlineRenderer.receiveShadows = false;
        }

        public void SetOutlined(bool on)
        {
            if (_outlineObj != null)
                _outlineObj.SetActive(on);
        }

        private void OnDestroy()
        {
            if (_runtimeMat != null)
                Destroy(_runtimeMat);
        }
    }
}
