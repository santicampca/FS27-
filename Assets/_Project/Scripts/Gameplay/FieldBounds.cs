using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// Rectangular play area centred on this transform (X = length, Z = width).
    /// Used to keep players inside the pitch. Intents are expressed in this field space.
    /// </summary>
    public class FieldBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 halfExtents = new Vector2(20f, 12.5f);

        public Vector2 HalfExtents => halfExtents;

        public void SetHalfExtents(Vector2 value)
        {
            halfExtents = value;
        }

        /// <summary>Clamps a world position inside the field, keeping <paramref name="margin"/> from the edges.</summary>
        public Vector3 Clamp(Vector3 position, float margin)
        {
            Vector3 c = transform.position;
            position.x = Mathf.Clamp(position.x, c.x - halfExtents.x + margin, c.x + halfExtents.x - margin);
            position.z = Mathf.Clamp(position.z, c.z - halfExtents.y + margin, c.z + halfExtents.y - margin);
            return position;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, new Vector3(halfExtents.x * 2f, 0.1f, halfExtents.y * 2f));
        }
    }
}
