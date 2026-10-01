using FS27.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FS27.Input
{
    /// <summary>
    /// On-screen joystick for the left thumb. This object's RectTransform is the touch area (invisible,
    /// but it must have a raycast-enabled Image). The stick appears under the thumb ("floating") and
    /// returns to a resting spot when released.
    ///
    /// Output is the stick deflection (0..1) as a <see cref="PlayerIntent"/>: how far the thumb is from
    /// the centre selects jog / run / sprint, exactly like an analogue gamepad stick. There is no sprint button.
    ///
    /// Sizes are in canvas units: with a Canvas Scaler (Scale With Screen Size) they scale with the screen.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IIntentSource, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;

        [SerializeField] private RectTransform zone;
        [Tooltip("Stick background. Its anchors must be bottom-left (0,0) of the zone.")]
        [SerializeField] private RectTransform stickBase;
        [Tooltip("Moving knob, child of the base (anchors centred).")]
        [SerializeField] private RectTransform knob;
        [SerializeField] private CanvasGroup visuals;

        [Header("Behaviour")]
        [Tooltip("Distance (canvas units) the thumb can move from the centre. Full deflection = this far.")]
        [SerializeField] private float radius = 150f;
        [Tooltip("Where the stick rests when not touched, measured from the zone's bottom-left corner.")]
        [SerializeField] private Vector2 restPosition = new Vector2(280f, 260f);
        [Tooltip("If true the stick appears where the thumb lands; if false it stays at the rest position.")]
        [SerializeField] private bool floating = true;
        [SerializeField] private float idleAlpha = 0.45f;

        private int activePointer = NoPointer;
        private Vector2 baseAnchored;
        private Vector2 value;

        public PlayerIntent ReadIntent()
        {
            return new PlayerIntent(new Vec2(value.x, value.y));
        }

        private void Reset()
        {
            zone = transform as RectTransform;
        }

        private void Awake()
        {
            if (zone == null) zone = transform as RectTransform;
            ResetStick();
        }

        private void Start()
        {
            // Default drag threshold (10 px) would swallow small thumb movements.
            if (EventSystem.current != null) EventSystem.current.pixelDragThreshold = 1;
        }

        private void OnDisable()
        {
            ResetStick();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointer != NoPointer) return;
            activePointer = eventData.pointerId;

            Vector2 local = ToZonePosition(eventData);
            if (floating)
            {
                Vector2 size = zone.rect.size;
                local.x = Mathf.Clamp(local.x, radius, Mathf.Max(radius, size.x - radius));
                local.y = Mathf.Clamp(local.y, radius, Mathf.Max(radius, size.y - radius));
                baseAnchored = local;
            }
            else
            {
                baseAnchored = restPosition;
            }

            stickBase.anchoredPosition = baseAnchored;
            if (visuals != null) visuals.alpha = 1f;
            UpdateValue(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;
            UpdateValue(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;
            ResetStick();
        }

        private void UpdateValue(PointerEventData eventData)
        {
            Vector2 offset = ToZonePosition(eventData) - baseAnchored;
            value = Vector2.ClampMagnitude(offset / radius, 1f);
            knob.anchoredPosition = value * radius;
        }

        private void ResetStick()
        {
            activePointer = NoPointer;
            value = Vector2.zero;
            baseAnchored = restPosition;
            if (stickBase != null) stickBase.anchoredPosition = restPosition;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (visuals != null) visuals.alpha = idleAlpha;
        }

        /// <summary>Pointer position relative to the zone's bottom-left corner, in canvas units.</summary>
        private Vector2 ToZonePosition(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out Vector2 local);
            return local - zone.rect.min;
        }
    }
}
