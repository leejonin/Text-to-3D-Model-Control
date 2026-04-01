using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Windows;
using static StateAi;

// StateAi manages AI-driven body animation for the archer character.
// It sends joint rotation requests to the OpenAI API every 5 seconds,
// parses the JSON response, and applies the result to MoveArcher.
public class StateAi : MonoBehaviour
{
    public MoveArcher moveArcher; // Reference to MoveArcher, assigned in Inspector

    private List<JObject> messageHistory = new List<JObject>(); // Stores conversation context sent to the AI (role + content pairs)
    private List<AnimationKey> animationList = new List<AnimationKey>(); // Reserved for keyframe-based animation playback

    // Deserialization target for the AI's JSON response.
    // Each joint maps to a float array: [Frame, X, Y, Z].
    // Frame = movement duration; X/Y/Z = rotation angles.
    [Serializable]
    public class BodyData
    {
        public float[] C_Spine { get; set; }
        public float[] C_Chest { get; set; }
        public float[] C_UpperChest { get; set; }
        public float[] C_Neck { get; set; }
        public float[] C_Head { get; set; }

        public float[] L_UpperArm { get; set; }
        public float[] L_LowerArm { get; set; }
        public float[] L_Hand { get; set; }

        public float[] L_Index1 { get; set; }
        public float[] L_Index2 { get; set; }
        public float[] L_Index3 { get; set; }

        public float[] L_Middle1 { get; set; }
        public float[] L_Middle2 { get; set; }
        public float[] L_Middle3 { get; set; }

        public float[] L_Ring1 { get; set; }
        public float[] L_Ring2 { get; set; }
        public float[] L_Ring3 { get; set; }

        public float[] L_Little1 { get; set; }
        public float[] L_Little2 { get; set; }
        public float[] L_Little3 { get; set; }

        public float[] L_Thumb1 { get; set; }
        public float[] L_Thumb2 { get; set; }
        public float[] L_Thumb3 { get; set; }

        public float[] R_UpperArm { get; set; }
        public float[] R_LowerArm { get; set; }
        public float[] R_Hand { get; set; }

        public float[] R_Index1 { get; set; }
        public float[] R_Index2 { get; set; }
        public float[] R_Index3 { get; set; }

        public float[] R_Middle1 { get; set; }
        public float[] R_Middle2 { get; set; }
        public float[] R_Middle3 { get; set; }

        public float[] R_Ring1 { get; set; }
        public float[] R_Ring2 { get; set; }
        public float[] R_Ring3 { get; set; }

        public float[] R_Little1 { get; set; }
        public float[] R_Little2 { get; set; }
        public float[] R_Little3 { get; set; }

        public float[] R_Thumb1 { get; set; }
        public float[] R_Thumb2 { get; set; }
        public float[] R_Thumb3 { get; set; }

        public float[] L_UpperLeg { get; set; }
        public float[] L_LowerLeg { get; set; }
        public float[] L_Foot { get; set; }
        public float[] L_ToeBase { get; set; }

        public float[] R_UpperLeg { get; set; }
        public float[] R_LowerLeg { get; set; }
        public float[] R_Foot { get; set; }
        public float[] R_ToeBase { get; set; }
    }

    private string apiKey = "putyourkey";  // OpenAI API key, overwritten by KeyLogic() at runtime
    public string currentState;            // Current named state of the character (e.g. "idle", "attack")
    private string aiResponse = "";        // Raw AI response text (stored for debugging)
    private string bodyjsondate = "", BodyRotation_3 = "", BodyRotation_4 = "", BodyRotation_5 = ""; // Loaded pose reference files
    private string sytemPrompt = "";       // System prompt text loaded from file
    private float timer = 0f;             // Tracks elapsed time since last API request
    private bool requestSent = false;     // Prevents duplicate requests while one is in flight

    public BodyData data; // Currently active body pose data (updated after each AI response)

    // Struct representing a single animation keyframe for a joint.
    // Used when building discrete frame-based animation sequences.
    public struct AnimationKey
    {
        public int Frame;       // Frame index or duration
        public float RotX;
        public float RotY;
        public float RotZ;
        public int JointIndex;  // Index into MoveArcher's joint_list

        public AnimationKey(int frame, float x, float y, float z, int jointIndex)
        {
            Frame = frame;
            RotX = x;
            RotY = y;
            RotZ = z;
            JointIndex = jointIndex;
        }
    }

    // Unity Start: loads the API key, reference data files, and the initial body pose.
    public async void Start()
    {
        KeyLogic();                                                          // Read API key from file
        bodyjsondate = LoadFile(@"code\Date\body_json_date.Json");           // Load default joint angle reference JSON
        BodyRotation_3 = LoadFile(@"code\Date\BodyRotation_3.txt");          // Load T-pose (arms spread left/right) sample
        BodyRotation_4 = LoadFile(@"code\Date\BodyRotation_4.txt");          // Load arms-forward sample
        BodyRotation_5 = LoadFile(@"code\Date\BodyRotation_5.txt");          // Load fully stretched pose sample
        sytemPrompt = LoadFile(@"code\Date\BodyPrompt.txt");                 // Load AI system prompt

        Setbody(); // Deserialize the default JSON into 'data' for the first AI request
    }

    // Unity Update: accumulates time and triggers a new AI request every 5 seconds.
    // requestSent prevents overlapping coroutines.
    private void Update()
    {
        timer += Time.deltaTime;
        Debug.Log("Timer: " + timer + " bool :" + requestSent + " if :" + (timer == 5f && requestSent == false));
        if (timer >= 5f && requestSent == false)
        {
            StartCoroutine(SendRequest(data)); // Send current body data to AI and request next pose
        }
    }

    // Reads the API key from a local text file line by line.
    // The last non-empty line in the file becomes the active key.
    private void KeyLogic()
    {
        string[] lines = System.IO.File.ReadAllLines(@"code\Date\bodykey.txt");
        foreach (string show in lines)
            apiKey = show;
    }

    // Loads a text file from the given path.
    // Returns empty string and logs an error if the file does not exist.
    private string LoadFile(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogError("[Body Rotation] File not found: " + path);
            return "";
        }
        return System.IO.File.ReadAllText(path);
    }

    // Reads the default body JSON file and deserializes it into 'data'.
    // This provides the AI with the current joint angles as context for the next pose.
    private void Setbody()
    {
        data = JsonConvert.DeserializeObject<BodyData>(System.IO.File.ReadAllText(@"code\Date\body_json_date.Json"));
    }

    // Sets a new named state and logs it for debugging.
    public void SetState(string newState)
    {
        currentState = newState;
        Debug.Log("State changed to: " + currentState);
    }

    // Returns the current state name.
    public string GetState()
    {
        return currentState;
    }

    // Clears the current state back to empty.
    public void ClearState()
    {
        currentState = string.Empty;
        Debug.Log("State cleared.");
    }

    // Coroutine: builds the AI prompt with current joint data and pose samples,
    // sends a POST request to the OpenAI Chat Completions API,
    // then parses and applies the returned joint rotation JSON.
    private IEnumerator SendRequest(BodyData body_date)
    {
        Debug.Log("[Body Rotation] Timer reached 5 seconds, sending request to GPT.");
        requestSent = true;

        // Keep message history at most 6 entries to avoid token overflow.
        // Remove oldest messages (starting from index 1) when over the limit.
        if (messageHistory.Count > 6)
        {
            int removeCount = messageHistory.Count - 6;
            messageHistory.RemoveRange(1, removeCount);
            Debug.Log($"[Body Rotation] Optimized message history, removed {removeCount} old messages");
        }

        // Build the system message: includes the prompt, reference joint data, pose samples,
        // current joint angles, and strict output format instructions.
        messageHistory.Add(new JObject
    {
        { "role", "system" },
        { "content", $"{sytemPrompt} + {bodyjsondate} + value [Frame,X,Y,Z]. Frame is the speed of joint movement. " +
        $"The sample only shows joint x y z values and tells you the pose name at the end. The joint order is the same as the existing {bodyjsondate} order." +
        $"{BodyRotation_3} This is a sample pose facing forward with both arms spread left and right, palms facing down." +
        $"{BodyRotation_4} This is a sample pose facing forward with both arms spread forward, palms facing down." +
        $"{BodyRotation_5} This is a fully stretched pose facing forward." +
        $"The current angle data for each joint is {body_date}. The minimum Frame must be at least 1 second, and do not replicate the sample exactly." +
        $"You must act according to {sytemPrompt}." +
        $"{{ 'joint': [Frame,X,Y,Z],'joint': [Frame,X,Y,Z]}} No additional explanation outside this format is allowed. Move as much as possible at once. Do not omit any joints." }
    });

        // Add the user message that describes the desired pose.
        messageHistory.Add(new JObject
    {
        { "role", "user" },
        { "content", "At-ease stance, both arms down, back straight." }
    });

        // Build the full request payload for the OpenAI API.
        // Streaming is enabled (stream: true) so the response arrives as SSE chunks.
        JObject payload = new JObject
    {
        { "model", "gpt-4.1-nano" },
        { "messages", new JArray(messageHistory) },
        { "max_tokens", 4096 },
        { "temperature", 0.7 },
        { "stream", true }
    };

        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload.ToString());

        using (UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey); // Attach API key as Bearer token

            var asyncOperation = request.SendWebRequest();
            float timeoutTimer = 0f;
            float timeout = 30f; // Abort if the server does not respond within 30 seconds

            // Wait for the request to complete, checking for timeout each frame.
            while (!asyncOperation.isDone)
            {
                timeoutTimer += Time.deltaTime;
                if (timeoutTimer > timeout)
                {
                    Debug.LogError("[Body Rotation] Request timeout after 30 seconds");
                    request.Abort();
                    requestSent = false;
                    yield break;
                }
                yield return null;
            }

            // If the HTTP request itself failed, log and abort.
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[Body Rotation] " + request.error);
                Debug.LogError("[Body Rotation] " + request.downloadHandler.text);
                requestSent = false;
                yield break;
            }

            try
            {
                string responseText = request.downloadHandler.text;
                string aiResponseContent = "";

                // Handle Server-Sent Events (SSE) streaming response:
                // The body contains multiple "data: {...}" lines; extract and concatenate the delta content.
                if (responseText.Contains("data: "))
                {
                    string[] lines = responseText.Split('\n');
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("data: ") && !line.Contains("[DONE]"))
                        {
                            string jsonData = line.Substring(6).Trim(); // Strip the "data: " prefix
                            if (!string.IsNullOrEmpty(jsonData))
                            {
                                try
                                {
                                    JObject streamChunk = JObject.Parse(jsonData);
                                    // Extract the text delta from the streaming chunk
                                    string delta = streamChunk["choices"]?[0]?["delta"]?["content"]?.ToString();
                                    if (!string.IsNullOrEmpty(delta))
                                    {
                                        aiResponseContent += delta; // Append each chunk to build the full response
                                    }
                                }
                                catch { } // Skip malformed chunks silently
                            }
                        }
                    }
                }
                else
                {
                    // Non-streaming response: parse the full JSON directly
                    JObject responseJson = JObject.Parse(responseText);
                    aiResponseContent = responseJson["choices"][0]["message"]["content"].ToString();
                }

                Debug.Log($"[Body Rotation] Received response length: {aiResponseContent.Length}");

                // Strip potential markdown code fences the AI may wrap around its JSON output
                aiResponseContent = aiResponseContent.Trim();
                if (aiResponseContent.StartsWith("```json"))
                {
                    aiResponseContent = aiResponseContent.Substring(7);
                }
                else if (aiResponseContent.StartsWith("```"))
                {
                    aiResponseContent = aiResponseContent.Substring(3);
                }
                if (aiResponseContent.EndsWith("```"))
                {
                    aiResponseContent = aiResponseContent.Substring(0, aiResponseContent.Length - 3);
                }
                aiResponseContent = aiResponseContent.Trim();

                // Append the AI's reply to history so future requests include it as context
                messageHistory.Add(new JObject
            {
                { "role", "assistant" },
                { "content", aiResponseContent }
            });

                // Deserialize the cleaned JSON string into a BodyData object
                data = JsonConvert.DeserializeObject<BodyData>(aiResponseContent);

                if (data != null)
                {
                    StartCoroutine(ApplyBodyMovement(data)); // Apply the new joint rotations to the character
                    Debug.Log("[Body Rotation] Successfully applied body movement");
                }
                else
                {
                    Debug.LogError("[Body Rotation] Failed to deserialize body data - data is null");
                }

                // Reset timer and flag so the next request can fire after another 5 seconds
                timer = 0f;
                requestSent = false;
            }
            catch (JsonReaderException jsonEx)
            {
                Debug.LogError("[Body Rotation] JSON parsing error: " + jsonEx.Message);
                Debug.LogError("[Body Rotation] Response text: " + request.downloadHandler.text);
                requestSent = false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Body Rotation] Error parsing response: " + ex.Message);
                Debug.LogError("[Body Rotation] Response text: " + request.downloadHandler.text);
                requestSent = false;
            }
        }
    }

    // Coroutine: iterates over all joints in BodyData and calls SmoothRotate on each.
    // jointIndex increments sequentially to match MoveArcher's joint_list order.
    // A joint is skipped if its array is null or has fewer than 4 elements (needs [Frame, X, Y, Z]).
    private IEnumerator ApplyBodyMovement(BodyData bodyData)
    {
        Debug.Log("[Body Rotation] Starting ApplyBodyMovement");

        int jointIndex = 0;   // Tracks the current position in MoveArcher's joint_list
        int f = 3;            // Minimum array length check: index 3 (Z) must exist
        float smoothDuration = 0.8f; // All joints animate over 0.8 seconds

        if (bodyData.C_Spine != null && bodyData.C_Spine.Length > f)
        {
            Debug.Log($"[Body Rotation] Moving C_Spine - Index: {jointIndex}");
            moveArcher.SmoothRotate(jointIndex++,
                bodyData.C_Spine[1],  // X rotation
                bodyData.C_Spine[2],  // Y rotation
                bodyData.C_Spine[3],  // Z rotation
                smoothDuration);
        }

        if (bodyData.C_Chest != null && bodyData.C_Chest.Length > f)
        {
            Debug.Log($"[Body Rotation] Moving C_Chest - Index: {jointIndex}");
            moveArcher.SmoothRotate(jointIndex++,
                bodyData.C_Chest[1],
                bodyData.C_Chest[2],
                bodyData.C_Chest[3],
                smoothDuration);
        }

        if (bodyData.C_UpperChest != null && bodyData.C_UpperChest.Length > f)
        {
            Debug.Log($"[Body Rotation] Moving C_UpperChest - Index: {jointIndex}");
            moveArcher.SmoothRotate(jointIndex++,
                bodyData.C_UpperChest[1],
                bodyData.C_UpperChest[2],
                bodyData.C_UpperChest[3],
                smoothDuration);
        }

        if (bodyData.C_Neck != null && bodyData.C_Neck.Length > f)
        {
            Debug.Log($"[Body Rotation] Moving C_Neck - Index: {jointIndex}");
            moveArcher.SmoothRotate(jointIndex++,
                bodyData.C_Neck[1],
                bodyData.C_Neck[2],
                bodyData.C_Neck[3],
                smoothDuration);
        }

        if (bodyData.C_Head != null && bodyData.C_Head.Length > f)
        {
            Debug.Log($"[Body Rotation] Moving C_Head - Index: {jointIndex}");
            moveArcher.SmoothRotate(jointIndex++,
                bodyData.C_Head[1],
                bodyData.C_Head[2],
                bodyData.C_Head[3],
                smoothDuration);
        }

        if (bodyData.L_UpperArm != null && bodyData.L_UpperArm.Length > f)
        {
            Debug.Log($"[Body Rotation] Moving L_UpperArm - Index: {jointIndex}");
            moveArcher.SmoothRotate(jointIndex++,
                bodyData.L_UpperArm[1],
                bodyData.L_UpperArm[2],
                bodyData.L_UpperArm[3],
                smoothDuration);
        }

        if (bodyData.L_LowerArm != null && bodyData.L_LowerArm.Length > f)
        {
            Debug.Log($"[Body Rotation] Moving L_LowerArm - Index: {jointIndex}");
            moveArcher.SmoothRotate(jointIndex++,
                bodyData.L_LowerArm[1],
                bodyData.L_LowerArm[2],
                bodyData.L_LowerArm[3],
                smoothDuration);
        }

        if (bodyData.L_Hand != null && bodyData.L_Hand.Length > f)
        {
            Debug.Log($"[Body Rotation] Moving L_Hand - Index: {jointIndex}");
            moveArcher.SmoothRotate(jointIndex++,
                bodyData.L_Hand[1],
                bodyData.L_Hand[2],
                bodyData.L_Hand[3],
                smoothDuration);
        }

        // Left fingers: each segment (1-3) processed in order, index auto-incremented
        if (bodyData.L_Index1 != null && bodyData.L_Index1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Index1[1], bodyData.L_Index1[2], bodyData.L_Index1[3], smoothDuration);

        if (bodyData.L_Index2 != null && bodyData.L_Index2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Index2[1], bodyData.L_Index2[2], bodyData.L_Index2[3], smoothDuration);

        if (bodyData.L_Index3 != null && bodyData.L_Index3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Index3[1], bodyData.L_Index3[2], bodyData.L_Index3[3], smoothDuration);

        if (bodyData.L_Middle1 != null && bodyData.L_Middle1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Middle1[1], bodyData.L_Middle1[2], bodyData.L_Middle1[3], smoothDuration);

        if (bodyData.L_Middle2 != null && bodyData.L_Middle2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Middle2[1], bodyData.L_Middle2[2], bodyData.L_Middle2[3], smoothDuration);

        if (bodyData.L_Middle3 != null && bodyData.L_Middle3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Middle3[1], bodyData.L_Middle3[2], bodyData.L_Middle3[3], smoothDuration);

        if (bodyData.L_Ring1 != null && bodyData.L_Ring1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Ring1[1], bodyData.L_Ring1[2], bodyData.L_Ring1[3], smoothDuration);

        if (bodyData.L_Ring2 != null && bodyData.L_Ring2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Ring2[1], bodyData.L_Ring2[2], bodyData.L_Ring2[3], smoothDuration);

        if (bodyData.L_Ring3 != null && bodyData.L_Ring3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Ring3[1], bodyData.L_Ring3[2], bodyData.L_Ring3[3], smoothDuration);

        if (bodyData.L_Little1 != null && bodyData.L_Little1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Little1[1], bodyData.L_Little1[2], bodyData.L_Little1[3], smoothDuration);

        if (bodyData.L_Little2 != null && bodyData.L_Little2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Little2[1], bodyData.L_Little2[2], bodyData.L_Little2[3], smoothDuration);

        if (bodyData.L_Little3 != null && bodyData.L_Little3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Little3[1], bodyData.L_Little3[2], bodyData.L_Little3[3], smoothDuration);

        if (bodyData.L_Thumb1 != null && bodyData.L_Thumb1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Thumb1[1], bodyData.L_Thumb1[2], bodyData.L_Thumb1[3], smoothDuration);

        if (bodyData.L_Thumb2 != null && bodyData.L_Thumb2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Thumb2[1], bodyData.L_Thumb2[2], bodyData.L_Thumb2[3], smoothDuration);

        if (bodyData.L_Thumb3 != null && bodyData.L_Thumb3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Thumb3[1], bodyData.L_Thumb3[2], bodyData.L_Thumb3[3], smoothDuration);

        // Right arm segments
        if (bodyData.R_UpperArm != null && bodyData.R_UpperArm.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_UpperArm[1], bodyData.R_UpperArm[2], bodyData.R_UpperArm[3], smoothDuration);

        if (bodyData.R_LowerArm != null && bodyData.R_LowerArm.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_LowerArm[1], bodyData.R_LowerArm[2], bodyData.R_LowerArm[3], smoothDuration);

        if (bodyData.R_Hand != null && bodyData.R_Hand.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Hand[1], bodyData.R_Hand[2], bodyData.R_Hand[3], smoothDuration);

        // Right fingers
        if (bodyData.R_Index1 != null && bodyData.R_Index1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Index1[1], bodyData.R_Index1[2], bodyData.R_Index1[3], smoothDuration);

        if (bodyData.R_Index2 != null && bodyData.R_Index2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Index2[1], bodyData.R_Index2[2], bodyData.R_Index2[3], smoothDuration);

        if (bodyData.R_Index3 != null && bodyData.R_Index3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Index3[1], bodyData.R_Index3[2], bodyData.R_Index3[3], smoothDuration);

        if (bodyData.R_Middle1 != null && bodyData.R_Middle1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Middle1[1], bodyData.R_Middle1[2], bodyData.R_Middle1[3], smoothDuration);

        if (bodyData.R_Middle2 != null && bodyData.R_Middle2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Middle2[1], bodyData.R_Middle2[2], bodyData.R_Middle2[3], smoothDuration);

        if (bodyData.R_Middle3 != null && bodyData.R_Middle3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Middle3[1], bodyData.R_Middle3[2], bodyData.R_Middle3[3], smoothDuration);

        if (bodyData.R_Ring1 != null && bodyData.R_Ring1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Ring1[1], bodyData.R_Ring1[2], bodyData.R_Ring1[3], smoothDuration);

        if (bodyData.R_Ring2 != null && bodyData.R_Ring2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Ring2[1], bodyData.R_Ring2[2], bodyData.R_Ring2[3], smoothDuration);

        if (bodyData.R_Ring3 != null && bodyData.R_Ring3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Ring3[1], bodyData.R_Ring3[2], bodyData.R_Ring3[3], smoothDuration);

        if (bodyData.R_Little1 != null && bodyData.R_Little1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Little1[1], bodyData.R_Little1[2], bodyData.R_Little1[3], smoothDuration);

        if (bodyData.R_Little2 != null && bodyData.R_Little2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Little2[1], bodyData.R_Little2[2], bodyData.R_Little2[3], smoothDuration);

        if (bodyData.R_Little3 != null && bodyData.R_Little3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Little3[1], bodyData.R_Little3[2], bodyData.R_Little3[3], smoothDuration);

        if (bodyData.R_Thumb1 != null && bodyData.R_Thumb1.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Thumb1[1], bodyData.R_Thumb1[2], bodyData.R_Thumb1[3], smoothDuration);

        if (bodyData.R_Thumb2 != null && bodyData.R_Thumb2.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Thumb2[1], bodyData.R_Thumb2[2], bodyData.R_Thumb2[3], smoothDuration);

        if (bodyData.R_Thumb3 != null && bodyData.R_Thumb3.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Thumb3[1], bodyData.R_Thumb3[2], bodyData.R_Thumb3[3], smoothDuration);

        // Left leg segments
        if (bodyData.L_UpperLeg != null && bodyData.L_UpperLeg.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_UpperLeg[1], bodyData.L_UpperLeg[2], bodyData.L_UpperLeg[3], smoothDuration);

        if (bodyData.L_LowerLeg != null && bodyData.L_LowerLeg.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_LowerLeg[1], bodyData.L_LowerLeg[2], bodyData.L_LowerLeg[3], smoothDuration);

        if (bodyData.L_Foot != null && bodyData.L_Foot.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_Foot[1], bodyData.L_Foot[2], bodyData.L_Foot[3], smoothDuration);

        if (bodyData.L_ToeBase != null && bodyData.L_ToeBase.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.L_ToeBase[1], bodyData.L_ToeBase[2], bodyData.L_ToeBase[3], smoothDuration);

        // Right leg segments
        if (bodyData.R_UpperLeg != null && bodyData.R_UpperLeg.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_UpperLeg[1], bodyData.R_UpperLeg[2], bodyData.R_UpperLeg[3], smoothDuration);

        if (bodyData.R_LowerLeg != null && bodyData.R_LowerLeg.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_LowerLeg[1], bodyData.R_LowerLeg[2], bodyData.R_LowerLeg[3], smoothDuration);

        if (bodyData.R_Foot != null && bodyData.R_Foot.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_Foot[1], bodyData.R_Foot[2], bodyData.R_Foot[3], smoothDuration);

        if (bodyData.R_ToeBase != null && bodyData.R_ToeBase.Length > f)
            moveArcher.SmoothRotate(jointIndex++, bodyData.R_ToeBase[1], bodyData.R_ToeBase[2], bodyData.R_ToeBase[3], smoothDuration);

        Debug.Log("[Body Rotation] All joints processed");

        // Wait for all smooth rotations to finish before returning
        yield return new WaitForSeconds(smoothDuration);
    }

}
