using UnityEngine;
using UnityEngine.InputSystem;

namespace BellwortBurrow.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 4f;

        Rigidbody2D body;
        Vector2 moveInput;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        void FixedUpdate()
        {
            body.linearVelocity = moveInput.normalized * moveSpeed;
        }
    }
}
