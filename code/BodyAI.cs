using UnityEngine;
//using RLMatrix.Agents.Common;  // Reserved for future RL agent integration
//using RLMatrix;                // Reserved for future RL agent integration

// BodyAI acts as the decision layer that sits above MoveArcher.
// It is responsible for higher-level body animation logic and,
// in future iterations, will drive behavior via a reinforcement learning agent.
public class BodyAI : MonoBehaviour
{
    MoveArcher ma; // Reference to MoveArcher, used to verify initialization state

    // Called by MoveArcher.Start() after it has finished building its joint and bone lists.
    // Ensures BodyAI only runs logic after the skeleton is fully initialized.
    public void StartAfterMoveArcher()
    {
        // Guard: abort if MoveArcher has no joints registered.
        // This would indicate MoveArcher did not initialize correctly.
        if (ma.joint_list.Count == 0)
        {
            Debug.LogError("[BodyAI] MoveArcher's joint list is empty. Ensure MoveArcher is properly initialized.");
            return;
        }

        // Initialization logic for BodyAI goes here.
        // e.g. set up RL agent, register state observers, trigger initial pose, etc.
    }
}
