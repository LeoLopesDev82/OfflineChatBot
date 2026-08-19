# 🤖 OfflineChatBot

A desktop AI chat application built with C# and WPF to demonstrate local inference capabilities using the **Qwen 2.5** model family. The project runs entirely offline without relying on external cloud APIs, ensuring data privacy and local execution.

The core objective of this repository is to showcase software engineering practices, including MVVM architecture, asynchronous programming, thread safety, and integration with C++ bindings for local AI execution via [LLamaSharp](https://github.com/SciSharp/LLamaSharp).

## 🛠️ Technical Highlights

* **Local Inference Engine:** Executes `.gguf` quantized models locally.
* **Integrated Model Manager:** Includes an asynchronous download manager to fetch HuggingFace models directly from the UI, with proper stream handling and progress reporting.
* **Clean Architecture:** Built heavily upon SOLID principles and Single Responsibility. 
* **MVVM Pattern:** Strict separation of UI logic and business rules using `CommunityToolkit.Mvvm`.
* **Resource Management:** Safe handling of unmanaged C++ memory handles (llama.cpp) during model loading, unloading, and deletion.
* **WPF UI:** Features real-time Markdown rendering and syntax highlighting for code blocks.

## 🚀 Getting Started

### Prerequisites
* Windows OS
* .NET 9.0 SDK or later

### Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/LeoLopesDev82/OfflineChatBot.git
   ```
2. Open the solution in Visual Studio.
3. Build and Run the project.
4. On the first launch, open the **Model Manager** to download a Qwen 2.5 model (e.g., 0.5B or 1.5B) to begin chatting.

## 🗺️ Roadmap & Future Implementation

While the current text-based engine is functional, the architecture was designed to be extended. Upcoming milestones include:

- [ ] **Multimodal & Document Analysis:** Implementing Vision-Language Models (VLMs) and basic RAG (Retrieval-Augmented Generation). The goal is to allow users to drag-and-drop images or text documents into the chat for context-aware interactions.
- [ ] **Hardware Acceleration Options:** Adding explicit UI toggles for CUDA / Vulkan / Metal backends to test inference scaling on dedicated GPUs.

## ⚙️ Technology Stack
* **Language:** C# 13 / .NET 9.0
* **UI Framework:** WPF (Windows Presentation Foundation)
* **Design Pattern:** MVVM
* **AI Engine:** LLamaSharp (llama.cpp wrapper)

## 🤝 Contributing
Contributions, issues, and feature requests are welcome. Feel free to check the issues page.

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.
