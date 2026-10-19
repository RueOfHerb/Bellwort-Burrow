using UnityEngine;
using UnityEngine.InputSystem;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Walks the player around with the Input System's Move action (WASD, arrow keys or a stick).
    /// With a PlayerInput on the same object it reads Move itself; without one, wire OnMove up to an input event.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;

        Rigidbody2D body;
        InputAction move;
        Vector2 moveInput;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
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
            body.linearVelocity = Vector2.ClampMagnitude(input, 1f) * moveSpeed;
        }
    }
}
