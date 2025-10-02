using UnityEngine;

namespace NewCss
{
    public class BoxFallPenalty : MonoBehaviour
    {
        [Header("Penalty Settings")]
        [Tooltip("Money deducted when the box hits the ground")]
        public int dropMoneyPenalty = 10;
        
        [Tooltip("Prestige deducted when the box hits the ground")]
        public float dropPrestigePenalty = 0.01f;

        private bool hasBeenThrown = false;
        private bool penaltyApplied = false;
        private Rigidbody rb;
        private float throwVelocityThreshold = 1f; // Minimum velocity to consider as "thrown"

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            ValidateComponents();
        }

        public void SetThrown()
        {
            hasBeenThrown = true;
            penaltyApplied = false;
        }

        public void SetThrownWithVelocity()
        {
            if (rb != null && rb.linearVelocity.magnitude > throwVelocityThreshold)
            {
                SetThrown();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!hasBeenThrown)
            {
                if (rb != null && rb.linearVelocity.magnitude > throwVelocityThreshold)
                {
                    SetThrown();
                }
                else
                {
                    return;
                }
            }

            if (penaltyApplied)
            {
                return;
            }

            if (IsGroundCollision(collision))
            {
                ApplyPenalty();
            }
        }

        private bool IsGroundCollision(Collision collision)
        {
            GameObject hitObject = collision.gameObject;
            
            bool isGroundDirect = hitObject.CompareTag("Ground");
            bool isGroundRoot = collision.transform.root.CompareTag("Ground");
            bool isGroundLayer = hitObject.layer == LayerMask.NameToLayer("Ground");
            
            return isGroundDirect || isGroundRoot || isGroundLayer;
        }

        private void ApplyPenalty()
        {
            bool penaltySuccessful = false;

            if (MoneySystem.Instance != null)
            {
                try
                {
                    MoneySystem.Instance.SpendMoney(dropMoneyPenalty);
                    penaltySuccessful = true;
                }
                catch (System.Exception e)
                {
                }
            }

            if (PrestigeManager.Instance != null)
            {
                try
                {
                    PrestigeManager.Instance.ModifyPrestige(-dropPrestigePenalty);
                }
                catch (System.Exception e)
                {
                }
            }

            if (penaltySuccessful)
            {
                penaltyApplied = true;
                OnPenaltyApplied();
            }
        }

        private void OnPenaltyApplied()
        {
            // Override this method or add events here for visual/audio feedback
            // Example: Play sound effect, show particle effect, etc.
            // AudioSource.PlayClipAtPoint(penaltySFX, transform.position);
        }

        public void ResetPenalty()
        {
            hasBeenThrown = false;
            penaltyApplied = false;
        }

        private void ValidateComponents()
        {
            // Component validation without logging
        }

        public void TestPenalty()
        {
            hasBeenThrown = true;
            penaltyApplied = false;
            ApplyPenalty();
        }

        public string GetState()
        {
            return $"HasBeenThrown: {hasBeenThrown}, PenaltyApplied: {penaltyApplied}, Velocity: {(rb != null ? rb.linearVelocity.magnitude.ToString("F2") : "No RB")}";
        }

        private void OnDrawGizmosSelected()
        {
            if (hasBeenThrown && !penaltyApplied)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
            else if (penaltyApplied)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
    }
}