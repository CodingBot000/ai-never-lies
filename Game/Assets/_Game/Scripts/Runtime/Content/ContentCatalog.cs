using UnityEngine;

namespace ANL.Runtime.Content
{
    [CreateAssetMenu(menuName = "ANL/Content Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        public TextAsset[] files;
        public string artManifestSha256;
        public string contentVersion = "ep01.v1.0.0";
    }
}
