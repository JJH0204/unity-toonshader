using UnityEngine;

namespace CharacterToon
{
    /// <summary>
    /// Injects face orientation vectors as global shader properties (_FaceForwardWS, _FaceRightWS)
    /// for SRP Batcher compatibility. The face SDF path samples left/right halves and flips based
    /// on light position relative to these frame vectors. Single-character for now (multi-character
    /// is decision #4).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CharacterToonFace : MonoBehaviour
    {
        [SerializeField]
        private Transform _faceAnchor;

        private static readonly int FaceForwardWSId = Shader.PropertyToID("_FaceForwardWS");
        private static readonly int FaceRightWSId = Shader.PropertyToID("_FaceRightWS");

        private void LateUpdate()
        {
            if (_faceAnchor != null)
            {
                Shader.SetGlobalVector(FaceForwardWSId, new Vector4(_faceAnchor.forward.x, _faceAnchor.forward.y, _faceAnchor.forward.z, 1f));
                Shader.SetGlobalVector(FaceRightWSId, new Vector4(_faceAnchor.right.x, _faceAnchor.right.y, _faceAnchor.right.z, 1f));
            }
        }

        // Leave globals bound on disable; face orientation is typically baked into the character skeleton.
        private void OnDisable()
        {
            // No clear needed; face vectors remain valid across disable.
        }
    }
}
