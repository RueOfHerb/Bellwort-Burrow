using BellwortBurrow.Systems.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Walks the player around with the Input System's Move action (WASD, arrow keys or a stick).
    /// With a PlayerInput on the same object it reads Move itself; without one, wire OnMove up to an input event.
    /// Hills change the pace: an InclineWalker (added with its defaults if the player has none) slows the walk uphill
    /// and quickens it downhill. A PlayerInteractor (added the same way) handles the Interact action.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;

        Rigidbody2D body;
        InputAction move;
        Vector2 moveInput;
        Vector2 facing = Vector2.down;
        InclineWalker incline;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (!TryGetComponent(out incline)) incline = gameObject.AddComponent<InclineWalker>();
            // The hands come with the walker, so no scene has to add them (sys-8).
            if (GetComponent<PlayerInteractor>() == null) gameObject.AddComponent<PlayerInteractor>();
            if (TryGetComponent<PlayerInput>(out var input) && input.actions != null)
                move = input.actions.FindAction("Move");
        }

        /// <summary>The way the player last walked, as a unit vector; down until they move.</summary>
        public Vector2 Facing => facing;

        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        void FixedUpdate()
        {
            Vector2 input = move != null ? move.ReadValue<Vector2>() : moveInput;
            Vector2 walk = Vector2.ClampMagnitude(input, 1f);
            if (walk.sqrMagnitude > 0.01f) facing = walk.normalized;
            body.linearVelocity = incline.VelocityFor(walk, moveSpeed);
        }
    }
}
