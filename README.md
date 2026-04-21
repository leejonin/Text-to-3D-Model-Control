# Text-to-3D-Model-Control

EN🏹 Text-to-3D-Model-Control (Unity & OpenAI)

An experimental project that leverages OpenAI’s LLM (GPT) to control a 3D character model’s rig (joints) in real time within Unity.
The goal is to generate natural poses and movements from text commands.

🚀 How It Works

This project is designed as a state-based intelligent animation system.

1. Context Construction (StateAi.cs)

All current joint angles of the character, reference pose samples, and system prompts are combined and sent to the OpenAI API.

2. AI Reasoning

The GPT-4 model analyzes the received data and generates joint data for the next frame in JSON format:
[Frame, X, Y, Z]
based on the requested text command (e.g., "At-ease stance").

3. Data Processing

The JSON received in StateAi is deserialized and passed to the MoveArcher class.

4. Smooth Interpolation (MoveArcher.cs)

Using the target joint angles, the system applies smooth animation through:

Quaternion.Slerp
Smoothstep easing

to interpolate natural motion.

5. Anatomical Constraints

All movements are constrained within physically valid joint limits defined in the Joint class (Min/Max clamp), preventing unnatural deformation.

🛠 Key Features
Real-time AI Control
Communicates with the LLM every 5 seconds to continuously update the character’s state.
Full Body Rigging
Supports detailed control of:
Spine
Head
Arms & Legs
Individual finger joints (Index, Middle, Ring, Little, Thumb)
Adaptive Animation
Executes dynamically generated poses instead of pre-defined animation clips.
Safety Clamping
Prevents abnormal joint rotation using Mathf.Clamp.
🚧 Current Limitations & Roadmap
Lack of Data
More pose datasets are required to achieve higher fidelity and realism.
Model Optimization
Model Optimization: Optimization is required to reduce the current response latency of GPT-4.
Prompt Engineering: The prompt structure needs to be advanced so that the AI ​​can understand 3D coordinate space more accurately.

KR🏹 Text-to-3D-Model-Control (Unity & OpenAI)
OpenAI의 LLM(GPT)을 활용하여 Unity 내 3D 캐릭터 모델의 관절(Rig)을 실시간으로 제어하는 실험적 프로젝트입니다. 텍스트 명령을 통해 캐릭터의 자연스러운 포즈와 움직임을 생성하는 것을 목표로 합니다.

🚀 구동 방식 (How it Works)
본 프로젝트는 **'상태 기반 지능형 애니메이션 시스템'**으로 설계되었습니다.

Context Construction (StateAi.cs): 현재 캐릭터의 모든 관절 각도(Joint Angles)와 참조용 포즈 샘플, 시스템 프롬프트를 결합하여 OpenAI API로 전송합니다.

AI Reasoning: GPT-4 모델이 수신된 데이터를 분석하고, 요청된 텍스트(예: "At-ease stance")에 적합한 다음 프롬프트의 관절 데이터([Frame, X, Y, Z])를 JSON 형식으로 생성합니다.

Data Processing: StateAi에서 수신된 JSON을 역직렬화(Deserialization)하고 MoveArcher 클래스로 전달합니다.

Smooth Interpolation (MoveArcher.cs): 전달받은 목표 각도를 바탕으로 Quaternion.Slerp와 Smoothstep 이징(Easing)을 사용하여 캐릭터의 움직임을 부드럽게 보간하여 적용합니다.

Anatomical Constraints: 모든 움직임은 Joint 클래스에 정의된 물리적 회전 제한 범위(Min/Max Clamp) 내에서만 이루어져 인체공학적 오류를 방지합니다.

🛠 주요 기능 (Key Features)
Real-time AI Control: 5초 주기로 LLM과 통신하여 실시간으로 캐릭터의 상태를 업데이트합니다.

Full Body Rigging: 척추, 머리, 팔다리는 물론 손가락 마디(Index, Middle, Ring, Little, Thumb)까지 세밀한 제어가 가능합니다.

Adaptive Animation: 고정된 애니메이션 클립이 아닌, AI가 상황에 맞춰 생성한 동적 포즈를 실행합니다.

Safety Clamping: Mathf.Clamp를 통해 관절이 비정상적으로 꺾이는 현상을 방지합니다.

🚧 현재 한계 및 개선 예정 사항 (Current Limitations & Roadmap)
데이터 부족: 더욱 정교한 동작 구현을 위해 더 많은 포즈 데이터셋(Dataset) 학습이 필요합니다.

모델 최적화: 현재 GPT-4 기반의 응답 대기 시간(Latency)을 줄이기 위한 최적화 작업이 필요합니다.

프롬프트 엔지니어링: AI가 3D 좌표 공간을 더 정확히 이해할 수 있도록 프롬프트 구조를 고도화하가 필요합니다
