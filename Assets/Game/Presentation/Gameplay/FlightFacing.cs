using UnityEngine;

namespace TypingDefense.Presentation.Gameplay
{
    /// <summary>
    /// Builds a stable world rotation so a projectile nose follows its flight direction.
    /// Unity LookRotation assumes local +Z is forward; meshForwardEulerOffset corrects art axes.
    /// </summary>
    public static class FlightFacing
    {
        public static Quaternion FromVelocity(
            Vector3 velocity,
            Vector3 meshForwardEulerOffset,
            Vector3 fallbackForward)
        {
            Vector3 forward = velocity.sqrMagnitude > 0.0001f
                ? velocity.normalized
                : fallbackForward;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            {
                up = Vector3.Cross(forward, Vector3.right);
                if (up.sqrMagnitude < 0.0001f)
                    up = Vector3.Cross(forward, Vector3.forward);
                up.Normalize();
            }

            return Quaternion.LookRotation(forward, up) * Quaternion.Euler(meshForwardEulerOffset);
        }
    }
}
