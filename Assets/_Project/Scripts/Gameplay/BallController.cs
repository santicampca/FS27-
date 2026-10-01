using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// Facade of the ball. Owns the Rigidbody + collider setup (mass, bounce, friction) and is the only
    /// way gameplay should act on the ball (<see cref="ApplyTouch"/> now; kicks/passes later).
    /// Extra forces (air drag, rolling resistance, later Magnus) live in <see cref="BallPhysics"/>.
    /// The ball is a real physics object: it is never parented to, or glued to, a player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class BallController : MonoBehaviour
    {
        [Header("Body")]
        [Tooltip("Ball diameter in metres. Larger than a real ball on purpose: it must read well from the TV camera.")]
        [SerializeField] private float diameter = 0.4f;
        [SerializeField] private float mass = 0.43f;
        [SerializeField] private float angularDamping = 0.8f;

        [Header("Surface")]
        [Range(0f, 1f)] [SerializeField] private float bounciness = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float friction = 0.6f;

        [Header("Visual (optional)")]
        [Tooltip("Child that holds the mesh. It is scaled to the diameter, so any model can replace the sphere.")]
        [SerializeField] private Transform visual;

        private Rigidbody body;
        private SphereCollider sphere;
        private PhysicsMaterial material;

        public Rigidbody Body => body;
        public float Radius => diameter * 0.5f;
        public Vector3 Position => body.position;
        public Vector3 Velocity => body.linearVelocity;
        public float Mass => mass;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphere = GetComponent<SphereCollider>();
            ApplySettings();
        }

        private void OnValidate()
        {
            diameter = Mathf.Max(0.05f, diameter);
            mass = Mathf.Max(0.01f, mass);
            if (!Application.isPlaying) return;
            if (body != null) ApplySettings();
        }

        /// <summary>Pushes the serialised settings into the Rigidbody, collider and material.</summary>
        public void ApplySettings()
        {
            body.mass = mass;
            body.useGravity = true;
            body.isKinematic = false;
            body.linearDamping = 0f;          // air drag is applied in BallPhysics (quadratic)
            body.angularDamping = angularDamping;
            body.maxAngularVelocity = 100f;
            body.maxDepenetrationVelocity = 6f; // stops a kinematic player from "launching" the ball when overlapping
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.sleepThreshold = 0.05f;

            sphere.radius = Radius;
            sphere.center = Vector3.zero;

            if (material == null)
                material = new PhysicsMaterial("Ball") { bounceCombine = PhysicsMaterialCombine.Maximum };
            material.bounciness = bounciness;
            material.dynamicFriction = friction;
            material.staticFriction = friction;
            sphere.sharedMaterial = material;

            if (visual != null) visual.localScale = Vector3.one * diameter;
        }

        /// <summary>
        /// Soft touch (dribble): nudges the ball's horizontal velocity toward a desired velocity.
        /// strength 0..1 = how much of the difference is applied this instant. Vertical speed is untouched.
        /// </summary>
        public void ApplyTouch(Vector3 desiredHorizontalVelocity, float strength)
        {
            Vector3 v = body.linearVelocity;
            Vector3 target = new Vector3(desiredHorizontalVelocity.x, v.y, desiredHorizontalVelocity.z);
            body.AddForce((target - v) * Mathf.Clamp01(strength), ForceMode.VelocityChange);
        }

        /// <summary>Puts the ball back at a position, at rest.</summary>
        public void ResetTo(Vector3 position)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            transform.position = position;
        }
    }
}
