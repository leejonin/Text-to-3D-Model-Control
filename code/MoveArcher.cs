using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI;

// Represents a single joint with its current rotation values and rotation limits.
// Stores X/Y/Z angles and their min/max clamp ranges per axis.
public class Joint
{
    private float joint_t;   // Transition time (animation speed factor)
    private float joint_x;   // Current rotation X
    private float joint_y;   // Current rotation Y
    private float joint_z;   // Current rotation Z

    private float rot_min_x; // Minimum allowed rotation on X axis
    private float rot_min_y;
    private float rot_min_z;

    private float rot_max_x; // Maximum allowed rotation on X axis
    private float rot_max_y;
    private float rot_max_z;

    // Initializes all joint values: current rotation, and min/max rotation limits per axis.
    public void Set(float t, float jx, float jy, float jz,
        float minrx, float minry, float minrz,
        float maxrx, float maxry, float maxrz)
    {
        joint_t = t;
        joint_x = jx;
        joint_y = jy;
        joint_z = jz;

        rot_min_x = minrx;
        rot_min_y = minry;
        rot_min_z = minrz;

        rot_max_x = maxrx;
        rot_max_y = maxry;
        rot_max_z = maxrz;
    }

    // Returns the transition time value for this joint.
    public float GetJointT()
    {
        return joint_t;
    }

    // Returns a rotation Vector3 clamped within the joint's min/max limits.
    // Used to prevent joints from exceeding anatomically valid angles.
    public Vector3 GetClampedRotation(float X, float Y, float Z)
    {
        float cx = Mathf.Clamp(X, rot_min_x, rot_max_x);
        float cy = Mathf.Clamp(Y, rot_min_y, rot_max_y);
        float cz = Mathf.Clamp(Z, rot_min_z, rot_max_z);
        return new Vector3(cx, cy, cz);
    }

    // Sets only the transition time without touching rotation values.
    public void SetJointT(float value)
    {
        joint_t = value;
    }

    // Sets joint rotation, clamping each axis independently to its allowed range.
    public void SetJoint(float X, float Y, float Z)
    {
        if (X < rot_min_x)
        {
            joint_x = rot_min_x;
        }
        else if (X > rot_max_x)
        {
            joint_x = rot_max_x;
        }
        else { joint_x = X; }

        if (Y < rot_min_y)
        {
            joint_y = rot_min_y;
        }
        else if (Y > rot_max_y)
        {
            joint_y = rot_max_y;
        }
        else { joint_y = Y; }

        if (Z < rot_min_z)
        {
            joint_z = rot_min_z;
        }
        else if (Z > rot_max_z)
        {
            joint_z = rot_max_z;
        }
        else { joint_z = Z; }
    }
}

// Tracks the animation state of a single joint during smooth interpolation.
// Used to store start/target rotations and elapsed time for Slerp transitions.
public class JointAnimationState
{
    public bool isMoving = false;        // Whether this joint is currently animating
    public float currentTime = 0f;      // Time elapsed since animation started
    public float targetTime = 0f;       // Total duration of the animation
    public Vector3 startRotation;       // Rotation at animation start
    public Vector3 targetRotation;      // Rotation to interpolate toward
}

// MoveArcher controls all bone rotations for the archer character.
// It manages the full skeleton joint list, maps them to Unity GameObjects,
// and provides both instant (DirectRotate) and smooth (SmoothRotate) rotation methods.
public class MoveArcher : MonoBehaviour
{
    private StateAi stateAi;   // Reference to the AI state controller
    private BodyAI ba;         // Reference to the BodyAI logic controller

    // Torso joints
    private Joint Spine, Chest, UpperChest, Neck, Head;

    // Left arm joints
    private Joint L_UpperArm, L_LowerArm, L_Hand;
    private Joint L_Index1, L_Index2, L_Index3;
    private Joint L_Middle1, L_Middle2, L_Middle3;
    private Joint L_Ring1, L_Ring2, L_Ring3;
    private Joint L_Little1, L_Little2, L_Little3;
    private Joint L_Thumb1, L_Thumb2, L_Thumb3;

    // Right arm joints
    private Joint R_UpperArm, R_LowerArm, R_Hand;
    private Joint R_Index1, R_Index2, R_Index3;
    private Joint R_Middle1, R_Middle2, R_Middle3;
    private Joint R_Ring1, R_Ring2, R_Ring3;
    private Joint R_Little1, R_Little2, R_Little3;
    private Joint R_Thumb1, R_Thumb2, R_Thumb3;

    // Left leg joints
    private Joint L_UpperLeg, L_LowerLeg, L_Foot, L_ToeBase;

    // Right leg joints
    private Joint R_UpperLeg, R_LowerLeg, R_Foot, R_ToeBase;

    // Bone name list used to find GameObjects in the scene via GameObject.Find("J_Bip_" + name).
    // Order must match joint_list order exactly, as they are accessed by index.
    private string[] bone_names = new string[]
    {
        "C_Spine", "C_Chest", "C_UpperChest", "C_Neck", "C_Head",
        "L_UpperArm", "L_LowerArm", "L_Hand",
        "L_Index1", "L_Index2", "L_Index3",
        "L_Middle1", "L_Middle2", "L_Middle3",
        "L_Ring1", "L_Ring2", "L_Ring3",
        "L_Little1", "L_Little2", "L_Little3",
        "L_Thumb1", "L_Thumb2", "L_Thumb3",
        "R_UpperArm", "R_LowerArm", "R_Hand",
        "R_Index1", "R_Index2", "R_Index3",
        "R_Middle1", "R_Middle2", "R_Middle3",
        "R_Ring1", "R_Ring2", "R_Ring3",
        "R_Little1", "R_Little2", "R_Little3",
        "R_Thumb1", "R_Thumb2", "R_Thumb3",
        "L_UpperLeg", "L_LowerLeg", "L_Foot", "L_ToeBase",
        "R_UpperLeg", "R_LowerLeg", "R_Foot", "R_ToeBase"
    };

    private List<GameObject> bone_list = new List<GameObject>();                         // Scene GameObjects for each bone, found by name
    public List<Joint> joint_list = new List<Joint>();                                   // Joint data objects, indexed in the same order as bone_list
    private List<JointAnimationState> jointAnimStates = new List<JointAnimationState>(); // Per-joint animation state tracking
    private Queue<int> movingJointQueue = new Queue<int>();                              // Queue of joint indices pending animation (reserved for future use)

    // Searches the scene for each bone by name and populates bone_list.
    // Logs an error if any bone is missing from the scene hierarchy.
    private void SetBoneList()
    {
        try
        {
            for (int i = 0; i < bone_names.Length; i++)
            {
                // Bones are named with "J_Bip_" prefix in the scene (e.g. "J_Bip_C_Spine")
                GameObject obj1 = GameObject.Find("J_Bip_" + bone_names[i]);
                if (obj1 != null)
                {
                    bone_list.Add(obj1);
                }
                else
                {
                    Debug.LogError("[Body Rotation] Bone not found: " + bone_names[i]);
                }
            }
            if (bone_list.Count == bone_names.Length)
            {
                Debug.Log("[Body Rotation] All bones successfully added to the list.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Body Rotation] Error while setting bone list: " + e.Message);
        }
    }

    // Creates all Joint instances and sets their initial rotation and rotation limit values.
    // Parameters order: Set(transitionTime, initX, initY, initZ, minX, minY, minZ, maxX, maxY, maxZ)
    private void SetUpJoints()
    {
        // Torso
        Spine = new Joint();
        Chest = new Joint();
        UpperChest = new Joint();
        Neck = new Joint();
        Head = new Joint();

        // Left arm
        L_UpperArm = new Joint();
        L_LowerArm = new Joint();
        L_Hand = new Joint();
        L_Index1 = new Joint();
        L_Index2 = new Joint();
        L_Index3 = new Joint();
        L_Middle1 = new Joint();
        L_Middle2 = new Joint();
        L_Middle3 = new Joint();
        L_Ring1 = new Joint();
        L_Ring2 = new Joint();
        L_Ring3 = new Joint();
        L_Little1 = new Joint();
        L_Little2 = new Joint();
        L_Little3 = new Joint();
        L_Thumb1 = new Joint();
        L_Thumb2 = new Joint();
        L_Thumb3 = new Joint();

        // Right arm
        R_UpperArm = new Joint();
        R_LowerArm = new Joint();
        R_Hand = new Joint();
        R_Index1 = new Joint();
        R_Index2 = new Joint();
        R_Index3 = new Joint();
        R_Middle1 = new Joint();
        R_Middle2 = new Joint();
        R_Middle3 = new Joint();
        R_Ring1 = new Joint();
        R_Ring2 = new Joint();
        R_Ring3 = new Joint();
        R_Little1 = new Joint();
        R_Little2 = new Joint();
        R_Little3 = new Joint();
        R_Thumb1 = new Joint();
        R_Thumb2 = new Joint();
        R_Thumb3 = new Joint();

        // Left leg
        L_UpperLeg = new Joint();
        L_LowerLeg = new Joint();
        L_Foot = new Joint();
        L_ToeBase = new Joint();

        // Right leg
        R_UpperLeg = new Joint();
        R_LowerLeg = new Joint();
        R_Foot = new Joint();
        R_ToeBase = new Joint();

        // --- Torso rotation limits ---
        Spine.Set(0f, 1.466f, 0f, 0f,
            -30f, -50f, -30f,
            90f, 50f, 30f);
        Chest.Set(0f, -15.658f, 0f, 0f,
            -30f, -10f, -30f,
            30f, 10f, 30f);
        UpperChest.Set(0f, -12.397f, 0f, 0f,
           -12f, -10f, -10f,
           12f, 10f, 10f);
        Neck.Set(0f, 26.226f, 0f, 0f,
            -50f, -30f, -10f,
            50f, 30f, 10f);
        Head.Set(0f, -6.229f, 0f, 0f,
            -70f, -50f, -30f,
            30f, 50f, 30f);

        // --- Left arm rotation limits ---
        // L_UpperArm (upper arm): wide range for shoulder movement
        L_UpperArm.Set(0f, 0f, 0f, 0f,
            -90f, -45f, -80f,
            90f, 45f, 80f);

        // L_LowerArm (forearm): limited to realistic elbow bend
        L_LowerArm.Set(0f, 0f, 0f, 0f,
            0f, 0f, -90f,
            145f, 90f, 90f);

        // L_Hand (wrist): limited wrist rotation range
        L_Hand.Set(0f, 0f, 0f, 0f,
            -70f, -20f, -30f,
            70f, 20f, 30f);

        // Left finger joints (Index, Middle, Ring, Little, Thumb)
        // Each finger has 3 segments with progressive bend limits
        L_Index1.Set(0f, 0f, 0f, 0f,
            -5f, 0f, -20f,
            90f, 0f, 20f);
        L_Index2.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            100f, 0f, 0f);
        L_Index3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            90f, 0f, 0f);

        L_Middle1.Set(0f, 0f, 0f, 0f,
            -5f, 0f, -10f,
            90f, 0f, 10f);
        L_Middle2.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            100f, 0f, 0f);
        L_Middle3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            90f, 0f, 0f);

        L_Ring1.Set(0f, 0f, 0f, 0f,
            -5f, 0f, -10f,
            90f, 0f, 10f);
        L_Ring2.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            100f, 0f, 0f);
        L_Ring3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            90f, 0f, 0f);

        L_Little1.Set(0f, 0f, 0f, 0f,
            -5f, 0f, -20f,
            90f, 0f, 20f);
        L_Little2.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            100f, 0f, 0f);
        L_Little3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            90f, 0f, 0f);

        // Thumb has wider lateral range (Z axis) for opposition movement
        L_Thumb1.Set(0f, 0f, 0f, 0f,
            -20f, -30f, -60f,
            20f, 30f, 60f);
        L_Thumb2.Set(0f, 0f, 0f, 0f,
            -10f, 0f, 0f,
            60f, 0f, 0f);
        L_Thumb3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            80f, 0f, 0f);

        // --- Right arm rotation limits ---
        // R_UpperArm (upper arm)
        R_UpperArm.Set(0f, 0f, 0f, 0f,
            90f, -45f, -80f,
            -90f, 45f, 80f);

        // R_LowerArm (forearm)
        R_LowerArm.Set(0f, 0f, 0f, 0f,
            0f, 0f, -90f,
            145f, 90f, 90f);

        // R_Hand (wrist)
        R_Hand.Set(0f, 0f, 0f, 0f,
            -70f, -20f, -30f,
            70f, 20f, 30f);

        // Right index finger
        R_Index1.Set(0f, 0f, 0f, 0f,
            -5f, 0f, -20f,
            90f, 0f, 20f);
        R_Index2.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            100f, 0f, 0f);
        R_Index3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            90f, 0f, 0f);

        // Right middle finger
        R_Middle1.Set(0f, 0f, 0f, 0f,
            -5f, 0f, -10f,
            90f, 0f, 10f);
        R_Middle2.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            100f, 0f, 0f);
        R_Middle3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            90f, 0f, 0f);

        // Right ring finger
        R_Ring1.Set(0f, 0f, 0f, 0f,
            -5f, 0f, -10f,
            90f, 0f, 10f);
        R_Ring2.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            100f, 0f, 0f);
        R_Ring3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            90f, 0f, 0f);

        // Right little finger
        R_Little1.Set(0f, 0f, 0f, 0f,
            -5f, 0f, -20f,
            90f, 0f, 20f);
        R_Little2.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            100f, 0f, 0f);
        R_Little3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            90f, 0f, 0f);

        // Right thumb
        R_Thumb1.Set(0f, 0f, 0f, 0f,
            -20f, -30f, -60f,
            20f, 30f, 30f);
        R_Thumb2.Set(0f, 0f, 0f, 0f,
            -10f, 0f, 0f,
            60f, 0f, 0f);
        R_Thumb3.Set(0f, 0f, 0f, 0f,
            0f, 0f, 0f,
            80f, 0f, 0f);

        // --- Left leg rotation limits ---
        // L_UpperLeg (thigh): wide range for hip flexion/extension/abduction
        L_UpperLeg.Set(0f, 0f, 0f, 0f,
            -120f, -30f, -45f,
            60f, 30f, 45f);

        // L_LowerLeg (shin): knee bends only forward (0 to 145 degrees)
        L_LowerLeg.Set(0f, 0f, 0f, 0f,
            0f, -20f, -20f,
            145f, 20f, 20f);

        // L_Foot (ankle)
        L_Foot.Set(0f, 0f, 0f, 0f,
            -45f, -30f, -30f,
            45f, 30f, 30f);

        // L_ToeBase (toe root)
        L_ToeBase.Set(0f, 0f, 0f, 0f,
            -45f, -10f, -10f,
            45f, 10f, 10f);

        // --- Right leg rotation limits ---
        // R_UpperLeg (thigh)
        R_UpperLeg.Set(0f, 0f, 0f, 0f,
            -120f, -30f, -45f,
            60f, 30f, 45f);

        // R_LowerLeg (shin)
        R_LowerLeg.Set(0f, 0f, 0f, 0f,
            0f, -20f, -20f,
            145f, 20f, 20f);

        // R_Foot (ankle)
        R_Foot.Set(0f, 0f, 0f, 0f,
            -45f, -30f, -30f,
            45f, 30f, 30f);

        // R_ToeBase (toe root)
        R_ToeBase.Set(0f, 0f, 0f, 0f,
            -45f, -10f, -10f,
            45f, 10f, 10f);
    }

    // Adds all Joint objects to joint_list in the same order as bone_names / bone_list.
    // Index alignment is critical: joint_list[i] must correspond to bone_list[i].
    private void SetJointsList()
    {
        joint_list.Add(Spine);
        joint_list.Add(Chest);
        joint_list.Add(UpperChest);
        joint_list.Add(Neck);
        joint_list.Add(Head);
        joint_list.Add(L_UpperArm);
        joint_list.Add(L_LowerArm);
        joint_list.Add(L_Hand);
        joint_list.Add(L_Index1);
        joint_list.Add(L_Index2);
        joint_list.Add(L_Index3);
        joint_list.Add(L_Middle1);
        joint_list.Add(L_Middle2);
        joint_list.Add(L_Middle3);
        joint_list.Add(L_Ring1);
        joint_list.Add(L_Ring2);
        joint_list.Add(L_Ring3);
        joint_list.Add(L_Little1);
        joint_list.Add(L_Little2);
        joint_list.Add(L_Little3);
        joint_list.Add(L_Thumb1);
        joint_list.Add(L_Thumb2);
        joint_list.Add(L_Thumb3);
        joint_list.Add(R_UpperArm);
        joint_list.Add(R_LowerArm);
        joint_list.Add(R_Hand);
        joint_list.Add(R_Index1);
        joint_list.Add(R_Index2);
        joint_list.Add(R_Index3);
        joint_list.Add(R_Middle1);
        joint_list.Add(R_Middle2);
        joint_list.Add(R_Middle3);
        joint_list.Add(R_Ring1);
        joint_list.Add(R_Ring2);
        joint_list.Add(R_Ring3);
        joint_list.Add(R_Little1);
        joint_list.Add(R_Little2);
        joint_list.Add(R_Little3);
        joint_list.Add(R_Thumb1);
        joint_list.Add(R_Thumb2);
        joint_list.Add(R_Thumb3);
        joint_list.Add(L_UpperLeg);
        joint_list.Add(L_LowerLeg);
        joint_list.Add(L_Foot);
        joint_list.Add(L_ToeBase);
        joint_list.Add(R_UpperLeg);
        joint_list.Add(R_LowerLeg);
        joint_list.Add(R_Foot);
        joint_list.Add(R_ToeBase);
    }

    // Creates a JointAnimationState for each joint and records the bone's initial rotation
    // as the starting point for future smooth transitions.
    private void InitAnimationStates()
    {
        for (int i = 0; i < joint_list.Count; i++)
        {
            JointAnimationState state = new JointAnimationState();
            if (i < bone_list.Count && bone_list[i] != null)
            {
                // Capture the bone's current local rotation as the animation baseline
                state.startRotation = bone_list[i].transform.localEulerAngles;
            }
            jointAnimStates.Add(state);
        }
    }

    // Instantly rotates a joint to the given X/Y/Z angles.
    // Clamps the values to the joint's allowed rotation range before applying.
    public void DirectRotate(int jointIndex, float x, float y, float z)
    {
        if (jointIndex >= 0 && jointIndex < bone_list.Count && bone_list[jointIndex] != null)
        {
            // Clamp to joint limits, then apply directly to transform
            Vector3 clamped = joint_list[jointIndex].GetClampedRotation(x, y, z);
            joint_list[jointIndex].SetJoint(clamped.x, clamped.y, clamped.z);
            bone_list[jointIndex].transform.localRotation = Quaternion.Euler(clamped.x, clamped.y, clamped.z);
            Debug.Log($"[MoveArcher] Rotated joint {jointIndex} to ({clamped.x}, {clamped.y}, {clamped.z})");
        }
        else
        {
            Debug.LogWarning($"[MoveArcher] Invalid joint index: {jointIndex} (bone_list count: {bone_list.Count})");
        }
    }

    // Starts a smooth rotation coroutine for the given joint index over the specified duration.
    // Validates the index before launching the coroutine.
    public void SmoothRotate(int jointIndex, float x, float y, float z, float duration = 0.5f)
    {
        if (jointIndex >= 0 && jointIndex < bone_list.Count && bone_list[jointIndex] != null)
        {
            StartCoroutine(SmoothRotateCoroutine(jointIndex, x, y, z, duration));
        }
        else
        {
            Debug.LogWarning($"[MoveArcher] Invalid joint index: {jointIndex}");
        }
    }

    // Coroutine that smoothly interpolates a bone's rotation from its current rotation
    // to the target rotation over 'duration' seconds using smoothstep easing.
    private IEnumerator SmoothRotateCoroutine(int jointIndex, float targetX, float targetY, float targetZ, float duration)
    {
        Transform targetTransform = bone_list[jointIndex].transform;
        Quaternion startRotation = targetTransform.localRotation; // Record current rotation as start

        // Clamp target to joint limits and store in joint data
        Vector3 clamped = joint_list[jointIndex].GetClampedRotation(targetX, targetY, targetZ);
        joint_list[jointIndex].SetJoint(clamped.x, clamped.y, clamped.z);
        Quaternion targetRotation = Quaternion.Euler(clamped.x, clamped.y, clamped.z);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration); // Normalize elapsed time to 0..1

            // Smoothstep easing (S-curve): ease-in-out for natural motion feel
            t = t * t * (3f - 2f * t);

            // Spherical interpolation between start and target quaternions
            targetTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null; // Wait one frame per step
        }

        // Snap to exact target at the end to eliminate floating-point drift
        targetTransform.localRotation = targetRotation;
    }

    // Unity Start: initializes all joints, finds scene bones, builds lists,
    // then notifies BodyAI that MoveArcher is ready.
    public void Start()
    {
        SetUpJoints();         // Create Joint objects with rotation limits
        SetBoneList();         // Find bone GameObjects in the scene by name
        SetJointsList();       // Register joints in index-aligned list
        InitAnimationStates(); // Record initial bone rotations for animation baseline

        stateAi = GetComponent<StateAi>(); // Get StateAi component on the same GameObject
        ba.StartAfterMoveArcher();         // Signal BodyAI that initialization is complete
    }
}
