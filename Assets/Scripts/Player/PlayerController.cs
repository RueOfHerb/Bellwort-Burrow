using BellwortBurrow.Systems.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Walks the player around with the Input System's Move action (WASD, arrow keys or a stick).
    /// With a PlayerInput on the same object it reads Move itself; without one, wire OnMove up to an input event.
    /// Hills change the pace: an InclineWalker (added with its defaults if the player has none) slows the walk uphill
    /// and quickens it downhill.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;

        Rigidbody2D body;
        InputAction move;
        Vector2 moveInput;
        InclineWalker incline;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (!TryGetComponent(out incline)) incline = gameObject.AddComponent<InclineWalker>();
            if (TryGetComponent<PlayerInput>(out var input) && input.actions != null)
                move = input.actions.FindAction("Move");
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        void FixedUpdate()
        {
            Vector2 input = move != null ? move.ReadValue<Vector2>() : moveInput;
            Vector2 walk = Vector2.ClampMagnitude(input, 1f);
            body.linearVelocity = walk * (moveSpeed * incline.SpeedFor(walk));
        }
    }
}
