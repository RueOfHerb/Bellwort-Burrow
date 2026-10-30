using UnityEngine;
using UnityEngine.InputSystem;
using BellwortBurrow.Systems.World;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// The player's hands (sys-5, sys-8, sys-10). The Interact action (E, or a gamepad button) uses the nearest
    /// interactable within reach of a point just in front of the player's feet: a forageable gives its item, a door is
    /// the bed. With nothing in reach, it works the farm's ground at that point (FarmField): till, plant, water,
    /// harvest. With a PlayerInput on the same object it reads Interact itself (E, a left click or the gamepad's top
    /// button); without one, wire OnInteract up to an input event.
    /// The mouse can pick instead (ui-5): while the pointer is over one of the tiles round the player (within
    /// <see cref="mouseRange"/> of the tile they stand on), that tile is the target rather than the one in front, and
    /// using it turns the player toward it. After a gamepad is used the mouse is ignored until it moves again.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("How far from the point in front of the feet an interactable may be, in units (16 px each).")]
        [SerializeField, Min(0.1f)] float reach = 1.25f;
        [Tooltip("How far in front of the feet the reach is centred, in the direction the player faces.")]
        [SerializeField, Min(0f)] float ahead = 0.5f;
        [Tooltip("Mark what Interact would use right now with corner brackets (ui-5).")]
        [SerializeField] bool showTarget = true;
        [Tooltip("How many tiles round the one the player stands on the mouse can pick (1: the eight round it and their own).")]
        [SerializeField, Range(0, 3)] int mouseRange = 1;
        [Tooltip("How far from the tile under the mouse an interactable may be, in units.")]
        [SerializeField, Min(0.1f)] float mouseReach = 0.75f;

        bool gamepadLast;

        InputAction interact;
        PlayerController controller;

        public float Reach => reach;

        void Awake()
        {
            TryGetComponent(out controller);
            if (TryGetComponent<PlayerInput>(out var input) && input.actions != null)
                interact = input.actions.FindAction("Interact");
            if (showTarget && GetComponentInChildren<TargetIndicator>() == null)
            {
                var marker = new GameObject("Target indicator");
                marker.transform.SetParent(transform, false);
                marker.AddComponent<TargetIndicator>().Follow(this);
            }
        }

        void Update()
        {
            var pad = Gamepad.current;
            var mouse = Mouse.current;
            if (pad != null && pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f || AnyButton(pad))) gamepadLast = true;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0.5f || mouse.leftButton.wasPressedThisFrame)) gamepadLast = false;
            // The press, not the action's Hold interaction, so a tap of E (or a click) is enough.
            if (interact != null && interact.WasPressedThisFrame()) InteractNearby();
        }

        static bool AnyButton(Gamepad pad) =>
            pad.buttonSouth.isPressed || pad.buttonNorth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed;

        /// <summary>
        /// The middle of the tile under the mouse, when it's one of the tiles round the player the mouse may pick. False
        /// with no mouse or camera, after a gamepad was used, or when the pointer is further away.
        /// </summary>
        public bool TryGetMouseTile(out Vector2 point)
        {
            point = default;
            var mouse = Mouse.current;
            var camera = Camera.main;
            if (gamepadLast || mouse == null || camera == null || !Application.isFocused) return false;
            Vector2 screen = mouse.position.ReadValue();
            if (screen.x < 0 || screen.y < 0 || screen.x > Screen.width || screen.y > Screen.height) return false;
            Vector2 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return TileNear(transform.position, world, mouseRange, out point);
        }

        /// <summary>Whether a point is on a tile within some tiles of the one under the feet, and that tile's middle.</summary>
        public static bool TileNear(Vector2 feet, Vector2 point, int range, out Vector2 middle)
        {
            Vector2Int mine = new(Mathf.FloorToInt(feet.x), Mathf.FloorToInt(feet.y));
            Vector2Int under = new(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            middle = under + new Vector2(0.5f, 0.5f);
            return Mathf.Abs(under.x - mine.x) <= range && Mathf.Abs(under.y - mine.y) <= range;
        }

        // Where to look, and how far round it: the tile under the mouse when it may pick, else just in front of the feet.
        (Vector2 at, float within, bool byMouse) Aim() =>
            TryGetMouseTile(out var tile) ? (tile, mouseReach, true) : (ReachPoint, reach, false);

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.started) InteractNearby();
        }

        /// <summary>The point the reach is measured from: a little in front of the feet, the way the player faces.</summary>
        public Vector2 ReachPoint
        {
            get
            {
                Vector2 facing = controller != null ? controller.Facing : Vector2.down;
                return (Vector2)transform.position + facing * ahead;
            }
        }

        /// <summary>
        /// What Interact would use right now, framed in world space: the nearest interactable within reach (round its
        /// sprites), or else the farm cell in front when working it would do something. False when Interact would do nothing.
        /// </summary>
        public bool TryGetTarget(out Bounds frame)
        {
            var (at, within, _) = Aim();
            var target = Interactables.Nearest(at, within, gameObject);
            if (target != null)
            {
                frame = FrameOf(target);
                IsFarmTarget = false;
                return true;
            }
            var farm = FarmField.TargetAt(at, out _);
            frame = farm != null ? farm.CellBounds(at) : default;
            IsFarmTarget = farm != null;
            return farm != null;
        }

        /// <summary>Whether the last target found was a farm tile rather than an interactable.</summary>
        public bool IsFarmTarget { get; private set; }

        // Round everything the interactable draws, or a tile round its position when it draws nothing.
        static Bounds FrameOf(IInteractable target)
        {
            if (target is Component component)
            {
                bool any = false;
                var frame = default(Bounds);
                foreach (var sprite in component.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!sprite.enabled || sprite.sprite == null) continue;
                    if (!any) { frame = sprite.bounds; any = true; }
                    else frame.Encapsulate(sprite.bounds);
                }
                if (any) return frame;
            }
            return new Bounds(target.Position, Vector3.one);
        }

        /// <summary>Uses the nearest interactable within reach, or else the farm's ground in front. True when something was used.</summary>
        public bool InteractNearby()
        {
            var (at, within, byMouse) = Aim();
            if (byMouse && controller != null) controller.FaceToward(at);
            var target = Interactables.Nearest(at, within, gameObject);
            if (target == null) return FarmField.TryUseAny(at, gameObject);
            target.Interact(gameObject);
            return true;
        }
    }
}
